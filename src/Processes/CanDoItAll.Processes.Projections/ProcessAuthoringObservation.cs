using System.ComponentModel;

namespace CanDoItAll.Processes.Projections;

public sealed record ProcessAuthoringObservation(Guid DatabaseProfileId, Guid ProjectId, Guid ProjectLifetimeId,
    ProcessDefinitionCatalogItemKey DefinitionKey, long Revision, string ContentHash, Guid? PublishedId) {
    public long InheritedRevision { get; init; }
    public bool HasSameOwner(ProcessAuthoringObservation other)
        => DatabaseProfileId == other.DatabaseProfileId && ProjectId == other.ProjectId &&
            ProjectLifetimeId == other.ProjectLifetimeId && DefinitionKey == other.DefinitionKey;

    public bool Supersedes(ProcessAuthoringObservation other) => HasSameOwner(other) &&
        (Revision > other.Revision || Revision == other.Revision && InheritedRevision > other.InheritedRevision);
}

[Description("Authoritative authoring projections after a command, or a warning when reconciliation was unavailable. The command receipt retains its outcome.")]
public sealed record ProcessAuthoringReconciliation(ProcessDefinitionEditorProjection? Editor, string? Warning);

public enum ProcessAuthoringOperationState { NotRecorded, Committed, Rejected, Unavailable }

[Description("Original authoring operation locator. Its captured token is checked against the native profile and project lifetime.")]
public sealed record ProcessAuthoringOperationQuery(ProcessWorkspaceShellScope Scope, ProcessDefinitionCatalogItemKey DefinitionKey,
    string VersionToken, Guid OperationId);

[Description("Durable operation observation. NotRecorded permits only an explicit retry of the original payload with the same operation identity.")]
public sealed record ProcessAuthoringOperationStatus(ProcessAuthoringOperationState State, ProcessAuthoringObservation? Observation);
