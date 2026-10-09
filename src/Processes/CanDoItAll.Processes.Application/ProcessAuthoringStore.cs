using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Templates;

namespace CanDoItAll.Processes.Application;

public sealed record ProcessAuthoringAddress(Guid DatabaseProfileId, Guid ProjectId, Guid ProjectLifetimeId, string DefinitionKey) {
    public void Validate() {
        if (DatabaseProfileId == Guid.Empty || (ProjectId == Guid.Empty) != (ProjectLifetimeId == Guid.Empty) ||
                string.IsNullOrWhiteSpace(DefinitionKey) || DefinitionKey != DefinitionKey.Trim() || DefinitionKey.Length > 200) {
            throw new ArgumentException("Authoring requires an explicit profile, canonical definition key and complete project lifetime.");
        }
    }
}

public enum ProcessAuthoringLifecycle { Draft, Published, Archived, Deleted }
public enum ProcessAuthoringOutcome { Accepted, Conflict }

public sealed record ProcessAuthoringContent(
    int SchemaVersion,
    ProcessTemplateDefinitionDocument Definition,
    IReadOnlyDictionary<string, IReadOnlyList<ProcessTemplateExecutionGuidanceDocument>> Guidance,
    IReadOnlyDictionary<string, ProcessTemplateRoleResourceDocument> RoleResources,
    ProcessAuthoringProvenance Base,
    IReadOnlyList<ProcessAuthoringReferencePlacement> References,
    IReadOnlyList<ProcessAuthoringImport> Imports) {
    public const int CurrentSchemaVersion = 1;
}

public sealed record ProcessAuthoringProvenance(string Source, string Version, string ContentHash);
public sealed record ProcessAuthoringReferencePlacement(string Key, ProcessDefinitionCanvasNodeKind Kind, string SemanticKey, string? StepKey, double X, double Y);
public sealed record ProcessAuthoringImport(Guid OperationId, ProcessAuthoringProvenance Source, string? TargetStepKey, IReadOnlyDictionary<string, string> Remap);

public sealed record ProcessAuthoringSnapshot(ProcessAuthoringAddress Address, long Revision, ProcessAuthoringLifecycle Lifecycle,
    ProcessAuthoringContent Content, Guid? PublishedId, DateTimeOffset UpdatedAtUtc);
public sealed record ProcessAuthoringPublication(Guid Id, ProcessAuthoringAddress Address, long Revision,
    string ContentHash, ProcessAuthoringContent Content, DateTimeOffset PublishedAtUtc);
public sealed record ProcessAuthoringCatalogEntry(ProcessAuthoringAddress Address, long Revision, ProcessAuthoringLifecycle Lifecycle,
    Guid? PublishedId, string Name, string Summary, string Criticality, string OperatingMode, DateTimeOffset UpdatedAtUtc);
public sealed record ProcessAuthoringCommit(ProcessAuthoringAddress Address, string CallerId, Guid OperationId, string RequestFingerprint,
    long ExpectedRevision, ProcessAuthoringContent Content, ProcessAuthoringLifecycle Lifecycle, bool Publish);
public sealed record ProcessAuthoringReceipt(Guid OperationId, ProcessAuthoringOutcome Outcome, ProcessAuthoringSnapshot? Snapshot);

public interface IProcessAuthoringStore {
    Task<ProcessAuthoringSnapshot?> ReadAsync(ProcessAuthoringAddress address, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProcessAuthoringCatalogEntry>> ListAsync(Guid databaseProfileId, Guid projectId, Guid projectLifetimeId, CancellationToken cancellationToken = default);
    Task<ProcessAuthoringPublication?> ReadPublicationAsync(Guid publicationId, CancellationToken cancellationToken = default);
    Task<ProcessAuthoringReceipt?> RecoverAsync(ProcessAuthoringAddress address, string callerId, Guid operationId, string requestFingerprint, CancellationToken cancellationToken = default);
    Task<ProcessAuthoringReceipt> CommitAsync(ProcessAuthoringCommit command, CancellationToken cancellationToken = default);
}

public interface IProcessAuthoringAdmissionPolicy {
    Task RequireAsync(ProcessAuthoringAddress address, string? callerId, bool underMutationGate, CancellationToken cancellationToken);
}
