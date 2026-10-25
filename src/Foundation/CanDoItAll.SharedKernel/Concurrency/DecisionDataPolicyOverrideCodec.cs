using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CanDoItAll.SharedKernel;

public static class DecisionDataPolicyOverrideCodec {
    public const int SchemaVersion = 1;
    public const int MaximumUtf8Bytes = DecisionDataPolicyCodec.MaximumUtf8Bytes;
    private static readonly JsonSerializerOptions Options = new() {
        Converters = { new DecisionDataPolicyOverrideJsonConverter() }
    };

    public static string Serialize(DecisionDataPolicyOverride value) {
        ArgumentNullException.ThrowIfNull(value);
        return JsonSerializer.Serialize(value, Options);
    }

    public static DecisionDataPolicyOverride Deserialize(string json) {
        ArgumentNullException.ThrowIfNull(json);
        if (Encoding.UTF8.GetByteCount(json) > MaximumUtf8Bytes) {
            throw new JsonException("The decision policy override exceeds its supported encoding bound.");
        }
        return JsonSerializer.Deserialize<DecisionDataPolicyOverride>(json, Options)
            ?? throw new JsonException("A decision policy override must be an object.");
    }
}

internal sealed class DecisionDataPolicyOverrideJsonConverter : JsonConverter<DecisionDataPolicyOverride> {
    private const string Version = "schemaVersion";
    private const string ReadBasis = "readBasisMode";
    private const string Freshness = "publicationFreshness";
    private const string Change = "onBasisChange";
    private const string Replans = "maximumReplans";
    private const string Work = "maximumWorkDuration";
    private const string Age = "maximumBasisAge";
    private const int RequiredPropertyCount = 7;

    public override bool HandleNull => true;

    public override DecisionDataPolicyOverride Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        if (reader.TokenType != JsonTokenType.StartObject) {
            throw new JsonException("A decision policy override must be an object.");
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
                reader.BytesConsumed - start > DecisionDataPolicyOverrideCodec.MaximumUtf8Bytes) {
                throw new JsonException("A decision policy override requires bounded, known properties.");
            }
            var property = reader.GetString()!;
            if (!properties.Add(property) || !reader.Read()) {
                throw new JsonException("A decision policy override cannot repeat or omit a property value.");
            }
            switch (property) {
                case Version:
                    version = ReadInteger(ref reader);
                    break;
                case ReadBasis:
                    read = (ReadBasisMode?)ReadNullableInteger(ref reader);
                    break;
                case Freshness:
                    freshness = (PublicationFreshness?)ReadNullableInteger(ref reader);
                    break;
                case Change:
                    change = (OnBasisChange?)ReadNullableInteger(ref reader);
                    break;
                case Replans:
                    replans = ReadNullableInteger(ref reader);
                    break;
                case Work:
                    work = ReadNullableDuration(ref reader);
                    break;
                case Age:
                    age = ReadNullableDuration(ref reader);
                    break;
                default:
                    throw new JsonException("The decision policy override contains an unknown property.");
            }
        }
        if (reader.TokenType != JsonTokenType.EndObject || reader.BytesConsumed - start > DecisionDataPolicyOverrideCodec.MaximumUtf8Bytes ||
            version != DecisionDataPolicyOverrideCodec.SchemaVersion || properties.Count != RequiredPropertyCount) {
            throw new JsonException("A decision policy override requires every version-one field; inherited fields must explicitly be null.");
        }
        var value = new DecisionDataPolicyOverride(read, freshness, change, replans, work, age);
        RequireValid(value);
        return value;
    }

    public override void Write(Utf8JsonWriter writer, DecisionDataPolicyOverride value, JsonSerializerOptions options) {
        ArgumentNullException.ThrowIfNull(value);
        RequireValid(value);
        writer.WriteStartObject();
        writer.WriteNumber(Version, DecisionDataPolicyOverrideCodec.SchemaVersion);
        WriteNullableInteger(writer, ReadBasis, (int?)value.ReadBasisMode);
        WriteNullableInteger(writer, Freshness, (int?)value.PublicationFreshness);
        WriteNullableInteger(writer, Change, (int?)value.OnBasisChange);
        WriteNullableInteger(writer, Replans, value.MaximumReplans);
        writer.WriteString(Work, value.MaximumWorkDuration?.ToString("c", CultureInfo.InvariantCulture));
        writer.WriteString(Age, value.MaximumBasisAge?.ToString("c", CultureInfo.InvariantCulture));
        writer.WriteEndObject();
    }

    private static void RequireValid(DecisionDataPolicyOverride value) {
        if (value.ReadBasisMode is { } read && !Enum.IsDefined(read) ||
            value.PublicationFreshness is { } freshness && !Enum.IsDefined(freshness) ||
            value.OnBasisChange is { } change && !Enum.IsDefined(change)) {
            throw new JsonException("The decision policy override contains an unsupported enum value.");
        }
        if (value.MaximumReplans is < 0 || value.MaximumWorkDuration is { } work && work <= TimeSpan.Zero ||
            value.MaximumBasisAge is { } age && age <= TimeSpan.Zero) {
            throw new JsonException("Explicit decision policy allowances require a nonnegative replan count and positive durations.");
        }
    }

    private static int ReadInteger(ref Utf8JsonReader reader)
        => reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var value)
            ? value : throw new JsonException("Decision policy override versions, modes and counts must be integers.");

    private static int? ReadNullableInteger(ref Utf8JsonReader reader)
        => reader.TokenType == JsonTokenType.Null ? null : ReadInteger(ref reader);

    private static TimeSpan? ReadNullableDuration(ref Utf8JsonReader reader) {
        if (reader.TokenType == JsonTokenType.Null) {
            return null;
        }
        if (reader.TokenType != JsonTokenType.String || reader.ValueSpan.Length > 32 ||
            !TimeSpan.TryParseExact(reader.GetString(), "c", CultureInfo.InvariantCulture, out var value)) {
            throw new JsonException("Decision policy override durations must use the invariant constant string format.");
        }
        return value;
    }

    private static void WriteNullableInteger(Utf8JsonWriter writer, string property, int? value) {
        if (value is { } supplied) {
            writer.WriteNumber(property, supplied);
        } else {
            writer.WriteNull(property);
        }
    }
}
