using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Runtime.Abstractions;
using CanDoItAll.Infrastructure.FileSystem;

namespace CanDoItAll.AgentFramework.Maf;

/// <summary>
/// Native MAF implementation of <see cref="IAgentExecutionRuntime"/>. Owns the full prompt-run
/// algorithm: request-scoped input attachment preparation, per-run workspace bundle creation,
/// runtime build acquisition, session restore/create, and the shared streaming turn (including
/// finalizer repair and recovery) through <see cref="MafStreamingTurnExecutor"/>.
/// </summary>
internal sealed class MafAgentExecutionAdapter : IAgentExecutionRuntime
{
    private readonly string workspaceRoot;
    private readonly PhysicalFileSystemCaseSensitivity workspaceRootCaseSensitivity;
    private readonly WorkspaceScopeDescriptor workspaceScope;
    private readonly IWorkspaceRuntimeServicesFactory workspaceRuntimeServicesFactory;
    private readonly MafRuntimeAgentFactory runtimeAgentFactory;
    private readonly InputAttachmentPreparer inputAttachmentPreparer;
    private readonly MafStreamingTurnExecutor streamingTurnExecutor;

    public MafAgentExecutionAdapter(
        string workspaceRoot,
        WorkspaceScopeDescriptor workspaceScope,
        IPhysicalFileSystemPathPolicyFactory physicalPathPolicyFactory,
        IWorkspaceRuntimeServicesFactory workspaceRuntimeServicesFactory,
        MafRuntimeAgentFactory runtimeAgentFactory,
        InputAttachmentPreparer inputAttachmentPreparer,
        MafStreamingTurnExecutor streamingTurnExecutor)
    {
        if (string.IsNullOrWhiteSpace(workspaceRoot))
        {
            throw new ArgumentException("Workspace root must be provided.", nameof(workspaceRoot));
        }

        this.workspaceRoot = MafRuntimePathResolver.ResolvePathFromWorkspace(
            workspaceRoot,
            string.Empty,
            allowExternal: false,
            physicalPathPolicyFactory: physicalPathPolicyFactory);
        workspaceRootCaseSensitivity = physicalPathPolicyFactory.Create(this.workspaceRoot).CaseSensitivity;
        this.workspaceScope = workspaceScope ?? throw new ArgumentNullException(nameof(workspaceScope));
        this.workspaceRuntimeServicesFactory = workspaceRuntimeServicesFactory ?? throw new ArgumentNullException(nameof(workspaceRuntimeServicesFactory));
        this.runtimeAgentFactory = runtimeAgentFactory ?? throw new ArgumentNullException(nameof(runtimeAgentFactory));
        this.inputAttachmentPreparer = inputAttachmentPreparer ?? throw new ArgumentNullException(nameof(inputAttachmentPreparer));
        this.streamingTurnExecutor = streamingTurnExecutor ?? throw new ArgumentNullException(nameof(streamingTurnExecutor));
    }

    public async Task<AgentRuntimeResponse> ExecuteAsync(
        AgentRuntimeExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var model = MafModelParametersBuilder.ResolveRuntimeModel(request.Agent, request.Provider);
        try
        {
            return await ExecuteCoreAsync(request, cancellationToken, forceOmitTemperature: false);
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
                return await ExecuteCoreAsync(request, cancellationToken, forceOmitTemperature: true);
            }
            catch (MafProviderConfigurationException configurationException)
            {
                throw MafRuntimeRequestHelpers.CreateProviderConfigurationUsageException(configurationException);
            }
        }
    }

    private async Task<AgentRuntimeResponse> ExecuteCoreAsync(
        AgentRuntimeExecutionRequest request,
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
        var preparedInput = await inputAttachmentPreparer.PrepareAsync(
            agent,
            provider,
            request.Prompt,
            runtimeOptions,
            progressCallback,
            cancellationToken);
        var prompt = preparedInput.Prompt;
        runtimeOptions = preparedInput.RuntimeOptions;
        await progressCallback(ExecutionState.Preparing, "Framework", "Composing the Microsoft Agent Framework runtime for the selected provider and capabilities.");
        if (suppressApprovalRequirements)
        {
            await progressCallback(ExecutionState.Preparing, "Approval policy", "Auto-approve is active for this run, so future tool approval gates will be suppressed.");
        }

        var effectiveWorkspaceScope = MafRuntimeAgentFactory.ResolveContextWorkspaceScope(
            runtimeOptions,
            workspaceScope);
        var workspaceRuntimeServices = workspaceRuntimeServicesFactory.Create(
            WorkspaceExecutionScope.ForRun(
                workspaceRoot,
                effectiveWorkspaceScope,
                runtimeOptions.Governance,
                externalTargetRootBindings: ExternalTargetRootBindingScope.Resolve(agent),
                rootCaseSensitivity: workspaceRootCaseSensitivity));
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
            InputAttachmentSupport.EnsureSupported(runtimeBuild.Provider, runtimeBuild.Model, runtimeOptions);

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

            await progressCallback(ExecutionState.Preparing, "Session", MafRuntimeSessionBuilder.ResolveSessionMessage(agent, runtimeBuild.Provider, runtimeBuild.Model, session, runtimeOptions));
            var runtimeSession = await MafRuntimeSessionBuilder.RestoreOrCreateSessionAsync(
                runtimeBuild.Agent,
                agent,
                runtimeBuild.Provider,
                runtimeBuild.Model,
                session,
                runtimeOptions,
                cancellationToken,
                isApprovalContinuation: false,
                progressCallback);
            var runOptions = MafRuntimeSessionBuilder.CreateRunOptions(
                agent,
                runtimeBuild.Provider,
                runtimeBuild.Model,
                runtimeBuild.HasApprovalTools,
                continuationToken: null,
                forceOmitTemperature: forceOmitTemperature,
                runtimeOptions);
            var inputMessages = MafRuntimeSessionBuilder.CreatePromptInputMessages(agent, runtimeBuild.Provider, runtimeBuild.Model, session, prompt, runtimeOptions).ToList();
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

            var response = await streamingTurnExecutor.ExecuteTurnAsync(
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

            return MafRuntimeResponseMapper.AttachPreparedInputUsageObservations(response, preparedInput.UsageObservations);
        }
    }
}
