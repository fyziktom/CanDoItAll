using System.Text.Json;
using System.Text.Json.Serialization;

namespace CanDoItAll.SharedProviders.Abstractions;

[JsonConverter(typeof(SharedProviderPublicationIdJsonConverter))]
public readonly record struct SharedProviderPublicationId
{
    public SharedProviderPublicationId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("A shared-provider publication id cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }

    public static SharedProviderPublicationId New() => new(Guid.NewGuid());

    public static bool TryParse(string? value, out SharedProviderPublicationId publicationId)
    {
        if (!Guid.TryParseExact(value, "D", out var parsed) || parsed == Guid.Empty)
        {
            publicationId = default;
            return false;
        }

        publicationId = new SharedProviderPublicationId(parsed);
        return true;
    }

    public override string ToString()
        => Value != Guid.Empty
            ? Value.ToString("D")
            : throw new InvalidOperationException("The shared-provider publication id is invalid.");
}

[JsonConverter(typeof(SharedProviderSourceInstanceIdJsonConverter))]
public readonly record struct SharedProviderSourceInstanceId
{
    public SharedProviderSourceInstanceId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("A shared-provider source-instance id cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }

    public static SharedProviderSourceInstanceId New() => new(Guid.NewGuid());

    public static bool TryParse(string? value, out SharedProviderSourceInstanceId sourceInstanceId)
    {
        if (!Guid.TryParseExact(value, "D", out var parsed) || parsed == Guid.Empty)
        {
            sourceInstanceId = default;
            return false;
        }

        sourceInstanceId = new SharedProviderSourceInstanceId(parsed);
        return true;
    }

    public override string ToString()
        => Value != Guid.Empty
            ? Value.ToString("D")
            : throw new InvalidOperationException("The shared-provider source-instance id is invalid.");
}

[JsonConverter(typeof(SharedProviderPublicRevisionJsonConverter))]
public readonly record struct SharedProviderPublicRevision
{
    public const string Prefix = "sha256:";
    public const int HashLength = 64;

    public SharedProviderPublicRevision(string value)
    {
        if (!IsValid(value))
        {
            throw new ArgumentException("A public revision must be a lowercase SHA-256 identifier.", nameof(value));
        }

        Value = value;
    }

    public string Value { get; }

    public static bool TryParse(string? value, out SharedProviderPublicRevision revision)
    {
        if (!IsValid(value))
        {
            revision = default;
            return false;
        }

        revision = new SharedProviderPublicRevision(value!);
        return true;
    }

    public override string ToString()
        => IsValid(Value)
            ? Value
            : throw new InvalidOperationException("The shared-provider public revision is invalid.");

    private static bool IsValid(string? value)
    {
        if (value is null ||
            value.Length != Prefix.Length + HashLength ||
            !value.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        foreach (var character in value.AsSpan(Prefix.Length))
        {
            if (!(character is >= '0' and <= '9' or >= 'a' and <= 'f'))
            {
                return false;
            }
        }

        return true;
    }
}

internal sealed class SharedProviderPublicationIdJsonConverter : JsonConverter<SharedProviderPublicationId>
{
    public override SharedProviderPublicationId Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        var value = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
        if (!SharedProviderPublicationId.TryParse(value, out var publicationId))
        {
            throw new JsonException("The publication id is invalid.");
        }

        return publicationId;
    }

    public override void Write(
        Utf8JsonWriter writer,
        SharedProviderPublicationId value,
        JsonSerializerOptions options)
    {
        if (value.Value == Guid.Empty)
        {
            throw new JsonException("The publication id is invalid.");
        }

        writer.WriteStringValue(value.ToString());
    }
}

internal sealed class SharedProviderSourceInstanceIdJsonConverter : JsonConverter<SharedProviderSourceInstanceId>
{
    public override SharedProviderSourceInstanceId Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        var value = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
        if (!SharedProviderSourceInstanceId.TryParse(value, out var sourceInstanceId))
        {
            throw new JsonException("The source-instance id is invalid.");
        }

        return sourceInstanceId;
    }

    public override void Write(
        Utf8JsonWriter writer,
        SharedProviderSourceInstanceId value,
        JsonSerializerOptions options)
    {
        if (value.Value == Guid.Empty)
        {
            throw new JsonException("The source-instance id is invalid.");
        }

        writer.WriteStringValue(value.ToString());
    }
}

internal sealed class SharedProviderPublicRevisionJsonConverter : JsonConverter<SharedProviderPublicRevision>
{
    public override SharedProviderPublicRevision Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        var value = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
        if (!SharedProviderPublicRevision.TryParse(value, out var revision))
        {
            throw new JsonException("The public revision is invalid.");
        }

        return revision;
    }

    public override void Write(
        Utf8JsonWriter writer,
        SharedProviderPublicRevision value,
        JsonSerializerOptions options)
    {
        if (!SharedProviderPublicRevision.TryParse(value.Value, out _))
        {
            throw new JsonException("The public revision is invalid.");
        }

        writer.WriteStringValue(value.ToString());
    }
}
