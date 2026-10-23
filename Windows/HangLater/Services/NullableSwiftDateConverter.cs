using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HangLater.Windows.Services;

internal sealed class NullableSwiftDateConverter : JsonConverter<DateTimeOffset?>
{
	private readonly SwiftDateConverter inner = new SwiftDateConverter();

	public override DateTimeOffset? Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
	{
		if (reader.TokenType != JsonTokenType.Null)
		{
			return inner.Read(ref reader, typeof(DateTimeOffset), options);
		}
		return null;
	}

	public override void Write(Utf8JsonWriter writer, DateTimeOffset? value, JsonSerializerOptions options)
	{
		if (!value.HasValue)
		{
			writer.WriteNullValue();
		}
		else
		{
			inner.Write(writer, value.Value, options);
		}
	}
}
