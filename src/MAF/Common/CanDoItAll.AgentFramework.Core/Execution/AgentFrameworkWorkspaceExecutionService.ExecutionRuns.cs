using System.Diagnostics;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core.Execution;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Runtime.Abstractions;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.AgentFramework.Core;

internal sealed partial class AgentFrameworkWorkspaceExecutionService
{
    private const string UnclassifiedRunFailureMessage =
        "The agent run failed outside a confirmed provider failure. Inspect the persisted run and its tool traces using the execution-run ID.";

    public Task<ExecutionRunResult> ExecuteRunWithinOperationAsync(
        IAgentExecutionActivityOperationLease operation,
        ExecutionRunRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateActivityOperation(
            operation,
            request.AgentId,
            request.ChatSessionId,
            request.InitialActivityOperationId);
        return ExecuteWithinActivityBoundaryAsync(
            operation,
            () => ExecuteRunEntryAsync(
                operation,
                request,
                cancellationToken));
    }

    private async Task<ExecutionRunResult> ExecuteRunEntryAsync(
        IAgentExecutionActivityOperationLease activityOperation,
        ExecutionRunRequest request,
        CancellationToken cancellationToken,
        bool persistTranscript = false)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureRequiredActivityOperationId(
            request.InitialActivityOperationId,
            nameof(request));

        if (request.StructuredOutput is not null && request.JsonSchemaOutput is not null)
        {
            throw new AgentJsonSchemaOutputContractException(
                "agents.structured-output-ambiguous",
                "Choose either a trusted in-process structured output contract or a portable JSON Schema contract, not both.");
        }

