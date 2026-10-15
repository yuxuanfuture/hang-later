using System.Text.Json.Serialization;

namespace HangLater.Windows.Models;

[JsonConverter(typeof(JsonStringEnumConverter<HangingTaskKind>))]
public enum HangingTaskKind { idea, reminder, meeting }

public sealed class HangingTask
{
    public Guid id { get; set; } = Guid.NewGuid();
    public string title { get; set; } = "";
    public string? url { get; set; }
    public string note { get; set; } = "";
    public DateTimeOffset createdAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset? completedAt { get; set; }
    public DateTimeOffset? dueAt { get; set; }
    public string? previewFile { get; set; }
    public int tint { get; set; }
    public string? captureID { get; set; }
    public string? filePath { get; set; }
    public string? appBundleIdentifier { get; set; }
    public string? appName { get; set; }
    public string? resumeURL { get; set; }
    public HangingTaskKind? kind { get; set; }
    public Guid? taskTypeID { get; set; }
    public double? lineX { get; set; }
    public int? lineLayer { get; set; }
    public bool? spikeAttached { get; set; }
    public double? spikeX { get; set; }
    public double? spikeY { get; set; }
    public double? spikeZ { get; set; }
    public double? spikeScale { get; set; }

    [JsonIgnore] public bool IsCompleted => completedAt is not null;
    [JsonIgnore] public bool IsOnSpike => IsCompleted && spikeAttached != false;
    [JsonIgnore] public double EffectiveSpikeScale => Math.Clamp(spikeScale ?? 1, .6, 1.5);
    [JsonIgnore] public string SourceLabel => filePath is not null
        ? Path.GetExtension(filePath).TrimStart('.').ToUpperInvariant()
        : Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host.Replace("www.", "")
        : kind == HangingTaskKind.idea ? "AN IDEA" : appName ?? "A SMALL THING";

    public HangingTask Clone() => (HangingTask)MemberwiseClone();
}

public sealed class TaskTypeDefinition
{
    public Guid id { get; set; } = Guid.NewGuid();
    public string name { get; set; } = "";
    public uint colorHex { get; set; }
    public TaskTypeDefinition Clone() => new() { id = id, name = name, colorHex = colorHex };
}

public enum CompletionGrouping { Day, Week, Month }
