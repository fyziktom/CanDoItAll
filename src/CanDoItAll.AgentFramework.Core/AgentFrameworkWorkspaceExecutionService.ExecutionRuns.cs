using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

internal sealed partial class AgentFrameworkWorkspaceExecutionService
{
    public async Task<ExecutionRunResult> ExecuteRunAsync(
        ExecutionRunRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            throw new InvalidOperationException("Prompt is required.");
        }

        var document = await store.LoadAsync(cancellationToken);
        var catalog = document.ToCatalog();
        var executionState = document.ToExecutionState();
        var agent = EnsureAgentExists(catalog, request.AgentId);
        var provider = await ResolveProviderForAgentAsync(agent, catalog, cancellationToken);
        var session = request.ChatSessionId.HasValue
            ? EnsureAgentOwnsSession(executionState, request.AgentId, request.ChatSessionId.Value)
            : null;

        return await ExecuteRunCoreAsync(
            agent,
            provider,
            catalog,
            executionState,
            session,
            request,
            persistTranscript: request.ChatSessionId.HasValue,
            cancellationToken);
    }

    public async Task<ExecutionRunResult> ContinueExecutionRunAsync(
        Guid executionRunId,
        bool approved,
        bool autoApprovePendingToolCalls = false,
        CancellationToken cancellationToken = default)
    {
        var currentRun = await LoadExecutionRunAsync(executionRunId, cancellationToken);
        if (currentRun.PendingApprovals.Count == 0)
        {
            if (currentRun.State is ExecutionState.Completed or ExecutionState.Failed)
            {
                return await LoadExistingExecutionRunResultAsync(executionRunId, cancellationToken);
            }

            throw new InvalidOperationException("This execution run is already being continued.");
        }

        var restoredCheckpoint = await executionCheckpointBridge.ValidatePendingApprovalResumeAsync(currentRun, cancellationToken);
        var continuationStart = await BeginPendingApprovalContinuationAsync(
            currentRun,
            approved,
            autoApprovePendingToolCalls,
            cancellationToken);

        if (continuationStart.Disposition == ExecutionRunContinuationDisposition.AlreadyFinalized)
        {
            return await LoadExistingExecutionRunResultAsync(executionRunId, cancellationToken);
        }

        if (continuationStart.Disposition == ExecutionRunContinuationDisposition.AlreadyInProgress)
        {
            throw new InvalidOperationException("This execution run is already being continued.");
        }

        var prepared = continuationStart.Prepared
            ?? throw new InvalidOperationException("Execution run continuation start did not return a prepared state.");
        var catalog = prepared.Catalog;
        var run = prepared.TransitionedRun;
        var session = prepared.Session;
        var agent = prepared.Agent;
        var provider = await ResolveProviderForAgentAsync(agent, catalog, cancellationToken);
        var attachedCapabilities = ResolveAttachedCapabilities(catalog, agent);
        var memory = ResolveAgentMemory(catalog, agent.Id);
        using var runActivity = AgentFrameworkTelemetry.StartRunActivity("agent.run.resume", prepared.OriginalRun);
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

        if (approved && autoApprovePendingToolCalls && !prepared.OriginalRun.AutoApprovePendingToolCalls)
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
            await executionGovernanceBridge.OnApprovalsDecidedAsync(run, prepared.DecidedApprovals, cancellationToken);
        }

        await AppendExecutionLogAsync(
            run.Id,
            agent.Id,
            run.ChatSessionId,
            ExecutionState.WaitingOnTool,
            approved ? "Approval granted" : "Approval rejected",
            approved
                ? "Continuing the execution run after approving the pending tool request."
                : "Continuing the execution run after rejecting the pending tool request.",
            cancellationToken);

        var startedAt = DateTimeOffset.UtcNow;
        try
        {
            var runtimeSession = ChatSessionRuntimeCompatibilityAdapter.CreateRuntimeSession(run, agent.Id, session);
            AgentRuntimeResponse runtimeResponse;
            using (WorkspaceExecutionAuditContext.BeginScope(run))
            {
                runtimeResponse = await runtime.RespondToPendingApprovalsAsync(
                    agent,
                    provider,
                    runtimeSession,
                    attachedCapabilities,
                    memory,
                    approved,
                    string.IsNullOrWhiteSpace(run.RuntimeSessionKey) ? null : run.RuntimeSessionKey,
                    (state, phase, message) => AppendExecutionLogAsync(run.Id, agent.Id, run.ChatSessionId, state, phase, message, cancellationToken),
                    cancellationToken,
                    suppressApprovalRequirements: approved && ShouldAutoApprovePendingToolCalls(agent, runtimeSession));

                var totalInputTokens = runtimeResponse.InputTokens;
                var totalOutputTokens = runtimeResponse.OutputTokens;
                var totalToolCalls = runtimeResponse.ToolCalls;

                if (runtimeResponse.PendingApprovals.Count > 0 && approved && ShouldAutoApprovePendingToolCalls(agent, runtimeSession))
                {
                    var continuation = await ContinueAutoApprovedRunAsync(
                        run,
                        agent,
                        provider,
                        runtimeSession,
                        run.ChatSessionId,
                        attachedCapabilities,
                        memory,
                        runtimeResponse,
                        (state, phase, message) => AppendExecutionLogAsync(run.Id, agent.Id, run.ChatSessionId, state, phase, message, cancellationToken),
                        cancellationToken);

                    runtimeSession = continuation.Session;
                    runtimeResponse = continuation.Response;
                    totalInputTokens = continuation.TotalInputTokens;
                    totalOutputTokens = continuation.TotalOutputTokens;
                    totalToolCalls = continuation.TotalToolCalls;
                }

                var assistantMessage = session is null
                    ? null
                    : new ChatMessageRecord(
                        Id: Guid.NewGuid(),
                        Role: ChatMessageRole.Assistant,
                        Content: runtimeResponse.ResponseText,
                        CreatedAtUtc: DateTimeOffset.UtcNow,
                        TokenEstimate: totalOutputTokens);

                var metric = new AgentRunMetric(
                    Id: Guid.NewGuid(),
                    AgentId: agent.Id,
                    ChatSessionId: run.ChatSessionId,
                    CreatedAtUtc: DateTimeOffset.UtcNow,
                    Outcome: runtimeResponse.PendingApprovals.Count > 0 ? RunOutcome.Cancelled : RunOutcome.Succeeded,
                    ProviderName: provider.Name,
                    Model: ResolveModel(agent, provider),
                    DurationMs: Math.Max(1, (long)(DateTimeOffset.UtcNow - startedAt).TotalMilliseconds),
                    InputTokens: totalInputTokens,
                    OutputTokens: totalOutputTokens,
                    ToolCalls: totalToolCalls)
                {
                    ExecutionRunId = run.Id
                };

                var updatedRun = UpdateRunFromResponse(
                    run,
                    runtimeResponse,
                    runtimeResponse.PendingApprovals.Count > 0 ? ExecutionState.WaitingOnTool : ExecutionState.Completed,
                    runtimeResponse.PendingApprovals.Count > 0 ? null : RunOutcome.Succeeded,
                    DateTimeOffset.UtcNow);

                var approvalUpdate = ExecutionRunStateTransitions.SynchronizePendingApprovals(
                    prepared.RunApprovals,
                    updatedRun,
                    runtimeResponse.PendingApprovals,
                    DateTimeOffset.UtcNow);

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
                            Metric: metric),
                        cancellationToken);
                }
                else
                {
                    await PersistExecutionMutationAsync(
                        new ExecutionStateMutation(
                            Run: updatedRun,
                            RunApprovals: approvalUpdate.RunApprovals,
                            Metric: metric),
                        cancellationToken);
                }

                if (approvalUpdate.Pending.Count > 0)
                {
                    await executionGovernanceBridge.OnApprovalsRequestedAsync(updatedRun, approvalUpdate.Pending, cancellationToken);
                }

                var completionState = runtimeResponse.PendingApprovals.Count > 0 ? ExecutionState.WaitingOnTool : ExecutionState.Completed;
                if (completionState == ExecutionState.Completed)
                {
                    AgentFrameworkTelemetry.RecordRunOutcome(updatedRun);
                }

                await AppendExecutionLogAsync(
                    run.Id,
                    agent.Id,
                    run.ChatSessionId,
                    completionState,
                    completionState == ExecutionState.Completed ? "Completed" : "Approval",
                    completionState == ExecutionState.Completed
                        ? "Execution run response persisted after the approval decision."
                        : "The execution run still requires another approval decision before it can continue.",
                    cancellationToken);

                return new ExecutionRunResult(run.Id, run.ChatSessionId, runtimeResponse.ResponseText, assistantMessage, metric);
            }
        }
        catch (Exception exception)
        {
            var failureMetric = new AgentRunMetric(
                Id: Guid.NewGuid(),
                AgentId: agent.Id,
                ChatSessionId: run.ChatSessionId,
                CreatedAtUtc: DateTimeOffset.UtcNow,
                Outcome: RunOutcome.Failed,
                ProviderName: provider.Name,
                Model: ResolveModel(agent, provider),
                DurationMs: Math.Max(1, (long)(DateTimeOffset.UtcNow - startedAt).TotalMilliseconds),
                InputTokens: 0,
                OutputTokens: 0,
                ToolCalls: 0)
            {
                ExecutionRunId = run.Id
            };

            var failedRun = run with
            {
                Revision = NextRunRevision(run.Revision),
                UpdatedAtUtc = DateTimeOffset.UtcNow,
                CompletedAtUtc = DateTimeOffset.UtcNow,
                State = ExecutionState.Failed,
                Outcome = RunOutcome.Failed,
                ResultSummary = CreateExecutionSummary($"Execution run continuation failed: {exception.Message}"),
                PendingApprovals = []
            };

            if (session is not null)
            {
                var updatedSession = ChatSessionRuntimeCompatibilityAdapter.ClearCompatibility(
                    session,
                    failedRun.UpdatedAtUtc,
                    failedRun.Id);
                await PersistExecutionMutationAsync(
                    new ExecutionStateMutation(
                        Run: failedRun,
                        Session: updatedSession,
                        Metric: failureMetric),
                    cancellationToken);
            }
            else
            {
                await PersistExecutionMutationAsync(
                    new ExecutionStateMutation(
                        Run: failedRun,
                        Metric: failureMetric),
                    cancellationToken);
            }
            AgentFrameworkTelemetry.RecordRunOutcome(failedRun);

            await AppendExecutionLogAsync(
                run.Id,
                agent.Id,
                run.ChatSessionId,
                ExecutionState.Failed,
                "Failed",
                $"Execution run approval continuation failed: {exception.Message}",
                cancellationToken);

            throw;
        }
    }

    public async Task<IReadOnlyList<ExecutionRunRecord>> ListExecutionRunsAsync(
        ExecutionRunQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var executionState = await store.LoadExecutionAsync(cancellationToken);
        var runs = executionState.ExecutionRuns.AsEnumerable();

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

        if (!string.IsNullOrWhiteSpace(query.ProcessStepId))
        {
            runs = runs.Where(item => string.Equals(item.ProcessStepId, query.ProcessStepId, StringComparison.OrdinalIgnoreCase));
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

        if (query.ApprovalStatus.HasValue)
        {
            runs = runs.Where(item => ExecutionRunStateTransitions.MatchesApprovalStatus(executionState.ExecutionApprovals, item, query.ApprovalStatus.Value));
        }

        return runs
            .OrderByDescending(item => item.UpdatedAtUtc)
            .Take(query.Take <= 0 ? 50 : query.Take)
            .ToList();
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
        AgentDefinition agent,
        ProviderProfile provider,
        SandboxWorkspaceCatalog catalog,
        SandboxWorkspaceExecutionState executionState,
        ChatSessionRecord? session,
        ExecutionRunRequest request,
        bool persistTranscript,
        CancellationToken cancellationToken)
    {
        var prompt = request.Prompt.Trim();
        var context = request.Context ?? ExecutionInvocationContext.Empty;
        ChatMessageRecord? userMessage = null;
        ExecutionRunRecord run;

        if (persistTranscript)
        {
            var prepared = await BeginChatBackedRunAsync(
                agent.Id,
                provider,
                session?.Id,
                prompt,
                context,
                request.AutoApprovePendingToolCalls,
                cancellationToken);

            catalog = prepared.Catalog;
            agent = prepared.Agent;
            provider = prepared.Provider;
            session = prepared.Session;
            run = prepared.Run;
            userMessage = prepared.UserMessage;
        }
        else
        {
            if (session is not null && TryGetBlockingSessionRun(executionState, session, out _))
            {
                throw new InvalidOperationException(DescribeSessionBusyMessage(executionState, session));
            }

            var now = DateTimeOffset.UtcNow;
            run = CreatePreparingRun(
                agent,
                provider,
                session?.Id,
                session?.Title ?? CreateSessionTitle(prompt),
                context,
                prompt,
                now,
                request.AutoApprovePendingToolCalls);

            if (session is not null)
            {
                session = ChatSessionRuntimeCompatibilityAdapter.ClearCompatibility(
                    session,
                    session.UpdatedAtUtc,
                    run.Id);
                await PersistExecutionMutationAsync(
                    new ExecutionStateMutation(
                        Run: run,
                        Session: session),
                    cancellationToken);
            }
            else
            {
                await PersistExecutionMutationAsync(
                    new ExecutionStateMutation(Run: run),
                    cancellationToken);
            }
        }

        return await CompletePreparedExecutionRunAsync(
            agent,
            provider,
            catalog,
            session,
            request,
            run,
            userMessage,
            cancellationToken);
    }

    private async Task<ExecutionRunResult> CompletePreparedExecutionRunAsync(
        AgentDefinition agent,
        ProviderProfile provider,
        SandboxWorkspaceCatalog catalog,
        ChatSessionRecord? session,
        ExecutionRunRequest request,
        ExecutionRunRecord run,
        ChatMessageRecord? userMessage,
        CancellationToken cancellationToken)
    {
        var prompt = request.Prompt.Trim();
        var attachedCapabilities = ResolveAttachedCapabilities(catalog, agent);
        var memory = ResolveAgentMemory(catalog, agent.Id);
        using var runActivity = AgentFrameworkTelemetry.StartRunActivity("agent.run", run);

        await AppendExecutionLogAsync(
            run.Id,
            agent.Id,
            run.ChatSessionId,
            ExecutionState.Preparing,
            "Planning",
            $"Preparing provider {provider.Name}.",
            cancellationToken);

        var startedAt = DateTimeOffset.UtcNow;
        try
        {
            var runtimeSession = ChatSessionRuntimeCompatibilityAdapter.CreateRuntimeSession(run, agent.Id, session);
            AgentRuntimeResponse runtimeResponse;
            using (WorkspaceExecutionAuditContext.BeginScope(run))
            {
                runtimeResponse = await runtime.RunAsync(
                    agent,
                    provider,
                    runtimeSession,
                    attachedCapabilities,
                    memory,
                    prompt,
                    string.IsNullOrWhiteSpace(run.RuntimeSessionKey) ? null : run.RuntimeSessionKey,
                    (state, phase, message) => AppendExecutionLogAsync(run.Id, agent.Id, run.ChatSessionId, state, phase, message, cancellationToken),
                    cancellationToken,
                    suppressApprovalRequirements: ShouldAutoApprovePendingToolCalls(agent, runtimeSession));

                var totalInputTokens = runtimeResponse.InputTokens;
                var totalOutputTokens = runtimeResponse.OutputTokens;
                var totalToolCalls = runtimeResponse.ToolCalls;

                if (runtimeResponse.PendingApprovals.Count > 0 && ShouldAutoApprovePendingToolCalls(agent, runtimeSession))
                {
                    var continuation = await ContinueAutoApprovedRunAsync(
                        run,
                        agent,
                        provider,
                        runtimeSession,
                        run.ChatSessionId,
                        attachedCapabilities,
                        memory,
                        runtimeResponse,
                        (state, phase, message) => AppendExecutionLogAsync(run.Id, agent.Id, run.ChatSessionId, state, phase, message, cancellationToken),
                        cancellationToken);

                    runtimeSession = continuation.Session;
                    runtimeResponse = continuation.Response;
                    totalInputTokens = continuation.TotalInputTokens;
                    totalOutputTokens = continuation.TotalOutputTokens;
                    totalToolCalls = continuation.TotalToolCalls;
                }

                var assistantMessage = session is null
                    ? null
                    : new ChatMessageRecord(
                        Id: Guid.NewGuid(),
                        Role: ChatMessageRole.Assistant,
                        Content: runtimeResponse.ResponseText,
                        CreatedAtUtc: DateTimeOffset.UtcNow,
                        TokenEstimate: totalOutputTokens);

                var metric = new AgentRunMetric(
                    Id: Guid.NewGuid(),
                    AgentId: agent.Id,
                    ChatSessionId: run.ChatSessionId,
                    CreatedAtUtc: DateTimeOffset.UtcNow,
                    Outcome: runtimeResponse.PendingApprovals.Count > 0 ? RunOutcome.Cancelled : RunOutcome.Succeeded,
                    ProviderName: provider.Name,
                    Model: ResolveModel(agent, provider),
                    DurationMs: Math.Max(1, (long)(DateTimeOffset.UtcNow - startedAt).TotalMilliseconds),
                    InputTokens: totalInputTokens + (userMessage?.TokenEstimate ?? EstimateTokens(prompt)),
                    OutputTokens: totalOutputTokens,
                    ToolCalls: totalToolCalls)
                {
                    ExecutionRunId = run.Id
                };

                var updatedRun = UpdateRunFromResponse(
                    run,
                    runtimeResponse,
                    runtimeResponse.PendingApprovals.Count > 0 ? ExecutionState.WaitingOnTool : ExecutionState.Completed,
                    runtimeResponse.PendingApprovals.Count > 0 ? null : RunOutcome.Succeeded,
                    DateTimeOffset.UtcNow);

                var approvalUpdate = ExecutionRunStateTransitions.SynchronizePendingApprovals(
                    [],
                    updatedRun,
                    runtimeResponse.PendingApprovals,
                    DateTimeOffset.UtcNow);

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
                            Metric: metric),
                        cancellationToken);
                }
                else
                {
                    await PersistExecutionMutationAsync(
                        new ExecutionStateMutation(
                            Run: updatedRun,
                            RunApprovals: approvalUpdate.RunApprovals,
                            Metric: metric),
                        cancellationToken);
                }

                if (approvalUpdate.Pending.Count > 0)
                {
                    await executionGovernanceBridge.OnApprovalsRequestedAsync(updatedRun, approvalUpdate.Pending, cancellationToken);
                }

                var completionState = runtimeResponse.PendingApprovals.Count > 0 ? ExecutionState.WaitingOnTool : ExecutionState.Completed;
                if (completionState == ExecutionState.Completed)
                {
                    AgentFrameworkTelemetry.RecordRunOutcome(updatedRun);
                }

                await AppendExecutionLogAsync(
                    run.Id,
                    agent.Id,
                    run.ChatSessionId,
                    completionState,
                    completionState == ExecutionState.Completed ? "Completed" : "Approval",
                    completionState == ExecutionState.Completed
                        ? "Execution run response persisted."
                        : "The execution run is waiting for an approval response before it can continue.",
                    cancellationToken);

                return new ExecutionRunResult(run.Id, run.ChatSessionId, runtimeResponse.ResponseText, assistantMessage, metric);
            }
        }
        catch (Exception exception)
        {
            var failureMetric = new AgentRunMetric(
                Id: Guid.NewGuid(),
                AgentId: agent.Id,
                ChatSessionId: run.ChatSessionId,
                CreatedAtUtc: DateTimeOffset.UtcNow,
                Outcome: RunOutcome.Failed,
                ProviderName: provider.Name,
                Model: ResolveModel(agent, provider),
                DurationMs: Math.Max(1, (long)(DateTimeOffset.UtcNow - startedAt).TotalMilliseconds),
                InputTokens: userMessage?.TokenEstimate ?? EstimateTokens(prompt),
                OutputTokens: 0,
                ToolCalls: 0)
            {
                ExecutionRunId = run.Id
            };

            var failedRun = run with
            {
                UpdatedAtUtc = DateTimeOffset.UtcNow,
                CompletedAtUtc = DateTimeOffset.UtcNow,
                State = ExecutionState.Failed,
                Outcome = RunOutcome.Failed,
                ResultSummary = CreateExecutionSummary(exception.Message),
                PendingApprovals = []
            };

            if (session is not null)
            {
                var updatedSession = ChatSessionRuntimeCompatibilityAdapter.ClearCompatibility(
                    session,
                    failedRun.UpdatedAtUtc,
                    failedRun.Id);
                await PersistExecutionMutationAsync(
                    new ExecutionStateMutation(
                        Run: failedRun,
                        Session: updatedSession,
                        Metric: failureMetric),
                    cancellationToken);
            }
            else
            {
                await PersistExecutionMutationAsync(
                    new ExecutionStateMutation(
                        Run: failedRun,
                        Metric: failureMetric),
                    cancellationToken);
            }
            AgentFrameworkTelemetry.RecordRunOutcome(failedRun);

            await AppendExecutionLogAsync(
                run.Id,
                agent.Id,
                run.ChatSessionId,
                ExecutionState.Failed,
                "Failed",
                $"Execution run failed for {provider.Name}: {exception.Message}",
                cancellationToken);

            if (session is not null)
            {
                throw new AgentChatRunFailedException(
                    agent.Id,
                    run.Id,
                    session.Id,
                    provider.Name,
                    ResolveModel(agent, provider),
                    exception);
            }

            throw;
        }
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
            ResultSummary = response.PendingApprovals.Count > 0
                ? $"Awaiting approval for {response.PendingApprovals.Count} tool request(s)."
                : CreateExecutionSummary(response.ResponseText)
        };
    }

    private async Task<Guid> ResolveOrCreatePendingExecutionRunIdAsync(
        Guid agentId,
        Guid chatSessionId,
        CancellationToken cancellationToken)
    {
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

    private static string ResolveModel(AgentDefinition agent, ProviderProfile provider)
    {
        return string.IsNullOrWhiteSpace(agent.Model)
            ? provider.DefaultModel
            : agent.Model;
    }

    private static string CreateExecutionSummary(string value)
    {
        var cleaned = value.Trim().ReplaceLineEndings(" ");
        return cleaned.Length <= 160
            ? cleaned
            : $"{cleaned[..157]}...";
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
