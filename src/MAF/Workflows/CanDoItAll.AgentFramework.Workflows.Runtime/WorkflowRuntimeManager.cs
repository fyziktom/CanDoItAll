using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.AgentFramework.Core;

public sealed class WorkflowRuntimeManager : IWorkflowRuntimeManager
{
    private static readonly WorkflowRunState[] RunningState = [WorkflowRunState.Running];
    private static readonly WorkflowRunState[] WaitingState = [WorkflowRunState.WaitingForInput];

    private readonly IReadOnlyDictionary<WorkflowRuntimeBackendKind, IWorkflowExecutionBackend> backends;
    private readonly IWorkflowRunStore store;
    private readonly IWorkflowActiveRunRegistry activeRuns;
    private readonly TimeProvider timeProvider;
    private readonly IWorkflowEventSink eventSink;
    private readonly IWorkflowUsageObservationStore? usageStore;

    public WorkflowRuntimeManager(
        IEnumerable<IWorkflowExecutionBackend> backends,
        IWorkflowRunStore store,
        IWorkflowEventSink? eventSink = null,
        IWorkflowUsageObservationStore? usageStore = null)
        : this(
            backends,
            store,
            new WorkflowActiveRunRegistry(),
            TimeProvider.System,
            eventSink,
            usageStore)
    {
    }

    public WorkflowRuntimeManager(
        IEnumerable<IWorkflowExecutionBackend> backends,
        IWorkflowRunStore store,
        IWorkflowActiveRunRegistry activeRuns,
        TimeProvider timeProvider,
        IWorkflowEventSink? eventSink = null,
        IWorkflowUsageObservationStore? usageStore = null)
    {
        ArgumentNullException.ThrowIfNull(backends);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(activeRuns);
        ArgumentNullException.ThrowIfNull(timeProvider);

        this.backends = backends.ToDictionary(item => item.Descriptor.Kind);
        this.store = store;
        this.activeRuns = activeRuns;
        this.timeProvider = timeProvider;
        this.eventSink = eventSink ?? new NullWorkflowEventSink();
        this.usageStore = usageStore;
    }

    public async Task<WorkflowRunSnapshot> StartAsync(
        WorkflowDefinition definition,
        WorkflowRunStartRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(request);

        var requestedBackend = request.RequestedBackend ?? definition.RuntimePolicy.PreferredBackend;
        ValidateBackendPolicy(definition, requestedBackend);
        var backend = GetRequiredBackend(definition, requestedBackend);
        var runId = request.RequestedRunId ?? WorkflowRunId.New();
        var existingRequestedRun = await WorkflowRequestedRunRecovery.TryResolveAsync(store, request.RequestedRunId, definition, requestedBackend, cancellationToken);
        if (existingRequestedRun is not null)
        {
            return existingRequestedRun;
        }

        var now = timeProvider.GetUtcNow();
        var running = new WorkflowRunSnapshot(
            runId,
            definition.Id,
            definition.VersionId,
            WorkflowRunState.Running,
            requestedBackend,
            BackendRunId: runId.ToString(),
            Summary: $"Workflow '{definition.Name}' is running.",
            CreatedAtUtc: now,
            UpdatedAtUtc: now)
        {
            Origin = request.Origin
        };
        var startedEvent = CreateStartedEvent(definition, running, now);
        if (!activeRuns.TryRegister(
                runId,
                backend.Descriptor.SupportsActiveCancellation,
                cancellationToken,
                out var activeRun))
        {
            throw new InvalidOperationException($"Workflow run '{runId}' is already active.");
        }

        using (activeRun)
        {
            try
            {
                await store.CreateRunWithStartedEventAsync(running, startedEvent, cancellationToken);
            }
            catch (WorkflowRunAlreadyExistsException) when (request.RequestedRunId is not null)
            {
                return await WorkflowRequestedRunRecovery.ResolveRequiredAsync(store, runId, definition, requestedBackend, cancellationToken);
            }

            try
            {
                await eventSink.PublishAsync(startedEvent, cancellationToken);
            }
            catch (OperationCanceledException) when (activeRun.IsCancellationRequested)
            {
                return await FinalizeCancellationAsync(running, CancellationToken.None);
            }
            catch
            {
                await FinalizeFailureAsync(
                    running,
                    "Workflow start notification failed before backend invocation.",
                    CancellationToken.None);
                throw;
            }

            var progressObserver = new StoreBackedWorkflowNodeExecutionProgressObserver(
                running,
                store,
                usageStore,
                eventSink);
            try
            {
                WorkflowBackendStartResult result;
                using (WorkflowNodeExecutionProgressScope.Push(progressObserver))
                {
                    result = await backend.StartAsync(
                        definition,
                        request,
                        runId,
                        activeRun.Token);
                }

                if (!activeRun.TryClaimCompletion())
                {
                    return await FinalizeCancellationAsync(running, CancellationToken.None);
                }

                return await PersistBackendResultAsync(
                    result,
                    running,
                    RunningState,
                    CancellationToken.None);
            }
            catch (OperationCanceledException) when (activeRun.IsCancellationRequested)
            {
                return await FinalizeCancellationAsync(running, CancellationToken.None);
            }
            catch (Exception exception) when (WorkflowRuntimeTransitionRules.TryFindUsageObservationException(exception, out var usageException))
            {
                await PersistFailureUsageAndFinalizeAsync(
                    running,
                    exception,
                    usageException!,
                    CancellationToken.None);
                throw;
            }
            catch (Exception exception)
            {
                await FinalizeFailureAsync(
                    running,
                    WorkflowRuntimeTransitionRules.CreateSafeFailureSummary(exception),
                    CancellationToken.None);
                throw;
            }
        }
    }

