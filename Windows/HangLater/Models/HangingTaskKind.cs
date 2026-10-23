using System.Text.Json.Serialization;

namespace HangLater.Windows.Models;

[JsonConverter(typeof(JsonStringEnumConverter<HangingTaskKind>))]
public enum HangingTaskKind
{
	idea,
	reminder,
	meeting
}
