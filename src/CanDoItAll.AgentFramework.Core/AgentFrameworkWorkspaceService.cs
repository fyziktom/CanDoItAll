using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed partial class AgentFrameworkWorkspaceService : IAgentFrameworkWorkspaceService
{
    private readonly ISandboxWorkspaceStore store;
    private readonly AgentFrameworkWorkspaceCatalogService catalogService;
    private readonly AgentFrameworkWorkspaceExecutionService executionService;
    private readonly IProviderProfileRegistry providerProfileRegistry;
    private readonly ExecutionBoundaryDescriptor toolExecutionBoundary;

    public AgentFrameworkWorkspaceService(
        ISandboxWorkspaceStore store,
        IAgentPackageService packageService,
        IAgentRuntime runtime,
        ICapabilityProofService capabilityProofService,
        IProviderProfileService? providerProfileService = null,
        IProviderProfileRegistry? providerProfileRegistry = null,
        IProviderDiagnosticsService? providerDiagnosticsService = null,
        IAgentExecutionGovernanceBridge? executionGovernanceBridge = null,
        IAgentExecutionEventSink? executionEventSink = null,
        IAgentExecutionCheckpointBridge? executionCheckpointBridge = null,
        IWorkspaceProcessHost? workspaceProcessHost = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(packageService);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(capabilityProofService);

        this.store = store;

        var resolvedProviderProfileService = providerProfileService ?? new ProviderProfileService();
        var resolvedProviderProfileRegistry = providerProfileRegistry ?? new WorkspaceBackedProviderProfileRegistry(store, resolvedProviderProfileService);
        this.providerProfileRegistry = resolvedProviderProfileRegistry;
        var resolvedProviderDiagnosticsService = providerDiagnosticsService ?? new ProviderDiagnosticsService(runtime);
        var resolvedExecutionGovernanceBridge = executionGovernanceBridge ?? new NullAgentExecutionGovernanceBridge();
        var resolvedExecutionEventSink = executionEventSink ?? new NullAgentExecutionEventSink();
        var resolvedExecutionCheckpointBridge = executionCheckpointBridge ?? new NullAgentExecutionCheckpointBridge();
        toolExecutionBoundary = workspaceProcessHost?.DescribeBoundary() ?? ExecutionBoundaryDescriptor.Unknown;

        catalogService = new AgentFrameworkWorkspaceCatalogService(
            store,
            packageService,
            capabilityProofService,
            resolvedProviderProfileService,
            resolvedProviderDiagnosticsService,
            resolvedProviderProfileRegistry);

        executionService = new AgentFrameworkWorkspaceExecutionService(
            store,
            runtime,
            resolvedExecutionGovernanceBridge,
            resolvedExecutionEventSink,
            resolvedExecutionCheckpointBridge,
            resolvedProviderProfileRegistry);

        executionService.ExecutionUpdated += (_, entry) => ExecutionUpdated?.Invoke(this, entry);
    }

    public event EventHandler<ExecutionLogEntry>? ExecutionUpdated;

    public async Task<SandboxDashboardSnapshot> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        var document = await store.LoadAsync(cancellationToken);
        var catalog = document.ToCatalog();
        var executionState = document.ToExecutionState();
        var providers = await providerProfileRegistry.ListProvidersAsync(cancellationToken);
        var recentWindow = DateTimeOffset.UtcNow.AddHours(-1);

        return new SandboxDashboardSnapshot(
            AgentCount: catalog.Agents.Count(item => !item.IsTemplate),
            TemplateCount: catalog.Agents.Count(item => item.IsTemplate),
            ProviderCount: providers.Count,
            CapabilityCount: catalog.Capabilities.Count,
            SessionCount: executionState.ChatSessions.Count,
            MemoryCount: catalog.Memory.Count,
            ActiveRuns: executionState.ExecutionRuns.Count(item =>
                item.UpdatedAtUtc >= recentWindow &&
                item.State is ExecutionState.Preparing or ExecutionState.Running or ExecutionState.WaitingOnTool or ExecutionState.Persisting),
            FailedRuns: executionState.ExecutionRuns.Count(item => item.Outcome == RunOutcome.Failed),
            ToolExecutionBoundary: toolExecutionBoundary);
    }
}
