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
        var authorityObject = new JsonObject
        {
            ["authorityId"] = authority.AuthorityId.Value.ToString("N"),
            ["agentId"] = authority.AgentId.ToString("N"),
            ["databaseProfileId"] = authority.DatabaseProfileId.ToString("N"),
            ["workspaceScopeKind"] = authority.WorkspaceScope.Kind.ToString(),
            ["workspaceScopeKey"] = authority.WorkspaceScope.Key,
            ["databaseProfileGeneration"] = authority.DatabaseProfileGeneration.Value,
            ["readAllowed"] = authority.ReadAllowed,
            ["mutationAllowed"] = authority.MutationAllowed,
            ["policyVersion"] = authority.PolicyVersion,
            ["policyFingerprint"] = authority.PolicyFingerprint,
            ["schemaVersion"] = authority.SchemaVersion
        };
        WriteEntries(authorityObject, "allowedOperations", authority.AllowedOperations);
        WriteEntries(authorityObject, "allowedCapabilityKeys", authority.AllowedCapabilityKeys);
        WriteEntries(authorityObject, "allowedExternalTargetAliases", authority.AllowedExternalTargetAliases);
        WriteEntries(authorityObject, "readOnlyExternalTargetAliases", authority.ReadOnlyExternalTargetAliases);
        metadata[ExecutionAuthorityMetadataKey] = authorityObject;
        return metadata.ToJsonString(AgentOutputJson.SerializerOptions);
    }

    private static void WriteEntries(JsonObject target, string propertyName, IReadOnlyList<string> entries)
    {
        if (entries.Count == 0)
        {
            return;
        }

        var array = new JsonArray();
        foreach (var entry in entries)
        {
            array.Add(entry);
        }

        target[propertyName] = array;
    }

    private static IReadOnlyList<string> ReadEntries(JsonObject source, string propertyName)
    {
        if (source[propertyName] is not JsonArray array || array.Count == 0)
        {
            return [];
        }

        var entries = new List<string>(array.Count);
        foreach (var node in array)
        {
            if (node?.GetValue<string>() is { } value && !string.IsNullOrWhiteSpace(value))
            {
                entries.Add(value);
            }
        }

        return entries;
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

    /// <summary>
    /// Rebuilds the immutable execution governance snapshot from the safe
    /// persisted authority projection of one admitted run. Returns
    /// <c>null</c> for metadata written before the authority projection
    /// existed and for any malformed projection — callers treat that as
    /// "no context-admitted authority", never as a wider grant. Metadata
    /// written before the agent/profile identifiers were persisted maps them
    /// to empty values; the execution-time validator skips empty identity
    /// comparisons instead of failing legacy runs.
    /// </summary>
    public static AgentExecutionGovernanceSnapshot? TryReadExecutionGovernanceSnapshot(string? metadataJson)
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

            var agentIdText = authority["agentId"]?.GetValue<string>();
            var databaseProfileIdText = authority["databaseProfileId"]?.GetValue<string>();
            return new AgentExecutionGovernanceSnapshot(
                new AgentExecutionAuthorityId(ReadGuid(authority, "authorityId")),
                string.IsNullOrWhiteSpace(agentIdText)
                    ? LegacyProjectionAgentId
                    : Guid.ParseExact(agentIdText, "N"),
                string.IsNullOrWhiteSpace(databaseProfileIdText)
                    ? LegacyProjectionProfileId
                    : Guid.ParseExact(databaseProfileIdText, "N"),
                new DatabaseProfileGeneration(authority["databaseProfileGeneration"]?.GetValue<long>() ?? 0),
                new WorkspaceScopeDescriptor(
                    scopeKind,
                    authority["workspaceScopeKey"]?.GetValue<string>()),
                authority["readAllowed"]?.GetValue<bool>() ?? false,
                authority["mutationAllowed"]?.GetValue<bool>() ?? false,
                authority["policyVersion"]?.GetValue<string>() is { Length: > 0 } policyVersion
                    ? policyVersion
                    : "unknown",
                authority["policyFingerprint"]?.GetValue<string>() is { Length: > 0 } policyFingerprint
                    ? policyFingerprint
                    : "unknown",
                ReadEntries(authority, "allowedOperations"),
                ReadEntries(authority, "allowedCapabilityKeys"),
                ReadEntries(authority, "allowedExternalTargetAliases"),
                ReadEntries(authority, "readOnlyExternalTargetAliases"));
        }
        catch (Exception exception) when (
            exception is ArgumentException or ArgumentOutOfRangeException or FormatException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>
    /// Sentinel identities for authority projections persisted before agent and
    /// profile identifiers were part of the projection. They are stable,
    /// non-empty, and can never match a real identity comparison — the
    /// execution-time validator recognizes and skips them explicitly.
    /// </summary>
    public static readonly Guid LegacyProjectionAgentId = new("00000000-0000-0000-0000-000000000001");

    public static readonly Guid LegacyProjectionProfileId = new("00000000-0000-0000-0000-000000000001");

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
