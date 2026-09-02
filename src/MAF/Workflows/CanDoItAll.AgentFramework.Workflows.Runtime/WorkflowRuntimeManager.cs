using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.AgentFramework.Core;

public sealed class WorkflowRuntimeManager : IWorkflowRuntimeManager
{
    private static readonly WorkflowRunState[] RunningState = [WorkflowRunState.Running];
    private readonly IReadOnlyDictionary<WorkflowRuntimeBackendKind, IWorkflowExecutionBackend> backends;
    private readonly IWorkflowRunStore store;
    private readonly IWorkflowActiveRunRegistry activeRuns;
    private readonly TimeProvider timeProvider;
    private readonly IWorkflowEventSink eventSink;
    private readonly IWorkflowUsageObservationStore? usageStore;
    private readonly IWorkflowExternalRequestBoundaryStore requestBoundaryStore;
    private readonly IWorkflowResumeBoundaryStore resumeBoundaryStore;

    public static WorkflowRuntimeManager CreateInMemory(
        IEnumerable<IWorkflowExecutionBackend> backends,
        IWorkflowRunStore store,
        IWorkflowEventSink? eventSink = null,
        IWorkflowUsageObservationStore? usageStore = null)
        => WorkflowInMemoryRuntimeFactory.Create(
            backends,
            store,
            eventSink,
            usageStore);

    public static WorkflowRuntimeManager CreateInMemory(
        IEnumerable<IWorkflowExecutionBackend> backends,
        InMemoryWorkflowRunStore store,
        InMemoryWorkflowBackendCheckpointPayloadStore checkpointPayloadStore,
        IWorkflowEventSink? eventSink = null,
        InMemoryWorkflowUsageObservationStore? usageStore = null)
        => WorkflowInMemoryRuntimeFactory.Create(
            backends,
            store,
            checkpointPayloadStore,
            eventSink,
            usageStore);

    public static WorkflowRuntimeManager CreateInMemory(
        IEnumerable<IWorkflowExecutionBackend> backends,
        IWorkflowRunStore store,
        IWorkflowActiveRunRegistry activeRuns,
        TimeProvider timeProvider,
        IWorkflowEventSink? eventSink = null,
        IWorkflowUsageObservationStore? usageStore = null)
        => WorkflowInMemoryRuntimeFactory.Create(
            backends,
            store,
            activeRuns,
            timeProvider,
            eventSink,
            usageStore);

    public static WorkflowRuntimeManager CreateInMemory(
        IEnumerable<IWorkflowExecutionBackend> backends,
        InMemoryWorkflowRunStore store,
        InMemoryWorkflowBackendCheckpointPayloadStore checkpointPayloadStore,
        IWorkflowActiveRunRegistry activeRuns,
        TimeProvider timeProvider,
        IWorkflowEventSink? eventSink = null,
        InMemoryWorkflowUsageObservationStore? usageStore = null)
        => WorkflowInMemoryRuntimeFactory.Create(
            backends,
            store,
            checkpointPayloadStore,
            activeRuns,
            timeProvider,
            eventSink,
            usageStore);

