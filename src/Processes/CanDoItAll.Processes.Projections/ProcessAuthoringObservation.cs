using System.ComponentModel;

namespace CanDoItAll.Processes.Projections;

[Description("Authoritative owner, revision and effective content identity shared by all editor families for one definition. This observation does not grant write authority.")]
public sealed record ProcessAuthoringObservation(
    [property: Description("Identity of the runtime database profile that owns this observation.")] Guid DatabaseProfileId,
    [property: Description("Project identity, or the empty GUID for the global workspace.")] Guid ProjectId,
    [property: Description("Captured project lifetime identity, or the empty GUID for the global workspace.")] Guid ProjectLifetimeId,
    [property: Description("Stable key of the process definition whose authoring content is observed.")] ProcessDefinitionCatalogItemKey DefinitionKey,
    [property: Description("Monotone revision of this scope's authored head, including a reset tombstone; zero when no local head exists.")] long Revision,
    [property: Description("SHA-256 identity of the complete effective authoring content represented by this observation.")] string ContentHash,
    [property: Description("Immutable publication retained by this scope's authored head, or null when that head has no publication. A separate inherited source may still be launchable.")] Guid? PublishedId) {
    [Description("Revision of the inherited global head consulted when local content is absent or reset; zero when no inherited head is observed.")]
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
