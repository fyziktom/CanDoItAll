using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Runtime.Abstractions;

namespace CanDoItAll.AgentFramework.Maf;

/// <summary>
/// Native MAF implementation of <see cref="IAgentContinuationRuntime"/>. Owns the pending-approval
/// continuation algorithm: it passes per-proposal decisions through unmangled to
/// <see cref="IMafApprovalContinuationDriver"/> (SB15), evaluates runtime-state compatibility
/// before restoring session state, and reuses the shared streaming turn.
/// </summary>
internal sealed class MafAgentContinuationAdapter : IAgentContinuationRuntime
{
    private readonly string workspaceRoot;
    private readonly WorkspaceScopeDescriptor workspaceScope;
    private readonly IWorkspaceRuntimeServicesFactory workspaceRuntimeServicesFactory;
    private readonly MafRuntimeAgentFactory runtimeAgentFactory;
    private readonly IMafApprovalContinuationDriver approvalContinuationDriver;
    private readonly MafStreamingTurnExecutor streamingTurnExecutor;

    public MafAgentContinuationAdapter(
        string workspaceRoot,
        WorkspaceScopeDescriptor workspaceScope,
        IWorkspaceRuntimeServicesFactory workspaceRuntimeServicesFactory,
        MafRuntimeAgentFactory runtimeAgentFactory,
        IMafApprovalContinuationDriver approvalContinuationDriver,
        MafStreamingTurnExecutor streamingTurnExecutor)
    {
        if (string.IsNullOrWhiteSpace(workspaceRoot))
        {
            throw new ArgumentException("Workspace root must be provided.", nameof(workspaceRoot));
        }

        this.workspaceRoot = Path.GetFullPath(workspaceRoot);
        this.workspaceScope = workspaceScope ?? throw new ArgumentNullException(nameof(workspaceScope));
        this.workspaceRuntimeServicesFactory = workspaceRuntimeServicesFactory ?? throw new ArgumentNullException(nameof(workspaceRuntimeServicesFactory));
        this.runtimeAgentFactory = runtimeAgentFactory ?? throw new ArgumentNullException(nameof(runtimeAgentFactory));
        this.approvalContinuationDriver = approvalContinuationDriver ?? throw new ArgumentNullException(nameof(approvalContinuationDriver));
        this.streamingTurnExecutor = streamingTurnExecutor ?? throw new ArgumentNullException(nameof(streamingTurnExecutor));
    }

    public async Task<AgentRuntimeResponse> ContinueAsync(
        AgentRuntimeContinuationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var decisions = request.Decisions;
        var model = MafModelParametersBuilder.ResolveRuntimeModel(request.Agent, request.Provider);
        try
        {
            return await ContinueCoreAsync(request, decisions, cancellationToken, forceOmitTemperature: false);
        }
        catch (MafProviderConfigurationException exception)
        {
            throw MafRuntimeRequestHelpers.CreateProviderConfigurationUsageException(exception);
        }
        catch (Exception exception) when (MafModelParametersBuilder.ShouldRetryWithoutTemperature(request.Provider, model, exception))
        {
            await request.ProgressCallback(ExecutionState.Preparing, "Model parameters", MafModelParametersBuilder.BuildTemperatureRetryMessage(model));
            try
            {
                return await ContinueCoreAsync(request, decisions, cancellationToken, forceOmitTemperature: true);
            }
            catch (MafProviderConfigurationException configurationException)
            {
                throw MafRuntimeRequestHelpers.CreateProviderConfigurationUsageException(configurationException);
            }
        }
    }

