using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Builder;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessBlockedRunPersistedRecoveryTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 25, 12, 0, 0, TimeSpan.Zero);
    private static readonly DispatcherOwnerId OwnerId = new("persisted-recovery-test");
    private static readonly ProcessStrategyBindingSnapshot Binding = new(
        new DriverId("driver.persisted-recovery"),
        new StrategyId("strategy.persisted-recovery.execute"),
        "1.0.0",
        "factory.1.0.0",
        "runtime.1",
        "runtime.1",
        "sha256:persisted-recovery-binding",
        []);

    [Fact]
    public async Task Producer_and_consumer_recovery_history_survives_restarts_and_denies_replay()
    {
        var databaseName = $"process-blocked-recovery-{Guid.NewGuid():N}";
        var databaseRoot = new InMemoryDatabaseRoot();
        var runId = ProcessRunId.New();
        var planId = ProcessInstancePlanId.New();
        var producerStepId = ProcessStepInstanceId.New();
        var producerDefinitionId = ProcessStepDefinitionId.New();
        var consumerStepId = ProcessStepInstanceId.New();
        var consumerDefinitionId = ProcessStepDefinitionId.New();
        var artifactSlotId = ArtifactSlotId.New();
        var sourceResultKey = StrategyResultIdempotencyKey.New();
        var initialState = CreateBlockedState(
            runId,
            planId,
            producerStepId,
            producerDefinitionId,
            consumerStepId,
            consumerDefinitionId,
            artifactSlotId,
            sourceResultKey);
        var plan = CreateSimpleAppPlan(planId);
        var dispatchQueue = new RecordingDispatchQueue();

        await using (var seedContext = CreateDbContext(databaseName, databaseRoot))
        {
            var store = new EfProcessRuntimeUnitOfWork(seedContext);
            var seedResult = await store.CommitAsync(new ProcessRuntimeCommitRequest(
                RuntimeCommandId.New(),
                initialState,
                Applied(initialState),
                InitialPlan: plan));

            Assert.True(seedResult.Succeeded);
        }

        await using (var producerRecoveryContext = CreateDbContext(databaseName, databaseRoot))
        {
            var store = new EfProcessRuntimeUnitOfWork(producerRecoveryContext);
            var coordinator = CreateCoordinator(
                producerRecoveryContext,
                store,
                dispatchQueue,
                Now.AddMinutes(1));

            var producerRecovery = await coordinator.TryRecoverAsync(runId, "persisted-recovery-test");

            Assert.Equal(ProcessBlockedRunRecoveryOutcome.Recovered, producerRecovery.Outcome);
            Assert.Equal(
                ProcessBlockedRunRecoveryActionKind.UpstreamStepRework,
                producerRecovery.ActionKind);
            var persisted = await store.LoadAsync(runId);
            Assert.NotNull(persisted);
            var producerAction = Assert.Single(persisted.BlockedRecoveryActions);
            Assert.Equal(ProcessRuntimeBlockedRecoveryPhase.UpstreamProducer, producerAction.Phase);
            Assert.Equal(sourceResultKey, producerAction.SourceResultIdempotencyKey);
            Assert.Equal(producerStepId, producerAction.TargetStepInstanceId);
        }

        await using (var producerExecutionContext = CreateDbContext(databaseName, databaseRoot))
        {
            var store = new EfProcessRuntimeUnitOfWork(producerExecutionContext);
            var restartedState = await store.LoadAsync(runId);
            Assert.NotNull(restartedState);
            Assert.Equal(
                ProcessRuntimeBlockedRecoveryPhase.UpstreamProducer,
                Assert.Single(restartedState.BlockedRecoveryActions).Phase);

            var producerResult = await ExecuteStepAsync(
                store,
                restartedState,
                producerStepId,
                producerDefinitionId,
                CreateProducerResult(artifactSlotId),
                Now.AddMinutes(2));
            Assert.Equal(
                ProcessRuntimeStepStatus.Completed,
                producerResult.Steps.Single(step => step.StepInstanceId == producerStepId).Status);
            var restoredInput = Assert.Single(producerResult.ConnectedInputArtifacts);
            Assert.Equal(ProcessArtifactInputAvailability.Available, restoredInput.Availability);
            Assert.NotNull(restoredInput.ArtifactId);
            Assert.Equal("sha256:restored-input", restoredInput.ContentHash);

            var blockedResult = await PersistBlockedRunAsync(
                store,
                producerResult,
                Now.AddMinutes(5));
            Assert.Equal(ProcessRuntimeStatus.Blocked, blockedResult.Status);
        }

        await using (var consumerRecoveryContext = CreateDbContext(databaseName, databaseRoot))
        {
            var store = new EfProcessRuntimeUnitOfWork(consumerRecoveryContext);
            var restartedState = await store.LoadAsync(runId);
            Assert.NotNull(restartedState);
            Assert.Equal(ProcessRuntimeStatus.Blocked, restartedState.Status);
            Assert.Equal(
                ProcessRuntimeBlockedRecoveryPhase.UpstreamProducer,
                Assert.Single(restartedState.BlockedRecoveryActions).Phase);
            Assert.Equal(
                ProcessArtifactInputAvailability.Available,
                Assert.Single(restartedState.ConnectedInputArtifacts).Availability);
            var coordinator = CreateCoordinator(
                consumerRecoveryContext,
                store,
                dispatchQueue,
                Now.AddMinutes(6));

            var consumerRecovery = await coordinator.TryRecoverAsync(runId, "persisted-recovery-test");

            Assert.Equal(ProcessBlockedRunRecoveryOutcome.Recovered, consumerRecovery.Outcome);
            Assert.Equal(
                ProcessBlockedRunRecoveryActionKind.CurrentStepRework,
                consumerRecovery.ActionKind);
            var persisted = await store.LoadAsync(runId);
            Assert.NotNull(persisted);
            Assert.Collection(
                persisted.BlockedRecoveryActions,
                action => Assert.Equal(ProcessRuntimeBlockedRecoveryPhase.UpstreamProducer, action.Phase),
                action => Assert.Equal(ProcessRuntimeBlockedRecoveryPhase.RestoredConsumer, action.Phase));
            Assert.All(
                persisted.BlockedRecoveryActions,
                action => Assert.Equal(sourceResultKey, action.SourceResultIdempotencyKey));
        }

        await using (var replaySetupContext = CreateDbContext(databaseName, databaseRoot))
        {
            var store = new EfProcessRuntimeUnitOfWork(replaySetupContext);
            var restartedState = await store.LoadAsync(runId);
            Assert.NotNull(restartedState);
            Assert.Equal(2, restartedState.BlockedRecoveryActions.Count);

            var unavailableState = restartedState with
            {
                AvailableArtifactSlots = restartedState.AvailableArtifactSlots
                    .Where(slotId => slotId != artifactSlotId)
                    .ToHashSet(),
                ConnectedInputArtifacts = restartedState.ConnectedInputArtifacts
                    .Select(receipt => receipt.RequiredSlotId == artifactSlotId
                        ? receipt with
                        {
                            Availability = ProcessArtifactInputAvailability.Expected,
                            ArtifactId = null,
                            ContentHash = string.Empty,
                            ConnectionHash = "sha256:recurrent-missing-input"
                        }
                        : receipt)
                    .ToArray(),
                UpdatedAtUtc = Now.AddMinutes(7)
            };
            var unavailableCommit = await store.CommitAsync(new ProcessRuntimeCommitRequest(
                RuntimeCommandId.New(),
                restartedState,
                Applied(unavailableState)));
            Assert.True(unavailableCommit.Succeeded);

            var recurrentResult = await ExecuteStepAsync(
                store,
                unavailableCommit.State,
                consumerStepId,
                consumerDefinitionId,
                CreateMissingInputResult(artifactSlotId),
                Now.AddMinutes(8));
            var recurrentReceipt = ProcessRuntimeBlockedRecoveryAuthorizationRules
                .FindLatestBlockedManagerRequiredReceipt(recurrentResult, consumerStepId);
            Assert.NotNull(recurrentReceipt);
            Assert.NotEqual(sourceResultKey, recurrentReceipt.IdempotencyKey);
            var initialReceipt = recurrentResult.AppliedResults.Single(
                receipt => receipt.IdempotencyKey == sourceResultKey);
            Assert.Equal(
                initialReceipt.RecoveryDecision!.DiagnosticFingerprint,
                recurrentReceipt.RecoveryDecision!.DiagnosticFingerprint);

            var blockedResult = await PersistBlockedRunAsync(
                store,
                recurrentResult,
                Now.AddMinutes(11));
            Assert.Equal(ProcessRuntimeStatus.Blocked, blockedResult.Status);
            Assert.Equal(2, blockedResult.BlockedRecoveryActions.Count);
        }

        await using (var replayContext = CreateDbContext(databaseName, databaseRoot))
        {
            var store = new EfProcessRuntimeUnitOfWork(replayContext);
            var restartedState = await store.LoadAsync(runId);
            Assert.NotNull(restartedState);
            Assert.Equal(2, restartedState.BlockedRecoveryActions.Count);
            var coordinator = CreateCoordinator(
                replayContext,
                store,
                dispatchQueue,
                Now.AddMinutes(12));

            var replay = await coordinator.TryRecoverAsync(runId, "persisted-recovery-test");

            Assert.Equal(ProcessBlockedRunRecoveryOutcome.RequiresAttention, replay.Outcome);
            Assert.Contains(
                replay.Diagnostics,
                diagnostic => diagnostic.Contains("budget", StringComparison.OrdinalIgnoreCase));
            var persisted = await store.LoadAsync(runId);
            Assert.NotNull(persisted);
            Assert.Collection(
                persisted.BlockedRecoveryActions,
                action => Assert.Equal(ProcessRuntimeBlockedRecoveryPhase.UpstreamProducer, action.Phase),
                action => Assert.Equal(ProcessRuntimeBlockedRecoveryPhase.RestoredConsumer, action.Phase));
            Assert.Equal(2, dispatchQueue.Requests.Count);
        }
    }

    private static async Task<ProcessRuntimeStateSnapshot> ExecuteStepAsync(
        EfProcessRuntimeUnitOfWork store,
        ProcessRuntimeStateSnapshot state,
        ProcessStepInstanceId stepId,
        ProcessStepDefinitionId stepDefinitionId,
        StrategyResultEnvelope result,
        DateTimeOffset startedAtUtc)
    {
        var engine = new ProcessRuntimeEngine(store);
        var claimToken = DispatchClaimToken.New();
        var workItem = new DispatchWorkItem(
            state.RunId,
            stepId,
            stepDefinitionId,
            Binding,
            attemptNumber: 1);
        var claim = await engine.CreateClaimAsync(
            state,
            Context(startedAtUtc),
            new CreateDispatchClaimCommand(
                workItem,
                OwnerId,
                claimToken,
                startedAtUtc.AddMinutes(10)));
        Assert.True(claim.Succeeded);
        var running = await engine.MarkClaimRunningAsync(
            claim.State,
            Context(startedAtUtc.AddMinutes(1)),
            stepId,
            claimToken);
        Assert.True(running.Succeeded);
        var completed = await engine.SubmitStrategyResultAsync(
            running.State,
            Context(startedAtUtc.AddMinutes(2)),
            new SubmitStrategyResultCommand(
                stepId,
                OwnerId,
                claimToken,
                StrategyResultIdempotencyKey.New(),
                result));
        Assert.True(completed.Succeeded);
        return completed.State;
    }

    private static async Task<ProcessRuntimeStateSnapshot> PersistBlockedRunAsync(
        EfProcessRuntimeUnitOfWork store,
        ProcessRuntimeStateSnapshot state,
        DateTimeOffset occurredAtUtc)
    {
        var blockedState = state with
        {
            Status = ProcessRuntimeStatus.Blocked,
            UpdatedAtUtc = occurredAtUtc
        };
        var runtimeEvent = new ProcessRuntimeEventEnvelope(
            RuntimeEventId.New(),
            blockedState.RootRunId,
            blockedState.RunId,
            new ProcessCorrelationId($"persisted-recovery-{Guid.NewGuid():N}"),
            CausationId: null,
            new ProcessEventActor(
                ProcessEventActorKind.System,
                new ProcessActorId("persisted-recovery-test")),
            ProcessContractVersions.RuntimeEventEnvelopeV1,
            ProcessEventSensitivity.Normal,
            occurredAtUtc,
            ProcessRuntimeEventTypes.ProcessRunBlocked,
            "sha256:persisted-recovery-blocked");
        var mutation = new ProcessRuntimeMutation(
            ProcessRuntimeTransitionOutcome.Applied,
            blockedState,
            [runtimeEvent],
            [
                new ProcessOutboxMessage(
                    RuntimeOutboxMessageId.New(),
                    runtimeEvent.EventId,
                    ProcessOutboxSubscriberKind.RuntimeProjection,
                    runtimeEvent.PayloadHash)
            ],
            [],
            []);
        var commit = await store.CommitAsync(new ProcessRuntimeCommitRequest(
            RuntimeCommandId.New(),
            state,
            mutation));
        Assert.True(commit.Succeeded);
        return commit.State;
    }

    private static ProcessBlockedRunRecoveryCoordinator CreateCoordinator(
        ProcessPersistenceDbContext dbContext,
        EfProcessRuntimeUnitOfWork store,
        RecordingDispatchQueue dispatchQueue,
        DateTimeOffset now)
    {
        var projectionStore = new EfProcessProjectionStore(dbContext);
        var clock = new FixedProcessProjectionClock(now);
        var operatorService = new ProcessRuntimeOperatorApplicationService(
            clock,
            store,
            store,
            EmptyAssignmentStore.Instance,
            store,
            dispatchQueue,
            new ProcessRuntimeProjectionCatchupService(
                EmptyRuntimeEventReplayStore.Instance,
                projectionStore,
                new ProcessRuntimeProjectionProjector(
                    projectionStore,
                    ProcessProjectionJsonCodec.Default,
                    clock,
                    new EfProcessRunRecordStore(dbContext)),
                clock),
            []);
        return new ProcessBlockedRunRecoveryCoordinator(
            store,
            new EfProcessInstancePlanStore(dbContext),
            new ProcessBlockedRunRecoveryCommandExecutor(operatorService),
            new ProcessBlockedRunRecoveryPolicyCatalog());
    }

    private static ProcessRuntimeStateSnapshot CreateBlockedState(
        ProcessRunId runId,
        ProcessInstancePlanId planId,
        ProcessStepInstanceId producerStepId,
        ProcessStepDefinitionId producerDefinitionId,
        ProcessStepInstanceId consumerStepId,
        ProcessStepDefinitionId consumerDefinitionId,
        ArtifactSlotId artifactSlotId,
        StrategyResultIdempotencyKey sourceResultKey)
    {
        var producerResultKey = StrategyResultIdempotencyKey.New();
        var diagnostic = new StrategyResultDiagnosticReceipt(
            ProcessRuntimeDiagnosticCodes.MissingRequiredInputArtifact,
            StrategyDiagnosticSensitivity.Normal,
            "sha256:missing-input",
            "Required input artifact is missing.",
            RestrictedEvidenceReference: null,
            ProcessDiagnosticRetrySafety.UnsafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Idempotent);
        var recoveryDecision = ProcessRecoveryClassifier.Default.ClassifyBlocked(
            new ProcessRecoveryClassificationInput(
                consumerStepId,
                ProcessFailureCategory.MissingArtifact,
                diagnostic.Code,
                ProcessRecoveryRouteKind.UpstreamStepRework,
                producerStepId,
                [diagnostic],
                []));
        return new ProcessRuntimeStateSnapshot(
            runId,
            runId,
            planId,
            "sha256:persisted-recovery-plan",
            ProcessRuntimeStatus.Blocked,
            [
                new ProcessRuntimeStepState(
                    producerStepId,
                    producerDefinitionId,
                    ProcessRuntimeStepStatus.Completed,
                    IsExecutable: true,
                    AttemptNumber: 1,
                    DependencyStepIds: new HashSet<ProcessStepInstanceId>(),
                    RequiredArtifactSlots: new HashSet<ArtifactSlotId>(),
                    ActiveClaimToken: null,
                    CompletedResultKey: producerResultKey)
                {
                    ProducedArtifactSlots = new HashSet<ArtifactSlotId> { artifactSlotId }
                },
                new ProcessRuntimeStepState(
                    consumerStepId,
                    consumerDefinitionId,
                    ProcessRuntimeStepStatus.Blocked,
                    IsExecutable: true,
                    AttemptNumber: 1,
                    DependencyStepIds: new HashSet<ProcessStepInstanceId> { producerStepId },
                    RequiredArtifactSlots: new HashSet<ArtifactSlotId> { artifactSlotId },
                    ActiveClaimToken: null,
                    CompletedResultKey: null)
            ],
            [],
            [
                new StrategyResultReceipt(
                    producerStepId,
                    Binding.StrategyId,
                    producerResultKey,
                    StrategyOutcome.Succeeded,
                    ProcessRuntimeStepStatus.Completed,
                    "sha256:initial-producer-result")
                {
                    AppliedSequence = 1
                },
                new StrategyResultReceipt(
                    consumerStepId,
                    Binding.StrategyId,
                    sourceResultKey,
                    StrategyOutcome.NeedsManager,
                    ProcessRuntimeStepStatus.Blocked,
                    "sha256:missing-input-result",
                    [diagnostic],
                    recoveryDecision: recoveryDecision)
                {
                    AppliedSequence = 2
                }
            ],
            new HashSet<ArtifactSlotId>(),
            Now)
        {
            ConnectedInputArtifacts =
            [
                new ProcessRuntimeInputArtifactReceipt(
                    consumerStepId,
                    artifactSlotId,
                    ProcessArtifactInputAvailability.Expected,
                    producerStepId,
                    ArtifactId: null,
                    ContentHash: string.Empty,
                    ConnectionHash: "sha256:producer-to-consumer")
            ]
        };
    }

    private static ProcessInstancePlan CreateSimpleAppPlan(ProcessInstancePlanId planId)
    {
        return new ProcessInstancePlan(
            new ProcessInstancePlanHeader(
                planId,
                planId,
                ParentPlanId: null,
                ParentStepId: null,
                "processes.instance-plan.v1",
                Now,
                HierarchyDepth: 0),
            new ResolvedProcessDefinitionSnapshot(
                ProcessDefinitionId.New(),
                ProcessDefinitionVersionId.New(),
                "sha256:persisted-recovery-definition",
                "template/1",
                "template/1",
                [],
                [
                    new ResolvedTemplateComponentSnapshot(
                        TemplateComponentId.New(),
                        "simple-app-delivery",
                        "1.0.0",
                        "sha256:simple-app-template")
                ],
                []),
            new DriverStackSnapshot([]),
            new StrategyBindingSet([], [], [], []),
            [],
            new ArtifactPlan([], []),
            new BranchRouteTable([]),
            [],
            new ManagerPlan("sha256:manager-policy", null, [], []),
            new BudgetPlan([]),
            new MonitoringPlan(true, "sha256:monitoring"),
            new SecurityPlan("sha256:security", []),
            "sha256:persisted-recovery-plan");
    }

    private static StrategyResultEnvelope CreateProducerResult(ArtifactSlotId artifactSlotId)
    {
        return new StrategyResultEnvelope(
            Binding.StrategyId,
            "1.0.0",
            Guid.NewGuid(),
            StrategyOutcome.Succeeded,
            [
                new ProducedArtifactRef(
                    ArtifactInstanceId.New(),
                    artifactSlotId,
                    "sha256:restored-input")
            ],
            [],
            [],
            [],
            "sha256:restored-producer-result");
    }

    private static StrategyResultEnvelope CreateMissingInputResult(ArtifactSlotId artifactSlotId)
    {
        return new StrategyResultEnvelope(
            Binding.StrategyId,
            "1.0.0",
            Guid.NewGuid(),
            StrategyOutcome.NeedsManager,
            [],
            [new RequestedArtifactRef(artifactSlotId, "sha256:requested-input")],
            [
                new StrategyDiagnosticRef(
                    new StrategyDiagnosticCode(ProcessRuntimeDiagnosticCodes.MissingRequiredInputArtifact),
                    StrategyDiagnosticSensitivity.Normal,
                    "sha256:missing-input",
                    "Required input artifact is missing.",
                    RestrictedEvidenceReference: null,
                    ProcessDiagnosticRetrySafety.UnsafeToRetry,
                    ProcessDiagnosticIdempotencyClassification.Idempotent)
            ],
            [
                new ManagerSignal(
                    new ManagerSignalCode(ProcessRuntimeDiagnosticCodes.MissingRequiredInputArtifact),
                    "sha256:missing-input",
                    "Required input artifact is missing.")
            ],
            "sha256:recurrent-missing-input-result");
    }

    private static ProcessRuntimeMutation Applied(ProcessRuntimeStateSnapshot state)
    {
        return new ProcessRuntimeMutation(
            ProcessRuntimeTransitionOutcome.Applied,
            state,
            [],
            [],
            [],
            []);
    }

    private static RuntimeCommandContext Context(DateTimeOffset occurredAtUtc)
    {
        return new RuntimeCommandContext(
            RuntimeCommandId.New(),
            new ProcessEventActor(
                ProcessEventActorKind.System,
                new ProcessActorId("persisted-recovery-test")),
            new ProcessCorrelationId($"persisted-recovery-{Guid.NewGuid():N}"),
            occurredAtUtc);
    }

    private static ProcessPersistenceDbContext CreateDbContext(
        string databaseName,
        InMemoryDatabaseRoot databaseRoot)
    {
        var options = new DbContextOptionsBuilder<ProcessPersistenceDbContext>()
            .UseInMemoryDatabase(databaseName, databaseRoot)
            .Options;
        return new ProcessPersistenceDbContext(options);
    }

    private sealed class FixedProcessProjectionClock(DateTimeOffset utcNow) : IProcessProjectionClock
    {
        public DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class RecordingDispatchQueue : IProcessRuntimeDispatchQueue
    {
        public List<ProcessRuntimeDispatchQueueRequest> Requests { get; } = [];

        public ValueTask EnqueueAsync(
            ProcessRuntimeDispatchQueueRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class EmptyAssignmentStore : IProcessRuntimeStepAssignmentStore
    {
        public static EmptyAssignmentStore Instance { get; } = new();

        public ValueTask SaveAsync(
            IReadOnlyList<ProcessRuntimeStepAssignment> assignments,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.CompletedTask;
        }

        public ValueTask<IReadOnlyList<ProcessRuntimeStepAssignment>> LoadByRunAsync(
            ProcessRunId runId,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult<IReadOnlyList<ProcessRuntimeStepAssignment>>([]);
        }

        public ValueTask<IReadOnlyList<ProcessRuntimeStepAssignment>> FindByLaunchVariablesAsync(
            IReadOnlyDictionary<string, string> requiredVariables,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult<IReadOnlyList<ProcessRuntimeStepAssignment>>([]);
        }

        public ValueTask<ProcessRuntimeStepAssignment?> LoadAsync(
            ProcessRunId runId,
            ProcessStepInstanceId stepInstanceId,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult<ProcessRuntimeStepAssignment?>(null);
        }
    }

    private sealed class EmptyRuntimeEventReplayStore : IProcessRuntimeEventReplayStore
    {
        public static EmptyRuntimeEventReplayStore Instance { get; } = new();

        public Task<IReadOnlyList<ProcessStoredRuntimeEvent>> ReadAfterGlobalSequenceAsync(
            long globalSequenceExclusive,
            int take,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ProcessStoredRuntimeEvent>>([]);
        }

        public Task<IReadOnlyList<ProcessStoredRuntimeEvent>> ReadByRootRunAsync(
            ProcessRunId rootRunId,
            long rootSequenceExclusive,
            int take,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ProcessStoredRuntimeEvent>>([]);
        }
    }
}