    public Task<WorkflowRunSnapshot?> GetRunAsync(
        WorkflowRunId runId,
        CancellationToken cancellationToken = default)
        => store.GetRunAsync(runId, cancellationToken);

    public Task<IReadOnlyList<WorkflowRunSnapshot>> ListRunsAsync(
        WorkflowId? workflowId = null,
        CancellationToken cancellationToken = default)
        => store.ListRunsAsync(workflowId, cancellationToken);

    public Task<IReadOnlyList<WorkflowEventRecord>> ListEventsAsync(
        WorkflowRunId runId,
        CancellationToken cancellationToken = default)
        => store.ListEventsAsync(runId, cancellationToken);

    public Task<IReadOnlyList<WorkflowCheckpointRecord>> ListCheckpointsAsync(
        WorkflowRunId runId,
        CancellationToken cancellationToken = default)
        => store.ListCheckpointsAsync(runId, cancellationToken);

    public Task<WorkflowListPage<WorkflowEventRecord>> ListEventPageAsync(
        WorkflowEventPageRequest request,
        CancellationToken cancellationToken = default)
        => store.ListEventPageAsync(request, cancellationToken);

    public async Task<WorkflowRunSnapshot> CancelAsync(
        WorkflowRunId runId,
        CancellationToken cancellationToken = default)
    {
        var result = await RequestCancellationAsync(runId, cancellationToken);
        return result.Outcome switch
        {
            WorkflowRunCancellationOutcome.CancellationRequested or
            WorkflowRunCancellationOutcome.AlreadyTerminal => result.Run
                ?? throw new InvalidOperationException(result.Message),
            WorkflowRunCancellationOutcome.NotFound => throw new KeyNotFoundException(result.Message),
            _ => throw new InvalidOperationException(result.Message)
        };
    }

    public async Task<WorkflowRunCancellationResult> RequestCancellationAsync(
        WorkflowRunId runId,
        CancellationToken cancellationToken = default)
    {
        var run = await store.GetRunAsync(runId, cancellationToken);
        if (run is null)
        {
            return new WorkflowRunCancellationResult(
                WorkflowRunCancellationOutcome.NotFound,
                Run: null,
                $"Workflow run '{runId}' was not found.");
        }

        if (WorkflowRuntimeTransitionRules.IsTerminal(run.State))
        {
            return new WorkflowRunCancellationResult(
                WorkflowRunCancellationOutcome.AlreadyTerminal,
                run,
                $"Workflow run '{runId}' is already {run.State}.");
        }

        var signal = activeRuns.TrySignalCancellation(runId);
        return signal switch
        {
            WorkflowActiveRunCancellationSignal.Signalled => new WorkflowRunCancellationResult(
                WorkflowRunCancellationOutcome.CancellationRequested,
                run,
                $"Cancellation was requested for workflow run '{runId}'."),
            WorkflowActiveRunCancellationSignal.NotSupported => new WorkflowRunCancellationResult(
                WorkflowRunCancellationOutcome.BackendNotCancellable,
                run,
                $"Workflow runtime backend '{run.Backend}' does not support active cancellation."),
            _ => new WorkflowRunCancellationResult(
                WorkflowRunCancellationOutcome.NotActive,
                run,
                $"Workflow run '{runId}' is not active in this runtime host and was not cancelled.")
        };
    }

