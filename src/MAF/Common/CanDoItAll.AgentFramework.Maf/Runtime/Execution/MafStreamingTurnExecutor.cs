using System.Diagnostics;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Core.Execution;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace CanDoItAll.AgentFramework.Maf;

/// <summary>
/// The shared streaming/response tail used by both the execution and continuation adapters:
/// provider update pumping, actionable-content retry, required-finalizer short-circuit and
/// repair orchestration (including provider-streaming-timeout and missing-required-finalizer
/// recovery through the injected <see cref="IAgentExecutionOutcomeRecoveryPolicy"/> collaborators),
/// trace/usage capture, background-response polling, session persistence, and response assembly via
/// <see cref="MafRuntimeResponseMapper"/>.
/// </summary>
internal sealed class MafStreamingTurnExecutor
{
    private static readonly ProviderProfileService ProviderFeatureService = new();

    private readonly string workspaceRoot;
    private readonly WorkspaceScopeDescriptor workspaceScope;
    private readonly IMafProviderAgentFactory providerAgentFactory;
    private readonly IMafApprovalContinuationDriver approvalContinuationDriver;
    private readonly IMafRuntimeSessionPersistenceDriver sessionPersistenceDriver;
    private readonly IReadOnlyList<IAgentExecutionOutcomeRecoveryPolicy> executionOutcomeRecoveryPolicies;
    private readonly MafProviderUpdatePump providerUpdatePump = new();

    public MafStreamingTurnExecutor(
        string workspaceRoot,
        WorkspaceScopeDescriptor workspaceScope,
        IMafProviderAgentFactory providerAgentFactory,
        IMafApprovalContinuationDriver approvalContinuationDriver,
        IMafRuntimeSessionPersistenceDriver sessionPersistenceDriver,
        IReadOnlyList<IAgentExecutionOutcomeRecoveryPolicy>? executionOutcomeRecoveryPolicies = null)
    {
        if (string.IsNullOrWhiteSpace(workspaceRoot))
        {
            throw new ArgumentException("Workspace root must be provided.", nameof(workspaceRoot));
        }

        this.workspaceRoot = Path.GetFullPath(workspaceRoot);
        this.workspaceScope = workspaceScope ?? throw new ArgumentNullException(nameof(workspaceScope));
        this.providerAgentFactory = providerAgentFactory ?? throw new ArgumentNullException(nameof(providerAgentFactory));
        this.approvalContinuationDriver = approvalContinuationDriver ?? throw new ArgumentNullException(nameof(approvalContinuationDriver));
        this.sessionPersistenceDriver = sessionPersistenceDriver ?? throw new ArgumentNullException(nameof(sessionPersistenceDriver));
        this.executionOutcomeRecoveryPolicies = executionOutcomeRecoveryPolicies ?? [];
    }

