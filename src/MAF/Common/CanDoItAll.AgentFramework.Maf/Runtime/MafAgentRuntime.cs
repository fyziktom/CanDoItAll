using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Runtime.Abstractions;
using Microsoft.Agents.AI;

namespace CanDoItAll.AgentFramework.Maf;

/// <summary>
/// Composition root for one native MAF runtime port set per runtime scope, built from
/// <see cref="MafAgentRuntimeDependencies"/>. SB18 deleted the broad legacy runtime interface this
/// type used to implement; every composition site (Hosting, the Modules.AgentFramework module
/// registrations, and <c>CanDoItAllAgentWorkspaceFactory</c>) now consumes the four port
/// properties directly. No streaming, session, finalizer, or response-assembly logic lives here.
/// </summary>
public sealed class MafAgentRuntime
{
    private readonly HistoryAgentRuntime historyRuntime;
    private readonly MafProviderDiagnosticsAdapter diagnosticsAdapter;
    private readonly MafProviderModelAdministrationAdapter modelAdministrationAdapter;
    private readonly MafHostedAgentFactory hostedAgentFactory;

    internal MafAgentRuntime(
        string workspaceRoot,
        WorkspaceScopeDescriptor? workspaceScope,
        MafAgentRuntimeDependencies dependencies)
    {
        if (string.IsNullOrWhiteSpace(workspaceRoot))
        {
            throw new ArgumentException("Workspace root must be provided.", nameof(workspaceRoot));
        }

        ArgumentNullException.ThrowIfNull(dependencies);

        var normalizedWorkspaceRoot = Path.GetFullPath(workspaceRoot);
        var resolvedWorkspaceScope = workspaceScope ?? WorkspaceScopeDescriptor.Sandbox;
        var runtimeCapabilityComposer = new RuntimeCapabilityComposer(
            normalizedWorkspaceRoot,
            resolvedWorkspaceScope,
            dependencies.ProviderCredentialService,
            dependencies.ImageAnalysisService,
            dependencies.RuntimeToolProviderComposer,
            dependencies.CompositionMetrics,
            dependencies.PhysicalPathPolicyFactory,
            dependencies.SpreadsheetDocumentService,
            dependencies.CapabilityDependencies);
        var runtimeAgentFactory = new MafRuntimeAgentFactory(
            normalizedWorkspaceRoot,
            resolvedWorkspaceScope,
            dependencies.ProviderCredentialService,
            dependencies.ProviderAgentFactory,
            runtimeCapabilityComposer,
            dependencies.PhysicalPathPolicyFactory,
            dependencies.CapabilityDependencies.LoggerFactory,
            dependencies.ToolInvocationPolicyPipeline);
        var streamingTurnExecutor = new MafStreamingTurnExecutor(
            normalizedWorkspaceRoot,
            resolvedWorkspaceScope,
            dependencies.ProviderAgentFactory,
            dependencies.ApprovalContinuationDriver,
            dependencies.SessionPersistenceDriver,
            dependencies.PhysicalPathPolicyFactory,
            dependencies.ExecutionOutcomeRecoveryPolicies);
        var executionAdapter = new MafAgentExecutionAdapter(
            normalizedWorkspaceRoot,
            resolvedWorkspaceScope,
            dependencies.PhysicalPathPolicyFactory,
            dependencies.WorkspaceRuntimeServicesFactory,
            runtimeAgentFactory,
            new InputAttachmentPreparer(
                dependencies.ProviderCredentialService,
                dependencies.ProviderRuntimeGateway),
            streamingTurnExecutor);
        var continuationAdapter = new MafAgentContinuationAdapter(
            normalizedWorkspaceRoot,
            resolvedWorkspaceScope,
            dependencies.PhysicalPathPolicyFactory,
            dependencies.WorkspaceRuntimeServicesFactory,
            runtimeAgentFactory,
            dependencies.ApprovalContinuationDriver,
            streamingTurnExecutor);
        historyRuntime = new HistoryAgentRuntime(executionAdapter, continuationAdapter);
        diagnosticsAdapter = new MafProviderDiagnosticsAdapter(dependencies.ProviderRuntimeGateway);
        modelAdministrationAdapter = new MafProviderModelAdministrationAdapter(dependencies.ProviderRuntimeGateway);
        hostedAgentFactory = new MafHostedAgentFactory(
            normalizedWorkspaceRoot,
            resolvedWorkspaceScope,
            dependencies.PhysicalPathPolicyFactory,
            dependencies.WorkspaceRuntimeServicesFactory,
            runtimeAgentFactory);
    }

    public MafAgentRuntime(
        string workspaceRoot,
        IServiceProvider serviceProvider,
        WorkspaceScopeDescriptor? workspaceScope = null,
        IWorkspaceRuntimeServicesFactory? workspaceRuntimeServicesFactory = null)
        : this(
            workspaceRoot,
            workspaceScope,
            CreateDependencies(serviceProvider, workspaceRuntimeServicesFactory))
    {
    }

    private static MafAgentRuntimeDependencies CreateDependencies(
        IServiceProvider serviceProvider,
        IWorkspaceRuntimeServicesFactory? workspaceRuntimeServicesFactoryOverride)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        var dependencies = MafAgentRuntimeDependencies.FromServices(serviceProvider);
        return workspaceRuntimeServicesFactoryOverride is null
            ? dependencies
            : dependencies with { WorkspaceRuntimeServicesFactory = workspaceRuntimeServicesFactoryOverride };
    }

    /// <summary>The native execution port served by this runtime composition.</summary>
    public IAgentExecutionRuntime ExecutionPort => historyRuntime;

    /// <summary>The native continuation port served by this runtime composition.</summary>
    public IAgentContinuationRuntime ContinuationPort => historyRuntime;

    /// <summary>The native provider diagnostics port served by this runtime composition.</summary>
    public IProviderDiagnosticsRuntime DiagnosticsPort => diagnosticsAdapter;

    /// <summary>The native provider model administration port served by this runtime composition.</summary>
    public IProviderModelAdministrationRuntime ModelAdministrationPort => modelAdministrationAdapter;

    public Task<AIAgent> CreateHostedAgentAsync(
        AgentDefinition agent,
        ProviderProfile provider,
        IReadOnlyList<CapabilityCatalogItem> capabilities,
        IReadOnlyList<AgentMemoryRecord> memory,
        CancellationToken cancellationToken = default,
        bool suppressApprovalRequirements = false,
        bool forceOmitTemperature = false,
        AgentRuntimeExecutionOptions? executionOptions = null)
        => hostedAgentFactory.CreateHostedAgentAsync(
            agent,
            provider,
            capabilities,
            memory,
            cancellationToken,
            suppressApprovalRequirements,
            forceOmitTemperature,
            executionOptions);
}
