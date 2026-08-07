using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

/// <summary>
/// Safe persisted projection of the admitted turn context and execution
/// authority. Only identifiers, versions, and fingerprints are written; raw
/// model context, opaque attachments, and authorization internals never enter
/// run metadata. V1 metadata written before this codec existed stays readable:
/// both readers return <c>null</c> when the keys are absent.
/// </summary>
public static class AgentTurnContextMetadata
{
    public const string TurnContextReferenceMetadataKey = "agentTurnContextReference";
    public const string ExecutionAuthorityMetadataKey = "agentExecutionAuthority";
    public const string EmptyModelContextDigest = "none";

    public static string Apply(
        string? metadataJson,
        AgentTurnContextReference turnReference,
        AgentExecutionAuthorityRecord authority,
        AgentContextTransition? transition = null)
    {
        ArgumentNullException.ThrowIfNull(turnReference);
        ArgumentNullException.ThrowIfNull(authority);

        var metadata = ParseObject(metadataJson);
        var referenceObject = new JsonObject
        {
            ["turnContextId"] = turnReference.TurnContextId.Value.ToString("N"),
            ["contextEpochId"] = turnReference.ContextEpochId.Value.ToString("N"),
            ["sourceKind"] = turnReference.SourceKind.Value,
            ["sourceId"] = turnReference.SourceId.Value,
            ["surface"] = turnReference.Surface,
            ["view"] = turnReference.View,
            ["observationVersion"] = turnReference.ObservationVersion,
            ["modelContextDigest"] = turnReference.ModelContextDigest,
            ["capturedAtUtc"] = turnReference.CapturedAtUtc.ToString("O"),
            ["schemaVersion"] = turnReference.SchemaVersion
        };
        if (transition is not null && transition.Kind != AgentContextTransitionKind.None)
        {
            referenceObject["transitionKind"] = transition.Kind.ToString();
            referenceObject["transitionSummary"] = transition.Summary;
        }

        metadata[TurnContextReferenceMetadataKey] = referenceObject;
        metadata[ExecutionAuthorityMetadataKey] = new JsonObject
        {
            ["authorityId"] = authority.AuthorityId.Value.ToString("N"),
            ["workspaceScopeKind"] = authority.WorkspaceScope.Kind.ToString(),
            ["workspaceScopeKey"] = authority.WorkspaceScope.Key,
            ["databaseProfileGeneration"] = authority.DatabaseProfileGeneration.Value,
            ["readAllowed"] = authority.ReadAllowed,
            ["mutationAllowed"] = authority.MutationAllowed,
            ["policyVersion"] = authority.PolicyVersion,
            ["policyFingerprint"] = authority.PolicyFingerprint,
            ["schemaVersion"] = authority.SchemaVersion
        };
        return metadata.ToJsonString(AgentOutputJson.SerializerOptions);
    }

    public static AgentTurnContextReference? TryReadTurnContextReference(string? metadataJson)
    {
        if (TryParseObject(metadataJson) is not { } metadata ||
            metadata[TurnContextReferenceMetadataKey] is not JsonObject reference)
        {
            return null;
        }

        try
        {
            var turnContextId = ReadGuid(reference, "turnContextId");
            var contextEpochId = ReadGuid(reference, "contextEpochId");
            var sourceKind = ReadString(reference, "sourceKind");
            var sourceId = ReadString(reference, "sourceId");
            var observationVersion = reference["observationVersion"]?.GetValue<long>() ?? 0;
            var modelContextDigest = ReadString(reference, "modelContextDigest");
            var capturedAtUtc = DateTimeOffset.Parse(ReadString(reference, "capturedAtUtc"));
            var schemaVersion = reference["schemaVersion"]?.GetValue<int>()
                ?? AgentTurnContextReference.CurrentSchemaVersion;
            return new AgentTurnContextReference(
                new AgentTurnContextId(turnContextId),
                new AgentContextEpochId(contextEpochId),
                new AgentChatContextSourceKind(sourceKind),
                new AgentChatContextSourceId(sourceId),
                reference["surface"]?.GetValue<string>() ?? string.Empty,
                reference["view"]?.GetValue<string>() ?? string.Empty,
                observationVersion,
                modelContextDigest,
                capturedAtUtc,
                schemaVersion: schemaVersion);
        }
        catch (Exception exception) when (
            exception is ArgumentException or FormatException or InvalidOperationException)
        {
            return null;
        }
    }

    public static AgentTurnContextAuthorityProjection? TryReadExecutionAuthority(string? metadataJson)
    {
        if (TryParseObject(metadataJson) is not { } metadata ||
            metadata[ExecutionAuthorityMetadataKey] is not JsonObject authority)
        {
            return null;
        }

        try
        {
            var scopeKindText = ReadString(authority, "workspaceScopeKind");
            if (!Enum.TryParse<WorkspaceScopeKind>(scopeKindText, ignoreCase: true, out var scopeKind))
            {
                return null;
            }

            return new AgentTurnContextAuthorityProjection(
                new AgentExecutionAuthorityId(ReadGuid(authority, "authorityId")),
                new WorkspaceScopeDescriptor(
                    scopeKind,
                    authority["workspaceScopeKey"]?.GetValue<string>()),
                new DatabaseProfileGeneration(authority["databaseProfileGeneration"]?.GetValue<long>() ?? 0),
                authority["readAllowed"]?.GetValue<bool>() ?? false,
                authority["mutationAllowed"]?.GetValue<bool>() ?? false,
                authority["policyVersion"]?.GetValue<string>() ?? string.Empty,
                authority["policyFingerprint"]?.GetValue<string>() ?? string.Empty);
        }
        catch (Exception exception) when (
            exception is ArgumentException or ArgumentOutOfRangeException or FormatException or InvalidOperationException)
        {
            return null;
        }
    }

    private static Guid ReadGuid(JsonObject source, string propertyName)
    {
        var text = ReadString(source, propertyName);
        return Guid.ParseExact(text, "N");
    }

    private static string ReadString(JsonObject source, string propertyName)
        => source[propertyName]?.GetValue<string>()
            ?? throw new FormatException($"Metadata property '{propertyName}' is missing.");

    private static JsonObject ParseObject(string? metadataJson)
        => TryParseObject(metadataJson) ?? [];

    private static JsonObject? TryParseObject(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(metadataJson) as JsonObject;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }
}

/// <summary>
/// Safe persisted authority facts for one admitted run. This is a projection
/// of the durable metadata, not the richer runtime authorization object.
/// </summary>
public sealed record AgentTurnContextAuthorityProjection(
    AgentExecutionAuthorityId AuthorityId,
    WorkspaceScopeDescriptor WorkspaceScope,
    DatabaseProfileGeneration DatabaseProfileGeneration,
    bool ReadAllowed,
    bool MutationAllowed,
    string PolicyVersion,
    string PolicyFingerprint);
