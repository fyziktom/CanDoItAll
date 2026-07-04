using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace CanDoItAll.AgentFramework.Maf;

public sealed partial class MafAgentRuntime(
    string workspaceRoot,
    IServiceProvider services,
    WorkspaceScopeDescriptor? workspaceScope = null) : IAgentRuntime
{
    private const string LocalHistoryConversationId = "_agent_local_chat_history";
    private const int MaxRepeatedToolInvocationCount = 3;
    private const int MaxFinalizerRepairPreviousAssistantTextCharacters = 12_000;
    private const int MaxRecoveredProcessArtifactSummaryCharacters = 1_200;
    private const string ProcessArtifactBranchOutcomeKeyLineKey = "Branch outcome key";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan FinalizerSessionSerializationTimeout = TimeSpan.FromSeconds(5);
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(20)
    };
    private static readonly ProviderProfileService ProviderFeatureService = new();

    private readonly string workspaceRoot = Path.GetFullPath(workspaceRoot);
    private readonly IServiceProvider services = services;
    private readonly WorkspaceScopeDescriptor workspaceScope = workspaceScope ?? WorkspaceScopeDescriptor.Sandbox;
    private readonly IMafProviderRuntimeGateway providerRuntimeGateway =
        services.GetService(typeof(IMafProviderRuntimeGateway)) is IMafProviderRuntimeGateway gateway
            ? gateway
            : MafProviderRuntimeGateway.CreateFallback(services);
    private readonly IMafProviderStreamingDispatchGate providerStreamingDispatchGate =
        services.GetService(typeof(IMafProviderStreamingDispatchGate)) is IMafProviderStreamingDispatchGate streamingDispatchGate
            ? streamingDispatchGate
            : CreateFallbackProviderStreamingDispatchGate(services);
    private readonly ConcurrentDictionary<Guid, IReadOnlyList<ToolApprovalRequestContent>> pendingApprovalCache = new();

    private static IMafProviderStreamingDispatchGate CreateFallbackProviderStreamingDispatchGate(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var providerFactory = services.GetService(typeof(IAgentProviderFactory)) is IAgentProviderFactory resolvedFactory
            ? resolvedFactory
            : MafProviderRuntimeServiceCollectionExtensions.CreateDefaultProviderFactory(services);
        var dispatchLaneGate = services.GetService(typeof(IProviderDispatchLaneGate)) is IProviderDispatchLaneGate resolvedGate
            ? resolvedGate
            : new ProviderDispatchLaneGate(providerFactory);
        return new MafProviderStreamingDispatchGate(dispatchLaneGate);
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
        var model = ResolveRuntimeModel(agent, provider);
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
        catch (Exception exception) when (ShouldRetryWithoutTemperature(provider, model, exception))
        {
            await progressCallback(ExecutionState.Preparing, "Model parameters", BuildTemperatureRetryMessage(model));
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
        var runtimeOptions = NormalizeRuntimeExecutionOptions(structuredOutput, executionOptions);
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

        await using var runtimeBuild = await CreateRuntimeBuildAsync(
            agent,
            provider,
            capabilities,
            memory,
            progressCallback,
            cancellationToken,
            suppressApprovalRequirements,
            forceOmitTemperature,
            runtimeOptions);
        EnsureInputAttachmentsSupported(runtimeBuild.Provider, runtimeBuild.Model, runtimeOptions);

        if (runtimeBuild.IsTemperatureOmitted)
        {
            await progressCallback(ExecutionState.Preparing, "Model parameters", BuildTemperatureOmittedMessage(runtimeBuild.Model));
        }

        await progressCallback(ExecutionState.Preparing, "Session", ResolveSessionMessage(agent, runtimeBuild.Provider, session, runtimeOptions));
        var runtimeSession = await RestoreOrCreateSessionAsync(
            runtimeBuild.Agent,
            agent,
            runtimeBuild.Provider,
            session,
            runtimeOptions,
            cancellationToken,
            isApprovalContinuation: false);
        var runOptions = CreateRunOptions(
            agent,
            runtimeBuild.Provider,
            runtimeBuild.Model,
            runtimeBuild.HasApprovalTools,
            continuationToken: null,
            forceOmitTemperature: forceOmitTemperature,
            runtimeOptions);
        var inputMessages = CreatePromptInputMessages(agent, runtimeBuild.Provider, session, prompt, runtimeOptions).ToList();
        var contextManifest = CreateContextAssemblyManifest(
            agent,
            runtimeBuild.Provider,
            runtimeBuild.Model,
            runtimeOptions,
            runtimeBuild,
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
        var model = ResolveRuntimeModel(agent, provider);
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
        catch (Exception exception) when (ShouldRetryWithoutTemperature(provider, model, exception))
        {
            await progressCallback(ExecutionState.Preparing, "Model parameters", BuildTemperatureRetryMessage(model));
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
        var runtimeOptions = NormalizeRuntimeExecutionOptions(structuredOutput, executionOptions);
        await progressCallback(ExecutionState.Preparing, "Framework", "Rehydrating the Microsoft Agent Framework runtime to continue from a pending approval.");
        if (suppressApprovalRequirements)
        {
            await progressCallback(ExecutionState.Preparing, "Approval policy", "Auto-approve remains active, so future tool approval gates will be suppressed after this decision is replayed.");
        }

        await using var runtimeBuild = await CreateRuntimeBuildAsync(
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
            await progressCallback(ExecutionState.Preparing, "Model parameters", BuildTemperatureOmittedMessage(runtimeBuild.Model));
        }

        await progressCallback(ExecutionState.Preparing, "Session", "Restoring the session state prior to replaying the approval response.");
        var runtimeSession = await RestoreOrCreateSessionAsync(
            runtimeBuild.Agent,
            agent,
            runtimeBuild.Provider,
            session,
            runtimeOptions,
            cancellationToken,
            isApprovalContinuation: true);
        var runOptions = CreateRunOptions(
            agent,
            runtimeBuild.Provider,
            runtimeBuild.Model,
            runtimeBuild.HasApprovalTools,
            continuationToken: null,
            forceOmitTemperature: forceOmitTemperature,
            runtimeOptions);
        var inputMessages = CreateApprovalInputMessages(session, approved).ToList();
        var contextManifest = CreateContextAssemblyManifest(
            agent,
            runtimeBuild.Provider,
            runtimeBuild.Model,
            runtimeOptions,
            runtimeBuild,
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
        var streamedFinalizerRecorder = new StreamedFinalizerInvocationRecorder(structuredOutput, finalizerMode);
        Func<IReadOnlyList<AgentToolInvocationTrace>> snapshotEffectiveToolInvocationTraces = () =>
            CreateEffectiveToolInvocationTraces(
                snapshotToolInvocationTraces(),
                streamedFinalizerRecorder.SnapshotToolInvocationTraces());
        Func<IReadOnlyList<AgentFinalizerInvocation>> snapshotEffectiveFinalizerInvocations = () =>
            CreateEffectiveFinalizerInvocations(
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
            var snapshot = SnapshotUpdate(update);
            updates.Add(snapshot);

            if (!announcedStreaming && !string.IsNullOrWhiteSpace(snapshot.Text))
            {
                announcedStreaming = true;
                await progressCallback(ExecutionState.Running, "Streaming", "The agent is producing streamed output.");
            }

            foreach (var toolCall in snapshot.Contents.OfType<ToolCallContent>())
            {
                var toolKey = ResolveToolCallKey(toolCall);
                if (toolCall.CallId is null || guardedToolCallIds.Add(toolCall.CallId))
                {
                    repeatedToolInvocationGuard.Guard(toolCall);
                }

                streamedFinalizerRecorder.Record(toolCall);
                if (!announcedToolCalls.Add(toolKey))
                {
                    continue;
                }

                await progressCallback(ExecutionState.WaitingOnTool, "Tool", DescribeToolInvocation(toolCall));
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
            if (!ShouldRequestMissingRequiredFinalizerRepair(
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

            var finalizerTool = ResolveRequiredFinalizerTool(finalizerPolicy, finalizerTools);
            if (toolInvocationTraceRecorder is null)
            {
                throw new InvalidOperationException(
                    $"Cannot repair missing required finalizer '{finalizerPolicy.ToolName}' because tool invocation tracing is unavailable.");
            }

            var repairContext = BuildRequiredFinalizerRepairContext(
                response,
                snapshotEffectiveToolInvocationTraces(),
                inputMessages);
            var repairRunOptions = CreateRequiredFinalizerRepairRunOptions(finalizerPolicy, finalizerTool);
            var repairMessages = new[]
            {
                CreateRequiredFinalizerRepairMessage(finalizerPolicy, response, repairContext)
            };

            try
            {
                await foreach (var repairUpdate in RunProviderStreamingAsync(provider, resolvedModel, runtimeAgent, runtimeSession, repairMessages, repairRunOptions, cancellationToken))
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
            var jsonRepairRunOptions = CreateRequiredFinalizerJsonRepairRunOptions();
            var jsonRepairMessages = new[]
            {
                CreateRequiredFinalizerJsonRepairMessage(finalizerPolicy, previousResponse, repairContext)
            };
            var jsonRepairUpdates = new List<AgentResponseUpdate>();

            try
            {
                await foreach (var jsonRepairUpdate in RunProviderStreamingAsync(provider, resolvedModel, jsonRepairAgent, jsonRepairSession, jsonRepairMessages, jsonRepairRunOptions, cancellationToken))
                {
                    jsonRepairUpdates.Add(SnapshotUpdate(jsonRepairUpdate));
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
                    await foreach (var update in RunProviderStreamingAsync(provider, resolvedModel, runtimeAgent, runtimeSession, inputMessages, runOptions, cancellationToken))
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
            runOptions = CreateRunOptions(
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

    internal static bool ShouldRequestMissingRequiredFinalizerRepair(
        AgentStructuredOutputContract? structuredOutput,
        AgentFinalizerMode finalizerMode,
        AgentRuntimeExecutionOptions runtimeOptions,
        IReadOnlyCollection<ToolApprovalRequestContent> approvalRequests,
        IReadOnlyList<AgentFinalizerInvocation> finalizerInvocations,
        out AgentFinalizerPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(runtimeOptions);
        ArgumentNullException.ThrowIfNull(approvalRequests);
        ArgumentNullException.ThrowIfNull(finalizerInvocations);

        policy = AgentFinalizerPolicy.NotRequired;
        if (finalizerMode != AgentFinalizerMode.Required ||
            runtimeOptions.MaxStructuredOutputRepairAttempts <= 0 ||
            approvalRequests.Count > 0 ||
            !AgentFinalizerPolicies.TryResolveForStructuredOutput(structuredOutput, out policy))
        {
            return false;
        }

        var toolName = policy.ToolName;
        return !finalizerInvocations.Any(invocation =>
            string.Equals(invocation.ToolName, toolName, StringComparison.OrdinalIgnoreCase));
    }

    internal static AITool ResolveRequiredFinalizerTool(
        AgentFinalizerPolicy policy,
        IReadOnlyList<AITool> finalizerTools)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(finalizerTools);

        var matchingFinalizerTools = finalizerTools
            .Where(tool => string.Equals(tool.Name, policy.ToolName, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (matchingFinalizerTools.Count != 1)
        {
            throw new InvalidOperationException(
                $"Cannot repair missing required finalizer '{policy.ToolName}' because the runtime did not expose exactly one matching finalizer tool.");
        }

        return matchingFinalizerTools[0];
    }

    internal static void ConfigureRequiredFinalizerRepairChatOptions(
        ChatOptions chatOptions,
        AgentFinalizerPolicy policy,
        AITool finalizerTool)
    {
        ArgumentNullException.ThrowIfNull(chatOptions);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(finalizerTool);

        chatOptions.AllowMultipleToolCalls = false;
        chatOptions.Instructions = BuildRequiredFinalizerRepairInstructions(policy);
        chatOptions.Tools = [finalizerTool];
        chatOptions.ToolMode = ChatToolMode.RequireSpecific(policy.ToolName);
    }

    internal static ChatClientAgentRunOptions CreateRequiredFinalizerRepairRunOptions(
        AgentFinalizerPolicy policy,
        AITool finalizerTool)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(finalizerTool);

        var chatOptions = new ChatOptions
        {
            AllowMultipleToolCalls = false,
            Instructions = BuildRequiredFinalizerRepairInstructions(policy),
            Tools = [finalizerTool],
            ToolMode = ChatToolMode.RequireSpecific(policy.ToolName)
        };

        return new ChatClientAgentRunOptions(chatOptions)
        {
            AllowBackgroundResponses = false,
            ContinuationToken = null
        };
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
        var chatOptions = CreateModelCompatibleChatOptions(
            provider,
            model,
            (float)agent.Temperature,
            forceOmitTemperature,
            agent.ConfigurationJson);
        ConfigureRequiredFinalizerRepairChatOptions(chatOptions, policy, finalizerTool);

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
        return CreateInstrumentedAgent(
            CreateFrameworkAgent(provider, model, repairOptions, frameworkManagedHistory: false),
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
        var chatOptions = CreateModelCompatibleChatOptions(
            provider,
            model,
            (float)agent.Temperature,
            forceOmitTemperature,
            agent.ConfigurationJson);
        chatOptions.AllowMultipleToolCalls = false;
        chatOptions.Instructions = BuildRequiredFinalizerJsonRepairInstructions(policy);
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
        return CreateFrameworkAgent(provider, model, repairOptions, frameworkManagedHistory: false);
    }

    private static string BuildRequiredFinalizerRepairInstructions(
        AgentFinalizerPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        return
            $"You are completing a bounded finalizer repair turn for `{policy.OutputContract.ContractKey}`." + Environment.NewLine +
            $"Call `{policy.ToolName}` exactly once." + Environment.NewLine +
            "Do not call any other tool. Do not emit Markdown, prose, code fences, or machine JSON outside the finalizer tool call." + Environment.NewLine +
            BuildRequiredFinalizerArgumentInstructions(policy);
    }

    private static string BuildRequiredFinalizerJsonRepairInstructions(
        AgentFinalizerPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        return
            $"You are completing a bounded typed-output repair turn for `{policy.OutputContract.ContractKey}`." + Environment.NewLine +
            "Return exactly one JSON object matching the requested contract. Do not use Markdown, prose, code fences, or tool calls." + Environment.NewLine +
            BuildRequiredFinalizerArgumentInstructions(policy);
    }

    internal static ChatClientAgentRunOptions CreateRequiredFinalizerJsonRepairRunOptions()
    {
        return new ChatClientAgentRunOptions(new ChatOptions
        {
            AllowMultipleToolCalls = false,
            ToolMode = null,
            Tools = []
        })
        {
            AllowBackgroundResponses = false,
            ContinuationToken = null
        };
    }

    internal static ChatMessage CreateRequiredFinalizerJsonRepairMessage(
        AgentFinalizerPolicy policy,
        AgentResponse previousResponse)
        => CreateRequiredFinalizerJsonRepairMessage(policy, previousResponse, string.Empty);

    internal static ChatMessage CreateRequiredFinalizerJsonRepairMessage(
        AgentFinalizerPolicy policy,
        AgentResponse previousResponse,
        string repairContext)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(previousResponse);

        return new ChatMessage(
            ChatRole.User,
            BuildRequiredFinalizerJsonRepairPrompt(policy, previousResponse.Text, repairContext));
    }

    internal static string BuildRequiredFinalizerJsonRepairPrompt(
        AgentFinalizerPolicy policy,
        string? previousAssistantText)
        => BuildRequiredFinalizerJsonRepairPrompt(policy, previousAssistantText, string.Empty);

    internal static string BuildRequiredFinalizerJsonRepairPrompt(
        AgentFinalizerPolicy policy,
        string? previousAssistantText,
        string? repairContext)
    {
        ArgumentNullException.ThrowIfNull(policy);

        var previousTextSummary = BuildBoundedFinalizerRepairPreviousTextSummary(previousAssistantText);
        var repairContextSummary = BuildBoundedFinalizerRepairContextSummary(repairContext);

        return
            $"The previous repair turn could not submit `{policy.ToolName}` through provider tool calling." + Environment.NewLine +
            $"Return exactly one JSON object for `{policy.OutputContract.ContractKey}` now." + Environment.NewLine +
            "Use only the prior response text, session context, tool results, and process artifacts already available in the conversation. If evidence is insufficient, return the contract's blocking or failure state with actionable next actions where the contract supports them." + Environment.NewLine +
            "Do not return a generic no-prior-evidence blocker when the repair context below lists current-run tool calls, observed artifact refs, or primary managed output refs. If completion is impossible because a required managed output was not written, name that missing primary write ref and the next tool action that must create it." + Environment.NewLine +
            previousTextSummary + Environment.NewLine +
            repairContextSummary;
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

    internal static ChatMessage CreateRequiredFinalizerRepairMessage(
        AgentFinalizerPolicy policy,
        AgentResponse previousResponse)
        => CreateRequiredFinalizerRepairMessage(policy, previousResponse, string.Empty);

    internal static ChatMessage CreateRequiredFinalizerRepairMessage(
        AgentFinalizerPolicy policy,
        AgentResponse previousResponse,
        string repairContext)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(previousResponse);

        return new ChatMessage(
            ChatRole.User,
            BuildRequiredFinalizerRepairPrompt(policy, previousResponse.Text, repairContext));
    }

    internal static string BuildRequiredFinalizerRepairPrompt(
        AgentFinalizerPolicy policy,
        string? previousAssistantText)
        => BuildRequiredFinalizerRepairPrompt(policy, previousAssistantText, string.Empty);

    internal static string BuildRequiredFinalizerRepairPrompt(
        AgentFinalizerPolicy policy,
        string? previousAssistantText,
        string? repairContext)
    {
        ArgumentNullException.ThrowIfNull(policy);

        var previousTextSummary = BuildBoundedFinalizerRepairPreviousTextSummary(previousAssistantText);
        var repairContextSummary = BuildBoundedFinalizerRepairContextSummary(repairContext);

        return
            $"The previous turn ended without the required `{policy.ToolName}` finalizer tool call.{Environment.NewLine}" +
            $"Call `{policy.ToolName}` exactly once now to submit the final governed `{policy.OutputContract.ContractKey}` outcome.{Environment.NewLine}" +
            "Use only the current session context, prior tool results, and process artifacts. If the available evidence is insufficient for a successful outcome, submit the contract's failure or blocking state with actionable next actions where the contract supports them." + Environment.NewLine +
            "Do not submit a generic no-prior-evidence blocker when the repair context below lists current-run tool calls, observed artifact refs, or primary managed output refs. If completion is impossible because a required managed output was not written, name that missing primary write ref and the next tool action that must create it." + Environment.NewLine +
            "Do not call any other tool. Do not emit Markdown, prose, or machine JSON outside the finalizer tool call." + Environment.NewLine +
            previousTextSummary + Environment.NewLine +
            repairContextSummary;
    }

    internal static string BuildBoundedFinalizerRepairPreviousTextSummary(string? previousAssistantText)
    {
        if (string.IsNullOrWhiteSpace(previousAssistantText))
        {
            return "The previous turn returned no assistant text.";
        }

        var trimmed = previousAssistantText.Trim();
        if (trimmed.Length <= MaxFinalizerRepairPreviousAssistantTextCharacters)
        {
            return $"Previous assistant text:{Environment.NewLine}{trimmed}";
        }

        var headLength = MaxFinalizerRepairPreviousAssistantTextCharacters / 2;
        var tailLength = MaxFinalizerRepairPreviousAssistantTextCharacters - headLength;
        var head = trimmed[..headLength];
        var tail = trimmed[^tailLength..];
        return
            $"Previous assistant text (truncated from {trimmed.Length} to {MaxFinalizerRepairPreviousAssistantTextCharacters} characters for bounded finalizer repair):" + Environment.NewLine +
            head + Environment.NewLine +
            Environment.NewLine +
            "[... middle of previous assistant text omitted for bounded finalizer repair ...]" + Environment.NewLine +
            Environment.NewLine +
            tail;
    }

    private static string BuildRequiredFinalizerRepairContext(
        AgentResponse previousResponse,
        IReadOnlyList<AgentToolInvocationTrace> toolInvocationTraces,
        IEnumerable<ChatMessage> originalInputMessages)
    {
        ArgumentNullException.ThrowIfNull(previousResponse);
        ArgumentNullException.ThrowIfNull(toolInvocationTraces);
        ArgumentNullException.ThrowIfNull(originalInputMessages);

        var builder = new StringBuilder();
        var toolCallSummaries = BuildPreviousTurnToolCallSummaries(previousResponse);
        if (toolCallSummaries.Count > 0)
        {
            builder.AppendLine("Previous turn tool calls observed by the provider:");
            foreach (var summary in toolCallSummaries)
            {
                builder.AppendLine($"- {summary}");
            }
        }

        if (toolInvocationTraces.Count > 0)
        {
            if (builder.Length > 0)
            {
                builder.AppendLine();
            }

            builder.AppendLine("Previous turn tool trace results:");
            foreach (var trace in toolInvocationTraces.OrderBy(item => item.Sequence).Take(20))
            {
                var status = trace.CompletedAtUtc is null
                    ? "started"
                    : trace.Succeeded
                        ? "succeeded"
                        : $"failed: {WorkflowExecutorRedaction.RedactText(trace.FailureMessage)}";
                builder.AppendLine($"- #{trace.Sequence} {trace.ToolName}: {status}");
            }
        }

        var inputSummary = BuildRequiredFinalizerRepairInputSummary(originalInputMessages);
        if (!string.IsNullOrWhiteSpace(inputSummary))
        {
            if (builder.Length > 0)
            {
                builder.AppendLine();
            }

            builder.AppendLine("Original governed process brief lines relevant to finalization:");
            builder.Append(inputSummary);
        }

        return builder.ToString().Trim();
    }

    private static IReadOnlyList<string> BuildPreviousTurnToolCallSummaries(AgentResponse previousResponse)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var summaries = new List<string>();
        foreach (var toolCall in previousResponse.Messages.SelectMany(message => message.Contents).OfType<ToolCallContent>())
        {
            var key = ResolveToolCallKey(toolCall);
            if (!seen.Add(key))
            {
                continue;
            }

            summaries.Add(DescribeToolInvocation(toolCall));
            if (summaries.Count >= 20)
            {
                break;
            }
        }

        return summaries;
    }

    private static string BuildRequiredFinalizerRepairInputSummary(IEnumerable<ChatMessage> originalInputMessages)
    {
        var lines = new List<string>();
        foreach (var text in originalInputMessages
                     .SelectMany(message => message.Contents)
                     .OfType<TextContent>()
                     .Select(content => content.Text)
                     .Where(text => !string.IsNullOrWhiteSpace(text)))
        {
            foreach (var rawLine in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
            {
                var line = rawLine.Trim();
                if (!IsFinalizerRepairRelevantInputLine(line))
                {
                    continue;
                }

                lines.Add(line);
                if (lines.Count >= 120)
                {
                    return string.Join(Environment.NewLine, lines);
                }
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static bool IsFinalizerRepairRelevantInputLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        return line.StartsWith("Process:", StringComparison.OrdinalIgnoreCase) ||
               line.StartsWith("Step key:", StringComparison.OrdinalIgnoreCase) ||
               line.StartsWith("Step title:", StringComparison.OrdinalIgnoreCase) ||
               line.StartsWith("Process run id:", StringComparison.OrdinalIgnoreCase) ||
               line.StartsWith("Managed artifact root:", StringComparison.OrdinalIgnoreCase) ||
               line.StartsWith("Allowed operations:", StringComparison.OrdinalIgnoreCase) ||
               line.StartsWith("Operation target scope:", StringComparison.OrdinalIgnoreCase) ||
               line.StartsWith("Required upstream artifact slots:", StringComparison.OrdinalIgnoreCase) ||
               line.StartsWith("Produced artifact slots:", StringComparison.OrdinalIgnoreCase) ||
               line.StartsWith("Artifact refs to inspect", StringComparison.OrdinalIgnoreCase) ||
               line.StartsWith("Expectation key rule:", StringComparison.OrdinalIgnoreCase) ||
               line.StartsWith("Primary write ref:", StringComparison.OrdinalIgnoreCase) ||
               line.StartsWith("Runtime rule:", StringComparison.OrdinalIgnoreCase) ||
               line.StartsWith("Completion rule:", StringComparison.OrdinalIgnoreCase) ||
               line.StartsWith("Validation:", StringComparison.OrdinalIgnoreCase) ||
               line.Contains("workspace_write_file", StringComparison.OrdinalIgnoreCase) ||
               line.Contains("workspace_read_file", StringComparison.OrdinalIgnoreCase) ||
               line.Contains("submit_process_step_outcome", StringComparison.OrdinalIgnoreCase) ||
               line.Contains("evidenceRefs", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildBoundedFinalizerRepairContextSummary(string? repairContext)
    {
        if (string.IsNullOrWhiteSpace(repairContext))
        {
            return "Repair context summary: no prior tool call or governed brief summary was available.";
        }

        var trimmed = repairContext.Trim();
        if (trimmed.Length <= MaxFinalizerRepairPreviousAssistantTextCharacters)
        {
            return $"Repair context summary:{Environment.NewLine}{trimmed}";
        }

        var headLength = MaxFinalizerRepairPreviousAssistantTextCharacters / 2;
        var tailLength = MaxFinalizerRepairPreviousAssistantTextCharacters - headLength;
        var head = trimmed[..headLength];
        var tail = trimmed[^tailLength..];
        return
            $"Repair context summary (truncated from {trimmed.Length} to {MaxFinalizerRepairPreviousAssistantTextCharacters} characters):" + Environment.NewLine +
            head + Environment.NewLine +
            Environment.NewLine +
            "[... middle of repair context omitted for bounded finalizer repair ...]" + Environment.NewLine +
            Environment.NewLine +
            tail;
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
        if (!TryNormalizeFinalizerJsonRepairText(policy, repairText, out var argumentsJson, out failureMessage))
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

    internal static bool TryNormalizeFinalizerJsonRepairText(
        AgentFinalizerPolicy policy,
        string? repairText,
        out string argumentsJson,
        out string failureMessage)
    {
        ArgumentNullException.ThrowIfNull(policy);

        argumentsJson = string.Empty;
        failureMessage = string.Empty;
        if (!TryExtractJsonObject(repairText, out var rawJson))
        {
            failureMessage = "No JSON object was found in the repair response.";
            return false;
        }

        if (TryNormalizeKnownFinalizerOutput(policy, rawJson, out argumentsJson, out failureMessage))
        {
            return true;
        }

        if (TryDeserializeFinalizerOutput(policy, rawJson, out argumentsJson, out failureMessage))
        {
            return true;
        }

        try
        {
            using var document = JsonDocument.Parse(rawJson);
            if (document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty("result", out var resultElement) &&
                resultElement.ValueKind == JsonValueKind.Object)
            {
                var resultRawJson = resultElement.GetRawText();
                if (TryNormalizeKnownFinalizerOutput(policy, resultRawJson, out argumentsJson, out failureMessage))
                {
                    return true;
                }

                if (TryDeserializeFinalizerOutput(policy, resultRawJson, out argumentsJson, out failureMessage))
                {
                    return true;
                }
            }
        }
        catch (JsonException exception)
        {
            failureMessage = exception.Message;
        }

        return false;
    }

    private static bool TryDeserializeFinalizerOutput(
        AgentFinalizerPolicy policy,
        string rawJson,
        out string argumentsJson,
        out string failureMessage)
    {
        argumentsJson = string.Empty;
        failureMessage = string.Empty;

        try
        {
            var output = JsonSerializer.Deserialize(rawJson, policy.OutputType, AgentOutputJson.SerializerOptions);
            if (output is null)
            {
                failureMessage = "The JSON payload deserialized to null.";
                return false;
            }

            argumentsJson = JsonSerializer.Serialize(output, policy.OutputType, AgentOutputJson.SerializerOptions);
            return true;
        }
        catch (JsonException exception)
        {
            failureMessage = exception.Message;
            return false;
        }
    }

    private static bool TryNormalizeKnownFinalizerOutput(
        AgentFinalizerPolicy policy,
        string rawJson,
        out string argumentsJson,
        out string failureMessage)
    {
        argumentsJson = string.Empty;
        failureMessage = string.Empty;

        if (policy.OutputType == typeof(ProcessStepOutcomeResult))
        {
            return TryNormalizeProcessStepOutcomeResultJson(rawJson, out argumentsJson, out failureMessage);
        }

        return false;
    }

    private static bool TryNormalizeProcessStepOutcomeResultJson(
        string rawJson,
        out string argumentsJson,
        out string failureMessage)
    {
        argumentsJson = string.Empty;
        failureMessage = string.Empty;

        try
        {
            if (JsonNode.Parse(rawJson) is not JsonObject jsonObject)
            {
                failureMessage = "The JSON payload was not an object.";
                return false;
            }

            NormalizeStringArrayProperty(jsonObject, "evidenceRefs");
            NormalizeStringArrayProperty(jsonObject, "nextActions");
            NormalizeProcessStepOutcomeReason(jsonObject);

            var output = jsonObject.Deserialize<ProcessStepOutcomeResult>(AgentOutputJson.SerializerOptions);
            if (output is null)
            {
                failureMessage = "The normalized JSON payload deserialized to null.";
                return false;
            }

            argumentsJson = JsonSerializer.Serialize(output, AgentOutputJson.SerializerOptions);
            return true;
        }
        catch (JsonException exception)
        {
            failureMessage = exception.Message;
            return false;
        }
    }

    private static void NormalizeProcessStepOutcomeReason(JsonObject jsonObject)
    {
        if (TryReadNonEmptyStringProperty(jsonObject, "reason", out _))
        {
            return;
        }

        if (TryReadNonEmptyStringProperty(jsonObject, "humanReadableSummaryMarkdown", out var humanSummary) ||
            TryReadNonEmptyStringProperty(jsonObject, "branchOutcomeTitle", out humanSummary))
        {
            jsonObject["reason"] = humanSummary;
        }
    }

    private static bool TryReadNonEmptyStringProperty(
        JsonObject jsonObject,
        string propertyName,
        out string value)
    {
        value = string.Empty;
        if (!jsonObject.TryGetPropertyValue(propertyName, out var node))
        {
            return false;
        }

        value = ConvertJsonNodeToString(node).Trim();
        return !string.IsNullOrWhiteSpace(value);
    }

    private static void NormalizeStringArrayProperty(JsonObject jsonObject, string propertyName)
    {
        if (!jsonObject.TryGetPropertyValue(propertyName, out var value) ||
            value is not JsonArray values)
        {
            return;
        }

        var normalizedValues = new JsonArray();
        foreach (var item in values)
        {
            var text = ConvertJsonNodeToString(item);
            if (!string.IsNullOrWhiteSpace(text))
            {
                normalizedValues.Add(text);
            }
        }

        jsonObject[propertyName] = normalizedValues;
    }

    private static string ConvertJsonNodeToString(JsonNode? node)
    {
        if (node is null)
        {
            return string.Empty;
        }

        if (node is JsonValue value)
        {
            return value.TryGetValue<string>(out var text)
                ? text
                : value.ToJsonString();
        }

        if (node is JsonObject jsonObject)
        {
            return string.Join(
                "; ",
                jsonObject.Select(property => $"{property.Key}: {ConvertJsonNodeToString(property.Value)}"));
        }

        if (node is JsonArray jsonArray)
        {
            return string.Join(", ", jsonArray.Select(ConvertJsonNodeToString));
        }

        return node.ToJsonString();
    }

    private static bool TryExtractJsonObject(
        string? text,
        out string rawJson)
    {
        rawJson = string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var firstLineEnd = trimmed.IndexOf('\n');
            var fenceEnd = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            if (firstLineEnd >= 0 && fenceEnd > firstLineEnd)
            {
                trimmed = trimmed[(firstLineEnd + 1)..fenceEnd].Trim();
            }
        }

        if (trimmed.StartsWith('{') && trimmed.EndsWith('}'))
        {
            rawJson = trimmed;
            return true;
        }

        var start = trimmed.IndexOf('{');
        var end = trimmed.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            return false;
        }

        rawJson = trimmed[start..(end + 1)].Trim();
        return true;
    }

    internal static IReadOnlyList<AgentFinalizerInvocation> CreateEffectiveFinalizerInvocations(
        AgentStructuredOutputContract? structuredOutput,
        AgentFinalizerMode finalizerMode,
        IReadOnlyList<AgentFinalizerInvocation> capturedInvocations,
        IReadOnlyList<AgentToolInvocationTrace> capturedToolInvocationTraces,
        IReadOnlyList<AgentFinalizerInvocation> streamedInvocations,
        IReadOnlyList<AgentFinalizerInvocation> synthesizedInvocations)
    {
        if (finalizerMode != AgentFinalizerMode.Required ||
            !AgentFinalizerPolicies.TryResolveForStructuredOutput(structuredOutput, out var policy))
        {
            return synthesizedInvocations.Count == 0
                ? capturedInvocations
                : capturedInvocations
                    .Concat(synthesizedInvocations)
                    .OrderBy(invocation => invocation.Sequence)
                    .ToList();
        }

        var normalizedCapturedInvocations = AgentFinalizerInvocationNormalizer.NormalizeRequired(policy, capturedInvocations);
        if (IsValidFinalizerInvocationSet(policy, normalizedCapturedInvocations))
        {
            return normalizedCapturedInvocations;
        }

        if (TrySelectLastValidFinalizerInvocation(policy, capturedInvocations, out var capturedInvocation))
        {
            return [capturedInvocation];
        }

        var normalizedStreamedInvocations = AgentFinalizerInvocationNormalizer.NormalizeRequired(policy, streamedInvocations);
        if (IsValidFinalizerInvocationSet(policy, normalizedStreamedInvocations))
        {
            return normalizedStreamedInvocations;
        }

        if (TrySelectLastValidFinalizerInvocation(policy, streamedInvocations, out var streamedInvocation))
        {
            return [streamedInvocation];
        }

        var normalizedSynthesizedInvocations = AgentFinalizerInvocationNormalizer.NormalizeRequired(policy, synthesizedInvocations);
        if (IsValidFinalizerInvocationSet(policy, normalizedSynthesizedInvocations))
        {
            return normalizedSynthesizedInvocations;
        }

        if (TrySelectLastValidFinalizerInvocation(policy, synthesizedInvocations, out var synthesizedInvocation))
        {
            return [synthesizedInvocation];
        }

        if (synthesizedInvocations.Count > 0)
        {
            return synthesizedInvocations;
        }

        if (capturedInvocations.Count > 0)
        {
            return capturedInvocations;
        }

        return streamedInvocations;
    }

    private static bool IsValidFinalizerInvocationSet(
        AgentFinalizerPolicy policy,
        IReadOnlyList<AgentFinalizerInvocation> invocations)
    {
        if (invocations.Count == 0)
        {
            return false;
        }

        var validation = new DefaultAgentFinalizerValidator().Validate(policy, invocations);
        return validation.Succeeded && validation.Output is not null;
    }

    private static bool TrySelectLastValidFinalizerInvocation(
        AgentFinalizerPolicy policy,
        IReadOnlyList<AgentFinalizerInvocation> invocations,
        out AgentFinalizerInvocation invocation)
    {
        invocation = default!;
        for (var index = invocations.Count - 1; index >= 0; index--)
        {
            var candidate = invocations[index];
            var validation = new DefaultAgentFinalizerValidator().Validate(policy, [candidate]);
            if (validation.Succeeded && validation.Output is not null)
            {
                invocation = candidate;
                return true;
            }
        }

        return false;
    }

    private static IReadOnlyList<AgentToolInvocationTrace> CreateEffectiveToolInvocationTraces(
        IReadOnlyList<AgentToolInvocationTrace> capturedToolInvocationTraces,
        IReadOnlyList<AgentToolInvocationTrace> streamedToolInvocationTraces)
    {
        if (streamedToolInvocationTraces.Count == 0)
        {
            return capturedToolInvocationTraces;
        }

        var capturedToolNames = capturedToolInvocationTraces
            .Select(trace => trace.ToolName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missingStreamedTraces = streamedToolInvocationTraces
            .Where(trace => !capturedToolNames.Contains(trace.ToolName))
            .ToList();
        if (missingStreamedTraces.Count == 0)
        {
            return capturedToolInvocationTraces;
        }

        return capturedToolInvocationTraces
            .Concat(missingStreamedTraces)
            .OrderBy(trace => trace.Sequence)
            .ToList();
    }

    private static AgentFinalizerInvocation? TryCreateStreamedFinalizerInvocation(
        AgentFinalizerPolicy policy,
        ToolCallContent toolCall,
        int sequence)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(toolCall);

        if (!policy.IsRequired ||
            !string.Equals(ResolveToolName(toolCall), policy.ToolName, StringComparison.OrdinalIgnoreCase) ||
            toolCall is not FunctionCallContent functionCall ||
            functionCall.Arguments is null)
        {
            return null;
        }

        var payload = functionCall.Arguments.Count == 1 &&
                      functionCall.Arguments.TryGetValue("result", out var result)
            ? result
            : functionCall.Arguments;
        var argumentsJson = SerializeStreamedFinalizerPayload(payload);
        if (string.IsNullOrWhiteSpace(argumentsJson))
        {
            return null;
        }

        return new AgentFinalizerInvocation(
            policy.ToolName,
            argumentsJson,
            sequence);
    }

    private static string SerializeStreamedFinalizerPayload(object? payload)
    {
        return payload switch
        {
            null => "null",
            JsonElement jsonElement => SerializeStreamedFinalizerJsonElement(jsonElement),
            string text when TryUseRawJsonObjectOrArray(text, out var rawJson) => rawJson,
            string text => JsonSerializer.Serialize(text, AgentOutputJson.SerializerOptions),
            _ => JsonSerializer.Serialize(payload, AgentOutputJson.SerializerOptions)
        };
    }

    private static string SerializeStreamedFinalizerJsonElement(JsonElement jsonElement)
    {
        if (jsonElement.ValueKind == JsonValueKind.String &&
            TryUseRawJsonObjectOrArray(jsonElement.GetString(), out var rawJson))
        {
            return rawJson;
        }

        return jsonElement.GetRawText();
    }

    private static bool TryUseRawJsonObjectOrArray(string? text, out string rawJson)
    {
        rawJson = string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.Trim();
        if ((trimmed.StartsWith('{') && trimmed.EndsWith('}')) ||
            (trimmed.StartsWith('[') && trimmed.EndsWith(']')))
        {
            rawJson = trimmed;
            return true;
        }

        return false;
    }

    internal sealed class StreamedFinalizerInvocationRecorder(
        AgentStructuredOutputContract? structuredOutput,
        AgentFinalizerMode finalizerMode)
    {
        private readonly object gate = new();
        private readonly AgentFinalizerPolicy? policy = finalizerMode == AgentFinalizerMode.Required &&
                                                        AgentFinalizerPolicies.TryResolveForStructuredOutput(structuredOutput, out var resolvedPolicy)
            ? resolvedPolicy
            : null;
        private readonly List<AgentFinalizerInvocation> finalizerInvocations = [];
        private readonly List<AgentToolInvocationTrace> toolInvocationTraces = [];
        private int nextSequence;

        public void Record(ToolCallContent toolCall)
        {
            var sequence = Interlocked.Increment(ref nextSequence);
            if (policy is null)
            {
                return;
            }

            var invocation = TryCreateStreamedFinalizerInvocation(policy, toolCall, sequence);
            if (invocation is null)
            {
                return;
            }

            lock (gate)
            {
                finalizerInvocations.Add(invocation);
                toolInvocationTraces.Add(new AgentToolInvocationTrace(
                    policy.ToolName,
                    ToolInvocationClassification.Read,
                    sequence,
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow,
                    Succeeded: true,
                    FailureMessage: "Captured from a streamed required-finalizer tool call."));
            }
        }

        public IReadOnlyList<AgentFinalizerInvocation> SnapshotFinalizerInvocations()
        {
            lock (gate)
            {
                return finalizerInvocations.ToList();
            }
        }

        public IReadOnlyList<AgentToolInvocationTrace> SnapshotToolInvocationTraces()
        {
            lock (gate)
            {
                return toolInvocationTraces.ToList();
            }
        }
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
        if (!TryBuildCurrentStepPrimaryManagedArtifactPath(contextIntent, out var primaryArtifactRef, out _))
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

        if (!TryCreateProcessStepOutcomeFromPrimaryArtifact(
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

    internal static bool TryCreateProcessStepOutcomeFromPrimaryArtifact(
        AgentRuntimeContextIntent contextIntent,
        string primaryArtifactRef,
        string artifactMarkdown,
        out ProcessStepOutcomeResult outcome,
        out string failureMessage)
    {
        outcome = default!;
        failureMessage = string.Empty;

        if (!contextIntent.IsGovernedProcessStep ||
            !string.Equals(contextIntent.SourceKind, "process-step", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(contextIntent.ProcessRunId) ||
            string.IsNullOrWhiteSpace(contextIntent.SourceId))
        {
            failureMessage = "The runtime context is not a governed process step.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(primaryArtifactRef))
        {
            failureMessage = "The primary artifact reference is required.";
            return false;
        }

        var statusWasDeclared = TryReadProcessArtifactStatus(artifactMarkdown, out var status, out var hasStatusLine);
        if (!statusWasDeclared &&
            hasStatusLine)
        {
            failureMessage = "The primary process artifact does not contain a recoverable Status line.";
            return false;
        }

        if (!statusWasDeclared &&
            !TryInferProcessArtifactStatus(artifactMarkdown, out status))
        {
            failureMessage = "The primary process artifact is empty or does not contain recoverable process outcome evidence.";
            return false;
        }

        if (statusWasDeclared &&
            status == ProcessStepOutcomeStatus.Blocked &&
            IsStatusOnlyRecoveredBlockedArtifact(artifactMarkdown))
        {
            failureMessage = "The primary process artifact declares Blocked without concrete blocker evidence.";
            return false;
        }

        if (!TryReadProcessArtifactBranchOutcomeKey(
            artifactMarkdown,
            out var branchOutcomeKey,
            out var branchOutcomeFailure))
        {
            failureMessage = branchOutcomeFailure;
            return false;
        }

        var reason =
            statusWasDeclared
                ? $"Recovered governed process step outcome from primary managed artifact '{primaryArtifactRef}' after provider timeout. The artifact declares status '{status}'."
                : $"Recovered governed process step outcome from primary managed artifact '{primaryArtifactRef}' after provider timeout. The artifact did not declare a Status line, so the runtime inferred status '{status}' from the artifact text.";
        outcome = new ProcessStepOutcomeResult
        {
            Status = status,
            Reason = reason,
            BranchOutcomeKey = branchOutcomeKey,
            EvidenceRefs = [primaryArtifactRef],
            NextActions = CreateRecoveredProcessArtifactNextActions(status, primaryArtifactRef),
            HumanReadableSummaryMarkdown = BuildRecoveredProcessArtifactSummary(primaryArtifactRef, artifactMarkdown)
        };
        return true;
    }

    internal static bool TryBuildCurrentStepPrimaryManagedArtifactPath(
        AgentRuntimeContextIntent contextIntent,
        out string primaryArtifactRef,
        out string failureMessage)
    {
        primaryArtifactRef = string.Empty;
        failureMessage = string.Empty;

        if (!Guid.TryParse(contextIntent.ProcessRunId, out var processRunId))
        {
            failureMessage = "The process run id is not a GUID.";
            return false;
        }

        var sourceId = contextIntent.SourceId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(sourceId) ||
            sourceId.Contains('/') ||
            sourceId.Contains('\\') ||
            sourceId.Contains("..", StringComparison.Ordinal))
        {
            failureMessage = "The process step source id is not a safe artifact file name.";
            return false;
        }

        primaryArtifactRef = WorkspaceScopeDescriptor.NormalizeRelativePath(
            $"artifacts/process-runs/{processRunId:D}/steps/{sourceId}.md");
        return true;
    }

    private static bool TryReadProcessArtifactStatus(
        string artifactMarkdown,
        out ProcessStepOutcomeStatus status,
        out bool hasStatusLine)
    {
        status = default;
        hasStatusLine = false;
        foreach (var rawLine in artifactMarkdown.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim().TrimStart('#', '-', '*', ' ');
            var separatorIndex = line.IndexOf(':', StringComparison.Ordinal);
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = line[..separatorIndex].Trim(' ', '*', '`');
            if (!string.Equals(key, "Status", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            hasStatusLine = true;
            return TryMapProcessArtifactStatus(line[(separatorIndex + 1)..], out status);
        }

        return false;
    }

    private static bool TryReadProcessArtifactBranchOutcomeKey(
        string artifactMarkdown,
        out string branchOutcomeKey,
        out string failureMessage)
    {
        branchOutcomeKey = string.Empty;
        failureMessage = string.Empty;
        var declaredKeys = new HashSet<string>(StringComparer.Ordinal);
        var lines = artifactMarkdown.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

        for (var index = 0; index < lines.Length; index++)
        {
            var line = NormalizeProcessArtifactMetadataLine(lines[index]);
            var separatorIndex = line.IndexOf(':', StringComparison.Ordinal);
            if (separatorIndex <= 0)
            {
                if (!string.Equals(line, ProcessArtifactBranchOutcomeKeyLineKey, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (index + 1 >= lines.Length ||
                    !TryAddProcessArtifactBranchOutcomeKey(
                        NormalizeProcessArtifactBranchOutcomeKeyValue(NormalizeProcessArtifactMetadataLine(lines[index + 1])),
                        declaredKeys,
                        out failureMessage))
                {
                    failureMessage = string.IsNullOrWhiteSpace(failureMessage)
                        ? "The primary process artifact contains an invalid Branch outcome key section."
                        : failureMessage;
                    return false;
                }

                continue;
            }

            var key = line[..separatorIndex].Trim(' ', '*', '`');
            if (!string.Equals(key, ProcessArtifactBranchOutcomeKeyLineKey, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = NormalizeProcessArtifactBranchOutcomeKeyValue(line[(separatorIndex + 1)..]);
            if (!TryAddProcessArtifactBranchOutcomeKey(value, declaredKeys, out failureMessage))
            {
                return false;
            }
        }

        branchOutcomeKey = declaredKeys.SingleOrDefault() ?? string.Empty;
        return true;
    }

    private static string NormalizeProcessArtifactMetadataLine(string value)
        => value.Trim().TrimStart('#', '-', '*', ' ');

    private static bool TryAddProcessArtifactBranchOutcomeKey(
        string value,
        ISet<string> declaredKeys,
        out string failureMessage)
    {
        failureMessage = string.Empty;
        if (!IsSafeProcessArtifactBranchOutcomeKey(value))
        {
            failureMessage = "The primary process artifact contains an invalid Branch outcome key line.";
            return false;
        }

        declaredKeys.Add(value);
        if (declaredKeys.Count <= 1)
        {
            return true;
        }

        failureMessage = "The primary process artifact contains multiple different Branch outcome key lines.";
        return false;
    }

    private static string NormalizeProcessArtifactBranchOutcomeKeyValue(string value)
    {
        var trimmed = value.Trim().Trim('*', '`', '.', ';');
        var commentIndex = trimmed.IndexOf('#', StringComparison.Ordinal);
        if (commentIndex >= 0)
        {
            trimmed = trimmed[..commentIndex].Trim();
        }

        return trimmed.Trim('*', '`', '.', ';');
    }

    private static bool IsSafeProcessArtifactBranchOutcomeKey(string value)
        => !string.IsNullOrWhiteSpace(value) &&
           char.IsLetterOrDigit(value[0]) &&
           value.All(character => char.IsLetterOrDigit(character) || character is '-' or '_' or '.');

    private static bool TryInferProcessArtifactStatus(
        string artifactMarkdown,
        out ProcessStepOutcomeStatus status)
    {
        status = default;
        var text = artifactMarkdown.ReplaceLineEndings(" ").Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        if (ContainsAny(
            text,
            "waiting approval",
            "approval required",
            "pending approval",
            "human approval"))
        {
            status = ProcessStepOutcomeStatus.WaitingApproval;
            return true;
        }

        if (ContainsAny(
            text,
            "blocked",
            "cannot proceed",
            "unable to proceed",
            "missing required",
            "requires manager",
            "manager action required",
            "policydenied",
            "permission denied",
            "access denied",
            "not authorized"))
        {
            status = ProcessStepOutcomeStatus.Blocked;
            return true;
        }

        if (ContainsAny(
            text,
            "unrecoverable failure",
            "unrecoverable error",
            "execution failed",
            "validation failed",
            "failed to complete"))
        {
            status = ProcessStepOutcomeStatus.Failed;
            return true;
        }

        status = ProcessStepOutcomeStatus.Completed;
        return true;
    }

    private static bool ContainsAny(string text, params string[] values)
        => values.Any(value => text.Contains(value, StringComparison.OrdinalIgnoreCase));

    private static bool IsStatusOnlyRecoveredBlockedArtifact(string artifactMarkdown)
    {
        var normalized = artifactMarkdown.Trim();
        if (normalized.Length > 700)
        {
            return false;
        }

        return !ContainsAny(
            normalized,
            "PolicyDenied",
            "denied",
            "failed",
            "failure",
            "exception",
            "error",
            "cannot proceed",
            "unable to proceed",
            "missing",
            "required tool",
            "unavailable",
            "approval",
            "dependency",
            "environment",
            "boundary",
            "evidence",
            "receipt");
    }

    private static bool TryMapProcessArtifactStatus(
        string value,
        out ProcessStepOutcomeStatus status)
    {
        status = default;
        var normalized = NormalizeProcessArtifactStatusValue(value);
        status = normalized switch
        {
            "completed" or "complete" or "succeeded" or "success" => ProcessStepOutcomeStatus.Completed,
            "blocked" or "waiting" or "waitingonchild" or "waitingforchild" => ProcessStepOutcomeStatus.Blocked,
            "failed" or "failure" => ProcessStepOutcomeStatus.Failed,
            "waitingapproval" or "pendingapproval" => ProcessStepOutcomeStatus.WaitingApproval,
            "refused" or "rejected" => ProcessStepOutcomeStatus.Refused,
            _ => default
        };
        return normalized is "completed" or "complete" or "succeeded" or "success" or
            "blocked" or "waiting" or "waitingonchild" or "waitingforchild" or
            "failed" or "failure" or
            "waitingapproval" or "pendingapproval" or
            "refused" or "rejected";
    }

    private static string NormalizeProcessArtifactStatusValue(string value)
    {
        var trimmed = value.Trim().Trim('*', '`', '.', ';');
        var commentIndex = trimmed.IndexOf('#', StringComparison.Ordinal);
        if (commentIndex >= 0)
        {
            trimmed = trimmed[..commentIndex].Trim();
        }

        return new string(
            trimmed
                .Where(character => char.IsLetterOrDigit(character))
                .Select(char.ToLowerInvariant)
                .ToArray());
    }

    private static IReadOnlyList<string> CreateRecoveredProcessArtifactNextActions(
        ProcessStepOutcomeStatus status,
        string primaryArtifactRef)
    {
        if (status == ProcessStepOutcomeStatus.Completed)
        {
            return [];
        }

        return
        [
            $"Review '{primaryArtifactRef}' and re-dispatch or rework the governed process step with the recorded evidence."
        ];
    }

    private static string BuildRecoveredProcessArtifactSummary(
        string primaryArtifactRef,
        string artifactMarkdown)
    {
        var trimmed = string.IsNullOrWhiteSpace(artifactMarkdown)
            ? string.Empty
            : artifactMarkdown.Trim();
        if (trimmed.Length > MaxRecoveredProcessArtifactSummaryCharacters)
        {
            trimmed = trimmed[..MaxRecoveredProcessArtifactSummaryCharacters] + Environment.NewLine + "[... artifact summary truncated during provider-timeout recovery ...]";
        }

        return string.IsNullOrWhiteSpace(trimmed)
            ? $"Recovered outcome from primary process artifact `{primaryArtifactRef}` after provider timeout."
            : $"Recovered outcome from primary process artifact `{primaryArtifactRef}` after provider timeout.{Environment.NewLine}{Environment.NewLine}{trimmed}";
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
                HasRequestScopedInputAttachments(runtimeOptions));
    }

    private static bool HasRequestScopedInputAttachments(AgentRuntimeExecutionOptions runtimeOptions)
        => runtimeOptions.InputAttachments?.Count > 0;

    internal static void EnsureInputAttachmentsSupported(
        ProviderProfile provider,
        string model,
        AgentRuntimeExecutionOptions runtimeOptions)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(runtimeOptions);

        var attachments = runtimeOptions.InputAttachments ?? [];
        if (attachments.Count == 0)
        {
            return;
        }

        var selectedModel = string.IsNullOrWhiteSpace(model) ? provider.DefaultModel : model.Trim();
        var featureMatrix = ProviderFeatureService.ResolveFeatureMatrixForModel(provider, selectedModel);
        if (featureMatrix.SupportsVision)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Provider '{provider.Name}' model '{selectedModel}' does not support vision/image input, but the request includes {attachments.Count:N0} image attachment(s). Choose a vision-capable provider/model or remove the attachment(s).");
    }

    internal static string ResolveRuntimeModelForInputAttachments(
        ProviderProfile provider,
        string model,
        AgentRuntimeExecutionOptions runtimeOptions)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(runtimeOptions);

        var selectedModel = string.IsNullOrWhiteSpace(model) ? provider.DefaultModel : model.Trim();
        var attachments = runtimeOptions.InputAttachments ?? [];
        if (attachments.Count == 0)
        {
            return selectedModel;
        }

        if (ProviderFeatureService.ResolveFeatureMatrixForModel(provider, selectedModel).SupportsVision)
        {
            return selectedModel;
        }

        var imageAnalysisModel = ResolveProviderImageAnalysisModel(provider, selectedModel);
        return ProviderFeatureService.ResolveFeatureMatrixForModel(provider, imageAnalysisModel).SupportsVision
            ? imageAnalysisModel
            : selectedModel;
    }

    private static string ResolveRuntimeSessionSerializationSkipMessage(AgentRuntimeExecutionOptions runtimeOptions)
    {
        return HasRequestScopedInputAttachments(runtimeOptions)
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

        if (!HasRequestScopedInputAttachments(runtimeOptions))
        {
            return serializedSessionJson;
        }

        var scrubbedSessionJson = RemoveRequestScopedDataContentFromSerializedSession(serializedSessionJson);
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

    internal static string? RemoveRequestScopedDataContentFromSerializedSession(string? serializedSessionJson)
    {
        if (string.IsNullOrWhiteSpace(serializedSessionJson))
        {
            return serializedSessionJson;
        }

        try
        {
            var root = JsonNode.Parse(serializedSessionJson);
            if (root is null)
            {
                return serializedSessionJson;
            }

            return RemoveRequestScopedDataContentNodes(root)
                ? root.ToJsonString(SerializerOptions)
                : serializedSessionJson;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool RemoveRequestScopedDataContentNodes(JsonNode node)
    {
        return node switch
        {
            JsonObject jsonObject => RemoveRequestScopedDataContentNodes(jsonObject),
            JsonArray jsonArray => RemoveRequestScopedDataContentNodes(jsonArray),
            _ => false
        };
    }

    private static bool RemoveRequestScopedDataContentNodes(JsonObject jsonObject)
    {
        var removedAny = false;
        foreach (var property in jsonObject.ToList())
        {
            if (property.Value is not null)
            {
                removedAny |= RemoveRequestScopedDataContentNodes(property.Value);
            }
        }

        return removedAny;
    }

    private static bool RemoveRequestScopedDataContentNodes(JsonArray jsonArray)
    {
        var removedAny = false;
        var dataContentIndexes = new List<int>();
        for (var index = 0; index < jsonArray.Count; index++)
        {
            var item = jsonArray[index];
            if (IsRequestScopedDataContentNode(item))
            {
                dataContentIndexes.Add(index);
                continue;
            }

            if (item is not null)
            {
                removedAny |= RemoveRequestScopedDataContentNodes(item);
            }
        }

        for (var index = dataContentIndexes.Count - 1; index >= 0; index--)
        {
            jsonArray.RemoveAt(dataContentIndexes[index]);
            removedAny = true;
        }

        if (dataContentIndexes.Count > 0 && jsonArray.Count == 0)
        {
            jsonArray.Add(new JsonObject
            {
                ["$type"] = "text",
                ["text"] = "[Request-scoped attachment omitted from persisted session state.]"
            });
        }

        return removedAny;
    }

    private static bool IsRequestScopedDataContentNode(JsonNode? node)
    {
        return node is JsonObject jsonObject &&
               jsonObject.TryGetPropertyValue("$type", out var typeNode) &&
               string.Equals(typeNode?.GetValue<string>(), "data", StringComparison.OrdinalIgnoreCase);
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
        var arguments = DeserializeArguments(record.ArgumentsJson);
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
            ToolName: ResolveToolName(toolCall),
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
                var argumentSummary = DescribeArguments(item.ArgumentsJson);
                return item.ToolKind == "mcp"
                    ? $"- Approval required for MCP tool '{item.ToolName}' on server '{item.Details}'{FormatInlineArgumentSummary(argumentSummary)}."
                    : $"- Approval required for tool '{item.ToolName}'{FormatInlineArgumentSummary(argumentSummary)}.";
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
            && SupportsBackgroundResponses(provider)
            && response.ContinuationToken is not null;
    }

    private static int CountToolCalls(AgentResponse response)
    {
        return response.Messages
            .SelectMany(message => message.Contents)
            .Select(content => content switch
            {
                ToolApprovalRequestContent approval => approval.ToolCall?.CallId ?? approval.ToolCall?.ToString(),
                ToolCallContent toolCall => toolCall.CallId ?? ResolveToolName(toolCall),
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

    private sealed class RequiredFinalizerCapturedException(string toolName) : Exception(
        $"Required finalizer tool '{toolName}' was captured.")
    {
        public string ToolName { get; } = toolName;
    }

    private static string ResolveToolName(ToolCallContent toolCall)
    {
        return toolCall switch
        {
            FunctionCallContent functionCall when !string.IsNullOrWhiteSpace(functionCall.Name) => functionCall.Name,
            McpServerToolCallContent mcpToolCall when !string.IsNullOrWhiteSpace(mcpToolCall.Name) => mcpToolCall.Name,
            _ => "Unnamed tool"
        };
    }

    private static string ResolveToolCallKey(ToolCallContent toolCall)
    {
        return toolCall.CallId
            ?? $"{ResolveToolName(toolCall)}|{DescribeToolCallArguments(toolCall)}";
    }

    private sealed class RepeatedToolInvocationGuard
    {
        private readonly Dictionary<string, int> repeatedToolInvocationCounts = new(StringComparer.OrdinalIgnoreCase);
        private int mutationGeneration;

        public void Guard(ToolCallContent toolCall)
        {
            var toolName = ResolveToolName(toolCall);
            if (!ShouldGuardRepeatedToolInvocation(toolName))
            {
                return;
            }

            var signature = ResolveToolInvocationSignature(toolCall);
            if (IsValidationToolInvocation(toolName))
            {
                signature = $"{signature}|mutationGeneration={mutationGeneration}";
            }

            var repeatedToolInvocationCount = repeatedToolInvocationCounts.TryGetValue(signature, out var currentCount)
                ? currentCount + 1
                : 1;
            repeatedToolInvocationCounts[signature] = repeatedToolInvocationCount;
            if (repeatedToolInvocationCount > MaxRepeatedToolInvocationCount)
            {
                throw new InvalidOperationException(
                    $"Agent repeated identical tool invocation '{signature}' {repeatedToolInvocationCount} times in one run. Stop repeating the same tool call and either call the required next validation tool, inspect and change the underlying cause, or return a governed blocked/failed outcome.");
            }

            if (IsMutationToolInvocation(toolName))
            {
                mutationGeneration++;
            }
        }
    }

    private static bool ShouldGuardRepeatedToolInvocation(string toolName)
    {
        return IsValidationToolInvocation(toolName) || IsMutationToolInvocation(toolName);
    }

    private static bool IsValidationToolInvocation(string toolName)
        => AgentToolInvocationPolicyMetadata.IsValidationTool(toolName);

    private static bool IsMutationToolInvocation(string toolName)
        => AgentToolInvocationPolicyMetadata.IsMutationTool(toolName);

    private static string ResolveToolInvocationSignature(ToolCallContent toolCall)
    {
        return $"{ResolveToolName(toolCall)}|{DescribeToolCallArguments(toolCall)}";
    }

    private static string DescribeToolInvocation(ToolCallContent toolCall)
    {
        var toolName = ResolveToolName(toolCall);
        var arguments = DescribeToolCallArguments(toolCall);
        return string.IsNullOrWhiteSpace(arguments)
            ? $"Invoking tool '{toolName}'."
            : $"Invoking tool '{toolName}' with {arguments}.";
    }

    private static string DescribeToolCallArguments(ToolCallContent toolCall)
    {
        return toolCall switch
        {
            FunctionCallContent functionCall => SummarizeArguments(functionCall.Arguments),
            McpServerToolCallContent mcpToolCall => SummarizeArguments(mcpToolCall.Arguments),
            _ => string.Empty
        };
    }

    private static string DescribeArguments(string? argumentsJson)
    {
        return string.IsNullOrWhiteSpace(argumentsJson)
            ? string.Empty
            : FormatArgumentSummary(DeserializeArguments(argumentsJson));
    }

    private static string FormatInlineArgumentSummary(string argumentSummary)
    {
        return string.IsNullOrWhiteSpace(argumentSummary)
            ? string.Empty
            : $" with {argumentSummary}";
    }

    private static string SummarizeArguments(IDictionary<string, object?>? arguments)
    {
        if (arguments is null || arguments.Count == 0)
        {
            return string.Empty;
        }

        return FormatArgumentSummary(arguments);
    }

    private static string FormatArgumentSummary(IEnumerable<KeyValuePair<string, object?>> arguments)
    {
        var parts = arguments
            .Where(item => item.Value is not null)
            .Select(item => $"{item.Key}={FormatArgumentValue(item.Value)}")
            .ToList();

        return parts.Count == 0
            ? string.Empty
            : string.Join(", ", parts);
    }

    private static string FormatArgumentValue(object? value)
    {
        if (value is null)
        {
            return "<null>";
        }

        var text = value switch
        {
            string stringValue => stringValue,
            JsonElement jsonValue => jsonValue.ToString(),
            _ => JsonSerializer.Serialize(value, SerializerOptions)
        };

        if (string.IsNullOrWhiteSpace(text))
        {
            return "\"\"";
        }

        text = text.ReplaceLineEndings(" ").Trim();
        if (text.Length > 120)
        {
            text = text[..120] + $"...#{ComputeStableHash(text)}";
        }

        return $"\"{text}\"";
    }

    private static string ComputeStableHash(string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes, 0, 6).ToLowerInvariant();
    }

    private static Dictionary<string, object?> DeserializeArguments(string? argumentsJson)
    {
        if (string.IsNullOrWhiteSpace(argumentsJson))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(argumentsJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return [];
            }

            return document.RootElement.EnumerateObject()
                .ToDictionary(property => property.Name, property => ConvertJsonValue(property.Value));
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static object? ConvertJsonValue(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            JsonValueKind.String => value.GetString(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number when value.TryGetInt64(out var longValue) => longValue,
            JsonValueKind.Number when value.TryGetDouble(out var doubleValue) => doubleValue,
            JsonValueKind.Array => value.EnumerateArray().Select(ConvertJsonValue).ToList(),
            JsonValueKind.Object => value.EnumerateObject().ToDictionary(property => property.Name, property => ConvertJsonValue(property.Value)),
            _ => value.ToString()
        };
    }
}
