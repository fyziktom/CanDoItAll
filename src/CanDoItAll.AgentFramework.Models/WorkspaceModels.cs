namespace CanDoItAll.AgentFramework.Models;

public sealed record SandboxWorkspaceDocument(
    string Version,
    IReadOnlyList<AgentDefinition> Agents,
    IReadOnlyList<ProviderProfile> Providers,
    IReadOnlyList<CapabilityCatalogItem> Capabilities,
    IReadOnlyList<ChatSessionRecord> ChatSessions,
    IReadOnlyList<ExecutionLogEntry> ExecutionLog,
    IReadOnlyList<AgentRunMetric> Metrics,
    IReadOnlyList<AgentMemoryRecord> Memory)
{
    public IReadOnlyList<ExecutionRunRecord> ExecutionRuns { get; init; } = [];
    public IReadOnlyList<ExecutionApprovalRecord> ExecutionApprovals { get; init; } = [];
    public IReadOnlyList<ExecutionArtifactRecord> ExecutionArtifacts { get; init; } = [];
    public IReadOnlyList<ExecutionWorkflowCheckpointRecord> ExecutionWorkflowCheckpoints { get; init; } = [];
    public IReadOnlyList<ToolExecutionReceiptRecord> ToolExecutionReceipts { get; init; } = [];

    public static SandboxWorkspaceDocument Empty { get; } = new(
        Version: "1.0",
        Agents: [],
        Providers: [],
        Capabilities: [],
        ChatSessions: [],
        ExecutionLog: [],
        Metrics: [],
        Memory: []);

    public SandboxWorkspaceCatalog ToCatalog()
    {
        return new SandboxWorkspaceCatalog(
            Version,
            Agents,
            Providers,
            Capabilities,
            Memory);
    }

    public SandboxWorkspaceExecutionState ToExecutionState()
    {
        return new SandboxWorkspaceExecutionState(
            Version,
            ChatSessions,
            ExecutionLog,
            Metrics)
        {
            ExecutionRuns = ExecutionRuns,
            ExecutionApprovals = ExecutionApprovals,
            ExecutionArtifacts = ExecutionArtifacts,
            ExecutionWorkflowCheckpoints = ExecutionWorkflowCheckpoints,
            ToolExecutionReceipts = ToolExecutionReceipts
        };
    }

    public static SandboxWorkspaceDocument Combine(
        SandboxWorkspaceCatalog catalog,
        SandboxWorkspaceExecutionState executionState)
    {
        var version = string.IsNullOrWhiteSpace(catalog.Version)
            ? executionState.Version
            : catalog.Version;

        return new SandboxWorkspaceDocument(
            Version: string.IsNullOrWhiteSpace(version) ? "1.0" : version,
            Agents: catalog.Agents,
            Providers: catalog.Providers,
            Capabilities: catalog.Capabilities,
            ChatSessions: executionState.ChatSessions,
            ExecutionLog: executionState.ExecutionLog,
            Metrics: executionState.Metrics,
            Memory: catalog.Memory)
        {
            ExecutionRuns = executionState.ExecutionRuns,
            ExecutionApprovals = executionState.ExecutionApprovals,
            ExecutionArtifacts = executionState.ExecutionArtifacts,
            ExecutionWorkflowCheckpoints = executionState.ExecutionWorkflowCheckpoints,
            ToolExecutionReceipts = executionState.ToolExecutionReceipts
        };
    }
}

public sealed record SandboxWorkspaceCatalog(
    string Version,
    IReadOnlyList<AgentDefinition> Agents,
    IReadOnlyList<ProviderProfile> Providers,
    IReadOnlyList<CapabilityCatalogItem> Capabilities,
    IReadOnlyList<AgentMemoryRecord> Memory)
{
    public static SandboxWorkspaceCatalog Empty { get; } = new(
        Version: "1.0",
        Agents: [],
        Providers: [],
        Capabilities: [],
        Memory: []);
}

public sealed record SandboxWorkspaceExecutionState(
    string Version,
    IReadOnlyList<ChatSessionRecord> ChatSessions,
    IReadOnlyList<ExecutionLogEntry> ExecutionLog,
    IReadOnlyList<AgentRunMetric> Metrics)
{
    public IReadOnlyList<ExecutionRunRecord> ExecutionRuns { get; init; } = [];
    public IReadOnlyList<ExecutionApprovalRecord> ExecutionApprovals { get; init; } = [];
    public IReadOnlyList<ExecutionArtifactRecord> ExecutionArtifacts { get; init; } = [];
    public IReadOnlyList<ExecutionWorkflowCheckpointRecord> ExecutionWorkflowCheckpoints { get; init; } = [];
    public IReadOnlyList<ToolExecutionReceiptRecord> ToolExecutionReceipts { get; init; } = [];

    public static SandboxWorkspaceExecutionState Empty { get; } = new(
        Version: "1.0",
        ChatSessions: [],
        ExecutionLog: [],
        Metrics: []);
}

public sealed record SandboxDashboardSnapshot(
    int AgentCount,
    int TemplateCount,
    int ProviderCount,
    int CapabilityCount,
    int SessionCount,
    int MemoryCount,
    int ActiveRuns,
    int FailedRuns,
    ExecutionBoundaryDescriptor ToolExecutionBoundary);

public sealed record SelectOption(string Value, string Text);
