using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using HangLater.Windows.Models;

namespace HangLater.Windows.Services;

/// <summary>Resolve only document-path metadata; never read Markdown contents or scan the disk.</summary>
public static class TyporaResumeService
{
    public static bool IsTypora(string? processName) => string.Equals(processName,"Typora",StringComparison.OrdinalIgnoreCase) ||
        string.Equals(processName,"Typora.exe",StringComparison.OrdinalIgnoreCase);

    public static bool HasDocumentTitle(string? title) => DocumentTitle(title) is string name && IsMarkdown(name);

    public static string? ExistingDocument(string? path)
    {
        try
        {
            // No relative paths, UNC/network authentication, URLs, alternate data streams or executable arguments.
            if (string.IsNullOrWhiteSpace(path) || path.Length<3 || !char.IsAsciiLetter(path[0]) || path[1]!=':' ||
                (path[2]!='\\' && path[2]!='/') || path.IndexOf(':',2)>=0 || !IsMarkdown(path)) return null;
            string full = Path.GetFullPath(path);
            return File.Exists(full) ? full : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException) { return null; }
    }

    public static string? TryResolve(string? processName, string? windowTitle, string? historyFile = null)
    {
        if (!IsTypora(processName) || DocumentTitle(windowTitle) is not string name || !IsMarkdown(name)) return null;
        string? absolute = ExistingDocument(name);
        if (absolute != null) return absolute;
        // A fully qualified title is authoritative, even when its document was removed.
        if (Path.IsPathFullyQualified(name)) return null;
        historyFile ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"Typora","history.data");
        try
        {
            if (!File.Exists(historyFile) || new FileInfo(historyFile).Length>2*1024*1024) return null;
            string data = File.ReadAllText(historyFile).Trim().TrimStart('\uFEFF');
            // Typora 1.9 stores this JSON as UTF-8 hexadecimal; other versions use plain JSON.
            if (!data.StartsWith("{",StringComparison.Ordinal)) data = new UTF8Encoding(false,true).GetString(Convert.FromHexString(data));
            using JsonDocument history = JsonDocument.Parse(data);
            if (!history.RootElement.TryGetProperty("recentDocument",out var entries) || entries.ValueKind!=JsonValueKind.Array) return null;
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in entries.EnumerateArray())
            {
                if (entry.ValueKind!=JsonValueKind.Object || !entry.TryGetProperty("path",out var value) || value.ValueKind!=JsonValueKind.String) continue;
                string? path = value.GetString();
                if (path == null || !Path.IsPathFullyQualified(path) || !string.Equals(Path.GetFileName(path),name,StringComparison.OrdinalIgnoreCase)) continue;
                // Count missing historical matches too: never silently redirect a deleted receipt to a namesake.
                paths.Add(Path.GetFullPath(path));
                if (paths.Count>1) return null;
            }
            foreach (string path in paths) return ExistingDocument(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException or
            NotSupportedException or InvalidOperationException or FormatException) { }
        return null;
    }

    public static ProcessStartInfo? StartInfo(HangingTask task, string? historyFile = null)
    {
        string? process = task.capturedProcessName ?? task.appName;
        if (!IsTypora(process)) return null;
        string? saved = string.IsNullOrWhiteSpace(task.capturedDocumentPath) ? task.filePath : task.capturedDocumentPath;
        string? path = string.IsNullOrWhiteSpace(saved) ? TryResolve(process,task.capturedWindowTitle ?? task.title,historyFile) : ExistingDocument(saved);
        if (path == null)
        {
            if (!string.IsNullOrWhiteSpace(saved)) throw new FileNotFoundException("Markdown 文档不存在或路径无效，请在便签的“编辑”中更新完整文档路径。",saved);
            if (HasDocumentTitle(task.capturedWindowTitle ?? task.title))
                throw new InvalidOperationException("此便签没有唯一的 Markdown 文档路径。请在“编辑”中填写完整路径，以免跳转到同名的其他文档。");
            return null; // Unsaved/untitled windows have no on-disk document to reopen.
        }
        var start = ApplicationLaunchService.StartInfo(HangingTaskTarget.FromTask(task)) ??
            throw new FileNotFoundException("找不到 Typora 程序，请检查其安装位置。",task.appBundleIdentifier);
        start.ArgumentList.Add(path); // One quoted argument, independent of Windows' default .md editor.
        return start;
    }

    private static bool IsMarkdown(string path) => Path.GetExtension(path).ToLowerInvariant() is ".md" or ".markdown" or ".mdown" or ".mkd" or ".mkdn" or ".mdtext";
    private static string? DocumentTitle(string? title)
    {
        const string suffix = " - Typora";
        if (string.IsNullOrWhiteSpace(title) || !title.EndsWith(suffix,StringComparison.OrdinalIgnoreCase)) return null;
        return title[..^suffix.Length].Trim().Trim('*').Trim();
    }
}
