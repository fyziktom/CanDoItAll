using CanDoItAll.AgentFramework.Core.Execution;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;
using CanDoItAll.AgentFramework.Runtime.Abstractions;
using CanDoItAll.SharedKernel;
using CanDoItAll.Infrastructure.Storage;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.AgentFramework.Core;

public sealed partial class AgentFrameworkWorkspaceService :
    IAgentFrameworkWorkspaceService,
    IAgentFrameworkWorkspaceActivityExecutionService,
    IDisposable
{
    private readonly ISandboxWorkspaceStore store;
    private readonly AgentFrameworkWorkspaceCatalogService catalogService;
    private readonly AgentFrameworkWorkspaceExecutionService executionService;
    private readonly AgentPackageImportService packageImportService;
    private readonly AgentExternalProvisioningService externalProvisioningService;
    private readonly ExecutionBoundaryDescriptor toolExecutionBoundary;
    private readonly ILogger<AgentFrameworkWorkspaceService> logger;
    private readonly IAgentExecutionActivityCoordinator activityCoordinator;
    private readonly AgentExecutionActivityWorkspaceIdentity activityWorkspaceIdentity;
    private readonly IsolatedCompatibilityEventDispatcher<ExecutionLogEntry> executionUpdatedDispatcher;
    private readonly WorkspaceRuntimeServices? ownedWorkspaceBundle;
    private bool disposed;

    public AgentFrameworkWorkspaceService(
        ISandboxWorkspaceStore store,
        IAgentPackageService packageService,
        IAgentExecutionRuntime executionRuntime,
        IAgentContinuationRuntime continuationRuntime,
        IProviderDiagnosticsRuntime providerDiagnosticsRuntime,
        IProviderModelAdministrationRuntime providerModelAdministrationRuntime,
        ICapabilityProofService capabilityProofService,
        ILogger<AgentFrameworkWorkspaceService> logger,
        IAgentExecutionActivityCoordinator activityCoordinator,
        AgentExecutionActivityWorkspaceIdentity activityWorkspaceIdentity,
        IAgentExecutionPreparationCache executionPreparationCache,
        IAgentExecutionProfileGenerationSource executionProfileGenerationSource,
        IWorkspaceExecutionRunProcessLeaseCleaner workspaceProcessLeaseCleaner,
        IExternalTargetPathRegistryFactory externalTargetPathRegistryFactory,
        IProviderProfileService? providerProfileService = null,
        IProviderProfileRegistry? providerProfileRegistry = null,
        IAgentProviderCredentialResolver? providerCredentialResolver = null,
        IProviderDiagnosticsService? providerDiagnosticsService = null,
        IAgentExecutionGovernanceBridge? executionGovernanceBridge = null,
        IAgentExecutionEventSink? executionEventSink = null,
        IAgentExecutionCheckpointBridge? executionCheckpointBridge = null,
        IWorkspaceProcessHost? workspaceProcessHost = null,
        IAgentExecutionCancellationRegistry? executionCancellationRegistry = null,
        IAgentOutputRepairService? outputRepairService = null,
        IWorkspacePathResolutionService? workspacePathResolutionService = null,
        IProviderRuntimeProfileSource? providerRuntimeProfileSource = null,
        IEnumerable<IAgentExecutionProviderSelectionPolicy>? providerSelectionPolicies = null,
        IEnumerable<IAgentExecutionRunCriticalityPolicy>? runCriticalityPolicies = null,
        WorkspaceRuntimeServices? ownedWorkspaceBundle = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(packageService);
        ArgumentNullException.ThrowIfNull(executionRuntime);
        ArgumentNullException.ThrowIfNull(continuationRuntime);
        ArgumentNullException.ThrowIfNull(providerDiagnosticsRuntime);
        ArgumentNullException.ThrowIfNull(providerModelAdministrationRuntime);
        ArgumentNullException.ThrowIfNull(capabilityProofService);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(activityCoordinator);
        ArgumentNullException.ThrowIfNull(activityWorkspaceIdentity);
        ArgumentNullException.ThrowIfNull(executionPreparationCache);
        ArgumentNullException.ThrowIfNull(executionProfileGenerationSource);
        ArgumentNullException.ThrowIfNull(workspaceProcessLeaseCleaner);
        ArgumentNullException.ThrowIfNull(externalTargetPathRegistryFactory);
        this.store = store;
        this.logger = logger;
        this.activityCoordinator = activityCoordinator;
        this.activityWorkspaceIdentity = activityWorkspaceIdentity;
        this.ownedWorkspaceBundle = ownedWorkspaceBundle;
        executionUpdatedDispatcher = CreateExecutionUpdatedDispatcher(logger);

        var resolvedProviderProfileService = providerProfileService ?? new ProviderProfileService();
        var resolvedProviderProfileRegistry = providerProfileRegistry ?? new WorkspaceBackedProviderProfileRegistry(store, resolvedProviderProfileService);
        var resolvedProviderRuntimeProfileSource = providerRuntimeProfileSource
            ?? resolvedProviderProfileRegistry as IProviderRuntimeProfileSource
            ?? throw new InvalidOperationException(
                "The configured provider registry must also expose the canonical runtime provider source.");
        var resolvedProviderRuntimeProfileSnapshotSource =
            resolvedProviderRuntimeProfileSource
                as IProviderRuntimeProfileSnapshotSource
            ?? throw new InvalidOperationException(
                "The configured runtime provider source must expose atomic provider snapshot leases.");
        var resolvedProviderCredentialResolver = providerCredentialResolver ?? new EnvironmentVariableAgentProviderCredentialResolver();
        // SB10: the workspace service consumes the four narrow runtime ports directly. SB18
        // deleted the broad legacy runtime interface and its compatibility facade entirely.
        var resolvedProviderDiagnosticsService = providerDiagnosticsService
            ?? new ProviderDiagnosticsService(providerDiagnosticsRuntime, providerModelAdministrationRuntime);
        var resolvedExecutionGovernanceBridge = executionGovernanceBridge ?? new NullAgentExecutionGovernanceBridge();
        var resolvedExecutionEventSink = executionEventSink ?? new NullAgentExecutionEventSink();
        var resolvedExecutionCheckpointBridge = executionCheckpointBridge ?? new NullAgentExecutionCheckpointBridge();
        toolExecutionBoundary = workspaceProcessHost?.DescribeBoundary() ?? ExecutionBoundaryDescriptor.Unknown;
        packageImportService = new AgentPackageImportService(
            store,
            packageService,
            resolvedProviderProfileService);
        externalProvisioningService = new AgentExternalProvisioningService(
            store,
            resolvedProviderProfileService);
        var executionPreparationService =
            new AgentExecutionPreparationService(
                store,
                resolvedProviderRuntimeProfileSnapshotSource,
                executionPreparationCache,
                executionProfileGenerationSource,
                activityWorkspaceIdentity);

        catalogService = new AgentFrameworkWorkspaceCatalogService(
            store,
            packageService,
            capabilityProofService,
            resolvedProviderProfileService,
            resolvedProviderDiagnosticsService,
            resolvedProviderProfileRegistry,
            resolvedProviderRuntimeProfileSource);

        executionService = new AgentFrameworkWorkspaceExecutionService(
            store,
            store,
            executionRuntime,
            continuationRuntime,
            resolvedExecutionGovernanceBridge,
            resolvedExecutionEventSink,
            resolvedExecutionCheckpointBridge,
            resolvedProviderRuntimeProfileSource,
            resolvedProviderCredentialResolver,
            externalTargetPathRegistryFactory,
            logger,
            activityWorkspaceIdentity,
            executionPreparationService,
            workspaceProcessLeaseCleaner,
            executionCancellationRegistry,
            outputRepairService,
            workspacePathResolutionService,
            providerSelectionPolicies,
            runCriticalityPolicies);

        executionService.ExecutionUpdated += HandleExecutionUpdated;
    }

    public event EventHandler<ExecutionLogEntry>? ExecutionUpdated
    {
        add
        {
            if (value is not null)
            {
                executionUpdatedDispatcher.Subscribe(value);
            }
        }
        remove
        {
            if (value is not null)
            {
                executionUpdatedDispatcher.Unsubscribe(value);
            }
        }
    }

    private void HandleExecutionUpdated(object? sender, ExecutionLogEntry entry)
    {
        executionUpdatedDispatcher.Publish(this, entry);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        executionService.ExecutionUpdated -= HandleExecutionUpdated;
        executionUpdatedDispatcher.Dispose();
        executionService.Dispose();
        // The workspace owner disposes its scope-bound service bundle exactly
        // once; the bundle owns the workspace-level process host and every
        // other scope-bound service it constructed.
        if (ownedWorkspaceBundle is not null)
        {
            ownedWorkspaceBundle.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    private static IsolatedCompatibilityEventDispatcher<ExecutionLogEntry>
        CreateExecutionUpdatedDispatcher(ILogger logger)
    {
        return new(
            failure => logger.LogWarning(
                failure.Exception,
                "ExecutionUpdated subscriber failed for execution run {ExecutionRunId}, agent {AgentId}, chat session {ChatSessionId}, event {ExecutionEventId}, phase {Phase}, and state {ExecutionState}.",
                failure.Event.ExecutionRunId,
                failure.Event.AgentId,
                failure.Event.ChatSessionId,
                failure.Event.Id,
                failure.Event.Phase,
                failure.Event.State),
            overflow => logger.LogWarning(
                "ExecutionUpdated subscriber mailbox overflow dropped {DroppedEventCount} update(s) at capacity {MailboxCapacity} while preserving canonical execution. Latest dropped identity: execution run {ExecutionRunId}, agent {AgentId}, chat session {ChatSessionId}, event {ExecutionEventId}, phase {Phase}, and state {ExecutionState}.",
                overflow.DroppedEventCount,
                overflow.MailboxCapacity,
                overflow.LastDroppedEvent.ExecutionRunId,
                overflow.LastDroppedEvent.AgentId,
                overflow.LastDroppedEvent.ChatSessionId,
                overflow.LastDroppedEvent.Id,
                overflow.LastDroppedEvent.Phase,
                overflow.LastDroppedEvent.State));
    }

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

    public async Task<AgentOverviewSnapshot> GetAgentOverviewAsync(CancellationToken cancellationToken = default)
    {
        var catalogTask = store.LoadCatalogAsync(cancellationToken);
        var executionSummaryTask = store.LoadExecutionSummaryAsync(cancellationToken);
        var usageProjectionTask = store.LoadUsageProjectionAsync(cancellationToken);
        await Task.WhenAll(catalogTask, executionSummaryTask, usageProjectionTask);

        var catalog = await catalogTask;
        var executionSummary = await executionSummaryTask;
        var usageProjection = await usageProjectionTask;
        var totals = CreateOverviewTotals(catalog, executionSummary, usageProjection);
        var agentRows = MapAgentRows(catalog, usageProjection.Agents);

        return new AgentOverviewSnapshot(
            totals,
            agentRows.Take(5).ToList(),
            agentRows
                .Where(item => item.FailedRunCount > 0)
                .OrderByDescending(item => item.FailedRunCount)
                .ThenByDescending(item => item.RunCount)
                .ThenBy(item => item.AgentName, StringComparer.OrdinalIgnoreCase)
                .Take(5)
                .ToList(),
            MapProviderRows(usageProjection.Providers),
            MapModelRows(usageProjection.Models),
            MapTeamShortcutRows(catalog.AgentTeams),
            usageProjection.UpdatedAtUtc,
            toolExecutionBoundary);
    }

    public async Task<AgentUsageDetailSnapshot> GetAgentUsageDetailsAsync(CancellationToken cancellationToken = default)
    {
        var catalogTask = store.LoadCatalogAsync(cancellationToken);
        var executionSummaryTask = store.LoadExecutionSummaryAsync(cancellationToken);
        var usageProjectionTask = store.LoadUsageProjectionAsync(cancellationToken);
        await Task.WhenAll(catalogTask, executionSummaryTask, usageProjectionTask);

        var catalog = await catalogTask;
        var executionSummary = await executionSummaryTask;
        var usageProjection = await usageProjectionTask;
        return new AgentUsageDetailSnapshot(
            MapAgentRows(catalog, usageProjection.Agents),
            CreateOverviewTotals(catalog, executionSummary, usageProjection),
            usageProjection.UpdatedAtUtc);
    }

    public async Task<ProviderUsageDetailSnapshot> GetProviderUsageDetailsAsync(CancellationToken cancellationToken = default)
    {
        var catalogTask = store.LoadCatalogAsync(cancellationToken);
        var executionSummaryTask = store.LoadExecutionSummaryAsync(cancellationToken);
        var usageProjectionTask = store.LoadUsageProjectionAsync(cancellationToken);
        await Task.WhenAll(catalogTask, executionSummaryTask, usageProjectionTask);

        var catalog = await catalogTask;
        var executionSummary = await executionSummaryTask;
        var usageProjection = await usageProjectionTask;
        return new ProviderUsageDetailSnapshot(
            MapProviderRows(usageProjection.Providers),
            CreateOverviewTotals(catalog, executionSummary, usageProjection),
            usageProjection.UpdatedAtUtc);
    }

    public async Task<ModelUsageDetailSnapshot> GetModelUsageDetailsAsync(CancellationToken cancellationToken = default)
    {
        var catalogTask = store.LoadCatalogAsync(cancellationToken);
        var executionSummaryTask = store.LoadExecutionSummaryAsync(cancellationToken);
        var usageProjectionTask = store.LoadUsageProjectionAsync(cancellationToken);
        await Task.WhenAll(catalogTask, executionSummaryTask, usageProjectionTask);

        var catalog = await catalogTask;
        var executionSummary = await executionSummaryTask;
        var usageProjection = await usageProjectionTask;
        return new ModelUsageDetailSnapshot(
            MapModelRows(usageProjection.Models),
            CreateOverviewTotals(catalog, executionSummary, usageProjection),
            usageProjection.UpdatedAtUtc);
    }

    private static AgentOverviewTotals CreateOverviewTotals(
        SandboxWorkspaceCatalog catalog,
        SandboxWorkspaceExecutionSummary executionSummary,
        AgentUsageProjection usageProjection)
    {
        return new AgentOverviewTotals(
            AgentCount: catalog.Agents.Count(item => !item.IsTemplate),
            TemplateCount: catalog.Agents.Count(item => item.IsTemplate),
            TeamCount: catalog.AgentTeams.Count,
            ProviderCount: catalog.Providers.Count,
            CapabilityCount: catalog.Capabilities.Count,
            SessionCount: executionSummary.SessionCount,
            MemoryCount: catalog.Memory.Count,
            ActiveRuns: executionSummary.ActiveRuns,
            FailedRuns: executionSummary.FailedRuns,
            UsageObservationCount: usageProjection.UsageObservationCount,
            KnownUsageObservationCount: usageProjection.KnownUsageObservationCount,
            UnknownUsageObservationCount: usageProjection.UnknownUsageObservationCount,
            InputTokens: usageProjection.InputTokens,
            CachedInputTokens: usageProjection.CachedInputTokens,
            OutputTokens: usageProjection.OutputTokens,
            ReasoningTokens: usageProjection.ReasoningTokens,
            TotalTokens: usageProjection.TotalTokens,
            KnownCostUsd: usageProjection.KnownCostUsd);
    }

    private static IReadOnlyList<AgentOverviewUsageRow> MapAgentRows(
        SandboxWorkspaceCatalog catalog,
        IReadOnlyList<AgentUsageProjectionRow> rows)
    {
        var agentsById = catalog.Agents.ToDictionary(item => item.Id);
        return rows
            .Select(row =>
            {
                agentsById.TryGetValue(row.AgentId, out var agent);
                return new AgentOverviewUsageRow(
                    row.AgentId,
                    agent?.Name ?? "Unknown agent",
                    agent?.AvatarImageUrl,
                    row.RunCount,
                    row.FailedRunCount,
                    row.UsageObservationCount,
                    row.KnownUsageObservationCount,
                    row.UnknownUsageObservationCount,
                    row.InputTokens,
                    row.CachedInputTokens,
                    row.OutputTokens,
                    row.ReasoningTokens,
                    row.TotalTokens,
                    row.KnownCostUsd,
                    row.LastUsedAtUtc);
            })
            .ToList();
    }

    private static IReadOnlyList<AgentTeamOverviewShortcutRow> MapTeamShortcutRows(
        IReadOnlyList<AgentTeamDefinition> teams)
    {
        return teams
            .OrderByDescending(item => item.AgentIds.Count)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Select(item => new AgentTeamOverviewShortcutRow(
                item.Id,
                item.Name,
                item.Description,
                AgentTeamIconCatalog.Normalize(item.Icon),
                item.AgentIds.Count))
            .ToList();
    }

    private static IReadOnlyList<ProviderOverviewUsageRow> MapProviderRows(
        IReadOnlyList<ProviderUsageProjectionRow> rows)
    {
        return rows
            .Select(row => new ProviderOverviewUsageRow(
                row.ProviderName,
                row.ProviderKind,
                row.UsageObservationCount,
                row.KnownUsageObservationCount,
                row.UnknownUsageObservationCount,
                row.InputTokens,
                row.CachedInputTokens,
                row.OutputTokens,
                row.ReasoningTokens,
                row.TotalTokens,
                row.KnownCostUsd,
                row.FailedRunCount,
                row.LastUsedAtUtc))
            .ToList();
    }

    private static IReadOnlyList<ModelOverviewUsageRow> MapModelRows(
        IReadOnlyList<ModelUsageProjectionRow> rows)
    {
        return rows
            .Select(row => new ModelOverviewUsageRow(
                row.ProviderName,
                row.ProviderKind,
                row.Model,
                row.UsageObservationCount,
                row.KnownUsageObservationCount,
                row.UnknownUsageObservationCount,
                row.InputTokens,
                row.CachedInputTokens,
                row.OutputTokens,
                row.ReasoningTokens,
                row.TotalTokens,
                row.KnownCostUsd,
                row.LastUsedAtUtc))
            .ToList();
    }
}
