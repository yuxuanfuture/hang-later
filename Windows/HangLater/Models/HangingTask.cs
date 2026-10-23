using System;
using System.IO;
using System.Text.Json.Serialization;

namespace HangLater.Windows.Models;

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

	// A Windows capture must remember the window, not just the application's executable.
	public string? capturedWindowTitle { get; set; }
	public string? capturedProcessName { get; set; }
	public string? capturedLaunchExecutablePath { get; set; }
	public string? capturedDocumentPath { get; set; }
	public long? capturedWindowHandle { get; set; }
	public int? capturedProcessId { get; set; }
	public long? capturedProcessStartUtcTicks { get; set; }

	public HangingTaskKind? kind { get; set; }

	public Guid? taskTypeID { get; set; }

	public double? lineX { get; set; }

	public int? lineLayer { get; set; }

	public bool? spikeAttached { get; set; }

	public double? spikeX { get; set; }

	public double? spikeY { get; set; }

	public double? spikeZ { get; set; }

	public int? spikePositionVersion { get; set; }

	public double? spikeScale { get; set; }

	[JsonIgnore]
	public bool IsCompleted => completedAt.HasValue;

	[JsonIgnore]
	public bool IsOnSpike
	{
		get
		{
			if (IsCompleted)
			{
				return spikeAttached != false;
			}
			return false;
		}
	}

	[JsonIgnore]
	public double EffectiveSpikeScale => Math.Clamp(spikeScale ?? 1.0, 0.6, 1.5);

	[JsonIgnore]
	public string SourceLabel
	{
		get
		{
			string text;
			if (filePath == null)
			{
				if (Uri.TryCreate(url, UriKind.Absolute, out Uri result))
				{
					return result.Host.Replace("www.", "");
				}
				if (kind == HangingTaskKind.idea)
				{
					return "AN IDEA";
				}
				text = appName;
				if (text == null)
				{
					return "A SMALL THING";
				}
			}
			else
			{
				text = Path.GetExtension(filePath).TrimStart('.').ToUpperInvariant();
			}
			return text;
		}
	}

	public HangingTask Clone()
	{
		return (HangingTask)MemberwiseClone();
	}
}
