using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using HangLater.Windows.Models;

namespace HangLater.Windows.Services;

public sealed class TaskRepository
{
	private List<HangingTask> tasks = new List<HangingTask>();

	private List<TaskTypeDefinition> taskTypes = new List<TaskTypeDefinition>();

	public string RootDirectory { get; }

	public string PreviewDirectory { get; }

	public string InboxDirectory { get; }

	public string TaskFile => Path.Combine(RootDirectory, "tasks.json");

	public string TypeFile => Path.Combine(RootDirectory, "task-types.json");

	public IReadOnlyList<HangingTask> Tasks => tasks;

	public IReadOnlyList<TaskTypeDefinition> TaskTypes => taskTypes;

	public IEnumerable<HangingTask> Pending => tasks.Where((HangingTask x) => !x.IsCompleted);

	public IEnumerable<HangingTask> Completed => from x in tasks
		where x.IsCompleted
		orderby x.completedAt descending
		select x;

	public TaskRepository(string? root = null)
	{
		RootDirectory = root ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HangLater");
		PreviewDirectory = Path.Combine(RootDirectory, "Previews");
		InboxDirectory = Path.Combine(RootDirectory, "BrowserBridge", "Inbox");
		Directory.CreateDirectory(RootDirectory);
		Directory.CreateDirectory(PreviewDirectory);
		Directory.CreateDirectory(InboxDirectory);
		tasks = ReadOrThrow<List<HangingTask>>(TaskFile) ?? new List<HangingTask>();
		taskTypes = ReadOrThrow<List<TaskTypeDefinition>>(TypeFile) ?? new List<TaskTypeDefinition>();
	}

	public HangingTask Add(string title, string? url = null, string note = "", DateTimeOffset? dueAt = null, string? previewFile = null, string? filePath = null, string? appName = null, string? resumeUrl = null, HangingTaskKind? kind = null, Guid? taskTypeID = null)
	{
		title = title.Trim();
		if (title.Length == 0)
		{
			throw new InvalidOperationException("Please name this task.");
		}
		if (url != null && !IsWebUrl(url))
		{
			throw new InvalidOperationException("Use a complete http or https link.");
		}
		HangingTask hangingTask = new HangingTask
		{
			title = title,
			url = url,
			note = note,
			dueAt = dueAt,
			previewFile = previewFile,
			filePath = filePath,
			appName = appName,
			resumeURL = resumeUrl,
			kind = kind,
			taskTypeID = taskTypeID,
			tint = tasks.Count % 4,
			lineX = FindOpenPosition()
		};
		tasks.Insert(0, hangingTask);
		SaveTasks();
		return hangingTask;
	}

	public void Update(HangingTask task)
	{
		if (string.IsNullOrWhiteSpace(task.title))
		{
			throw new InvalidOperationException("Please name this task.");
		}
		int num = tasks.FindIndex((HangingTask x) => x.id == task.id);
		if (num < 0)
		{
			throw new InvalidOperationException("This task no longer exists.");
		}
		tasks[num] = task;
		SaveTasks();
	}

	public void Complete(Guid id, DateTimeOffset? at = null)
	{
		HangingTask hangingTask = Find(id);
		hangingTask.completedAt = at ?? DateTimeOffset.Now;
		hangingTask.spikeAttached = true;
		SaveTasks();
	}

	public void CompleteAll(IEnumerable<Guid> ids)
	{
		HashSet<Guid> set = ids.ToHashSet();
		DateTimeOffset now = DateTimeOffset.Now;
		foreach (HangingTask item in tasks.Where((HangingTask x) => set.Contains(x.id) && !x.IsCompleted))
		{
			item.completedAt = now;
			item.spikeAttached = true;
		}
		SaveTasks();
	}

	public void Restore(Guid id)
	{
		HangingTask hangingTask = Find(id);
		hangingTask.completedAt = null;
		hangingTask.spikeAttached = null;
		SaveTasks();
	}

	public void Delete(Guid id)
	{
		tasks.RemoveAll((HangingTask x) => x.id == id);
		SaveTasks();
	}

	public void DeleteAll(IEnumerable<Guid> ids)
	{
		HashSet<Guid> set = ids.ToHashSet();
		tasks.RemoveAll((HangingTask x) => set.Contains(x.id));
		SaveTasks();
	}

	public void SetSpike(Guid id, bool attached)
	{
		Find(id).spikeAttached = attached;
		SaveTasks();
	}

	public void ResizeSpike(Guid id, double scale)
	{
		Find(id).spikeScale = Math.Clamp(scale, 0.6, 1.5);
		SaveTasks();
	}

	public void MoveSpike(Guid id, double x, double y, double z)
	{
		HangingTask hangingTask = Find(id);
		hangingTask.spikeX = Math.Clamp(x, -0.95, 0.95);
		hangingTask.spikeY = Math.Clamp(y, 0.72, 3.05);
		hangingTask.spikeZ = Math.Clamp(z, -0.38, 0.38);
		hangingTask.spikePositionVersion = 1;
		SaveTasks();
	}