    public WorkflowRuntimeManager(
        IEnumerable<IWorkflowExecutionBackend> backends,
        IWorkflowRunStore store,
        IWorkflowActiveRunRegistry activeRuns,
        TimeProvider timeProvider,
        IWorkflowExternalRequestBoundaryStore requestBoundaryStore,
        IWorkflowResumeBoundaryStore resumeBoundaryStore,
        IWorkflowEventSink? eventSink = null,
        IWorkflowUsageObservationStore? usageStore = null)
    {
        ArgumentNullException.ThrowIfNull(backends);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(activeRuns);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(requestBoundaryStore);
        ArgumentNullException.ThrowIfNull(resumeBoundaryStore);

        this.backends = backends.ToDictionary(item => item.Descriptor.Kind);
        this.store = store;
        this.activeRuns = activeRuns;
        this.timeProvider = timeProvider;
        this.requestBoundaryStore = requestBoundaryStore;
        this.resumeBoundaryStore = resumeBoundaryStore;
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
            catch (OperationCanceledException exception) when (activeRun.IsCancellationRequested)
            {
                if (WorkflowRuntimeTransitionRules.TryFindUsageObservationException(exception, out var usageException)) {
                    await AppendUsageObservationsAsync(running, usageException!.Observations, CancellationToken.None);
                }
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
        if (signal == WorkflowActiveRunCancellationSignal.Signalled)
        {
            return new WorkflowRunCancellationResult(
                WorkflowRunCancellationOutcome.CancellationRequested,
                run,
                $"Cancellation was requested for workflow run '{runId}'.");
        }

        if (signal == WorkflowActiveRunCancellationSignal.NotSupported)
        {
            return new WorkflowRunCancellationResult(
                WorkflowRunCancellationOutcome.BackendNotCancellable,
                run,
                $"Workflow runtime backend '{run.Backend}' does not support active cancellation.");
        }

        if (run.State == WorkflowRunState.WaitingForInput)
        {
            var pendingRequests = await store.ListPendingExternalRequestsAsync(runId, cancellationToken);
            var pending = pendingRequests
                .OrderBy(request => request.CreatedAtUtc)
                .ThenBy(request => request.Id.Value)
                .FirstOrDefault();
            if (pending is not null)
            {
                var boundary = await requestBoundaryStore.ReadAsync(pending.Id, cancellationToken);
                if (boundary.Succeeded && boundary.Boundary is not null)
                {
                    var cancellation = await resumeBoundaryStore.TryCancelAsync(
                        new WorkflowResumeBoundaryCancellationRequest(
                            runId,
                            pending.Id,
                            boundary.Boundary.RequestVersion,
                            timeProvider.GetUtcNow(),
                            "Workflow run was cancelled while waiting for external input."),
                        cancellationToken);
                    return cancellation.Outcome switch
                    {
                        WorkflowResumeBoundaryCancellationOutcome.Cancelled => new WorkflowRunCancellationResult(
                            WorkflowRunCancellationOutcome.CancellationRequested,
                            cancellation.Run,
                            $"Workflow run '{runId}' was cancelled while waiting for external input."),
                        WorkflowResumeBoundaryCancellationOutcome.AlreadyTerminal => new WorkflowRunCancellationResult(
                            WorkflowRunCancellationOutcome.AlreadyTerminal,
                            cancellation.Run,
                            $"Workflow run '{runId}' is already terminal."),
                        WorkflowResumeBoundaryCancellationOutcome.ActiveResume => new WorkflowRunCancellationResult(
                            WorkflowRunCancellationOutcome.NotActive,
                            cancellation.Run ?? run,
                            $"Workflow run '{runId}' is being resumed by another durable lease owner."),
                        _ => new WorkflowRunCancellationResult(
                            WorkflowRunCancellationOutcome.TransitionRejected,
                            cancellation.Run ?? run,
                            $"Workflow waiting-run cancellation was rejected: {cancellation.Outcome}.")
                    };
                }

                var hasOnlyExplicitLegacyRequests =
                    boundary.Outcome == WorkflowExternalRequestBoundaryReadOutcome.LegacyNonResumable &&
                    pendingRequests.All(IsExplicitLegacyRequest);
                if (!hasOnlyExplicitLegacyRequests)
                {
                    return new WorkflowRunCancellationResult(
                        WorkflowRunCancellationOutcome.TransitionRejected,
                        run,
                        $"Workflow run '{runId}' has a native external request whose durable response boundary is unavailable.");
                }

                var now = timeProvider.GetUtcNow();
                foreach (var legacyRequest in pendingRequests)
                {
                    await store.SaveExternalRequestAsync(
                        legacyRequest with
                        {
                            State = WorkflowExternalRequestState.Cancelled,
                            RespondedAtUtc = now
                        },
                        cancellationToken);
                }
            }

            var cancelled = await FinalizeCancellationAsync(run, cancellationToken);
            return new WorkflowRunCancellationResult(
                WorkflowRunCancellationOutcome.CancellationRequested,
                cancelled,
                $"Workflow run '{runId}' was cancelled while waiting for external input.");
        }

        return signal switch
        {
            _ => new WorkflowRunCancellationResult(
                WorkflowRunCancellationOutcome.NotActive,
                run,
                $"Workflow run '{runId}' is not active in this runtime host and was not cancelled.")
        };
    }

    private static bool IsExplicitLegacyRequest(WorkflowExternalRequestRecord request)
        => request.State == WorkflowExternalRequestState.LegacyNonResumable &&
           request.ResponseContract is null &&
           request.Continuation is null;

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
            if (WorkflowExternalRequestBoundaryRecord.TryCreate(request, out var boundary) &&
                boundary is not null)
            {
                var savedBoundary = await requestBoundaryStore.UpsertAsync(boundary, cancellationToken);
                if (!savedBoundary.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"Workflow external request '{request.Id}' native boundary was not persisted: {savedBoundary.Outcome}.");
                }
            }
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
