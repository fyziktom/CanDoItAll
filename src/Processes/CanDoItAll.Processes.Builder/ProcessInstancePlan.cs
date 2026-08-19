using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;

namespace CanDoItAll.Processes.Builder;

public sealed record ProcessInstancePlan(
    ProcessInstancePlanHeader Header,
    ResolvedProcessDefinitionSnapshot Definition,
    DriverStackSnapshot DriverStack,
    StrategyBindingSet Strategies,
    IReadOnlyList<StepInstancePlan> Steps,
    ArtifactPlan ArtifactPlan,
    BranchRouteTable Branches,
    IReadOnlyList<SubprocessInstancePlanRef> Subprocesses,
    ManagerPlan Manager,
    BudgetPlan Budgets,
    MonitoringPlan Monitoring,
    SecurityPlan Security,
    string PlanHash);

public sealed record ProcessInstancePlanHeader(
    ProcessInstancePlanId PlanId,
    ProcessInstancePlanId RootPlanId,
    ProcessInstancePlanId? ParentPlanId,
    ProcessStepInstanceId? ParentStepId,
    string PlanSchemaVersion,
    DateTimeOffset CreatedAtUtc,
    int HierarchyDepth);

public sealed record ResolvedProcessDefinitionSnapshot(
    ProcessDefinitionId DefinitionId,
    ProcessDefinitionVersionId VersionId,
    string DefinitionContentHash,
    string SourceSchemaVersion,
    string TargetSchemaVersion,
    IReadOnlyList<string> AppliedMigrationIds,
    IReadOnlyList<ResolvedTemplateComponentSnapshot> TemplateComponents,
    IReadOnlyList<string> AppliedLocalOverridePointers);

public sealed record ResolvedTemplateComponentSnapshot(
    TemplateComponentId ComponentId,
    string Key,
    string ContentVersion,
    string ContentHash);

public sealed record DriverStackSnapshot(
    IReadOnlyList<ResolvedDriverSnapshot> Drivers)
{
    public ProcessHostProfileId HostProfileId { get; init; } = new("unknown");

    public IReadOnlyList<ProcessHostCapabilityFact> HostCapabilities { get; init; } = [];
}

public sealed record ResolvedDriverSnapshot(
    DriverId DriverId,
    string DriverVersion,
    ProcessDriverLayer Layer,
    string MinRuntimeSchema,
    string MaxRuntimeSchema,
    IReadOnlySet<CapabilityTag> CapabilityTags)
{
    public IReadOnlySet<ProcessHostCapabilityId> RequiredHostCapabilities { get; init; } =
        new HashSet<ProcessHostCapabilityId>();
}

public sealed record StrategyBindingSet(
    IReadOnlyList<ProcessStrategyBindingSnapshot> ExecutionBindings,
    IReadOnlyList<ProcessStrategyBindingSnapshot> ManagerBindings,
    IReadOnlyList<ProcessStrategyBindingSnapshot> RecoveryBindings,
    IReadOnlyList<ProcessStrategyBindingSnapshot> ResupplyBindings);

public sealed record StepInstancePlan(
    ProcessStepInstanceId StepInstanceId,
    ProcessStepDefinitionId StepDefinitionId,
    string StepKey,
    ProcessStepKind Kind,
    bool IsExecutable,
    bool StartsSubprocess,
    ProcessStrategyBindingSnapshot? ExecutionStrategyBinding)
{
    public IReadOnlySet<ProcessHostCapabilityId> RequiredHostCapabilities { get; init; } =
        new HashSet<ProcessHostCapabilityId>();

    public IReadOnlyList<string> RequiredRuntimeToolNames { get; init; } = [];
}

public sealed record ArtifactPlan(
    IReadOnlyList<ArtifactSlotPlan> Slots,
    IReadOnlyList<ArtifactLedgerSeed> InitialLedgerEntries);

public sealed record ArtifactSlotPlan(
    ArtifactSlotId SlotId,
    string SlotKey,
    ArtifactDefinitionId ArtifactDefinitionId,
    ProcessArtifactRequirementMode RequirementMode,
    ProcessArtifactScope Scope);

public sealed record ArtifactLedgerSeed(
    ArtifactSlotId SlotId,
    ArtifactInstanceId ArtifactId,
    ProcessArtifactScope Scope,
    string ContentHash);

public sealed record BranchRouteTable(
    IReadOnlyList<BranchRoutePlan> Routes);

public sealed record BranchRoutePlan(
    ProcessStepDefinitionId BranchStepId,
    BranchFamilyId FamilyId,
    BranchOutcomeId OutcomeId,
    BranchOutcomeCategory Category,
    ProcessRouteTarget RouteTarget,
    LoopBudgetPlan? LoopBudget);

public sealed record LoopBudgetPlan(
    string SourceKey,
    int MaximumRepeats,
    LoopFingerprintPolicyId FingerprintPolicyId,
    ProcessRouteTarget EscalationTarget);

public sealed record SubprocessInstancePlanRef(
    ProcessStepInstanceId ParentStepInstanceId,
    ProcessInstancePlanId ChildPlanId,
    string ChildPlanHash,
    int HierarchyDepth,
    string ParentToChildArtifactProjectionHash,
    string ChildToParentArtifactProjectionHash,
    string CancellationPolicyHash,
    string EscalationPolicyHash);

public sealed record ManagerPlan(
    string PolicyHash,
    ProcessStrategyBindingSnapshot? ManagerStrategyBinding,
    IReadOnlyList<ProcessStrategyBindingSnapshot> RecoveryBindings,
    IReadOnlyList<ProcessStrategyBindingSnapshot> ResupplyBindings);

public sealed record BudgetPlan(
    IReadOnlyList<LoopBudgetPlan> LoopBudgets);

public sealed record MonitoringPlan(
    bool Enabled,
    string ProjectionConfigHash);

public sealed record SecurityPlan(
    string GovernancePolicyHash,
    IReadOnlyList<string> RequiredApprovalKeys);
