using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace HangLater.Windows.Services;

/// <summary>Read only Obsidian's vault/file names. Never read note content or alter its workspace.</summary>
public static class ObsidianResumeService
{
    public static string? TryResolve(string? processName, string? windowTitle, string? configurationFile = null, bool capturingCurrentWindow = false)
    {
        if (!string.Equals(processName, "Obsidian", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(windowTitle)) return null;
        configurationFile ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "obsidian", "obsidian.json");
        try
        {
            using JsonDocument config = JsonDocument.Parse(File.ReadAllText(configurationFile));
            if (!config.RootElement.TryGetProperty("vaults", out JsonElement vaults)) return null;
            var matches = new List<(string Id, string Root, string Note)>();
            foreach (JsonProperty vault in vaults.EnumerateObject())
            {
                if (!vault.Value.TryGetProperty("path", out JsonElement pathValue) || pathValue.GetString() is not string root || !Directory.Exists(root)) continue;
                string marker = " - " + Path.GetFileName(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) + " - Obsidian";
                int split = windowTitle.LastIndexOf(marker, StringComparison.OrdinalIgnoreCase);
                if (split < 0) continue;
                string suffix = windowTitle[(split + marker.Length)..];
                if (suffix.Length > 0 && !char.IsWhiteSpace(suffix[0])) continue;
                matches.Add((vault.Name, Path.GetFullPath(root), windowTitle[..split].Trim().TrimEnd('*').TrimEnd()));
            }
            // Two equally named vaults must not silently send a receipt to an arbitrary document.
            if (matches.Count != 1) return null;
            var match = matches[0];
            if (match.Note is "New tab" or "新标签页" or "新建标签页" || match.Note.Length == 0)
                return "obsidian://open?vault=" + Uri.EscapeDataString(match.Id);

            // Workspace paths disambiguate duplicate basenames only when the captured title agrees.
            string? active = capturingCurrentWindow ? ActiveFile(match.Root) : null;
            if (active != null && MatchesTitle(active, match.Note) && SafeExistingPath(match.Root, active) is string activePath)
                return OpenPath(activePath);

            var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint | FileAttributes.Hidden | FileAttributes.System };
            string? found = null;
            int examined = 0;
            foreach (string file in Directory.EnumerateFiles(match.Root, "*", options))
            {
                if (++examined > 50000) return null;
                if (!MatchesTitle(file, match.Note)) continue;
                if (found != null) return null;
                found = file;
            }
            return found == null ? null : OpenPath(found);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException or NotSupportedException or InvalidOperationException)
        {
            return null;
        }
    }

    public static bool IsOpenUri(string? value) => Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) &&
        uri.Scheme.Equals("obsidian", StringComparison.OrdinalIgnoreCase) && uri.Host.Equals("open", StringComparison.OrdinalIgnoreCase);

    private static string OpenPath(string path) => "obsidian://open?path=" + Uri.EscapeDataString(Path.GetFullPath(path));

    private static bool MatchesTitle(string file, string note) =>
        string.Equals(Path.GetFileName(file), note, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Path.GetFileNameWithoutExtension(file), note, StringComparison.OrdinalIgnoreCase);

    private static string? ActiveFile(string root)
    {
        try
        {
            using JsonDocument workspace = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, ".obsidian", "workspace.json")));
            if (!workspace.RootElement.TryGetProperty("active", out JsonElement active)) return null;
            string? id = active.GetString();
            return id == null ? null : FindActiveFile(workspace.RootElement, id);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException) { return null; }
    }

    private static string? FindActiveFile(JsonElement node, string id)
    {
        if (node.ValueKind == JsonValueKind.Object)
        {
            if (node.TryGetProperty("id", out JsonElement nodeId) && nodeId.GetString() == id &&
                node.TryGetProperty("state", out JsonElement state) && state.TryGetProperty("state", out JsonElement details) &&
                details.TryGetProperty("file", out JsonElement file)) return file.GetString();
            foreach (JsonProperty property in node.EnumerateObject())
                if (FindActiveFile(property.Value, id) is string found) return found;
        }
        else if (node.ValueKind == JsonValueKind.Array)
            foreach (JsonElement child in node.EnumerateArray())
                if (FindActiveFile(child, id) is string found) return found;
        return null;
    }

    private static string? SafeExistingPath(string root, string relative)
    {
        string path = Path.GetFullPath(Path.Combine(root, relative));
        return path.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && File.Exists(path) ? path : null;
    }
}
