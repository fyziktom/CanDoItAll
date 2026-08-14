using CanDoItAll.Processes.Abstractions;

namespace CanDoItAll.Processes.Drivers.Abstractions;

public sealed record ProcessDriverDescriptor(
    DriverId DriverId,
    string DisplayName,
    string DriverVersion,
    string MinRuntimeSchema,
    string MaxRuntimeSchema,
    ProcessDriverLayer Layer,
    IReadOnlySet<CapabilityTag> CapabilityTags,
    IReadOnlyList<ProcessDriverDependency> Dependencies,
    IReadOnlyList<ProcessDriverConflict> Conflicts,
    IReadOnlyList<ProcessDriverFacetDescriptor> Facets,
    IReadOnlyList<ProcessStrategyDescriptor> Strategies)
{
    public IReadOnlySet<ProcessHostCapabilityId> RequiredHostCapabilities { get; init; } =
        new HashSet<ProcessHostCapabilityId>();
}

public sealed record ProcessDriverDependency(
    DriverId DriverId,
    string VersionRange);

public sealed record ProcessDriverConflict(
    DriverId? DriverId,
    CapabilityTag? ExclusiveCapabilityTag,
    string Reason);

public sealed record ProcessDriverFacetDescriptor(
    DriverFacetKey Key,
    string SchemaVersion,
    string Description);

public sealed record ProcessStrategyDescriptor(
    StrategyId StrategyId,
    string StrategyVersion,
    ProcessStrategyKind Kind,
    IReadOnlySet<CapabilityTag> RequiredCapabilityTags)
{
    public IReadOnlySet<ProcessHostCapabilityId> RequiredHostCapabilities { get; init; } =
        new HashSet<ProcessHostCapabilityId>();
}

public enum ProcessDriverLayer
{
    BroadBase,
    Platform,
    Framework,
    Scenario,
    LocalOverride
}

public enum ProcessStrategyKind
{
    StepExecution,
    BranchDecision,
    ManagerDecision,
    ErrorPreprocessing,
    ArtifactRecovery,
    ArtifactResupply,
    ArtifactValidation,
    SubprocessCommunication,
    LoopProtection,
    TemplateOperation
}
