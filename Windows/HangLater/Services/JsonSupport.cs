using System.Text.Json;
using System.Text.Json.Serialization;

namespace HangLater.Windows.Services;

internal static class JsonSupport
{
	public static readonly JsonSerializerOptions Options = Create();

	private static JsonSerializerOptions Create()
	{
		return new JsonSerializerOptions
		{
			WriteIndented = true,
			PropertyNameCaseInsensitive = true,
			DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
			Converters =
			{
				(JsonConverter)new SwiftDateConverter(),
				(JsonConverter)new NullableSwiftDateConverter(),
				(JsonConverter)new JsonStringEnumConverter()
			}
		};
	}
}
