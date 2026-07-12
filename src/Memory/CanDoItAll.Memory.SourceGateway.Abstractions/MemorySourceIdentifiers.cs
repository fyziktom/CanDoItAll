using System.Text.Json.Serialization;

namespace CanDoItAll.Memory.SourceGateway;

public readonly record struct MemorySourceItemId
{
    public MemorySourceItemId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value.Trim();
    }

    public string Value { get; }

    public static bool TryParse(MemorySourceItemId id, out MemorySourceItemKey key)
    {
        var parts = id.Value.Split(':', 4, StringSplitOptions.TrimEntries);
        if (parts.Length != 4 ||
            !Enum.TryParse(parts[0], ignoreCase: false, out MemorySourceKind sourceKind) ||
            !Guid.TryParseExact(parts[1], "N", out var scopeId) ||
            !Enum.TryParse(parts[2], ignoreCase: false, out MemorySourceEntityKind entityKind) ||
            string.IsNullOrWhiteSpace(parts[3]))
        {
            key = default;
            return false;
        }

        key = new MemorySourceItemKey(sourceKind, scopeId, entityKind, parts[3]);
        return true;
    }

    public static MemorySourceItemId Create(
        MemorySourceKind sourceKind,
        Guid scopeId,
        MemorySourceEntityKind entityKind,
        string sourceEntityId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceEntityId);
        return new MemorySourceItemId($"{sourceKind}:{scopeId:N}:{entityKind}:{sourceEntityId.Trim()}");
    }

    public override string ToString() => Value;
}

public readonly record struct MemorySourceItemKey(
    MemorySourceKind SourceKind,
    Guid ScopeId,
    MemorySourceEntityKind EntityKind,
    string SourceEntityId);

public readonly record struct MemorySourceSnapshotId
{
    [JsonConstructor]
    public MemorySourceSnapshotId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value.Trim();
    }

    public string Value { get; }

    public static MemorySourceSnapshotId Create(
        MemorySourceKind sourceKind,
        Guid scopeId,
        string contentHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentHash);
        return new MemorySourceSnapshotId($"{sourceKind}:{scopeId:N}:{contentHash.Trim()}");
    }

    public override string ToString() => Value;
}

public enum MemorySourceKind
{
    WorkbenchProjectStructure,
    ProcessRuntime,
    WorkflowRuntime,
    CrmHr,
    ResourceCatalog,
    ManualInput
}

public enum MemorySourceEntityKind
{
    ProjectNode,
    ProjectLink,
    ProcessRun,
    ProcessStepEvidence,
    ProcessRunAssignment,
    ProcessWorkBrief,
    ProcessDecision,
    ProcessArtifact,
    ProcessJournal,
    ProcessConformanceObservation,
    ProcessImprovementCandidate,
    ProcessWorkflowRunLink,
    WorkflowRun,
    WorkflowEvent,
    WorkflowArtifact,
    WorkflowExternalRequest,
    ProcessDefinition,
    ProcessAgentSession,
    ProcessCompletionOutcome,
    CrmParty,
    CrmAccountProfile,
    CrmOpportunity,
    CrmInteraction,
    HrWorkforceProfile,
    ResourceReference,
    ManualText,
    ManualFileReference,
    ManualLinkReference
}

public enum MemorySourceSensitivity
{
    Public,
    Internal,
    Confidential,
    Sensitive
}

public enum MemorySourceAccessMode
{
    ReadOnly,
    Redacted
}

public enum MemorySourceSnapshotPageStatus
{
    PageReturned,
    EndOfSource
}

public enum MemorySourceSnapshotHashScope
{
    FullSnapshot,
    PageScoped,
    ProviderScope
}

public enum MemorySourceHashClassification
{
    PublicExportable,
    InternalIntegrity,
    RestrictedIntegrity
}

public enum MemorySourceHashPayloadBasis
{
    RedactedContent,
    SourceMetadata,
    RawSensitivePayload
}

public sealed record MemorySourceHashPolicy(
    MemorySourceHashClassification Classification,
    MemorySourceHashPayloadBasis PayloadBasis,
    bool Exportable,
    string UsageSummary)
{
    public static MemorySourceHashPolicy InternalIntegrity { get; } = new(
        MemorySourceHashClassification.InternalIntegrity,
        MemorySourceHashPayloadBasis.SourceMetadata,
        Exportable: false,
        "Internal source-change detection only. Do not expose as browser-visible metadata or vector payload data.");

    public static MemorySourceHashPolicy PublicRedactedContent { get; } = new(
        MemorySourceHashClassification.PublicExportable,
        MemorySourceHashPayloadBasis.RedactedContent,
        Exportable: true,
        "Hash is derived from redacted exposed content and may be used for non-sensitive public integrity checks.");

    public static MemorySourceHashPolicy RestrictedRawPayloadIntegrity(string usageSummary)
        => new(
            MemorySourceHashClassification.RestrictedIntegrity,
            MemorySourceHashPayloadBasis.RawSensitivePayload,
            Exportable: false,
            usageSummary);
}
