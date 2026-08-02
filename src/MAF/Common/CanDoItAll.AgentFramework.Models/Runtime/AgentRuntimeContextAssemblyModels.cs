namespace CanDoItAll.AgentFramework.Models;

public enum AgentRuntimeContextSourceDecision
{
    Included,
    Excluded
}

public static class AgentRuntimeContextSourceCategories
{
    public const string AgentInstructions = "agent-instructions";
    public const string InputMessages = "input-messages";
    public const string Memory = "memory";
    public const string ContextContributor = "context-contributor";
    public const string Skills = "skills";
    public const string WorkspaceTools = "workspace-tools";
    public const string RuntimeToolProvider = "runtime-tool-provider";
    public const string A2ARemoteAgents = "a2a-remote-agents";
    public const string CatalogCapability = "catalog-capability";
    public const string Compaction = "compaction";
    public const string FrameworkTool = "framework-tool";
}

public enum AgentRuntimeContextPurpose
{
    Unspecified = 0,
    InteractiveChat = 1,
    GovernedProcessAutomation = 2,
    AutoApprovedNonInteractive = 3,
    A2AEndpoint = 4
}

public sealed record AgentRuntimeContextIntent(
    string SourceKind,
    string SourceId,
    string ProcessRunId,
    string ProcessStepId,
    string TargetScope,
    bool IsGovernedProcessStep,
    bool BrowserToolsAllowed,
    bool AllowsProductMutation,
    AgentWorkspaceToolProfileKind? WorkspaceToolProfile,
    WorkspaceScopeDescriptor? WorkspaceScope,
    IReadOnlyList<string> AllowedOperations,
    bool RuntimeToolProvidersEnabled = true,
    bool WorkspaceToolsEnabled = true,
    AgentRuntimeCapabilityScopeOverride? CapabilityScopeOverride = null)
{
    public bool ToolCapabilitiesEnabled { get; init; } = true;

    public AgentRuntimeContextPurpose Purpose { get; init; } = AgentRuntimeContextPurpose.Unspecified;

    public static AgentRuntimeContextIntent Empty { get; } = new(
        SourceKind: string.Empty,
        SourceId: string.Empty,
        ProcessRunId: string.Empty,
        ProcessStepId: string.Empty,
        TargetScope: string.Empty,
        IsGovernedProcessStep: false,
        BrowserToolsAllowed: true,
        AllowsProductMutation: true,
        WorkspaceToolProfile: null,
        WorkspaceScope: null,
        AllowedOperations: [],
        RuntimeToolProvidersEnabled: true,
        WorkspaceToolsEnabled: true,
        CapabilityScopeOverride: null);
}

public sealed record AgentRuntimeContextManifestTotals(
    int InputMessageCount,
    int InputMessageChars,
    int InputMessageEstimatedTokens,
    int ToolCount,
    int ToolSchemaEstimatedChars,
    int ToolSchemaEstimatedTokens,
    int ContextProviderCount,
    int FrameworkToolCount,
    int RuntimeToolProviderCount,
    int EstimatedInputTokens);

public sealed record AgentRuntimeContextManifestSource(
    string Category,
    string SourceId,
    AgentRuntimeContextSourceDecision Decision,
    string Reason,
    int ItemCount,
    int EstimatedChars,
    int EstimatedTokens)
{
    public static AgentRuntimeContextManifestSource Included(
        string category,
        string sourceId,
        string reason,
        int itemCount,
        int estimatedChars = 0)
        => new(
            Normalize(category),
            Normalize(sourceId),
            AgentRuntimeContextSourceDecision.Included,
            Normalize(reason),
            Math.Max(0, itemCount),
            Math.Max(0, estimatedChars),
            EstimateTokens(estimatedChars));

    public static AgentRuntimeContextManifestSource Excluded(
        string category,
        string sourceId,
        string reason)
        => new(
            Normalize(category),
            Normalize(sourceId),
            AgentRuntimeContextSourceDecision.Excluded,
            Normalize(reason),
            ItemCount: 0,
            EstimatedChars: 0,
            EstimatedTokens: 0);

    private static string Normalize(string value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static int EstimateTokens(int estimatedChars)
        => estimatedChars <= 0 ? 0 : Math.Max(1, estimatedChars / 4);
}

public sealed record AgentRuntimeContextAssemblyManifest(
    Guid Id,
    DateTimeOffset CreatedAtUtc,
    Guid AgentId,
    string AgentName,
    string ProviderName,
    ProviderKind ProviderKind,
    string Model,
    ProviderTransportKind TransportKind,
    AgentRuntimeContextIntent Intent,
    AgentRuntimeContextManifestTotals Totals,
    IReadOnlyList<AgentRuntimeContextManifestSource> Sources);
