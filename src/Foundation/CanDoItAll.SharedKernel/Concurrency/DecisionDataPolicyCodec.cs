using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CanDoItAll.SharedKernel;

public static class DecisionDataPolicyCodec {
    public const int MaximumUtf8Bytes = 1024;

    public static string Serialize(DecisionDataPolicy policy) {
        ArgumentNullException.ThrowIfNull(policy);
        return JsonSerializer.Serialize(policy);
    }

    public static DecisionDataPolicy Deserialize(string json) {
        ArgumentNullException.ThrowIfNull(json);
        if (Encoding.UTF8.GetByteCount(json) > MaximumUtf8Bytes) {
            throw new JsonException("The decision policy exceeds its supported encoding bound.");
        }
        return JsonSerializer.Deserialize<DecisionDataPolicy>(json)
            ?? throw new JsonException("A decision policy must be an object.");
    }
}

internal sealed class DecisionDataPolicyJsonConverter : JsonConverter<DecisionDataPolicy> {
    private const string Version = "schemaVersion";
    private const string ReadBasis = "readBasisMode";
    private const string Freshness = "publicationFreshness";
    private const string Change = "onBasisChange";
    private const string Replans = "maximumReplans";
    private const string Work = "maximumWorkDuration";
    private const string Age = "maximumBasisAge";

    public override bool HandleNull => true;

    public override DecisionDataPolicy Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        if (reader.TokenType != JsonTokenType.StartObject) {
            throw new JsonException("A decision policy must be an object.");
        }
        var start = reader.TokenStartIndex;
        var properties = new HashSet<string>(StringComparer.Ordinal);
        int? version = null;
        ReadBasisMode? read = null;
        PublicationFreshness? freshness = null;
        OnBasisChange? change = null;
        int? replans = null;
        TimeSpan? work = null;
        TimeSpan? age = null;
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject) {
            if (reader.TokenType != JsonTokenType.PropertyName || reader.ValueSpan.Length > 32 ||
                reader.BytesConsumed - start > DecisionDataPolicyCodec.MaximumUtf8Bytes) {
                throw new JsonException("A decision policy requires bounded, known properties.");
            }
            var property = reader.GetString()!;
            if (!properties.Add(property) || !reader.Read() || reader.BytesConsumed - start > DecisionDataPolicyCodec.MaximumUtf8Bytes) {
                throw new JsonException("A decision policy cannot repeat or omit a property value.");
            }
            switch (property) {
                case Version:
                    version = ReadInteger(ref reader);
                    break;
                case ReadBasis:
                    read = (ReadBasisMode)ReadInteger(ref reader);
                    break;
                case Freshness:
                    freshness = (PublicationFreshness)ReadInteger(ref reader);
                    break;
                case Change:
                    change = (OnBasisChange)ReadInteger(ref reader);
                    break;
                case Replans:
                    replans = ReadInteger(ref reader);
                    break;
                case Work:
                    work = ReadDuration(ref reader);
                    break;
                case Age:
                    age = ReadDuration(ref reader);
                    break;
                default:
                    throw new JsonException("The decision policy contains an unknown property.");
            }
        }
        if (reader.TokenType != JsonTokenType.EndObject || reader.BytesConsumed - start > DecisionDataPolicyCodec.MaximumUtf8Bytes ||
            version != DecisionDataPolicy.SchemaVersion || read is null || freshness is null || change is null ||
            replans is null || work is null || age is null) {
            throw new JsonException("The decision policy requires every version-one field and its supported schema version.");
        }
        try {
            return new(read.Value, freshness.Value, change.Value, new(replans.Value, work.Value, age.Value));
        } catch (DecisionDataPolicyException exception) {
            throw new JsonException("The saved decision policy is invalid.", exception);
        }
    }

    public override void Write(Utf8JsonWriter writer, DecisionDataPolicy value, JsonSerializerOptions options) {
        ArgumentNullException.ThrowIfNull(value);
        writer.WriteStartObject();
        writer.WriteNumber(Version, DecisionDataPolicy.SchemaVersion);
        writer.WriteNumber(ReadBasis, (int)value.ReadBasisMode);
        writer.WriteNumber(Freshness, (int)value.PublicationFreshness);
        writer.WriteNumber(Change, (int)value.OnBasisChange);
        writer.WriteNumber(Replans, value.Budget.MaximumReplans);
        writer.WriteString(Work, value.Budget.MaximumWorkDuration.ToString("c", CultureInfo.InvariantCulture));
        writer.WriteString(Age, value.Budget.MaximumBasisAge.ToString("c", CultureInfo.InvariantCulture));
        writer.WriteEndObject();
    }

    private static int ReadInteger(ref Utf8JsonReader reader)
        => reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var value)
            ? value : throw new JsonException("Decision policy versions, modes and counts must be integers.");

    private static TimeSpan ReadDuration(ref Utf8JsonReader reader) {
        if (reader.TokenType != JsonTokenType.String || reader.ValueSpan.Length > 32 ||
            !TimeSpan.TryParseExact(reader.GetString(), "c", CultureInfo.InvariantCulture, out var value)) {
            throw new JsonException("Decision policy durations must use the invariant constant string format.");
        }
        return value;
    }
}
