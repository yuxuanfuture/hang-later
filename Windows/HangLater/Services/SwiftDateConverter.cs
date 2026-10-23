using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HangLater.Windows.Services;

internal sealed class SwiftDateConverter : JsonConverter<DateTimeOffset>
{
	private static readonly DateTimeOffset Epoch = new DateTimeOffset(2001, 1, 1, 0, 0, 0, TimeSpan.Zero);

	public override DateTimeOffset Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
	{
		if (reader.TokenType == JsonTokenType.Number)
		{
			return Epoch.AddSeconds(reader.GetDouble());
		}
		if (reader.TokenType == JsonTokenType.String && DateTimeOffset.TryParse(reader.GetString(), out var result))
		{
			return result;
		}
		throw new JsonException("Invalid date value");
	}

	public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
	{
		writer.WriteNumberValue((value.ToUniversalTime() - Epoch).TotalSeconds);
	}
}
