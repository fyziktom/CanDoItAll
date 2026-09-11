namespace CanDoItAll.AgentFramework.Models;

public enum WorkflowStructureOutputKind {
    Task,
    Asset
}

public enum WorkflowStructureOutputRole {
    RequiredResult
}

public enum WorkflowStructureAuthorityChannel {
    AgentExecution,
    AuthenticatedOperator,
    LocalOperator
}

public enum WorkflowStructureOperatorSurface {
    UserInterface,
    Api
}

public sealed record WorkflowStructureAuthority(
    WorkflowStructureAuthorityChannel Channel,
    WorkflowLaunchActor Principal,
    Guid DatabaseProfileId,
    Guid ProjectId,
    bool CanCreateTasks,
    bool CanCreateAssets,
    DateTimeOffset? ExpiresAtUtc,
    string PolicyFingerprint) {
    public WorkflowStructureOperatorSurface OperatorSurface { get; init; }
    public AgentExecutionGovernanceSnapshot? AgentGovernance { get; init; }
    public WorkflowStructureProcessAuthority? ProcessAuthority { get; init; }
    public WorkflowStructureSchedulerAuthority? SchedulerAuthority { get; init; }
    public bool AllProjects { get; init; }
    public IReadOnlyList<Guid> ProjectIds { get; init; } = [];
}

public sealed record WorkflowStructureSchedulerAuthority(Guid PlanId, Guid FireAdmissionId, string AuthorityFingerprint);

public sealed record WorkflowStructureProcessAuthority(Guid RunId, Guid StepInstanceId, string ReadinessHash);

public sealed record WorkflowStructureAdmissionBinding(
    Guid IntentId,
    WorkflowRunId RunId,
    long Sequence,
    Guid NativeNodeId,
    string BindingFingerprint,
    WorkflowProjectStructureNodeId OutputParent,
    WorkflowStructureOutputRole OutputRole,
    WorkflowStructureAuthority Authority) {
    public WorkflowStructureLeaseOwner? LeaseOwner { get; init; }
}

public sealed record WorkflowStructureLeaseOwner(string AgentId, string AgentName, string MachineName,
    string RepositoryRoot, string BranchName, string SessionId);

public sealed record WorkflowStructureEffectContext(WorkflowExecutionOccurrence Occurrence,
    WorkflowVersionId VersionId, WorkflowNodeId StepId, int Slot);

public sealed record WorkflowStructureOutputIdentity(
    WorkflowExecutionOccurrence Occurrence,
    int Slot);

public sealed record WorkflowStructureOutputPlan(
    WorkflowStructureOutputIdentity Identity,
    WorkflowVersionId WorkflowVersionId,
    WorkflowNodeId StepId,
    Guid ProjectId,
    WorkflowProjectStructureNodeId ParentNodeId,
    string TargetBindingFingerprint,
    WorkflowStructureOutputKind Kind,
    WorkflowStructureOutputRole Role,
    string Fingerprint);

public sealed record WorkflowStructureOutputReceipt(
    WorkflowStructureOutputIdentity Identity,
    string Fingerprint,
    Guid ProjectId,
    string NodeId,
    Guid? AssetId,
    string StoragePath,
    DateTimeOffset AppliedAtUtc);

public enum WorkflowStructureOutputState {
    Prepared,
    Applied,
    AppliedTargetDeleted
}

public sealed record WorkflowStructureOutput(
    WorkflowStructureOutputPlan Plan,
    WorkflowStructureOutputState State,
    WorkflowStructureOutputReceipt? Receipt) {
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public Guid? StoragePlacementIntentId { get; init; }
}
