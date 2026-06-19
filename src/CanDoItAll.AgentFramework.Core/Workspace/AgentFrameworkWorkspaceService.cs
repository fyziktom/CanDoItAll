using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed partial class AgentFrameworkWorkspaceService : IAgentFrameworkWorkspaceService
{
    private readonly ISandboxWorkspaceStore store;
    private readonly AgentFrameworkWorkspaceCatalogService catalogService;
    private readonly AgentFrameworkWorkspaceExecutionService executionService;
    private readonly ExecutionBoundaryDescriptor toolExecutionBoundary;

    public AgentFrameworkWorkspaceService(
        ISandboxWorkspaceStore store,
        IAgentPackageService packageService,
        IAgentRuntime runtime,
        ICapabilityProofService capabilityProofService,
        IProviderProfileService? providerProfileService = null,
        IProviderProfileRegistry? providerProfileRegistry = null,
        IAgentProviderCredentialResolver? providerCredentialResolver = null,
        IProviderDiagnosticsService? providerDiagnosticsService = null,
        IAgentExecutionGovernanceBridge? executionGovernanceBridge = null,
        IAgentExecutionEventSink? executionEventSink = null,
        IAgentExecutionCheckpointBridge? executionCheckpointBridge = null,
        IWorkspaceProcessHost? workspaceProcessHost = null,
        IAgentExecutionCancellationRegistry? executionCancellationRegistry = null,
        IAgentOutputRepairService? outputRepairService = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(packageService);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(capabilityProofService);

        this.store = store;

        var resolvedProviderProfileService = providerProfileService ?? new ProviderProfileService();
        var resolvedProviderProfileRegistry = providerProfileRegistry ?? new WorkspaceBackedProviderProfileRegistry(store, resolvedProviderProfileService);
        var resolvedProviderCredentialResolver = providerCredentialResolver ?? new EnvironmentVariableAgentProviderCredentialResolver();
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
            resolvedProviderProfileRegistry,
            resolvedProviderCredentialResolver,
            executionCancellationRegistry,
            outputRepairService);

        executionService.ExecutionUpdated += (_, entry) => ExecutionUpdated?.Invoke(this, entry);
    }

    public event EventHandler<ExecutionLogEntry>? ExecutionUpdated;

    public async Task<SandboxDashboardSnapshot> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        var catalogTask = store.LoadCatalogAsync(cancellationToken);
        var executionSummaryTask = store.LoadExecutionSummaryAsync(cancellationToken);
        await Task.WhenAll(catalogTask, executionSummaryTask);

        var catalog = await catalogTask;
        var executionSummary = await executionSummaryTask;

        return new SandboxDashboardSnapshot(
            AgentCount: catalog.Agents.Count(item => !item.IsTemplate),
            TemplateCount: catalog.Agents.Count(item => item.IsTemplate),
            ProviderCount: catalog.Providers.Count,
            CapabilityCount: catalog.Capabilities.Count,
            SessionCount: executionSummary.SessionCount,
            MemoryCount: catalog.Memory.Count,
            ActiveRuns: executionSummary.ActiveRuns,
            FailedRuns: executionSummary.FailedRuns,
            ToolExecutionBoundary: toolExecutionBoundary);
    }
}
