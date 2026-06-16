using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Builder;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.AgentFramework.Models;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessPersistenceStoreTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Commit_writes_runtime_state_events_outbox_ledger_and_idempotency_atomically()
    {
        await using var dbContext = CreateDbContext();
        var unitOfWork = new EfProcessRuntimeUnitOfWork(dbContext);
        var request = NewCommitRequest(includeArtifactLedger: true);

        var result = await unitOfWork.CommitAsync(request);

        Assert.Equal(ProcessRuntimeTransitionOutcome.Applied, result.Outcome);
        Assert.Equal(1, await dbContext.RuntimeStates.CountAsync());
        Assert.Equal(1, await dbContext.RuntimeSteps.CountAsync());
        Assert.Equal(1, await dbContext.RuntimeEvents.CountAsync());
        Assert.Equal(1, await dbContext.OutboxMessages.CountAsync());
        Assert.Equal(1, await dbContext.ArtifactLedgerEvents.CountAsync());
        Assert.Equal(1, await dbContext.IdempotencyKeys.CountAsync());

        var loaded = await unitOfWork.LoadAsync(request.Mutation.State.RunId);
        Assert.NotNull(loaded);
        Assert.Equal(ProcessRuntimeStatus.Completed, loaded.Status);
        Assert.Contains(loaded.AvailableArtifactSlots, slot => slot == RequiredArtifactSlotId);
    }

    [Fact]
    public async Task Commit_rejects_broken_event_outbox_atomicity_before_writing_rows()
    {
        await using var dbContext = CreateDbContext();
        var unitOfWork = new EfProcessRuntimeUnitOfWork(dbContext);
        var request = NewCommitRequest(includeArtifactLedger: false);
        var brokenOutbox = new ProcessOutboxMessage(
            RuntimeOutboxMessageId.New(),
            RuntimeEventId.New(),
            ProcessOutboxSubscriberKind.RuntimeProjection,
            "hash:broken");
        var brokenMutation = request.Mutation with
        {
            OutboxMessages = [brokenOutbox]
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            unitOfWork.CommitAsync(request with { Mutation = brokenMutation }));

        Assert.Equal(0, await dbContext.RuntimeStates.CountAsync());
        Assert.Equal(0, await dbContext.RuntimeEvents.CountAsync());
        Assert.Equal(0, await dbContext.OutboxMessages.CountAsync());
    }

    [Fact]
    public async Task Duplicate_command_returns_existing_result_without_second_event_append()
    {
        await using var dbContext = CreateDbContext();
        var unitOfWork = new EfProcessRuntimeUnitOfWork(dbContext);
        var request = NewCommitRequest(includeArtifactLedger: false);

        await unitOfWork.CommitAsync(request);
        var duplicate = await unitOfWork.CommitAsync(request);

        Assert.Equal(ProcessRuntimeTransitionOutcome.Applied, duplicate.Outcome);
        Assert.Equal(1, await dbContext.RuntimeEvents.CountAsync());
        Assert.Equal(1, await dbContext.OutboxMessages.CountAsync());
        Assert.Equal(1, await dbContext.IdempotencyKeys.CountAsync());
    }

    [Fact]
    public async Task Stale_original_state_rejects_commit_without_overwriting_current_state()
    {
        await using var dbContext = CreateDbContext();
        var unitOfWork = new EfProcessRuntimeUnitOfWork(dbContext);
        var initial = NewCommitRequest(includeArtifactLedger: false);

        await unitOfWork.CommitAsync(initial);
        var original = await unitOfWork.LoadAsync(initial.Mutation.State.RunId);
        Assert.NotNull(original);

        var concurrent = NewCommitRequest(
            includeArtifactLedger: false,
            runId: initial.Mutation.State.RunId,
            rootRunId: initial.Mutation.State.RootRunId,
            commandId: RuntimeCommandId.New(),
            eventType: ProcessRuntimeEventTypes.StepCompleted,
            updatedAtUtc: Now.AddMinutes(1));
        await unitOfWork.CommitAsync(concurrent with { OriginalState = original });

        var stale = NewCommitRequest(
            includeArtifactLedger: false,
            runId: initial.Mutation.State.RunId,
            rootRunId: initial.Mutation.State.RootRunId,
            commandId: RuntimeCommandId.New(),
            eventType: ProcessRuntimeEventTypes.StepFailed,
            updatedAtUtc: Now.AddMinutes(2));

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            unitOfWork.CommitAsync(stale with { OriginalState = original }));

        Assert.Equal(2, await dbContext.RuntimeEvents.CountAsync());
        var current = await unitOfWork.LoadAsync(initial.Mutation.State.RunId);
        Assert.NotNull(current);
        Assert.Equal(Now.AddMinutes(1), current.UpdatedAtUtc);
    }

    [Fact]
    public async Task Replay_store_reads_global_and_root_sequences_in_order()
    {
        await using var dbContext = CreateDbContext();
        var unitOfWork = new EfProcessRuntimeUnitOfWork(dbContext);
        var first = NewCommitRequest(includeArtifactLedger: false);
        var second = NewCommitRequest(
            includeArtifactLedger: false,
            runId: first.Mutation.State.RunId,
            rootRunId: first.Mutation.State.RootRunId,
            commandId: RuntimeCommandId.New(),
            eventType: ProcessRuntimeEventTypes.StepCompleted);

        await unitOfWork.CommitAsync(first);
        var original = await unitOfWork.LoadAsync(first.Mutation.State.RunId);
        Assert.NotNull(original);
        await unitOfWork.CommitAsync(second with { OriginalState = original });

        var replayStore = new EfProcessRuntimeEventStore(dbContext);
        var globalEvents = await replayStore.ReadAfterGlobalSequenceAsync(0, 10);
        var rootEvents = await replayStore.ReadByRootRunAsync(first.Mutation.State.RootRunId, 0, 10);

        Assert.Equal([1, 2], globalEvents.Select(runtimeEvent => runtimeEvent.GlobalSequence));
        Assert.Equal([1, 2], rootEvents.Select(runtimeEvent => runtimeEvent.RootSequence));
        Assert.All(globalEvents, runtimeEvent => Assert.Equal(first.Mutation.State.RootRunId, runtimeEvent.Envelope.RootRunId));
    }

    [Fact]
    public async Task Outbox_store_claims_retries_and_marks_delivery_explicitly()
    {
        await using var dbContext = CreateDbContext();
        var unitOfWork = new EfProcessRuntimeUnitOfWork(dbContext);
        var request = NewCommitRequest(includeArtifactLedger: false);
        await unitOfWork.CommitAsync(request);

        var outboxStore = new EfProcessOutboxStore(dbContext);
        var lockId = new ProcessOutboxLockId("worker-1");
        var claimed = await outboxStore.ClaimPendingAsync(Now, 1, lockId);

        Assert.Single(claimed);
        Assert.Equal(1, claimed[0].AttemptCount);

        await outboxStore.MarkFailedAsync(claimed[0].MessageId, lockId, "Transient", Now.AddMinutes(1));
        Assert.Empty(await outboxStore.ClaimPendingAsync(Now, 1, new ProcessOutboxLockId("worker-2")));

        var retried = await outboxStore.ClaimPendingAsync(Now.AddMinutes(1), 1, new ProcessOutboxLockId("worker-2"));
        Assert.Single(retried);
        Assert.Equal(2, retried[0].AttemptCount);

        await outboxStore.MarkDeliveredAsync(retried[0].MessageId, new ProcessOutboxLockId("worker-2"), Now.AddMinutes(2));
        Assert.Empty(await outboxStore.ClaimPendingAsync(Now.AddMinutes(3), 1, new ProcessOutboxLockId("worker-3")));
    }

    [Fact]
    public async Task Projection_store_upserts_snapshots_offsets_and_dead_letters()
    {
        await using var dbContext = CreateDbContext();
        var projectionStore = new EfProcessProjectionStore(dbContext);
        var projectorName = new ProcessProjectorName("runtime.live");
        var projectionKey = new ProcessProjectionKey("run:alpha");
        var shardKey = new ProcessProjectionShardKey("root:alpha");
        var deadLetter = new ProcessProjectionDeadLetter(
            ProcessProjectionDeadLetterId.New(),
            projectorName,
            shardKey,
            RuntimeEventId.New(),
            7,
            "SchemaMismatch",
            "diag-7",
            "manual-review",
            Now);

        await projectionStore.UpsertSnapshotAsync(new ProcessProjectionSnapshot(
            projectorName,
            projectionKey,
            ProcessContractVersions.RuntimeProjectionV1,
            """{"status":"Active"}""",
            "hash:projection-1",
            Now));
        await projectionStore.UpsertSnapshotAsync(new ProcessProjectionSnapshot(
            projectorName,
            projectionKey,
            ProcessContractVersions.RuntimeProjectionV1,
            """{"status":"Completed"}""",
            "hash:projection-2",
            Now.AddMinutes(1)));
        await projectionStore.SaveOffsetAsync(new ProcessProjectorOffset(projectorName, shardKey, 7, Now));
        await projectionStore.SaveOffsetAsync(new ProcessProjectorOffset(projectorName, shardKey, 5, Now.AddMinutes(1)));
        await projectionStore.WriteDeadLetterAsync(deadLetter);

        var snapshot = await projectionStore.LoadSnapshotAsync(projectorName, projectionKey);
        var offset = await projectionStore.LoadOffsetAsync(projectorName, shardKey);
        var deadLetters = await projectionStore.ReadDeadLettersAsync(projectorName, shardKey, 10);

        Assert.NotNull(snapshot);
        Assert.Equal("hash:projection-2", snapshot.PayloadHash);
        Assert.NotNull(offset);
        Assert.Equal(7, offset.GlobalSequence);
        Assert.Single(deadLetters);
        Assert.Equal(deadLetter.DeadLetterId, deadLetters[0].DeadLetterId);
    }

    [Fact]
    public async Task Instance_plan_store_round_trips_strategy_binding_for_dispatch()
    {
        await using var dbContext = CreateDbContext();
        var store = new EfProcessInstancePlanStore(dbContext);
        var stepId = ProcessStepInstanceId.New();
        var stepDefinitionId = ProcessStepDefinitionId.New();
        var binding = new ProcessStrategyBindingSnapshot(
            new DriverId("driver.persistence-test"),
            new StrategyId("strategy.persistence-test.execute"),
            "1.0.0",
            "factory.1.0.0",
            RuntimeSchemaVersion,
            RuntimeSchemaVersion,
            "sha256:binding",
            [new StrategyBindingInput(new StrategyBindingInputKey("operation"), "sha256:operation")]);
        var plan = NewDispatchablePlan(stepId, stepDefinitionId, binding);

        await store.PersistAsync(plan);
        var loaded = await store.LoadAsync(plan.Header.PlanId);

        Assert.NotNull(loaded);
        var loadedStep = Assert.Single(loaded.Steps);
        Assert.Equal(stepId, loadedStep.StepInstanceId);
        Assert.NotNull(loadedStep.ExecutionStrategyBinding);
        Assert.Equal(binding.StrategyId, loadedStep.ExecutionStrategyBinding.StrategyId);
        var state = new ProcessRuntimeStateSnapshot(
            ProcessRunId.New(),
            ProcessRunId.New(),
            loaded.Header.PlanId,
            loaded.PlanHash,
            ProcessRuntimeStatus.Active,
            [
                new ProcessRuntimeStepState(
                    stepId,
                    stepDefinitionId,
                    ProcessRuntimeStepStatus.Ready,
                    true,
                    0,
                    new HashSet<ProcessStepInstanceId>(),
                    new HashSet<ArtifactSlotId>(),
                    null,
                    null)
            ],
            [],
            [],
            new HashSet<ArtifactSlotId>(),
            Now);

        var readyWork = new ProcessRuntimeScheduler().CalculateReadyWork(state, loaded, Now);

        var workItem = Assert.Single(readyWork);
        Assert.Equal(stepId, workItem.StepInstanceId);
        Assert.Equal(binding.StrategyId, workItem.StrategyBinding.StrategyId);
    }

    [Fact]
    public async Task Runtime_step_assignment_store_round_trips_launch_variables_for_execution_metadata()
    {
        await using var dbContext = CreateDbContext();
        var store = new EfProcessRuntimeStepAssignmentStore(dbContext);
        var runId = ProcessRunId.New();
        var stepId = ProcessStepInstanceId.New();
        var producedSlotId = ArtifactSlotId.New();
        var requiredSlotId = ArtifactSlotId.New();
        var assignment = new ProcessRuntimeStepAssignment(
            runId,
            ProcessInstancePlanId.New(),
            stepId,
            "implement-blazor-change",
            "blazor-engineer",
            ProcessLaunchExecutorKinds.Agent,
            Guid.NewGuid().ToString("D"),
            "Blazor engineer",
            "Execute the step.",
            "sha256:readiness",
            "Resolved from test.",
            [producedSlotId],
            [requiredSlotId],
            [ProcessOperationContractNames.MutateProductTarget, ProcessOperationContractNames.ReadProjectStructure],
            ProcessOperationContractNames.ExternalProductTargetMutable,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["ProjectId"] = Guid.NewGuid().ToString("D"),
                ["RepositoryRoot"] = @"C:\programovani\dotnet\output"
            },
            new ProcessRuntimeBranchGate("validate-blazor-runtime", "repair-required"),
            Now);

        await store.SaveAsync([assignment]);
        var loaded = await store.LoadAsync(runId, stepId);

        Assert.NotNull(loaded);
        Assert.Equal(ProcessOperationContractNames.ExternalProductTargetMutable, loaded.OperationTargetScope);
        Assert.Contains(ProcessOperationContractNames.MutateProductTarget, loaded.AllowedOperations);
        Assert.True(loaded.LaunchVariables.TryGetValue("RepositoryRoot", out var repositoryRoot));
        Assert.Equal(@"C:\programovani\dotnet\output", repositoryRoot);
        Assert.Equal("repair-required", loaded.BranchGate?.RequiredOutcomeKey);
    }

    [Fact]
    public void Persistence_model_declares_required_unique_constraints()
    {
        using var dbContext = CreateDbContext();

        AssertHasUniqueConstraint<ProcessRuntimeIdempotencyEntity>(
            dbContext,
            nameof(ProcessRuntimeIdempotencyEntity.RunId),
            nameof(ProcessRuntimeIdempotencyEntity.CommandId));
        AssertHasUniqueConstraint<ProcessDispatchClaimEntity>(
            dbContext,
            nameof(ProcessDispatchClaimEntity.StepInstanceId),
            nameof(ProcessDispatchClaimEntity.ClaimToken));
        AssertHasUniqueConstraint<ProcessStrategyResultReceiptEntity>(
            dbContext,
            nameof(ProcessStrategyResultReceiptEntity.StepInstanceId),
            nameof(ProcessStrategyResultReceiptEntity.StrategyId),
            nameof(ProcessStrategyResultReceiptEntity.IdempotencyKey));
        AssertHasUniqueConstraint<ProcessRuntimeEventEntity>(
            dbContext,
            nameof(ProcessRuntimeEventEntity.EventId));
        AssertHasUniqueConstraint<ProcessRuntimeEventEntity>(
            dbContext,
            nameof(ProcessRuntimeEventEntity.RootRunId),
            nameof(ProcessRuntimeEventEntity.RootSequence));
        AssertHasUniqueConstraint<ProcessOutboxMessageEntity>(
            dbContext,
            nameof(ProcessOutboxMessageEntity.EventId),
            nameof(ProcessOutboxMessageEntity.SubscriberKind));
        AssertHasUniqueConstraint<ProcessArtifactLedgerEventEntity>(
            dbContext,
            nameof(ProcessArtifactLedgerEventEntity.SlotId),
            nameof(ProcessArtifactLedgerEventEntity.LedgerEventId));
    }

    private static ProcessPersistenceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ProcessPersistenceDbContext>()
            .UseInMemoryDatabase($"process-persistence-{Guid.NewGuid():N}")
            .Options;
        return new ProcessPersistenceDbContext(options);
    }

    private static void AssertHasUniqueConstraint<TEntity>(
        ProcessPersistenceDbContext dbContext,
        params string[] propertyNames)
    {
        var entityType = dbContext.Model.FindEntityType(typeof(TEntity))
            ?? throw new InvalidOperationException($"{typeof(TEntity).Name} is not mapped.");

        var keyPropertyNames = entityType.GetKeys()
            .Select(key => key.Properties.Select(property => property.Name).ToArray());
        var indexPropertyNames = entityType.GetIndexes()
            .Where(index => index.IsUnique)
            .Select(index => index.Properties.Select(property => property.Name).ToArray());
        var hasConstraint = keyPropertyNames
            .Concat(indexPropertyNames)
            .Any(actualPropertyNames => actualPropertyNames.SequenceEqual(propertyNames));

        Assert.True(
            hasConstraint,
            $"{typeof(TEntity).Name} must declare a unique constraint on {string.Join(", ", propertyNames)}.");
    }

    private static ProcessRuntimeCommitRequest NewCommitRequest(
        bool includeArtifactLedger,
        ProcessRunId? runId = null,
        ProcessRunId? rootRunId = null,
        RuntimeCommandId? commandId = null,
        ProcessEventType? eventType = null,
        DateTimeOffset? updatedAtUtc = null)
    {
        var actualRunId = runId ?? ProcessRunId.New();
        var actualRootRunId = rootRunId ?? actualRunId;
        var actualUpdatedAtUtc = updatedAtUtc ?? Now;
        var state = NewState(actualRunId, actualRootRunId, actualUpdatedAtUtc);
        var runtimeEvent = NewEvent(
            actualRunId,
            actualRootRunId,
            eventType ?? ProcessRuntimeEventTypes.ProcessRunCompleted,
            actualUpdatedAtUtc);
        var outbox = new ProcessOutboxMessage(
            RuntimeOutboxMessageId.New(),
            runtimeEvent.EventId,
            ProcessOutboxSubscriberKind.RuntimeProjection,
            runtimeEvent.PayloadHash);
        IReadOnlyList<ProcessArtifactLedgerEvent> ledgerEvents = includeArtifactLedger
            ? [
                new ProcessArtifactLedgerEvent(
                    ArtifactLedgerEventId.New(),
                    runtimeEvent.EventId,
                    RequiredArtifactSlotId,
                    ArtifactInstanceId.New(),
                    "hash:artifact")
            ]
            : [];
        var mutation = new ProcessRuntimeMutation(
            ProcessRuntimeTransitionOutcome.Applied,
            state,
            [runtimeEvent],
            [outbox],
            ledgerEvents,
            []);

        return new ProcessRuntimeCommitRequest(
            commandId ?? RuntimeCommandId.New(),
            state,
            mutation);
    }

    private static ProcessRuntimeStateSnapshot NewState(
        ProcessRunId runId,
        ProcessRunId rootRunId,
        DateTimeOffset updatedAtUtc)
    {
        var stepId = ProcessStepInstanceId.New();
        return new ProcessRuntimeStateSnapshot(
            rootRunId,
            runId,
            ProcessInstancePlanId.New(),
            "hash:plan",
            ProcessRuntimeStatus.Completed,
            [
                new ProcessRuntimeStepState(
                    stepId,
                    ProcessStepDefinitionId.New(),
                    ProcessRuntimeStepStatus.Completed,
                    true,
                    1,
                    new HashSet<ProcessStepInstanceId>(),
                    new HashSet<ArtifactSlotId> { RequiredArtifactSlotId },
                    null,
                    StrategyResultIdempotencyKey.New())
            ],
            [],
            [
                new StrategyResultReceipt(
                    stepId,
                    new StrategyId("strategy.test"),
                    StrategyResultIdempotencyKey.New(),
                    StrategyOutcome.Succeeded,
                    ProcessRuntimeStepStatus.Completed,
                    "hash:result")
            ],
            new HashSet<ArtifactSlotId> { RequiredArtifactSlotId },
            updatedAtUtc);
    }

    private static ProcessRuntimeEventEnvelope NewEvent(
        ProcessRunId runId,
        ProcessRunId rootRunId,
        ProcessEventType eventType,
        DateTimeOffset occurredAtUtc)
    {
        return new ProcessRuntimeEventEnvelope(
            RuntimeEventId.New(),
            rootRunId,
            runId,
            new ProcessCorrelationId("corr-1"),
            null,
            new ProcessEventActor(ProcessEventActorKind.System, new ProcessActorId("system")),
            ProcessContractVersions.RuntimeEventEnvelopeV1,
            ProcessEventSensitivity.Normal,
            occurredAtUtc,
            eventType,
            "hash:event");
    }

    private static ProcessInstancePlan NewDispatchablePlan(
        ProcessStepInstanceId stepId,
        ProcessStepDefinitionId stepDefinitionId,
        ProcessStrategyBindingSnapshot binding)
    {
        var planId = ProcessInstancePlanId.New();
        return new ProcessInstancePlan(
            new ProcessInstancePlanHeader(planId, planId, null, null, "processes.instance-plan.v1", Now, 0),
            new ResolvedProcessDefinitionSnapshot(
                ProcessDefinitionId.New(),
                ProcessDefinitionVersionId.New(),
                "sha256:definition",
                RuntimeSchemaVersion,
                RuntimeSchemaVersion,
                [],
                [],
                []),
            new DriverStackSnapshot(
                [
                    new ResolvedDriverSnapshot(
                        binding.DriverId,
                        "1.0.0",
                        ProcessDriverLayer.Scenario,
                        RuntimeSchemaVersion,
                        RuntimeSchemaVersion,
                        new HashSet<CapabilityTag> { new("test") })
                ]),
            new StrategyBindingSet([binding], [], [], []),
            [
                new StepInstancePlan(
                    stepId,
                    stepDefinitionId,
                    "execute-test",
                    ProcessStepKind.Activity,
                    true,
                    false,
                    binding)
            ],
            new ArtifactPlan([], []),
            new BranchRouteTable([]),
            [],
            new ManagerPlan("sha256:manager", null, [], []),
            new BudgetPlan([]),
            new MonitoringPlan(false, "sha256:projection"),
            new SecurityPlan("sha256:governance", []),
            "sha256:plan");
    }

    private const string RuntimeSchemaVersion = "runtime/1.0";
    private static readonly ArtifactSlotId RequiredArtifactSlotId = ArtifactSlotId.New();
}