        _ = AgentJsonSchemaOutputContractProcessor.Prepare(request.JsonSchemaOutput);
        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            throw new InvalidOperationException("Prompt is required.");
        }

        persistTranscript |= request.ChatSessionId.HasValue;
        activityOperation.Report(
            AgentExecutionActivityPhase.PreparingInput,
            "Preparing input attachments.");
        var inputAttachments = await ResolveRuntimeInputAttachmentsAsync(
            request.InputAttachmentPaths,
            cancellationToken);
        var preparation = await AcquireExecutionPreparationAsync(
            activityOperation,
            request.AgentId,
            atomicConsumer:
                persistTranscript &&
                store is ISandboxWorkspaceChatRunStartStore,
            cancellationToken).ConfigureAwait(false);
        var provider = await ResolvePreparedProviderAsync(
            activityOperation,
            preparation,
            request,
            cancellationToken).ConfigureAwait(false);

        return await ExecuteRunCoreAsync(
            preparation,
            provider,
            request,
            persistTranscript,
            inputAttachments,
            cancellationToken,
            activityOperation);
    }

    public Task<ExecutionRunSourceExecutionResult> ExecuteSameSourceRunWithinOperationAsync(
        IAgentExecutionActivityOperationLease operation,
        ExecutionRunSourceKey source,
        ExecutionRunRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateActivityOperation(
            operation,
            request.AgentId,
            request.ChatSessionId,
            request.InitialActivityOperationId);
        return ExecuteWithinActivityBoundaryAsync(
            operation,
            () => ExecuteSameSourceRunEntryAsync(
                operation,
                source,
                request,
                cancellationToken));
    }

    private async Task<ExecutionRunSourceExecutionResult> ExecuteSameSourceRunEntryAsync(
        IAgentExecutionActivityOperationLease activityOperation,
        ExecutionRunSourceKey source,
        ExecutionRunRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(request);
        EnsureRequiredActivityOperationId(
            request.InitialActivityOperationId,
            nameof(request));

        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            throw new InvalidOperationException("Prompt is required.");
        }

        if (request.ChatSessionId.HasValue)
        {
            throw new InvalidOperationException(
                "Atomic same-source execution is supported only for non-chat execution runs.");
        }

        if (store is not ISandboxWorkspaceExecutionRunReservationStore reservationStore)
        {
            throw new NotSupportedException(
                "The workspace store does not support atomic same-source execution reservation.");
        }

        var context = PrepareInvocationContext(request) with
        {
            SourceKind = source.SourceKind,
            SourceId = source.SourceId
        };
        request = request with { Context = context };

        activityOperation.Report(
            AgentExecutionActivityPhase.PreparingInput,
            "Preparing input attachments.");
        var inputAttachments = await ResolveRuntimeInputAttachmentsAsync(
            request.InputAttachmentPaths,
            cancellationToken);
        var preparation = await AcquireExecutionPreparationAsync(
            activityOperation,
            request.AgentId,
            atomicConsumer: false,
            cancellationToken).ConfigureAwait(false);
        var provider = await ResolvePreparedProviderAsync(
            activityOperation,
            preparation,
            request,
            cancellationToken).ConfigureAwait(false);
        activityOperation.Report(
            AgentExecutionActivityPhase.CreatingExecution,
            "Preparing source execution admission.");
        var agent = CreateProviderCompatibleRuntimeAgent(
            preparation.Blueprint.Agent,
            provider,
            ResolveEffectiveManagedSeedModel(
                preparation.Blueprint.Agent,
                provider));
        var prompt = request.Prompt.Trim();
        var run = CreatePreparingRun(
            agent,
            provider,
            chatSessionId: null,
            CreateSessionTitle(prompt),
            context,
            prompt,
            DateTimeOffset.UtcNow,
            request.AutoApprovePendingToolCalls,
            request.InitialActivityOperationId,
            request.StructuredOutput);
        var reservation = await reservationStore
            .ReserveExecutionRunAsync(
                source,
                CreateExecutionRunDetail(
                    run,
                    session: null,
                    executionLog: [],
                    metrics: [],
                    usageObservations: [],
                    approvals: [],
                    artifacts: [],
                    checkpoints: [],
                    toolReceipts: []),
                cancellationToken)
            .ConfigureAwait(false);

        if (reservation.Disposition != ExecutionRunSourceDisposition.Created)
        {
            EnsureActivityRunBinding(activityOperation, reservation.Run);
            TerminalizeSameSourceReservationActivity(
                activityOperation,
                reservation);
            return new ExecutionRunSourceExecutionResult(
                reservation.Disposition,
                reservation.Run,
                CreatedExecutionResult: null);
        }

        EnsureActivityRunBinding(activityOperation, reservation.Run);
        var startup = new AgentExecutionStartupAggregate(
            preparation.Blueprint,
            preparation.CatalogSnapshot,
            agent,
            provider,
            Session: null,
            reservation.Run,
            UserMessage: null,
            inputAttachments);
        var result = await CompletePreparedExecutionRunAsync(
                startup,
                request,
                cancellationToken,
                activityOperation)
            .ConfigureAwait(false);
        return new ExecutionRunSourceExecutionResult(
            ExecutionRunSourceDisposition.Created,
            reservation.Run,
            result);
    }

    public Task<ExecutionRunResult> ContinueExecutionRunWithinOperationAsync(
        IAgentExecutionActivityOperationLease operation,
        Guid executionRunId,
        IReadOnlyList<PendingToolApprovalDecision> decisions,
        bool autoApprovePendingToolCalls = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(decisions);
        ValidateActivityOperationScope(operation);
        EnsureRequiredActivityOperationId(
            operation.StreamId.OperationId,
            nameof(operation));
        return ExecuteWithinActivityBoundaryAsync(
            operation,
            () => ContinueExecutionRunEntryAsync(
                operation,
                executionRunId,
                operation.StreamId.OperationId,
                decisions,
                autoApprovePendingToolCalls,
                cancellationToken));
    }

    private static string DescribeApprovalDecisionPhase(IReadOnlyList<PendingToolApprovalDecision> decisions)
    {
        var approvedCount = decisions.Count(decision => decision.Approved);
        if (approvedCount == decisions.Count)
        {
            return "Approval granted";
        }

        return approvedCount == 0
            ? "Approval rejected"
            : "Approval decided";
    }

    private static string DescribeApprovalDecisionMessage(IReadOnlyList<PendingToolApprovalDecision> decisions)
    {
        var approvedCount = decisions.Count(decision => decision.Approved);
        if (approvedCount == decisions.Count)
        {
            return "Continuing the execution run after approving the pending tool request(s).";
        }

        if (approvedCount == 0)
        {
            return "Continuing the execution run after rejecting the pending tool request(s).";
        }

        return $"Continuing the execution run after deciding {decisions.Count} pending tool request(s): {approvedCount} approved, {decisions.Count - approvedCount} rejected.";
    }

    private async Task<ExecutionRunResult> ContinueExecutionRunEntryAsync(
        IAgentExecutionActivityOperationLease activityOperation,
        Guid executionRunId,
        AgentExecutionOperationId activityOperationId,
        IReadOnlyList<PendingToolApprovalDecision> decisions,
        bool autoApprovePendingToolCalls,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(decisions);
        EnsureRequiredActivityOperationId(
            activityOperationId,
            nameof(activityOperationId));
        if (activityOperation.StreamId.OperationId != activityOperationId)
        {
            throw new InvalidOperationException(
                "The activity lease operation id does not match the continuation operation id.");
        }

        activityOperation.Report(
            AgentExecutionActivityPhase.ResolvingSession,
            "Loading the execution run awaiting approval.");
        var currentRun = await LoadExecutionRunAsync(executionRunId, cancellationToken);
        EnsureActivityRunBinding(activityOperation, currentRun);
        if (currentRun.PendingApprovals.Count == 0)
        {
            if (currentRun.State is ExecutionState.Completed or ExecutionState.Failed)
            {
                var existingResult = await LoadExistingExecutionRunResultAsync(
                    executionRunId,
                    cancellationToken);
                await CleanupTerminalExecutionRunProcessesAsync(
                    currentRun,
                    terminalRunPersisted: true);
                transientContextRegistry.Remove(currentRun.Id);
                ReportAndTerminalizeExistingResult(activityOperation, existingResult);
                return existingResult;
            }

            throw new InvalidOperationException("This execution run is already being continued.");
        }

        AgentApprovalDecisionMismatchException.ValidateExactCoverage(decisions, currentRun.PendingApprovals);
        var allApproved = decisions.All(decision => decision.Approved);

        var restoredCheckpoint = await executionCheckpointBridge
            .ValidatePendingApprovalResumeAsync(
                currentRun,
                cancellationToken);
        var preparation = await AcquireExecutionPreparationAsync(
            activityOperation,
            currentRun.AgentId,
            atomicConsumer: true,
            cancellationToken).ConfigureAwait(false);
        ExecutionRunContinuationStart continuationStart;
        const int maximumContinuationStartAttempts = 3;
        for (var attempt = 1; ; attempt++)
        {
            var continuationProvider =
                await ResolveContinuationProviderLeaseAsync(
                    currentRun,
                    preparation,
                    cancellationToken)
                .ConfigureAwait(false);

            try
            {
                continuationStart =
                    await BeginPendingApprovalContinuationAsync(
                        preparation,
                        currentRun,
                        continuationProvider,
                        decisions,
                        autoApprovePendingToolCalls,
                        cancellationToken);
                break;
            }
            catch (AgentExecutionPreparationStaleException)
                when (attempt < maximumContinuationStartAttempts)
            {
                activityOperation.Report(
                    AgentExecutionActivityPhase.ResolvingPreparation,
                    "The agent configuration changed before approval continuation; refreshing the prepared data.");
                preparation = await AcquireExecutionPreparationAsync(
                    activityOperation,
                    currentRun.AgentId,
                    atomicConsumer: true,
                    cancellationToken).ConfigureAwait(false);
            }
        }

        if (continuationStart.Disposition == ExecutionRunContinuationDisposition.AlreadyFinalized)
        {
            var existingResult = await LoadExistingExecutionRunResultAsync(
                executionRunId,
                cancellationToken);
            var finalizedRun = await LoadExecutionRunAsync(
                executionRunId,
                cancellationToken);
            await CleanupTerminalExecutionRunProcessesAsync(
                finalizedRun,
                terminalRunPersisted: true);
            transientContextRegistry.Remove(finalizedRun.Id);
            ReportAndTerminalizeExistingResult(activityOperation, existingResult);
            return existingResult;
        }

        if (continuationStart.Disposition == ExecutionRunContinuationDisposition.AlreadyInProgress)
        {
            throw new InvalidOperationException("This execution run is already being continued.");
        }

        var prepared = continuationStart.Prepared
            ?? throw new InvalidOperationException("Execution run continuation start did not return a prepared state.");
        var run = prepared.TransitionedRun;
        var session = prepared.Session;
        var agent = prepared.Agent;
        var provider = prepared.Provider;
        var attachedCapabilities = prepared.Blueprint.Capabilities;
        IReadOnlyList<AgentMemoryRecord> memory =
            IsGovernedMachineCriticalRun(run)
                ? []
                : prepared.Blueprint.Memory;
        var structuredOutput =
            ResolveContinuationStructuredOutputContract(currentRun);
        var startedAt = DateTimeOffset.UtcNow;
        AgentRuntimeHandoffExecutionOptions? handoffOptions = null;
        AgentDefinition? runtimeAgent = null;
        AgentRuntimeResponse? lastRuntimeResponse = null;
        var lastEntryAgentRequestCompatibilityEvidence = run.EntryAgentRequestCompatibilityEvidence;

        AgentRuntimeResponse TrackRuntimeResponse(AgentRuntimeResponse response)
        {
            lastEntryAgentRequestCompatibilityEvidence =
                response.EntryAgentRequestCompatibilityEvidence ??
                lastEntryAgentRequestCompatibilityEvidence;
            var trackedResponse = response.EntryAgentRequestCompatibilityEvidence is null &&
                                  lastEntryAgentRequestCompatibilityEvidence is not null
                ? response with
                {
                    EntryAgentRequestCompatibilityEvidence =
                        lastEntryAgentRequestCompatibilityEvidence
                }
                : response;
            lastRuntimeResponse = trackedResponse;
            return trackedResponse;
        }

        IAgentExecutionCancellationRegistration? executionCancellation = null;
        AgentProviderCredentialDispatchLease? credentialDispatch = null;
        IAgentProviderCredentialDispatchScope? credentialScope = null;
        var terminalRunPersisted = false;
        try
        {
            activityOperation.Report(
                AgentExecutionActivityPhase.ResolvingProvider,
                "Using the canonical provider lease for approval continuation.");
            activityOperation.Report(
                AgentExecutionActivityPhase.PreparingCapabilities,
                "Using prepared tools and memory for approval continuation.");
            activityOperation.Report(
                AgentExecutionActivityPhase.PreparingRuntime,
                "Preparing the agent runtime for approval continuation.");
            handoffOptions = await ResolveHandoffExecutionOptionsAsync(
                    agent,
                    prepared.CatalogSnapshot.Catalog,
                    currentRun,
                    cancellationToken)
                .ConfigureAwait(false);
            credentialDispatch =
                await providerCredentialDispatchScopeFactory.PrepareAsync(
                    ResolveProviderCredentialDispatchProviders(
                        provider,
                        handoffOptions),
                    cancellationToken);
            credentialScope = credentialDispatch.BeginScope();
            runtimeAgent = CreateProviderCompatibleRuntimeAgent(
                agent,
                provider,
                currentRun.Model);
            using var runActivity = AgentFrameworkTelemetry.StartRunActivity(
                "agent.run.resume",
                prepared.OriginalRun);
            AgentFrameworkTelemetry.RecordRunResume(prepared.OriginalRun);

            if (restoredCheckpoint is not null)
            {
                await AppendExecutionLogAsync(
                    run.Id,
                    agent.Id,
                    run.ChatSessionId,
                    ExecutionState.WaitingOnTool,
                    "Workflow",
                    $"Restored workflow checkpoint '{restoredCheckpoint.WorkflowCheckpointId}' before replaying the approval decision.",
                    cancellationToken);
            }

            if (allApproved &&
                autoApprovePendingToolCalls &&
                !prepared.OriginalRun.AutoApprovePendingToolCalls)
            {
                await AppendExecutionLogAsync(
                    run.Id,
                    agent.Id,
                    run.ChatSessionId,
                    ExecutionState.WaitingOnTool,
                    "Approval policy",
                    "Run-level auto-approve enabled for future approval continuations.",
                    cancellationToken);
            }

            if (prepared.DecidedApprovals.Count > 0)
            {
                await executionGovernanceBridge.OnApprovalsDecidedAsync(
                    run,
                    prepared.DecidedApprovals,
                    cancellationToken);
            }

            await AppendExecutionLogAsync(
                run.Id,
                agent.Id,
                run.ChatSessionId,
                ExecutionState.WaitingOnTool,
                DescribeApprovalDecisionPhase(decisions),
                DescribeApprovalDecisionMessage(decisions),
                cancellationToken);

            executionCancellation = executionCancellationRegistry.Register(run, cancellationToken);
            var runtimeCancellationToken = executionCancellation.Token;
            var runtimeSession = ChatSessionRuntimeCompatibilityAdapter.CreateRuntimeSession(
                prepared.OriginalRun,
                agent.Id,
                session);
            var runtimeExecutionOptions = CreateRuntimeExecutionOptionsCore(
                run,
                activityOperationId,
                structuredOutput,
                handoffOptions,
                inputAttachments: null,
                jsonSchemaOutput: null);
            AgentRuntimeResponse runtimeResponse;
            using (WorkspaceExecutionAuditContext.BeginScope(
                       run,
                       runtimeExecutionOptions.ContextWorkspaceScope,
                       activityWorkspaceIdentity.WorkspaceScope))
            {
                activityOperation.Report(
                    AgentExecutionActivityPhase.WaitingForProvider,
                    "Waiting for the configured provider to continue the run.");
                runtimeResponse = TrackRuntimeResponse(await continuationRuntime.ContinueAsync(
                    new AgentRuntimeContinuationRequest(
                        runtimeAgent,
                        provider,
                        runtimeSession,
                        attachedCapabilities,
                        memory,
                        Decisions: ToRuntimeApprovalDecisions(decisions),
                        string.IsNullOrWhiteSpace(run.RuntimeSessionKey) ? null : run.RuntimeSessionKey,
                        (state, phase, message) => ReportRuntimeProgressAsync(
                            activityOperation,
                            run,
                            agent.Id,
                            state,
                            phase,
                            message,
                            cancellationToken),
                        SuppressApprovalRequirements: false,
                        StructuredOutput: structuredOutput,
                        ExecutionOptions: runtimeExecutionOptions)
                    {
                        ResolvedApprovalRequestIds = ResolveDecidedApprovalRequestIds(prepared.RunApprovals)
                    },
                    runtimeCancellationToken));

                var totalInputTokens = runtimeResponse.InputTokens;
                var totalCachedInputTokens = runtimeResponse.CachedInputTokens;
                var totalOutputTokens = runtimeResponse.OutputTokens;
                var totalToolCalls = runtimeResponse.ToolCalls;

                if (runtimeResponse.PendingApprovals.Count > 0 && allApproved && ShouldAutoApprovePendingToolCalls(agent, runtimeSession))
                {
                    var continuation = await ContinueAutoApprovedRunAsync(
                        run,
                        activityOperationId,
                        runtimeAgent,
                        provider,
                        runtimeSession,
                        run.ChatSessionId,
                        attachedCapabilities,
                        memory,
                        runtimeResponse,
                        (state, phase, message) => ReportRuntimeProgressAsync(
                            activityOperation,
                            run,
                            agent.Id,
                            state,
                            phase,
                            message,
                            cancellationToken),
                        structuredOutput,
                        handoffOptions,
                        ResolveDecidedApprovalRequestIds(prepared.RunApprovals),
                        response =>
                        {
                            TrackRuntimeResponse(response);
                        },
                        runtimeCancellationToken);

                    runtimeSession = continuation.Session;
                    runtimeResponse = TrackRuntimeResponse(continuation.Response);
                    totalInputTokens = continuation.TotalInputTokens;
                    totalCachedInputTokens = continuation.TotalCachedInputTokens;
                    totalOutputTokens = continuation.TotalOutputTokens;
                    totalToolCalls = continuation.TotalToolCalls;
                }

                runtimeResponse = TrackRuntimeResponse(await ValidateMachineOutputBeforeCompletionAsync(
                    run,
                    structuredOutput,
                    runtimeResponse,
                    runtimeCancellationToken));
                var portableStructuredOutput = await ValidatePortableJsonSchemaOutputBeforeCompletionAsync(
                    run,
                    runtimeResponse,
                    runtimeCancellationToken);
                var completionState = runtimeResponse.PendingApprovals.Count > 0
                    ? ExecutionState.WaitingOnTool
                    : portableStructuredOutput is null ||
                      portableStructuredOutput.ValidationStatus == AgentJsonSchemaOutputValidationStatus.Valid
                        ? ExecutionState.Completed
                        : ExecutionState.Failed;
                var completionOutcome = completionState switch
                {
                    ExecutionState.Completed => RunOutcome.Succeeded,
                    ExecutionState.Failed => RunOutcome.Failed,
                    _ => (RunOutcome?)null
                };

                var assistantMessage = session is null
                    ? null
                    : new ChatMessageRecord(
                        Id: Guid.NewGuid(),
                        Role: ChatMessageRole.Assistant,
                        Content: runtimeResponse.ResponseText,
                        CreatedAtUtc: DateTimeOffset.UtcNow,
                        TokenEstimate: totalOutputTokens);

                var metric = PriceMetric(
                    new AgentRunMetric(
                        Id: Guid.NewGuid(),
                        AgentId: agent.Id,
                        ChatSessionId: run.ChatSessionId,
                        CreatedAtUtc: DateTimeOffset.UtcNow,
                        Outcome: runtimeResponse.PendingApprovals.Count > 0
                            ? RunOutcome.Cancelled
                            : completionOutcome ?? RunOutcome.Succeeded,
                        ProviderName: provider.Name,
                        Model: ResolveModel(runtimeAgent, provider),
                        DurationMs: Math.Max(1, (long)(DateTimeOffset.UtcNow - startedAt).TotalMilliseconds),
                        InputTokens: totalInputTokens,
                        OutputTokens: totalOutputTokens,
                        ToolCalls: totalToolCalls)
                    {
                        CachedInputTokens = totalCachedInputTokens,
                        ExecutionRunId = run.Id
                    },
                    provider);
                var usageObservations = UsageObservationAssembler.BuildUsageObservations(
                    run,
                    runtimeAgent,
                    provider,
                    metric,
                    runtimeResponse,
                    ResolveEffectiveManagedSeedModel(runtimeAgent, provider));

                var updatedRun = UpdateRunFromResponse(
                    run,
                    runtimeResponse,
                    completionState,
                    completionOutcome,
                    DateTimeOffset.UtcNow);
                updatedRun = ApplyPortableJsonSchemaOutputEvidence(updatedRun, portableStructuredOutput);
                var toolInvocationTraceReceipts = CreateToolInvocationTraceReceipts(run, runtimeResponse);

                var approvalUpdate = ExecutionRunStateTransitions.SynchronizePendingApprovals(
                    prepared.RunApprovals,
                    updatedRun,
                    runtimeResponse.PendingApprovals,
                    DateTimeOffset.UtcNow);

                activityOperation.Report(
                    completionState == ExecutionState.WaitingOnTool
                        ? AgentExecutionActivityPhase.AwaitingApproval
                        : AgentExecutionActivityPhase.PersistingResult,
                    completionState == ExecutionState.WaitingOnTool
                        ? "The agent still requires a tool approval decision."
                        : "Persisting the continued agent result.");
                if (session is not null)
                {
                    var updatedSession = ChatSessionRuntimeCompatibilityAdapter.ClearCompatibility(
                        session with
                        {
                            Messages = assistantMessage is null
                                ? session.Messages
                                : session.Messages.Append(assistantMessage).ToList()
                        },
                        updatedRun.UpdatedAtUtc,
                        updatedRun.Id);

                    await PersistExecutionMutationAsync(
                        new ExecutionStateMutation(
                            Run: updatedRun,
                            Session: updatedSession,
                            RunApprovals: approvalUpdate.RunApprovals,
                            Metric: metric,
                            UsageObservations: usageObservations,
                            ToolReceipts: toolInvocationTraceReceipts),
                        cancellationToken);
                }
                else
                {
                    await PersistExecutionMutationAsync(
                        new ExecutionStateMutation(
                            Run: updatedRun,
                            RunApprovals: approvalUpdate.RunApprovals,
                            Metric: metric,
                            UsageObservations: usageObservations,
                            ToolReceipts: toolInvocationTraceReceipts),
                        cancellationToken);
                }
                terminalRunPersisted =
                    completionState is ExecutionState.Completed or ExecutionState.Failed;

                if (approvalUpdate.Pending.Count > 0)
                {
                    await executionGovernanceBridge.OnApprovalsRequestedAsync(updatedRun, approvalUpdate.Pending, cancellationToken);
                }

                if (completionState is ExecutionState.Completed or ExecutionState.Failed)
                {
                    AgentFrameworkTelemetry.RecordRunOutcome(updatedRun);
                    transientContextRegistry.Remove(run.Id);
                }

                await AppendExecutionLogAsync(
                    run.Id,
                    agent.Id,
                    run.ChatSessionId,
                    completionState,
                    completionState switch
                    {
                        ExecutionState.Completed => "Completed",
                        ExecutionState.Failed => "Output validation",
                        _ => "Approval"
                    },
                    completionState switch
                    {
                        ExecutionState.Completed => "Execution run response persisted after the approval decision.",
                        ExecutionState.Failed => "Execution run response and portable JSON Schema validation failure were persisted after the approval decision.",
                        _ => "The execution run still requires another approval decision before it can continue."
                    },
                    cancellationToken);

                TerminalizePersistedActivity(
                    activityOperation,
                    completionState);
                return new ExecutionRunResult(run.Id, run.ChatSessionId, runtimeResponse.ResponseText, assistantMessage, metric)
                {
                    State = completionState,
                    StructuredOutput = portableStructuredOutput,
                    ContextCompletionNotification = AgentChatContextInvocationFactory.CreateCompletionNotification(updatedRun)
                };
            }
        }
        catch (AgentExecutionActivityPublicationException)
        {
            throw;
        }
        catch (Exception exception)
        {
            transientContextRegistry.Remove(run.Id);
            var cancellationKind = ClassifyExecutionCancellation(exception, executionCancellation, cancellationToken);
            var wasCancelled = cancellationKind != ExecutionCancellationKind.None;
            var outcome = wasCancelled ? RunOutcome.Cancelled : RunOutcome.Failed;
            var failureIdentity = ResolveProviderFailureIdentity(exception);
            var failureProvider = await ResolveFailureProviderAsync(provider, exception);
            var failureModel = ResolveFailureProviderModel(
                runtimeAgent?.Model ?? run.Model,
                exception);
            AgentProviderFailureDisplay? failureDisplay = null;
            if (!wasCancelled)
            {
                AgentProviderFailureDisplayFormatter.TryFormat(
                    failureProvider,
                    exception,
                    out failureDisplay!);
            }
            var resultSummary = cancellationKind switch
            {
                ExecutionCancellationKind.ProcessRegistry => "Execution run cancelled because the owning process run was cancelled.",
                ExecutionCancellationKind.CallerRequest => "Execution run cancelled because the caller request was cancelled.",
                _ => ResolveRunFailureDisplayMessage(exception, failureDisplay)
            };
            var failureToolInvocationTraces = ResolveFailureToolInvocationTraces(
                lastRuntimeResponse,
                exception);
            var failureMetric = PriceMetric(
                new AgentRunMetric(
                    Id: Guid.NewGuid(),
                    AgentId: agent.Id,
                    ChatSessionId: run.ChatSessionId,
                    CreatedAtUtc: DateTimeOffset.UtcNow,
                    Outcome: outcome,
                    ProviderName: failureProvider.Name,
                    Model: failureModel,
                    DurationMs: Math.Max(1, (long)(DateTimeOffset.UtcNow - startedAt).TotalMilliseconds),
                    InputTokens: 0,
                    OutputTokens: 0,
                    ToolCalls: failureToolInvocationTraces.Count)
                {
                    ExecutionRunId = run.Id
                },
                failureProvider);
            var failureUsageObservations = lastRuntimeResponse is null
                ? UsageObservationAssembler.BuildFailureUsageObservations(
                    run,
                    runtimeAgent ?? agent,
                    failureProvider,
                    failureMetric,
                    exception,
                    failureModel)
                : UsageObservationAssembler.BuildRuntimeResponseUsageObservations(
                    run,
                    runtimeAgent ?? agent,
                    failureProvider,
                    failureMetric,
                    lastRuntimeResponse,
                    failureModel);
            var failureToolReceipts = CreateToolInvocationTraceReceipts(
                run,
                failureToolInvocationTraces);

            var failedRun = run with
            {
                Revision = NextRunRevision(run.Revision),
                UpdatedAtUtc = DateTimeOffset.UtcNow,
                CompletedAtUtc = DateTimeOffset.UtcNow,
                State = ExecutionState.Failed,
                Outcome = outcome,
                ResultSummary = CreateExecutionSummary(resultSummary),
                PendingApprovals = [],
                FailureProviderProfileId = failureIdentity?.ProviderProfileId,
                FailureProviderName = failureIdentity is null ? string.Empty : failureProvider.Name,
                FailureModel = failureIdentity is null ? string.Empty : failureModel,
                EntryAgentRequestCompatibilityEvidence = ResolveEntryAgentRequestCompatibilityEvidence(
                    lastRuntimeResponse,
                    exception) ?? run.EntryAgentRequestCompatibilityEvidence
            };
            var terminalPersistenceToken = CancellationToken.None;
            try
            {
                var persistence = await PersistFailedExecutionRunAsync(
                    failedRun,
                    session,
                    failureMetric,
                    failureUsageObservations,
                    failureToolReceipts,
                    agent.Id,
                    wasCancelled ? "Cancelled" : "Failed",
                    wasCancelled
                        ? resultSummary
                        : failureDisplay is null
                            ? resultSummary
                            : $"Execution run approval continuation failed for {failureProvider.Name}: {failureDisplay.Message}",
                    terminalPersistenceToken);
                terminalRunPersisted = persistence.TerminalRunPersisted;
                if (persistence.ExecutionLogFailure is not null)
                {
                    LogTerminalFailurePersistenceFailure(
                        run.Id,
                        agent.Id,
                        run.ChatSessionId,
                        exception,
                        persistence.ExecutionLogFailure);
                }
            }
            catch (Exception persistenceException)
            {
                LogTerminalFailurePersistenceFailure(
                    run.Id,
                    agent.Id,
                    run.ChatSessionId,
                    exception,
                    persistenceException);
                TerminalizeFailedActivityBestEffort(
                    activityOperation,
                    wasCancelled: false,
                    run.Id);
                throw CreateRunFailureException(
                    agent,
                    run,
                    session,
                    failureProvider,
                    failureModel,
                    new AggregateException(
                        "The agent run failed and its terminal failure state could not be fully persisted.",
                        exception,
                        persistenceException),
                    UnclassifiedRunFailureMessage,
                    failureCategory: null);
            }

            TerminalizeFailedActivityBestEffort(
                activityOperation,
                wasCancelled,
                run.Id);
            if (cancellationKind == ExecutionCancellationKind.ProcessRegistry)
            {
                throw new AgentExecutionCancelledException(run.Id, run.ProcessRunId, resultSummary, exception);
            }

            if (cancellationKind == ExecutionCancellationKind.CallerRequest)
            {
                throw new OperationCanceledException(resultSummary, exception, cancellationToken);
            }

            throw CreateRunFailureException(
                agent,
                run,
                session,
                failureProvider,
                failureModel,
                exception,
                resultSummary,
                failureDisplay?.Category);
        }
        finally
        {
            executionCancellation?.Dispose();
            credentialScope?.Dispose();
            credentialDispatch?.Dispose();
            await CleanupTerminalExecutionRunProcessesAsync(
                run,
                terminalRunPersisted);
        }
    }

    public async Task<IReadOnlyList<ExecutionRunRecord>> ListExecutionRunsAsync(
        ExecutionRunQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var executionState = query.ApprovalStatus.HasValue
            ? await store.LoadExecutionAsync(cancellationToken)
            : null;
        var runs = executionState is not null
            ? executionState.ExecutionRuns.AsEnumerable()
            : TryGetExecutionRunStore() is { } executionRunStore
                ? (await executionRunStore.ListExecutionRunsAsync(cancellationToken)).AsEnumerable()
                : (await store.LoadExecutionAsync(cancellationToken)).ExecutionRuns.AsEnumerable();

        if (query.ExecutionRunId.HasValue)
        {
            runs = runs.Where(item => item.Id == query.ExecutionRunId.Value);
        }

        if (query.AgentId.HasValue)
        {
            runs = runs.Where(item => item.AgentId == query.AgentId.Value);
        }

        if (query.ChatSessionId.HasValue)
        {
            runs = runs.Where(item => item.ChatSessionId == query.ChatSessionId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.CorrelationId))
        {
            runs = runs.Where(item => string.Equals(item.CorrelationId, query.CorrelationId, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query.SourceKind))
        {
            runs = runs.Where(item => string.Equals(item.SourceKind, query.SourceKind, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query.SourceId))
        {
            runs = runs.Where(item => string.Equals(item.SourceId, query.SourceId, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query.ProcessRunId))
        {
            runs = runs.Where(item => string.Equals(item.ProcessRunId, query.ProcessRunId, StringComparison.OrdinalIgnoreCase));
        }

        if (query.ProcessRunIds.Count > 0)
        {
            var processRunIds = query.ProcessRunIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
            runs = runs.Where(item => processRunIds.Contains(item.ProcessRunId));
        }

        if (!string.IsNullOrWhiteSpace(query.ProcessStepId))
        {
            runs = runs.Where(item => string.Equals(item.ProcessStepId, query.ProcessStepId, StringComparison.OrdinalIgnoreCase));
        }

        if (query.ProcessStepIds.Count > 0)
        {
            var processStepIds = query.ProcessStepIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
            runs = runs.Where(item => processStepIds.Contains(item.ProcessStepId));
        }

        if (!string.IsNullOrWhiteSpace(query.SchedulerRunId))
        {
            runs = runs.Where(item => string.Equals(item.SchedulerRunId, query.SchedulerRunId, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query.MessageId))
        {
            runs = runs.Where(item => string.Equals(item.MessageId, query.MessageId, StringComparison.OrdinalIgnoreCase));
        }

        if (query.State.HasValue)
        {
            runs = runs.Where(item => item.State == query.State.Value);
        }

        if (query.Outcome.HasValue)
        {
            runs = runs.Where(item => item.Outcome == query.Outcome.Value);
        }

        if (query.CreatedFromUtc.HasValue)
        {
            runs = runs.Where(item => item.CreatedAtUtc >= query.CreatedFromUtc.Value);
        }

        if (query.CreatedToUtc.HasValue)
        {
            runs = runs.Where(item => item.CreatedAtUtc <= query.CreatedToUtc.Value);
        }

        if (query.UpdatedFromUtc.HasValue)
        {
            runs = runs.Where(item => item.UpdatedAtUtc >= query.UpdatedFromUtc.Value);
        }

        if (query.UpdatedToUtc.HasValue)
        {
            runs = runs.Where(item => item.UpdatedAtUtc <= query.UpdatedToUtc.Value);
        }

        if (query.MetadataStringEquals.Count > 0)
        {
            if (query.MetadataStringEquals.Any(filter => string.IsNullOrWhiteSpace(filter.Key)))
            {
                throw new ArgumentException(
                    "Execution metadata string filters must use non-empty keys.",
                    nameof(query));
            }

            runs = runs.Where(item =>
                MatchesMetadataStringValues(
                    item.MetadataJson,
                    query.MetadataStringEquals));
        }

        if (query.ApprovalStatus.HasValue)
        {
            executionState ??= await store.LoadExecutionAsync(cancellationToken);
            runs = runs.Where(item => ExecutionRunStateTransitions.MatchesApprovalStatus(executionState.ExecutionApprovals, item, query.ApprovalStatus.Value));
        }

        return runs
            .OrderByDescending(item => item.UpdatedAtUtc)
            .Take(query.Take <= 0 ? 50 : query.Take)
            .ToList();
    }

    internal static bool MatchesMetadataStringValues(
        string? metadataJson,
        IReadOnlyDictionary<string, string> expectedValues)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(metadataJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            foreach (var expected in expectedValues)
            {
                if (!document.RootElement.TryGetProperty(expected.Key, out var value) ||
                    value.ValueKind != JsonValueKind.String ||
                    !string.Equals(value.GetString(), expected.Value, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public Task<AgentExecutionReportPage> QueryExecutionReportAsync(
        AgentExecutionReportQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return executionReportReader.QueryExecutionReportAsync(
            query,
            cancellationToken);
    }

    public async Task<ExecutionRunDetail> GetExecutionRunDetailAsync(
        Guid executionRunId,
        CancellationToken cancellationToken = default)
    {
        return EnrichProviderNativeMcpDetail(
            await LoadExecutionRunDetailAsync(executionRunId, cancellationToken));
    }

    public async Task<IReadOnlyList<ExecutionArtifactRecord>> ListExecutionArtifactsAsync(
        Guid executionRunId,
        CancellationToken cancellationToken = default)
    {
        return (await GetExecutionRunDetailAsync(executionRunId, cancellationToken)).Artifacts;
    }

    public async Task<IReadOnlyList<ExecutionWorkflowCheckpointRecord>> ListExecutionWorkflowCheckpointsAsync(
        Guid executionRunId,
        CancellationToken cancellationToken = default)
    {
        return (await LoadExecutionRunDetailAsync(executionRunId, cancellationToken)).Checkpoints;
    }

    public async Task<IReadOnlyList<ToolExecutionReceiptRecord>> ListToolExecutionReceiptsAsync(
        Guid executionRunId,
        CancellationToken cancellationToken = default)
    {
        return (await GetExecutionRunDetailAsync(executionRunId, cancellationToken)).ToolReceipts;
    }

    private async Task<ExecutionRunResult> ExecuteRunCoreAsync(
        AgentExecutionPreparationSnapshot preparation,
        ProviderProfile provider,
        ExecutionRunRequest request,
        bool persistTranscript,
        IReadOnlyList<AgentRuntimeInputAttachment> inputAttachments,
        CancellationToken cancellationToken,
        IAgentExecutionActivityOperationLease activityOperation)
    {
        var prompt = request.Prompt.Trim();
        var jsonSchemaOutput = AgentJsonSchemaOutputContractProcessor.Prepare(request.JsonSchemaOutput);
        var context = PrepareInvocationContext(request);
        request = request with { Context = context };
        activityOperation.Report(
            AgentExecutionActivityPhase.CreatingExecution,
            persistTranscript
                ? "Preparing atomic chat execution admission."
                : "Preparing execution admission.");
        AgentExecutionStartupAggregate startup;

        if (persistTranscript)
        {
            const int maximumStartAttempts = 3;
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    startup = await BeginChatBackedRunAsync(
                        preparation,
                        provider,
                        request.ChatSessionId,
                        prompt,
                        context,
                        request.AutoApprovePendingToolCalls,
                        request.StructuredOutput,
                        jsonSchemaOutput,
                        inputAttachments,
                        request.InitialActivityOperationId,
                        cancellationToken);
                    break;
                }
                catch (Exception exception)
                    when (attempt < maximumStartAttempts &&
                          exception is
                              AgentExecutionPreparationStaleException or
                              SandboxWorkspaceCatalogConcurrencyException)
                {
                    activityOperation.Report(
                        AgentExecutionActivityPhase.ResolvingPreparation,
                        "The agent configuration changed before execution creation; refreshing the prepared data.");
                    preparation = await AcquireExecutionPreparationAsync(
                        activityOperation,
                        request.AgentId,
                        atomicConsumer:
                            store is ISandboxWorkspaceChatRunStartStore,
                        cancellationToken).ConfigureAwait(false);
                    provider = await ResolvePreparedProviderAsync(
                        activityOperation,
                        preparation,
                        request,
                        cancellationToken).ConfigureAwait(false);
                }
            }
        }
        else
        {
            var now = DateTimeOffset.UtcNow;
            EnsurePreparationCurrentForUse(
                preparation.Blueprint,
                preparation.CatalogSnapshot);
            var agent = CreateProviderCompatibleRuntimeAgent(
                preparation.Blueprint.Agent,
                provider,
                ResolveEffectiveManagedSeedModel(
                    preparation.Blueprint.Agent,
                    provider));
            var run = CreatePreparingRun(
                agent,
                provider,
                chatSessionId: null,
                CreateSessionTitle(prompt),
                context,
                prompt,
                now,
                request.AutoApprovePendingToolCalls,
                request.InitialActivityOperationId,
                request.StructuredOutput,
                jsonSchemaOutput);

            await PersistNewExecutionRunAsync(
                run,
                cancellationToken);
            startup = new AgentExecutionStartupAggregate(
                preparation.Blueprint,
                preparation.CatalogSnapshot,
                agent,
                provider,
                Session: null,
                run,
                UserMessage: null,
                inputAttachments);
        }

        activityOperation.BindExecutionRun(
            startup.Run.Id,
            startup.Run.ChatSessionId);

        return await CompletePreparedExecutionRunAsync(
            startup,
            request,
            cancellationToken,
            activityOperation);
    }

    private async Task<ExecutionRunResult> CompletePreparedExecutionRunAsync(
        AgentExecutionStartupAggregate startup,
        ExecutionRunRequest request,
        CancellationToken cancellationToken,
        IAgentExecutionActivityOperationLease activityOperation)
    {
        EnsureRequiredActivityOperationId(
            request.InitialActivityOperationId,
            nameof(request));
        if (activityOperation.StreamId.OperationId != request.InitialActivityOperationId)
        {
            throw new InvalidOperationException(
                "The activity operation does not match the execution request.");
        }

        var agent = startup.Agent;
        var provider = startup.Provider;
        var catalog = startup.CatalogSnapshot.Catalog;
        var session = startup.Session;
        var run = startup.Run;
        var userMessage = startup.UserMessage;
        var prompt = request.Prompt.Trim();
        var jsonSchemaOutput = AgentJsonSchemaOutputContractProcessor.Restore(run);
        using var runActivity = AgentFrameworkTelemetry.StartRunActivity("agent.run", run);
        var startedAt = DateTimeOffset.UtcNow;
        AgentDefinition? runtimeAgent = null;
        AgentRuntimeResponse? lastRuntimeResponse = null;
        var lastEntryAgentRequestCompatibilityEvidence = run.EntryAgentRequestCompatibilityEvidence;

        AgentRuntimeResponse TrackRuntimeResponse(AgentRuntimeResponse response)
        {
            lastEntryAgentRequestCompatibilityEvidence =
                response.EntryAgentRequestCompatibilityEvidence ??
                lastEntryAgentRequestCompatibilityEvidence;
            var trackedResponse = response.EntryAgentRequestCompatibilityEvidence is null &&
                                  lastEntryAgentRequestCompatibilityEvidence is not null
                ? response with
                {
                    EntryAgentRequestCompatibilityEvidence =
                        lastEntryAgentRequestCompatibilityEvidence
                }
                : response;
            lastRuntimeResponse = trackedResponse;
            return trackedResponse;
        }

        IAgentExecutionCancellationRegistration? executionCancellation = null;
        AgentProviderCredentialDispatchLease? credentialDispatch = null;
        IAgentProviderCredentialDispatchScope? credentialScope = null;
        var terminalRunPersisted = false;
        try
        {
            if (!run.ChatSessionId.HasValue)
            {
                var currentCatalogSnapshot = await store
                    .LoadCatalogSnapshotAsync(cancellationToken)
                    .ConfigureAwait(false);
                EnsurePreparationCurrentForUse(
                    startup.Blueprint,
                    currentCatalogSnapshot);
                catalog = currentCatalogSnapshot.Catalog;
            }

            activityOperation.Report(
                AgentExecutionActivityPhase.PreparingCapabilities,
                "Validating prepared tools, memory, and handoff capabilities.");
            EnsurePreparationCurrentForUse(
                startup.Blueprint,
                startup.CatalogSnapshot);
            var attachedCapabilities = startup.Blueprint.Capabilities;
            IReadOnlyList<AgentMemoryRecord> memory =
                IsGovernedMachineCriticalRun(run)
                    ? []
                    : startup.Blueprint.Memory;
            var handoffOptions = await ResolveHandoffExecutionOptionsAsync(
                agent,
                catalog,
                run,
                cancellationToken);
            activityOperation.Report(
                AgentExecutionActivityPhase.PreparingRuntime,
                "Preparing the agent runtime.");

            credentialDispatch =
                await providerCredentialDispatchScopeFactory.PrepareAsync(
                    ResolveProviderCredentialDispatchProviders(
                        provider,
                        handoffOptions),
                    cancellationToken);
            credentialScope = credentialDispatch.BeginScope();
            runtimeAgent = CreateProviderCompatibleRuntimeAgent(
                agent,
                provider,
                run.Model);

            await AppendExecutionLogAsync(
                run.Id,
                agent.Id,
                run.ChatSessionId,
                ExecutionState.Preparing,
                "Planning",
                $"Preparing provider {provider.Name}.",
                cancellationToken);
            await AppendProcessCooperationLogAsync(
                run,
                agent.Id,
                run.ChatSessionId,
                cancellationToken);

            if (request.TransientContext is not null)
            {
                transientContextRegistry.Register(run, request.TransientContext);
            }

            executionCancellation = executionCancellationRegistry.Register(run, cancellationToken);
            var runtimeCancellationToken = executionCancellation.Token;
            var runtimeSession = ChatSessionRuntimeCompatibilityAdapter.CreateRuntimeSession(run, agent.Id, session);
            var runtimeExecutionOptions = CreateRuntimeExecutionOptionsCore(
                run,
                activityOperation.StreamId.OperationId,
                request.StructuredOutput,
                handoffOptions,
                startup.InputAttachments,
                jsonSchemaOutput);
            AgentRuntimeResponse runtimeResponse;
            using (WorkspaceExecutionAuditContext.BeginScope(
                       run,
                       runtimeExecutionOptions.ContextWorkspaceScope,
                       activityWorkspaceIdentity.WorkspaceScope))
            {
                activityOperation.Report(
                    AgentExecutionActivityPhase.WaitingForProvider,
                    "Waiting for the configured provider.");
                runtimeResponse = TrackRuntimeResponse(await executionRuntime.ExecuteAsync(
                    new AgentRuntimeExecutionRequest(
                        runtimeAgent,
                        provider,
                        runtimeSession,
                        attachedCapabilities,
                        memory,
                        prompt,
                        string.IsNullOrWhiteSpace(run.RuntimeSessionKey) ? null : run.RuntimeSessionKey,
                        (state, phase, message) => ReportRuntimeProgressAsync(
                            activityOperation,
                            run,
                            agent.Id,
                            state,
                            phase,
                            message,
                            cancellationToken),
                        SuppressApprovalRequirements: ShouldAutoApprovePendingToolCalls(agent, runtimeSession),
                        StructuredOutput: request.StructuredOutput,
                        ExecutionOptions: runtimeExecutionOptions),
                    runtimeCancellationToken));

                var totalInputTokens = runtimeResponse.InputTokens;
                var totalCachedInputTokens = runtimeResponse.CachedInputTokens;
                var totalOutputTokens = runtimeResponse.OutputTokens;
                var totalToolCalls = runtimeResponse.ToolCalls;

                if (runtimeResponse.PendingApprovals.Count > 0 && ShouldAutoApprovePendingToolCalls(agent, runtimeSession))
                {
                    var continuation = await ContinueAutoApprovedRunAsync(
                        run,
                        activityOperation.StreamId.OperationId,
                        runtimeAgent,
                        provider,
                        runtimeSession,
                        run.ChatSessionId,
                        attachedCapabilities,
                        memory,
                        runtimeResponse,
                        (state, phase, message) => ReportRuntimeProgressAsync(
                            activityOperation,
                            run,
                            agent.Id,
                            state,
                            phase,
                            message,
                            cancellationToken),
                        request.StructuredOutput,
                        handoffOptions,
                        new HashSet<string>(StringComparer.Ordinal),
                        response =>
                        {
                            TrackRuntimeResponse(response);
                        },
                        runtimeCancellationToken);

                    runtimeSession = continuation.Session;
                    runtimeResponse = TrackRuntimeResponse(continuation.Response);
                    totalInputTokens = continuation.TotalInputTokens;
                    totalCachedInputTokens = continuation.TotalCachedInputTokens;
                    totalOutputTokens = continuation.TotalOutputTokens;
                    totalToolCalls = continuation.TotalToolCalls;
                }

                runtimeResponse = TrackRuntimeResponse(await ValidateMachineOutputBeforeCompletionAsync(
                    run,
                    request.StructuredOutput,
                    runtimeResponse,
                    runtimeCancellationToken));
                var portableStructuredOutput = await ValidatePortableJsonSchemaOutputBeforeCompletionAsync(
                    run,
                    runtimeResponse,
                    runtimeCancellationToken);
                var completionState = runtimeResponse.PendingApprovals.Count > 0
                    ? ExecutionState.WaitingOnTool
                    : portableStructuredOutput is null ||
                      portableStructuredOutput.ValidationStatus == AgentJsonSchemaOutputValidationStatus.Valid
                        ? ExecutionState.Completed
                        : ExecutionState.Failed;
                var completionOutcome = completionState switch
                {
                    ExecutionState.Completed => RunOutcome.Succeeded,
                    ExecutionState.Failed => RunOutcome.Failed,
                    _ => (RunOutcome?)null
                };

                var assistantMessage = session is null
                    ? null
                    : new ChatMessageRecord(
                        Id: Guid.NewGuid(),
                        Role: ChatMessageRole.Assistant,
                        Content: runtimeResponse.ResponseText,
                        CreatedAtUtc: DateTimeOffset.UtcNow,
                        TokenEstimate: totalOutputTokens);

                var metric = PriceMetric(
                    new AgentRunMetric(
                        Id: Guid.NewGuid(),
                        AgentId: agent.Id,
                        ChatSessionId: run.ChatSessionId,
                        CreatedAtUtc: DateTimeOffset.UtcNow,
                        Outcome: runtimeResponse.PendingApprovals.Count > 0
                            ? RunOutcome.Cancelled
                            : completionOutcome ?? RunOutcome.Succeeded,
                        ProviderName: provider.Name,
                        Model: ResolveModel(runtimeAgent, provider),
                        DurationMs: Math.Max(1, (long)(DateTimeOffset.UtcNow - startedAt).TotalMilliseconds),
                        InputTokens: totalInputTokens,
                        OutputTokens: totalOutputTokens,
                        ToolCalls: totalToolCalls)
                    {
                        CachedInputTokens = totalCachedInputTokens,
                        ExecutionRunId = run.Id
                    },
                    provider);
                var usageObservations = UsageObservationAssembler.BuildUsageObservations(
                    run,
                    runtimeAgent,
                    provider,
                    metric,
                    runtimeResponse,
                    ResolveEffectiveManagedSeedModel(runtimeAgent, provider));

                var updatedRun = UpdateRunFromResponse(
                    run,
                    runtimeResponse,
                    completionState,
                    completionOutcome,
                    DateTimeOffset.UtcNow);
                updatedRun = ApplyPortableJsonSchemaOutputEvidence(updatedRun, portableStructuredOutput);
                var toolInvocationTraceReceipts = CreateToolInvocationTraceReceipts(run, runtimeResponse);

                var approvalUpdate = ExecutionRunStateTransitions.SynchronizePendingApprovals(
                    [],
                    updatedRun,
                    runtimeResponse.PendingApprovals,
                    DateTimeOffset.UtcNow);

                activityOperation.Report(
                    completionState == ExecutionState.WaitingOnTool
                        ? AgentExecutionActivityPhase.AwaitingApproval
                        : AgentExecutionActivityPhase.PersistingResult,
                    completionState == ExecutionState.WaitingOnTool
                        ? "The agent is waiting for a tool approval decision."
                        : "Persisting the agent result.");
                if (session is not null)
                {
                    var updatedSession = ChatSessionRuntimeCompatibilityAdapter.ClearCompatibility(
                        session with
                        {
                            Messages = assistantMessage is null
                                ? session.Messages
                                : session.Messages.Append(assistantMessage).ToList()
                        },
                        updatedRun.UpdatedAtUtc,
                        updatedRun.Id);

                    await PersistExecutionMutationAsync(
                        new ExecutionStateMutation(
                            Run: updatedRun,
                            Session: updatedSession,
                            RunApprovals: approvalUpdate.RunApprovals,
                            Metric: metric,
                            UsageObservations: usageObservations,
                            ToolReceipts: toolInvocationTraceReceipts),
                        cancellationToken);
                }
                else
                {
                    await PersistExecutionMutationAsync(
                        new ExecutionStateMutation(
                            Run: updatedRun,
                            RunApprovals: approvalUpdate.RunApprovals,
                            Metric: metric,
                            UsageObservations: usageObservations,
                            ToolReceipts: toolInvocationTraceReceipts),
                        cancellationToken);
                }
                terminalRunPersisted =
                    completionState is ExecutionState.Completed or ExecutionState.Failed;

                if (approvalUpdate.Pending.Count > 0)
                {
                    await executionGovernanceBridge.OnApprovalsRequestedAsync(updatedRun, approvalUpdate.Pending, cancellationToken);
                }

                if (completionState is ExecutionState.Completed or ExecutionState.Failed)
                {
                    AgentFrameworkTelemetry.RecordRunOutcome(updatedRun);
                    transientContextRegistry.Remove(run.Id);
                }

                await AppendExecutionLogAsync(
                    run.Id,
                    agent.Id,
                    run.ChatSessionId,
                    completionState,
                    completionState switch
                    {
                        ExecutionState.Completed => "Completed",
                        ExecutionState.Failed => "Output validation",
                        _ => "Approval"
                    },
                    completionState switch
                    {
                        ExecutionState.Completed => "Execution run response persisted.",
                        ExecutionState.Failed => "Execution run response and portable JSON Schema validation failure were persisted.",
                        _ => "The execution run is waiting for an approval response before it can continue."
                    },
                    cancellationToken);

                TerminalizePersistedActivity(
                    activityOperation,
                    completionState);
                return new ExecutionRunResult(run.Id, run.ChatSessionId, runtimeResponse.ResponseText, assistantMessage, metric)
                {
                    State = completionState,
                    StructuredOutput = portableStructuredOutput,
                    ContextCompletionNotification = AgentChatContextInvocationFactory.CreateCompletionNotification(updatedRun)
                };
            }
        }
        catch (AgentExecutionActivityPublicationException)
        {
            throw;
        }
        catch (Exception exception)
        {
            transientContextRegistry.Remove(run.Id);
            var cancellationKind = ClassifyExecutionCancellation(exception, executionCancellation, cancellationToken);
            var wasCancelled = cancellationKind != ExecutionCancellationKind.None;
            var outcome = wasCancelled ? RunOutcome.Cancelled : RunOutcome.Failed;
            var failureIdentity = ResolveProviderFailureIdentity(exception);
            var failureProvider = await ResolveFailureProviderAsync(provider, exception);
            var failureModel = ResolveFailureProviderModel(
                runtimeAgent?.Model ?? run.Model,
                exception);
            AgentProviderFailureDisplay? failureDisplay = null;
            if (!wasCancelled)
            {
                AgentProviderFailureDisplayFormatter.TryFormat(
                    failureProvider,
                    exception,
                    out failureDisplay!);
            }
            var resultSummary = cancellationKind switch
            {
                ExecutionCancellationKind.ProcessRegistry => "Execution run cancelled because the owning process run was cancelled.",
                ExecutionCancellationKind.CallerRequest => "Execution run cancelled because the caller request was cancelled.",
                _ => ResolveRunFailureDisplayMessage(exception, failureDisplay)
            };
            var failureToolInvocationTraces = ResolveFailureToolInvocationTraces(
                lastRuntimeResponse,
                exception);
            var failureMetric = PriceMetric(
                new AgentRunMetric(
                    Id: Guid.NewGuid(),
                    AgentId: agent.Id,
                    ChatSessionId: run.ChatSessionId,
                    CreatedAtUtc: DateTimeOffset.UtcNow,
                    Outcome: outcome,
                    ProviderName: failureProvider.Name,
                    Model: failureModel,
                    DurationMs: Math.Max(1, (long)(DateTimeOffset.UtcNow - startedAt).TotalMilliseconds),
                    InputTokens: userMessage?.TokenEstimate ?? EstimateTokens(prompt),
                    OutputTokens: 0,
                    ToolCalls: failureToolInvocationTraces.Count)
                {
                    ExecutionRunId = run.Id
                },
                failureProvider);
            var failureUsageObservations = lastRuntimeResponse is null
                ? UsageObservationAssembler.BuildFailureUsageObservations(
                    run,
                    runtimeAgent ?? agent,
                    failureProvider,
                    failureMetric,
                    exception,
                    failureModel)
                : UsageObservationAssembler.BuildRuntimeResponseUsageObservations(
                    run,
                    runtimeAgent ?? agent,
                    failureProvider,
                    failureMetric,
                    lastRuntimeResponse,
                    failureModel);
            var failureToolReceipts = CreateToolInvocationTraceReceipts(
                run,
                failureToolInvocationTraces);

            var failedRun = run with
            {
                UpdatedAtUtc = DateTimeOffset.UtcNow,
                CompletedAtUtc = DateTimeOffset.UtcNow,
                State = ExecutionState.Failed,
                Outcome = outcome,
                ResultSummary = CreateExecutionSummary(resultSummary),
                PendingApprovals = [],
                FailureProviderProfileId = failureIdentity?.ProviderProfileId,
                FailureProviderName = failureIdentity is null ? string.Empty : failureProvider.Name,
                FailureModel = failureIdentity is null ? string.Empty : failureModel,
                EntryAgentRequestCompatibilityEvidence = ResolveEntryAgentRequestCompatibilityEvidence(
                    lastRuntimeResponse,
                    exception) ?? run.EntryAgentRequestCompatibilityEvidence
            };
            var terminalPersistenceToken = CancellationToken.None;
            try
            {
                var persistence = await PersistFailedExecutionRunAsync(
                    failedRun,
                    session,
                    failureMetric,
                    failureUsageObservations,
                    failureToolReceipts,
                    agent.Id,
                    wasCancelled ? "Cancelled" : "Failed",
                    wasCancelled
                        ? resultSummary
                        : failureDisplay is null
                            ? resultSummary
                            : $"Execution run failed for {failureProvider.Name}: {failureDisplay.Message}",
                    terminalPersistenceToken);
                terminalRunPersisted = persistence.TerminalRunPersisted;
                if (persistence.ExecutionLogFailure is not null)
                {
                    LogTerminalFailurePersistenceFailure(
                        run.Id,
                        agent.Id,
                        run.ChatSessionId,
                        exception,
                        persistence.ExecutionLogFailure);
                }
            }
            catch (Exception persistenceException)
            {
                LogTerminalFailurePersistenceFailure(
                    run.Id,
                    agent.Id,
                    run.ChatSessionId,
                    exception,
                    persistenceException);
                TerminalizeFailedActivityBestEffort(
                    activityOperation,
                    wasCancelled: false,
                    run.Id);
                throw CreateRunFailureException(
                    agent,
                    run,
                    session,
                    failureProvider,
                    failureModel,
                    new AggregateException(
                        "The agent run failed and its terminal failure state could not be fully persisted.",
                        exception,
                        persistenceException),
                    UnclassifiedRunFailureMessage,
                    failureCategory: null);
            }

            TerminalizeFailedActivityBestEffort(
                activityOperation,
                wasCancelled,
                run.Id);
            if (cancellationKind == ExecutionCancellationKind.ProcessRegistry)
            {
                throw new AgentExecutionCancelledException(run.Id, run.ProcessRunId, resultSummary, exception);
            }

            if (cancellationKind == ExecutionCancellationKind.CallerRequest)
            {
                throw new OperationCanceledException(resultSummary, exception, cancellationToken);
            }

            throw CreateRunFailureException(
                agent,
                run,
                session,
                failureProvider,
                failureModel,
                exception,
                resultSummary,
                failureDisplay?.Category);
        }
        finally
        {
            executionCancellation?.Dispose();
            credentialScope?.Dispose();
            credentialDispatch?.Dispose();
            await CleanupTerminalExecutionRunProcessesAsync(
                run,
                terminalRunPersisted);
        }
    }

    private async Task ReportRuntimeProgressAsync(
        IAgentExecutionActivityOperationLease activityOperation,
        ExecutionRunRecord run,
        Guid agentId,
        ExecutionState state,
        string phase,
        string message,
        CancellationToken cancellationToken)
    {
        var activityPhase =
            AgentExecutionActivityRuntimeProgressPolicy.ResolvePhase(
                state,
                phase);
        if (activityPhase.HasValue)
        {
            activityOperation.Report(
                activityPhase.Value,
                AgentExecutionActivityRuntimeProgressPolicy.ResolveMessage(
                    state,
                    phase));
        }

        await AppendExecutionLogAsync(
            run.Id,
            agentId,
            run.ChatSessionId,
            state,
            phase,
            message,
            cancellationToken);
    }

    private static void EnsureActivityRunBinding(
        IAgentExecutionActivityOperationLease activityOperation,
        ExecutionRunRecord run)
    {
        if (activityOperation.AgentId is { } boundAgentId)
        {
            if (boundAgentId != run.AgentId)
            {
                throw new InvalidOperationException(
                    "The activity operation belongs to a different agent.");
            }
        }
        else
        {
            activityOperation.BindAgent(run.AgentId);
        }

        if (activityOperation.ChatSessionId is { } boundSessionId &&
            run.ChatSessionId != boundSessionId)
        {
            throw new InvalidOperationException(
                "The activity operation belongs to a different chat session.");
        }

        if (activityOperation.ExecutionRunId is { } boundRunId)
        {
            if (boundRunId != run.Id)
            {
                throw new InvalidOperationException(
                    "The activity operation is bound to a different execution run.");
            }

            return;
        }

        activityOperation.BindExecutionRun(run.Id, run.ChatSessionId);
    }

    private static void TerminalizeSameSourceReservationActivity(
        IAgentExecutionActivityOperationLease activityOperation,
        ExecutionRunSourceReservationResult reservation)
    {
        switch (reservation.Disposition)
        {
            case ExecutionRunSourceDisposition.ReusedCompleted:
                activityOperation.Report(
                    AgentExecutionActivityPhase.PersistingResult,
                    "A completed execution run already exists for this source.");
                TerminalizePersistedActivity(
                    activityOperation,
                    reservation.Run.State);
                break;
            case ExecutionRunSourceDisposition.ExistingActive:
                activityOperation.Report(
                    AgentExecutionActivityPhase.PersistingResult,
                    "An active execution run already exists for this source.");
                activityOperation.Complete(
                    "The existing active execution run was returned.");
                break;
            case ExecutionRunSourceDisposition.Created:
                throw new ArgumentOutOfRangeException(
                    nameof(reservation),
                    reservation.Disposition,
                    "A created reservation is terminalized by execution.");
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(reservation),
                    reservation.Disposition,
                    null);
        }
    }

    private static async Task<TResult> ExecuteWithinActivityBoundaryAsync<TResult>(
        IAgentExecutionActivityOperationLease activityOperation,
        Func<Task<TResult>> executeAsync)
    {
        ArgumentNullException.ThrowIfNull(activityOperation);
        ArgumentNullException.ThrowIfNull(executeAsync);

        try
        {
            var result = await executeAsync().ConfigureAwait(false);
            if (!activityOperation.IsTerminal)
            {
                activityOperation.Fail(
                    "The agent operation completed without a terminal activity outcome.",
                    AgentExecutionActivityFailureCodes.UnhandledExecutionFailure);
                throw new InvalidOperationException(
                    "The workspace execution completed without a terminal activity outcome.");
            }

            return result;
        }
        catch (AgentExecutionActivityPublicationException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            TerminalizeFailedActivity(
                activityOperation,
                wasCancelled: true);
            throw;
        }
        catch
        {
            TerminalizeFailedActivity(
                activityOperation,
                wasCancelled: false);
            throw;
        }
    }

    private static void ReportAndTerminalizeExistingResult(
        IAgentExecutionActivityOperationLease activityOperation,
        ExecutionRunResult result)
    {
        activityOperation.Report(
            result.State == ExecutionState.WaitingOnTool
                ? AgentExecutionActivityPhase.AwaitingApproval
                : AgentExecutionActivityPhase.PersistingResult,
            "The execution run was already finalized.");
        TerminalizePersistedActivity(activityOperation, result.State);
    }

    private static void TerminalizePersistedActivity(
        IAgentExecutionActivityOperationLease activityOperation,
        ExecutionState completionState)
    {
        if (activityOperation.IsTerminal)
        {
            return;
        }

        var terminalPhase = completionState switch
        {
            ExecutionState.Completed => AgentExecutionActivityPhase.Completed,
            ExecutionState.Failed => AgentExecutionActivityPhase.Failed,
            ExecutionState.WaitingOnTool => AgentExecutionActivityPhase.AwaitingApproval,
            _ => throw new ArgumentOutOfRangeException(
                nameof(completionState),
                completionState,
                "The execution state is not terminal for an agent operation.")
        };
        try
        {
            switch (completionState)
            {
                case ExecutionState.Completed:
                    activityOperation.Complete("The agent operation completed.");
                    break;
                case ExecutionState.Failed:
                    activityOperation.Fail(
                        "The agent output did not satisfy its required contract.",
                        AgentExecutionActivityFailureCodes.OutputValidationFailure);
                    break;
                case ExecutionState.WaitingOnTool:
                    activityOperation.Suspend(
                        "The agent operation is suspended pending an approval decision.");
                    break;
            }
        }
        catch (Exception exception)
        {
            throw new AgentExecutionActivityPublicationException(
                activityOperation.StreamId,
                terminalPhase,
                exception);
        }
    }

    private static Exception CreateRunFailureException(
        AgentDefinition agent,
        ExecutionRunRecord run,
        ChatSessionRecord? session,
        ProviderProfile provider,
        string model,
        Exception innerException,
        string displayMessage,
        AgentProviderFailureCategory? failureCategory)
    {
        return session is not null
            ? new AgentChatRunFailedException(
                agent.Id,
                run.Id,
                session.Id,
                provider.Name,
                model,
                innerException,
                displayMessage,
                failureCategory)
            : new AgentRunFailedException(
                agent.Id,
                run.Id,
                run.ChatSessionId,
                provider.Name,
                model,
                innerException,
                displayMessage,
                failureCategory);
    }

    private static string ResolveRunFailureDisplayMessage(
        Exception exception,
        AgentProviderFailureDisplay? providerFailureDisplay)
    {
        if (providerFailureDisplay is not null)
        {
            return providerFailureDisplay.Message;
        }

        return exception is AgentExecutionGovernanceException governanceException
            ? governanceException.SanitizedDisplayMessage
            : UnclassifiedRunFailureMessage;
    }

    private void LogTerminalFailurePersistenceFailure(
        Guid executionRunId,
        Guid agentId,
        Guid? chatSessionId,
        Exception originalException,
        Exception persistenceException)
    {
        logger.LogError(
            "The agent run failed and its terminal failure state could not be fully persisted. ExecutionRunId={ExecutionRunId} AgentId={AgentId} ChatSessionId={ChatSessionId} OriginalFailureType={OriginalFailureType} PersistenceFailureType={PersistenceFailureType}.",
            executionRunId,
            agentId,
            chatSessionId,
            originalException.GetType().Name,
            persistenceException.GetType().Name);
    }

    private void TerminalizeFailedActivityBestEffort(
        IAgentExecutionActivityOperationLease activityOperation,
        bool wasCancelled,
        Guid executionRunId)
    {
        try
        {
            TerminalizeFailedActivity(activityOperation, wasCancelled);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                "Could not publish the terminal activity state for failed execution run {ExecutionRunId}. FailureType={FailureType}.",
                executionRunId,
                exception.GetType().Name);
        }
    }

    private static void TerminalizeFailedActivity(
        IAgentExecutionActivityOperationLease activityOperation,
        bool wasCancelled)
    {
        if (activityOperation.IsTerminal)
        {
            return;
        }

        var terminalPhase = wasCancelled
            ? AgentExecutionActivityPhase.Cancelled
            : AgentExecutionActivityPhase.Failed;
        try
        {
            if (wasCancelled)
            {
                activityOperation.Cancel("The agent operation was cancelled.");
            }
            else
            {
                activityOperation.Fail(
                    "The agent operation failed.",
                    AgentExecutionActivityFailureCodes.UnhandledExecutionFailure);
            }
        }
        catch (Exception exception)
        {
            throw new AgentExecutionActivityPublicationException(
                activityOperation.StreamId,
                terminalPhase,
                exception);
        }
    }

    private static IReadOnlyList<ProviderProfile>
        ResolveProviderCredentialDispatchProviders(
        ProviderProfile provider,
        AgentRuntimeHandoffExecutionOptions? handoffOptions)
    {
        return handoffOptions is null
            ? [provider]
            : [provider, .. handoffOptions.Participants.Select(
                participant => participant.Provider)];
    }

    private static AgentRunMetric PriceMetric(
        AgentRunMetric metric,
        ProviderProfile provider)
    {
        return ProviderPricingCalculator.TryCalculate(metric, provider, out var cost)
            ? metric with { CostUsd = cost.TotalUsd }
            : metric;
    }

    private enum ExecutionCancellationKind
    {
        None,
        ProcessRegistry,
        CallerRequest
    }

    private static ExecutionCancellationKind ClassifyExecutionCancellation(
        Exception exception,
        IAgentExecutionCancellationRegistration? executionCancellation,
        CancellationToken callerCancellationToken)
    {
        if (exception is not OperationCanceledException)
        {
            return ExecutionCancellationKind.None;
        }

        if (executionCancellation?.IsCancellationRequested == true &&
            !callerCancellationToken.IsCancellationRequested)
        {
            return ExecutionCancellationKind.ProcessRegistry;
        }

        return callerCancellationToken.IsCancellationRequested
            ? ExecutionCancellationKind.CallerRequest
            : ExecutionCancellationKind.None;
    }

    private static ExecutionRunRecord UpdateRunFromResponse(
        ExecutionRunRecord run,
        AgentRuntimeResponse response,
        ExecutionState state,
        RunOutcome? outcome,
        DateTimeOffset updatedAtUtc)
    {
        return run with
        {
            Revision = NextRunRevision(run.Revision),
            UpdatedAtUtc = updatedAtUtc,
            CompletedAtUtc = state == ExecutionState.Completed || state == ExecutionState.Failed ? updatedAtUtc : null,
            RuntimeSessionKey = response.RuntimeSessionKey,
            SerializedSessionStateJson = response.SerializedSessionStateJson,
            PendingApprovals = response.PendingApprovals,
            AutoApprovePendingToolCalls = run.AutoApprovePendingToolCalls,
            State = state,
            Outcome = outcome,
            EntryAgentRequestCompatibilityEvidence = response.EntryAgentRequestCompatibilityEvidence ?? run.EntryAgentRequestCompatibilityEvidence,
            ResultSummary = response.PendingApprovals.Count > 0
                ? $"Awaiting approval for {response.PendingApprovals.Count} tool request(s)."
                : CreateExecutionSummary(run, response)
        };
    }

    private static ProviderRequestCompatibilityEvidence? ResolveEntryAgentRequestCompatibilityEvidence(
        AgentRuntimeResponse? lastRuntimeResponse,
        Exception exception)
    {
        if (lastRuntimeResponse?.EntryAgentRequestCompatibilityEvidence is not null)
        {
            return lastRuntimeResponse.EntryAgentRequestCompatibilityEvidence;
        }

        return exception is AgentRuntimeUsageException usageException
            ? usageException.EntryAgentRequestCompatibilityEvidence
            : null;
    }

    private static ExecutionRunRecord ApplyPortableJsonSchemaOutputEvidence(
        ExecutionRunRecord run,
        AgentJsonSchemaOutputResult? result)
    {
        if (result is null)
        {
            return run;
        }

        return run with
        {
            StructuredOutputRawOutput = result.RawOutput,
            StructuredOutputValidationStatus = result.ValidationStatus.ToString(),
            StructuredOutputValidationErrorsJson = JsonSerializer.Serialize(
                result.ValidationErrors,
                AgentOutputJson.SerializerOptions)
        };
    }

    private AgentStructuredOutputContract? ResolveContinuationStructuredOutputContract(ExecutionRunRecord run)
    {
        if (AgentStructuredOutputContracts.TryResolve(run.StructuredOutputContractKey, out var storedContract))
        {
            return storedContract;
        }

        if (!string.IsNullOrWhiteSpace(run.StructuredOutputTypeName) &&
            AgentStructuredOutputContracts.TryResolve(run.StructuredOutputTypeName, out var typeContract))
        {
            return typeContract;
        }

        if (IsGovernedMachineCriticalRun(run))
        {
            throw new AgentExecutionGovernanceException(
                AgentExecutionGovernanceFailureKind.StructuredOutputContract,
                $"Execution run '{run.Id:N}' is a governed process-step run, but it does not carry a resolvable structured-output contract for approval continuation.");
        }

        return null;
    }

    private async Task<AgentRuntimeResponse> ValidateMachineOutputBeforeCompletionAsync(
        ExecutionRunRecord run,
        AgentStructuredOutputContract? structuredOutput,
        AgentRuntimeResponse response,
        CancellationToken cancellationToken)
    {
        if (structuredOutput is null || response.PendingApprovals.Count > 0)
        {
            return response;
        }

        if (!ExecutionInvocationMetadata.ResolveRequireStructuredOutputValidation(run) &&
            !IsGovernedMachineCriticalRun(run))
        {
            return response;
        }

        var registry = DefaultAgentOutputValidatorRegistry.Instance;
        if (!registry.TryResolve(structuredOutput.OutputType, out var validator))
        {
            if (IsGovernedMachineCriticalRun(run))
            {
                throw new AgentExecutionGovernanceException(
                    AgentExecutionGovernanceFailureKind.StructuredOutputContract,
                    $"Structured-output contract '{structuredOutput.ContractKey}' does not have a registered machine-output validator.");
            }

            return response;
        }

        response = await ValidateFinalizerBeforeCompletionAsync(
            run,
            structuredOutput,
            response,
            registry,
            validator,
            cancellationToken);

        var validation = validator.DeserializeAndValidate(response.ResponseText);
        var originalRawOutputHash = validation.RawOutputHash;
        var repairAttemptCount = 0;
        var maxRepairAttempts = ExecutionInvocationMetadata.ResolveMaxStructuredOutputRepairAttempts(run);
        while (!validation.Succeeded && repairAttemptCount < maxRepairAttempts)
        {
            repairAttemptCount++;
            await AppendExecutionLogAsync(
                run.Id,
                run.AgentId,
                run.ChatSessionId,
                ExecutionState.Persisting,
                "Output repair",
                $"Attempting structured output repair {repairAttemptCount}/{maxRepairAttempts} for contract '{structuredOutput.ContractKey}'. Raw output hash: {validation.RawOutputHash}. Errors: {FormatValidationErrors(validation.Validation.Errors)}",
                cancellationToken);

            var repair = await outputRepairService.TryRepairAsync(
                new AgentOutputRepairRequest
                {
                    ContractName = structuredOutput.ContractKey,
                    SchemaName = structuredOutput.SchemaName,
                    SchemaDescription = structuredOutput.SchemaDescription,
                    InvalidRawOutput = validation.RawOutput,
                    InvalidRawOutputHash = validation.RawOutputHash,
                    ValidationErrors = validation.Validation.Errors,
                    AttemptNumber = repairAttemptCount,
                    MaxAttempts = maxRepairAttempts
                },
                cancellationToken);
            response = AgentProviderUsageObservationAssembler.AppendRepairUsageObservations(response, repair);

            if (!repair.Succeeded || string.IsNullOrWhiteSpace(repair.RepairedRawOutput))
            {
                await AppendExecutionLogAsync(
                    run.Id,
                    run.AgentId,
                    run.ChatSessionId,
                    ExecutionState.Persisting,
                    "Output repair",
                    string.IsNullOrWhiteSpace(repair.FailureMessage)
                        ? $"Structured output repair {repairAttemptCount}/{maxRepairAttempts} did not produce a repair candidate for contract '{structuredOutput.ContractKey}'."
                        : $"Structured output repair {repairAttemptCount}/{maxRepairAttempts} failed for contract '{structuredOutput.ContractKey}': {repair.FailureMessage}",
                    cancellationToken);
                break;
            }

            validation = validator.DeserializeAndValidate(repair.RepairedRawOutput);
            if (validation.Succeeded)
            {
                response = response with
                {
                    ResponseText = repair.RepairedRawOutput
                };

                await AppendExecutionLogAsync(
                    run.Id,
                    run.AgentId,
                    run.ChatSessionId,
                    ExecutionState.Persisting,
                    "Output repair",
                    $"Structured output repair {repairAttemptCount}/{maxRepairAttempts} succeeded for contract '{structuredOutput.ContractKey}'. Repaired raw output hash: {validation.RawOutputHash}.",
                    cancellationToken);
                break;
            }

            await AppendExecutionLogAsync(
                run.Id,
                run.AgentId,
                run.ChatSessionId,
                ExecutionState.Persisting,
                "Output repair",
                $"Structured output repair {repairAttemptCount}/{maxRepairAttempts} still failed validation for contract '{structuredOutput.ContractKey}'. Repaired raw output hash: {validation.RawOutputHash}. Errors: {FormatValidationErrors(validation.Validation.Errors)}",
                cancellationToken);
        }

        Activity.Current?.SetTag("agentframework.repair_attempt_count", repairAttemptCount);
        Activity.Current?.SetTag("agentframework.repair_original_raw_hash", originalRawOutputHash);
        Activity.Current?.SetTag("agentframework.repair_final_raw_hash", validation.RawOutputHash);

        var errorSummary = FormatValidationErrors(validation.Validation.Errors);
        if (!validation.Succeeded)
        {
            await AppendExecutionLogAsync(
                run.Id,
                run.AgentId,
                run.ChatSessionId,
                ExecutionState.Failed,
                "Output validation",
                $"Structured output contract '{structuredOutput.ContractKey}' failed validation. Raw output hash: {validation.RawOutputHash}. Errors: {errorSummary}",
                cancellationToken);

            throw new AgentExecutionGovernanceException(
                AgentExecutionGovernanceFailureKind.StructuredOutputValidation,
                $"Structured output contract '{structuredOutput.ContractKey}' failed validation. Raw output hash: {validation.RawOutputHash}. Errors: {errorSummary}");
        }

        Activity.Current?.SetTag("agentframework.structured_output_contract_key", structuredOutput.ContractKey);
        Activity.Current?.SetTag("agentframework.structured_output_raw_hash", validation.RawOutputHash);

        await AppendExecutionLogAsync(
            run.Id,
            run.AgentId,
            run.ChatSessionId,
            ExecutionState.Persisting,
            "Output validation",
            $"Validated structured output contract '{structuredOutput.ContractKey}'. Raw output hash: {validation.RawOutputHash}.",
            cancellationToken);
        return response;
    }

    private async Task<AgentJsonSchemaOutputResult?> ValidatePortableJsonSchemaOutputBeforeCompletionAsync(
        ExecutionRunRecord run,
        AgentRuntimeResponse response,
        CancellationToken cancellationToken)
    {
        var contract = AgentJsonSchemaOutputContractProcessor.Restore(run);
        if (contract is null || response.PendingApprovals.Count > 0)
        {
            return null;
        }

        var result = AgentJsonSchemaOutputContractProcessor.ValidateOutput(contract, response.ResponseText);
        Activity.Current?.SetTag("agentframework.structured_output_schema_hash", contract.SchemaHash);
        Activity.Current?.SetTag(
            "agentframework.structured_output_validation_status",
            result.ValidationStatus.ToString());

        var valid = result.ValidationStatus == AgentJsonSchemaOutputValidationStatus.Valid;
        await AppendExecutionLogAsync(
            run.Id,
            run.AgentId,
            run.ChatSessionId,
            valid ? ExecutionState.Persisting : ExecutionState.Failed,
            "Output validation",
            valid
                ? $"Validated portable JSON Schema output contract '{contract.Name}' with schema hash '{contract.SchemaHash}'."
                : $"Portable JSON Schema output contract '{contract.Name}' produced status '{result.ValidationStatus}' with schema hash '{contract.SchemaHash}'. Errors: {string.Join(" ", result.ValidationErrors.Select(error => $"{error.Code} at {error.Path}: {error.Message}"))}",
            cancellationToken);

        return result;
    }

    private async Task<AgentRuntimeHandoffExecutionOptions?> ResolveHandoffExecutionOptionsAsync(
        AgentDefinition agent,
        SandboxWorkspaceCatalog catalog,
        ExecutionRunRecord run,
        CancellationToken cancellationToken)
    {
        var settings = AgentHandoffMetadata.Read(agent.ConfigurationJson);
        if (!settings.Enabled)
        {
            return null;
        }

        var validation = AgentHandoffMetadata.Validate(settings);
        if (!validation.Succeeded)
        {
            throw new InvalidOperationException("Agent handoff configuration is invalid: " + string.Join(" ", validation.Errors));
        }

        var entryAgentId = settings.EntryAgentId.GetValueOrDefault(agent.Id);
        var participantIds = AgentHandoffMetadata
            .ResolveParticipantAgentIds(settings, entryAgentId)
            .ToHashSet();
        participantIds.Add(entryAgentId);

        var participants = new List<AgentRuntimeHandoffParticipant>();
        foreach (var participantId in participantIds.OrderBy(item => item))
        {
            var participantAgent = EnsureAgentExists(catalog, participantId);
            if (participantAgent.Status is AgentLifecycleStatus.Archived or AgentLifecycleStatus.Suspended)
            {
                throw new InvalidOperationException(
                    $"Handoff participant agent '{participantAgent.Name}' is {participantAgent.Status} and cannot be used in a runtime workflow.");
            }

            var participantProvider = await ResolveProviderForAgentAsync(participantAgent, catalog, cancellationToken);
            participants.Add(new AgentRuntimeHandoffParticipant(
                participantAgent,
                participantProvider,
                ResolveAttachedCapabilities(catalog, participantAgent),
                ResolveAgentMemoryForRun(catalog, participantAgent.Id, run)));
        }

        return new AgentRuntimeHandoffExecutionOptions(
            settings,
            participants,
            entryAgentId,
            string.IsNullOrWhiteSpace(run.CorrelationId) ? run.Id.ToString("D") : run.CorrelationId);
    }

    private AgentRuntimeExecutionOptions CreateRuntimeExecutionOptionsCore(
        ExecutionRunRecord run,
        AgentExecutionOperationId? activityOperationId,
        AgentStructuredOutputContract? structuredOutput,
        AgentRuntimeHandoffExecutionOptions? handoffOptions,
        IReadOnlyList<AgentRuntimeInputAttachment>? inputAttachments,
        PreparedAgentJsonSchemaOutputContract? jsonSchemaOutput = null)
    {
        ArgumentNullException.ThrowIfNull(run);

        jsonSchemaOutput ??= AgentJsonSchemaOutputContractProcessor.Restore(run);
        var transientContext = transientContextRegistry.Resolve(run);
        return BuildRuntimeExecutionOptions(
            run,
            activityOperationId,
            structuredOutput,
            handoffOptions,
            inputAttachments,
            jsonSchemaOutput,
            transientContext,
            activityWorkspaceIdentity);
    }

    private static AgentRuntimeExecutionOptions BuildRuntimeExecutionOptions(
        ExecutionRunRecord run,
        AgentExecutionOperationId? activityOperationId,
        AgentStructuredOutputContract? structuredOutput,
        AgentRuntimeHandoffExecutionOptions? handoffOptions,
        IReadOnlyList<AgentRuntimeInputAttachment>? inputAttachments,
        PreparedAgentJsonSchemaOutputContract? jsonSchemaOutput,
        AgentRuntimeTransientContext? transientContext,
        AgentExecutionActivityWorkspaceIdentity activityWorkspaceIdentity)
    {
        ArgumentNullException.ThrowIfNull(run);
        var transientWorkspaceScope = transientContext?.WorkspaceScope;
        var recordedWorkspaceScope = ExecutionInvocationMetadata.ResolveContextWorkspaceScope(run);
        if (transientWorkspaceScope is not null &&
            recordedWorkspaceScope is not null &&
            transientWorkspaceScope != recordedWorkspaceScope)
        {
            throw new InvalidOperationException(
                $"Execution run '{run.Id:N}' has conflicting trusted workspace scopes.");
        }

        var contextWorkspaceScope = transientWorkspaceScope ?? recordedWorkspaceScope;
        var governance = ResolveValidatedExecutionGovernance(
            run,
            contextWorkspaceScope,
            activityWorkspaceIdentity);

        return new AgentRuntimeExecutionOptions(
            StructuredOutput: structuredOutput,
            FinalizerMode: AgentFinalizerPolicies.ResolveMode(run, structuredOutput),
            RequireStructuredOutputValidation: ExecutionInvocationMetadata.ResolveRequireStructuredOutputValidation(run),
            MaxStructuredOutputRepairAttempts: ExecutionInvocationMetadata.ResolveMaxStructuredOutputRepairAttempts(run),
            Handoff: handoffOptions,
            ContextWorkspaceScope: contextWorkspaceScope,
            RequireJsonResponseFormat: jsonSchemaOutput is not null,
            ResponseFormatJsonSchema: jsonSchemaOutput?.SchemaJson ?? string.Empty,
            ResponseFormatSchemaName: jsonSchemaOutput?.Name ?? string.Empty,
            ResponseFormatSchemaDescription: jsonSchemaOutput is null
                ? string.Empty
                : $"Portable JSON Schema output contract {jsonSchemaOutput.Version}.",
            ContextIntent: CreateRuntimeContextIntent(run, contextWorkspaceScope),
            InputAttachments: inputAttachments)
        {
            ActivityOperationId = activityOperationId,
            TransientContext = transientContext,
            // Separated fingerprint dimensions: the model-context digest and
            // the authority policy fingerprint are independent compatibility
            // inputs; the capability and tool-contract fingerprints are
            // stamped by the adapter after capability composition.
            ModelContextDigest = AgentTurnContextMetadata.TryReadTurnContextReference(run.MetadataJson)?.ModelContextDigest
                ?? AgentTurnContextMetadata.EmptyModelContextDigest,
            AuthorityPolicyFingerprint = governance?.PolicyFingerprint ?? string.Empty,
            Governance = governance
        };
    }

    /// <summary>
    /// Rebuilds the admitted execution governance snapshot from the run's
    /// persisted authority projection and validates it against the run facts
    /// before it becomes the runtime enforcement input. Ordinary sends and
    /// approval continuation share this single restore path, so continuation
    /// always enforces the original turn's authority instead of recaptured
    /// state. A run without a projection (detached or legacy) yields
    /// <c>null</c>; a projection that contradicts the run fails closed.
    /// </summary>
    private static AgentExecutionGovernanceSnapshot? ResolveValidatedExecutionGovernance(
        ExecutionRunRecord run,
        WorkspaceScopeDescriptor? contextWorkspaceScope,
        AgentExecutionActivityWorkspaceIdentity activityWorkspaceIdentity)
    {
        ArgumentNullException.ThrowIfNull(activityWorkspaceIdentity);
        var readResult = AgentTurnContextMetadata.ReadExecutionGovernanceSnapshot(run.MetadataJson);
        if (readResult.State == AgentExecutionGovernanceReadState.Malformed)
        {
            throw new AgentExecutionAuthorityMismatchException(
                $"Execution run '{run.Id:N}' carries a malformed authority projection.");
        }

        if (readResult.State == AgentExecutionGovernanceReadState.Absent)
        {
            if (AgentTurnContextMetadata.ContainsTurnContextReference(run.MetadataJson) ||
                ExecutionInvocationMetadata.RequiresTransientContext(run))
            {
                throw new AgentExecutionAuthorityMismatchException(
                    $"Execution run '{run.Id:N}' proves context admission but carries no authority projection.");
            }

            return null;
        }

        var governance = readResult.Snapshot
            ?? throw new InvalidOperationException("A valid authority projection did not produce a governance snapshot.");
        if (governance.AgentId != run.AgentId)
        {
            throw new AgentExecutionAuthorityMismatchException(
                $"Execution run '{run.Id:N}' carries an authority projection for a different agent.");
        }

        if (governance.DatabaseProfileId != activityWorkspaceIdentity.DatabaseProfileId)
        {
            throw new AgentExecutionAuthorityMismatchException(
                $"Execution run '{run.Id:N}' carries an authority projection for a different database profile.");
        }

        if (governance.DatabaseProfileGeneration != activityWorkspaceIdentity.DatabaseProfileGeneration)
        {
            throw new AgentExecutionAuthorityMismatchException(
                $"Execution run '{run.Id:N}' carries an authority projection from a different database profile generation.");
        }

        if (contextWorkspaceScope is not null &&
            governance.WorkspaceScope != contextWorkspaceScope)
        {
            throw new AgentExecutionAuthorityMismatchException(
                $"Execution run '{run.Id:N}' workspace scope '{contextWorkspaceScope.DisplayName}' does not match the admitted authority scope '{governance.WorkspaceScope.DisplayName}'.");
        }

        if (contextWorkspaceScope is null && !governance.WorkspaceScope.IsDefaultSandbox)
        {
            throw new AgentExecutionAuthorityMismatchException(
                $"Execution run '{run.Id:N}' has no trusted workspace scope, but the admitted authority claims scope '{governance.WorkspaceScope.DisplayName}'.");
        }

        if (string.Equals(governance.PolicyVersion, "unknown", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(governance.PolicyFingerprint, "unknown", StringComparison.OrdinalIgnoreCase))
        {
            throw new AgentExecutionAuthorityMismatchException(
                $"Execution run '{run.Id:N}' carries an unrecognized authority policy identity.");
        }

        return governance;
    }

    private static ExecutionInvocationContext PrepareInvocationContext(
        ExecutionRunRequest request)
    {
        var context = request.Context ?? ExecutionInvocationContext.Empty;
        if (request.TransientContext is null)
        {
            return context;
        }

        var digest = AgentChatContextDigest.Compute(request.TransientContext);
        return context with
        {
            MetadataJson = ExecutionInvocationMetadata.ApplyTransientContextRequirement(
                context.MetadataJson,
                digest)
        };
    }

    private async Task<AgentExecutionPreparationSnapshot>
        AcquireExecutionPreparationAsync(
            IAgentExecutionActivityOperationLease activityOperation,
            Guid agentId,
            bool atomicConsumer,
            CancellationToken cancellationToken)
    {
        activityOperation.Report(
            AgentExecutionActivityPhase.ResolvingPreparation,
            "Checking prepared agent and provider configuration.");
        var preparation = atomicConsumer
            ? await executionPreparationService
                .AcquireForAtomicConsumerAsync(agentId, cancellationToken)
                .ConfigureAwait(false)
            : await executionPreparationService
                .AcquireAsync(agentId, cancellationToken)
                .ConfigureAwait(false);
        var action = preparation.Source ==
                     AgentExecutionPreparationSource.Reused
            ? "Reused"
            : "Refreshed";
        activityOperation.Report(
            AgentExecutionActivityPhase.ResolvingPreparation,
            $"{action} agent preparation revision {preparation.Blueprint.Request.Version.CatalogRevision.Value} in {Math.Max(0, (long)preparation.Elapsed.TotalMilliseconds)} ms.");
        return preparation;
    }

    private async Task<ProviderProfile> ResolvePreparedProviderAsync(
        IAgentExecutionActivityOperationLease activityOperation,
        AgentExecutionPreparationSnapshot preparation,
        ExecutionRunRequest request,
        CancellationToken cancellationToken)
    {
        activityOperation.Report(
            AgentExecutionActivityPhase.ResolvingProvider,
            "Applying the current provider and execution policy.");
        var configuredProvider = ApplyCredentialAwareManagedSeedFallback(
            preparation.Blueprint.Agent,
            preparation.Blueprint.Provider);
        return await ResolveProviderForExecutionRequestAsync(
                preparation.Blueprint.Agent,
                request,
                configuredProvider,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<ProviderProfile> ResolveProviderForExecutionRequestAsync(
        AgentDefinition agent,
        ExecutionRunRequest request,
        ProviderProfile configuredProvider,
        CancellationToken cancellationToken)
    {
        var selectionRequest = CreateProviderSelectionRequest(request, configuredProvider);
        var overridingPolicy = providerSelectionPolicies
            .FirstOrDefault(policy => policy.ShouldOverrideConfiguredProvider(selectionRequest));
        if (overridingPolicy is null)
        {
            return configuredProvider;
        }

        var availableProviders = await providerSource
            .ListProvidersAsync(cancellationToken)
            .ConfigureAwait(false);
        var candidates = overridingPolicy.SelectOverrideCandidates(selectionRequest, availableProviders);
        var selected = candidates.FirstOrDefault();
        return selected is null
            ? configuredProvider
            : ApplyCredentialAwareManagedSeedFallback(agent, selected);
    }

    private AgentExecutionProviderSelectionRequest CreateProviderSelectionRequest(
        ExecutionRunRequest request,
        ProviderProfile configuredProvider)
    {
        var sourceKind = request.Context?.SourceKind ?? string.Empty;
        var criticalitySnapshot = new AgentExecutionRunCriticalitySnapshot(
            sourceKind,
            request.Context?.ProcessRunId,
            request.Context?.ProcessStepId);
        return new AgentExecutionProviderSelectionRequest(
            sourceKind,
            runCriticalityPolicies.Any(policy => policy.IsMachineCritical(criticalitySnapshot)),
            request.StructuredOutput?.OutputType,
            request.StructuredOutput?.ContractKey,
            configuredProvider);
    }

    internal static ProviderProfile ResolveContinuationProvider(
        ExecutionRunRecord run,
        ProviderProfile configuredProvider,
        IReadOnlyList<ProviderProfile> catalogProviders)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(configuredProvider);
        ArgumentNullException.ThrowIfNull(catalogProviders);

        if (run.ProviderProfileId == configuredProvider.Id ||
            (!run.ProviderProfileId.HasValue &&
             (string.IsNullOrWhiteSpace(run.ProviderName) ||
              string.Equals(run.ProviderName, configuredProvider.Name, StringComparison.OrdinalIgnoreCase))))
        {
            EnsureContinuationProviderEnabled(run, configuredProvider);
            return configuredProvider;
        }

        var matches = run.ProviderProfileId.HasValue
            ? catalogProviders.Where(provider => provider.Id == run.ProviderProfileId.Value).ToArray()
            : catalogProviders
                .Where(provider => string.Equals(provider.Name, run.ProviderName, StringComparison.OrdinalIgnoreCase))
                .ToArray();
        if (matches.Length != 1)
        {
            var identity = run.ProviderProfileId.HasValue
                ? $"ID '{run.ProviderProfileId.Value:N}'"
                : $"name '{run.ProviderName}'";
            throw new InvalidOperationException(
                $"The provider recorded for execution run '{run.Id:N}' could not be resolved uniquely by {identity}.");
        }

        var provider = matches[0];
        EnsureContinuationProviderEnabled(run, provider);
        return provider;
    }

    private async Task<ProviderProfile> ResolveContinuationProviderLeaseAsync(
        ExecutionRunRecord run,
        AgentExecutionPreparationSnapshot preparation,
        CancellationToken cancellationToken)
    {
        var agent = preparation.Blueprint.Agent;
        var configuredProvider = ApplyCredentialAwareManagedSeedFallback(
            agent,
            preparation.Blueprint.Provider);
        if (RunUsesProvider(run, configuredProvider))
        {
            EnsureContinuationProviderEnabled(run, configuredProvider);
            return configuredProvider;
        }

        IReadOnlyList<ProviderProfile> candidates;
        if (run.ProviderProfileId is Guid providerId)
        {
            var provider = await providerSource
                .GetProviderAsync(providerId, cancellationToken)
                .ConfigureAwait(false);
            candidates = provider is null
                ? []
                : [provider];
        }
        else
        {
            candidates = await providerSource
                .ListProvidersAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        var selected = ResolveContinuationProvider(
            run,
            configuredProvider,
            candidates);
        return ApplyCredentialAwareManagedSeedFallback(agent, selected);
    }

    private static bool RunUsesProvider(
        ExecutionRunRecord run,
        ProviderProfile provider)
    {
        return run.ProviderProfileId == provider.Id ||
               (!run.ProviderProfileId.HasValue &&
                (string.IsNullOrWhiteSpace(run.ProviderName) ||
                 string.Equals(
                     run.ProviderName,
                     provider.Name,
                     StringComparison.OrdinalIgnoreCase)));
    }

    private static void EnsureContinuationProviderEnabled(
        ExecutionRunRecord run,
        ProviderProfile provider)
    {
        if (!provider.IsEnabled)
        {
            throw new InvalidOperationException(
                $"Provider '{provider.Name}' was disabled while execution run '{run.Id:N}' was waiting for approval.");
        }
    }

    private static void EnsureContinuationProviderLeaseMatches(
        ExecutionRunRecord run,
        ProviderProfile provider)
    {
        if (!RunUsesProvider(run, provider))
        {
            throw new InvalidOperationException(
                $"The canonical provider lease does not match the provider recorded for execution run '{run.Id:N}'.");
        }

        EnsureContinuationProviderEnabled(run, provider);
    }

    private static AgentDefinition CreateProviderCompatibleRuntimeAgent(
        AgentDefinition agent,
        ProviderProfile provider,
        string resolvedModel)
    {
        var model = ResolveProviderCompatibleRuntimeModel(agent, provider, resolvedModel);
        if (agent.ProviderProfileId == provider.Id &&
            string.Equals(agent.Model, model, StringComparison.Ordinal))
        {
            return agent;
        }

        return agent with
        {
            ProviderProfileId = provider.Id,
            Model = model
        };
    }

    internal static string ResolveProviderCompatibleRuntimeModel(
        AgentDefinition agent,
        ProviderProfile provider,
        string? resolvedModel = null)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(provider);

        var candidate = string.IsNullOrWhiteSpace(resolvedModel)
            ? agent.Model?.Trim() ?? string.Empty
            : resolvedModel.Trim();
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return provider.DefaultModel;
        }

        if (agent.ProviderProfileId == provider.Id ||
            IsProviderSupportedModel(provider, candidate))
        {
            return candidate;
        }

        return provider.DefaultModel;
    }

    private static bool IsProviderSupportedModel(
        ProviderProfile provider,
        string model)
    {
        return !string.IsNullOrWhiteSpace(model) &&
               (string.Equals(provider.DefaultModel, model, StringComparison.OrdinalIgnoreCase) ||
                provider.SuggestedModels.Contains(model, StringComparer.OrdinalIgnoreCase));
    }

    private async Task<IReadOnlyList<AgentRuntimeInputAttachment>> ResolveRuntimeInputAttachmentsAsync(
        IReadOnlyList<string>? attachmentPaths,
        CancellationToken cancellationToken)
    {
        var requestedPaths = attachmentPaths?
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path.Trim())
            .Distinct(ExternalTargetAliasCodec.EqualityComparer)
            .ToList()
            ?? [];
        if (requestedPaths.Count == 0)
        {
            return [];
        }

        if (workspacePathResolutionService is null)
        {
            throw new InvalidOperationException("Workspace attachment resolution is not available for this agent workspace.");
        }

        var attachments = new List<AgentRuntimeInputAttachment>();
        foreach (var requestedPath in requestedPaths)
        {
            if (!TryResolveImageContentType(requestedPath, out var contentType))
            {
                throw new InvalidOperationException(
                    $"Attachment '{requestedPath}' is not a supported PNG, JPEG, GIF, or WebP image.");
            }

            if (attachments.Count >=
                AgentRuntimeInputAttachmentPolicy.MaximumImageCount)
            {
                throw new InvalidOperationException(
                    $"A single agent request can include at most {AgentRuntimeInputAttachmentPolicy.MaximumImageCount:N0} image attachment(s).");
            }

            var resolved = workspacePathResolutionService.ResolveFilePath(requestedPath, allowMissing: false);
            attachments.Add(new AgentRuntimeInputAttachment(
                Name: Path.GetFileName(resolved.FullPath),
                ContentType: contentType,
                Bytes: await AgentRuntimeInputAttachmentPolicy.ReadFileAsync(
                        resolved.FullPath,
                        resolved.RelativePath,
                        cancellationToken)
                    .ConfigureAwait(false),
                SourcePath: resolved.RelativePath));
        }

        return attachments.AsReadOnly();
    }

    private static bool TryResolveImageContentType(
        string path,
        out string contentType)
    {
        contentType = Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            _ => string.Empty
        };
        return !string.IsNullOrWhiteSpace(contentType);
    }

    private static AgentRuntimeContextIntent CreateRuntimeContextIntent(
        ExecutionRunRecord run,
        WorkspaceScopeDescriptor? workspaceScope)
    {
        ArgumentNullException.ThrowIfNull(run);

        var isGovernedProcessStep = string.Equals(run.SourceKind, "process-step", StringComparison.OrdinalIgnoreCase) &&
                                    string.Equals(run.RequestedByKind, "system", StringComparison.OrdinalIgnoreCase) &&
                                    !string.IsNullOrWhiteSpace(run.ProcessRunId) &&
                                    !string.IsNullOrWhiteSpace(run.ProcessStepId);
        return new AgentRuntimeContextIntent(
            SourceKind: run.SourceKind,
            SourceId: run.SourceId,
            ProcessRunId: run.ProcessRunId,
            ProcessStepId: run.ProcessStepId,
            TargetScope: ExecutionInvocationMetadata.ResolveProcessStepTargetScope(run),
            IsGovernedProcessStep: isGovernedProcessStep,
            BrowserToolsAllowed: ExecutionInvocationMetadata.ResolveProcessBrowserToolsAllowed(run),
            AllowsProductMutation: ExecutionInvocationMetadata.ResolveProcessAllowsProductMutation(run),
            WorkspaceToolProfile: ExecutionInvocationMetadata.ResolveProcessWorkspaceToolProfile(run),
            WorkspaceScope: workspaceScope,
            AllowedOperations: ExecutionInvocationMetadata.ResolveProcessStepAllowedOperations(run),
            RuntimeToolProvidersEnabled: ExecutionInvocationMetadata.ResolveRuntimeToolProvidersEnabled(run),
            WorkspaceToolsEnabled: ExecutionInvocationMetadata.ResolveWorkspaceToolsEnabled(run),
            CapabilityScopeOverride: ExecutionInvocationMetadata.ResolveRuntimeCapabilityScopeOverride(run))
        {
            ToolCapabilitiesEnabled = ExecutionInvocationMetadata.ResolveToolCapabilitiesEnabled(run),
            Purpose = ResolveRuntimeContextPurpose(run, isGovernedProcessStep)
        };
    }

    private static AgentRuntimeContextPurpose ResolveRuntimeContextPurpose(
        ExecutionRunRecord run,
        bool isGovernedProcessStep)
    {
        if (isGovernedProcessStep)
        {
            return AgentRuntimeContextPurpose.GovernedProcessAutomation;
        }

        if (run.ChatSessionId.HasValue)
        {
            return AgentRuntimeContextPurpose.InteractiveChat;
        }

        return run.AutoApprovePendingToolCalls
            ? AgentRuntimeContextPurpose.AutoApprovedNonInteractive
            : AgentRuntimeContextPurpose.Unspecified;
    }

    private async Task AppendProcessCooperationLogAsync(
        ExecutionRunRecord run,
        Guid agentId,
        Guid? chatSessionId,
        CancellationToken cancellationToken)
    {
        var cooperationMode = ExecutionInvocationMetadata.ResolveProcessCooperationMode(run);
        var workspaceToolProfile = ExecutionInvocationMetadata.ResolveProcessWorkspaceToolProfile(run);
        if (cooperationMode is null && workspaceToolProfile is null)
        {
            return;
        }

        var summary = ExecutionInvocationMetadata.ResolveProcessCooperationSummary(run);
        var profileLabel = workspaceToolProfile.HasValue
            ? AgentWorkspaceToolAccessProfiles.GetProfileKey(workspaceToolProfile.Value)
            : "agent-configured";
        var message = string.IsNullOrWhiteSpace(summary)
            ? $"Process dispatch selected cooperation mode '{cooperationMode?.ToString() ?? "unspecified"}' with workspace tool profile '{profileLabel}'."
            : $"{summary} Workspace tool profile: {profileLabel}.";

        await AppendExecutionLogAsync(
            run.Id,
            agentId,
            chatSessionId,
            ExecutionState.Preparing,
            "Process cooperation",
            message,
            cancellationToken);
    }

    private async Task<AgentRuntimeResponse> ValidateFinalizerBeforeCompletionAsync(
        ExecutionRunRecord run,
        AgentStructuredOutputContract structuredOutput,
        AgentRuntimeResponse response,
        IAgentOutputValidatorRegistry registry,
        IAgentOutputContractValidator structuredOutputValidator,
        CancellationToken cancellationToken)
    {
        var finalizerMode = AgentFinalizerPolicies.ResolveMode(run, structuredOutput);
        if (finalizerMode == AgentFinalizerMode.Disabled ||
            !AgentFinalizerPolicies.TryResolveForStructuredOutput(structuredOutput, out var policy))
        {
            return response;
        }

        using var activity = AgentFrameworkTelemetry.ActivitySource.StartActivity("agent.finalizer.validate", ActivityKind.Internal);
        AgentFrameworkTelemetry.ApplyRunTags(activity, run);
        activity?.SetTag("agentframework.finalizer_mode", finalizerMode.ToString());
        activity?.SetTag("agentframework.finalizer_tool_name", policy.ToolName);
        activity?.SetTag("agentframework.structured_output_contract_key", structuredOutput.ContractKey);
        activity?.SetTag("agentframework.finalizer_invocation_count", response.FinalizerInvocations.Count);

        if (finalizerMode == AgentFinalizerMode.Shadow &&
            !response.FinalizerInvocations.Any(invocation => string.Equals(invocation.ToolName, policy.ToolName, StringComparison.OrdinalIgnoreCase)))
        {
            activity?.SetTag("agentframework.finalizer_status", "not_observed");
            await AppendExecutionLogAsync(
                run.Id,
                run.AgentId,
                run.ChatSessionId,
                ExecutionState.Persisting,
                "Finalizer validation",
                $"Shadow finalizer tool '{policy.ToolName}' was not observed for structured output contract '{structuredOutput.ContractKey}'. Structured output remains the source of truth.",
                cancellationToken);
            return response;
        }

        var effectiveFinalizerInvocations = finalizerMode == AgentFinalizerMode.Required
            ? AgentFinalizerInvocationNormalizer.NormalizeRequired(policy, response.FinalizerInvocations, registry)
            : response.FinalizerInvocations;
        activity?.SetTag("agentframework.effective_finalizer_invocation_count", effectiveFinalizerInvocations.Count);
        var effectiveResponse = ReferenceEquals(effectiveFinalizerInvocations, response.FinalizerInvocations)
            ? response
            : response with
            {
                FinalizerInvocations = effectiveFinalizerInvocations
            };
        if (finalizerMode == AgentFinalizerMode.Required &&
            effectiveFinalizerInvocations.Count != response.FinalizerInvocations.Count)
        {
            await AppendExecutionLogAsync(
                run.Id,
                run.AgentId,
                run.ChatSessionId,
                ExecutionState.Persisting,
                "Finalizer validation",
                $"Required finalizer tool '{policy.ToolName}' was observed {response.FinalizerInvocations.Count} times; using the last valid invocation for completion validation.",
                cancellationToken);
        }

        var validator = new DefaultAgentFinalizerValidator(registry);
        var result = validator.Validate(policy, effectiveFinalizerInvocations);
        activity?.SetTag("agentframework.finalizer_matching_invocation_count", result.MatchingInvocationCount);
        activity?.SetTag("agentframework.finalizer_raw_hash", result.RawOutputHash);
        activity?.SetTag("agentframework.finalizer_status", result.Succeeded ? "valid" : "invalid");

        if (!result.Succeeded)
        {
            var errorSummary = FormatValidationErrors(result.Errors);
            var message =
                $"Finalizer tool '{policy.ToolName}' in {finalizerMode} mode failed validation. Raw output hash: {result.RawOutputHash}. Errors: {errorSummary}";
            if (RequiredFinalizerStructuredOutputRecoveryPolicy.CanRecover(
                    ExecutionInvocationMetadata.ResolveAllowRequiredFinalizerStructuredOutputRecovery(run),
                    finalizerMode,
                    result))
            {
                await AppendExecutionLogAsync(
                    run.Id,
                    run.AgentId,
                    run.ChatSessionId,
                    ExecutionState.Persisting,
                    "Finalizer recovery",
                    $"Required finalizer tool '{policy.ToolName}' was not called. Explicit structured-output recovery is enabled; the raw response will be validated and, when needed, repaired against '{structuredOutput.ContractKey}'. Completion evidence gates remain authoritative.",
                    cancellationToken);
                return response;
            }

            await AppendExecutionLogAsync(
                run.Id,
                run.AgentId,
                run.ChatSessionId,
                finalizerMode == AgentFinalizerMode.Required ? ExecutionState.Failed : ExecutionState.Persisting,
                "Finalizer validation",
                message,
                cancellationToken);

            if (finalizerMode == AgentFinalizerMode.Required)
            {
                activity?.SetStatus(ActivityStatusCode.Error, message);
                throw new AgentExecutionGovernanceException(
                    AgentExecutionGovernanceFailureKind.FinalizerValidation,
                    message);
            }

            return response;
        }

        await ValidateFinalizerSequenceBeforeCompletionAsync(
            run,
            finalizerMode,
            policy,
            effectiveResponse,
            cancellationToken);

        var finalizerOutputJson = SerializeMachineOutput(result.Output, policy.OutputType);
        if (finalizerMode == AgentFinalizerMode.Required)
        {
            await AppendExecutionLogAsync(
                run.Id,
                run.AgentId,
                run.ChatSessionId,
                ExecutionState.Persisting,
                "Finalizer validation",
                $"Required finalizer tool '{policy.ToolName}' produced a valid '{structuredOutput.ContractKey}' result. Raw output hash: {result.RawOutputHash}.",
                cancellationToken);

            return effectiveResponse with
            {
                ResponseText = finalizerOutputJson
            };
        }

        var structuredValidation = structuredOutputValidator.DeserializeAndValidate(effectiveResponse.ResponseText);
        var finalizerMatchesStructuredOutput = structuredValidation.Succeeded &&
                                               string.Equals(
                                                   SerializeMachineOutput(structuredValidation.Output, structuredValidation.OutputType),
                                                   finalizerOutputJson,
                                                   StringComparison.Ordinal);
        activity?.SetTag("agentframework.finalizer_matches_structured_output", finalizerMatchesStructuredOutput);

        await AppendExecutionLogAsync(
            run.Id,
            run.AgentId,
            run.ChatSessionId,
            ExecutionState.Persisting,
            "Finalizer validation",
            finalizerMatchesStructuredOutput
                ? $"Shadow finalizer tool '{policy.ToolName}' matched structured output contract '{structuredOutput.ContractKey}'. Raw output hash: {result.RawOutputHash}."
                : $"Shadow finalizer tool '{policy.ToolName}' produced a valid result that differs from the structured output response. Raw output hash: {result.RawOutputHash}. Structured output remains the source of truth.",
            cancellationToken);

        return response;
    }

    private async Task ValidateFinalizerSequenceBeforeCompletionAsync(
        ExecutionRunRecord run,
        AgentFinalizerMode finalizerMode,
        AgentFinalizerPolicy policy,
        AgentRuntimeResponse response,
        CancellationToken cancellationToken)
    {
        if (finalizerMode != AgentFinalizerMode.Required)
        {
            return;
        }

        var governedRun = IsGovernedMachineCriticalRun(run);
        var sequenceValidation = AgentFinalizerSequenceValidator.Validate(policy, response.ToolInvocationTraces);
        Activity.Current?.SetTag("agentframework.finalizer_trace_available", sequenceValidation.TraceAvailable);
        Activity.Current?.SetTag("agentframework.finalizer_sequence", sequenceValidation.FinalizerSequence);
        Activity.Current?.SetTag("agentframework.post_finalizer_significant_tool_count", sequenceValidation.ViolatingToolInvocations.Count);

        if (!sequenceValidation.TraceAvailable)
        {
            var message =
                $"Required finalizer tool '{policy.ToolName}' sequencing was not verifiable because the runtime did not report ordered tool invocation traces.";
            await AppendExecutionLogAsync(
                run.Id,
                run.AgentId,
                run.ChatSessionId,
                governedRun ? ExecutionState.Failed : ExecutionState.Persisting,
                "Finalizer sequencing",
                message,
                cancellationToken);

            if (governedRun)
            {
                throw new AgentExecutionGovernanceException(
                    AgentExecutionGovernanceFailureKind.FinalizerSequenceValidation,
                    message);
            }

            return;
        }

        if (sequenceValidation.Succeeded)
        {
            return;
        }

        var errorSummary = FormatValidationErrors(sequenceValidation.Errors);
        var sequenceSummary = string.Join(
            ", ",
            sequenceValidation.ViolatingToolInvocations.Select(trace => $"{trace.ToolName}#{trace.Sequence}"));
        var validationMessage =
            $"Required finalizer tool '{policy.ToolName}' must be the last significant tool invocation. Finalizer sequence: {sequenceValidation.FinalizerSequence}. Later significant tools: {sequenceSummary}. Errors: {errorSummary}";
        await AppendExecutionLogAsync(
            run.Id,
            run.AgentId,
            run.ChatSessionId,
            governedRun ? ExecutionState.Failed : ExecutionState.Persisting,
            "Finalizer sequencing",
            validationMessage,
            cancellationToken);

        if (governedRun)
        {
            throw new AgentExecutionGovernanceException(
                AgentExecutionGovernanceFailureKind.FinalizerSequenceValidation,
                validationMessage);
        }
    }

    private static string FormatValidationErrors(IReadOnlyList<AgentOutputValidationError> errors)
    {
        return errors.Count == 0
            ? "none"
            : string.Join("; ", errors.Select(error => $"{error.Code}: {error.Message}"));
    }

    private static string SerializeMachineOutput(object? output, Type outputType)
    {
        if (output is null)
        {
            return "null";
        }

        return JsonSerializer.Serialize(output, outputType, AgentOutputJson.SerializerOptions);
    }

    private bool IsGovernedMachineCriticalRun(ExecutionRunRecord run)
    {
        ArgumentNullException.ThrowIfNull(run);
        var snapshot = new AgentExecutionRunCriticalitySnapshot(run.SourceKind, run.ProcessRunId, run.ProcessStepId);
        return runCriticalityPolicies.Any(policy => policy.IsMachineCritical(snapshot));
    }

    private async Task<Guid> ResolveOrCreatePendingExecutionRunIdAsync(
        Guid agentId,
        Guid chatSessionId,
        CancellationToken cancellationToken)
    {
        if (store is ISandboxWorkspaceExecutionRunStore executionRunStore &&
            store is ISandboxWorkspaceChatQueryStore chatQueryStore)
        {
            var catalog = await store.LoadCatalogAsync(cancellationToken);
            EnsureAgentExists(catalog, agentId);
            var splitSession = EnsureAgentOwnsSession(
                await chatQueryStore.GetChatSessionAsync(chatSessionId, cancellationToken),
                agentId,
                chatSessionId);

            ExecutionRunRecord? latestRun = null;
            if (splitSession.LatestExecutionRunId.HasValue)
            {
                latestRun = await executionRunStore.GetExecutionRunAsync(
                    splitSession.LatestExecutionRunId.Value,
                    cancellationToken);
                if (IsPendingApprovalRun(latestRun, agentId, chatSessionId))
                {
                    return latestRun!.Id;
                }
            }

            var summaries = await chatQueryStore.ListChatRunSummariesAsync(agentId, chatSessionId, cancellationToken);
            foreach (var summary in summaries)
            {
                if (summary.ExecutionRunId == latestRun?.Id || summary.State != ExecutionState.WaitingOnTool)
                {
                    continue;
                }

                var candidate = await executionRunStore.GetExecutionRunAsync(summary.ExecutionRunId, cancellationToken);
                if (IsPendingApprovalRun(candidate, agentId, chatSessionId))
                {
                    return candidate!.Id;
                }
            }

            if (latestRun is not null &&
                latestRun.AgentId == agentId &&
                latestRun.ChatSessionId == chatSessionId)
            {
                return latestRun.Id;
            }

            throw new InvalidOperationException("This session does not have any pending approvals.");
        }

        var document = await store.LoadAsync(cancellationToken);
        EnsureAgentExists(document.ToCatalog(), agentId);
        var executionState = document.ToExecutionState();
        var session = EnsureAgentOwnsSession(executionState, agentId, chatSessionId);

        if (session.LatestExecutionRunId.HasValue)
        {
            var latestRun = executionState.ExecutionRuns.FirstOrDefault(item =>
                item.Id == session.LatestExecutionRunId.Value
                && item.AgentId == agentId
                && item.ChatSessionId == chatSessionId);
            if (latestRun is not null && latestRun.PendingApprovals.Count > 0)
            {
                return latestRun.Id;
            }
        }

        var pendingRun = executionState.ExecutionRuns
            .Where(item => item.AgentId == agentId && item.ChatSessionId == chatSessionId && item.PendingApprovals.Count > 0)
            .OrderByDescending(item => item.UpdatedAtUtc)
            .FirstOrDefault();
        if (pendingRun is not null)
        {
            return pendingRun.Id;
        }

        if (session.LatestExecutionRunId.HasValue)
        {
            var latestRun = executionState.ExecutionRuns.FirstOrDefault(item =>
                item.Id == session.LatestExecutionRunId.Value
                && item.AgentId == agentId
                && item.ChatSessionId == chatSessionId);
            if (latestRun is not null)
            {
                return latestRun.Id;
            }
        }

        throw new InvalidOperationException("This session does not have any pending approvals.");
    }

    private static bool IsPendingApprovalRun(
        ExecutionRunRecord? run,
        Guid agentId,
        Guid chatSessionId)
        => run is not null &&
           run.AgentId == agentId &&
           run.ChatSessionId == chatSessionId &&
           run.PendingApprovals.Count > 0;

    private string ResolveModel(AgentDefinition agent, ProviderProfile provider)
    {
        return ResolveEffectiveManagedSeedModel(agent, provider);
    }

    private static string CreateExecutionSummary(string value)
    {
        var cleaned = value.Trim().ReplaceLineEndings(" ");
        if (IsMachineReadableExecutionSummary(cleaned))
        {
            return cleaned;
        }

        return cleaned.Length <= 160
            ? cleaned
            : $"{cleaned[..157]}...";
    }

    private static bool IsMachineReadableExecutionSummary(string value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value[0] != '{')
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(value);
            return document.RootElement.ValueKind == JsonValueKind.Object &&
                   document.RootElement.TryGetProperty("status", out var status) &&
                   status.ValueKind == JsonValueKind.String;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string CreateExecutionSummary(
        ExecutionRunRecord run,
        AgentRuntimeResponse response)
    {
        if (IsProcessStepOutcomeContract(run) &&
            TryCreateProcessStepOutcomeExecutionSummary(response.ResponseText, out var summary))
        {
            return summary;
        }

        return CreateExecutionSummary(response.ResponseText);
    }

    private static bool IsProcessStepOutcomeContract(ExecutionRunRecord run)
    {
        return string.Equals(
                   run.StructuredOutputContractKey,
                   AgentStructuredOutputContracts.ProcessStepOutcomeResultKey,
                   StringComparison.OrdinalIgnoreCase) ||
               string.Equals(
                   run.StructuredOutputTypeName,
                   nameof(ProcessStepOutcomeResult),
                   StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryCreateProcessStepOutcomeExecutionSummary(
        string responseText,
        out string summary)
    {
        summary = string.Empty;
        var validation = AgentOutputJson.DeserializeAndValidate(
            responseText,
            new ProcessStepOutcomeValidator());
        if (!validation.Succeeded || validation.Output is not ProcessStepOutcomeResult output)
        {
            return false;
        }

        summary = JsonSerializer.Serialize(output, AgentOutputJson.SerializerOptions);
        return true;
    }

    private static void EnsureExecutionRunExists(
        SandboxWorkspaceExecutionState executionState,
        Guid executionRunId)
    {
        if (!executionState.ExecutionRuns.Any(item => item.Id == executionRunId))
        {
            throw new InvalidOperationException("Execution run was not found.");
        }
    }

    private static long NextRunRevision(long revision)
        => revision <= 0 ? 1L : revision + 1L;
}
