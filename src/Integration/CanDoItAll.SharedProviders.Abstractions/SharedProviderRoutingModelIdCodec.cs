using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Diagnostics.CodeAnalysis;

namespace CanDoItAll.SharedProviders.Abstractions;

[JsonConverter(typeof(SharedProviderRoutingModelIdJsonConverter))]
public readonly record struct SharedProviderRoutingModelId
{
    internal SharedProviderRoutingModelId(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public override string ToString()
        => SharedProviderRoutingModelIdCodec.TryParse(Value, out _, out _)
            ? Value
            : throw new InvalidOperationException("The shared-provider routing model id is invalid.");
}

internal sealed class SharedProviderRoutingModelIdJsonConverter : JsonConverter<SharedProviderRoutingModelId>
{
    public override SharedProviderRoutingModelId Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        var value = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
        if (!SharedProviderRoutingModelIdCodec.TryParse(value, out var routingModelId, out _))
        {
            throw new JsonException("The routing model id is invalid.");
        }

        return routingModelId;
    }

    public override void Write(
        Utf8JsonWriter writer,
        SharedProviderRoutingModelId value,
        JsonSerializerOptions options)
    {
        if (!SharedProviderRoutingModelIdCodec.TryParse(value.Value, out _, out _))
        {
            throw new JsonException("The routing model id is invalid.");
        }

        writer.WriteStringValue(value.Value);
    }
}

public sealed record SharedProviderRoutingModelRoute
{
    internal SharedProviderRoutingModelRoute(
        SharedProviderPublicationId publicationId,
        string modelFingerprint)
    {
        if (publicationId.Value == Guid.Empty ||
            modelFingerprint is not { Length: 64 } ||
            modelFingerprint.Any(character => !(character is >= '0' and <= '9' or >= 'a' and <= 'f')))
        {
            throw new ArgumentException("The shared-provider routing model route is invalid.");
        }

        PublicationId = publicationId;
        ModelFingerprint = modelFingerprint;
    }

    public SharedProviderPublicationId PublicationId { get; }

    public string ModelFingerprint { get; }
}

public static class SharedProviderRoutingModelIdCodec
{
    public const string VersionPrefix = "sp1";
    public const int MaximumUpstreamModelIdLength = 256;

    private const int Sha256ByteLength = 32;
    private const int Sha256Base64UrlLength = 43;
    private const int PublicationIdLength = 32;
    private const int RoutingModelIdLength = 3 + 1 + PublicationIdLength + 1 + Sha256Base64UrlLength;
    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    public static SharedProviderRoutingModelId Create(
        SharedProviderPublicationId publicationId,
        string upstreamModelId)
    {
        if (publicationId.Value == Guid.Empty)
        {
            throw new ArgumentException("The publication id cannot be empty.", nameof(publicationId));
        }

        var exactModelId = ValidateUpstreamModelId(upstreamModelId);
        byte[] modelBytes;
        try
        {
            modelBytes = StrictUtf8.GetBytes(exactModelId);
        }
        catch (EncoderFallbackException exception)
        {
            throw new ArgumentException(
                "An upstream model id must contain valid Unicode text.",
                nameof(upstreamModelId),
                exception);
        }

        var hash = SHA256.HashData(modelBytes);
        var fingerprint = Convert.ToBase64String(hash)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        return new SharedProviderRoutingModelId(
            $"{VersionPrefix}.{publicationId.Value:N}.{fingerprint}");
    }

    public static SharedProviderRoutingModelId Parse(string value)
    {
        if (!TryParse(value, out var routingModelId, out _))
        {
            throw new FormatException("The shared-provider routing model id is malformed.");
        }

        return routingModelId;
    }

    public static bool TryParse(
        string? value,
        out SharedProviderRoutingModelId routingModelId,
        [NotNullWhen(true)] out SharedProviderRoutingModelRoute? route)
    {
        routingModelId = default;
        route = default;

        if (value is not { Length: RoutingModelIdLength })
        {
            return false;
        }

        var firstSeparator = value.IndexOf('.');
        var secondSeparator = value.IndexOf('.', firstSeparator + 1);
        if (firstSeparator != VersionPrefix.Length ||
            secondSeparator != VersionPrefix.Length + 1 + PublicationIdLength ||
            !value.AsSpan(0, firstSeparator).SequenceEqual(VersionPrefix) ||
            !TryParsePublicationId(value.AsSpan(firstSeparator + 1, PublicationIdLength), out var publicationId))
        {
            return false;
        }

        var fingerprint = value[(secondSeparator + 1)..];
        if (!TryDecodeFingerprint(fingerprint, out var decodedHash))
        {
            return false;
        }

        routingModelId = new SharedProviderRoutingModelId(value);
        route = new SharedProviderRoutingModelRoute(
            publicationId,
            Convert.ToHexStringLower(decodedHash));
        return true;
    }

    public static bool Matches(
        SharedProviderRoutingModelId routingModelId,
        SharedProviderPublicationId publicationId,
        string upstreamModelId)
    {
        if (!TryParse(routingModelId.Value, out _, out _))
        {
            return false;
        }

        var expected = Create(publicationId, upstreamModelId);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(routingModelId.Value),
            Encoding.ASCII.GetBytes(expected.Value));
    }

    private static string ValidateUpstreamModelId(string upstreamModelId)
    {
        ArgumentNullException.ThrowIfNull(upstreamModelId);

        if (upstreamModelId.Length == 0 ||
            upstreamModelId.Length > MaximumUpstreamModelIdLength ||
            upstreamModelId != upstreamModelId.Trim() ||
            upstreamModelId.Any(char.IsControl))
        {
            throw new ArgumentException(
                $"An upstream model id must be an exact 1 to {MaximumUpstreamModelIdLength} character token without outer whitespace or control characters.",
                nameof(upstreamModelId));
        }

        return upstreamModelId;
    }

    private static bool TryParsePublicationId(
        ReadOnlySpan<char> value,
        out SharedProviderPublicationId publicationId)
    {
        publicationId = default;
        if (value.IndexOfAnyExceptInRange('0', '9') < 0)
        {
            return Guid.TryParseExact(value, "N", out var numericGuid) &&
                numericGuid != Guid.Empty &&
                AssignPublicationId(numericGuid, out publicationId);
        }

        foreach (var character in value)
        {
            if (!(character is >= '0' and <= '9' or >= 'a' and <= 'f'))
            {
                return false;
            }
        }

        return Guid.TryParseExact(value, "N", out var parsed) &&
            parsed != Guid.Empty &&
            AssignPublicationId(parsed, out publicationId);
    }

    private static bool AssignPublicationId(
        Guid value,
        out SharedProviderPublicationId publicationId)
    {
        publicationId = new SharedProviderPublicationId(value);
        return true;
    }

    private static bool TryDecodeFingerprint(string fingerprint, out byte[] hash)
    {
        hash = [];
        if (fingerprint.Length != Sha256Base64UrlLength ||
            fingerprint.Any(character =>
                !(char.IsAsciiLetterOrDigit(character) || character is '-' or '_')))
        {
            return false;
        }

        var base64 = fingerprint.Replace('-', '+').Replace('_', '/') + "=";
        try
        {
            hash = Convert.FromBase64String(base64);
        }
        catch (FormatException)
        {
            return false;
        }

        return hash.Length == Sha256ByteLength &&
            Convert.ToBase64String(hash)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_') == fingerprint;
    }
}
