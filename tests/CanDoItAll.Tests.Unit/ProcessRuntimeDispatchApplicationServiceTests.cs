using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Builder;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessRuntimeDispatchApplicationServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 17, 9, 0, 0, TimeSpan.Zero);
    private static readonly ProcessRunId RunId = new(new Guid("4a3d2a7b-2dd2-4f5b-b170-0e9c65a62a59"));
    private static readonly ProcessInstancePlanId PlanId = new(new Guid("e9d54367-40da-4240-b98c-9dfbe99ee566"));
    private static readonly ProcessStepInstanceId ValidationStepId = new(new Guid("1c2a3f9c-8fa8-4be1-9a25-1c457608a37f"));
    private static readonly ProcessStepInstanceId RecheckStepId = new(new Guid("ac8f25f1-3194-4178-82f8-a42cc5f764e9"));
    private static readonly ProcessStepInstanceId HandoffStepId = new(new Guid("fa857d0f-a7c1-4538-9412-985580106ea6"));
    private static readonly ProcessStepInstanceId HandoffAfterRepairStepId = new(new Guid("7b7c93ff-6311-4824-a4bb-4a7e5a29cf70"));
    private static readonly ProcessStepInstanceId EscalationStepId = new(new Guid("337d37d2-e145-4d36-85a3-dba8db921078"));
    private static readonly ProcessStrategyBindingSnapshot Binding = new(
        new DriverId("driver.runtime"),
        new StrategyId("strategy.execute"),
        "1.0.0",
        "factory.1.0.0",
        "runtime.1",
        "runtime.1",
        "sha256:binding",
        []);

    [Fact]
    public async Task ExecuteReady_skips_branch_descendants_when_branch_source_was_skipped_and_closes_run()
    {
        var initialState = new ProcessRuntimeStateSnapshot(
            RunId,
            RunId,
            PlanId,
            "sha256:plan",
            ProcessRuntimeStatus.Active,
            [
                NewStep(ValidationStepId, ProcessRuntimeStepStatus.Completed),
                NewStep(RecheckStepId, ProcessRuntimeStepStatus.Skipped),
                NewStep(HandoffStepId, ProcessRuntimeStepStatus.Completed),
                NewStep(HandoffAfterRepairStepId, ProcessRuntimeStepStatus.Blocked),
                NewStep(EscalationStepId, ProcessRuntimeStepStatus.Blocked)
            ],
            [],
            [],
            new HashSet<ArtifactSlotId>(),
            Now);
        var stateStore = new InMemoryRuntimeStateStore(initialState);
        var unitOfWork = new RecordingRuntimeUnitOfWork(stateStore);
        var service = new ProcessRuntimeDispatchApplicationService(
            new TestProcessProjectionClock(Now),
            stateStore,
            unitOfWork,
            new InMemoryPlanStore(NewPlan()),
            new InMemoryAssignmentStore(
            [
                NewAssignment(ValidationStepId, "targeted-validation"),
                NewAssignment(RecheckStepId, "targeted-recheck"),
                NewAssignment(HandoffStepId, "feature-handoff"),
                NewAssignment(HandoffAfterRepairStepId, "feature-handoff-after-repair", new ProcessRuntimeBranchGate("targeted-recheck", "feature-accepted")),
                NewAssignment(EscalationStepId, "feature-repair-escalation", new ProcessRuntimeBranchGate("targeted-recheck", "feature-repair-escalation"))
            ]),
            new ThrowingStrategyFactoryResolver(),
            NewNoOpCatchupService());

        var result = await service.ExecuteReadyAsync(RunId, "unit-test");

        Assert.Equal(ProcessLaunchStage.Completed, result.Stage);
        Assert.Equal(ProcessRuntimeStatus.Completed, result.Status);
        Assert.Equal(ProcessRuntimeStatus.Completed, stateStore.State.Status);
        Assert.Equal(ProcessRuntimeStepStatus.Skipped, FindStep(stateStore.State, HandoffAfterRepairStepId).Status);
        Assert.Equal(ProcessRuntimeStepStatus.Skipped, FindStep(stateStore.State, EscalationStepId).Status);
        Assert.Contains(unitOfWork.Requests.SelectMany(request => request.Mutation.Events), runtimeEvent => runtimeEvent.EventType == ProcessRuntimeEventTypes.ProcessRunCompleted);
    }

    [Theory]
    [InlineData("business-market-sizing")]
    [InlineData("multistep-data-analysis")]
    [InlineData("claims-quality-review")]
    [InlineData("marketing-campaign-planning")]
    public async Task ExecuteReady_dispatches_domain_neutral_process_steps_through_same_strategy_path(string stepKey)
    {
        var observedAtUtc = DateTimeOffset.UtcNow;
        var stepId = ProcessStepInstanceId.New();
        var plan = NewSingleStepPlan(stepId, stepKey);
        var initialState = new ProcessRuntimeStateSnapshot(
            RunId,
            RunId,
            PlanId,
            "sha256:plan",
            ProcessRuntimeStatus.Active,
            [NewStep(stepId, ProcessRuntimeStepStatus.Pending)],
            [],
            [],
            new HashSet<ArtifactSlotId>(),
            observedAtUtc);
        var stateStore = new InMemoryRuntimeStateStore(initialState);
        var unitOfWork = new RecordingRuntimeUnitOfWork(stateStore);
        var strategyResolver = new RecordingStrategyFactoryResolver(stepKey);
        var service = new ProcessRuntimeDispatchApplicationService(
            new TestProcessProjectionClock(observedAtUtc),
            stateStore,
            unitOfWork,
            new InMemoryPlanStore(plan),
            new InMemoryAssignmentStore([]),
            strategyResolver,
            NewNoOpCatchupService());

        var result = await service.ExecuteReadyAsync(RunId, "unit-test");

        Assert.Equal(ProcessLaunchStage.Completed, result.Stage);
        Assert.Equal(ProcessRuntimeStatus.Completed, result.Status);
        Assert.Equal(ProcessRuntimeStepStatus.Completed, FindStep(stateStore.State, stepId).Status);
        Assert.Single(strategyResolver.ExecutionContexts);
        Assert.Equal(stepId, strategyResolver.ExecutionContexts[0].StepId);
        Assert.Contains(unitOfWork.Requests.SelectMany(request => request.Mutation.Events), runtimeEvent => runtimeEvent.EventType == ProcessRuntimeEventTypes.DispatchClaimCompleted);
        Assert.DoesNotContain(unitOfWork.Requests.SelectMany(request => request.Mutation.Events), runtimeEvent => runtimeEvent.PayloadHash.Contains("Tetris", StringComparison.OrdinalIgnoreCase));
    }

    private static ProcessRuntimeStepState NewStep(
        ProcessStepInstanceId stepId,
        ProcessRuntimeStepStatus status)
    {
        return new ProcessRuntimeStepState(
            stepId,
            ProcessStepDefinitionId.New(),
            status,
            true,
            0,
            new HashSet<ProcessStepInstanceId>(),
            new HashSet<ArtifactSlotId>(),
            null,
            null);
    }

    private static ProcessRuntimeStepState FindStep(
        ProcessRuntimeStateSnapshot state,
        ProcessStepInstanceId stepId)
        => state.Steps.Single(step => step.StepInstanceId == stepId);

    private static ProcessInstancePlan NewPlan()
    {
        return new ProcessInstancePlan(
            new ProcessInstancePlanHeader(PlanId, PlanId, null, null, "processes.instance-plan.v1", Now, 0),
            new ResolvedProcessDefinitionSnapshot(
                ProcessDefinitionId.New(),
                ProcessDefinitionVersionId.New(),
                "sha256:definition",
                "template/1",
                "template/1",
                [],
                [],
                []),
            new DriverStackSnapshot([]),
            new StrategyBindingSet([Binding], [], [], []),
            [
                NewPlanStep(ValidationStepId, "targeted-validation"),
                NewPlanStep(RecheckStepId, "targeted-recheck"),
                NewPlanStep(HandoffStepId, "feature-handoff"),
                NewPlanStep(HandoffAfterRepairStepId, "feature-handoff-after-repair"),
                NewPlanStep(EscalationStepId, "feature-repair-escalation")
            ],
            new ArtifactPlan([], []),
            new BranchRouteTable([]),
            [],
            new ManagerPlan("sha256:manager", null, [], []),
            new BudgetPlan([]),
            new MonitoringPlan(true, "sha256:monitoring"),
            new SecurityPlan("sha256:security", []),
            "sha256:plan");
    }

    private static ProcessInstancePlan NewSingleStepPlan(
        ProcessStepInstanceId stepId,
        string stepKey)
    {
        return new ProcessInstancePlan(
            new ProcessInstancePlanHeader(PlanId, PlanId, null, null, "processes.instance-plan.v1", Now, 0),
            new ResolvedProcessDefinitionSnapshot(
                ProcessDefinitionId.New(),
                ProcessDefinitionVersionId.New(),
                "sha256:definition",
                "template/1",
                "template/1",
                [],
                [],
                []),
            new DriverStackSnapshot([]),
            new StrategyBindingSet([Binding], [], [], []),
            [NewPlanStep(stepId, stepKey)],
            new ArtifactPlan([], []),
            new BranchRouteTable([]),
            [],
            new ManagerPlan("sha256:manager", null, [], []),
            new BudgetPlan([]),
            new MonitoringPlan(true, "sha256:monitoring"),
            new SecurityPlan("sha256:security", []),
            "sha256:plan");
    }

    private static StepInstancePlan NewPlanStep(
        ProcessStepInstanceId stepId,
        string stepKey)
        => new(stepId, ProcessStepDefinitionId.New(), stepKey, ProcessStepKind.Activity, true, false, Binding);

    private static ProcessRuntimeStepAssignment NewAssignment(
        ProcessStepInstanceId stepId,
        string stepKey,
        ProcessRuntimeBranchGate? branchGate = null)
    {
        return new ProcessRuntimeStepAssignment(
            RunId,
            PlanId,
            stepId,
            stepKey,
            "role",
            ProcessLaunchExecutorKinds.Agent,
            "agent",
            "Agent",
            "Prompt",
            "sha256:readiness",
            "Test assignment",
            [],
            [],
            [],
            "ExternalProductTargetReadOnly",
            new Dictionary<string, string>(),
            branchGate,
            Now);
    }

    private static ProcessRuntimeProjectionCatchupService NewNoOpCatchupService()
        => new(
            new EmptyRuntimeEventReplayStore(),
            new NoOpProjectionStore(),
            new NoOpRuntimeProjector(),
            new TestProcessProjectionClock(Now));

    private sealed class TestProcessProjectionClock(DateTimeOffset now) : IProcessProjectionClock
    {
        public DateTimeOffset GetUtcNow() => now;
    }

    private sealed class InMemoryRuntimeStateStore(ProcessRuntimeStateSnapshot initialState) : IProcessRuntimeStateStore
    {
        public ProcessRuntimeStateSnapshot State { get; set; } = initialState;

        public Task<ProcessRuntimeStateSnapshot?> LoadAsync(
            ProcessRunId runId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<ProcessRuntimeStateSnapshot?>(State.RunId == runId ? State : null);
    }

    private sealed class RecordingRuntimeUnitOfWork(InMemoryRuntimeStateStore stateStore) : IProcessRuntimeUnitOfWork
    {
        public List<ProcessRuntimeCommitRequest> Requests { get; } = [];

        public Task<ProcessRuntimeCommitResult> CommitAsync(
            ProcessRuntimeCommitRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            stateStore.State = request.Mutation.State;

            return Task.FromResult(ProcessRuntimeCommitResult.FromMutation(request.Mutation));
        }
    }

    private sealed class InMemoryPlanStore(ProcessInstancePlan plan) : IProcessInstancePlanStore
    {
        public ValueTask<PersistedProcessInstancePlan> PersistAsync(
            ProcessInstancePlan plan,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(new PersistedProcessInstancePlan(plan.Header.PlanId, plan.PlanHash));

        public ValueTask<ProcessInstancePlan?> LoadAsync(
            ProcessInstancePlanId planId,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult<ProcessInstancePlan?>(plan.Header.PlanId == planId ? plan : null);
    }

    private sealed class InMemoryAssignmentStore(IReadOnlyList<ProcessRuntimeStepAssignment> assignments) : IProcessRuntimeStepAssignmentStore
    {
        public ValueTask SaveAsync(
            IReadOnlyList<ProcessRuntimeStepAssignment> assignments,
            CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public ValueTask<IReadOnlyList<ProcessRuntimeStepAssignment>> LoadByRunAsync(
            ProcessRunId runId,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(assignments.Where(assignment => assignment.RunId == runId).ToArray() as IReadOnlyList<ProcessRuntimeStepAssignment>);

        public ValueTask<IReadOnlyList<ProcessRuntimeStepAssignment>> FindByLaunchVariablesAsync(
            IReadOnlyDictionary<string, string> requiredVariables,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IReadOnlyList<ProcessRuntimeStepAssignment>>([]);

        public ValueTask<ProcessRuntimeStepAssignment?> LoadAsync(
            ProcessRunId runId,
            ProcessStepInstanceId stepInstanceId,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(assignments.FirstOrDefault(assignment => assignment.RunId == runId && assignment.StepInstanceId == stepInstanceId));
    }

    private sealed class ThrowingStrategyFactoryResolver : IProcessRuntimeStrategyFactoryResolver
    {
        public ValueTask<IProcessStrategyFactory> ResolveAsync(
            ProcessStrategyBindingSnapshot binding,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("No strategy should be resolved when only branch skip propagation is required.");
    }

    private sealed class RecordingStrategyFactoryResolver(string resultKey) : IProcessRuntimeStrategyFactoryResolver
    {
        public List<ProcessStrategyExecutionContext> ExecutionContexts { get; } = [];

        public ValueTask<IProcessStrategyFactory> ResolveAsync(
            ProcessStrategyBindingSnapshot binding,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<IProcessStrategyFactory>(new RecordingStrategyFactory(binding, resultKey, ExecutionContexts));
        }
    }

    private sealed class RecordingStrategyFactory(
        ProcessStrategyBindingSnapshot binding,
        string resultKey,
        List<ProcessStrategyExecutionContext> executionContexts) : IProcessStrategyFactory
    {
        public ProcessStrategyDescriptor Descriptor { get; } = new(
            binding.StrategyId,
            binding.StrategyVersion,
            ProcessStrategyKind.StepExecution,
            new HashSet<CapabilityTag>());

        public ValueTask<IProcessStrategy> CreateAsync(
            ProcessStrategyBindingSnapshot binding,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<IProcessStrategy>(new RecordingStrategy(resultKey, executionContexts));
        }
    }

    private sealed class RecordingStrategy(
        string resultKey,
        List<ProcessStrategyExecutionContext> executionContexts) : IProcessStrategy
    {
        public ValueTask<StrategyResultEnvelope> ExecuteAsync(
            ProcessStrategyExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            executionContexts.Add(context);
            return ValueTask.FromResult(new StrategyResultEnvelope(
                context.Binding.StrategyId,
                context.Binding.StrategyVersion,
                Guid.NewGuid(),
                StrategyOutcome.Succeeded,
                [],
                [],
                [],
                [],
                $"sha256:{resultKey}"));
        }
    }

    private sealed class EmptyRuntimeEventReplayStore : IProcessRuntimeEventReplayStore
    {
        public Task<IReadOnlyList<ProcessStoredRuntimeEvent>> ReadAfterGlobalSequenceAsync(
            long globalSequenceExclusive,
            int take,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProcessStoredRuntimeEvent>>([]);

        public Task<IReadOnlyList<ProcessStoredRuntimeEvent>> ReadByRootRunAsync(
            ProcessRunId rootRunId,
            long rootSequenceExclusive,
            int take,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProcessStoredRuntimeEvent>>([]);
    }

    private sealed class NoOpProjectionStore : IProcessProjectionStore
    {
        public Task UpsertSnapshotAsync(
            ProcessProjectionSnapshot snapshot,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<ProcessProjectionSnapshot?> LoadSnapshotAsync(
            ProcessProjectorName projectorName,
            ProcessProjectionKey projectionKey,
            CancellationToken cancellationToken = default)
            => Task.FromResult<ProcessProjectionSnapshot?>(null);

        public Task<IReadOnlyList<ProcessProjectionSnapshot>> ReadSnapshotsAsync(
            ProcessProjectorName projectorName,
            ProcessProjectionKeyPrefix projectionKeyPrefix,
            int take,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProcessProjectionSnapshot>>([]);

        public Task AppendHistoryAsync(
            ProcessProjectionHistoryRecord history,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<ProcessProjectionHistoryRecord>> ReadHistoryAsync(
            ProcessProjectionHistoryQuery query,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProcessProjectionHistoryRecord>>([]);

        public Task SaveOffsetAsync(
            ProcessProjectorOffset offset,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<ProcessProjectorOffset?> LoadOffsetAsync(
            ProcessProjectorName projectorName,
            ProcessProjectionShardKey shardKey,
            CancellationToken cancellationToken = default)
            => Task.FromResult<ProcessProjectorOffset?>(null);

        public Task WriteDeadLetterAsync(
            ProcessProjectionDeadLetter deadLetter,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<ProcessProjectionDeadLetter>> ReadDeadLettersAsync(
            ProcessProjectorName projectorName,
            ProcessProjectionShardKey shardKey,
            int take,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProcessProjectionDeadLetter>>([]);
    }

    private sealed class NoOpRuntimeProjector : IProcessRuntimeProjector
    {
        public ProcessProjectorName ProjectorName { get; } = new("test-projector");

        public Task ProjectAsync(
            ProcessStoredRuntimeEvent runtimeEvent,
            ProcessProjectionExecutionContext context,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
