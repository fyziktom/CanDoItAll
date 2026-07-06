using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace CanDoItAll.AgentFramework.Maf;

public sealed class MafAgentRuntime : IAgentRuntime
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan FinalizerSessionSerializationTimeout = TimeSpan.FromSeconds(5);
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(20)
    };
    private static readonly ProviderProfileService ProviderFeatureService = new();

    private readonly string workspaceRoot;
    private readonly IServiceProvider services;
    private readonly WorkspaceScopeDescriptor workspaceScope;
    private readonly IMafRuntimeDependencyResolver dependencyResolver;
    private readonly IMafProviderCredentialService providerCredentialService;
    private readonly IMafProviderAgentFactory providerAgentFactory;
    private readonly IMafProviderRuntimeGateway providerRuntimeGateway;
    private readonly IMafProviderStreamingDispatchGate providerStreamingDispatchGate;
    private readonly IMafProviderStreamingRunner providerStreamingRunner;
    private readonly IRuntimeToolProviderComposer runtimeToolProviderComposer;
    private readonly IMafRuntimeCompositionMetrics compositionMetrics;
    private readonly IRuntimeCapabilityComposer runtimeCapabilityComposer;
    private readonly MafRuntimeAgentFactory runtimeAgentFactory;
    private readonly InputAttachmentPreparer inputAttachmentPreparer;
    private readonly ConcurrentDictionary<Guid, IReadOnlyList<ToolApprovalRequestContent>> pendingApprovalCache = new();

    public MafAgentRuntime(
        string workspaceRoot,
        IServiceProvider services,
        WorkspaceScopeDescriptor? workspaceScope = null)
    {
        if (string.IsNullOrWhiteSpace(workspaceRoot))
        {
            throw new ArgumentException("Workspace root must be provided.", nameof(workspaceRoot));
        }

        ArgumentNullException.ThrowIfNull(services);

        this.workspaceRoot = Path.GetFullPath(workspaceRoot);
        this.services = services;
        this.workspaceScope = workspaceScope ?? WorkspaceScopeDescriptor.Sandbox;
        dependencyResolver = MafRuntimeDependencyResolver.Resolve(services);
        providerCredentialService = services.GetService(typeof(IMafProviderCredentialService)) is IMafProviderCredentialService resolvedCredentialService
            ? resolvedCredentialService
            : new MafProviderCredentialService(services);
        providerAgentFactory = services.GetService(typeof(IMafProviderAgentFactory)) is IMafProviderAgentFactory resolvedAgentFactory
            ? resolvedAgentFactory
            : new MafProviderAgentFactory(providerCredentialService);
        var providerDependencies = dependencyResolver.ResolveProviderDependencies(services);
        providerRuntimeGateway = providerDependencies.ProviderRuntimeGateway;
        inputAttachmentPreparer = new InputAttachmentPreparer(providerCredentialService, providerRuntimeGateway);
        providerStreamingDispatchGate = providerDependencies.ProviderStreamingDispatchGate;
        providerStreamingRunner = services.GetService(typeof(IMafProviderStreamingRunner)) is IMafProviderStreamingRunner resolvedStreamingRunner
            ? resolvedStreamingRunner
            : new MafProviderStreamingRunner(providerStreamingDispatchGate);
        runtimeToolProviderComposer = services.GetService(typeof(IRuntimeToolProviderComposer)) is IRuntimeToolProviderComposer resolvedComposer
            ? resolvedComposer
            : RuntimeToolProviderComposer.Default;
        compositionMetrics = services.GetService(typeof(IMafRuntimeCompositionMetrics)) is IMafRuntimeCompositionMetrics resolvedMetrics
            ? resolvedMetrics
            : NoOpMafRuntimeCompositionMetrics.Instance;
        runtimeCapabilityComposer = new RuntimeCapabilityComposer(
            this.workspaceRoot,
            services,
            this.workspaceScope,
            dependencyResolver,
            providerCredentialService,
            providerRuntimeGateway,
            runtimeToolProviderComposer,
            compositionMetrics);
        runtimeAgentFactory = new MafRuntimeAgentFactory(
            this.workspaceRoot,
            services,
            this.workspaceScope,
            providerCredentialService,
            providerAgentFactory,
            runtimeCapabilityComposer);
    }

    public Task<AIAgent> CreateHostedAgentAsync(
        AgentDefinition agent,
        ProviderProfile provider,
        IReadOnlyList<CapabilityCatalogItem> capabilities,
        IReadOnlyList<AgentMemoryRecord> memory,
        CancellationToken cancellationToken = default,
        bool suppressApprovalRequirements = false,
        bool forceOmitTemperature = false,
        AgentRuntimeExecutionOptions? executionOptions = null)
    {
        return runtimeAgentFactory.CreateHostedAgentAsync(
            agent,
            provider,
            capabilities,
            memory,
            cancellationToken,
            suppressApprovalRequirements,
            forceOmitTemperature,
            executionOptions);
    }

    public Task<ProviderHealthResult> TestProviderAsync(
        ProviderProfile provider,
        CancellationToken cancellationToken = default)
        => ProviderRuntimeDiagnostics.TestProviderAsync(
            providerRuntimeGateway,
            provider,
            cancellationToken);

    public Task<ProviderTestChatResult> RunProviderTestChatAsync(
        ProviderProfile provider,
        ProviderTestChatRequest request,
        CancellationToken cancellationToken = default)
        => ProviderRuntimeDiagnostics.RunProviderTestChatAsync(
            providerRuntimeGateway,
            provider,
            request,
            cancellationToken);

    public Task<ProviderModelMaintenanceEditorResult> CreateOrUpdateProviderModelAsync(
        ProviderProfile provider,
        ProviderModelMaintenanceEditorRequest request,
        CancellationToken cancellationToken = default)
        => ProviderRuntimeDiagnostics.CreateOrUpdateProviderModelAsync(
            providerRuntimeGateway,
            provider,
            request,
            cancellationToken);

    private Task<PreparedInputAttachments> PrepareInputAttachmentsAsync(
        AgentDefinition agent,
        ProviderProfile provider,
        string prompt,
        AgentRuntimeExecutionOptions runtimeOptions,
        Func<ExecutionState, string, string, Task> progressCallback,
        CancellationToken cancellationToken)
    {
        return inputAttachmentPreparer.PrepareAsync(
            agent,
            provider,
            prompt,
            runtimeOptions,
            progressCallback,
            cancellationToken);
    }

    private static AgentRuntimeResponse AttachPreparedInputUsageObservations(
        AgentRuntimeResponse response,
        IReadOnlyList<ProviderUsageObservation>? usageObservations)
    {
        return InputAttachmentPreparer.AttachUsageObservations(response, usageObservations);
    }

    public async Task<AgentRuntimeResponse> RunAsync(
        AgentDefinition agent,
        ProviderProfile provider,
        ChatSessionRecord session,
        IReadOnlyList<CapabilityCatalogItem> capabilities,
        IReadOnlyList<AgentMemoryRecord> memory,
        string prompt,
        string? runtimeSessionKey,
        Func<ExecutionState, string, string, Task> progressCallback,
        CancellationToken cancellationToken = default,
        bool suppressApprovalRequirements = false,
        AgentStructuredOutputContract? structuredOutput = null,
        AgentRuntimeExecutionOptions? executionOptions = null)
    {
        var model = MafModelParametersBuilder.ResolveRuntimeModel(agent, provider);
        try
        {
            return await RunCoreAsync(
                agent,
                provider,
                session,
                capabilities,
                memory,
                prompt,
                runtimeSessionKey,
                progressCallback,
                cancellationToken,
                suppressApprovalRequirements,
                structuredOutput,
                executionOptions,
                forceOmitTemperature: false);
        }
        catch (Exception exception) when (MafModelParametersBuilder.ShouldRetryWithoutTemperature(provider, model, exception))
        {
            await progressCallback(ExecutionState.Preparing, "Model parameters", MafModelParametersBuilder.BuildTemperatureRetryMessage(model));
            return await RunCoreAsync(
                agent,
                provider,
                session,
                capabilities,
                memory,
                prompt,
                runtimeSessionKey,
                progressCallback,
                cancellationToken,
                suppressApprovalRequirements,
                structuredOutput,
                executionOptions,
                forceOmitTemperature: true);
        }
    }

    private async Task<AgentRuntimeResponse> RunCoreAsync(
        AgentDefinition agent,
        ProviderProfile provider,
        ChatSessionRecord session,
        IReadOnlyList<CapabilityCatalogItem> capabilities,
        IReadOnlyList<AgentMemoryRecord> memory,
        string prompt,
        string? runtimeSessionKey,
        Func<ExecutionState, string, string, Task> progressCallback,
        CancellationToken cancellationToken,
        bool suppressApprovalRequirements,
        AgentStructuredOutputContract? structuredOutput,
        AgentRuntimeExecutionOptions? executionOptions,
        bool forceOmitTemperature)
    {
        var runtimeOptions = MafRuntimeExecutionOptionsResolver.Normalize(structuredOutput, executionOptions);
        var preparedInput = await PrepareInputAttachmentsAsync(
            agent,
            provider,
            prompt,
            runtimeOptions,
            progressCallback,
            cancellationToken);
        prompt = preparedInput.Prompt;
        runtimeOptions = preparedInput.RuntimeOptions;
        await progressCallback(ExecutionState.Preparing, "Framework", "Composing the Microsoft Agent Framework runtime for the selected provider and capabilities.");
        if (suppressApprovalRequirements)
        {
            await progressCallback(ExecutionState.Preparing, "Approval policy", "Auto-approve is active for this run, so future tool approval gates will be suppressed.");
        }

        await using var runtimeBuild = await runtimeAgentFactory.CreateRuntimeBuildAsync(
            agent,
            provider,
            capabilities,
            memory,
            progressCallback,
            cancellationToken,
            suppressApprovalRequirements,
            forceOmitTemperature,
            runtimeOptions);
        InputAttachmentSupport.EnsureSupported(runtimeBuild.Provider, runtimeBuild.Model, runtimeOptions);

        if (runtimeBuild.IsTemperatureOmitted)
        {
            await progressCallback(ExecutionState.Preparing, "Model parameters", MafModelParametersBuilder.BuildTemperatureOmittedMessage(runtimeBuild.Model));
        }

        await progressCallback(ExecutionState.Preparing, "Session", MafRuntimeSessionBuilder.ResolveSessionMessage(agent, runtimeBuild.Provider, session, runtimeOptions));
        var runtimeSession = await MafRuntimeSessionBuilder.RestoreOrCreateSessionAsync(
            runtimeBuild.Agent,
            agent,
            runtimeBuild.Provider,
            session,
            runtimeOptions,
            cancellationToken,
            isApprovalContinuation: false);
        var runOptions = MafRuntimeSessionBuilder.CreateRunOptions(
            agent,
            runtimeBuild.Provider,
            runtimeBuild.Model,
            runtimeBuild.HasApprovalTools,
            continuationToken: null,
            forceOmitTemperature: forceOmitTemperature,
            runtimeOptions);
        var inputMessages = MafRuntimeSessionBuilder.CreatePromptInputMessages(agent, runtimeBuild.Provider, session, prompt, runtimeOptions).ToList();
        var capabilityState = runtimeBuild.CapabilityState;
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

        var response = await ExecuteRunAsync(
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
            contextManifest);

        return AttachPreparedInputUsageObservations(response, preparedInput.UsageObservations);
    }

    public async Task<AgentRuntimeResponse> RespondToPendingApprovalsAsync(
        AgentDefinition agent,
        ProviderProfile provider,
        ChatSessionRecord session,
        IReadOnlyList<CapabilityCatalogItem> capabilities,
        IReadOnlyList<AgentMemoryRecord> memory,
        bool approved,
        string? runtimeSessionKey,
        Func<ExecutionState, string, string, Task> progressCallback,
        CancellationToken cancellationToken = default,
        bool suppressApprovalRequirements = false,
        AgentStructuredOutputContract? structuredOutput = null,
        AgentRuntimeExecutionOptions? executionOptions = null)
    {
        var model = MafModelParametersBuilder.ResolveRuntimeModel(agent, provider);
        try
        {
            return await RespondToPendingApprovalsCoreAsync(
                agent,
                provider,
                session,
                capabilities,
                memory,
                approved,
                runtimeSessionKey,
                progressCallback,
                cancellationToken,
                suppressApprovalRequirements,
                structuredOutput,
                executionOptions,
                forceOmitTemperature: false);
        }
        catch (Exception exception) when (MafModelParametersBuilder.ShouldRetryWithoutTemperature(provider, model, exception))
        {
            await progressCallback(ExecutionState.Preparing, "Model parameters", MafModelParametersBuilder.BuildTemperatureRetryMessage(model));
            return await RespondToPendingApprovalsCoreAsync(
                agent,
                provider,
                session,
                capabilities,
                memory,
                approved,
                runtimeSessionKey,
                progressCallback,
                cancellationToken,
                suppressApprovalRequirements,
                structuredOutput,
                executionOptions,
                forceOmitTemperature: true);
        }
    }

    private async Task<AgentRuntimeResponse> RespondToPendingApprovalsCoreAsync(
        AgentDefinition agent,
        ProviderProfile provider,
        ChatSessionRecord session,
        IReadOnlyList<CapabilityCatalogItem> capabilities,
        IReadOnlyList<AgentMemoryRecord> memory,
        bool approved,
        string? runtimeSessionKey,
        Func<ExecutionState, string, string, Task> progressCallback,
        CancellationToken cancellationToken,
        bool suppressApprovalRequirements,
        AgentStructuredOutputContract? structuredOutput,
        AgentRuntimeExecutionOptions? executionOptions,
        bool forceOmitTemperature)
    {
        var runtimeOptions = MafRuntimeExecutionOptionsResolver.Normalize(structuredOutput, executionOptions);
        await progressCallback(ExecutionState.Preparing, "Framework", "Rehydrating the Microsoft Agent Framework runtime to continue from a pending approval.");
        if (suppressApprovalRequirements)
        {
            await progressCallback(ExecutionState.Preparing, "Approval policy", "Auto-approve remains active, so future tool approval gates will be suppressed after this decision is replayed.");
        }

        await using var runtimeBuild = await runtimeAgentFactory.CreateRuntimeBuildAsync(
            agent,
            provider,
            capabilities,
            memory,
            progressCallback,
            cancellationToken,
            suppressApprovalRequirements,
            forceOmitTemperature,
            runtimeOptions);

        if (runtimeBuild.IsTemperatureOmitted)
        {
            await progressCallback(ExecutionState.Preparing, "Model parameters", MafModelParametersBuilder.BuildTemperatureOmittedMessage(runtimeBuild.Model));
        }

        await progressCallback(ExecutionState.Preparing, "Session", "Restoring the session state prior to replaying the approval response.");
        var runtimeSession = await MafRuntimeSessionBuilder.RestoreOrCreateSessionAsync(
            runtimeBuild.Agent,
            agent,
            runtimeBuild.Provider,
            session,
            runtimeOptions,
            cancellationToken,
            isApprovalContinuation: true);
        var runOptions = MafRuntimeSessionBuilder.CreateRunOptions(
            agent,
            runtimeBuild.Provider,
            runtimeBuild.Model,
            runtimeBuild.HasApprovalTools,
            continuationToken: null,
            forceOmitTemperature: forceOmitTemperature,
            runtimeOptions);
        var inputMessages = CreateApprovalInputMessages(session, approved).ToList();
        var capabilityState = runtimeBuild.CapabilityState;
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

        return await ExecuteRunAsync(
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
            contextManifest);
    }

    private async Task<AgentRuntimeResponse> ExecuteRunAsync(
        AgentDefinition agent,
        ProviderProfile provider,
        string model,
        ChatSessionRecord session,
        AIAgent runtimeAgent,
        AgentSession runtimeSession,
        ChatClientAgentRunOptions runOptions,
        IEnumerable<ChatMessage> inputMessages,
        string? runtimeSessionKey,
        Func<ExecutionState, string, string, Task> progressCallback,
        CancellationToken cancellationToken,
        AgentStructuredOutputContract? structuredOutput,
        AgentFinalizerMode finalizerMode,
        AgentRuntimeExecutionOptions runtimeOptions,
        bool forceOmitTemperature,
        IReadOnlyList<AITool> finalizerTools,
        ToolInvocationTraceRecorder? toolInvocationTraceRecorder,
        Func<IReadOnlyList<AgentFinalizerInvocation>> snapshotFinalizerInvocations,
        Func<IReadOnlyList<AgentToolInvocationTrace>> snapshotToolInvocationTraces,
        Func<IReadOnlyList<AgentContextContributionTrace>> snapshotContextContributionTraces,
        AgentRuntimeContextAssemblyManifest contextManifest)
    {
        var updates = new List<AgentResponseUpdate>();
        var announcedStreaming = false;
        var announcedToolCalls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var guardedToolCallIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var repeatedToolInvocationGuard = new RepeatedToolInvocationGuard();
        var synthesizedFinalizerInvocations = new List<AgentFinalizerInvocation>();
        var streamedFinalizerRecorder = new MafFinalizerDriver.StreamedFinalizerInvocationRecorder(structuredOutput, finalizerMode);
        Func<IReadOnlyList<AgentToolInvocationTrace>> snapshotEffectiveToolInvocationTraces = () =>
            MafFinalizerDriver.CreateEffectiveToolInvocationTraces(
                snapshotToolInvocationTraces(),
                streamedFinalizerRecorder.SnapshotToolInvocationTraces());
        Func<IReadOnlyList<AgentFinalizerInvocation>> snapshotEffectiveFinalizerInvocations = () =>
            MafFinalizerDriver.CreateEffectiveFinalizerInvocations(
                structuredOutput,
                finalizerMode,
                snapshotFinalizerInvocations(),
                snapshotToolInvocationTraces(),
                streamedFinalizerRecorder.SnapshotFinalizerInvocations(),
                synthesizedFinalizerInvocations);
        var pollCount = 0;
        var resolvedModel = model;

        AgentRuntimeResponse AttachContextDiagnostics(AgentRuntimeResponse response)
            => response with
            {
                ContextAssemblyManifest = contextManifest,
                ContextContributionTraces = snapshotContextContributionTraces()
            };

        async Task<AgentRuntimeResponse?> RecordStreamingUpdateAsync(
            AgentResponseUpdate update,
            string usageSourcePhase)
        {
            var snapshot = MafAgentResponseSnapshotter.SnapshotUpdate(update);
            updates.Add(snapshot);

            if (!announcedStreaming && !string.IsNullOrWhiteSpace(snapshot.Text))
            {
                announcedStreaming = true;
                await progressCallback(ExecutionState.Running, "Streaming", "The agent is producing streamed output.");
            }

            foreach (var toolCall in snapshot.Contents.OfType<ToolCallContent>())
            {
                var toolKey = MafToolInvocationArgumentFormatter.ResolveToolCallKey(toolCall);
                if (toolCall.CallId is null || guardedToolCallIds.Add(toolCall.CallId))
                {
                    repeatedToolInvocationGuard.Guard(toolCall);
                }

                streamedFinalizerRecorder.Record(toolCall);
                if (!announcedToolCalls.Add(toolKey))
                {
                    continue;
                }

                await progressCallback(ExecutionState.WaitingOnTool, "Tool", MafToolInvocationArgumentFormatter.DescribeToolInvocation(toolCall));
            }

            return await TryCreateFinalizerResponseAfterRequiredFinalizerAsync(
                provider,
                resolvedModel,
                structuredOutput,
                finalizerMode,
                runtimeAgent,
                runtimeSession,
                runtimeSessionKey,
                runtimeOptions,
                updates,
                usageSourcePhase,
                progressCallback,
                cancellationToken,
                snapshotEffectiveFinalizerInvocations,
                snapshotEffectiveToolInvocationTraces);
        }

        async Task<AgentRuntimeResponse?> TryRunMissingRequiredFinalizerRepairAsync(
            AgentResponse response,
            IReadOnlyCollection<ToolApprovalRequestContent> approvalRequests)
        {
            if (!MafFinalizerDriver.ShouldRequestMissingRequiredFinalizerRepair(
                    structuredOutput,
                    finalizerMode,
                    runtimeOptions,
                    approvalRequests,
                    snapshotEffectiveFinalizerInvocations(),
                    out var finalizerPolicy))
            {
                return null;
            }

            var recoveredArtifactResponse = await TryCreateFinalizerResponseFromRecoveredProcessArtifactAsync(
                provider,
                resolvedModel,
                runtimeAgent,
                runtimeSession,
                runtimeSessionKey,
                runtimeOptions,
                finalizerPolicy,
                updates,
                ProviderUsageSourcePhases.FinalizerRecovery,
                "The provider completed without the required finalizer after the current process step primary artifact was written.",
                progressCallback,
                cancellationToken,
                snapshotEffectiveToolInvocationTraces).ConfigureAwait(false);
            if (recoveredArtifactResponse is not null)
            {
                return AttachContextDiagnostics(recoveredArtifactResponse);
            }

            await progressCallback(
                ExecutionState.Running,
                "Finalizer repair",
                $"Required finalizer tool '{finalizerPolicy.ToolName}' was missing after the provider completed. Requesting one bounded repair turn.");

            var finalizerTool = MafFinalizerDriver.ResolveRequiredFinalizerTool(finalizerPolicy, finalizerTools);
            if (toolInvocationTraceRecorder is null)
            {
                throw new InvalidOperationException(
                    $"Cannot repair missing required finalizer '{finalizerPolicy.ToolName}' because tool invocation tracing is unavailable.");
            }

            var repairContext = MafFinalizerDriver.BuildRequiredFinalizerRepairContext(
                response,
                snapshotEffectiveToolInvocationTraces(),
                inputMessages);
            var repairRunOptions = MafFinalizerDriver.CreateRequiredFinalizerRepairRunOptions(finalizerPolicy, finalizerTool);
            var repairMessages = new[]
            {
                MafFinalizerDriver.CreateRequiredFinalizerRepairMessage(finalizerPolicy, response, repairContext)
            };

            try
            {
                await foreach (var repairUpdate in providerStreamingRunner.RunStreamingAsync(provider, resolvedModel, runtimeAgent, runtimeSession, repairMessages, repairRunOptions, cancellationToken))
                {
                    var finalizerResponse = await RecordStreamingUpdateAsync(
                        repairUpdate,
                        ProviderUsageSourcePhases.FinalizerRecovery);
                    if (finalizerResponse is not null)
                    {
                        return AttachContextDiagnostics(finalizerResponse);
                    }
                }

                var requiredFinalizerResponse = await TryCreateFinalizerResponseAfterRequiredFinalizerAsync(
                    provider,
                    resolvedModel,
                    structuredOutput,
                    finalizerMode,
                    runtimeAgent,
                    runtimeSession,
                    runtimeSessionKey,
                    runtimeOptions,
                    updates,
                    ProviderUsageSourcePhases.FinalizerRecovery,
                    progressCallback,
                    cancellationToken,
                    snapshotEffectiveFinalizerInvocations,
                    snapshotEffectiveToolInvocationTraces);
                if (requiredFinalizerResponse is not null)
                {
                    return AttachContextDiagnostics(requiredFinalizerResponse);
                }

                var jsonRepairResponse = await TryRunRequiredFinalizerJsonRepairAsync(
                    finalizerPolicy,
                    response,
                    repairContext,
                    toolInvocationTraceRecorder);
                return jsonRepairResponse is null
                    ? null
                    : AttachContextDiagnostics(jsonRepairResponse);
            }
            catch (RequiredFinalizerCapturedException exception)
            {
                var finalizerResponse = await TryCreateFinalizerResponseAfterEarlyFinalizerAsync(
                    provider,
                    resolvedModel,
                    structuredOutput,
                    finalizerMode,
                    runtimeAgent,
                    runtimeSession,
                    runtimeSessionKey,
                    runtimeOptions,
                    updates,
                    ProviderUsageSourcePhases.FinalizerRecovery,
                    progressCallback,
                    cancellationToken,
                    snapshotEffectiveFinalizerInvocations,
                    snapshotEffectiveToolInvocationTraces);
                if (finalizerResponse is not null)
                {
                    return AttachContextDiagnostics(finalizerResponse);
                }

                throw new AgentRuntimeUsageException(
                    $"Required finalizer repair captured '{exception.ToolName}' but the governed result could not be validated.",
                    exception,
                    CreateProviderUsageObservations(
                        provider,
                        resolvedModel,
                        runtimeSession,
                        runtimeSessionKey,
                        updates,
                        ProviderUsageSourcePhases.FinalizerRecovery,
                        $"Required finalizer repair captured '{exception.ToolName}' but validation failed."));
            }
            catch (Exception exception)
            {
                var finalizerResponse = await TryCreateFinalizerResponseAfterProviderFailureAsync(
                    provider,
                    resolvedModel,
                    structuredOutput,
                    runtimeAgent,
                    runtimeSession,
                    runtimeSessionKey,
                    runtimeOptions,
                    finalizerMode,
                    exception,
                    updates,
                    progressCallback,
                    cancellationToken,
                    snapshotEffectiveFinalizerInvocations,
                    snapshotEffectiveToolInvocationTraces);
                if (finalizerResponse is not null)
                {
                    return AttachContextDiagnostics(finalizerResponse);
                }

                throw new AgentRuntimeUsageException(
                    "Provider runtime failed during the bounded required-finalizer repair turn. Usage was captured when available.",
                    exception,
                    CreateProviderUsageObservations(
                        provider,
                        resolvedModel,
                        runtimeSession,
                        runtimeSessionKey,
                        updates,
                    ProviderUsageSourcePhases.FinalizerRecovery,
                    BuildProviderFailureDiagnostic(exception)));
            }
        }

        async Task<AgentRuntimeResponse?> TryRunRequiredFinalizerJsonRepairAsync(
            AgentFinalizerPolicy finalizerPolicy,
            AgentResponse previousResponse,
            string repairContext,
            ToolInvocationTraceRecorder toolInvocationTraceRecorder)
        {
            await progressCallback(
                ExecutionState.Running,
                "Finalizer repair",
                $"Required finalizer tool '{finalizerPolicy.ToolName}' was still missing after the tool-only repair turn. Requesting a typed JSON fallback for the same governed contract.");

            var jsonRepairAgent = CreateRequiredFinalizerJsonRepairAgent(
                agent,
                provider,
                resolvedModel,
                forceOmitTemperature,
                finalizerPolicy);
            var jsonRepairSession = await jsonRepairAgent.CreateSessionAsync(cancellationToken);
            var jsonRepairRunOptions = MafFinalizerDriver.CreateRequiredFinalizerJsonRepairRunOptions();
            var jsonRepairMessages = new[]
            {
                MafFinalizerDriver.CreateRequiredFinalizerJsonRepairMessage(finalizerPolicy, previousResponse, repairContext)
            };
            var jsonRepairUpdates = new List<AgentResponseUpdate>();

            try
            {
                await foreach (var jsonRepairUpdate in providerStreamingRunner.RunStreamingAsync(provider, resolvedModel, jsonRepairAgent, jsonRepairSession, jsonRepairMessages, jsonRepairRunOptions, cancellationToken))
                {
                    jsonRepairUpdates.Add(MafAgentResponseSnapshotter.SnapshotUpdate(jsonRepairUpdate));
                    var streamedFinalizerResponse = await RecordStreamingUpdateAsync(
                        jsonRepairUpdate,
                        ProviderUsageSourcePhases.FinalizerRecovery);
                    if (streamedFinalizerResponse is not null)
                    {
                        return AttachContextDiagnostics(streamedFinalizerResponse);
                    }
                }

                var jsonRepairProviderResponse = jsonRepairUpdates.ToAgentResponse();
                if (!TryCaptureSynthesizedFinalizerInvocation(
                        finalizerPolicy,
                        jsonRepairProviderResponse.Text,
                        toolInvocationTraceRecorder,
                        synthesizedFinalizerInvocations,
                        out var captureFailure))
                {
                    await progressCallback(
                        ExecutionState.Running,
                        "Finalizer repair",
                        $"Typed JSON fallback did not produce a valid '{finalizerPolicy.OutputContract.ContractKey}' payload: {captureFailure}");
                    return null;
                }

                await progressCallback(
                    ExecutionState.Running,
                    "Finalizer repair",
                    $"Typed JSON fallback produced a valid '{finalizerPolicy.OutputContract.ContractKey}' payload. Validating it through the same required-finalizer contract.");

                var synthesizedFinalizerResponse = await TryCreateFinalizerResponseAfterRequiredFinalizerAsync(
                    provider,
                    resolvedModel,
                    structuredOutput,
                    finalizerMode,
                    runtimeAgent,
                    runtimeSession,
                    runtimeSessionKey,
                    runtimeOptions,
                    updates,
                    ProviderUsageSourcePhases.FinalizerRecovery,
                    progressCallback,
                    cancellationToken,
                    snapshotEffectiveFinalizerInvocations,
                    snapshotEffectiveToolInvocationTraces);
                return synthesizedFinalizerResponse is null
                    ? null
                    : AttachContextDiagnostics(synthesizedFinalizerResponse);
            }
            finally
            {
                await DisposeAgentAsync(jsonRepairAgent);
            }
        }

        while (true)
        {
            if (pollCount == 0)
            {
                await progressCallback(ExecutionState.Running, "Run", "Executing the run through Microsoft Agent Framework streaming.");
            }
            else
            {
                await progressCallback(ExecutionState.Running, "Background", $"Polling background response progress (attempt {pollCount}).");
            }

            using (var providerActivity = AgentFrameworkTelemetry.ActivitySource.StartActivity("provider.call", ActivityKind.Internal))
            {
                AgentFrameworkTelemetry.ApplyCurrentAuditScope(providerActivity);
                providerActivity?.SetTag("agentframework.provider_name", provider.Name);
                providerActivity?.SetTag("agentframework.model", resolvedModel);
                providerActivity?.SetTag("agentframework.background_poll", pollCount);

                try
                {
                    await foreach (var update in providerStreamingRunner.RunStreamingAsync(provider, resolvedModel, runtimeAgent, runtimeSession, inputMessages, runOptions, cancellationToken))
                    {
                        var finalizerResponse = await RecordStreamingUpdateAsync(
                            update,
                            ProviderUsageSourcePhases.FinalizerShortCircuit);
                        if (finalizerResponse is not null)
                        {
                            return AttachContextDiagnostics(finalizerResponse);
                        }
                    }

                    var postStreamingFinalizerResponse = await TryCreateFinalizerResponseAfterRequiredFinalizerAsync(
                        provider,
                        resolvedModel,
                        structuredOutput,
                        finalizerMode,
                        runtimeAgent,
                        runtimeSession,
                        runtimeSessionKey,
                        runtimeOptions,
                        updates,
                        ProviderUsageSourcePhases.FinalizerShortCircuit,
                        progressCallback,
                        cancellationToken,
                        snapshotEffectiveFinalizerInvocations,
                        snapshotEffectiveToolInvocationTraces);
                    if (postStreamingFinalizerResponse is not null)
                    {
                        return AttachContextDiagnostics(postStreamingFinalizerResponse);
                    }
                }
                catch (RequiredFinalizerCapturedException exception)
                {
                    providerActivity?.SetTag("agentframework.required_finalizer_tool_name", exception.ToolName);
                    var finalizerResponse = await TryCreateFinalizerResponseAfterEarlyFinalizerAsync(
                        provider,
                        resolvedModel,
                        structuredOutput,
                        finalizerMode,
                        runtimeAgent,
                        runtimeSession,
                        runtimeSessionKey,
                        runtimeOptions,
                        updates,
                        ProviderUsageSourcePhases.FinalizerShortCircuit,
                        progressCallback,
                        cancellationToken,
                        snapshotEffectiveFinalizerInvocations,
                        snapshotEffectiveToolInvocationTraces);
                    if (finalizerResponse is not null)
                    {
                        return AttachContextDiagnostics(finalizerResponse);
                    }

                    throw;
                }
                catch (Exception exception)
                {
                    AgentFrameworkTelemetry.RecordProviderError(provider, resolvedModel);
                    providerActivity?.SetStatus(ActivityStatusCode.Error, exception.Message);
                    var finalizerResponse = await TryCreateFinalizerResponseAfterProviderFailureAsync(
                        provider,
                        resolvedModel,
                        structuredOutput,
                        runtimeAgent,
                        runtimeSession,
                        runtimeSessionKey,
                        runtimeOptions,
                        finalizerMode,
                        exception,
                        updates,
                        progressCallback,
                        cancellationToken,
                        snapshotEffectiveFinalizerInvocations,
                        snapshotEffectiveToolInvocationTraces);
                    if (finalizerResponse is not null)
                    {
                        return AttachContextDiagnostics(finalizerResponse);
                    }

                    throw new AgentRuntimeUsageException(
                        "Provider runtime failed after provider activity. Usage was captured when available.",
                        exception,
                        CreateProviderUsageObservations(
                            provider,
                            resolvedModel,
                            runtimeSession,
                            runtimeSessionKey,
                            updates,
                            ProviderUsageSourcePhases.AgentRuntime,
                            BuildProviderFailureDiagnostic(exception)));
                }
            }

            var response = updates.ToAgentResponse();
            var approvalRequests = response.Messages
                .SelectMany(message => message.Contents)
                .OfType<ToolApprovalRequestContent>()
                .ToList();

            if (approvalRequests.Count > 0)
            {
                pendingApprovalCache[session.Id] = approvalRequests;
            }
            else
            {
                pendingApprovalCache.TryRemove(session.Id, out _);
            }

            if (!ShouldContinueBackgroundRun(agent, provider, response, approvalRequests))
            {
                var repairedFinalizerResponse = await TryRunMissingRequiredFinalizerRepairAsync(response, approvalRequests);
                if (repairedFinalizerResponse is not null)
                {
                    return AttachContextDiagnostics(repairedFinalizerResponse);
                }

                var pendingApprovals = approvalRequests.Select(MapPendingApproval).ToList();
                var serializedSessionJson = await TrySerializePersistableRuntimeSessionAsync(
                    runtimeAgent,
                    runtimeSession,
                    runtimeOptions,
                    pendingApprovals,
                    progressCallback,
                    cancellationToken);

                if (pendingApprovals.Count > 0)
                {
                    await progressCallback(ExecutionState.WaitingOnTool, "Approval", "The run is waiting for a tool approval response before it can continue.");
                }

                ThrowIfEmptyProviderCompletion(provider, resolvedModel, response, pendingApprovals);

                return AttachContextDiagnostics(new AgentRuntimeResponse(
                    ResponseText: ResolveResponseText(response, pendingApprovals),
                    InputTokens: ClampTokenCount(response.Usage?.InputTokenCount),
                    OutputTokens: ClampTokenCount(response.Usage?.OutputTokenCount),
                    ToolCalls: CountToolCalls(response),
                    RuntimeSessionKey: ResolveRuntimeSessionKey(runtimeSession, response, runtimeSessionKey),
                    SerializedSessionStateJson: serializedSessionJson,
                    PendingApprovals: pendingApprovals)
                {
                    CachedInputTokens = ClampTokenCount(response.Usage?.CachedInputTokenCount),
                    FinalizerInvocations = snapshotEffectiveFinalizerInvocations(),
                    ToolInvocationTraces = snapshotEffectiveToolInvocationTraces(),
                    UsageObservations = CreateProviderUsageObservations(
                        provider,
                        resolvedModel,
                        runtimeSession,
                        runtimeSessionKey,
                        updates,
                        ProviderUsageSourcePhases.AgentRuntime,
                        "Microsoft Agent Framework returned a runtime response.")
                });
            }

            pollCount++;
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            runOptions = MafRuntimeSessionBuilder.CreateRunOptions(
                agent,
                provider,
                resolvedModel,
                hasApprovalTools: false,
                continuationToken: response.ContinuationToken,
                forceOmitTemperature: forceOmitTemperature,
                runtimeOptions);
            inputMessages = [];
        }
    }

    private AIAgent CreateRequiredFinalizerRepairAgent(
        AgentDefinition agent,
        ProviderProfile provider,
        string model,
        bool forceOmitTemperature,
        AgentFinalizerPolicy policy,
        AITool finalizerTool,
        ToolInvocationTraceRecorder toolInvocationTraceRecorder)
    {
        var chatOptions = MafModelParametersBuilder.CreateModelCompatibleChatOptions(
            provider,
            model,
            (float)agent.Temperature,
            forceOmitTemperature,
            agent.ConfigurationJson);
        MafFinalizerDriver.ConfigureRequiredFinalizerRepairChatOptions(chatOptions, policy, finalizerTool);

        var repairOptions = new ChatClientAgentOptions
        {
            Id = agent.Id.ToString("D"),
            Name = agent.Name,
            Description = agent.Summary,
            ChatOptions = chatOptions,
            AIContextProviders = [],
            ChatHistoryProvider = null,
            RequirePerServiceCallChatHistoryPersistence = false
        };
        var repairCapabilityState = new RuntimeCapabilityState();
        repairCapabilityState.Tools.Add(finalizerTool);
        return runtimeAgentFactory.CreateInstrumentedAgent(
            providerAgentFactory.CreateFrameworkAgent(provider, model, repairOptions, frameworkManagedHistory: false, services),
            provider,
            agent,
            repairCapabilityState,
            suppressApprovalRequirements: true,
            toolInvocationTraceRecorder,
            policy,
            AgentFinalizerMode.Required);
    }

    private AIAgent CreateRequiredFinalizerJsonRepairAgent(
        AgentDefinition agent,
        ProviderProfile provider,
        string model,
        bool forceOmitTemperature,
        AgentFinalizerPolicy policy)
    {
        var chatOptions = MafModelParametersBuilder.CreateModelCompatibleChatOptions(
            provider,
            model,
            (float)agent.Temperature,
            forceOmitTemperature,
            agent.ConfigurationJson);
        chatOptions.AllowMultipleToolCalls = false;
        chatOptions.Instructions = MafFinalizerDriver.BuildRequiredFinalizerJsonRepairInstructions(policy);
        chatOptions.Tools = [];
        chatOptions.ToolMode = null;

        var featureMatrix = ProviderFeatureService.ResolveFeatureMatrix(provider);
        if (featureMatrix.SupportsResponseFormatJsonSchema)
        {
            chatOptions.ResponseFormat = ChatResponseFormat.ForJsonSchema(
                policy.OutputType,
                AgentOutputJson.SerializerOptions,
                string.IsNullOrWhiteSpace(policy.OutputContract.SchemaName) ? null : policy.OutputContract.SchemaName,
                string.IsNullOrWhiteSpace(policy.OutputContract.SchemaDescription) ? null : policy.OutputContract.SchemaDescription);
        }

        var repairOptions = new ChatClientAgentOptions
        {
            Id = agent.Id.ToString("D"),
            Name = agent.Name,
            Description = agent.Summary,
            ChatOptions = chatOptions,
            AIContextProviders = [],
            ChatHistoryProvider = null,
            RequirePerServiceCallChatHistoryPersistence = false
        };
        return providerAgentFactory.CreateFrameworkAgent(provider, model, repairOptions, frameworkManagedHistory: false, services);
    }

    private static async ValueTask DisposeAgentAsync(AIAgent agent)
    {
        switch (agent)
        {
            case IAsyncDisposable asyncDisposable:
                await asyncDisposable.DisposeAsync();
                break;
            case IDisposable disposable:
                disposable.Dispose();
                break;
        }
    }

    private static bool TryCaptureSynthesizedFinalizerInvocation(
        AgentFinalizerPolicy policy,
        string? repairText,
        ToolInvocationTraceRecorder toolInvocationTraceRecorder,
        List<AgentFinalizerInvocation> synthesizedInvocations,
        out string failureMessage)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(toolInvocationTraceRecorder);
        ArgumentNullException.ThrowIfNull(synthesizedInvocations);

        failureMessage = string.Empty;
        if (!MafFinalizerDriver.TryNormalizeFinalizerJsonRepairText(policy, repairText, out var argumentsJson, out failureMessage))
        {
            return false;
        }

        var sequence = toolInvocationTraceRecorder.Start(
            policy.ToolName,
            ToolInvocationClassification.Read,
            AgentToolInvocationPolicyMetadata.BuildSignature(
                policy.ToolName,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            runtimeToolOwnership: null);
        synthesizedInvocations.Add(new AgentFinalizerInvocation(
            policy.ToolName,
            argumentsJson,
            sequence));
        toolInvocationTraceRecorder.Complete(
            sequence,
            succeeded: true,
            failureMessage: "Captured from a typed JSON required-finalizer repair response.");
        return true;
    }

    private static async Task<AgentRuntimeResponse?> TryCreateFinalizerResponseAfterEarlyFinalizerAsync(
        ProviderProfile provider,
        string model,
        AgentStructuredOutputContract? structuredOutput,
        AgentFinalizerMode finalizerMode,
        AIAgent runtimeAgent,
        AgentSession runtimeSession,
        string? runtimeSessionKey,
        AgentRuntimeExecutionOptions runtimeOptions,
        IReadOnlyList<AgentResponseUpdate> updates,
        string usageSourcePhase,
        Func<ExecutionState, string, string, Task> progressCallback,
        CancellationToken cancellationToken,
        Func<IReadOnlyList<AgentFinalizerInvocation>> snapshotFinalizerInvocations,
        Func<IReadOnlyList<AgentToolInvocationTrace>> snapshotToolInvocationTraces)
    {
        var finalizerInvocations = snapshotFinalizerInvocations();
        var toolInvocationTraces = snapshotToolInvocationTraces();
        var serializedResponse = TryBuildRequiredFinalizerRuntimeResponse(
            structuredOutput,
            finalizerMode,
            ResolveRuntimeSessionKey(runtimeSession, runtimeSessionKey),
            serializedSessionStateJson: null,
            finalizerInvocations,
            toolInvocationTraces,
            CreateProviderUsageObservations(
                provider,
                model,
                runtimeSession,
                runtimeSessionKey,
                updates,
                usageSourcePhase,
                "Required finalizer was captured before the provider emitted final assistant prose."));
        if (serializedResponse is null)
        {
            return null;
        }

        await progressCallback(
            ExecutionState.Persisting,
            "Finalizer short-circuit",
            "Required finalizer tool produced a valid governed result. Persisting the typed result immediately without waiting for redundant post-finalizer assistant prose.");

        var serializedSessionStateJson = await TrySerializePersistableRuntimeSessionAsync(
            runtimeAgent,
            runtimeSession,
            runtimeOptions,
            [],
            progressCallback,
            cancellationToken);
        return serializedResponse with
        {
            SerializedSessionStateJson = serializedSessionStateJson
        };
    }

    private async Task<AgentRuntimeResponse?> TryCreateFinalizerResponseAfterProviderFailureAsync(
        ProviderProfile provider,
        string model,
        AgentStructuredOutputContract? structuredOutput,
        AIAgent runtimeAgent,
        AgentSession runtimeSession,
        string? runtimeSessionKey,
        AgentRuntimeExecutionOptions runtimeOptions,
        AgentFinalizerMode finalizerMode,
        Exception exception,
        IReadOnlyList<AgentResponseUpdate> updates,
        Func<ExecutionState, string, string, Task> progressCallback,
        CancellationToken cancellationToken,
        Func<IReadOnlyList<AgentFinalizerInvocation>> snapshotFinalizerInvocations,
        Func<IReadOnlyList<AgentToolInvocationTrace>> snapshotToolInvocationTraces)
    {
        if (finalizerMode != AgentFinalizerMode.Required ||
            !AgentFinalizerPolicies.TryResolveForStructuredOutput(structuredOutput, out var policy))
        {
            return null;
        }

        var finalizerInvocations = snapshotFinalizerInvocations();
        var finalizerValidation = new DefaultAgentFinalizerValidator().Validate(policy, finalizerInvocations);
        if ((!finalizerValidation.Succeeded || finalizerValidation.Output is null) &&
            finalizerInvocations.Count == 0)
        {
            if (IsProviderStreamingTimeout(exception))
            {
                return await TryCreateFinalizerResponseFromRecoveredProcessArtifactAsync(
                    provider,
                    model,
                    runtimeAgent,
                    runtimeSession,
                    runtimeSessionKey,
                    runtimeOptions,
                    policy,
                    updates,
                    ProviderUsageSourcePhases.FinalizerRecovery,
                    "Provider streaming timed out after the current process step primary artifact was written.",
                    progressCallback,
                    cancellationToken,
                    snapshotToolInvocationTraces).ConfigureAwait(false);
            }

            return null;
        }

        if (!finalizerValidation.Succeeded || finalizerValidation.Output is null)
        {
            return null;
        }

        var toolInvocationTraces = snapshotToolInvocationTraces();
        var sequenceValidation = AgentFinalizerSequenceValidator.Validate(policy, toolInvocationTraces);
        if (!sequenceValidation.Succeeded)
        {
            return null;
        }

        await progressCallback(
            ExecutionState.Persisting,
            "Finalizer recovery",
            $"Provider streaming failed after required finalizer '{policy.ToolName}' was captured. Persisting the governed finalizer outcome and preserving the provider error for diagnostics: {exception.Message}");

        var serializedSessionStateJson = await TrySerializePersistableRuntimeSessionAsync(
            runtimeAgent,
            runtimeSession,
            runtimeOptions,
            [],
            progressCallback,
            cancellationToken);
        return new AgentRuntimeResponse(
            JsonSerializer.Serialize(finalizerValidation.Output, policy.OutputType, AgentOutputJson.SerializerOptions),
            InputTokens: 0,
            OutputTokens: 0,
            ToolCalls: toolInvocationTraces
                .Where(trace => !string.IsNullOrWhiteSpace(trace.ToolName))
                .Select(trace => $"{trace.ToolName}|{trace.Sequence}")
                .Distinct(StringComparer.Ordinal)
                .Count(),
            RuntimeSessionKey: ResolveRuntimeSessionKey(runtimeSession, runtimeSessionKey),
            SerializedSessionStateJson: serializedSessionStateJson,
            PendingApprovals: [])
        {
            FinalizerInvocations = finalizerInvocations,
            ToolInvocationTraces = toolInvocationTraces,
            UsageObservations = CreateProviderUsageObservations(
                provider,
                model,
                runtimeSession,
                runtimeSessionKey,
                updates,
                ProviderUsageSourcePhases.FinalizerRecovery,
                "Provider streaming failed after a valid required finalizer was captured.")
        };
    }

    private async Task<AgentRuntimeResponse?> TryCreateFinalizerResponseFromRecoveredProcessArtifactAsync(
        ProviderProfile provider,
        string model,
        AIAgent runtimeAgent,
        AgentSession runtimeSession,
        string? runtimeSessionKey,
        AgentRuntimeExecutionOptions runtimeOptions,
        AgentFinalizerPolicy policy,
        IReadOnlyList<AgentResponseUpdate> updates,
        string usageSourcePhase,
        string recoveryReason,
        Func<ExecutionState, string, string, Task> progressCallback,
        CancellationToken cancellationToken,
        Func<IReadOnlyList<AgentToolInvocationTrace>> snapshotToolInvocationTraces)
    {
        if (policy.OutputType != typeof(ProcessStepOutcomeResult))
        {
            return null;
        }

        var contextIntent = runtimeOptions.ContextIntent ?? AgentRuntimeContextIntent.Empty;
        if (!ProcessArtifactRecoveryService.TryBuildCurrentStepPrimaryManagedArtifactPath(contextIntent, out var primaryArtifactRef, out _))
        {
            return null;
        }

        string artifactMarkdown;
        try
        {
            var resolver = new WorkspacePathResolutionService(workspaceRoot, workspaceScope);
            var resolvedArtifact = resolver.ResolveFilePath(primaryArtifactRef, allowMissing: false);
            artifactMarkdown = await File.ReadAllTextAsync(resolvedArtifact.FullPath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception readException) when (readException is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return null;
        }

        if (!ProcessArtifactRecoveryService.TryCreateProcessStepOutcomeFromPrimaryArtifact(
                contextIntent,
                primaryArtifactRef,
                artifactMarkdown,
                out var outcome,
                out _))
        {
            return null;
        }

        var argumentsJson = JsonSerializer.Serialize(outcome, AgentOutputJson.SerializerOptions);
        var existingToolTraces = snapshotToolInvocationTraces();
        var finalizerSequence = existingToolTraces.Count == 0
            ? 1
            : existingToolTraces.Max(trace => trace.Sequence) + 1;
        var timestamp = DateTimeOffset.UtcNow;
        var finalizerInvocation = new AgentFinalizerInvocation(
            policy.ToolName,
            argumentsJson,
            finalizerSequence);
        var toolInvocationTraces = existingToolTraces
            .Append(new AgentToolInvocationTrace(
                policy.ToolName,
                ToolInvocationClassification.Read,
                finalizerSequence,
                timestamp,
                timestamp,
                Succeeded: true,
                FailureMessage: string.Empty))
            .ToArray();

        var recoveredResponse = TryBuildRequiredFinalizerRuntimeResponse(
            policy.OutputContract,
            AgentFinalizerMode.Required,
            ResolveRuntimeSessionKey(runtimeSession, runtimeSessionKey),
            serializedSessionStateJson: null,
            [finalizerInvocation],
            toolInvocationTraces,
            CreateProviderUsageObservations(
                provider,
                model,
                runtimeSession,
                runtimeSessionKey,
                updates,
                usageSourcePhase,
                $"{recoveryReason} The required finalizer was synthesized from current process step primary artifact '{primaryArtifactRef}'."));
        if (recoveredResponse is null)
        {
            return null;
        }

        await progressCallback(
            ExecutionState.Persisting,
            "Finalizer recovery",
            $"{recoveryReason} Persisting a validated required-finalizer result synthesized from current process step artifact '{primaryArtifactRef}' with status '{outcome.Status}'.").ConfigureAwait(false);

        var serializedSessionStateJson = await TrySerializePersistableRuntimeSessionAsync(
            runtimeAgent,
            runtimeSession,
            runtimeOptions,
            [],
            progressCallback,
            cancellationToken).ConfigureAwait(false);
        return recoveredResponse with
        {
            SerializedSessionStateJson = serializedSessionStateJson
        };
    }

    private static bool IsProviderStreamingTimeout(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is TimeoutException)
            {
                return true;
            }
        }

        return false;
    }

    private static async Task<AgentRuntimeResponse?> TryCreateFinalizerResponseAfterRequiredFinalizerAsync(
        ProviderProfile provider,
        string model,
        AgentStructuredOutputContract? structuredOutput,
        AgentFinalizerMode finalizerMode,
        AIAgent runtimeAgent,
        AgentSession runtimeSession,
        string? runtimeSessionKey,
        AgentRuntimeExecutionOptions runtimeOptions,
        IReadOnlyList<AgentResponseUpdate> updates,
        string usageSourcePhase,
        Func<ExecutionState, string, string, Task> progressCallback,
        CancellationToken cancellationToken,
        Func<IReadOnlyList<AgentFinalizerInvocation>> snapshotFinalizerInvocations,
        Func<IReadOnlyList<AgentToolInvocationTrace>> snapshotToolInvocationTraces)
    {
        var finalizerInvocations = snapshotFinalizerInvocations();
        var toolInvocationTraces = snapshotToolInvocationTraces();
        var serializedResponse = TryBuildRequiredFinalizerRuntimeResponse(
            structuredOutput,
            finalizerMode,
            ResolveRuntimeSessionKey(runtimeSession, runtimeSessionKey),
            serializedSessionStateJson: null,
            finalizerInvocations,
            toolInvocationTraces,
            CreateProviderUsageObservations(
                provider,
                model,
                runtimeSession,
                runtimeSessionKey,
                updates,
                usageSourcePhase,
                "Required finalizer short-circuited the provider response."));
        if (serializedResponse is null)
        {
            return null;
        }

        await progressCallback(
            ExecutionState.Persisting,
            "Finalizer short-circuit",
            "Required finalizer tool produced a valid governed result. Persisting the typed result without waiting for redundant post-finalizer assistant prose.");

        var serializedSessionStateJson = await TrySerializePersistableRuntimeSessionAsync(
            runtimeAgent,
            runtimeSession,
            runtimeOptions,
            [],
            progressCallback,
            cancellationToken);
        return serializedResponse with
        {
            SerializedSessionStateJson = serializedSessionStateJson
        };
    }

    private static AgentRuntimeResponse? TryBuildRequiredFinalizerRuntimeResponse(
        AgentStructuredOutputContract? structuredOutput,
        AgentFinalizerMode finalizerMode,
        string runtimeSessionKey,
        string? serializedSessionStateJson,
        IReadOnlyList<AgentFinalizerInvocation> finalizerInvocations,
        IReadOnlyList<AgentToolInvocationTrace> toolInvocationTraces,
        IReadOnlyList<ProviderUsageObservation> usageObservations)
    {
        if (finalizerMode != AgentFinalizerMode.Required ||
            !AgentFinalizerPolicies.TryResolveForStructuredOutput(structuredOutput, out var policy))
        {
            return null;
        }

        var finalizerValidation = new DefaultAgentFinalizerValidator().Validate(policy, finalizerInvocations);
        if (!finalizerValidation.Succeeded || finalizerValidation.Output is null)
        {
            return null;
        }

        var sequenceValidation = AgentFinalizerSequenceValidator.Validate(policy, toolInvocationTraces);
        if (!sequenceValidation.Succeeded)
        {
            return null;
        }

        return new AgentRuntimeResponse(
            JsonSerializer.Serialize(finalizerValidation.Output, policy.OutputType, AgentOutputJson.SerializerOptions),
            InputTokens: 0,
            OutputTokens: 0,
            ToolCalls: toolInvocationTraces
                .Where(trace => !string.IsNullOrWhiteSpace(trace.ToolName))
                .Select(trace => $"{trace.ToolName}|{trace.Sequence}")
                .Distinct(StringComparer.Ordinal)
                .Count(),
            RuntimeSessionKey: runtimeSessionKey,
            SerializedSessionStateJson: serializedSessionStateJson,
            PendingApprovals: [])
        {
            FinalizerInvocations = finalizerInvocations,
            ToolInvocationTraces = toolInvocationTraces,
            UsageObservations = usageObservations
        };
    }

    private static IReadOnlyList<ProviderUsageObservation> CreateProviderUsageObservations(
        ProviderProfile provider,
        string model,
        AgentSession runtimeSession,
        string? runtimeSessionKey,
        IReadOnlyList<AgentResponseUpdate> updates,
        string sourcePhase,
        string diagnostic)
    {
        if (updates.Count == 0)
        {
            return
            [
                CreateMissingProviderUsageObservation(
                    provider,
                    model,
                    ResolveRuntimeSessionKey(runtimeSession, runtimeSessionKey),
                    sourcePhase,
                    diagnostic)
            ];
        }

        var response = updates.ToAgentResponse();
        var runtimeKey = ResolveRuntimeSessionKey(runtimeSession, response, runtimeSessionKey);
        var rawUsageJson = response.Usage is null
            ? string.Empty
            : JsonSerializer.Serialize(response.Usage, SerializerOptions);

        return
        [
            DefaultProviderUsageNormalizer.Instance.Normalize(new ProviderUsageNormalizationRequest(
                Provider: provider,
                Model: model,
                SourcePhase: sourcePhase,
                UsageStatus: response.Usage is null
                    ? ProviderUsageObservationStatus.UsageUnavailable
                    : ProviderUsageObservationStatus.Observed,
                InputTokens: ClampTokenCount(response.Usage?.InputTokenCount),
                CachedInputTokens: ClampTokenCount(response.Usage?.CachedInputTokenCount),
                OutputTokens: ClampTokenCount(response.Usage?.OutputTokenCount),
                ReasoningTokens: 0,
                TotalTokens: ClampTokenCount(response.Usage?.TotalTokenCount),
                ToolCallCount: CountToolCalls(response),
                ProviderResponseId: response.ResponseId ?? response.ContinuationToken?.ToString() ?? string.Empty,
                ProviderRequestId: string.Empty,
                RuntimeSessionKey: runtimeKey,
                RawUsageJson: rawUsageJson,
                DiagnosticsJson: JsonSerializer.Serialize(
                    new Dictionary<string, string>
                    {
                        ["diagnostic"] = diagnostic
                    },
                    SerializerOptions)))
        ];
    }

    private static string BuildProviderFailureDiagnostic(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var builder = new StringBuilder("Provider streaming failed before a successful runtime response.");
        var current = exception;
        var depth = 0;
        while (current is not null && depth < 4)
        {
            builder
                .Append(" Exception")
                .Append(depth)
                .Append('=')
                .Append(current.GetType().FullName ?? current.GetType().Name)
                .Append(": ")
                .Append(WorkflowExecutorRedaction.RedactText(current.Message))
                .Append('.');

            current = current.InnerException;
            depth++;
        }

        return builder.ToString();
    }

    private static ProviderUsageObservation CreateMissingProviderUsageObservation(
        ProviderProfile provider,
        string model,
        string runtimeSessionKey,
        string sourcePhase,
        string diagnostic)
    {
        return new ProviderUsageObservation(
            Id: Guid.NewGuid(),
            CreatedAtUtc: DateTimeOffset.UtcNow,
            ProviderName: provider.Name,
            ProviderKind: provider.Kind,
            Model: model,
            TransportKind: provider.Transport,
            SourcePhase: sourcePhase,
            UsageStatus: ProviderUsageObservationStatus.MissingAfterProviderActivity,
            InputTokens: 0,
            CachedInputTokens: 0,
            OutputTokens: 0,
            ReasoningTokens: 0,
            TotalTokens: 0,
            ToolCallCount: 0)
        {
            RuntimeSessionKey = runtimeSessionKey,
            DiagnosticsJson = JsonSerializer.Serialize(
                new Dictionary<string, string>
                {
                    ["diagnostic"] = diagnostic
                },
                SerializerOptions)
        };
    }

    private static int ClampTokenCount(long? tokenCount)
    {
        if (!tokenCount.HasValue || tokenCount.Value <= 0)
        {
            return 0;
        }

        return tokenCount.Value > int.MaxValue
            ? int.MaxValue
            : (int)tokenCount.Value;
    }

    private static bool ShouldSkipRuntimeSessionSerialization(
        AgentRuntimeExecutionOptions runtimeOptions,
        IReadOnlyCollection<PendingToolApprovalRecord> pendingApprovals)
    {
        return pendingApprovals.Count == 0 &&
               (runtimeOptions.ContextIntent?.IsGovernedProcessStep == true ||
                InputAttachmentSupport.HasRequestScopedInputAttachments(runtimeOptions));
    }

    private static string ResolveRuntimeSessionSerializationSkipMessage(AgentRuntimeExecutionOptions runtimeOptions)
    {
        return InputAttachmentSupport.HasRequestScopedInputAttachments(runtimeOptions)
            ? "Skipped Microsoft Agent Framework session serialization because request-scoped input attachments are not persisted into session state. The sandbox transcript keeps the text turn for future replay."
            : "Skipped Microsoft Agent Framework session serialization for a governed process step with no pending approvals. Process state is persisted through the typed outcome and artifacts.";
    }

    private static async Task<string?> TrySerializePersistableRuntimeSessionAsync(
        AIAgent runtimeAgent,
        AgentSession runtimeSession,
        AgentRuntimeExecutionOptions runtimeOptions,
        IReadOnlyCollection<PendingToolApprovalRecord> pendingApprovals,
        Func<ExecutionState, string, string, Task> progressCallback,
        CancellationToken cancellationToken)
    {
        if (ShouldSkipRuntimeSessionSerialization(runtimeOptions, pendingApprovals))
        {
            await progressCallback(
                ExecutionState.Persisting,
                "Session",
                ResolveRuntimeSessionSerializationSkipMessage(runtimeOptions));
            return null;
        }

        await progressCallback(ExecutionState.Persisting, "Session", "Serializing the Microsoft Agent Framework session.");
        var serializedSessionJson = await TrySerializeRuntimeSessionAsync(
            runtimeAgent,
            runtimeSession,
            cancellationToken);
        if (serializedSessionJson is null)
        {
            await progressCallback(
                ExecutionState.Persisting,
                "Session",
                "Microsoft Agent Framework session serialization did not complete within the bounded timeout. Continuing without serialized session state.");
            return null;
        }

        if (!InputAttachmentSupport.HasRequestScopedInputAttachments(runtimeOptions))
        {
            return serializedSessionJson;
        }

        var scrubbedSessionJson = RequestScopedSessionContentScrubber.RemoveRequestScopedDataContent(serializedSessionJson);
        if (scrubbedSessionJson is null)
        {
            await progressCallback(
                ExecutionState.Persisting,
                "Session",
                "Dropped serialized Microsoft Agent Framework session state because request-scoped attachment payload scrubbing failed.");
            return null;
        }

        if (!string.Equals(serializedSessionJson, scrubbedSessionJson, StringComparison.Ordinal))
        {
            await progressCallback(
                ExecutionState.Persisting,
                "Session",
                "Removed request-scoped attachment payloads from serialized Microsoft Agent Framework session state.");
        }

        return scrubbedSessionJson;
    }

    private static async Task<string?> TrySerializeRuntimeSessionAsync(
        AIAgent runtimeAgent,
        AgentSession runtimeSession,
        CancellationToken cancellationToken)
    {
        try
        {
            var serializedSession = await Task.Run(
                async () => await runtimeAgent.SerializeSessionAsync(
                    runtimeSession,
                    cancellationToken: cancellationToken),
                cancellationToken).WaitAsync(
                FinalizerSessionSerializationTimeout,
                cancellationToken);
            return JsonSerializer.Serialize(serializedSession, SerializerOptions);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TimeoutException)
        {
            return null;
        }
        catch
        {
            return null;
        }
    }

    private IEnumerable<ChatMessage> CreateApprovalInputMessages(ChatSessionRecord session, bool approved)
    {
        var approvals = GetCachedOrRehydratedApprovals(session);
        return approvals
            .Select(item => new ChatMessage(ChatRole.User, [item.CreateResponse(approved)]))
            .ToList();
    }

    private IReadOnlyList<ToolApprovalRequestContent> GetCachedOrRehydratedApprovals(ChatSessionRecord session)
    {
        if (pendingApprovalCache.TryGetValue(session.Id, out var cached))
        {
            return cached;
        }

        var compatibility = session.Compatibility;
        if (compatibility is null || compatibility.PendingApprovals.Count == 0)
        {
            throw new InvalidOperationException("This session does not have any cached approval requests to continue.");
        }

        var rehydrated = compatibility.PendingApprovals
            .Select(RehydratePendingApproval)
            .ToList();

        pendingApprovalCache[session.Id] = rehydrated;
        return rehydrated;
    }

    private static ToolApprovalRequestContent RehydratePendingApproval(PendingToolApprovalRecord record)
    {
        var arguments = MafToolInvocationArgumentFormatter.DeserializeArguments(record.ArgumentsJson);
        ToolCallContent toolCall = record.ToolKind switch
        {
            "mcp" or "hosted-mcp" => new McpServerToolCallContent(record.CallId, record.ToolName, record.Details)
            {
                Arguments = arguments
            },
            _ => new FunctionCallContent(record.CallId, record.ToolName, arguments)
        };

        return new ToolApprovalRequestContent(record.ApprovalId, toolCall);
    }

    private static PendingToolApprovalRecord MapPendingApproval(ToolApprovalRequestContent request)
    {
        var toolCall = request.ToolCall;
        var toolKind = toolCall switch
        {
            McpServerToolCallContent => "mcp",
            FunctionCallContent => "function",
            _ => "tool"
        };

        var details = toolCall switch
        {
            McpServerToolCallContent mcp => mcp.ServerName,
            _ => string.Empty
        };

        var argumentsJson = toolCall switch
        {
            McpServerToolCallContent mcp when mcp.Arguments is not null => JsonSerializer.Serialize(mcp.Arguments, SerializerOptions),
            FunctionCallContent function when function.Arguments is not null => JsonSerializer.Serialize(function.Arguments, SerializerOptions),
            _ => "{}"
        };

        return new PendingToolApprovalRecord(
            ApprovalId: request.RequestId ?? toolCall.CallId ?? Guid.NewGuid().ToString("N"),
            CallId: toolCall.CallId ?? string.Empty,
            ToolName: MafToolInvocationArgumentFormatter.ResolveToolName(toolCall),
            ToolKind: toolKind,
            Details: details ?? string.Empty,
            ArgumentsJson: argumentsJson);
    }

    private static string ResolveResponseText(
        AgentResponse response,
        IReadOnlyList<PendingToolApprovalRecord> pendingApprovals)
    {
        if (!string.IsNullOrWhiteSpace(response.Text))
        {
            return response.Text.Trim();
        }

        if (pendingApprovals.Count == 0)
        {
            return "The provider completed without returning text.";
        }

        var summary = string.Join(
            Environment.NewLine,
            pendingApprovals.Select(item =>
            {
                var argumentSummary = MafToolInvocationArgumentFormatter.DescribeArguments(item.ArgumentsJson);
                return item.ToolKind == "mcp"
                    ? $"- Approval required for MCP tool '{item.ToolName}' on server '{item.Details}'{MafToolInvocationArgumentFormatter.FormatInlineArgumentSummary(argumentSummary)}."
                    : $"- Approval required for tool '{item.ToolName}'{MafToolInvocationArgumentFormatter.FormatInlineArgumentSummary(argumentSummary)}.";
            }));

        return $"Approval is required before the run can continue.{Environment.NewLine}{summary}";
    }

    private static void ThrowIfEmptyProviderCompletion(
        ProviderProfile provider,
        string model,
        AgentResponse response,
        IReadOnlyList<PendingToolApprovalRecord> pendingApprovals)
    {
        if (!string.IsNullOrWhiteSpace(response.Text) ||
            pendingApprovals.Count > 0 ||
            CountToolCalls(response) > 0 ||
            ClampTokenCount(response.Usage?.OutputTokenCount) > 0)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Provider '{provider.Name}' model '{model}' completed without returning text, tool calls, approvals, or output tokens.");
    }

    private static bool ShouldContinueBackgroundRun(
        AgentDefinition agent,
        ProviderProfile provider,
        AgentResponse response,
        IReadOnlyCollection<ToolApprovalRequestContent> approvalRequests)
    {
        if (approvalRequests.Count > 0)
        {
            return false;
        }

        return agent.EnableBackgroundResponses
            && MafRuntimeSessionBuilder.SupportsBackgroundResponses(provider)
            && response.ContinuationToken is not null;
    }

    private static int CountToolCalls(AgentResponse response)
    {
        return response.Messages
            .SelectMany(message => message.Contents)
            .Select(content => content switch
            {
                ToolApprovalRequestContent approval => approval.ToolCall?.CallId ?? approval.ToolCall?.ToString(),
                ToolCallContent toolCall => toolCall.CallId ?? MafToolInvocationArgumentFormatter.ResolveToolName(toolCall),
                _ => null
            })
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
    }

    private static string ResolveRuntimeSessionKey(
        AgentSession runtimeSession,
        AgentResponse response,
        string? fallbackValue)
    {
        if (runtimeSession is ChatClientAgentSession chatSession && !string.IsNullOrWhiteSpace(chatSession.ConversationId))
        {
            return chatSession.ConversationId;
        }

        return response.ResponseId
            ?? response.ContinuationToken?.ToString()
            ?? fallbackValue
            ?? string.Empty;
    }

    private static string ResolveRuntimeSessionKey(
        AgentSession runtimeSession,
        string? fallbackValue)
    {
        if (runtimeSession is ChatClientAgentSession chatSession && !string.IsNullOrWhiteSpace(chatSession.ConversationId))
        {
            return chatSession.ConversationId;
        }

        return fallbackValue ?? string.Empty;
    }

}
