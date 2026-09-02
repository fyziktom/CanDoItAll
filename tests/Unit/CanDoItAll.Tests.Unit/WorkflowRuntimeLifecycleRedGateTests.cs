using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class WorkflowRuntimeLifecycleRedGateTests
{
    private static readonly DateTimeOffset FixedUtcNow = new(2026, 7, 12, 12, 0, 0, TimeSpan.Zero);
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    [Fact]
    public async Task StartPersistsRunningAndStartedBeforeBackendRelease()
    {
        var timeProvider = new FixedTimeProvider(FixedUtcNow);
        var store = new ControllableWorkflowRunStore();
        var backend = new ControllableWorkflowBackend(timeProvider, waitForRelease: true);
        var manager = WorkflowRuntimeManager.CreateInMemory(
            [backend],
            store,
            new WorkflowActiveRunRegistry(),
            timeProvider);
        var definition = CreateDefinition();

        var startTask = manager.StartAsync(definition, CreateStartRequest(definition));
        var invocation = await backend.Entered;
        var persistedBeforeRelease = await manager.GetRunAsync(invocation.RunId);
        var eventsBeforeRelease = await manager.ListEventsAsync(invocation.RunId);
        backend.Release();
        var completed = await startTask;

        Assert.NotNull(persistedBeforeRelease);
        Assert.Equal(WorkflowRunState.Running, persistedBeforeRelease.State);
        Assert.Equal(FixedUtcNow, persistedBeforeRelease.CreatedAtUtc);
        Assert.Contains(eventsBeforeRelease, workflowEvent => workflowEvent.Kind == WorkflowEventKind.Started);
        Assert.Equal(FixedUtcNow, completed.TerminalAtUtc);
    }

    [Fact]
    public async Task InitialPersistenceFailurePreventsBackendInvocation()
    {
        var store = new ControllableWorkflowRunStore
        {
            FailFirstRunSave = true
        };
        var backend = new ControllableWorkflowBackend(
            new FixedTimeProvider(FixedUtcNow),
            waitForRelease: false);
        var composition = WorkflowExternalResponseTestCompositionFactory.CreateLegacyCompatibility(
            [backend],
            store,
            new FixedTimeProvider(FixedUtcNow));
        var manager = composition.Manager;
        var definition = CreateDefinition();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            manager.StartAsync(definition, CreateStartRequest(definition)));

        var runId = Assert.IsType<WorkflowRunId>(store.LastInitialRunId);
        Assert.Equal(0, backend.InvocationCount);
        Assert.Null(await store.GetRunAsync(runId));
        Assert.Empty(await store.ListEventsAsync(runId));
    }

    [Fact]
    public async Task StartedEventPersistenceFailurePreventsBackendInvocation()
    {
        var store = new ControllableWorkflowRunStore
        {
            FailFirstEventSave = true
        };
        var backend = new ControllableWorkflowBackend(
            new FixedTimeProvider(FixedUtcNow),
            waitForRelease: false);
        var composition = WorkflowExternalResponseTestCompositionFactory.CreateLegacyCompatibility(
            [backend],
            store,
            new FixedTimeProvider(FixedUtcNow));
        var manager = composition.Manager;
        var definition = CreateDefinition();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            manager.StartAsync(definition, CreateStartRequest(definition)));

        var runId = Assert.IsType<WorkflowRunId>(store.LastInitialRunId);
        Assert.Equal(0, backend.InvocationCount);
        Assert.Null(await store.GetRunAsync(runId));
        Assert.Empty(await store.ListEventsAsync(runId));
    }

    [Fact]
    public async Task BackendFailurePreservesProgressAndPersistsFailedRun()
    {
        var timeProvider = new FixedTimeProvider(FixedUtcNow);
        var store = new ControllableWorkflowRunStore();
        var backend = new ControllableWorkflowBackend(
            timeProvider,
            waitForRelease: false,
            throwAfterProgress: true,
            progressWriter: async (runId, cancellationToken) =>
            {
                var observer = WorkflowNodeExecutionProgressScope.Current
                    ?? throw new InvalidOperationException("Workflow runtime did not install an incremental progress observer.");
                await observer.RecordAsync(
                    new WorkflowNodeExecutionProgress(
                        WorkflowId.New(),
                        WorkflowVersionId.New(),
                        runId,
                        new WorkflowNodeId("work"),
                        WorkflowNodeExecutionProgressState.Started,
                        timeProvider.GetUtcNow()),
                    cancellationToken);
            });
        var manager = WorkflowRuntimeManager.CreateInMemory([backend], store);
        var definition = CreateDefinition();

        _ = await Record.ExceptionAsync(() =>
            manager.StartAsync(definition, CreateStartRequest(definition)));
        var runId = Assert.IsType<WorkflowRunId>(backend.LastRunId);
        var persisted = await manager.GetRunAsync(runId);
        var events = await manager.ListEventsAsync(runId);

        Assert.NotNull(persisted);
        Assert.Equal(WorkflowRunState.Failed, persisted.State);
        Assert.Contains(events, workflowEvent => workflowEvent.Kind == WorkflowEventKind.ExecutorInvoked);
        Assert.Contains(events, workflowEvent => workflowEvent.Kind == WorkflowEventKind.Error);
    }

    [Fact]
    public async Task ActiveCancellationSignalsBackendToken()
    {
        var store = new ControllableWorkflowRunStore();
        var backend = new ControllableWorkflowBackend(
            new FixedTimeProvider(FixedUtcNow),
            waitForRelease: true);
        var manager = WorkflowRuntimeManager.CreateInMemory([backend], store);
        var definition = CreateDefinition();

        var startTask = manager.StartAsync(definition, CreateStartRequest(definition));
        var invocation = await backend.Entered;
        WorkflowRunCancellationResult? cancellationResult = null;
        var cancellationException = await Record.ExceptionAsync(async () =>
            cancellationResult = await manager.RequestCancellationAsync(invocation.RunId));
        if (cancellationException is null)
        {
            await backend.CancellationObserved;
        }
        else
        {
            backend.Release();
        }

        _ = await Record.ExceptionAsync(async () => await startTask);
        var persisted = await manager.GetRunAsync(invocation.RunId);
        var cancellationEvent = Assert.Single(
            await manager.ListEventsAsync(invocation.RunId),
            workflowEvent => workflowEvent.Kind == WorkflowEventKind.Cancelled);
        var payload = JsonSerializer.Deserialize<WorkflowEventPayloadEnvelope>(
            cancellationEvent.PayloadJson,
            JsonOptions)!;
        var diagnostic = JsonSerializer.Deserialize<WorkflowFailureDiagnosticEnvelope>(
            payload.InlineJson,
            JsonOptions)!;

        Assert.Null(cancellationException);
        Assert.Equal(WorkflowRunCancellationOutcome.CancellationRequested, cancellationResult?.Outcome);
        Assert.True(backend.CancellationWasObserved);
        Assert.NotNull(persisted);
        Assert.Equal(WorkflowRunState.Cancelled, persisted.State);
        Assert.Equal(WorkflowFailureKind.Cancellation, diagnostic.Kind);
        Assert.Equal(invocation.RunId, diagnostic.RunId);
        Assert.Equal(WorkflowFailureRetryability.NotRetryable, diagnostic.Retryability);
    }

    [Fact]
    public async Task LateBackendCompletionCannotOverwriteCancelled()
    {
        var store = new ControllableWorkflowRunStore();
        var backend = new ControllableWorkflowBackend(
            new FixedTimeProvider(FixedUtcNow),
            waitForRelease: true,
            ignoreCancellationForRace: true);
        var manager = WorkflowRuntimeManager.CreateInMemory([backend], store);
        var definition = CreateDefinition();

        var startTask = manager.StartAsync(definition, CreateStartRequest(definition));
        var invocation = await backend.Entered;
        WorkflowRunCancellationResult? cancellationResult = null;
        var cancellationException = await Record.ExceptionAsync(async () =>
            cancellationResult = await manager.RequestCancellationAsync(invocation.RunId));
        if (cancellationException is null)
        {
            await backend.CancellationObserved;
        }

        backend.Release();
        _ = await Record.ExceptionAsync(async () => await startTask);
        var persisted = await manager.GetRunAsync(invocation.RunId);

        Assert.Null(cancellationException);
        Assert.Equal(WorkflowRunCancellationOutcome.CancellationRequested, cancellationResult?.Outcome);
        Assert.True(backend.CancellationWasObserved);
        Assert.NotNull(persisted);
        Assert.Equal(WorkflowRunState.Cancelled, persisted.State);
    }

    [Fact]
    public async Task CallerCancellationWinsRaceWhenBackendIgnoresTokenAndDisablesOutOfBandCancellation()
    {
        using var callerCancellation = new CancellationTokenSource();
        var store = new ControllableWorkflowRunStore();
        var backend = new ControllableWorkflowBackend(
            new FixedTimeProvider(FixedUtcNow),
            waitForRelease: true,
            ignoreCancellationForRace: true,
            supportsActiveCancellation: false);
        var manager = WorkflowRuntimeManager.CreateInMemory([backend], store);
        var definition = CreateDefinition();

        var startTask = manager.StartAsync(
            definition,
            CreateStartRequest(definition),
            callerCancellation.Token);
        var invocation = await backend.Entered;
        callerCancellation.Cancel();
        await backend.CancellationObserved;
        backend.Release();

        var result = await startTask;
        var persisted = await manager.GetRunAsync(invocation.RunId);

        Assert.Equal(WorkflowRunState.Cancelled, result.State);
        Assert.NotNull(persisted);
        Assert.Equal(WorkflowRunState.Cancelled, persisted.State);
    }

    [Fact]
    public async Task NonActiveCancellationDoesNotFabricateCancelledState()
    {
        var store = new ControllableWorkflowRunStore();
        var manager = WorkflowRuntimeManager.CreateInMemory([], store);
        var run = CreateRun(WorkflowRunState.Running, WorkflowRuntimeBackendKind.DurableTask);
        await store.SaveRunAsync(run);

        var result = await manager.RequestCancellationAsync(run.RunId);
        var compatibilityException = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            manager.CancelAsync(run.RunId));
        var persisted = await manager.GetRunAsync(run.RunId);
        var events = await manager.ListEventsAsync(run.RunId);

        Assert.Equal(WorkflowRunCancellationOutcome.NotActive, result.Outcome);
        Assert.Contains("not active", compatibilityException.Message, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(persisted);
        Assert.Equal(WorkflowRunState.Running, persisted.State);
        Assert.DoesNotContain(events, workflowEvent => workflowEvent.Kind == WorkflowEventKind.Cancelled);
    }

    [Fact]
    public void CancellationContractExposesTypedCapabilityOutcome()
    {
        var method = typeof(IWorkflowRuntimeManager).GetMethod(
            nameof(IWorkflowRuntimeManager.RequestCancellationAsync));
        var resultType = Assert.Single(method!.ReturnType.GetGenericArguments());

        Assert.Equal("WorkflowRunCancellationResult", resultType.Name);
    }

    [Fact]
    public async Task InProcessExternalResponseRemainsWaitingWhenResumeUnsupported()
    {
        var store = new ControllableWorkflowRunStore();
        var backend = new ControllableWorkflowBackend(
            new FixedTimeProvider(FixedUtcNow),
            waitForRelease: false);
        var composition = WorkflowExternalResponseTestCompositionFactory.CreateLegacyCompatibility(
            [backend],
            store,
            new FixedTimeProvider(FixedUtcNow));
        var manager = composition.Manager;
        var waitingRun = CreateRun(WorkflowRunState.WaitingForInput, WorkflowRuntimeBackendKind.InProcess);
        var request = new WorkflowExternalRequestRecord(
            WorkflowExternalRequestId.New(),
            waitingRun.RunId,
            WorkflowExternalRequestKind.HumanInput,
            new WorkflowNodeId("work"),
            "answer-required",
            "{\"question\":\"Continue?\"}",
            string.Empty,
            FixedUtcNow,
            RespondedAtUtc: null);
        await store.SaveRunAsync(waitingRun);
        await store.SaveExternalRequestAsync(request);

        var result = await composition.Responses.SubmitAsync(request, "{\"answer\":\"yes\"}");
        var persistedRun = await manager.GetRunAsync(waitingRun.RunId);
        var persistedRequest = await store.GetExternalRequestAsync(request.Id);
        var pending = await store.ListPendingExternalRequestsAsync(waitingRun.RunId);
        var events = await manager.ListEventsAsync(waitingRun.RunId);

        Assert.Equal(WorkflowExternalResponseServiceOutcome.LegacyNonResumable, result.Outcome);
        Assert.NotNull(persistedRun);
        Assert.Equal(WorkflowRunState.WaitingForInput, persistedRun.State);
        Assert.NotNull(persistedRequest);
        Assert.Null(persistedRequest.RespondedAtUtc);
        Assert.Single(pending);
        Assert.DoesNotContain(events, workflowEvent => workflowEvent.Kind == WorkflowEventKind.Completed);
    }

    [Fact]
    public async Task ResumeCapableBackendAcceptsExternalResponseExactlyOnce()
    {
        var timeProvider = new FixedTimeProvider(FixedUtcNow);
        var store = new RedactedControllableWorkflowRunStore();
        var backend = new ResumeCapableWorkflowBackend(timeProvider);
        var checkpointStore = new InMemoryWorkflowBackendCheckpointPayloadStore(timeProvider);
        var composition = WorkflowExternalResponseTestCompositionFactory.CreateCompatibility(
            [backend],
            store,
            checkpointStore,
            timeProvider,
            eventSink: null,
            usageStore: null);
        var manager = composition.Manager;
        var waitingRun = CreateRun(
            WorkflowRunState.WaitingForInput,
            WorkflowRuntimeBackendKind.InProcess) with
        {
            Origin = CreateAuthorizedOrigin("lifecycle-resume")
        };
        var request = await WorkflowHitlTestCheckpointFactory.AddCheckpointAsync(
            checkpointStore,
            waitingRun,
            CreateNativeRequest(waitingRun),
            "{}");
        await store.SaveRunAsync(waitingRun);
        await store.SaveExternalRequestAsync(request);

        var first = await composition.Responses.SubmitAsync(request, "{\"answer\":\"yes\"}");
        var replay = await composition.Responses.SubmitAsync(request, "{\"answer\":\"yes\"}");
        var changedResponse = await composition.Responses.SubmitAsync(request, "{\"answer\":\"again\"}");
        var persistedRun = await manager.GetRunAsync(waitingRun.RunId);
        var persistedRequest = await store.GetExternalRequestAsync(request.Id);

        Assert.Equal(WorkflowExternalResponseServiceOutcome.Completed, first.Outcome);
        Assert.Equal("{\"answer\":\"yes\"}", first.Operation?.ResponsePayload.Json);
        Assert.Equal(WorkflowExternalResponseServiceOutcome.Completed, replay.Outcome);
        Assert.True(replay.Replayed);
        Assert.Equal(WorkflowExternalResponseServiceOutcome.IdempotencyConflict, changedResponse.Outcome);
        Assert.Equal(1, backend.ResumeInvocationCount);
        Assert.NotNull(persistedRun);
        Assert.Equal(WorkflowRunState.Completed, persistedRun.State);
        Assert.Equal(FixedUtcNow, persistedRun.TerminalAtUtc);
        Assert.NotNull(persistedRequest);
        Assert.Equal(FixedUtcNow, persistedRequest.RespondedAtUtc);
        Assert.Equal(string.Empty, persistedRequest.ResponseJson);
    }

    [Fact]
    public async Task NativeCompatibilityWithoutRedactedAcceptanceCapabilityFailsBeforeRuntimeComposition()
    {
        var timeProvider = new FixedTimeProvider(FixedUtcNow);
        var store = new ControllableWorkflowRunStore();
        var backend = new ResumeCapableWorkflowBackend(timeProvider);
        var checkpointStore = new InMemoryWorkflowBackendCheckpointPayloadStore(timeProvider);

        var exception = Assert.Throws<ArgumentException>(() =>
            WorkflowExternalResponseTestCompositionFactory.CreateCompatibility(
                [backend],
                store,
                checkpointStore,
                timeProvider,
                eventSink: null,
                usageStore: null));

        Assert.Equal("runStore", exception.ParamName);
        Assert.Contains("metadata-only", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, backend.ResumeInvocationCount);
        Assert.Empty(await store.ListRunsAsync());
    }

    private static WorkflowExternalRequestRecord CreateNativeRequest(WorkflowRunSnapshot run)
    {
        var requestId = WorkflowExternalRequestId.New();
        var continuation = new WorkflowExternalRequestContinuation(
            new WorkflowBackendExternalRequestLink(
                requestId,
                new WorkflowBackendRequestId("native-request-1"),
                new WorkflowBackendRequestPortId("human-input")),
            new WorkflowBackendCheckpointLink(
                new WorkflowBackendSessionId(run.BackendRunId),
                new WorkflowBackendCheckpointId("checkpoint-1")),
            new WorkflowCompilerContractVersion(1),
            WorkflowTopologyFingerprint.Create("lifecycle-red-gate"),
            WorkflowBackendCheckpointPayloadHash.Compute("{}"));
        return new WorkflowExternalRequestRecord(
            requestId,
            run.RunId,
            WorkflowExternalRequestKind.HumanInput,
            new WorkflowNodeId("work"),
            "answer-required",
            "{\"question\":\"Continue?\"}",
            string.Empty,
            FixedUtcNow,
            RespondedAtUtc: null)
        {
            State = WorkflowExternalRequestState.Pending,
            ResponseContract = new WorkflowExternalResponseContract(
                WorkflowExternalRequestKind.HumanInput,
                "test.human-input",
                1,
                "{\"type\":\"object\",\"properties\":{\"answer\":{\"type\":\"string\"}},\"required\":[\"answer\"],\"additionalProperties\":false}",
                4_096),
            Continuation = continuation,
            AuthorizationPolicy = new WorkflowExternalRequestAuthorizationPolicySnapshot(
                (run.Origin as WorkflowLaunchOrigin.Api)!.Actor,
                ExecutorId: null,
                WorkflowExecutorCapabilityFlags.None,
                WorkflowExecutorApprovalRequirement.NotRequired,
                IntendedApproverSubjectId: string.Empty)
            {
                AuthorizationScope = run.Origin.AuthorizationScope,
                AuthorizationPolicyFingerprint = run.Origin.AuthorizationPolicyFingerprint,
                ResponseAuthorizationLifetimeSeconds = WorkflowExternalResponseAuthorizationPolicy.ResponseLifetimeSeconds
            }
        };
    }

    [Fact]
    public void ExternalResponseContractRequiresGovernedServiceAndResumeCapableBackendPort()
    {
        Assert.Null(typeof(IWorkflowRuntimeManager).GetMethod("SubmitExternalResponseAsync"));
        Assert.Null(typeof(IWorkflowRuntimeManager).GetMethod("RespondToExternalRequestAsync"));
        var responseMethod = typeof(IWorkflowExternalResponseService).GetMethod(
            nameof(IWorkflowExternalResponseService.SubmitAsync));
        var responseResultType = Assert.Single(responseMethod!.ReturnType.GetGenericArguments());
        var resumeCapabilityType = typeof(IWorkflowExternalResponseBackend);
        var typedResumeMethod = resumeCapabilityType.GetMethod(
            nameof(IWorkflowExternalResponseBackend.ResumeAsync),
            [typeof(WorkflowBackendResumeRequest), typeof(CancellationToken)]);
        var compatibilityResumeMethod = resumeCapabilityType.GetMethod(
            nameof(IWorkflowExternalResponseBackend.ResumeAsync),
            [
                typeof(WorkflowRunSnapshot),
                typeof(WorkflowExternalRequestRecord),
                typeof(string),
                typeof(CancellationToken)
            ]);

        Assert.Equal("WorkflowExternalResponseServiceResult", responseResultType.Name);
        Assert.NotNull(typedResumeMethod);
        Assert.Equal(typeof(Task<WorkflowBackendStartResult>), typedResumeMethod.ReturnType);
        Assert.NotNull(compatibilityResumeMethod);
        Assert.Equal(typeof(Task<WorkflowBackendStartResult>), compatibilityResumeMethod.ReturnType);
    }

    private static WorkflowLaunchOrigin.Api CreateAuthorizedOrigin(string correlationId)
        => new(
            new WorkflowLaunchActor(WorkflowLaunchActorKind.User, "lifecycle-launcher"),
            new WorkflowLaunchCorrelationId(correlationId))
        {
            AuthorizationScope = WorkspaceScopeDescriptor.Organization("unit-tests"),
            AuthorizationPolicyFingerprint = WorkflowExternalResponseAuthorizationPolicy.CurrentFingerprint
        };

    [Fact]
    public void RuntimeManagerUsesInjectedTimeProviderForLifecycleTimestamps()
    {
        var hasTimeProviderDependency = typeof(WorkflowRuntimeManager)
            .GetConstructors()
            .SelectMany(constructor => constructor.GetParameters())
            .Any(parameter => parameter.ParameterType == typeof(TimeProvider));

        Assert.True(hasTimeProviderDependency);
    }

    private static WorkflowDefinition CreateDefinition()
    {
        var start = CreateNode("start", WorkflowNodeKind.Start);
        var work = CreateNode("work", WorkflowNodeKind.StrictLogic);
        var end = CreateNode("end", WorkflowNodeKind.End);
        return new WorkflowDefinition(
            WorkflowId.New(),
            WorkflowVersionId.New(),
            "Lifecycle red gate",
            "Deterministic workflow runtime lifecycle test.",
            WorkflowLifecycleStatus.Active,
            new WorkflowGraph(
                start.Id,
                [start, work, end],
                [
                    CreateEdge("start-work", start.Id, work.Id),
                    CreateEdge("work-end", work.Id, end.Id)
                ]),
            new WorkflowRuntimePolicy(
                WorkflowRuntimeBackendKind.InProcess,
                AllowInProcessPreviewRuns: true,
                RequireDurableProductionRuns: false,
                ExposeAzureFunctionsStatusEndpoint: false,
                ExposeAzureFunctionsMcpTool: false),
            FixedUtcNow,
            FixedUtcNow);
    }

    private static WorkflowRunStartRequest CreateStartRequest(WorkflowDefinition definition)
        => new(
            definition.Id,
            definition.VersionId,
            "{\"input\":\"lifecycle-red-gate\"}",
            WorkflowRuntimeBackendKind.InProcess,
            SourceProcessRunId: null,
            SourceProcessAssignmentId: null);

    private static WorkflowRunSnapshot CreateRun(
        WorkflowRunState state,
        WorkflowRuntimeBackendKind backend)
        => new(
            WorkflowRunId.New(),
            WorkflowId.New(),
            WorkflowVersionId.New(),
            state,
            backend,
            "red-gate-backend-run",
            $"Workflow run is {state}.",
            FixedUtcNow,
            FixedUtcNow);

    private static WorkflowNode CreateNode(string id, WorkflowNodeKind kind)
        => new(
            new WorkflowNodeId(id),
            kind,
            id,
            [],
            new WorkflowNodeSettings(
                ComponentId: null,
                AgentId: null,
                SubworkflowId: null,
                ExternalRequestKind: null,
                Instructions: string.Empty,
                InputShape: WorkflowValueShape.Text,
                ResultShape: WorkflowValueShape.Text));

    private static WorkflowEdge CreateEdge(
        string id,
        WorkflowNodeId source,
        WorkflowNodeId target)
        => new(
            new WorkflowEdgeId(id),
            source,
            SourcePortId: null,
            target,
            TargetPortId: null,
            WorkflowEdgeKind.Direct,
            ConditionExpression: string.Empty);

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed record BackendInvocation(
        WorkflowRunId RunId,
        CancellationToken CancellationToken);

    private sealed class ControllableWorkflowBackend(
        TimeProvider timeProvider,
        bool waitForRelease,
        bool throwAfterProgress = false,
        bool ignoreCancellationForRace = false,
        bool supportsActiveCancellation = true,
        Func<WorkflowRunId, CancellationToken, Task>? progressWriter = null) : IWorkflowExecutionBackend
    {
        private readonly TaskCompletionSource<BackendInvocation> entered = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> release = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> cancellationObserved = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public WorkflowRuntimeBackendDescriptor Descriptor { get; } = new(
            WorkflowRuntimeBackendKind.InProcess,
            "Controllable lifecycle backend",
            IsDurable: false,
            SupportsStreaming: true,
            SupportsExternalRequests: false,
            SupportsDashboardObservability: true,
            OperationalNotes: "Deterministic lifecycle red-gate backend.")
        {
            SupportsActiveCancellation = supportsActiveCancellation
        };

        public Task<BackendInvocation> Entered => entered.Task;

        public int InvocationCount { get; private set; }

        public WorkflowRunId? LastRunId { get; private set; }

        public CancellationToken CapturedCancellationToken { get; private set; }

        public Task CancellationObserved => cancellationObserved.Task;

        public bool CancellationWasObserved => cancellationObserved.Task.IsCompletedSuccessfully;

        public void Release() => release.TrySetResult(true);

        public async Task<WorkflowBackendStartResult> StartAsync(
            WorkflowDefinition definition,
            WorkflowRunStartRequest request,
            WorkflowRunId runId,
            CancellationToken cancellationToken = default)
        {
            InvocationCount++;
            LastRunId = runId;
            CapturedCancellationToken = cancellationToken;
            entered.TrySetResult(new BackendInvocation(runId, cancellationToken));
            if (progressWriter is not null)
            {
                await progressWriter(runId, cancellationToken);
            }

            if (throwAfterProgress)
            {
                throw new InvalidOperationException("Deterministic backend failure after progress.");
            }

            if (waitForRelease)
            {
                if (ignoreCancellationForRace)
                {
                    using var registration = cancellationToken.Register(() =>
                        cancellationObserved.TrySetResult(true));
                    await release.Task;
                }
                else
                {
                    try
                    {
                        await release.Task.WaitAsync(cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        cancellationObserved.TrySetResult(true);
                        throw;
                    }
                }
            }

            var now = timeProvider.GetUtcNow();
            var completed = new WorkflowRunSnapshot(
                runId,
                definition.Id,
                definition.VersionId,
                WorkflowRunState.Completed,
                Descriptor.Kind,
                runId.ToString(),
                "Controllable backend completed.",
                now,
                now);
            var completedEvent = new WorkflowEventRecord(
                Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                runId,
                WorkflowEventKind.Completed,
                NodeId: null,
                completed.Summary,
                "{}",
                now);
            return new WorkflowBackendStartResult(completed, [completedEvent], [], []);
        }
    }

    private sealed class ResumeCapableWorkflowBackend(TimeProvider timeProvider) :
        IWorkflowExecutionBackend,
        IWorkflowExternalResponseBackend
    {
        public WorkflowRuntimeBackendDescriptor Descriptor { get; } = new(
            WorkflowRuntimeBackendKind.InProcess,
            "Resume-capable lifecycle backend",
            IsDurable: false,
            SupportsStreaming: true,
            SupportsExternalRequests: true,
            SupportsDashboardObservability: true,
            OperationalNotes: "Deterministic external-response lifecycle backend.")
        {
            SupportsExternalResponseResume = true,
            SupportsActiveCancellation = true
        };

        public int ResumeInvocationCount { get; private set; }

        public Task<WorkflowBackendStartResult> StartAsync(
            WorkflowDefinition definition,
            WorkflowRunStartRequest request,
            WorkflowRunId runId,
            CancellationToken cancellationToken = default)
            => Task.FromException<WorkflowBackendStartResult>(
                new InvalidOperationException("This fake is only used to resume waiting runs."));

        public Task<WorkflowBackendStartResult> ResumeAsync(
            WorkflowRunSnapshot run,
            WorkflowExternalRequestRecord request,
            string responseJson,
            CancellationToken cancellationToken = default)
        {
            ResumeInvocationCount++;
            var now = timeProvider.GetUtcNow();
            var completed = run with
            {
                State = WorkflowRunState.Completed,
                Summary = "Resumed workflow completed.",
                UpdatedAtUtc = now
            };
            return Task.FromResult(new WorkflowBackendStartResult(completed, [], [], []));
        }
    }

    private class ControllableWorkflowRunStore : IWorkflowRunStore
    {
        private readonly InMemoryWorkflowRunStore inner = new();
        private int eventSaveCount;
        private int initialCreateCount;
        private int runSaveCount;

        public bool FailFirstRunSave { get; init; }

        public bool FailFirstEventSave { get; init; }

        public WorkflowRunId? LastInitialRunId { get; private set; }

        public Task CreateRunWithStartedEventAsync(
            WorkflowRunSnapshot run,
            WorkflowEventRecord startedEvent,
            CancellationToken cancellationToken = default)
        {
            LastInitialRunId = run.RunId;
            if (Interlocked.Increment(ref initialCreateCount) == 1)
            {
                if (FailFirstRunSave)
                {
                    throw new InvalidOperationException("Deterministic initial persistence failure.");
                }

                if (FailFirstEventSave)
                {
                    throw new InvalidOperationException("Deterministic started-event persistence failure.");
                }
            }

            return inner.CreateRunWithStartedEventAsync(run, startedEvent, cancellationToken);
        }

        public Task<WorkflowRunTransitionResult> TryTransitionRunAsync(
            WorkflowRunId runId,
            IReadOnlyCollection<WorkflowRunState> expectedStates,
            WorkflowRunSnapshot updatedRun,
            WorkflowEventRecord? transitionEvent = null,
            CancellationToken cancellationToken = default)
            => inner.TryTransitionRunAsync(
                runId,
                expectedStates,
                updatedRun,
                transitionEvent,
                cancellationToken);

        public Task<WorkflowExternalResponseAcceptanceResult> TryAcceptExternalResponseAsync(
            WorkflowExternalRequestId requestId,
            string responseJson,
            DateTimeOffset respondedAtUtc,
            CancellationToken cancellationToken = default)
            => inner.TryAcceptExternalResponseAsync(
                requestId,
                responseJson,
                respondedAtUtc,
                cancellationToken);

        protected Task<WorkflowExternalResponseAcceptanceResult> ForwardRedactedExternalResponseAcceptanceAsync(
            WorkflowExternalRequestId requestId,
            DateTimeOffset respondedAtUtc,
            CancellationToken cancellationToken)
            => ((IWorkflowRedactedExternalResponseAcceptanceStore)inner)
                .TryAcceptRedactedExternalResponseAsync(
                    requestId,
                    respondedAtUtc,
                cancellationToken);

        public Task SaveRunAsync(
            WorkflowRunSnapshot run,
            CancellationToken cancellationToken = default)
        {
            var currentSave = Interlocked.Increment(ref runSaveCount);
            if (FailFirstRunSave && currentSave == 1)
            {
                throw new InvalidOperationException("Deterministic initial persistence failure.");
            }

            return inner.SaveRunAsync(run, cancellationToken);
        }

        public Task<WorkflowRunSnapshot?> GetRunAsync(
            WorkflowRunId runId,
            CancellationToken cancellationToken = default)
            => inner.GetRunAsync(runId, cancellationToken);

        public Task<IReadOnlyList<WorkflowRunSnapshot>> ListRunsAsync(
            WorkflowId? workflowId = null,
            CancellationToken cancellationToken = default)
            => inner.ListRunsAsync(workflowId, cancellationToken);

        public Task<WorkflowListPage<WorkflowRunSnapshot>> ListRunPageAsync(
            WorkflowRunPageRequest request,
            CancellationToken cancellationToken = default)
            => inner.ListRunPageAsync(request, cancellationToken);

        public Task SaveEventAsync(
            WorkflowEventRecord workflowEvent,
            CancellationToken cancellationToken = default)
        {
            var currentSave = Interlocked.Increment(ref eventSaveCount);
            if (FailFirstEventSave && currentSave == 1)
            {
                throw new InvalidOperationException("Deterministic started-event persistence failure.");
            }

            return inner.SaveEventAsync(workflowEvent, cancellationToken);
        }

        public Task<IReadOnlyList<WorkflowEventRecord>> ListEventsAsync(
            WorkflowRunId runId,
            CancellationToken cancellationToken = default)
            => inner.ListEventsAsync(runId, cancellationToken);

        public Task<WorkflowListPage<WorkflowEventRecord>> ListEventPageAsync(
            WorkflowEventPageRequest request,
            CancellationToken cancellationToken = default)
            => inner.ListEventPageAsync(request, cancellationToken);

        public Task SaveExternalRequestAsync(
            WorkflowExternalRequestRecord request,
            CancellationToken cancellationToken = default)
            => inner.SaveExternalRequestAsync(request, cancellationToken);

        public Task<WorkflowExternalRequestRecord?> GetExternalRequestAsync(
            WorkflowExternalRequestId requestId,
            CancellationToken cancellationToken = default)
            => inner.GetExternalRequestAsync(requestId, cancellationToken);

        public Task<IReadOnlyList<WorkflowExternalRequestRecord>> ListPendingExternalRequestsAsync(
            WorkflowRunId runId,
            CancellationToken cancellationToken = default)
            => inner.ListPendingExternalRequestsAsync(runId, cancellationToken);

        public Task SaveArtifactAsync(
            WorkflowArtifactRecord artifact,
            CancellationToken cancellationToken = default)
            => inner.SaveArtifactAsync(artifact, cancellationToken);

        public Task<IReadOnlyList<WorkflowArtifactRecord>> ListArtifactsAsync(
            WorkflowRunId runId,
            CancellationToken cancellationToken = default)
            => inner.ListArtifactsAsync(runId, cancellationToken);

        public Task<WorkflowCheckpointRecord> SaveCheckpointAsync(
            WorkflowCheckpointRecord checkpoint,
            CancellationToken cancellationToken = default)
            => inner.SaveCheckpointAsync(checkpoint, cancellationToken);

        public Task<WorkflowCheckpointRecord?> GetCheckpointAsync(
            WorkflowCheckpointId checkpointId,
            CancellationToken cancellationToken = default)
            => inner.GetCheckpointAsync(checkpointId, cancellationToken);

        public Task<IReadOnlyList<WorkflowCheckpointRecord>> ListCheckpointsAsync(
            WorkflowRunId runId,
            CancellationToken cancellationToken = default)
            => inner.ListCheckpointsAsync(runId, cancellationToken);

        public Task<WorkflowCheckpointRecord> MarkCheckpointResumedAsync(
            WorkflowCheckpointId checkpointId,
            DateTimeOffset resumedAtUtc,
            CancellationToken cancellationToken = default)
            => inner.MarkCheckpointResumedAsync(checkpointId, resumedAtUtc, cancellationToken);
    }

    private sealed class RedactedControllableWorkflowRunStore :
        ControllableWorkflowRunStore,
        IWorkflowRedactedExternalResponseAcceptanceStore
    {
        Task<WorkflowExternalResponseAcceptanceResult>
            IWorkflowRedactedExternalResponseAcceptanceStore.TryAcceptRedactedExternalResponseAsync(
                WorkflowExternalRequestId requestId,
                DateTimeOffset respondedAtUtc,
                CancellationToken cancellationToken)
            => ForwardRedactedExternalResponseAcceptanceAsync(
                requestId,
                respondedAtUtc,
                cancellationToken);
    }
}