    private async Task<AgentRuntimeResponse> ContinueCoreAsync(
        AgentRuntimeContinuationRequest request,
        IReadOnlyList<AgentRuntimeApprovalDecision> decisions,
        CancellationToken cancellationToken,
        bool forceOmitTemperature)
    {
        var agent = request.Agent;
        var provider = request.Provider;
        var session = request.Session;
        var runtimeSessionKey = request.RuntimeSessionKey;
        var progressCallback = request.ProgressCallback;
        var suppressApprovalRequirements = request.SuppressApprovalRequirements;

        var runtimeOptions = MafRuntimeExecutionOptionsResolver.Normalize(request.StructuredOutput, request.ExecutionOptions);
        await progressCallback(ExecutionState.Preparing, "Framework", "Rehydrating the Microsoft Agent Framework runtime to continue from a pending approval.");
        if (suppressApprovalRequirements)
        {
            await progressCallback(ExecutionState.Preparing, "Approval policy", "Auto-approve remains active, so future tool approval gates will be suppressed after this decision is replayed.");
        }

        var effectiveWorkspaceScope = MafRuntimeAgentFactory.ResolveContextWorkspaceScope(
            runtimeOptions,
            workspaceScope);
        var workspaceRuntimeServices = workspaceRuntimeServicesFactory.Create(
            WorkspaceExecutionScope.ForRun(workspaceRoot, effectiveWorkspaceScope, runtimeOptions.Governance));
        RuntimeBuildResult runtimeBuild;
        try
        {
            runtimeBuild = await runtimeAgentFactory.CreateRuntimeBuildAsync(
                agent,
                provider,
                request.Capabilities,
                request.Memory,
                workspaceRuntimeServices,
                progressCallback,
                cancellationToken,
                suppressApprovalRequirements,
                forceOmitTemperature,
                runtimeOptions,
                MafRuntimeRequestHelpers.ResolveRuntimeToolProviderSessionKey(session, runtimeSessionKey));
        }
        catch
        {
            await workspaceRuntimeServices.DisposeAsync();
            throw;
        }

        return await runtimeBuild.ExecuteWithLifetimeAsync(ExecuteWithBuildAsync);

        async Task<AgentRuntimeResponse> ExecuteWithBuildAsync()
        {
            if (runtimeBuild.IsTemperatureOmitted)
            {
                await progressCallback(ExecutionState.Preparing, "Model parameters", MafModelParametersBuilder.BuildTemperatureOmittedMessage(runtimeBuild.Model));
            }

            // The runtime toolset (and hence its fingerprint) is only definitively known once
            // capability composition has produced runtimeBuild.CapabilityState, so this must run
            // before session restore evaluates runtime-state compatibility.
            var capabilityState = runtimeBuild.CapabilityState;
            runtimeOptions = runtimeOptions with
            {
                ToolsetFingerprint = MafToolsetFingerprint.ComputeContractFingerprint(
                    capabilityState?.Tools ?? []),
                LegacyToolsetNameFingerprint = MafToolsetFingerprint.Compute(
                    (capabilityState?.Tools ?? []).Select(tool => tool.Name)),
                CapabilityPolicyFingerprint = MafToolsetFingerprint.Compute(
                    (capabilityState?.EffectiveCapabilityDescriptors ?? [])
                        .Select(descriptor => descriptor.Identity.ToString())),
                HistoryMode = agent.ChatHistoryMode
            };

            await progressCallback(ExecutionState.Preparing, "Session", "Restoring the session state prior to replaying the approval response.");
            var runtimeSession = await MafRuntimeSessionBuilder.RestoreOrCreateSessionAsync(
                runtimeBuild.Agent,
                agent,
                runtimeBuild.Provider,
                runtimeBuild.Model,
                session,
                runtimeOptions,
                cancellationToken,
                isApprovalContinuation: true,
                progressCallback);
            var runOptions = MafRuntimeSessionBuilder.CreateRunOptions(
                agent,
                runtimeBuild.Provider,
                runtimeBuild.Model,
                runtimeBuild.HasApprovalTools,
                continuationToken: null,
                forceOmitTemperature: forceOmitTemperature,
                runtimeOptions);
            var inputMessages = approvalContinuationDriver.CreateApprovalInputMessages(session, decisions).ToList();
            var contextManifest = MafContextManifestBuilder.Create(
                agent,
                runtimeBuild.Provider,
                runtimeBuild.Model,
                runtimeOptions,
                capabilityState?.Tools ?? [],
                capabilityState?.ContextSources ?? [],
                capabilityState?.FrameworkToolNames ?? [],
                capabilityState?.ContextProviders.Count ?? 0,
                capabilityState?.RuntimeToolProviderDescriptors.Count ?? 0,
                inputMessages);

            return await streamingTurnExecutor.ExecuteTurnAsync(
                agent,
                runtimeBuild.Provider,
                runtimeBuild.Model,
                session,
                runtimeBuild.Agent,
                runtimeSession,
                runOptions,
                inputMessages,
                runtimeSessionKey,
                progressCallback,
                cancellationToken,
                runtimeOptions.StructuredOutput,
                runtimeOptions.FinalizerMode,
                runtimeOptions,
                forceOmitTemperature,
                runtimeBuild.FinalizerTools,
                runtimeBuild.ToolInvocationTraceRecorder,
                runtimeBuild.SnapshotFinalizerInvocations,
                runtimeBuild.SnapshotToolInvocationTraces,
                runtimeBuild.SnapshotContextContributionTraces,
                contextManifest,
                runtimeBuild.IsTerminalResponseUpdate,
                runtimeBuild.EntryAgentRequestCompatibilityEvidence);
        }
    }
}