    public async Task<WorkflowRunSnapshot> RespondToExternalRequestAsync(
        WorkflowExternalRequestId requestId,
        string responseJson,
        CancellationToken cancellationToken = default)
    {
        var result = await SubmitExternalResponseAsync(requestId, responseJson, cancellationToken);
        return result.Outcome switch
        {
            WorkflowExternalResponseOutcome.Accepted or
            WorkflowExternalResponseOutcome.UnsupportedResume => result.Run
                ?? throw new InvalidOperationException(result.Message),
            WorkflowExternalResponseOutcome.RequestNotFound => throw new KeyNotFoundException(result.Message),
            WorkflowExternalResponseOutcome.RunNotFound => throw new KeyNotFoundException(result.Message),
            WorkflowExternalResponseOutcome.AlreadyResponded => throw new InvalidOperationException(result.Message),
            WorkflowExternalResponseOutcome.RunNotWaiting => throw new InvalidOperationException(result.Message),
            WorkflowExternalResponseOutcome.BackendUnavailable => throw new InvalidOperationException(result.Message),
            WorkflowExternalResponseOutcome.ResumeFailed => throw new InvalidOperationException(result.Message),
            _ => throw new InvalidOperationException(result.Message)
        };
    }

    public async Task<WorkflowExternalResponseResult> SubmitExternalResponseAsync(
        WorkflowExternalRequestId requestId,
        string responseJson,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(responseJson);
        var request = await store.GetExternalRequestAsync(requestId, cancellationToken);
        if (request is null)
        {
            return new WorkflowExternalResponseResult(
                WorkflowExternalResponseOutcome.RequestNotFound,
                Run: null,
                Request: null,
                $"Workflow external request '{requestId}' was not found.");
        }

        if (request.RespondedAtUtc.HasValue)
        {
            return new WorkflowExternalResponseResult(
                WorkflowExternalResponseOutcome.AlreadyResponded,
                Run: null,
                request,
                $"Workflow external request '{requestId}' has already been answered.");
        }

        var run = await store.GetRunAsync(request.RunId, cancellationToken);
        if (run is null)
        {
            return new WorkflowExternalResponseResult(
                WorkflowExternalResponseOutcome.RunNotFound,
                Run: null,
                request,
                $"Workflow run '{request.RunId}' was not found.");
        }

        if (run.State != WorkflowRunState.WaitingForInput)
        {
            return new WorkflowExternalResponseResult(
                WorkflowExternalResponseOutcome.RunNotWaiting,
                run,
                request,
                $"Workflow run '{run.RunId}' is not waiting for external input.");
        }

        WorkflowRuntimeTransitionRules.ValidateExternalResponse(request, responseJson);
        if (!backends.TryGetValue(run.Backend, out var backend))
        {
            return new WorkflowExternalResponseResult(
                WorkflowExternalResponseOutcome.BackendUnavailable,
                run,
                request,
                $"Workflow runtime backend '{run.Backend}' is not registered.");
        }

        if (!backend.Descriptor.SupportsExternalResponseResume || backend is not IWorkflowExternalResponseBackend resumeBackend)
        {
            return new WorkflowExternalResponseResult(
                WorkflowExternalResponseOutcome.UnsupportedResume,
                run,
                request,
                $"Workflow runtime backend '{run.Backend}' cannot resume external requests; the run remains waiting.");
        }

        if (!activeRuns.TryRegister(
                run.RunId,
                backend.Descriptor.SupportsActiveCancellation,
                cancellationToken,
                out var activeRun))
        {
            return new WorkflowExternalResponseResult(
                WorkflowExternalResponseOutcome.TransitionRejected,
                run,
                request,
                $"Workflow run '{run.RunId}' is already active.");
        }

        using (activeRun)
        {
            var accepted = await store.TryAcceptExternalResponseAsync(
                requestId,
                responseJson,
                timeProvider.GetUtcNow(),
                cancellationToken);
            if (accepted.Outcome != WorkflowExternalResponseAcceptanceOutcome.Accepted)
            {
                return new WorkflowExternalResponseResult(
                    accepted.Outcome == WorkflowExternalResponseAcceptanceOutcome.NotFound
                        ? WorkflowExternalResponseOutcome.RequestNotFound
                        : WorkflowExternalResponseOutcome.AlreadyResponded,
                    run,
                    accepted.Request,
                    accepted.Outcome == WorkflowExternalResponseAcceptanceOutcome.NotFound
                        ? $"Workflow external request '{requestId}' was not found."
                        : $"Workflow external request '{requestId}' has already been answered.");
            }

            var progressObserver = new StoreBackedWorkflowNodeExecutionProgressObserver(
                run,
                store,
                usageStore,
                eventSink);
            try
            {
                WorkflowBackendStartResult result;
                using (WorkflowNodeExecutionProgressScope.Push(progressObserver))
                {
                    result = await resumeBackend.ResumeAsync(
                        run,
                        accepted.Request!,
                        responseJson,
                        activeRun.Token);
                }

                var persisted = !activeRun.TryClaimCompletion()
                    ? await FinalizeCancellationAsync(run, CancellationToken.None)
                    : await PersistBackendResultAsync(
                        result,
                        run,
                        WaitingState,
                        CancellationToken.None);
                return new WorkflowExternalResponseResult(
                    WorkflowExternalResponseOutcome.Accepted,
                    persisted,
                    accepted.Request,
                    "Workflow external response was accepted exactly once.");
            }
            catch (OperationCanceledException) when (activeRun.IsCancellationRequested)
            {
                var cancelled = await FinalizeCancellationAsync(run, CancellationToken.None);
                return new WorkflowExternalResponseResult(
                    WorkflowExternalResponseOutcome.Accepted,
                    cancelled,
                    accepted.Request,
                    "Workflow external response was accepted and resumed execution was cancelled.");
            }
            catch (Exception exception) when (WorkflowRuntimeTransitionRules.TryFindUsageObservationException(exception, out var usageException))
            {
                var failed = await PersistFailureUsageAndFinalizeAsync(
                    run,
                    exception,
                    usageException!,
                    CancellationToken.None);
                return new WorkflowExternalResponseResult(
                    WorkflowExternalResponseOutcome.ResumeFailed,
                    failed,
                    accepted.Request,
                    "Workflow external response was accepted, but backend resume failed.");
            }
            catch (Exception exception)
            {
                var failed = await FinalizeFailureAsync(
                    run,
                    WorkflowRuntimeTransitionRules.CreateSafeFailureSummary(exception),
                    CancellationToken.None);
                return new WorkflowExternalResponseResult(
                    WorkflowExternalResponseOutcome.ResumeFailed,
                    failed,
                    accepted.Request,
                    "Workflow external response was accepted, but backend resume failed.");
            }
        }
    }

