namespace CanDoItAll.Memory.Abstractions;

public enum MemoryProviderDriverKind
{
    Http = 0,
    Mcp = 1,
    NativeRemote = 2,
    Mock = 3,
    InProcessMigration = 4
}

public enum MemoryProviderHealthState
{
    Unknown = 0,
    Healthy = 1,
    Degraded = 2,
    Unreachable = 3
}

public enum MemoryProviderWorkspaceScope
{
    AllWorkspaces = 0,
    SingleWorkspace = 1
}

public enum MemoryProviderFallbackBehavior
{
    DenyImplicitFallback = 0,
    AllowDefaultProviderWhenNoAssignment = 1
}

public enum MemoryProviderAssignmentScope
{
    Agent = 0,
    AgentRole = 1,
    Workflow = 2,
    WorkflowNode = 3,
    Process = 4
}

public enum MemoryProviderSelectionStatus
{
    Selected = 0,
    NoProviderConfigured = 1,
    NoEnabledProvider = 2,
    ProviderNotFound = 3,
    ProviderDisabled = 4,
    CapabilityUnavailable = 5,
    CapabilityDenied = 6,
    ProviderDenied = 7,
    ProviderSelectionRequired = 8,
    ProviderConfigurationFailed = 9
}

public enum MemoryProviderSelectionReason
{
    None = 0,
    ExplicitProvider = 1,
    AssignmentOverride = 2,
    DefaultProvider = 3
}

public sealed record MemoryProviderProfile(
    MemoryProviderInstanceId InstanceId,
    string DisplayName,
    MemoryProviderDriverKind DriverKind,
    bool IsEnabled,
    MemoryProviderHealthState HealthState,
    MemoryProviderWorkspaceScope WorkspaceScope,
    IReadOnlyList<string> SelectionTags,
    MemoryProviderProfilePolicy DefaultPolicy,
    MemoryProviderManifest Manifest);

public sealed record MemoryProviderProfilePolicy(
    MemoryProviderFallbackBehavior FallbackBehavior)
{
    public static readonly MemoryProviderProfilePolicy Default = new(MemoryProviderFallbackBehavior.DenyImplicitFallback);
}

public sealed record MemoryProviderAssignment(
    MemoryProviderAssignmentScope Scope,
    string Key,
    MemoryProviderInstanceId ProviderInstanceId);

public sealed record MemoryProviderSelectionContext(
    string? AgentId,
    string? AgentRole,
    string? WorkflowId,
    string? WorkflowNodeId,
    string? ProcessId)
{
    public static readonly MemoryProviderSelectionContext None = new(null, null, null, null, null);
}

public sealed record MemoryProviderSelectionPolicy(
    MemoryCapabilityId RequiredCapability,
    MemoryProviderInstanceId? ExplicitProviderId,
    MemoryProviderInstanceId? DefaultProviderId,
    IReadOnlyList<MemoryProviderAssignment> Assignments,
    IReadOnlyList<MemoryCapabilityId> AllowedCapabilities,
    IReadOnlyList<MemoryCapabilityId> DeniedCapabilities,
    MemoryProviderFallbackBehavior FallbackBehavior)
{
    public IReadOnlyList<MemoryProviderInstanceId> AllowedProviderIds { get; init; } = [];

    public static MemoryProviderSelectionPolicy RequireCapability(MemoryCapabilityId capability) =>
        new(
            capability,
            ExplicitProviderId: null,
            DefaultProviderId: null,
            Assignments: [],
            AllowedCapabilities: [],
            DeniedCapabilities: [],
            MemoryProviderFallbackBehavior.DenyImplicitFallback);
}

public sealed record MemoryProviderSelectionResult(
    MemoryProviderSelectionStatus Status,
    MemoryProviderSelectionReason Reason,
    MemoryProviderProfile? SelectedProvider,
    MemoryCapabilityId RequiredCapability,
    bool DispatchAllowed,
    string Diagnostic,
    IReadOnlyList<MemoryProviderInstanceId> CandidateProviderIds)
{
    public static MemoryProviderSelectionResult Selected(
        MemoryProviderProfile provider,
        MemoryProviderSelectionReason reason,
        MemoryCapabilityId requiredCapability) =>
        new(
            MemoryProviderSelectionStatus.Selected,
            reason,
            provider,
            requiredCapability,
            DispatchAllowed: true,
            $"Selected memory provider '{provider.InstanceId}'.",
            [provider.InstanceId]);

    public static MemoryProviderSelectionResult Rejected(
        MemoryProviderSelectionStatus status,
        MemoryProviderSelectionReason reason,
        MemoryCapabilityId requiredCapability,
        string diagnostic,
        IReadOnlyList<MemoryProviderInstanceId> candidateProviderIds) =>
        new(
            status,
            reason,
            SelectedProvider: null,
            requiredCapability,
            DispatchAllowed: false,
            diagnostic,
            candidateProviderIds);
}
