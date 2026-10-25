using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CanDoItAll.AgentFramework.Models;

public sealed class AgentHistoricalReceiptRevisionJsonConverter : JsonConverter<long> {
    public override long Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        if (reader.TokenType != JsonTokenType.String || reader.HasValueSequence && reader.ValueSequence.Length > 19 ||
                !reader.HasValueSequence && reader.ValueSpan.Length > 19) {
            throw new JsonException("A historical receipt revision must be a canonical positive decimal string.");
        }
        var value = reader.GetString();
        if (value is not { Length: > 0 and <= 19 } || value[0] is < '1' or > '9') {
            throw new JsonException("A historical receipt revision must be a canonical positive decimal string.");
        }
        foreach (var character in value) {
            if (character is < '0' or > '9') {
                throw new JsonException("A historical receipt revision must contain only decimal digits.");
            }
        }
        return long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var revision)
            ? revision : throw new JsonException("The historical receipt revision exceeds Int64.");
    }

    public override void Write(Utf8JsonWriter writer, long value, JsonSerializerOptions options) {
        if (value < 1) {
            throw new JsonException("A historical receipt revision must be positive.");
        }
        writer.WriteStringValue(value.ToString(CultureInfo.InvariantCulture));
    }
}
