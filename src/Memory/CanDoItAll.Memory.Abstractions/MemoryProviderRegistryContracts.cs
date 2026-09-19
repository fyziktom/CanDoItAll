namespace CanDoItAll.Memory.Abstractions;

/// <summary>
/// Driver that connects the host to a memory provider. In the memory-provider HTTP API it is a PascalCase string token:
/// <c>Http</c> (a provider speaking the host's HTTP memory protocol), <c>Mcp</c> (a remote Model Context Protocol
/// server), <c>NativeRemote</c> (a remote Cognitive Memory service reached over HTTP), <c>Mock</c> (a deterministic
/// built-in provider for tests and demonstrations) or <c>InProcessMigration</c> (an internal migration driver that the
/// HTTP API cannot configure).
/// </summary>
public enum MemoryProviderDriverKind
{
    Http = 0,
    Mcp = 1,
    NativeRemote = 2,
    Mock = 3,
    InProcessMigration = 4
}

/// <summary>
/// Last recorded health of a memory provider. In the memory-provider HTTP API it is a PascalCase string token:
/// <c>Unknown</c> (not checked yet), <c>Healthy</c>, <c>Degraded</c> or <c>Unreachable</c>.
/// </summary>
public enum MemoryProviderHealthState
{
    Unknown = 0,
    Healthy = 1,
    Degraded = 2,
    Unreachable = 3
}

/// <summary>
/// Workspaces a memory provider serves. In the memory-provider HTTP API it is a PascalCase string token:
/// <c>AllWorkspaces</c> or <c>SingleWorkspace</c> (never selected, because the host cannot verify the workspace).
/// </summary>
public enum MemoryProviderWorkspaceScope
{
    AllWorkspaces = 0,
    SingleWorkspace = 1
}

/// <summary>
/// Whether a memory provider may be used as the implicit default provider. In the memory-provider HTTP API it is a
/// PascalCase string token: <c>DenyImplicitFallback</c> (only when chosen explicitly or by an assignment) or
/// <c>AllowDefaultProviderWhenNoAssignment</c> (also as the default when no assignment applies).
/// </summary>
public enum MemoryProviderFallbackBehavior
{
    DenyImplicitFallback = 0,
    AllowDefaultProviderWhenNoAssignment = 1
}

/// <summary>
/// What the key of a memory provider assignment identifies, as a JSON integer: 0 Agent (the agent identifier), 1
/// AgentRole (the agent's workload name), 2 Workflow (the workflow identifier), 3 WorkflowNode (the workflow node
/// identifier), 4 Process (the process identifier).
/// </summary>
public enum MemoryProviderAssignmentScope
{
    Agent = 0,
    AgentRole = 1,
    Workflow = 2,
    WorkflowNode = 3,
    Process = 4
}

/// <summary>
/// Result of choosing a memory provider for an operation. In the memory-provider HTTP API it is a PascalCase string
/// token: <c>Selected</c>, <c>NoProviderConfigured</c>, <c>NoEnabledProvider</c>, <c>ProviderNotFound</c>,
/// <c>ProviderDisabled</c>, <c>CapabilityUnavailable</c> (the provider does not advertise the required capability),
/// <c>CapabilityDenied</c> (policy denies the capability), <c>ProviderDenied</c> (policy or workspace scope excludes
/// the provider, or the caller may not access the operation), <c>ProviderSelectionRequired</c> (no provider was named
/// and no default applies) or <c>ProviderConfigurationFailed</c> (the provider configuration could not be loaded).
/// </summary>
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

/// <summary>
/// Why a memory provider was considered for an operation. In the memory-provider HTTP API it is a PascalCase string
/// token: <c>None</c>, <c>ExplicitProvider</c> (named by the request), <c>AssignmentOverride</c> (assigned to the
/// agent, workflow or process) or <c>DefaultProvider</c>.
/// </summary>
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