    private void ValidateBackendPolicy(
        WorkflowDefinition definition,
        WorkflowRuntimeBackendKind requestedBackend)
    {
        if (requestedBackend == WorkflowRuntimeBackendKind.InProcess &&
            definition.RuntimePolicy.RequireDurableProductionRuns &&
            !definition.RuntimePolicy.AllowInProcessPreviewRuns)
        {
            throw WorkflowRuntimeFailureDiagnosticMapper.CreateDurableBackendRequiredException(
                definition,
                requestedBackend,
                $"Workflow '{definition.Id}' requires a durable production runtime and does not allow in-process preview runs.");
        }
    }

    private IWorkflowExecutionBackend GetRequiredBackend(
        WorkflowDefinition definition,
        WorkflowRuntimeBackendKind requestedBackend)
        => backends.TryGetValue(requestedBackend, out var backend)
            ? backend
            : throw WorkflowRuntimeFailureDiagnosticMapper.CreateBackendUnavailableException(
                definition,
                requestedBackend,
                $"Workflow runtime backend '{requestedBackend}' is not registered. Configure a backend explicitly instead of falling back silently.");

    private async Task<WorkflowRunSnapshot> PersistBackendResultAsync(
        WorkflowBackendStartResult result,
        WorkflowRunSnapshot sourceRun,
        IReadOnlyCollection<WorkflowRunState> expectedStates,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(result);
        var runId = result.Run.RunId;
        if (runId != sourceRun.RunId ||
            result.Run.WorkflowId != sourceRun.WorkflowId ||
            result.Run.VersionId != sourceRun.VersionId)
        {
            throw new InvalidOperationException(
                $"Workflow backend returned run '{runId}' while executing '{sourceRun.RunId}'.");
        }

        await AppendUsageObservationsAsync(
            sourceRun,
            result.UsageObservations,
            cancellationToken);

        var backendTransitionEvent = WorkflowRuntimeTransitionRules.FindBackendTransitionEvent(
            result.Run.State,
            result.Events);
        var existingEvents = await store.ListEventsAsync(runId, cancellationToken);
        foreach (var workflowEvent in result.Events)
        {
            if ((backendTransitionEvent is not null && workflowEvent.Id == backendTransitionEvent.Id) ||
                IsLifecycleEvent(workflowEvent) ||
                IsDuplicateProgressEvent(existingEvents, workflowEvent))
            {
                continue;
            }

            await PublishAndStoreEventAsync(workflowEvent, cancellationToken);
        }

        foreach (var request in result.ExternalRequests)
        {
            await store.SaveExternalRequestAsync(request, cancellationToken);
        }

        foreach (var checkpoint in result.Checkpoints)
        {
            await store.SaveCheckpointAsync(checkpoint, cancellationToken);
        }

        foreach (var artifact in result.Artifacts)
        {
            await store.SaveArtifactAsync(artifact, cancellationToken);
        }

        var updatedAtUtc = timeProvider.GetUtcNow();
        var updated = result.Run with
        {
            CreatedAtUtc = sourceRun.CreatedAtUtc,
            UpdatedAtUtc = updatedAtUtc,
            TerminalAtUtc = WorkflowRuntimeTransitionRules.IsTerminal(result.Run.State) ? updatedAtUtc : null,
            Origin = result.Run.Origin ?? sourceRun.Origin
        };
        var transitionEvent = backendTransitionEvent ?? CreateStateTransitionEvent(updated);
        var transition = await store.TryTransitionRunAsync(
            runId,
            expectedStates,
            updated,
            transitionEvent,
            cancellationToken);
        if (transition.Transitioned && transitionEvent is not null)
        {
            await eventSink.PublishAsync(transitionEvent, cancellationToken);
        }

        return transition.Run ?? updated;
    }

