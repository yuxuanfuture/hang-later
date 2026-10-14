using System.Text.Json;
using HangLater.Windows.Models;

namespace HangLater.Windows.Services;

public sealed class TaskRepository
{
    public string RootDirectory { get; }
    public string PreviewDirectory { get; }
    public string InboxDirectory { get; }
    public string TaskFile => Path.Combine(RootDirectory, "tasks.json");
    public string TypeFile => Path.Combine(RootDirectory, "task-types.json");
    public IReadOnlyList<HangingTask> Tasks => tasks;
    public IReadOnlyList<TaskTypeDefinition> TaskTypes => taskTypes;
    private List<HangingTask> tasks = [];
    private List<TaskTypeDefinition> taskTypes = [];

    public TaskRepository(string? root = null)
    {
        RootDirectory = root ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HangLater");
        PreviewDirectory = Path.Combine(RootDirectory, "Previews");
        InboxDirectory = Path.Combine(RootDirectory, "BrowserBridge", "Inbox");
        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(PreviewDirectory);
        Directory.CreateDirectory(InboxDirectory);
        tasks = ReadOrThrow<List<HangingTask>>(TaskFile) ?? [];
        taskTypes = ReadOrThrow<List<TaskTypeDefinition>>(TypeFile) ?? [];
    }

    public IEnumerable<HangingTask> Pending => tasks.Where(x => !x.IsCompleted);
    public IEnumerable<HangingTask> Completed => tasks.Where(x => x.IsCompleted).OrderByDescending(x => x.completedAt);

    public HangingTask Add(string title, string? url = null, string note = "", DateTimeOffset? dueAt = null,
        string? previewFile = null, string? filePath = null, string? appName = null, string? resumeUrl = null,
        HangingTaskKind? kind = null)
    {
        title = title.Trim();
        if (title.Length == 0) throw new InvalidOperationException("Please name this task.");
        if (url is not null && !IsWebUrl(url)) throw new InvalidOperationException("Use a complete http or https link.");
        var task = new HangingTask
        {
            title = title, url = url, note = note, dueAt = dueAt, previewFile = previewFile,
            filePath = filePath, appName = appName, resumeURL = resumeUrl, kind = kind,
            tint = tasks.Count % 4, lineX = FindOpenPosition()
        };
        tasks.Insert(0, task); SaveTasks(); return task;
    }

    public void Update(HangingTask task)
    {
        if (string.IsNullOrWhiteSpace(task.title)) throw new InvalidOperationException("Please name this task.");
        var index = tasks.FindIndex(x => x.id == task.id);
        if (index < 0) throw new InvalidOperationException("This task no longer exists.");
        tasks[index] = task; SaveTasks();
    }

    public void Complete(Guid id, DateTimeOffset? at = null)
    {
        var task = Find(id); task.completedAt = at ?? DateTimeOffset.Now; task.spikeAttached = true; SaveTasks();
    }
    public void CompleteAll(IEnumerable<Guid> ids)
    {
        var set = ids.ToHashSet(); var now = DateTimeOffset.Now;
        foreach (var task in tasks.Where(x => set.Contains(x.id) && !x.IsCompleted)) { task.completedAt = now; task.spikeAttached = true; }
        SaveTasks();
    }
    public void Restore(Guid id) { var task = Find(id); task.completedAt = null; task.spikeAttached = null; SaveTasks(); }
    public void Delete(Guid id) { tasks.RemoveAll(x => x.id == id); SaveTasks(); }
    public void DeleteAll(IEnumerable<Guid> ids) { var set = ids.ToHashSet(); tasks.RemoveAll(x => set.Contains(x.id)); SaveTasks(); }
    public void SetSpike(Guid id, bool attached) { var task = Find(id); task.spikeAttached = attached; SaveTasks(); }
    public void ResizeSpike(Guid id, double scale) { var task = Find(id); task.spikeScale = Math.Clamp(scale, .6, 1.5); SaveTasks(); }
    public void Place(Guid id, double x)
    {
        var task = Find(id); task.lineX = Math.Clamp(x, 0, 1); task.lineLayer = tasks.Max(x => x.lineLayer ?? 0) + 1; SaveTasks();
    }

    public void ReplaceTaskTypes(IEnumerable<TaskTypeDefinition> values)
    {
        var cleaned = values.Select(x => new TaskTypeDefinition { id = x.id, name = x.name.Trim(), colorHex = x.colorHex & 0xFFFFFF }).ToList();
        if (cleaned.Any(x => x.name.Length == 0)) throw new InvalidOperationException("Name every task type.");
        if (cleaned.GroupBy(x => x.name, StringComparer.CurrentCultureIgnoreCase).Any(x => x.Count() > 1))
            throw new InvalidOperationException("Task type names must be unique.");
        taskTypes = cleaned;
        var valid = cleaned.Select(x => x.id).ToHashSet();
        foreach (var task in tasks.Where(x => x.taskTypeID is not null && !valid.Contains(x.taskTypeID.Value))) task.taskTypeID = null;
        AtomicWrite(TypeFile, JsonSerializer.Serialize(taskTypes, JsonSupport.Options)); SaveTasks();
    }

    public async Task<int> ImportBrowserInboxAsync()
    {
        var imported = 0;
        foreach (var file in Directory.EnumerateFiles(InboxDirectory, "*.json").OrderBy(x => x).ToArray())
        {
            try
            {
                using var document = JsonDocument.Parse(await File.ReadAllTextAsync(file));
                var root = document.RootElement;
                var url = root.GetProperty("url").GetString()!;
                var title = root.TryGetProperty("title", out var titleValue) ? titleValue.GetString() ?? new Uri(url).Host : new Uri(url).Host;
                string? previewFile = null;
                if (root.TryGetProperty("screenshot", out var shot) && shot.ValueKind == JsonValueKind.String)
                {
                    var bytes = Convert.FromBase64String(shot.GetString()!);
                    previewFile = $"browser-{Guid.NewGuid():N}.png";
                    await File.WriteAllBytesAsync(Path.Combine(PreviewDirectory, previewFile), bytes);
                }
                Add(title, url: url, previewFile: previewFile, resumeUrl: url);
                File.Delete(file); imported++;
            }
            catch { /* Preserve invalid messages for manual inspection. */ }
        }
        return imported;
    }

    public static bool IsWebUrl(string value) => Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) && !string.IsNullOrWhiteSpace(uri.Host)
        && string.IsNullOrEmpty(uri.UserInfo);

    private HangingTask Find(Guid id) => tasks.FirstOrDefault(x => x.id == id) ?? throw new InvalidOperationException("This task no longer exists.");
    private double FindOpenPosition()
    {
        var occupied = Pending.Select(x => x.lineX ?? .5).ToArray();
        var candidates = Enumerable.Range(0, 19).Select(x => .05 + x * .05);
        return candidates.OrderByDescending(x => occupied.Length == 0 ? 1 : occupied.Min(y => Math.Abs(x - y))).First();
    }
    private static T? ReadOrThrow<T>(string path)
    {
        if (!File.Exists(path)) return default;
        try { return JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonSupport.Options); }
        catch (Exception ex) { throw new InvalidDataException($"Could not read {Path.GetFileName(path)}. The original file was preserved.", ex); }
    }
    private void SaveTasks() => AtomicWrite(TaskFile, JsonSerializer.Serialize(tasks, JsonSupport.Options));
    private static void AtomicWrite(string path, string contents)
    {
        var temp = path + ".tmp-" + Guid.NewGuid().ToString("N");
        File.WriteAllText(temp, contents);
        File.Move(temp, path, true);
    }
}
