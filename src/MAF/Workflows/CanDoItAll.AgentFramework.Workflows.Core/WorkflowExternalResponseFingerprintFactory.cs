using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public static class WorkflowExternalResponseFingerprintFactory
{
    public static WorkflowExternalResponseFingerprint Create(
        WorkflowExternalRequestId requestId,
        WorkflowExternalRequestVersion requestVersion,
        WorkflowLaunchActor actor,
        WorkspaceScopeDescriptor authorizationScope,
        string authorizationPolicyFingerprint,
        WorkflowExternalResponseIdempotencyKey idempotencyKey,
        string responseJson)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(authorizationScope);
        ArgumentException.ThrowIfNullOrWhiteSpace(authorizationPolicyFingerprint);
        var canonicalJson = CanonicalizeJson(responseJson);
        var actorScopeFingerprint = CreateActorScopeFingerprint(
            actor,
            authorizationScope,
            authorizationPolicyFingerprint);
        var keyHash = new WorkflowExternalResponseIdempotencyKeyHash(
            HashParts(
                requestId.ToString(),
                requestVersion.ToString(),
                actorScopeFingerprint.Value,
                idempotencyKey.Value));
        var canonicalPayload = new WorkflowExternalResponsePayload(canonicalJson);
        var payloadHash = CreatePayloadHash(canonicalPayload);
        return new WorkflowExternalResponseFingerprint(
            keyHash,
            payloadHash,
            actorScopeFingerprint,
            canonicalPayload);
    }

    public static string CanonicalizeJson(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        using var document = JsonDocument.Parse(
            json,
            new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow
            });
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            WriteCanonicalJson(writer, document.RootElement);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteCanonicalJson(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                var properties = element.EnumerateObject().ToArray();
                if (properties
                    .GroupBy(property => property.Name, StringComparer.OrdinalIgnoreCase)
                    .Any(group => group.Count() > 1))
                {
                    throw new ArgumentException(
                        "Workflow external response payload cannot contain case-insensitive duplicate object properties.");
                }

                foreach (var property in properties.OrderBy(property => property.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonicalJson(writer, property.Value);
                }

                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    WriteCanonicalJson(writer, item);
                }

                writer.WriteEndArray();
                break;
            case JsonValueKind.String:
                writer.WriteStringValue(element.GetString());
                break;
            case JsonValueKind.Number:
                writer.WriteRawValue(element.GetRawText());
                break;
            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;
            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;
            case JsonValueKind.Null:
                writer.WriteNullValue();
                break;
            default:
                throw new ArgumentException($"Unsupported JSON value kind '{element.ValueKind}'.");
        }
    }

    private static string HashParts(params string[] parts)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var length = new byte[sizeof(int)];
        foreach (var part in parts)
        {
            var bytes = Encoding.UTF8.GetBytes(part);
            BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length);
            hash.AppendData(length);
            hash.AppendData(bytes);
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private static string Hash(string value)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    internal static WorkflowExternalResponseActorScopeFingerprint CreateActorScopeFingerprint(
        WorkflowLaunchActor actor,
        WorkspaceScopeDescriptor authorizationScope,
        string authorizationPolicyFingerprint)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(authorizationScope);
        ArgumentException.ThrowIfNullOrWhiteSpace(authorizationPolicyFingerprint);
        return new WorkflowExternalResponseActorScopeFingerprint(
            HashParts(
                ((int)actor.Kind).ToString(System.Globalization.CultureInfo.InvariantCulture),
                actor.SubjectId,
                ((int)authorizationScope.Kind).ToString(System.Globalization.CultureInfo.InvariantCulture),
                authorizationScope.Key,
                authorizationPolicyFingerprint.Trim()));
    }

    internal static WorkflowExternalResponsePayloadHash CreatePayloadHash(
        WorkflowExternalResponsePayload payload)
        => new(Hash(payload.Json));
}
