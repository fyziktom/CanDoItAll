using System.Text.Json;
using System.Text.Json.Serialization;

namespace CanDoItAll.SharedKernel;

[JsonConverter(typeof(DatasetNamespaceIdJsonConverter))]
public sealed record DatasetNamespaceId {
    public DatasetNamespaceId(Guid value) {
        if (value == Guid.Empty) {
            throw new ArgumentException("A dataset namespace must be nonempty.", nameof(value));
        }
        Value = value;
    }

    public Guid Value { get; }

    public override string ToString() => Value.ToString("D");
}

internal sealed class DatasetNamespaceIdJsonConverter : JsonConverter<DatasetNamespaceId> {
    public override DatasetNamespaceId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        if (reader.TokenType != JsonTokenType.String || !reader.TryGetGuid(out var value) || value == Guid.Empty) {
            throw new JsonException("A dataset namespace must be a nonempty UUID string.");
        }
        return new(value);
    }

    public override void Write(Utf8JsonWriter writer, DatasetNamespaceId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}
