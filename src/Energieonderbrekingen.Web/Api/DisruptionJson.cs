using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Energieonderbrekingen.Web.Api;

public static class DisruptionJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new FlexibleDateTimeOffsetConverter() },
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };
}

/// <summary>
/// De API levert datums als "2008-04-06T15:58:12+0200" (offset zonder dubbele punt)
/// en lege strings in plaats van null. Deze converter vangt beide af.
/// </summary>
public sealed class FlexibleDateTimeOffsetConverter : JsonConverter<DateTimeOffset?>
{
    private static readonly string[] Formats =
    [
        "yyyy-MM-dd'T'HH:mm:sszzz",
        "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz",
        "yyyy-MM-dd'T'HH:mm:ssK",
        "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK",
        "yyyy-MM-dd",
    ];

    public override DateTimeOffset? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        var value = reader.GetString();
        if (string.IsNullOrWhiteSpace(value))
            return null;

        // "+0200" -> "+02:00" zodat de standaard formats het begrijpen.
        if (value.Length > 5 && (value[^5] == '+' || value[^5] == '-') && value[^4..].All(char.IsAsciiDigit))
            value = value[..^2] + ":" + value[^2..];

        if (DateTimeOffset.TryParseExact(value, Formats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var exact))
            return exact;

        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed)
            ? parsed
            : null;
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset? value, JsonSerializerOptions options)
    {
        if (value is null)
            writer.WriteNullValue();
        else
            writer.WriteStringValue(value.Value.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture));
    }
}
