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
    private const int DispatchAttemptBudget = 20;
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
    public void Constructor_rejects_dispatch_lease_that_cannot_outlive_step_timeout()
    {
        var stepId = ProcessStepInstanceId.New();
        var stateStore = new InMemoryRuntimeStateStore(new ProcessRuntimeStateSnapshot(
            RunId,
            RunId,
            PlanId,
            "sha256:plan",
            ProcessRuntimeStatus.Active,
            [NewStep(stepId, ProcessRuntimeStepStatus.Pending)],
            [],
            [],
            new HashSet<ArtifactSlotId>(),
            Now));

        var exception = Assert.Throws<InvalidOperationException>(() => new ProcessRuntimeDispatchApplicationService(
            new TestProcessProjectionClock(Now),
            stateStore,
            new RecordingRuntimeUnitOfWork(stateStore),
            new InMemoryPlanStore(NewSingleStepPlan(stepId, "implementation")),
            new InMemoryAssignmentStore([]),
            new RecordingStrategyFactoryResolver("implementation"),
            NewNoOpCatchupService(),
            new ProcessRuntimeDispatchOptions
            {
                DispatchLease = TimeSpan.FromMinutes(5),
                StepExecutionTimeout = TimeSpan.FromMinutes(5)
            }));

        Assert.Contains("dispatch lease", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("step execution timeout", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

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

    [Fact]
    public async Task ExecuteReady_skips_dependency_descendants_when_branch_path_was_skipped_and_closes_run()
    {
        var releaseApprovalAfterRepairStepId = ProcessStepInstanceId.New();
        var executeReleaseAfterRepairStepId = ProcessStepInstanceId.New();
        var postReleaseAfterRepairStepId = ProcessStepInstanceId.New();
        var initialState = new ProcessRuntimeStateSnapshot(
            RunId,
            RunId,
            PlanId,
            "sha256:plan",
            ProcessRuntimeStatus.Active,
            [
                NewStep(ValidationStepId, ProcessRuntimeStepStatus.Completed),
                NewStep(RecheckStepId, ProcessRuntimeStepStatus.Completed),
                NewStep(HandoffStepId, ProcessRuntimeStepStatus.Completed),
                NewStep(releaseApprovalAfterRepairStepId, ProcessRuntimeStepStatus.Skipped),
                NewStep(executeReleaseAfterRepairStepId, ProcessRuntimeStepStatus.Pending, dependencies: [releaseApprovalAfterRepairStepId]),
                NewStep(postReleaseAfterRepairStepId, ProcessRuntimeStepStatus.Pending, dependencies: [executeReleaseAfterRepairStepId])
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
            new InMemoryPlanStore(NewPlan(
            [
                (ValidationStepId, "qa-validation"),
                (RecheckStepId, "qa-recheck"),
                (HandoffStepId, "post-release-learning"),
                (releaseApprovalAfterRepairStepId, "release-approval-after-repair"),
                (executeReleaseAfterRepairStepId, "execute-release-rollout-after-repair"),
                (postReleaseAfterRepairStepId, "post-release-learning-after-repair")
            ])),
            new InMemoryAssignmentStore(
            [
                NewAssignment(ValidationStepId, "qa-validation"),
                NewAssignment(RecheckStepId, "qa-recheck"),
                NewAssignment(HandoffStepId, "post-release-learning"),
                NewAssignment(releaseApprovalAfterRepairStepId, "release-approval-after-repair", new ProcessRuntimeBranchGate("qa-recheck", "quality-accepted")),
                NewAssignment(executeReleaseAfterRepairStepId, "execute-release-rollout-after-repair"),
                NewAssignment(postReleaseAfterRepairStepId, "post-release-learning-after-repair")
            ]),
            new ThrowingStrategyFactoryResolver(),
            NewNoOpCatchupService());

        var result = await service.ExecuteReadyAsync(RunId, "unit-test");

        Assert.Equal(ProcessLaunchStage.Completed, result.Stage);
        Assert.Equal(ProcessRuntimeStatus.Completed, result.Status);
        Assert.Equal(ProcessRuntimeStepStatus.Skipped, FindStep(stateStore.State, executeReleaseAfterRepairStepId).Status);
        Assert.Equal(ProcessRuntimeStepStatus.Skipped, FindStep(stateStore.State, postReleaseAfterRepairStepId).Status);
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

    [Fact]
    public async Task ExecuteReady_activates_created_run_before_dispatching_pending_work()
    {
        var observedAtUtc = DateTimeOffset.UtcNow;
        var stepId = ProcessStepInstanceId.New();
        var plan = NewSingleStepPlan(stepId, "implementation");
        var initialState = new ProcessRuntimeStateSnapshot(
            RunId,
            RunId,
            PlanId,
            "sha256:plan",
            ProcessRuntimeStatus.Created,
            [NewStep(stepId, ProcessRuntimeStepStatus.Pending)],
            [],
            [],
            new HashSet<ArtifactSlotId>(),
            observedAtUtc);
        var stateStore = new InMemoryRuntimeStateStore(initialState);
        var unitOfWork = new RecordingRuntimeUnitOfWork(stateStore);
        var strategyResolver = new RecordingStrategyFactoryResolver("implementation");
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
        Assert.Contains(unitOfWork.Requests.SelectMany(request => request.Mutation.Events), runtimeEvent => runtimeEvent.EventType == ProcessRuntimeEventTypes.ProcessRunActivated);
    }

    [Fact]
    public async Task ExecuteReady_releases_stale_pre_running_claim_before_dispatching_again()
    {
        var observedAtUtc = DateTimeOffset.UtcNow;
        var stepId = ProcessStepInstanceId.New();
        var staleClaimToken = DispatchClaimToken.New();
        var plan = NewSingleStepPlan(stepId, "implementation");
        var initialState = new ProcessRuntimeStateSnapshot(
            RunId,
            RunId,
            PlanId,
            "sha256:plan",
            ProcessRuntimeStatus.Active,
            [NewStep(stepId, ProcessRuntimeStepStatus.Claimed, activeClaimToken: staleClaimToken)],
            [
                new DispatchClaimState(
                    staleClaimToken,
                    stepId,
                    new DispatcherOwnerId("process-runtime-dispatcher"),
                    DispatchClaimStatus.Claimed,
                    AttemptNumber: 1,
                    observedAtUtc.AddMinutes(-5),
                    observedAtUtc.AddMinutes(20),
                    RenewedAtUtc: null,
                    ResultIdempotencyKey: null)
            ],
            [],
            new HashSet<ArtifactSlotId>(),
            observedAtUtc.AddMinutes(-5));
        var stateStore = new InMemoryRuntimeStateStore(initialState);
        var unitOfWork = new RecordingRuntimeUnitOfWork(stateStore);
        var strategyResolver = new RecordingStrategyFactoryResolver("implementation");
        var service = new ProcessRuntimeDispatchApplicationService(
            new TestProcessProjectionClock(observedAtUtc),
            stateStore,
            unitOfWork,
            new InMemoryPlanStore(plan),
            new InMemoryAssignmentStore([]),
            strategyResolver,
            NewNoOpCatchupService(),
            new ProcessRuntimeDispatchOptions
            {
                DispatchLease = TimeSpan.FromMinutes(25),
                StepExecutionTimeout = TimeSpan.FromMinutes(20),
                PreRunningClaimStaleAfter = TimeSpan.FromMinutes(2)
            });

        var result = await service.ExecuteReadyAsync(RunId, "unit-test");

        Assert.Equal(ProcessLaunchStage.Completed, result.Stage);
        Assert.Equal(ProcessRuntimeStatus.Completed, result.Status);
        Assert.Equal(ProcessRuntimeStepStatus.Completed, FindStep(stateStore.State, stepId).Status);
        Assert.Contains(stateStore.State.Claims, claim => claim.ClaimToken == staleClaimToken && claim.Status == DispatchClaimStatus.Released);
        Assert.Contains(
            unitOfWork.Requests.SelectMany(request => request.Mutation.Events),
            runtimeEvent => runtimeEvent.EventType == ProcessRuntimeEventTypes.DispatchClaimReleased);
        Assert.Contains(
            unitOfWork.Requests.SelectMany(request => request.Mutation.Events),
            runtimeEvent => runtimeEvent.EventType == ProcessRuntimeEventTypes.DispatchClaimCompleted);
        Assert.Single(strategyResolver.ExecutionContexts);
    }

    [Fact]
    public async Task ExecuteReady_dispatches_downstream_pending_step_after_producer_satisfies_required_artifact()
    {
        var observedAtUtc = DateTimeOffset.UtcNow;
        var producerStepId = ProcessStepInstanceId.New();
        var consumerStepId = ProcessStepInstanceId.New();
        var producedArtifactSlotId = ArtifactSlotId.New();
        var producedArtifact = new ProducedArtifactRef(
            ArtifactInstanceId.New(),
            producedArtifactSlotId,
            "sha256:producer-artifact");
        var plan = NewPlan(
        [
            (producerStepId, "feature-intake"),
            (consumerStepId, "architecture-review")
        ]);
        var initialState = new ProcessRuntimeStateSnapshot(
            RunId,
            RunId,
            PlanId,
            "sha256:plan",
            ProcessRuntimeStatus.Active,
            [
                NewStep(producerStepId, ProcessRuntimeStepStatus.Pending),
                NewStep(
                    consumerStepId,
                    ProcessRuntimeStepStatus.Pending,
                    dependencies: [producerStepId],
                    requiredArtifacts: [producedArtifactSlotId])
            ],
            [],
            [],
            new HashSet<ArtifactSlotId>(),
            observedAtUtc);
        var stateStore = new InMemoryRuntimeStateStore(initialState);
        var unitOfWork = new RecordingRuntimeUnitOfWork(stateStore);
        var strategyResolver = new RecordingStrategyFactoryResolver(
            "artifact-chain",
            new Dictionary<ProcessStepInstanceId, IReadOnlyList<ProducedArtifactRef>>
            {
                [producerStepId] = [producedArtifact]
            });
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
        Assert.Equal(ProcessRuntimeStepStatus.Completed, FindStep(stateStore.State, producerStepId).Status);
        Assert.Equal(ProcessRuntimeStepStatus.Completed, FindStep(stateStore.State, consumerStepId).Status);
        Assert.Contains(producedArtifactSlotId, stateStore.State.AvailableArtifactSlots);
        Assert.Equal(
            [producerStepId, consumerStepId],
            strategyResolver.ExecutionContexts.Select(context => context.StepId!.Value).ToArray());
        Assert.Contains(
            unitOfWork.Requests.SelectMany(request => request.Mutation.Events),
            runtimeEvent => runtimeEvent.EventType == ProcessRuntimeEventTypes.StepReady);
        Assert.Equal(
            2,
            unitOfWork.Requests
                .SelectMany(request => request.Mutation.Events)
                .Count(runtimeEvent => runtimeEvent.EventType == ProcessRuntimeEventTypes.StepCompleted));
    }

    [Fact]
    public async Task ExecuteReady_defers_ready_step_without_blocking_when_strategy_reports_active_child_run()
    {
        var observedAtUtc = DateTimeOffset.UtcNow;
        var stepId = ProcessStepInstanceId.New();
        var childRunId = ProcessRunId.New();
        var plan = NewSingleStepPlan(stepId, "architecture-review");
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
        var dispatchQueue = new RecordingDispatchQueue();
        var service = new ProcessRuntimeDispatchApplicationService(
            new TestProcessProjectionClock(observedAtUtc),
            stateStore,
            unitOfWork,
            new InMemoryPlanStore(plan),
            new InMemoryAssignmentStore([]),
            new DeferredStrategyFactoryResolver(childRunId),
            NewNoOpCatchupService(),
            dispatchQueue: dispatchQueue);

        var result = await service.ExecuteReadyAsync(RunId, "unit-test");

        Assert.Equal(ProcessLaunchStage.Running, result.Stage);
        Assert.Equal(ProcessRuntimeStatus.Active, result.Status);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Contains(childRunId.ToString(), StringComparison.OrdinalIgnoreCase));
        Assert.Equal(ProcessRuntimeStepStatus.Waiting, FindStep(stateStore.State, stepId).Status);
        Assert.Equal(1, FindStep(stateStore.State, stepId).AttemptNumber);
        Assert.Contains(stateStore.State.Claims, claim => claim.StepInstanceId == stepId && claim.Status == DispatchClaimStatus.Released);
        Assert.Single(stateStore.State.Claims);
        var queuedRequest = Assert.Single(dispatchQueue.Requests);
        Assert.Equal(childRunId, queuedRequest.RunId);
        Assert.Equal("unit-test", queuedRequest.RequestedBy);
        Assert.Contains(unitOfWork.Requests.SelectMany(request => request.Mutation.Events), runtimeEvent => runtimeEvent.EventType == ProcessRuntimeEventTypes.StepWaiting);
        Assert.Contains(unitOfWork.Requests.SelectMany(request => request.Mutation.Events), runtimeEvent => runtimeEvent.EventType == ProcessRuntimeEventTypes.DispatchClaimReleased);
        Assert.DoesNotContain(unitOfWork.Requests.SelectMany(request => request.Mutation.Events), runtimeEvent => runtimeEvent.EventType == ProcessRuntimeEventTypes.StepBlocked);

        var secondResult = await service.ExecuteReadyAsync(RunId, "unit-test");

        Assert.Equal(ProcessLaunchStage.Running, secondResult.Stage);
        Assert.Equal(ProcessRuntimeStatus.Active, secondResult.Status);
        Assert.Equal(ProcessRuntimeStepStatus.Waiting, FindStep(stateStore.State, stepId).Status);
        Assert.Equal(1, FindStep(stateStore.State, stepId).AttemptNumber);
        Assert.Single(stateStore.State.Claims);
    }

    [Fact]
    public async Task ExecuteReady_retries_deferred_claim_cleanup_when_runtime_state_changes()
    {
        var observedAtUtc = DateTimeOffset.UtcNow;
        var stepId = ProcessStepInstanceId.New();
        var childRunId = ProcessRunId.New();
        var plan = NewSingleStepPlan(stepId, "architecture-review");
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
        var unitOfWork = new TransientConcurrencyRuntimeUnitOfWork(
            stateStore,
            ProcessRuntimeEventTypes.StepWaiting);
        var service = new ProcessRuntimeDispatchApplicationService(
            new TestProcessProjectionClock(observedAtUtc),
            stateStore,
            unitOfWork,
            new InMemoryPlanStore(plan),
            new InMemoryAssignmentStore([]),
            new DeferredStrategyFactoryResolver(childRunId),
            NewNoOpCatchupService());

        var result = await service.ExecuteReadyAsync(RunId, "unit-test");

        Assert.Equal(ProcessLaunchStage.Running, result.Stage);
        Assert.Equal(ProcessRuntimeStatus.Active, result.Status);
        Assert.Equal(1, unitOfWork.ConcurrencyFailures);
        Assert.Equal(ProcessRuntimeStepStatus.Waiting, FindStep(stateStore.State, stepId).Status);
        Assert.Equal(1, FindStep(stateStore.State, stepId).AttemptNumber);
        Assert.Contains(stateStore.State.Claims, claim => claim.StepInstanceId == stepId && claim.Status == DispatchClaimStatus.Released);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Contains("deferral failed", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExecuteReady_retries_claim_creation_when_runtime_state_changes()
    {
        var observedAtUtc = DateTimeOffset.UtcNow;
        var stepId = ProcessStepInstanceId.New();
        var plan = NewSingleStepPlan(stepId, "implementation");
        var initialState = new ProcessRuntimeStateSnapshot(
            RunId,
            RunId,
            PlanId,
            "sha256:plan",
            ProcessRuntimeStatus.Active,
            [NewStep(stepId, ProcessRuntimeStepStatus.Ready)],
            [],
            [],
            new HashSet<ArtifactSlotId>(),
            observedAtUtc);
        var stateStore = new InMemoryRuntimeStateStore(initialState);
        var unitOfWork = new TransientConcurrencyRuntimeUnitOfWork(
            stateStore,
            ProcessRuntimeEventTypes.StepClaimed);
        var strategyResolver = new RecordingStrategyFactoryResolver("implementation");
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
        Assert.Equal(1, unitOfWork.ConcurrencyFailures);
        Assert.Equal(ProcessRuntimeStepStatus.Completed, FindStep(stateStore.State, stepId).Status);
        Assert.Single(strategyResolver.ExecutionContexts);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Contains("changed concurrently", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExecuteReady_retries_running_transition_when_runtime_state_changes()
    {
        var observedAtUtc = DateTimeOffset.UtcNow;
        var stepId = ProcessStepInstanceId.New();
        var plan = NewSingleStepPlan(stepId, "implementation");
        var initialState = new ProcessRuntimeStateSnapshot(
            RunId,
            RunId,
            PlanId,
            "sha256:plan",
            ProcessRuntimeStatus.Active,
            [NewStep(stepId, ProcessRuntimeStepStatus.Ready)],
            [],
            [],
            new HashSet<ArtifactSlotId>(),
            observedAtUtc);
        var stateStore = new InMemoryRuntimeStateStore(initialState);
        var unitOfWork = new TransientConcurrencyRuntimeUnitOfWork(
            stateStore,
            ProcessRuntimeEventTypes.StepRunning);
        var strategyResolver = new RecordingStrategyFactoryResolver("implementation");
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
        Assert.Equal(1, unitOfWork.ConcurrencyFailures);
        Assert.Equal(ProcessRuntimeStepStatus.Completed, FindStep(stateStore.State, stepId).Status);
        Assert.Single(strategyResolver.ExecutionContexts);
    }

    [Fact]
    public async Task ExecuteReady_does_not_invoke_strategy_when_run_is_cancelled_after_claim_running()
    {
        var observedAtUtc = DateTimeOffset.UtcNow;
        var stepId = ProcessStepInstanceId.New();
        var plan = NewSingleStepPlan(stepId, "implementation");
        var initialState = new ProcessRuntimeStateSnapshot(
            RunId,
            RunId,
            PlanId,
            "sha256:plan",
            ProcessRuntimeStatus.Active,
            [NewStep(stepId, ProcessRuntimeStepStatus.Ready)],
            [],
            [],
            new HashSet<ArtifactSlotId>(),
            observedAtUtc);
        var stateStore = new InMemoryRuntimeStateStore(initialState);
        var unitOfWork = new CancelAfterRunningRuntimeUnitOfWork(stateStore);
        var strategyResolver = new RecordingStrategyFactoryResolver("implementation");
        var service = new ProcessRuntimeDispatchApplicationService(
            new TestProcessProjectionClock(observedAtUtc),
            stateStore,
            unitOfWork,
            new InMemoryPlanStore(plan),
            new InMemoryAssignmentStore([]),
            strategyResolver,
            NewNoOpCatchupService());

        var result = await service.ExecuteReadyAsync(RunId, "unit-test");

        Assert.Equal(ProcessLaunchStage.Failed, result.Stage);
        Assert.Equal(ProcessRuntimeStatus.Cancelled, result.Status);
        Assert.Equal(ProcessRuntimeStatus.Cancelled, stateStore.State.Status);
        Assert.Equal(ProcessRuntimeStepStatus.Cancelled, FindStep(stateStore.State, stepId).Status);
        Assert.Empty(strategyResolver.ExecutionContexts);
        Assert.Contains(stateStore.State.Claims, claim => claim.StepInstanceId == stepId && claim.Status == DispatchClaimStatus.Cancelled);
        Assert.DoesNotContain(unitOfWork.Requests.SelectMany(request => request.Mutation.Events), runtimeEvent => runtimeEvent.EventType == ProcessRuntimeEventTypes.DispatchClaimCompleted);
    }

    [Fact]
    public async Task ExecuteReady_retries_result_submission_when_runtime_state_changes()
    {
        var observedAtUtc = DateTimeOffset.UtcNow;
        var stepId = ProcessStepInstanceId.New();
        var plan = NewSingleStepPlan(stepId, "implementation");
        var initialState = new ProcessRuntimeStateSnapshot(
            RunId,
            RunId,
            PlanId,
            "sha256:plan",
            ProcessRuntimeStatus.Active,
            [NewStep(stepId, ProcessRuntimeStepStatus.Ready)],
            [],
            [],
            new HashSet<ArtifactSlotId>(),
            observedAtUtc);
        var stateStore = new InMemoryRuntimeStateStore(initialState);
        var unitOfWork = new TransientConcurrencyRuntimeUnitOfWork(
            stateStore,
            ProcessRuntimeEventTypes.StepCompleted);
        var strategyResolver = new RecordingStrategyFactoryResolver("implementation");
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
        Assert.Equal(1, unitOfWork.ConcurrencyFailures);
        Assert.Equal(ProcessRuntimeStepStatus.Completed, FindStep(stateStore.State, stepId).Status);
        Assert.Single(strategyResolver.ExecutionContexts);
        Assert.Single(stateStore.State.AppliedResults);
    }

    [Fact]
    public async Task ExecuteReady_retries_retryable_adapter_contract_violation_without_manager_block()
    {
        var observedAtUtc = DateTimeOffset.UtcNow;
        var stepId = ProcessStepInstanceId.New();
        var plan = NewSingleStepPlan(stepId, "review-architecture-design");
        var initialState = new ProcessRuntimeStateSnapshot(
            RunId,
            RunId,
            PlanId,
            "sha256:plan",
            ProcessRuntimeStatus.Active,
            [NewStep(stepId, ProcessRuntimeStepStatus.Ready)],
            [],
            [],
            new HashSet<ArtifactSlotId>(),
            observedAtUtc);
        var stateStore = new InMemoryRuntimeStateStore(initialState);
        var unitOfWork = new RecordingRuntimeUnitOfWork(stateStore);
        var assignmentStore = new InMemoryAssignmentStore([NewAssignment(stepId, "review-architecture-design")]);
        var strategyResolver = new RetryableAdapterViolationThenSuccessStrategyFactoryResolver();
        var service = new ProcessRuntimeDispatchApplicationService(
            new TestProcessProjectionClock(observedAtUtc),
            stateStore,
            unitOfWork,
            new InMemoryPlanStore(plan),
            assignmentStore,
            strategyResolver,
            NewNoOpCatchupService());

        var result = await service.ExecuteReadyAsync(RunId, "unit-test");

        Assert.Equal(ProcessLaunchStage.Completed, result.Stage);
        Assert.Equal(ProcessRuntimeStatus.Completed, result.Status);
        Assert.Equal(ProcessRuntimeStepStatus.Completed, FindStep(stateStore.State, stepId).Status);
        Assert.Equal(2, strategyResolver.ExecutionCount);
        Assert.Contains(
            stateStore.State.AppliedResults,
            receipt => receipt.Outcome == StrategyOutcome.NeedsManager &&
                       receipt.AppliedStepStatus == ProcessRuntimeStepStatus.Ready);
        Assert.Contains(
            stateStore.State.AppliedResults,
            receipt => receipt.Outcome == StrategyOutcome.Succeeded &&
                       receipt.AppliedStepStatus == ProcessRuntimeStepStatus.Completed);
        Assert.Contains(
            "Runtime automatic rework instruction",
            assignmentStore.Assignments.Single().Prompt,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            unitOfWork.Requests.SelectMany(request => request.Mutation.Events),
            runtimeEvent => runtimeEvent.EventType == ProcessRuntimeEventTypes.StepBlocked);
    }

    [Fact]
    public async Task ExecuteReady_retries_branch_signal_application_when_runtime_state_changes()
    {
        var observedAtUtc = DateTimeOffset.UtcNow;
        var plan = NewPlan(
        [
            (ValidationStepId, "targeted-validation"),
            (HandoffStepId, "feature-handoff"),
            (HandoffAfterRepairStepId, "feature-handoff-after-repair")
        ]);
        var initialState = new ProcessRuntimeStateSnapshot(
            RunId,
            RunId,
            PlanId,
            "sha256:plan",
            ProcessRuntimeStatus.Active,
            [
                NewStep(ValidationStepId, ProcessRuntimeStepStatus.Ready),
                NewStep(HandoffStepId, ProcessRuntimeStepStatus.Blocked, dependencies: [ValidationStepId]),
                NewStep(HandoffAfterRepairStepId, ProcessRuntimeStepStatus.Blocked, dependencies: [ValidationStepId])
            ],
            [],
            [],
            new HashSet<ArtifactSlotId>(),
            observedAtUtc);
        var stateStore = new InMemoryRuntimeStateStore(initialState);
        var unitOfWork = new TransientConcurrencyRuntimeUnitOfWork(
            stateStore,
            ProcessRuntimeEventTypes.StepSkipped);
        var strategyResolver = new RecordingStrategyFactoryResolver(
            "feature-accepted",
            branchOutcomeKey: "feature-accepted");
        var service = new ProcessRuntimeDispatchApplicationService(
            new TestProcessProjectionClock(observedAtUtc),
            stateStore,
            unitOfWork,
            new InMemoryPlanStore(plan),
            new InMemoryAssignmentStore(
            [
                NewAssignment(ValidationStepId, "targeted-validation"),
                NewAssignment(HandoffStepId, "feature-handoff", new ProcessRuntimeBranchGate("targeted-validation", "feature-accepted")),
                NewAssignment(HandoffAfterRepairStepId, "feature-handoff-after-repair", new ProcessRuntimeBranchGate("targeted-validation", "feature-repair"))
            ]),
            strategyResolver,
            NewNoOpCatchupService());

        var result = await service.ExecuteReadyAsync(RunId, "unit-test");

        Assert.Equal(ProcessLaunchStage.Completed, result.Stage);
        Assert.Equal(ProcessRuntimeStatus.Completed, result.Status);
        Assert.Equal(1, unitOfWork.ConcurrencyFailures);
        Assert.Equal(ProcessRuntimeStepStatus.Completed, FindStep(stateStore.State, ValidationStepId).Status);
        Assert.Equal(ProcessRuntimeStepStatus.Completed, FindStep(stateStore.State, HandoffStepId).Status);
        Assert.Equal(ProcessRuntimeStepStatus.Skipped, FindStep(stateStore.State, HandoffAfterRepairStepId).Status);
        Assert.Equal(
            [ValidationStepId, HandoffStepId],
            strategyResolver.ExecutionContexts.Select(context => context.StepId!.Value).ToArray());
    }

    [Fact]
    public async Task ExecuteReady_does_not_count_released_claims_as_retry_budget()
    {
        var observedAtUtc = DateTimeOffset.UtcNow;
        var stepId = ProcessStepInstanceId.New();
        var plan = NewSingleStepPlan(stepId, "implementation");
        var initialState = new ProcessRuntimeStateSnapshot(
            RunId,
            RunId,
            PlanId,
            "sha256:plan",
            ProcessRuntimeStatus.Active,
            [NewStep(stepId, ProcessRuntimeStepStatus.Ready, attemptNumber: DispatchAttemptBudget)],
            [],
            [],
            new HashSet<ArtifactSlotId>(),
            observedAtUtc);
        var stateStore = new InMemoryRuntimeStateStore(initialState);
        var unitOfWork = new RecordingRuntimeUnitOfWork(stateStore);
        var strategyResolver = new RecordingStrategyFactoryResolver("implementation");
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
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Contains("retry limit", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExecuteReady_blocks_over_budget_ready_step_without_invoking_strategy()
    {
        var observedAtUtc = DateTimeOffset.UtcNow;
        var stepId = ProcessStepInstanceId.New();
        var plan = NewSingleStepPlan(stepId, "implementation");
        var initialState = new ProcessRuntimeStateSnapshot(
            RunId,
            RunId,
            PlanId,
            "sha256:plan",
            ProcessRuntimeStatus.Active,
            [NewStep(stepId, ProcessRuntimeStepStatus.Ready, attemptNumber: DispatchAttemptBudget)],
            [],
            NewSubmittedResults(stepId, DispatchAttemptBudget),
            new HashSet<ArtifactSlotId>(),
            observedAtUtc);
        var stateStore = new InMemoryRuntimeStateStore(initialState);
        var unitOfWork = new RecordingRuntimeUnitOfWork(stateStore);
        var service = new ProcessRuntimeDispatchApplicationService(
            new TestProcessProjectionClock(observedAtUtc),
            stateStore,
            unitOfWork,
            new InMemoryPlanStore(plan),
            new InMemoryAssignmentStore([]),
            new ThrowingStrategyFactoryResolver(),
            NewNoOpCatchupService());

        var result = await service.ExecuteReadyAsync(RunId, "unit-test");

        Assert.Equal(ProcessLaunchStage.Blocked, result.Stage);
        Assert.Equal(ProcessRuntimeStatus.Blocked, result.Status);
        Assert.Equal(ProcessRuntimeStatus.Blocked, stateStore.State.Status);
        Assert.Equal(ProcessRuntimeStepStatus.Blocked, FindStep(stateStore.State, stepId).Status);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Contains("retry limit", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(unitOfWork.Requests.SelectMany(request => request.Mutation.Events), runtimeEvent => runtimeEvent.EventType == ProcessRuntimeEventTypes.StepBlocked);
        Assert.Contains(unitOfWork.Requests.SelectMany(request => request.Mutation.Events), runtimeEvent => runtimeEvent.EventType == ProcessRuntimeEventTypes.ProcessRunBlocked);
        Assert.Contains(stateStore.State.AppliedResults, receipt => receipt.Outcome == StrategyOutcome.NeedsManager);
    }

    [Fact]
    public async Task ExecuteReady_marks_active_run_blocked_when_no_runnable_path_remains()
    {
        var observedAtUtc = DateTimeOffset.UtcNow;
        var implementationStepId = ProcessStepInstanceId.New();
        var validationStepId = ProcessStepInstanceId.New();
        var plan = NewPlan(
        [
            (implementationStepId, "implement-code-change"),
            (validationStepId, "add-tests-and-proof")
        ]);
        var initialState = new ProcessRuntimeStateSnapshot(
            RunId,
            RunId,
            PlanId,
            "sha256:plan",
            ProcessRuntimeStatus.Active,
            [
                NewStep(implementationStepId, ProcessRuntimeStepStatus.Blocked),
                NewStep(
                    validationStepId,
                    ProcessRuntimeStepStatus.Pending,
                    dependencies: [implementationStepId])
            ],
            [],
            [],
            new HashSet<ArtifactSlotId>(),
            observedAtUtc);
        var stateStore = new InMemoryRuntimeStateStore(initialState);
        var unitOfWork = new RecordingRuntimeUnitOfWork(stateStore);
        var service = new ProcessRuntimeDispatchApplicationService(
            new TestProcessProjectionClock(observedAtUtc),
            stateStore,
            unitOfWork,
            new InMemoryPlanStore(plan),
            new InMemoryAssignmentStore([]),
            new ThrowingStrategyFactoryResolver(),
            NewNoOpCatchupService());

        var result = await service.ExecuteReadyAsync(RunId, "unit-test");

        Assert.Equal(ProcessLaunchStage.Blocked, result.Stage);
        Assert.Equal(ProcessRuntimeStatus.Blocked, result.Status);
        Assert.Equal(ProcessRuntimeStatus.Blocked, stateStore.State.Status);
        Assert.Equal(ProcessRuntimeStepStatus.Blocked, FindStep(stateStore.State, implementationStepId).Status);
        Assert.Equal(ProcessRuntimeStepStatus.Pending, FindStep(stateStore.State, validationStepId).Status);
        Assert.Contains(unitOfWork.Requests.SelectMany(request => request.Mutation.Events), runtimeEvent => runtimeEvent.EventType == ProcessRuntimeEventTypes.ProcessRunBlocked);
    }

    [Fact]
    public async Task ExecuteReady_times_out_strategy_that_blocks_before_returning_task()
    {
        var observedAtUtc = DateTimeOffset.UtcNow;
        var stepId = ProcessStepInstanceId.New();
        var plan = NewSingleStepPlan(stepId, "implementation");
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
        var service = new ProcessRuntimeDispatchApplicationService(
            new TestProcessProjectionClock(observedAtUtc),
            stateStore,
            unitOfWork,
            new InMemoryPlanStore(plan),
            new InMemoryAssignmentStore([]),
            new BlockingStrategyFactoryResolver(TimeSpan.FromSeconds(5)),
            NewNoOpCatchupService(),
            new ProcessRuntimeDispatchOptions
            {
                DispatchLease = TimeSpan.FromMinutes(5),
                StepExecutionTimeout = TimeSpan.FromMilliseconds(100)
            });

        var result = await service.ExecuteReadyAsync(RunId, "unit-test");

        Assert.Equal(ProcessLaunchStage.Failed, result.Stage);
        Assert.Equal(ProcessRuntimeStatus.Failed, result.Status);
        Assert.Equal(ProcessRuntimeStepStatus.Failed, FindStep(stateStore.State, stepId).Status);
        Assert.Contains(
            unitOfWork.Requests.SelectMany(request => request.Mutation.Events),
            runtimeEvent => runtimeEvent.EventType == ProcessRuntimeEventTypes.StepFailed);
        Assert.Contains(
            stateStore.State.AppliedResults,
            receipt => receipt.Outcome == StrategyOutcome.Failed &&
                       receipt.ResultHash.StartsWith("sha256:", StringComparison.Ordinal));
    }

    private static ProcessRuntimeStepState NewStep(
        ProcessStepInstanceId stepId,
        ProcessRuntimeStepStatus status,
        int attemptNumber = 0,
        IReadOnlyList<ProcessStepInstanceId>? dependencies = null,
        IReadOnlyList<ArtifactSlotId>? requiredArtifacts = null,
        DispatchClaimToken? activeClaimToken = null)
    {
        return new ProcessRuntimeStepState(
            stepId,
            ProcessStepDefinitionId.New(),
            status,
            true,
            attemptNumber,
            dependencies?.ToHashSet() ?? new HashSet<ProcessStepInstanceId>(),
            requiredArtifacts?.ToHashSet() ?? new HashSet<ArtifactSlotId>(),
            activeClaimToken,
            null);
    }

    private static IReadOnlyList<StrategyResultReceipt> NewSubmittedResults(
        ProcessStepInstanceId stepId,
        int count)
    {
        var receipts = new List<StrategyResultReceipt>(count);
        for (var index = 0; index < count; index++)
        {
            receipts.Add(new StrategyResultReceipt(
                stepId,
                Binding.StrategyId,
                StrategyResultIdempotencyKey.New(),
                StrategyOutcome.Failed,
                ProcessRuntimeStepStatus.Failed,
                $"sha256:previous-{index}"));
        }

        return receipts;
    }

    private static ProcessRuntimeStepState FindStep(
        ProcessRuntimeStateSnapshot state,
        ProcessStepInstanceId stepId)
        => state.Steps.Single(step => step.StepInstanceId == stepId);

    private static ProcessInstancePlan NewPlan()
        => NewPlan(
        [
            (ValidationStepId, "targeted-validation"),
            (RecheckStepId, "targeted-recheck"),
            (HandoffStepId, "feature-handoff"),
            (HandoffAfterRepairStepId, "feature-handoff-after-repair"),
            (EscalationStepId, "feature-repair-escalation")
        ]);

    private static ProcessInstancePlan NewPlan(IReadOnlyList<(ProcessStepInstanceId StepId, string StepKey)> steps)
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
            steps.Select(step => NewPlanStep(step.StepId, step.StepKey)).ToArray(),
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
            "role",
            "Role",
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

    private sealed class TransientConcurrencyRuntimeUnitOfWork(
        InMemoryRuntimeStateStore stateStore,
        ProcessEventType eventTypeToFailOnce) : IProcessRuntimeUnitOfWork
    {
        public List<ProcessRuntimeCommitRequest> Requests { get; } = [];

        public int ConcurrencyFailures { get; private set; }

        public Task<ProcessRuntimeCommitResult> CommitAsync(
            ProcessRuntimeCommitRequest request,
            CancellationToken cancellationToken = default)
        {
            if (ConcurrencyFailures == 0 &&
                request.Mutation.Events.Any(runtimeEvent => runtimeEvent.EventType == eventTypeToFailOnce))
            {
                ConcurrencyFailures++;
                throw new ProcessRuntimeOptimisticConcurrencyException(
                    request.OriginalState.RunId,
                    request.OriginalState.UpdatedAtUtc);
            }

            Requests.Add(request);
            stateStore.State = request.Mutation.State;

            return Task.FromResult(ProcessRuntimeCommitResult.FromMutation(request.Mutation));
        }
    }

    private sealed class CancelAfterRunningRuntimeUnitOfWork(InMemoryRuntimeStateStore stateStore) : IProcessRuntimeUnitOfWork
    {
        public List<ProcessRuntimeCommitRequest> Requests { get; } = [];

        public Task<ProcessRuntimeCommitResult> CommitAsync(
            ProcessRuntimeCommitRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            stateStore.State = request.Mutation.State;

            if (request.Mutation.Events.Any(runtimeEvent => runtimeEvent.EventType == ProcessRuntimeEventTypes.StepRunning))
            {
                stateStore.State = stateStore.State with
                {
                    Status = ProcessRuntimeStatus.Cancelled,
                    Steps = stateStore.State.Steps
                        .Select(step => step with
                        {
                            Status = ProcessRuntimeStepStatus.Cancelled,
                            ActiveClaimToken = null
                        })
                        .ToArray(),
                    Claims = stateStore.State.Claims
                        .Select(claim => claim with
                        {
                            Status = DispatchClaimStatus.Cancelled
                        })
                        .ToArray(),
                    UpdatedAtUtc = stateStore.State.UpdatedAtUtc.AddMilliseconds(1)
                };
            }

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

    private sealed class InMemoryAssignmentStore(IReadOnlyList<ProcessRuntimeStepAssignment> initialAssignments) : IProcessRuntimeStepAssignmentStore
    {
        private readonly List<ProcessRuntimeStepAssignment> assignments = initialAssignments.ToList();

        public IReadOnlyList<ProcessRuntimeStepAssignment> Assignments => assignments;

        public ValueTask SaveAsync(
            IReadOnlyList<ProcessRuntimeStepAssignment> nextAssignments,
            CancellationToken cancellationToken = default)
        {
            foreach (var nextAssignment in nextAssignments)
            {
                var index = assignments.FindIndex(assignment =>
                    assignment.RunId == nextAssignment.RunId &&
                    assignment.StepInstanceId == nextAssignment.StepInstanceId);
                if (index >= 0)
                {
                    assignments[index] = nextAssignment;
                    continue;
                }

                assignments.Add(nextAssignment);
            }

            return ValueTask.CompletedTask;
        }

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

    private sealed class RecordingStrategyFactoryResolver(
        string resultKey,
        IReadOnlyDictionary<ProcessStepInstanceId, IReadOnlyList<ProducedArtifactRef>>? producedArtifactsByStep = null,
        string? branchOutcomeKey = null) : IProcessRuntimeStrategyFactoryResolver
    {
        public List<ProcessStrategyExecutionContext> ExecutionContexts { get; } = [];

        public ValueTask<IProcessStrategyFactory> ResolveAsync(
            ProcessStrategyBindingSnapshot binding,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<IProcessStrategyFactory>(new RecordingStrategyFactory(
                binding,
                resultKey,
                branchOutcomeKey,
                producedArtifactsByStep ?? new Dictionary<ProcessStepInstanceId, IReadOnlyList<ProducedArtifactRef>>(),
                ExecutionContexts));
        }
    }

    private sealed class DeferredStrategyFactoryResolver(ProcessRunId childRunId) : IProcessRuntimeStrategyFactoryResolver
    {
        public ValueTask<IProcessStrategyFactory> ResolveAsync(
            ProcessStrategyBindingSnapshot binding,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<IProcessStrategyFactory>(new DeferredStrategyFactory(binding, childRunId));
        }
    }

    private sealed class BlockingStrategyFactoryResolver(TimeSpan blockDuration) : IProcessRuntimeStrategyFactoryResolver
    {
        public ValueTask<IProcessStrategyFactory> ResolveAsync(
            ProcessStrategyBindingSnapshot binding,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<IProcessStrategyFactory>(new BlockingStrategyFactory(binding, blockDuration));
        }
    }

    private sealed class RetryableAdapterViolationThenSuccessStrategyFactoryResolver : IProcessRuntimeStrategyFactoryResolver
    {
        public int ExecutionCount { get; private set; }

        public ValueTask<IProcessStrategyFactory> ResolveAsync(
            ProcessStrategyBindingSnapshot binding,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<IProcessStrategyFactory>(new RetryableAdapterViolationThenSuccessStrategyFactory(
                binding,
                () => ++ExecutionCount));
        }
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

    private sealed class RecordingStrategyFactory(
        ProcessStrategyBindingSnapshot binding,
        string resultKey,
        string? branchOutcomeKey,
        IReadOnlyDictionary<ProcessStepInstanceId, IReadOnlyList<ProducedArtifactRef>> producedArtifactsByStep,
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
            return ValueTask.FromResult<IProcessStrategy>(new RecordingStrategy(
                resultKey,
                branchOutcomeKey,
                producedArtifactsByStep,
                executionContexts));
        }
    }

    private sealed class DeferredStrategyFactory(
        ProcessStrategyBindingSnapshot binding,
        ProcessRunId childRunId) : IProcessStrategyFactory
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
            return ValueTask.FromResult<IProcessStrategy>(new DeferredStrategy(childRunId));
        }
    }

    private sealed class BlockingStrategyFactory(
        ProcessStrategyBindingSnapshot binding,
        TimeSpan blockDuration) : IProcessStrategyFactory
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
            return ValueTask.FromResult<IProcessStrategy>(new BlockingStrategy(blockDuration));
        }
    }

    private sealed class RetryableAdapterViolationThenSuccessStrategyFactory(
        ProcessStrategyBindingSnapshot binding,
        Func<int> nextExecutionNumber) : IProcessStrategyFactory
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
            return ValueTask.FromResult<IProcessStrategy>(new RetryableAdapterViolationThenSuccessStrategy(nextExecutionNumber));
        }
    }

    private sealed class RecordingStrategy(
        string resultKey,
        string? branchOutcomeKey,
        IReadOnlyDictionary<ProcessStepInstanceId, IReadOnlyList<ProducedArtifactRef>> producedArtifactsByStep,
        List<ProcessStrategyExecutionContext> executionContexts) : IProcessStrategy
    {
        public ValueTask<StrategyResultEnvelope> ExecuteAsync(
            ProcessStrategyExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            executionContexts.Add(context);
            IReadOnlyList<ProducedArtifactRef> producedArtifacts = context.StepId is { } stepId &&
                                                                    producedArtifactsByStep.TryGetValue(stepId, out var artifacts)
                ? artifacts
                : [];
            IReadOnlyList<ManagerSignal> managerSignals = string.IsNullOrWhiteSpace(branchOutcomeKey)
                ? []
                :
                [
                    new ManagerSignal(
                        ProcessBranchSignalCodes.Outcome(branchOutcomeKey),
                        $"sha256:{branchOutcomeKey}",
                        $"Branch outcome selected: {branchOutcomeKey}")
                ];
            return ValueTask.FromResult(new StrategyResultEnvelope(
                context.Binding.StrategyId,
                context.Binding.StrategyVersion,
                Guid.NewGuid(),
                StrategyOutcome.Succeeded,
                producedArtifacts,
                [],
                [],
                managerSignals,
                $"sha256:{resultKey}"));
        }
    }

    private sealed class DeferredStrategy(ProcessRunId childRunId) : IProcessStrategy
    {
        public ValueTask<StrategyResultEnvelope> ExecuteAsync(
            ProcessStrategyExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new ProcessRuntimeDispatchDeferredException(
                $"Step '{context.StepId}' is waiting for active child process run '{childRunId}'.",
                childRunId);
        }
    }

    private sealed class BlockingStrategy(TimeSpan blockDuration) : IProcessStrategy
    {
        public async ValueTask<StrategyResultEnvelope> ExecuteAsync(
            ProcessStrategyExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            Thread.Sleep(blockDuration);
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            return new StrategyResultEnvelope(
                context.Binding.StrategyId,
                context.Binding.StrategyVersion,
                Guid.NewGuid(),
                StrategyOutcome.Succeeded,
                [],
                [],
                [],
                [],
                "sha256:blocking");
        }
    }

    private sealed class RetryableAdapterViolationThenSuccessStrategy(Func<int> nextExecutionNumber) : IProcessStrategy
    {
        public ValueTask<StrategyResultEnvelope> ExecuteAsync(
            ProcessStrategyExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var executionNumber = nextExecutionNumber();
            if (executionNumber == 1)
            {
                return ValueTask.FromResult(new StrategyResultEnvelope(
                    context.Binding.StrategyId,
                    context.Binding.StrategyVersion,
                    Guid.NewGuid(),
                    StrategyOutcome.NeedsManager,
                    [],
                    [],
                    [
                        new StrategyDiagnosticRef(
                            new StrategyDiagnosticCode("process.adapter.produced_artifact_evidence_missing"),
                            StrategyDiagnosticSensitivity.Normal,
                            "sha256:missing-evidence",
                            "Expected one of the managed artifact refs listed in the process step brief.",
                            RestrictedEvidenceReference: null,
                            ProcessDiagnosticRetrySafety.SafeToRetry,
                            ProcessDiagnosticIdempotencyClassification.Idempotent)
                    ],
                    [
                        new ManagerSignal(
                            new ManagerSignalCode("process.adapter.produced_artifact_evidence_missing"),
                            "sha256:missing-evidence",
                            "Expected one of the managed artifact refs listed in the process step brief.")
                    ],
                    "sha256:retryable-missing-evidence"));
            }

            return ValueTask.FromResult(new StrategyResultEnvelope(
                context.Binding.StrategyId,
                context.Binding.StrategyVersion,
                Guid.NewGuid(),
                StrategyOutcome.Succeeded,
                [],
                [],
                [],
                [],
                "sha256:retryable-success"));
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
