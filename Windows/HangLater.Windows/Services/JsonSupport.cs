using System.Text.Json;
using System.Text.Json.Serialization;

namespace HangLater.Windows.Services;

internal static class JsonSupport
{
    public static readonly JsonSerializerOptions Options = Create();
    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        options.Converters.Add(new SwiftDateConverter());
        options.Converters.Add(new NullableSwiftDateConverter());
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}

/// Swift Foundation's default JSON date representation is seconds since 2001-01-01 UTC.
internal sealed class SwiftDateConverter : JsonConverter<DateTimeOffset>
{
    private static readonly DateTimeOffset Epoch = new(2001, 1, 1, 0, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number) return Epoch.AddSeconds(reader.GetDouble());
        if (reader.TokenType == JsonTokenType.String && DateTimeOffset.TryParse(reader.GetString(), out var parsed)) return parsed;
        throw new JsonException("Invalid date value");
    }
    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
        writer.WriteNumberValue((value.ToUniversalTime() - Epoch).TotalSeconds);
}

internal sealed class NullableSwiftDateConverter : JsonConverter<DateTimeOffset?>
{
    private readonly SwiftDateConverter inner = new();
    public override DateTimeOffset? Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.Null ? null : inner.Read(ref reader, typeof(DateTimeOffset), options);
    public override void Write(Utf8JsonWriter writer, DateTimeOffset? value, JsonSerializerOptions options)
    {
        if (value is null) writer.WriteNullValue(); else inner.Write(writer, value.Value, options);
    }
}