    private async Task AppendUsageObservationsAsync(
        WorkflowRunSnapshot run,
        IReadOnlyList<WorkflowUsageObservation> observations,
        CancellationToken cancellationToken)
    {
        if (observations.Count == 0)
        {
            return;
        }

        if (usageStore is null)
        {
            throw new InvalidOperationException(
                "Workflow usage observations were produced, but no usage observation store is configured.");
        }

        var correlatedObservations = observations
            .Select(observation => WorkflowUsageObservationCorrelator.Correlate(
                observation,
                run.RunId,
                run.WorkflowId,
                run.VersionId,
                run.Origin))
            .ToArray();
        await usageStore.AppendRangeAsync(correlatedObservations, cancellationToken);
    }

    private async Task<WorkflowRunSnapshot> PersistFailureUsageAndFinalizeAsync(
        WorkflowRunSnapshot run,
        Exception backendException,
        WorkflowUsageObservationException usageException,
        CancellationToken cancellationToken)
    {
        try
        {
            await AppendUsageObservationsAsync(run, usageException.Observations, cancellationToken);
        }
        catch (Exception persistenceException)
        {
            await FinalizeFailureAsync(
                run,
                WorkflowRuntimeTransitionRules.CreateSafeFailureSummary(persistenceException),
                cancellationToken);
            throw new InvalidOperationException(
                $"Workflow run '{run.RunId}' failed and its usage observations could not be persisted.",
                new AggregateException(backendException, persistenceException));
        }

        return await FinalizeFailureAsync(
            run,
            WorkflowRuntimeTransitionRules.CreateSafeFailureSummary(backendException),
            cancellationToken);
    }

    private async Task<WorkflowRunSnapshot> FinalizeCancellationAsync(
        WorkflowRunSnapshot sourceRun,
        CancellationToken cancellationToken)
    {
        var current = await store.GetRunAsync(sourceRun.RunId, cancellationToken) ?? sourceRun;
        if (WorkflowRuntimeTransitionRules.IsTerminal(current.State))
        {
            return current;
        }

        var now = timeProvider.GetUtcNow();
        var cancelled = current with
        {
            State = WorkflowRunState.Cancelled,
            Summary = "Workflow run was cancelled.",
            UpdatedAtUtc = now,
            TerminalAtUtc = now
        };
        var diagnostic = WorkflowRuntimeFailureDiagnosticMapper.Cancelled(cancelled, now);
        var workflowEvent = new WorkflowEventRecord(
            Guid.NewGuid(),
            cancelled.RunId,
            WorkflowEventKind.Cancelled,
            NodeId: null,
            cancelled.Summary,
            WorkflowEventPayloads.Serialize(
                WorkflowEventPayloadSource.Runtime,
                "WorkflowCancelled",
                inlineJson: WorkflowRuntimeFailureDiagnosticMapper.Serialize(diagnostic)),
            now);
        var transition = await store.TryTransitionRunAsync(
            cancelled.RunId,
            [current.State],
            cancelled,
            workflowEvent,
            cancellationToken);
        if (transition.Transitioned)
        {
            await eventSink.PublishAsync(workflowEvent, cancellationToken);
        }

        return transition.Run ?? cancelled;
    }