    public async Task<AgentRuntimeResponse> ExecuteTurnAsync(
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
        AgentRuntimeContextAssemblyManifest contextManifest,
        Func<AgentResponseUpdate, bool>? isTerminalResponseUpdate,
        ProviderRequestCompatibilityEvidence? entryAgentRequestCompatibilityEvidence)
    {
        var updates = new List<AgentResponseUpdate>();
        AgentResponseUpdate? lastTerminalResponseUpdate = null;
        var announcedStreaming = false;
        var announcedToolCalls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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
            => MafRuntimeResponseMapper.AttachContextDiagnostics(
                response,
                contextManifest,
                snapshotContextContributionTraces(),
                entryAgentRequestCompatibilityEvidence);

        async Task<AgentRuntimeResponse?> RecordStreamingUpdateAsync(
            AgentResponseUpdate update,
            string usageSourcePhase)
        {
            var snapshot = MafAgentResponseSnapshotter.SnapshotUpdate(update);
            updates.Add(snapshot);
            if (isTerminalResponseUpdate?.Invoke(update) == true)
            {
                lastTerminalResponseUpdate = snapshot;
            }

            if (!announcedStreaming && !string.IsNullOrWhiteSpace(snapshot.Text))
            {
                announcedStreaming = true;
                await progressCallback(ExecutionState.Running, "Streaming", "The agent is producing streamed output.");
            }

            foreach (var toolCall in snapshot.Contents.OfType<ToolCallContent>())
            {
                var toolKey = MafToolInvocationArgumentFormatter.ResolveToolCallKey(toolCall);
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

        MafProviderUpdatePumpContext CreateProviderUpdatePumpContext(
            AgentSession sourceSession,
            string usageSourcePhase)
            => new(
                provider,
                resolvedModel,
                sourceSession,
                runtimeSessionKey,
                updates,
                usageSourcePhase,
                snapshotEffectiveToolInvocationTraces,
                entryAgentRequestCompatibilityEvidence);

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
                var repairResponse = await providerUpdatePump.PumpAsync(
                    MafProviderStreamingInvocation.RunStreamingAsync(
                        provider,
                        resolvedModel,
                        runtimeAgent,
                        runtimeSession,
                        repairMessages,
                        repairRunOptions,
                        cancellationToken),
                    CreateProviderUpdatePumpContext(
                        runtimeSession,
                        ProviderUsageSourcePhases.FinalizerRecovery),
                    repairUpdate => RecordStreamingUpdateAsync(
                        repairUpdate,
                        ProviderUsageSourcePhases.FinalizerRecovery),
                    async () =>
                    {
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
                            return requiredFinalizerResponse;
                        }

                        var jsonRepairResponse = await TryRunRequiredFinalizerJsonRepairAsync(
                            finalizerPolicy,
                            response,
                            repairContext,
                            toolInvocationTraceRecorder);
                        if (jsonRepairResponse is not null)
                        {
                            return jsonRepairResponse;
                        }

                        return await TryCreateFinalizerResponseFromRecoveryPoliciesAsync(
                            provider,
                            resolvedModel,
                            runtimeAgent,
                            runtimeSession,
                            runtimeSessionKey,
                            runtimeOptions,
                            finalizerPolicy,
                            updates,
                            ProviderUsageSourcePhases.FinalizerRecovery,
                            AgentExecutionOutcomeFailureCause.MissingRequiredFinalizer,
                            progressCallback,
                            cancellationToken,
                            snapshotEffectiveToolInvocationTraces).ConfigureAwait(false);
                    },
                    cancellationToken);
                return repairResponse is null
                    ? null
                    : AttachContextDiagnostics(repairResponse);
            }
            catch (RequiredFinalizerCapturedException exception)
            {
                AgentRuntimeResponse? finalizerResponse;
                try
                {
                    finalizerResponse = await TryCreateFinalizerResponseAfterEarlyFinalizerAsync(
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
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception recoveryException)
                {
                    throw new AgentRuntimeUsageException(
                        "Required finalizer repair was captured, but governed recovery failed.",
                        new AggregateException(exception, recoveryException),
                        MafRuntimeResponseAssembler.CreateProviderUsageObservations(
                            provider,
                            resolvedModel,
                            runtimeSession,
                            runtimeSessionKey,
                            updates,
                            ProviderUsageSourcePhases.FinalizerRecovery,
                            "Required finalizer recovery failed after capture."),
                        snapshotEffectiveToolInvocationTraces(),
                        entryAgentRequestCompatibilityEvidence,
                        AgentRuntimeFailureOrigin.Finalizer);
                }

                if (finalizerResponse is not null)
                {
                    return AttachContextDiagnostics(finalizerResponse);
                }

                throw new AgentRuntimeUsageException(
                    $"Required finalizer repair captured '{exception.ToolName}' but the governed result could not be validated.",
                    exception,
                    MafRuntimeResponseAssembler.CreateProviderUsageObservations(
                        provider,
                        resolvedModel,
                        runtimeSession,
                        runtimeSessionKey,
                        updates,
                        ProviderUsageSourcePhases.FinalizerRecovery,
                        $"Required finalizer repair captured '{exception.ToolName}' but validation failed."),
                    snapshotEffectiveToolInvocationTraces(),
                    entryAgentRequestCompatibilityEvidence,
                    AgentRuntimeFailureOrigin.Finalizer);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                var failureOrigin = MafRuntimeFailureOriginClassifier.ResolveOutsideProviderBoundary(exception);
                if (failureOrigin == AgentRuntimeFailureOrigin.Provider)
                {
                    AgentRuntimeResponse? finalizerResponse;
                    try
                    {
                        finalizerResponse = await TryCreateFinalizerResponseAfterProviderFailureAsync(
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
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception recoveryException)
                    {
                        throw new AgentRuntimeUsageException(
                            "Provider failure finalizer recovery failed.",
                            new AggregateException(exception, recoveryException),
                            MafRuntimeResponseAssembler.CreateProviderUsageObservations(
                                provider,
                                resolvedModel,
                                runtimeSession,
                                runtimeSessionKey,
                                updates,
                                ProviderUsageSourcePhases.FinalizerRecovery,
                                "Provider failure finalizer recovery failed."),
                            snapshotEffectiveToolInvocationTraces(),
                            entryAgentRequestCompatibilityEvidence,
                            AgentRuntimeFailureOrigin.Finalizer);
                    }

                    if (finalizerResponse is not null)
                    {
                        return AttachContextDiagnostics(finalizerResponse);
                    }
                }

                if (exception is AgentRuntimeUsageException)
                {
                    throw;
                }

                throw new AgentRuntimeUsageException(
                    "Agent runtime failed during the bounded required-finalizer repair turn. Usage was captured when available.",
                    exception,
                    MafRuntimeResponseAssembler.CreateProviderUsageObservations(
                        provider,
                        resolvedModel,
                        runtimeSession,
                        runtimeSessionKey,
                        updates,
                        ProviderUsageSourcePhases.FinalizerRecovery,
                        MafProviderUpdatePump.BuildFailureDiagnostic(exception, failureOrigin)),
                    snapshotEffectiveToolInvocationTraces(),
                    entryAgentRequestCompatibilityEvidence,
                    failureOrigin,
                    MafRuntimeFailureOriginClassifier.ResolveProviderFailureIdentity(
                        exception,
                        failureOrigin));
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
                var jsonRepairResponse = await providerUpdatePump.PumpAsync(
                    MafProviderStreamingInvocation.RunStreamingAsync(
                        provider,
                        resolvedModel,
                        jsonRepairAgent,
                        jsonRepairSession,
                        jsonRepairMessages,
                        jsonRepairRunOptions,
                        cancellationToken),
                    CreateProviderUpdatePumpContext(
                        jsonRepairSession,
                        ProviderUsageSourcePhases.FinalizerRecovery),
                    async jsonRepairUpdate =>
                    {
                        jsonRepairUpdates.Add(MafAgentResponseSnapshotter.SnapshotUpdate(jsonRepairUpdate));
                        return await RecordStreamingUpdateAsync(
                            jsonRepairUpdate,
                            ProviderUsageSourcePhases.FinalizerRecovery);
                    },
                    async () =>
                    {
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
                            ProviderUsageSourcePhases.FinalizerRecovery,
                            progressCallback,
                            cancellationToken,
                            snapshotEffectiveFinalizerInvocations,
                            snapshotEffectiveToolInvocationTraces);
                    },
                    cancellationToken);
                return jsonRepairResponse is null
                    ? null
                    : AttachContextDiagnostics(jsonRepairResponse);
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
                    var streamedFinalizerResponse = await providerUpdatePump.PumpAsync(
                        MafProviderStreamingInvocation.RunStreamingAsync(
                            provider,
                            resolvedModel,
                            runtimeAgent,
                            runtimeSession,
                            inputMessages,
                            runOptions,
                            cancellationToken),
                        CreateProviderUpdatePumpContext(
                            runtimeSession,
                            ProviderUsageSourcePhases.AgentRuntime),
                        update => RecordStreamingUpdateAsync(
                            update,
                            ProviderUsageSourcePhases.FinalizerShortCircuit),
                        () => TryCreateFinalizerResponseAfterRequiredFinalizerAsync(
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
                            snapshotEffectiveToolInvocationTraces),
                        cancellationToken);
                    if (streamedFinalizerResponse is not null)
                    {
                        return AttachContextDiagnostics(streamedFinalizerResponse);
                    }
                }
                catch (RequiredFinalizerCapturedException exception)
                {
                    providerActivity?.SetTag("agentframework.required_finalizer_tool_name", exception.ToolName);
                    AgentRuntimeResponse? finalizerResponse;
                    try
                    {
                        finalizerResponse = await TryCreateFinalizerResponseAfterEarlyFinalizerAsync(
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
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception recoveryException)
                    {
                        providerActivity?.SetStatus(
                            ActivityStatusCode.Error,
                            "Required finalizer recovery failed.");
                        throw new AgentRuntimeUsageException(
                            "Required finalizer was captured, but governed recovery failed.",
                            new AggregateException(exception, recoveryException),
                            MafRuntimeResponseAssembler.CreateProviderUsageObservations(
                                provider,
                                resolvedModel,
                                runtimeSession,
                                runtimeSessionKey,
                                updates,
                                ProviderUsageSourcePhases.FinalizerRecovery,
                                "Required finalizer recovery failed after capture."),
                            snapshotEffectiveToolInvocationTraces(),
                            entryAgentRequestCompatibilityEvidence,
                            AgentRuntimeFailureOrigin.Finalizer);
                    }

                    if (finalizerResponse is not null)
                    {
                        return AttachContextDiagnostics(finalizerResponse);
                    }

                    providerActivity?.SetStatus(
                        ActivityStatusCode.Error,
                        "Required finalizer capture could not be validated.");
                    throw new AgentRuntimeUsageException(
                        $"Required finalizer '{exception.ToolName}' was captured, but the governed result could not be validated.",
                        exception,
                        MafRuntimeResponseAssembler.CreateProviderUsageObservations(
                            provider,
                            resolvedModel,
                            runtimeSession,
                            runtimeSessionKey,
                            updates,
                            ProviderUsageSourcePhases.FinalizerRecovery,
                            $"Required finalizer '{exception.ToolName}' capture validation failed."),
                        snapshotEffectiveToolInvocationTraces(),
                        entryAgentRequestCompatibilityEvidence,
                        AgentRuntimeFailureOrigin.Finalizer);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    var failureOrigin = MafRuntimeFailureOriginClassifier.ResolveOutsideProviderBoundary(exception);
                    if (failureOrigin == AgentRuntimeFailureOrigin.Provider)
                    {
                        AgentFrameworkTelemetry.RecordProviderError(provider, resolvedModel);
                    }

                    providerActivity?.SetTag(
                        "agentframework.failure_origin",
                        failureOrigin.ToString());
                    providerActivity?.SetTag(
                        "agentframework.failure_type",
                        exception.GetType().FullName ?? exception.GetType().Name);
                    providerActivity?.SetStatus(
                        ActivityStatusCode.Error,
                        "Agent runtime failure.");
                    if (failureOrigin == AgentRuntimeFailureOrigin.Provider)
                    {
                        AgentRuntimeResponse? finalizerResponse;
                        try
                        {
                            finalizerResponse = await TryCreateFinalizerResponseAfterProviderFailureAsync(
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
                        }
                        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                        {
                            throw;
                        }
                        catch (Exception recoveryException)
                        {
                            throw new AgentRuntimeUsageException(
                                "Provider failure finalizer recovery failed.",
                                new AggregateException(exception, recoveryException),
                                MafRuntimeResponseAssembler.CreateProviderUsageObservations(
                                    provider,
                                    resolvedModel,
                                    runtimeSession,
                                    runtimeSessionKey,
                                    updates,
                                    ProviderUsageSourcePhases.FinalizerRecovery,
                                    "Provider failure finalizer recovery failed."),
                                snapshotEffectiveToolInvocationTraces(),
                                entryAgentRequestCompatibilityEvidence,
                                AgentRuntimeFailureOrigin.Finalizer);
                        }

                        if (finalizerResponse is not null)
                        {
                            return AttachContextDiagnostics(finalizerResponse);
                        }
                    }

                    if (exception is AgentRuntimeUsageException)
                    {
                        throw;
                    }

                    throw new AgentRuntimeUsageException(
                        "Agent runtime failed after provider activity. Usage was captured when available.",
                        exception,
                        MafRuntimeResponseAssembler.CreateProviderUsageObservations(
                            provider,
                            resolvedModel,
                            runtimeSession,
                            runtimeSessionKey,
                            updates,
                            ProviderUsageSourcePhases.AgentRuntime,
                            MafProviderUpdatePump.BuildFailureDiagnostic(exception, failureOrigin)),
                        snapshotEffectiveToolInvocationTraces(),
                        entryAgentRequestCompatibilityEvidence,
                        failureOrigin,
                        MafRuntimeFailureOriginClassifier.ResolveProviderFailureIdentity(
                            exception,
                            failureOrigin));
                }
            }

            var activityResponse = updates.ToAgentResponse();
            var approvalRequests = activityResponse.Messages
                .SelectMany(message => message.Contents)
                .OfType<ToolApprovalRequestContent>()
                .ToList();
            var response = approvalRequests.Count > 0
                ? activityResponse
                : MafRuntimeResponseAssembler.ProjectTerminalResponse(
                    activityResponse,
                    lastTerminalResponseUpdate);

            if (approvalRequests.Count > 0)
            {
                approvalContinuationDriver.StorePendingApprovals(session.Id, approvalRequests);
            }
            else
            {
                approvalContinuationDriver.ClearPendingApprovals(session.Id);
            }

            if (!MafRuntimeResponseAssembler.ShouldContinueBackgroundRun(agent, provider, response, approvalRequests))
            {
                var repairedFinalizerResponse = await TryRunMissingRequiredFinalizerRepairAsync(response, approvalRequests);
                if (repairedFinalizerResponse is not null)
                {
                    return AttachContextDiagnostics(repairedFinalizerResponse);
                }

                var pendingApprovals = approvalRequests.Select(approvalContinuationDriver.MapPendingApproval).ToList();
                var serializedSessionJson = await sessionPersistenceDriver.TrySerializePersistableRuntimeSessionAsync(
                    runtimeAgent,
                    runtimeSession,
                    provider,
                    resolvedModel,
                    runtimeOptions,
                    pendingApprovals,
                    progressCallback,
                    cancellationToken);

                if (pendingApprovals.Count > 0)
                {
                    await progressCallback(ExecutionState.WaitingOnTool, "Approval", "The run is waiting for a tool approval response before it can continue.");
                }

                MafRuntimeResponseAssembler.ThrowIfEmptyProviderCompletion(provider, resolvedModel, response, pendingApprovals);

                return AttachContextDiagnostics(MafRuntimeResponseMapper.CreateTerminalRuntimeResponse(
                    approvalContinuationDriver.ResolveResponseText(response, pendingApprovals),
                    response,
                    activityResponse,
                    MafRuntimeResponseAssembler.ResolveRuntimeSessionKey(runtimeSession, response, runtimeSessionKey),
                    serializedSessionJson,
                    pendingApprovals,
                    snapshotEffectiveFinalizerInvocations(),
                    snapshotEffectiveToolInvocationTraces(),
                    MafRuntimeResponseAssembler.CreateProviderUsageObservations(
                        provider,
                        resolvedModel,
                        runtimeSession,
                        runtimeSessionKey,
                        updates,
                        ProviderUsageSourcePhases.AgentRuntime,
                        "Microsoft Agent Framework returned a runtime response.")));
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

        var repairOptions = MafChatClientAgentOptionsFactory.Create(chatOptions);
        repairOptions.Id = agent.Id.ToString("D");
        repairOptions.Name = agent.Name;
        repairOptions.Description = agent.Summary;
        repairOptions.AIContextProviders = [];
        repairOptions.ChatHistoryProvider = null;
        repairOptions.RequirePerServiceCallChatHistoryPersistence = false;
        return providerAgentFactory.CreateFrameworkAgent(
            provider,
            model,
            repairOptions,
            frameworkManagedHistory: false,
            allowBackgroundResponses: false);
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
            runtimeToolOwnership: null,
            ToolInvocationPathArgumentSet.Empty);
        synthesizedInvocations.Add(new AgentFinalizerInvocation(
            policy.ToolName,
            argumentsJson,
            sequence));
        toolInvocationTraceRecorder.Complete(
            sequence,
            succeeded: true,
            failureMessage: "Captured from a typed JSON required-finalizer repair response.",
            failureMessageSafeForPersistence: true);
        return true;
    }

    private async Task<AgentRuntimeResponse?> TryCreateFinalizerResponseAfterEarlyFinalizerAsync(
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
        if (finalizerMode != AgentFinalizerMode.Required)
        {
            return null;
        }

        var finalizerInvocations = snapshotFinalizerInvocations();
        var toolInvocationTraces = snapshotToolInvocationTraces();
        var serializedResponse = MafRuntimeResponseAssembler.TryBuildRequiredFinalizerRuntimeResponse(
            structuredOutput,
            finalizerMode,
            MafRuntimeResponseAssembler.ResolveRuntimeSessionKey(runtimeSession, runtimeSessionKey),
            serializedSessionStateJson: null,
            finalizerInvocations,
            toolInvocationTraces,
            MafRuntimeResponseAssembler.CreateProviderUsageObservations(
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

        var serializedSessionStateJson = await sessionPersistenceDriver.TrySerializePersistableRuntimeSessionAsync(
            runtimeAgent,
            runtimeSession,
            provider,
            model,
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
        if (!finalizerValidation.Succeeded || finalizerValidation.Output is null)
        {
            if (MafFinalizerDriver.ShouldAttemptProviderFailureArtifactRecovery(
                    policy,
                    finalizerInvocations,
                    exception))
            {
                return await TryCreateFinalizerResponseFromRecoveryPoliciesAsync(
                    provider,
                    model,
                    runtimeAgent,
                    runtimeSession,
                    runtimeSessionKey,
                    runtimeOptions,
                    policy,
                    updates,
                    ProviderUsageSourcePhases.FinalizerRecovery,
                    AgentExecutionOutcomeFailureCause.ProviderStreamingTimeout,
                    progressCallback,
                    cancellationToken,
                    snapshotToolInvocationTraces).ConfigureAwait(false);
            }

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
            $"Provider streaming failed after required finalizer '{policy.ToolName}' was captured. Persisting the governed finalizer outcome and preserving the redacted provider error for diagnostics: {WorkflowExecutorRedaction.RedactText(exception.Message)}");

        var serializedSessionStateJson = await sessionPersistenceDriver.TrySerializePersistableRuntimeSessionAsync(
            runtimeAgent,
            runtimeSession,
            provider,
            model,
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
            RuntimeSessionKey: MafRuntimeResponseAssembler.ResolveRuntimeSessionKey(runtimeSession, runtimeSessionKey),
            SerializedSessionStateJson: serializedSessionStateJson,
            PendingApprovals: [])
        {
            FinalizerInvocations = finalizerInvocations,
            ToolInvocationTraces = toolInvocationTraces,
            UsageObservations = MafRuntimeResponseAssembler.CreateProviderUsageObservations(
                provider,
                model,
                runtimeSession,
                runtimeSessionKey,
                updates,
                ProviderUsageSourcePhases.FinalizerRecovery,
                "Provider streaming failed after a valid required finalizer was captured.")
        };
    }

    /// <summary>
    /// Offers typed, runtime-neutral evidence to every registered <see cref="IAgentExecutionOutcomeRecoveryPolicy"/>
    /// (DI registration order) after MAF-native bounded repair is exhausted. MAF never reads artifact content or
    /// contract identity itself; it only builds the evidence envelope and applies the first
    /// <see cref="AgentExecutionOutcomeRecoveryStatus.Recovered"/> decision through the same required-finalizer
    /// response assembly used for ordinary completion. An empty policy list (no product module loaded) is the
    /// fail-closed default: this always returns <see langword="null"/>, falling through to the normal failure path.
    /// Internal (not private) so tests can exercise the coordinator path directly without re-driving the entire
    /// bounded streaming/repair sequence.
    /// </summary>
    internal async Task<AgentRuntimeResponse?> TryCreateFinalizerResponseFromRecoveryPoliciesAsync(
        ProviderProfile provider,
        string model,
        AIAgent runtimeAgent,
        AgentSession runtimeSession,
        string? runtimeSessionKey,
        AgentRuntimeExecutionOptions runtimeOptions,
        AgentFinalizerPolicy policy,
        IReadOnlyList<AgentResponseUpdate> updates,
        string usageSourcePhase,
        AgentExecutionOutcomeFailureCause recoveryCause,
        Func<ExecutionState, string, string, Task> progressCallback,
        CancellationToken cancellationToken,
        Func<IReadOnlyList<AgentToolInvocationTrace>> snapshotToolInvocationTraces)
    {
        if (executionOutcomeRecoveryPolicies.Count == 0)
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var contextIntent = runtimeOptions.ContextIntent ?? AgentRuntimeContextIntent.Empty;
        var existingToolTraces = snapshotToolInvocationTraces();
        var evidence = new AgentExecutionOutcomeRecoveryEvidence(
            contextIntent,
            recoveryCause,
            policy.ToolName,
            policy.OutputContract.ContractKey,
            policy.OutputType,
            existingToolTraces,
            new WorkspaceRecoveryArtifactReader(workspaceRoot, workspaceScope));

        AgentExecutionOutcomeRecoveryDecision? recoveredDecision = null;
        foreach (var recoveryPolicy in executionOutcomeRecoveryPolicies)
        {
            var decision = recoveryPolicy.Evaluate(evidence);
            if (decision.Status == AgentExecutionOutcomeRecoveryStatus.Recovered)
            {
                recoveredDecision = decision;
                break;
            }

            // NotApplicable/Rejected diagnostics are intentionally not surfaced further here: a missing
            // recovery falls through to the normal (non-recovered) failure path, matching the prior
            // silent-null behavior. They remain available to callers that inspect decisions directly
            // (for example policy-level unit tests).
        }

        if (recoveredDecision is not { } recovered)
        {
            return null;
        }

        var finalizerSequence = existingToolTraces.Count == 0
            ? 1
            : existingToolTraces.Max(trace => trace.Sequence) + 1;
        var timestamp = DateTimeOffset.UtcNow;
        var finalizerInvocation = new AgentFinalizerInvocation(
            policy.ToolName,
            recovered.MachineOutputJson,
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

        var recoveredResponse = MafRuntimeResponseAssembler.TryBuildRequiredFinalizerRuntimeResponse(
            policy.OutputContract,
            AgentFinalizerMode.Required,
            MafRuntimeResponseAssembler.ResolveRuntimeSessionKey(runtimeSession, runtimeSessionKey),
            serializedSessionStateJson: null,
            [finalizerInvocation],
            toolInvocationTraces,
            MafRuntimeResponseAssembler.CreateProviderUsageObservations(
                provider,
                model,
                runtimeSession,
                runtimeSessionKey,
                updates,
                usageSourcePhase,
                $"{recovered.RecoveryReason} The required finalizer was synthesized from evidence reference '{recovered.EvidenceReference}'."));
        if (recoveredResponse is null)
        {
            return null;
        }

        await progressCallback(
            ExecutionState.Persisting,
            "Finalizer recovery",
            $"{recovered.RecoveryReason} Persisting a validated required-finalizer result synthesized from evidence reference '{recovered.EvidenceReference}' with status '{recovered.OutcomeStatusLabel}'.").ConfigureAwait(false);

        var serializedSessionStateJson = await sessionPersistenceDriver.TrySerializePersistableRuntimeSessionAsync(
            runtimeAgent,
            runtimeSession,
            provider,
            model,
            runtimeOptions,
            [],
            progressCallback,
            cancellationToken).ConfigureAwait(false);
        return recoveredResponse with
        {
            SerializedSessionStateJson = serializedSessionStateJson
        };
    }

    internal static bool TryReadCompleteRecoveryArtifact(
        string workspaceRoot,
        WorkspaceScopeDescriptor workspaceScope,
        string artifactRef,
        out string artifactMarkdown)
    {
        var readResult = new WorkspaceFileService(workspaceRoot, workspaceScope)
            .ReadTextFile(artifactRef, WorkspaceFileLimits.MaxTextReadCharacters);
        if (!readResult.Succeeded ||
            readResult.IsTruncated ||
            readResult.TotalCharacters > WorkspaceFileLimits.MaxTextReadCharacters ||
            readResult.TotalCharacters != readResult.Content.Length)
        {
            artifactMarkdown = string.Empty;
            return false;
        }

        artifactMarkdown = readResult.Content;
        return true;
    }

    /// <summary>
    /// Bounded, complete-read-only adapter over <see cref="TryReadCompleteRecoveryArtifact"/> for recovery
    /// policies. Closes over the current turn's workspace root/scope so policies never receive workspace SDK
    /// objects.
    /// </summary>
    private sealed class WorkspaceRecoveryArtifactReader(string workspaceRoot, WorkspaceScopeDescriptor workspaceScope)
        : IAgentExecutionRecoveryArtifactReader
    {
        public bool TryReadCompleteTextFile(string relativeManagedPath, out string content)
            => TryReadCompleteRecoveryArtifact(workspaceRoot, workspaceScope, relativeManagedPath, out content);
    }

    private async Task<AgentRuntimeResponse?> TryCreateFinalizerResponseAfterRequiredFinalizerAsync(
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
        var serializedResponse = MafRuntimeResponseAssembler.TryBuildRequiredFinalizerRuntimeResponse(
            structuredOutput,
            finalizerMode,
            MafRuntimeResponseAssembler.ResolveRuntimeSessionKey(runtimeSession, runtimeSessionKey),
            serializedSessionStateJson: null,
            finalizerInvocations,
            toolInvocationTraces,
            MafRuntimeResponseAssembler.CreateProviderUsageObservations(
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

        var serializedSessionStateJson = await sessionPersistenceDriver.TrySerializePersistableRuntimeSessionAsync(
            runtimeAgent,
            runtimeSession,
            provider,
            model,
            runtimeOptions,
            [],
            progressCallback,
            cancellationToken);
        return serializedResponse with
        {
            SerializedSessionStateJson = serializedSessionStateJson
        };
    }
}