	public void Place(Guid id, double x)
	{
		HangingTask hangingTask = Find(id);
		hangingTask.lineX = Math.Clamp(x, 0.0, 1.0);
		hangingTask.lineLayer = tasks.Max((HangingTask hangingTask2) => hangingTask2.lineLayer.GetValueOrDefault()) + 1;
		SaveTasks();
	}

	public void ReplaceTaskTypes(IEnumerable<TaskTypeDefinition> values)
	{
		List<TaskTypeDefinition> source = values.Select((TaskTypeDefinition x) => new TaskTypeDefinition
		{
			id = x.id,
			name = x.name.Trim(),
			colorHex = (x.colorHex & 0xFFFFFF)
		}).ToList();
		if (source.Any((TaskTypeDefinition x) => x.name.Length == 0))
		{
			throw new InvalidOperationException("Name every task type.");
		}
		if (source.GroupBy<TaskTypeDefinition, string>((TaskTypeDefinition x) => x.name, StringComparer.CurrentCultureIgnoreCase).Any((IGrouping<string, TaskTypeDefinition> x) => x.Count() > 1))
		{
			throw new InvalidOperationException("Task type names must be unique.");
		}
		taskTypes = source;
		HashSet<Guid> valid = source.Select((TaskTypeDefinition x) => x.id).ToHashSet();
		foreach (HangingTask item in tasks.Where((HangingTask x) => x.taskTypeID.HasValue && !valid.Contains(x.taskTypeID.Value)))
		{
			item.taskTypeID = null;
		}
		AtomicWrite(TypeFile, JsonSerializer.Serialize(taskTypes, JsonSupport.Options));
		SaveTasks();
	}

	public async Task<int> ImportBrowserInboxAsync()
	{
		int imported = 0;
		string[] array = (from x in Directory.EnumerateFiles(InboxDirectory, "*.json")
			orderby x
			select x).ToArray();
		foreach (string file in array)
		{
			try
			{
				using JsonDocument document = JsonDocument.Parse(await File.ReadAllTextAsync(file));
				JsonElement rootElement = document.RootElement;
				string url = rootElement.GetProperty("url").GetString();
				JsonElement value;
				string title = (rootElement.TryGetProperty("title", out value) ? (value.GetString() ?? new Uri(url).Host) : new Uri(url).Host);
				string previewFile = null;
				if (rootElement.TryGetProperty("screenshot", out var value2) && value2.ValueKind == JsonValueKind.String)
				{
					byte[] bytes = Convert.FromBase64String(value2.GetString());
					previewFile = $"browser-{Guid.NewGuid():N}.png";
					await File.WriteAllBytesAsync(Path.Combine(PreviewDirectory, previewFile), bytes);
				}
				Add(title, url, "", null, previewFile, null, null, url);
				File.Delete(file);
				imported++;
			}
			catch
			{
			}
		}
		return imported;
	}

	public static bool IsWebUrl(string value)
	{
		if (Uri.TryCreate(value.Trim(), UriKind.Absolute, out Uri result) && (result.Scheme == Uri.UriSchemeHttp || result.Scheme == Uri.UriSchemeHttps) && !string.IsNullOrWhiteSpace(result.Host))
		{
			return string.IsNullOrEmpty(result.UserInfo);
		}
		return false;
	}

	private HangingTask Find(Guid id)
	{
		return tasks.FirstOrDefault((HangingTask x) => x.id == id) ?? throw new InvalidOperationException("This task no longer exists.");
	}

	private double FindOpenPosition()
	{
		double[] occupied = Pending.Select((HangingTask x) => x.lineX ?? 0.5).ToArray();
		return (from x in Enumerable.Range(0, 19)
			select 0.05 + (double)x * 0.05 into x
			orderby (occupied.Length != 0) ? occupied.Min((double y) => Math.Abs(x - y)) : 1.0 descending
			select x).First();
	}

	private static T? ReadOrThrow<T>(string path)
	{
		if (!File.Exists(path))
		{
			return default(T);
		}
		try
		{
			return JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonSupport.Options);
		}
		catch (Exception innerException)
		{
			throw new InvalidDataException("Could not read " + Path.GetFileName(path) + ". The original file was preserved.", innerException);
		}
	}

	private void SaveTasks()
	{
		AtomicWrite(TaskFile, JsonSerializer.Serialize(tasks, JsonSupport.Options));
	}

	private static void AtomicWrite(string path, string contents)
	{
		string text = path + ".tmp-" + Guid.NewGuid().ToString("N");
		File.WriteAllText(text, contents);
		File.Move(text, path, overwrite: true);
	}
}