    private async Task<WorkflowRunSnapshot> FinalizeFailureAsync(
        WorkflowRunSnapshot sourceRun,
        string summary,
        CancellationToken cancellationToken)
    {
        var current = await store.GetRunAsync(sourceRun.RunId, cancellationToken) ?? sourceRun;
        if (WorkflowRuntimeTransitionRules.IsTerminal(current.State))
        {
            return current;
        }

        var now = timeProvider.GetUtcNow();
        var failed = current with
        {
            State = WorkflowRunState.Failed,
            Summary = WorkflowRuntimeTransitionRules.NormalizeSummary(summary),
            UpdatedAtUtc = now,
            TerminalAtUtc = now
        };
        var workflowEvent = new WorkflowEventRecord(
            Guid.NewGuid(),
            failed.RunId,
            WorkflowEventKind.Error,
            NodeId: null,
            failed.Summary,
            WorkflowEventPayloads.Serialize(
                WorkflowEventPayloadSource.Runtime,
                "WorkflowBackendFailed"),
            now);
        var transition = await store.TryTransitionRunAsync(
            failed.RunId,
            [current.State],
            failed,
            workflowEvent,
            cancellationToken);
        if (transition.Transitioned)
        {
            await eventSink.PublishAsync(workflowEvent, cancellationToken);
        }

        return transition.Run ?? failed;
    }

    private static WorkflowEventRecord CreateStartedEvent(
        WorkflowDefinition definition,
        WorkflowRunSnapshot run,
        DateTimeOffset occurredAtUtc)
        => new(
            Guid.NewGuid(),
            run.RunId,
            WorkflowEventKind.Started,
            NodeId: null,
            $"Workflow '{definition.Name}' started.",
            WorkflowEventPayloads.Serialize(
                WorkflowEventPayloadSource.Runtime,
                "WorkflowStarted"),
            occurredAtUtc);

    private WorkflowEventRecord? CreateStateTransitionEvent(WorkflowRunSnapshot run)
    {
        var kind = run.State switch
        {
            WorkflowRunState.Completed => WorkflowEventKind.Completed,
            WorkflowRunState.Failed => WorkflowEventKind.Error,
            WorkflowRunState.Cancelled => WorkflowEventKind.Cancelled,
            WorkflowRunState.WaitingForInput => WorkflowEventKind.WaitingForInput,
            _ => (WorkflowEventKind?)null
        };
        return kind.HasValue
            ? new WorkflowEventRecord(
                Guid.NewGuid(),
                run.RunId,
                kind.Value,
                NodeId: null,
                run.Summary,
                WorkflowEventPayloads.Serialize(
                    WorkflowEventPayloadSource.Runtime,
                    $"Workflow{run.State}"),
                timeProvider.GetUtcNow())
            : null;
    }

    private static bool IsLifecycleEvent(WorkflowEventRecord workflowEvent)
        => workflowEvent.Kind == WorkflowEventKind.Started ||
           workflowEvent.NodeId is null && workflowEvent.Kind is
               WorkflowEventKind.Completed or
               WorkflowEventKind.Cancelled or
               WorkflowEventKind.WaitingForInput or
               WorkflowEventKind.Error;

    private static bool IsDuplicateProgressEvent(
        IReadOnlyList<WorkflowEventRecord> existingEvents,
        WorkflowEventRecord candidate)
        => (candidate.Kind is
               WorkflowEventKind.ExecutorInvoked or
               WorkflowEventKind.ExecutorCompleted or
               WorkflowEventKind.ExecutorFailed) &&
           candidate.NodeId.HasValue &&
           existingEvents.Any(workflowEvent =>
               workflowEvent.Kind == candidate.Kind &&
               workflowEvent.NodeId == candidate.NodeId &&
               workflowEvent.CreatedAtUtc == candidate.CreatedAtUtc);

    private async Task PublishAndStoreEventAsync(
        WorkflowEventRecord workflowEvent,
        CancellationToken cancellationToken)
    {
        await store.SaveEventAsync(workflowEvent, cancellationToken);
        await eventSink.PublishAsync(workflowEvent, cancellationToken);
    }

}
