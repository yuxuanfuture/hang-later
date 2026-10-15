using System.Text.Json;
using HangLater.Windows.Models;
using HangLater.Windows.Services;

var root = Path.Combine(Path.GetTempPath(), "HangLater-Windows-Checks-" + Guid.NewGuid().ToString("N"));
try
{
    var repository = new TaskRepository(root);
    var idea = repository.Add("A better research flow", note: "An idea", kind: HangingTaskKind.idea);
    Check(idea.lineX is >= 0 and <= 1, "new tasks find an open clothesline position");
    var meeting = repository.Add("Weekly meeting", url: "https://example.zoom.us/j/123456", dueAt: DateTimeOffset.Now.AddHours(1), kind: HangingTaskKind.meeting);
    Check(repository.Tasks.Count == 2, "tasks persist in memory");
    repository.Complete(idea.id);
    Check(repository.Completed.Single().id == idea.id && repository.Completed.Single().IsOnSpike, "completion puts a task on the spike");
    repository.ResizeSpike(idea.id, 9);
    Check(repository.Completed.Single().EffectiveSpikeScale == 1.5, "spike scale is clamped");
    repository.Restore(idea.id);
    Check(repository.Pending.Count() == 2, "completed tasks can be restored");

    repository.ReplaceTaskTypes([new TaskTypeDefinition { name = "Research", colorHex = 0x2F87E1 }]);
    Check(repository.TaskTypes.Single().colorHex == 0x2F87E1, "task types persist");
    var reloaded = new TaskRepository(root);
    Check(reloaded.Tasks.Count == 2 && reloaded.TaskTypes.Count == 1, "repository reloads JSON");
    using var document = JsonDocument.Parse(File.ReadAllText(reloaded.TaskFile));
    Check(document.RootElement[0].GetProperty("createdAt").ValueKind == JsonValueKind.Number, "dates remain Swift JSON compatible");

    var schedule = new WorkdaySchedule(15 * 60, 6 * 60, true);
    var atThree = new DateTimeOffset(2026, 10, 16, 3, 0, 0, TimeSpan.Zero);
    var interval = schedule.IntervalContaining(atThree);
    Check(interval?.Start.Hour == 15 && interval?.Start.Day == 15 && interval?.End.Hour == 6 && interval?.End.Day == 16, "overnight workday uses the previous start day");

    Check(EdgeActivationPolicy.FixedHeightPixels == 3 && EdgeActivationPolicy.FixedHoverMilliseconds == 500, "edge geometry and dwell time are fixed");
    Check(EdgeActivationPolicy.Contains(450, 0, 0, 0, 900, EdgeActivationZone.CenterThird), "center-third edge zone accepts its center");
    Check(!EdgeActivationPolicy.Contains(10, 0, 0, 0, 900, EdgeActivationZone.CenterThird), "center-third edge zone rejects the left side");
    Check(!EdgeActivationPolicy.Contains(450, 3, 0, 0, 900, EdgeActivationZone.FullWidth), "edge zone is exactly three pixels high");

    var settings = new SettingsRepository(root);
    settings.Save(new AppSettings { EdgeHoverMilliseconds = 12, EdgeHeightPixels = 90, GlobalHotKey = "Ctrl+Alt+L" });
    Check(settings.Value.EdgeHoverMilliseconds == 500 && settings.Value.EdgeHeightPixels == 3, "settings cannot weaken accidental-trigger protection");
    Console.WriteLine("Hang Later Windows: 14 checks passed.");
}
finally { if (Directory.Exists(root)) Directory.Delete(root, true); }

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception("Check failed: " + message);
    Console.WriteLine("✓ " + message);
}
