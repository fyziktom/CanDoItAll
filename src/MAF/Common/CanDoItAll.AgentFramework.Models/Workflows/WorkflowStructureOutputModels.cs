using System.Text.Json.Serialization;

namespace CanDoItAll.AgentFramework.Models;

public enum WorkflowStructureOutputKind {
    Task,
    Asset
}

/// <summary>
/// Role of Project Structure outputs bound to a workflow run, as a JSON integer: 0 RequiredResult.
/// </summary>
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

/// <summary>
/// Project Structure output authority captured when a workflow run was launched: the actor, database profile, project
/// scope and output permissions the run may use. It is written as an opaque, versioned JSON object whose members are
/// internal; clients must not rely on them.
/// </summary>
[JsonConverter(typeof(WorkflowStructureAuthorityJsonConverter))]
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
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public WorkflowStructureProjectScope? ProjectScope { get; init; }
}

public sealed record WorkflowStructureSchedulerAuthority(Guid PlanId, Guid FireAdmissionId, string AuthorityFingerprint);

public sealed record WorkflowStructureProcessAuthority(Guid RunId, Guid StepInstanceId, string ReadinessHash) {
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? ExecutionRunId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OwnerFingerprint { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public WorkflowProcessToolBinding? ToolInvocation { get; init; }
}

/// <summary>
/// Admission that binds a workflow run started from a Project Structure node to the project outputs it may create.
/// Internal lineage recorded in the launch origin.
/// </summary>
/// <param name="IntentId">Identifier of the start intent that admitted the run.</param>
/// <param name="RunId">Identifier of the admitted workflow run.</param>
/// <param name="Sequence">Admission sequence number within the node.</param>
/// <param name="NativeNodeId">Identifier of the node record in Project Structure.</param>
/// <param name="BindingFingerprint">Fingerprint of the output binding; internal.</param>
/// <param name="OutputParent">Project Structure node under which outputs are created.</param>
/// <param name="OutputRole">Role of the outputs, as a JSON integer: 0 RequiredResult.</param>
/// <param name="Authority">Output authority captured for the run; an opaque internal object.</param>
public sealed record WorkflowStructureAdmissionBinding(
    Guid IntentId,
    WorkflowRunId RunId,
    long Sequence,
    Guid NativeNodeId,
    string BindingFingerprint,
    WorkflowProjectStructureNodeId OutputParent,
    WorkflowStructureOutputRole OutputRole,
    WorkflowStructureAuthority Authority) {
    /// <summary>Holder of the Project Structure lease used at admission; null when none was used.</summary>
    public WorkflowStructureLeaseOwner? LeaseOwner { get; init; }
}

/// <summary>
/// Holder of a Project Structure lease recorded with a workflow admission.
/// </summary>
/// <param name="AgentId">Identifier of the lease holder.</param>
/// <param name="AgentName">Display name of the lease holder.</param>
/// <param name="MachineName">Machine the lease holder ran on.</param>
/// <param name="RepositoryRoot">Repository root recorded by the lease holder; may be empty.</param>
/// <param name="BranchName">Branch recorded by the lease holder; may be empty.</param>
/// <param name="SessionId">Session of the lease holder.</param>
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
    string Fingerprint) {
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public WorkflowProjectLifetime? ProjectLifetime { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SourceAuthorityFingerprint { get; init; }
}

public sealed record WorkflowStructureOutputReceipt(
    WorkflowStructureOutputIdentity Identity,
    string Fingerprint,
    Guid ProjectId,
    string NodeId,
    Guid? AssetId,
    string StoragePath,
    DateTimeOffset AppliedAtUtc) {
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public WorkflowProjectLifetime? ProjectLifetime { get; init; }
}

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
