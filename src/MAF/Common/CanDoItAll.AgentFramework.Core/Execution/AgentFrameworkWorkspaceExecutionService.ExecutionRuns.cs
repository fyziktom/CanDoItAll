using System.Diagnostics;
using System.Text.Json;
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

        if (TryGetExecutionRunStore() is not null)
        {
            var catalogOnly = await store.LoadCatalogAsync(cancellationToken);
            var agentOnly = EnsureAgentExists(catalogOnly, request.AgentId);
            var providerOnly = await ResolveProviderForExecutionRequestAsync(
                agentOnly,
                catalogOnly,
                request,
                await ResolveProviderForAgentAsync(agentOnly, catalogOnly, cancellationToken).ConfigureAwait(false),
                cancellationToken).ConfigureAwait(false);

            if (request.ChatSessionId.HasValue &&
                store is ISandboxWorkspaceChatQueryStore chatQueryStore)
            {
                var sessionOnly = EnsureAgentOwnsSession(
                    await chatQueryStore.GetChatSessionAsync(request.ChatSessionId.Value, cancellationToken),
                    request.AgentId,
                    request.ChatSessionId.Value);

                return await ExecuteRunCoreAsync(
                    agentOnly,
                    providerOnly,
                    catalogOnly,
                    SandboxWorkspaceExecutionState.Empty,
                    sessionOnly,
                    request,
                    persistTranscript: true,
                    cancellationToken);
            }

            if (request.ChatSessionId.HasValue)
            {
                return await ExecuteRunWithWorkspaceDocumentAsync(request, cancellationToken);
            }

            return await ExecuteRunCoreAsync(
                agentOnly,
                providerOnly,
                catalogOnly,
                SandboxWorkspaceExecutionState.Empty,
                session: null,
                request,
                persistTranscript: false,
                cancellationToken);
        }

        return await ExecuteRunWithWorkspaceDocumentAsync(request, cancellationToken);
    }

    private async Task<ExecutionRunResult> ExecuteRunWithWorkspaceDocumentAsync(
        ExecutionRunRequest request,
        CancellationToken cancellationToken)
    {
        var document = await store.LoadAsync(cancellationToken);
        var catalog = document.ToCatalog();
        var executionState = document.ToExecutionState();
        var agent = EnsureAgentExists(catalog, request.AgentId);
        var provider = await ResolveProviderForExecutionRequestAsync(
            agent,
            catalog,
            request,
            await ResolveProviderForAgentAsync(agent, catalog, cancellationToken).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);
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
        var configuredProvider = await ResolveProviderForAgentAsync(agent, catalog, cancellationToken);
        var provider = ResolveContinuationProvider(run, configuredProvider, catalog.Providers);
        var runtimeAgent = CreateProviderCompatibleRuntimeAgent(agent, provider, run.Model);
        var attachedCapabilities = ResolveAttachedCapabilities(catalog, agent);
        var memory = ResolveAgentMemoryForRun(catalog, agent.Id, run);
        var structuredOutput = ResolveContinuationStructuredOutputContract(run);
        var handoffOptions = await ResolveHandoffExecutionOptionsAsync(agent, catalog, run, cancellationToken);
        using var runActivity = AgentFrameworkTelemetry.StartRunActivity("agent.run.resume", prepared.OriginalRun);
        AgentFrameworkTelemetry.RecordRunResume(prepared.OriginalRun);

        PrimeProviderCredentialEnvironment(provider);

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
        AgentRuntimeResponse? lastRuntimeResponse = null;
        IAgentExecutionCancellationRegistration? executionCancellation = null;
        try
        {
            executionCancellation = executionCancellationRegistry.Register(run, cancellationToken);
            var runtimeCancellationToken = executionCancellation.Token;
            var runtimeSession = ChatSessionRuntimeCompatibilityAdapter.CreateRuntimeSession(run, agent.Id, session);
            AgentRuntimeResponse runtimeResponse;
            using (WorkspaceExecutionAuditContext.BeginScope(run))
            {
                runtimeResponse = await runtime.RespondToPendingApprovalsAsync(
                    runtimeAgent,
                    provider,
                    runtimeSession,
                    attachedCapabilities,
                    memory,
                    approved,
                    string.IsNullOrWhiteSpace(run.RuntimeSessionKey) ? null : run.RuntimeSessionKey,
                    (state, phase, message) => AppendExecutionLogAsync(run.Id, agent.Id, run.ChatSessionId, state, phase, message, cancellationToken),
                    runtimeCancellationToken,
                    suppressApprovalRequirements: approved && ShouldAutoApprovePendingToolCalls(agent, runtimeSession),
                    structuredOutput: structuredOutput,
                    executionOptions: CreateRuntimeExecutionOptions(run, structuredOutput, handoffOptions));
                lastRuntimeResponse = runtimeResponse;

                var totalInputTokens = runtimeResponse.InputTokens;
                var totalCachedInputTokens = runtimeResponse.CachedInputTokens;
                var totalOutputTokens = runtimeResponse.OutputTokens;
                var totalToolCalls = runtimeResponse.ToolCalls;

                if (runtimeResponse.PendingApprovals.Count > 0 && approved && ShouldAutoApprovePendingToolCalls(agent, runtimeSession))
                {
                    var continuation = await ContinueAutoApprovedRunAsync(
                        run,
                        runtimeAgent,
                        provider,
                        runtimeSession,
                        run.ChatSessionId,
                        attachedCapabilities,
                        memory,
                        runtimeResponse,
                        (state, phase, message) => AppendExecutionLogAsync(run.Id, agent.Id, run.ChatSessionId, state, phase, message, cancellationToken),
                        structuredOutput,
                        handoffOptions,
                        runtimeCancellationToken);

                    runtimeSession = continuation.Session;
                    runtimeResponse = continuation.Response;
                    lastRuntimeResponse = runtimeResponse;
                    totalInputTokens = continuation.TotalInputTokens;
                    totalCachedInputTokens = continuation.TotalCachedInputTokens;
                    totalOutputTokens = continuation.TotalOutputTokens;
                    totalToolCalls = continuation.TotalToolCalls;
                }

                runtimeResponse = await ValidateMachineOutputBeforeCompletionAsync(
                    run,
                    structuredOutput,
                    runtimeResponse,
                    runtimeCancellationToken);
                lastRuntimeResponse = runtimeResponse;

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
                        Outcome: runtimeResponse.PendingApprovals.Count > 0 ? RunOutcome.Cancelled : RunOutcome.Succeeded,
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
                var usageObservations = BuildUsageObservations(run, runtimeAgent, provider, metric, runtimeResponse);

                var updatedRun = UpdateRunFromResponse(
                    run,
                    runtimeResponse,
                    runtimeResponse.PendingApprovals.Count > 0 ? ExecutionState.WaitingOnTool : ExecutionState.Completed,
                    runtimeResponse.PendingApprovals.Count > 0 ? null : RunOutcome.Succeeded,
                    DateTimeOffset.UtcNow);
                var runtimeToolReceipts = CreateRuntimeProviderToolReceipts(run, runtimeResponse);

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
                            Metric: metric,
                            UsageObservations: usageObservations,
                            ToolReceipts: runtimeToolReceipts),
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
                            ToolReceipts: runtimeToolReceipts),
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
                    transientContextRegistry.Remove(run.Id);
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

                return new ExecutionRunResult(run.Id, run.ChatSessionId, runtimeResponse.ResponseText, assistantMessage, metric)
                {
                    State = completionState,
                    ContextCompletionNotification = AgentChatContextInvocationFactory.CreateCompletionNotification(updatedRun)
                };
            }
        }
        catch (Exception exception)
        {
            transientContextRegistry.Remove(run.Id);
            var cancellationKind = ClassifyExecutionCancellation(exception, executionCancellation, cancellationToken);
            var wasCancelled = cancellationKind != ExecutionCancellationKind.None;
            var outcome = wasCancelled ? RunOutcome.Cancelled : RunOutcome.Failed;
            var failureDisplay = wasCancelled
                ? null
                : AgentProviderFailureDisplayFormatter.Format(provider, exception);
            var resultSummary = cancellationKind switch
            {
                ExecutionCancellationKind.ProcessRegistry => "Execution run cancelled because the owning process run was cancelled.",
                ExecutionCancellationKind.CallerRequest => "Execution run cancelled because the caller request was cancelled.",
                _ => failureDisplay!.Message
            };
            var failureMetric = PriceMetric(
                new AgentRunMetric(
                    Id: Guid.NewGuid(),
                    AgentId: agent.Id,
                    ChatSessionId: run.ChatSessionId,
                    CreatedAtUtc: DateTimeOffset.UtcNow,
                    Outcome: outcome,
                    ProviderName: provider.Name,
                    Model: ResolveModel(runtimeAgent, provider),
                    DurationMs: Math.Max(1, (long)(DateTimeOffset.UtcNow - startedAt).TotalMilliseconds),
                    InputTokens: 0,
                    OutputTokens: 0,
                    ToolCalls: 0)
                {
                    ExecutionRunId = run.Id
                },
                provider);
            var failureUsageObservations = lastRuntimeResponse is null
                ? BuildFailureUsageObservations(run, runtimeAgent, provider, failureMetric, exception)
                : BuildRuntimeResponseUsageObservations(run, runtimeAgent, provider, failureMetric, lastRuntimeResponse);

            var failedRun = run with
            {
                Revision = NextRunRevision(run.Revision),
                UpdatedAtUtc = DateTimeOffset.UtcNow,
                CompletedAtUtc = DateTimeOffset.UtcNow,
                State = ExecutionState.Failed,
                Outcome = outcome,
                ResultSummary = CreateExecutionSummary(resultSummary),
                PendingApprovals = []
            };
            var terminalPersistenceToken = CancellationToken.None;

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
                            Metric: failureMetric,
                            UsageObservations: failureUsageObservations),
                        terminalPersistenceToken);
            }
            else
            {
                await PersistExecutionMutationAsync(
                    new ExecutionStateMutation(
                        Run: failedRun,
                        Metric: failureMetric,
                        UsageObservations: failureUsageObservations),
                    terminalPersistenceToken);
            }
            AgentFrameworkTelemetry.RecordRunOutcome(failedRun);

            await AppendExecutionLogAsync(
                run.Id,
                agent.Id,
                run.ChatSessionId,
                ExecutionState.Failed,
                wasCancelled ? "Cancelled" : "Failed",
                wasCancelled
                    ? resultSummary
                    : $"Execution run approval continuation failed for {provider.Name}: {failureDisplay!.Message}",
                terminalPersistenceToken);

            if (cancellationKind == ExecutionCancellationKind.ProcessRegistry)
            {
                throw new AgentExecutionCancelledException(run.Id, run.ProcessRunId, resultSummary, exception);
            }

            if (cancellationKind == ExecutionCancellationKind.CallerRequest)
            {
                throw new OperationCanceledException(resultSummary, exception, cancellationToken);
            }

            throw session is not null
                ? new AgentChatRunFailedException(
                    agent.Id,
                    run.Id,
                    session.Id,
                    provider.Name,
                    ResolveModel(runtimeAgent, provider),
                    exception,
                    failureDisplay!.Message)
                : new AgentRunFailedException(
                    agent.Id,
                    run.Id,
                    run.ChatSessionId,
                    provider.Name,
                    ResolveModel(runtimeAgent, provider),
                    exception,
                    failureDisplay!.Message);
        }
        finally
        {
            executionCancellation?.Dispose();
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
            executionState ??= await store.LoadExecutionAsync(cancellationToken);
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
        var context = PrepareInvocationContext(request);
        request = request with { Context = context };
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
                request.StructuredOutput,
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
                request.AutoApprovePendingToolCalls,
                request.StructuredOutput);

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
        var runtimeAgent = CreateProviderCompatibleRuntimeAgent(agent, provider, run.Model);
        var attachedCapabilities = ResolveAttachedCapabilities(catalog, agent);
        var memory = ResolveAgentMemoryForRun(catalog, agent.Id, run);
        var handoffOptions = await ResolveHandoffExecutionOptionsAsync(agent, catalog, run, cancellationToken);
        var inputAttachments = await ResolveRuntimeInputAttachmentsAsync(request.InputAttachmentPaths, cancellationToken);
        using var runActivity = AgentFrameworkTelemetry.StartRunActivity("agent.run", run);

        PrimeProviderCredentialEnvironment(provider);

        await AppendExecutionLogAsync(
            run.Id,
            agent.Id,
            run.ChatSessionId,
            ExecutionState.Preparing,
            "Planning",
            $"Preparing provider {provider.Name}.",
            cancellationToken);
        await AppendProcessCooperationLogAsync(run, agent.Id, run.ChatSessionId, cancellationToken);

        var startedAt = DateTimeOffset.UtcNow;
        AgentRuntimeResponse? lastRuntimeResponse = null;
        IAgentExecutionCancellationRegistration? executionCancellation = null;
        try
        {
            if (request.TransientContext is not null)
            {
                transientContextRegistry.Register(run, request.TransientContext);
            }

            executionCancellation = executionCancellationRegistry.Register(run, cancellationToken);
            var runtimeCancellationToken = executionCancellation.Token;
            var runtimeSession = ChatSessionRuntimeCompatibilityAdapter.CreateRuntimeSession(run, agent.Id, session);
            AgentRuntimeResponse runtimeResponse;
            using (WorkspaceExecutionAuditContext.BeginScope(run))
            {
                runtimeResponse = await runtime.RunAsync(
                    runtimeAgent,
                    provider,
                    runtimeSession,
                    attachedCapabilities,
                    memory,
                    prompt,
                    string.IsNullOrWhiteSpace(run.RuntimeSessionKey) ? null : run.RuntimeSessionKey,
                    (state, phase, message) => AppendExecutionLogAsync(run.Id, agent.Id, run.ChatSessionId, state, phase, message, cancellationToken),
                    runtimeCancellationToken,
                    suppressApprovalRequirements: ShouldAutoApprovePendingToolCalls(agent, runtimeSession),
                    structuredOutput: request.StructuredOutput,
                    executionOptions: CreateRuntimeExecutionOptions(run, request.StructuredOutput, handoffOptions, inputAttachments));
                lastRuntimeResponse = runtimeResponse;

                var totalInputTokens = runtimeResponse.InputTokens;
                var totalCachedInputTokens = runtimeResponse.CachedInputTokens;
                var totalOutputTokens = runtimeResponse.OutputTokens;
                var totalToolCalls = runtimeResponse.ToolCalls;

                if (runtimeResponse.PendingApprovals.Count > 0 && ShouldAutoApprovePendingToolCalls(agent, runtimeSession))
                {
                    var continuation = await ContinueAutoApprovedRunAsync(
                        run,
                        runtimeAgent,
                        provider,
                        runtimeSession,
                        run.ChatSessionId,
                        attachedCapabilities,
                        memory,
                        runtimeResponse,
                        (state, phase, message) => AppendExecutionLogAsync(run.Id, agent.Id, run.ChatSessionId, state, phase, message, cancellationToken),
                        request.StructuredOutput,
                        handoffOptions,
                        runtimeCancellationToken);

                    runtimeSession = continuation.Session;
                    runtimeResponse = continuation.Response;
                    lastRuntimeResponse = runtimeResponse;
                    totalInputTokens = continuation.TotalInputTokens;
                    totalCachedInputTokens = continuation.TotalCachedInputTokens;
                    totalOutputTokens = continuation.TotalOutputTokens;
                    totalToolCalls = continuation.TotalToolCalls;
                }

                runtimeResponse = await ValidateMachineOutputBeforeCompletionAsync(
                    run,
                    request.StructuredOutput,
                    runtimeResponse,
                    runtimeCancellationToken);
                lastRuntimeResponse = runtimeResponse;

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
                        Outcome: runtimeResponse.PendingApprovals.Count > 0 ? RunOutcome.Cancelled : RunOutcome.Succeeded,
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
                var usageObservations = BuildUsageObservations(run, runtimeAgent, provider, metric, runtimeResponse);

                var updatedRun = UpdateRunFromResponse(
                    run,
                    runtimeResponse,
                    runtimeResponse.PendingApprovals.Count > 0 ? ExecutionState.WaitingOnTool : ExecutionState.Completed,
                    runtimeResponse.PendingApprovals.Count > 0 ? null : RunOutcome.Succeeded,
                    DateTimeOffset.UtcNow);
                var runtimeToolReceipts = CreateRuntimeProviderToolReceipts(run, runtimeResponse);

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
                            Metric: metric,
                            UsageObservations: usageObservations,
                            ToolReceipts: runtimeToolReceipts),
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
                            ToolReceipts: runtimeToolReceipts),
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
                    transientContextRegistry.Remove(run.Id);
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

                return new ExecutionRunResult(run.Id, run.ChatSessionId, runtimeResponse.ResponseText, assistantMessage, metric)
                {
                    State = completionState,
                    ContextCompletionNotification = AgentChatContextInvocationFactory.CreateCompletionNotification(updatedRun)
                };
            }
        }
        catch (Exception exception)
        {
            transientContextRegistry.Remove(run.Id);
            var cancellationKind = ClassifyExecutionCancellation(exception, executionCancellation, cancellationToken);
            var wasCancelled = cancellationKind != ExecutionCancellationKind.None;
            var outcome = wasCancelled ? RunOutcome.Cancelled : RunOutcome.Failed;
            var failureDisplay = wasCancelled
                ? null
                : AgentProviderFailureDisplayFormatter.Format(provider, exception);
            var resultSummary = cancellationKind switch
            {
                ExecutionCancellationKind.ProcessRegistry => "Execution run cancelled because the owning process run was cancelled.",
                ExecutionCancellationKind.CallerRequest => "Execution run cancelled because the caller request was cancelled.",
                _ => failureDisplay!.Message
            };
            var failureMetric = PriceMetric(
                new AgentRunMetric(
                    Id: Guid.NewGuid(),
                    AgentId: agent.Id,
                    ChatSessionId: run.ChatSessionId,
                    CreatedAtUtc: DateTimeOffset.UtcNow,
                    Outcome: outcome,
                    ProviderName: provider.Name,
                    Model: ResolveModel(runtimeAgent, provider),
                    DurationMs: Math.Max(1, (long)(DateTimeOffset.UtcNow - startedAt).TotalMilliseconds),
                    InputTokens: userMessage?.TokenEstimate ?? EstimateTokens(prompt),
                    OutputTokens: 0,
                    ToolCalls: 0)
                {
                    ExecutionRunId = run.Id
                },
                provider);
            var failureUsageObservations = lastRuntimeResponse is null
                ? BuildFailureUsageObservations(run, runtimeAgent, provider, failureMetric, exception)
                : BuildRuntimeResponseUsageObservations(run, runtimeAgent, provider, failureMetric, lastRuntimeResponse);

            var failedRun = run with
            {
                UpdatedAtUtc = DateTimeOffset.UtcNow,
                CompletedAtUtc = DateTimeOffset.UtcNow,
                State = ExecutionState.Failed,
                Outcome = outcome,
                ResultSummary = CreateExecutionSummary(resultSummary),
                PendingApprovals = []
            };
            var terminalPersistenceToken = CancellationToken.None;

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
                            Metric: failureMetric,
                            UsageObservations: failureUsageObservations),
                        terminalPersistenceToken);
            }
            else
            {
                await PersistExecutionMutationAsync(
                    new ExecutionStateMutation(
                        Run: failedRun,
                        Metric: failureMetric,
                        UsageObservations: failureUsageObservations),
                    terminalPersistenceToken);
            }
            AgentFrameworkTelemetry.RecordRunOutcome(failedRun);

            await AppendExecutionLogAsync(
                run.Id,
                agent.Id,
                run.ChatSessionId,
                ExecutionState.Failed,
                wasCancelled ? "Cancelled" : "Failed",
                wasCancelled
                    ? resultSummary
                    : $"Execution run failed for {provider.Name}: {failureDisplay!.Message}",
                terminalPersistenceToken);

            if (cancellationKind == ExecutionCancellationKind.ProcessRegistry)
            {
                throw new AgentExecutionCancelledException(run.Id, run.ProcessRunId, resultSummary, exception);
            }

            if (cancellationKind == ExecutionCancellationKind.CallerRequest)
            {
                throw new OperationCanceledException(resultSummary, exception, cancellationToken);
            }

            throw session is not null
                ? new AgentChatRunFailedException(
                    agent.Id,
                    run.Id,
                    session.Id,
                    provider.Name,
                    ResolveModel(runtimeAgent, provider),
                    exception,
                    failureDisplay!.Message)
                : new AgentRunFailedException(
                    agent.Id,
                    run.Id,
                    run.ChatSessionId,
                    provider.Name,
                    ResolveModel(runtimeAgent, provider),
                    exception,
                    failureDisplay!.Message);
        }
        finally
        {
            executionCancellation?.Dispose();
        }
    }

    private void PrimeProviderCredentialEnvironment(ProviderProfile provider)
    {
        var resolution = providerCredentialResolver.Resolve(provider);
        if (!resolution.IsResolved ||
            !resolution.ShouldPromoteToProcessEnvironment)
        {
            return;
        }

        AgentProviderEnvironmentCredential.PromoteProcessValue(
            provider.ApiKeyEnvironmentVariable,
            resolution.ApiKey);

        if (provider.Kind is ProviderKind.OpenAi or ProviderKind.AzureOpenAi)
        {
            AgentProviderEnvironmentCredential.PromoteProcessValue(
                "OPENAI_API_KEY",
                resolution.ApiKey);
        }
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
            ResultSummary = response.PendingApprovals.Count > 0
                ? $"Awaiting approval for {response.PendingApprovals.Count} tool request(s)."
                : CreateExecutionSummary(run, response)
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
            throw new InvalidOperationException(
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
                throw new InvalidOperationException(
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
            response = AppendRepairUsageObservations(response, repair);

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

            throw new InvalidOperationException(
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

    private AgentRuntimeExecutionOptions CreateRuntimeExecutionOptions(
        ExecutionRunRecord run,
        AgentStructuredOutputContract? structuredOutput,
        AgentRuntimeHandoffExecutionOptions? handoffOptions = null,
        IReadOnlyList<AgentRuntimeInputAttachment>? inputAttachments = null)
    {
        ArgumentNullException.ThrowIfNull(run);

        var transientContext = transientContextRegistry.Resolve(run);
        return new AgentRuntimeExecutionOptions(
            StructuredOutput: structuredOutput,
            FinalizerMode: AgentFinalizerPolicies.ResolveMode(run, structuredOutput),
            RequireStructuredOutputValidation: ExecutionInvocationMetadata.ResolveRequireStructuredOutputValidation(run),
            MaxStructuredOutputRepairAttempts: ExecutionInvocationMetadata.ResolveMaxStructuredOutputRepairAttempts(run),
            Handoff: handoffOptions,
            ContextWorkspaceScope: transientContext?.WorkspaceScope ?? ExecutionInvocationMetadata.ResolveContextWorkspaceScope(run),
            ContextIntent: CreateRuntimeContextIntent(run),
            InputAttachments: inputAttachments)
        {
            TransientContext = transientContext
        };
    }

    private static ExecutionInvocationContext PrepareInvocationContext(
        ExecutionRunRequest request)
    {
        var context = request.Context ?? ExecutionInvocationContext.Empty;
        if (request.TransientContext is null)
        {
            return context;
        }

        var digest = AgentChatContextDigest.Compute(request.TransientContext.Content);
        return context with
        {
            MetadataJson = ExecutionInvocationMetadata.ApplyTransientContextRequirement(
                context.MetadataJson,
                digest)
        };
    }

    private async Task<ProviderProfile> ResolveProviderForExecutionRequestAsync(
        AgentDefinition agent,
        SandboxWorkspaceCatalog? catalog,
        ExecutionRunRequest request,
        ProviderProfile configuredProvider,
        CancellationToken cancellationToken)
    {
        if (!ShouldOverrideProviderForGovernedProcessStep(request, configuredProvider))
        {
            return configuredProvider;
        }

        var candidates = await ResolveGovernedProcessProviderOverrideCandidatesAsync(configuredProvider, catalog, cancellationToken).ConfigureAwait(false);
        var selected = candidates.FirstOrDefault();
        return selected is null ? configuredProvider : selected;
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
        if (!provider.IsEnabled)
        {
            throw new InvalidOperationException(
                $"Provider '{provider.Name}' was disabled while execution run '{run.Id:N}' was waiting for approval.");
        }

        return provider;
    }

    internal static bool ShouldOverrideProviderForGovernedProcessStep(
        ExecutionRunRequest request,
        ProviderProfile configuredProvider)
    {
        if (!string.Equals(request.Context?.SourceKind, "process-step", StringComparison.OrdinalIgnoreCase) ||
            request.StructuredOutput?.OutputType != typeof(ProcessStepOutcomeResult))
        {
            return false;
        }

        var featureMatrix = ProviderFeatureService.ResolveFeatureMatrix(configuredProvider);
        return !featureMatrix.SupportsStructuredOutput;
    }

    private async Task<IReadOnlyList<ProviderProfile>> ResolveGovernedProcessProviderOverrideCandidatesAsync(
        ProviderProfile configuredProvider,
        SandboxWorkspaceCatalog? catalog,
        CancellationToken cancellationToken)
    {
        var providers = catalog?.Providers ?? await providerRegistry.ListProvidersAsync(cancellationToken).ConfigureAwait(false);
        return OrderGovernedProcessProviderOverrideCandidates(providers, configuredProvider, ProviderFeatureService)
            .ToArray();
    }

    internal static IReadOnlyList<ProviderProfile> OrderGovernedProcessProviderOverrideCandidates(
        IEnumerable<ProviderProfile> providers,
        ProviderProfile configuredProvider,
        IProviderProfileService providerProfileService)
    {
        ArgumentNullException.ThrowIfNull(providers);
        ArgumentNullException.ThrowIfNull(configuredProvider);
        ArgumentNullException.ThrowIfNull(providerProfileService);

        return providers
            .Where(provider => provider.IsEnabled)
            .Where(provider => provider.Purpose == ProviderProfilePurpose.Chat)
            .Where(provider => !IsScenarioHarnessProvider(provider))
            .Select(provider => new
            {
                Provider = provider,
                FeatureMatrix = providerProfileService.ResolveFeatureMatrix(provider)
            })
            .Where(item => item.FeatureMatrix.SupportsStructuredOutput)
            .OrderByDescending(item => SameProviderFamily(item.Provider, configuredProvider))
            .ThenByDescending(item => IsPreferredGovernedProcessProvider(item.Provider))
            .ThenByDescending(item => item.Provider.Kind is ProviderKind.OpenAi or ProviderKind.AzureOpenAi)
            .ThenByDescending(item => string.Equals(item.Provider.Name, ManagedSeedProviderFallbacks.OpenAiChatCompletionsProviderName, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(item => string.Equals(item.Provider.Name, configuredProvider.Name, StringComparison.OrdinalIgnoreCase))
            .ThenBy(item => item.Provider.Name, StringComparer.OrdinalIgnoreCase)
            .Select(item => item.Provider)
            .ToArray();
    }

    private static bool IsPreferredGovernedProcessProvider(ProviderProfile provider)
        => provider.Kind is ProviderKind.OpenAi or ProviderKind.AzureOpenAi &&
           provider.Transport == ProviderTransportKind.Responses;

    private static bool SameProviderFamily(ProviderProfile candidate, ProviderProfile configuredProvider)
    {
        return candidate.Kind == configuredProvider.Kind &&
               string.Equals(
                   NormalizeProviderBaseUrl(candidate.BaseUrl),
                   NormalizeProviderBaseUrl(configuredProvider.BaseUrl),
                   StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeProviderBaseUrl(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().TrimEnd('/');
    }

    private static bool IsScenarioHarnessProvider(ProviderProfile provider)
    {
        return provider.Tags.Any(tag => tag.Contains("scenario", StringComparison.OrdinalIgnoreCase)) ||
               provider.Name.Contains("Scenario Harness", StringComparison.OrdinalIgnoreCase);
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
            .Distinct(StringComparer.OrdinalIgnoreCase)
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

        const int maxImageAttachmentCount = 8;
        const long maxImageAttachmentBytes = 10 * 1024 * 1024;
        var attachments = new List<AgentRuntimeInputAttachment>();
        foreach (var requestedPath in requestedPaths)
        {
            if (!TryResolveImageContentType(requestedPath, out var contentType))
            {
                continue;
            }

            if (attachments.Count >= maxImageAttachmentCount)
            {
                throw new InvalidOperationException(
                    $"A single agent request can include at most {maxImageAttachmentCount:N0} image attachment(s).");
            }

            var resolved = workspacePathResolutionService.ResolveFilePath(requestedPath, allowMissing: false);
            var info = new FileInfo(resolved.FullPath);
            if (info.Length > maxImageAttachmentBytes)
            {
                throw new InvalidOperationException(
                    $"Image attachment '{resolved.RelativePath}' is {info.Length:N0} bytes, which exceeds the {maxImageAttachmentBytes:N0}-byte per-image limit.");
            }

            attachments.Add(new AgentRuntimeInputAttachment(
                Name: Path.GetFileName(resolved.FullPath),
                ContentType: contentType,
                Bytes: await File.ReadAllBytesAsync(resolved.FullPath, cancellationToken).ConfigureAwait(false),
                SourcePath: resolved.RelativePath));
        }

        return attachments;
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

    private static AgentRuntimeContextIntent CreateRuntimeContextIntent(ExecutionRunRecord run)
    {
        ArgumentNullException.ThrowIfNull(run);

        var workspaceScope = ExecutionInvocationMetadata.ResolveContextWorkspaceScope(run);
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
            ToolCapabilitiesEnabled = ExecutionInvocationMetadata.ResolveToolCapabilitiesEnabled(run)
        };
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
                throw new InvalidOperationException(message);
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
                throw new InvalidOperationException(message);
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
            throw new InvalidOperationException(validationMessage);
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

    private static bool IsGovernedMachineCriticalRun(ExecutionRunRecord run)
    {
        return string.Equals(run.SourceKind, "process-step", StringComparison.OrdinalIgnoreCase) ||
               !string.IsNullOrWhiteSpace(run.ProcessRunId) ||
               !string.IsNullOrWhiteSpace(run.ProcessStepId);
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
