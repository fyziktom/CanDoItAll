using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Builder;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessRuntimeDispatchApplicationServiceTests
{
    private const int DispatchAttemptBudget = 20;
    private const int TransientRetrySuppressionBudget = 5;
    private const string AdapterContractRetryDiagnosticCode = "process.adapter.produced_artifact_evidence_missing";
    private const string AdapterContractRetryDiagnosticSummary = "Expected one of the managed artifact refs listed in the process step brief.";
    private const string AgentTransientExecutionRetryDiagnosticCode = "process.adapter.agent_transient_execution_retry";
    private const string AgentTransientExecutionRetryDiagnosticSummary = "Agent execution failed with a transient provider/runtime error.";
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
    public void Dispatch_defaults_keep_governed_attempt_inside_claim_lease()
    {
        var options = new ProcessRuntimeDispatchOptions();

        Assert.Equal(TimeSpan.FromMinutes(60), options.StepExecutionTimeout);
        Assert.Equal(TimeSpan.FromMinutes(75), options.DispatchLease);
        Assert.True(options.DispatchLease > options.StepExecutionTimeout);
    }

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
    public async Task ExecuteReady_reports_only_typed_not_found_for_an_absent_run()
    {
        var existingStepId = ProcessStepInstanceId.New();
        var existingState = new ProcessRuntimeStateSnapshot(
            RunId,
            RunId,
            PlanId,
            "sha256:plan",
            ProcessRuntimeStatus.Active,
            [NewStep(existingStepId, ProcessRuntimeStepStatus.Pending)],
            [],
            [],
            new HashSet<ArtifactSlotId>(),
            Now);
        var stateStore = new InMemoryRuntimeStateStore(existingState);
        var service = new ProcessRuntimeDispatchApplicationService(
            new TestProcessProjectionClock(Now),
            stateStore,
            new RecordingRuntimeUnitOfWork(stateStore),
            new InMemoryPlanStore(NewSingleStepPlan(existingStepId, "implementation")),
            new InMemoryAssignmentStore([]),
            new RecordingStrategyFactoryResolver("implementation"),
            NewNoOpCatchupService());
        var missingRunId = ProcessRunId.New();

        var exception = await Assert.ThrowsAsync<ProcessRuntimeDispatchRunNotFoundException>(
            () => service.ExecuteReadyAsync(missingRunId, "unit-test"));

        Assert.Equal(missingRunId, exception.RunId);
    }

    [Fact]
    public async Task ExecuteReady_rejects_state_plan_hash_drift_before_runtime_mutation_or_strategy_resolution()
    {
        var stepId = ProcessStepInstanceId.New();
        var state = new ProcessRuntimeStateSnapshot(
            RunId,
            RunId,
            PlanId,
            "sha256:state-plan-drift",
            ProcessRuntimeStatus.Active,
            [NewStep(stepId, ProcessRuntimeStepStatus.Ready)],
            [],
            [],
            new HashSet<ArtifactSlotId>(),
            Now);
        var stateStore = new InMemoryRuntimeStateStore(state);
        var unitOfWork = new RecordingRuntimeUnitOfWork(stateStore);
        var strategyResolver = new RecordingStrategyFactoryResolver("implementation");
        var service = new ProcessRuntimeDispatchApplicationService(
            new TestProcessProjectionClock(Now),
            stateStore,
            unitOfWork,
            new InMemoryPlanStore(NewSingleStepPlan(stepId, "implementation")),
            new InMemoryAssignmentStore([]),
            strategyResolver,
            NewNoOpCatchupService());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ExecuteReadyAsync(RunId, "unit-test"));

        Assert.Contains("immutable plan", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(unitOfWork.Requests);
        Assert.Empty(strategyResolver.ExecutionContexts);
    }

    [Fact]
    public async Task ExecuteReady_does_not_dispatch_cancel_requested_run()
    {
        var stepId = ProcessStepInstanceId.New();
        var initialState = new ProcessRuntimeStateSnapshot(
            RunId,
            RunId,
            PlanId,
            "sha256:plan",
            ProcessRuntimeStatus.CancelRequested,
            [NewStep(stepId, ProcessRuntimeStepStatus.Cancelled)],
            [],
            [],
            new HashSet<ArtifactSlotId>(),
            Now);
        var stateStore = new InMemoryRuntimeStateStore(initialState);
        var unitOfWork = new RecordingRuntimeUnitOfWork(stateStore);
        var strategyResolver = new RecordingStrategyFactoryResolver("implementation");
        var service = new ProcessRuntimeDispatchApplicationService(
            new TestProcessProjectionClock(Now),
            stateStore,
            unitOfWork,
            new InMemoryPlanStore(NewSingleStepPlan(stepId, "implementation")),
            new InMemoryAssignmentStore([]),
            strategyResolver,
            NewNoOpCatchupService());

        var result = await service.ExecuteReadyAsync(RunId, "unit-test");

        Assert.Equal(ProcessLaunchStage.Running, result.Stage);
        Assert.Equal(ProcessRuntimeStatus.CancelRequested, result.Status);
        Assert.Empty(unitOfWork.Requests);
        Assert.Empty(strategyResolver.ExecutionContexts);
    }

    [Fact]
    public async Task ExecuteReady_routes_blocked_result_through_shared_recovery_boundary()
    {
        var stepId = ProcessStepInstanceId.New();
        var initialState = new ProcessRuntimeStateSnapshot(
            RunId,
            RunId,
            PlanId,
            "sha256:plan",
            ProcessRuntimeStatus.Blocked,
            [NewStep(stepId, ProcessRuntimeStepStatus.Blocked)],
            [],
            [],
            new HashSet<ArtifactSlotId>(),
            Now);
        var stateStore = new InMemoryRuntimeStateStore(initialState);
        var recoveryCoordinator = new RecordingBlockedRunRecoveryCoordinator(
            new ProcessBlockedRunRecoveryResult(
                RunId,
                ProcessBlockedRunRecoveryOutcome.Recovered,
                ProcessBlockedRunRecoveryActionKind.CurrentStepRework,
                stepId,
                ProcessBlockedRunRecoveryPolicy.SafeIdempotentRework,
                ProcessRuntimeStatus.Active,
                []));
        var service = new ProcessRuntimeDispatchApplicationService(
            new TestProcessProjectionClock(Now),
            stateStore,
            new RecordingRuntimeUnitOfWork(stateStore),
            new InMemoryPlanStore(NewSingleStepPlan(stepId, "implementation")),
            new InMemoryAssignmentStore([]),
            new RecordingStrategyFactoryResolver("implementation"),
            NewNoOpCatchupService(),
            blockedRunRecoveryCoordinator: recoveryCoordinator);

        var result = await service.ExecuteReadyAsync(RunId, "direct-api-test");

        Assert.Equal(ProcessLaunchStage.Running, result.Stage);
        Assert.Equal(ProcessRuntimeStatus.Active, result.Status);
        Assert.Equal((RunId, "direct-api-test"), Assert.Single(recoveryCoordinator.Requests));
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Contains(
                nameof(ProcessBlockedRunRecoveryPolicy.SafeIdempotentRework),
            StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteReady_queues_blocked_parent_recovery_when_child_completes_through_direct_dispatch()
    {
        var parentRunId = ProcessRunId.New();
        var parentStepId = ProcessStepInstanceId.New();
        var childStepId = ProcessStepInstanceId.New();
        var childState = new ProcessRuntimeStateSnapshot(
            parentRunId,
            RunId,
            PlanId,
            "sha256:child-plan",
            ProcessRuntimeStatus.Completed,
            [NewStep(childStepId, ProcessRuntimeStepStatus.Completed)],
            [],
            [],
            new HashSet<ArtifactSlotId>(),
            Now);
        var parentState = new ProcessRuntimeStateSnapshot(
            parentRunId,
            parentRunId,
            PlanId,
            "sha256:parent-plan",
            ProcessRuntimeStatus.Blocked,
            [NewStep(parentStepId, ProcessRuntimeStepStatus.Blocked)],
            [],
            [],
            new HashSet<ArtifactSlotId>(),
            Now.AddMinutes(-1));
        var stateStore = new InMemoryRuntimeStateStore(childState, [parentState]);
        var assignmentStore = new InMemoryAssignmentStore(
        [
            NewAssignment(
                childStepId,
                "child-work",
                launchVariables: ProcessRuntimeLaunchVariables.CreateParentStepLookup(
                    parentRunId,
                    parentStepId)) with
            {
                RunId = RunId
            }
        ]);
        var dispatchQueue = new RecordingDispatchQueue();
        var service = new ProcessRuntimeDispatchApplicationService(
            new TestProcessProjectionClock(Now),
            stateStore,
            new RecordingRuntimeUnitOfWork(stateStore),
            new InMemoryPlanStore(NewSingleStepPlan(childStepId, "child-work")),
            assignmentStore,
            new ThrowingStrategyFactoryResolver(),
            NewNoOpCatchupService(),
            dispatchQueue: dispatchQueue);

        var result = await service.ExecuteReadyAsync(RunId, "direct-api-test");

        Assert.Equal(ProcessRuntimeStatus.Completed, result.Status);
        var request = Assert.Single(dispatchQueue.Requests);
        Assert.Equal(parentRunId, request.RunId);
        Assert.Equal("direct-api-test", request.RequestedBy);
        Assert.True(request.IsRecovery);
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

    [Fact]
    public async Task BranchSignalRouter_applies_branch_outcome_after_engine_result_submission()
    {
        var ownerId = new DispatcherOwnerId("agent-execution-reconciliation");
        var claimToken = DispatchClaimToken.New();
        var resultKey = StrategyResultIdempotencyKey.New();
        var result = new StrategyResultEnvelope(
            Binding.StrategyId,
            Binding.StrategyVersion,
            resultKey.Value,
            StrategyOutcome.Succeeded,
            [],
            [],
            [],
            [
                new ManagerSignal(
                    ProcessBranchSignalCodes.Outcome("feature-accepted"),
                    ReceiptHash("feature-accepted"),
                    "Branch outcome selected: feature-accepted")
            ],
            ReceiptHash("recovered-feature-accepted"));
        var initialState = new ProcessRuntimeStateSnapshot(
            RunId,
            RunId,
            PlanId,
            "sha256:plan",
            ProcessRuntimeStatus.Active,
            [
                NewStep(ValidationStepId, ProcessRuntimeStepStatus.Running, activeClaimToken: claimToken),
                NewStep(HandoffStepId, ProcessRuntimeStepStatus.Blocked, dependencies: [ValidationStepId]),
                NewStep(HandoffAfterRepairStepId, ProcessRuntimeStepStatus.Blocked, dependencies: [ValidationStepId])
            ],
            [
                new DispatchClaimState(
                    claimToken,
                    ValidationStepId,
                    ownerId,
                    DispatchClaimStatus.Claimed,
                    1,
                    Now,
                    Now.AddMinutes(5),
                    null,
                    null)
            ],
            [],
            new HashSet<ArtifactSlotId>(),
            Now);
        var stateStore = new InMemoryRuntimeStateStore(initialState);
        var unitOfWork = new RecordingRuntimeUnitOfWork(stateStore);
        var engine = new ProcessRuntimeEngine(unitOfWork);
        var submitted = await engine.SubmitStrategyResultAsync(
            initialState,
            new RuntimeCommandContext(
                RuntimeCommandId.New(),
                new ProcessEventActor(ProcessEventActorKind.System, new ProcessActorId("agent-execution-reconciliation")),
                new ProcessCorrelationId("agent-execution-reconciliation-unit-test"),
                Now),
            new SubmitStrategyResultCommand(
                ValidationStepId,
                ownerId,
                claimToken,
                resultKey,
                result));
        var router = new ProcessRuntimeBranchSignalApplicationService(
            new TestProcessProjectionClock(Now),
            stateStore,
            unitOfWork,
            new InMemoryAssignmentStore(
            [
                NewAssignment(ValidationStepId, "targeted-validation"),
                NewAssignment(HandoffStepId, "feature-handoff", new ProcessRuntimeBranchGate("targeted-validation", "feature-accepted")),
                NewAssignment(HandoffAfterRepairStepId, "feature-handoff-after-repair", new ProcessRuntimeBranchGate("targeted-validation", "feature-repair"))
            ]),
            NewNoOpCatchupService());

        await router.ApplyForResultAsync(
            submitted.State,
            WithBranchRoutes(
                NewPlan(
                [
                    (ValidationStepId, "targeted-validation"),
                    (HandoffStepId, "feature-handoff"),
                    (HandoffAfterRepairStepId, "feature-handoff-after-repair")
                ]),
                ValidationStepId,
                "feature-accepted",
                "feature-repair"),
            result,
            "agent-execution-reconciliation");

        Assert.Equal(ProcessRuntimeStatus.Active, stateStore.State.Status);
        Assert.Equal(ProcessRuntimeStepStatus.Completed, FindStep(stateStore.State, ValidationStepId).Status);
        Assert.Equal(ProcessRuntimeStepStatus.Pending, FindStep(stateStore.State, HandoffStepId).Status);
        Assert.Equal(ProcessRuntimeStepStatus.Skipped, FindStep(stateStore.State, HandoffAfterRepairStepId).Status);
        Assert.Contains(
            unitOfWork.Requests.SelectMany(request => request.Mutation.Events),
            runtimeEvent => runtimeEvent.EventType == ProcessRuntimeEventTypes.StepSkipped);
    }

    [Theory]
    [InlineData("feature-unknown")]
    [InlineData("Feature-Accepted")]
    public async Task BranchSignalRouter_blocks_without_mutating_gates_when_outcome_is_not_an_exact_plan_route(
        string branchOutcomeKey)
    {
        var resultKey = StrategyResultIdempotencyKey.New();
        var result = new StrategyResultEnvelope(
            Binding.StrategyId,
            Binding.StrategyVersion,
            resultKey.Value,
            StrategyOutcome.Succeeded,
            [],
            [],
            [],
            [
                new ManagerSignal(
                    ProcessBranchSignalCodes.Outcome(branchOutcomeKey),
                    ReceiptHash(branchOutcomeKey.ToLowerInvariant()),
                    $"Branch outcome selected: {branchOutcomeKey}")
            ],
            ReceiptHash("invalid-branch-outcome"));
        var initialState = new ProcessRuntimeStateSnapshot(
            RunId,
            RunId,
            PlanId,
            "sha256:plan",
            ProcessRuntimeStatus.Active,
            [
                NewStep(ValidationStepId, ProcessRuntimeStepStatus.Completed) with
                {
                    CompletedResultKey = resultKey
                },
                NewStep(HandoffStepId, ProcessRuntimeStepStatus.Blocked, dependencies: [ValidationStepId]),
                NewStep(HandoffAfterRepairStepId, ProcessRuntimeStepStatus.Blocked, dependencies: [ValidationStepId])
            ],
            [],
            [
                new StrategyResultReceipt(
                    ValidationStepId,
                    Binding.StrategyId,
                    resultKey,
                    StrategyOutcome.Succeeded,
                    ProcessRuntimeStepStatus.Completed,
                    result.ResultHash)
            ],
            new HashSet<ArtifactSlotId>(),
            Now);
        var stateStore = new InMemoryRuntimeStateStore(initialState);
        var unitOfWork = new RecordingRuntimeUnitOfWork(stateStore);
        var router = new ProcessRuntimeBranchSignalApplicationService(
            new TestProcessProjectionClock(Now),
            stateStore,
            unitOfWork,
            new InMemoryAssignmentStore(
            [
                NewAssignment(ValidationStepId, "targeted-validation"),
                NewAssignment(HandoffStepId, "feature-handoff", new ProcessRuntimeBranchGate("targeted-validation", "feature-accepted")),
                NewAssignment(HandoffAfterRepairStepId, "feature-handoff-after-repair", new ProcessRuntimeBranchGate("targeted-validation", "feature-repair"))
            ]),
            NewNoOpCatchupService());
        var plan = WithBranchRoutes(
            NewPlan(
            [
                (ValidationStepId, "targeted-validation"),
                (HandoffStepId, "feature-handoff"),
                (HandoffAfterRepairStepId, "feature-handoff-after-repair")
            ]),
            ValidationStepId,
            "feature-accepted",
            "feature-repair");

        await router.ApplyForResultAsync(
            initialState,
            plan,
            result,
            "agent-execution-reconciliation");

        Assert.Equal(ProcessRuntimeStatus.Blocked, stateStore.State.Status);
        Assert.Equal(ProcessRuntimeStepStatus.Blocked, FindStep(stateStore.State, ValidationStepId).Status);
        Assert.Equal(ProcessRuntimeStepStatus.Blocked, FindStep(stateStore.State, HandoffStepId).Status);
        Assert.Equal(ProcessRuntimeStepStatus.Blocked, FindStep(stateStore.State, HandoffAfterRepairStepId).Status);
        var receipt = Assert.Single(stateStore.State.AppliedResults);
        Assert.Equal(StrategyOutcome.NeedsManager, receipt.Outcome);
        Assert.Contains(receipt.Diagnostics, diagnostic =>
            diagnostic.Code == ProcessRuntimeDiagnosticCodes.InvalidBranchOutcomeSignal &&
            diagnostic.RetrySafety == ProcessDiagnosticRetrySafety.UnsafeToRetry);
        Assert.Contains(
            unitOfWork.Requests.SelectMany(request => request.Mutation.Diagnostics),
            diagnostic => diagnostic.Code == ProcessRuntimeDiagnosticCodes.InvalidBranchOutcomeSignal);
        Assert.DoesNotContain(
            unitOfWork.Requests.SelectMany(request => request.Mutation.Events),
            runtimeEvent => runtimeEvent.EventType == ProcessRuntimeEventTypes.StepSkipped);
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
        var executionContext = Assert.Single(strategyResolver.ExecutionContexts);
        Assert.Equal(stepId, executionContext.StepId);
        var completedClaim = Assert.Single(
            stateStore.State.Claims,
            claim => claim.StepInstanceId == stepId &&
                     claim.Status == DispatchClaimStatus.Completed);
        Assert.Equal(
            completedClaim.ClaimToken.Value,
            executionContext.DispatchClaimIdentity.Value);
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
    public async Task ExecuteReady_blocks_custom_strategy_on_current_host_capability_loss_and_persists_evidence()
    {
        var observedAtUtc = DateTimeOffset.UtcNow;
        var stepId = ProcessStepInstanceId.New();
        var requiredCapabilities = new HashSet<ProcessHostCapabilityId>
        {
            ProcessHostCapabilityIds.PythonRuntime
        };
        var plan = NewSingleStepPlan(stepId, "implementation");
        plan = plan with
        {
            Steps =
            [
                plan.Steps.Single() with
                {
                    RequiredHostCapabilities = requiredCapabilities
                }
            ]
        };
        var initialState = new ProcessRuntimeStateSnapshot(
            RunId,
            RunId,
            PlanId,
            plan.PlanHash,
            ProcessRuntimeStatus.Active,
            [
                NewStep(stepId, ProcessRuntimeStepStatus.Ready) with
                {
                    RequiredHostCapabilities = requiredCapabilities
                }
            ],
            [],
            [],
            new HashSet<ArtifactSlotId>(),
            observedAtUtc);
        var stateStore = new InMemoryRuntimeStateStore(initialState);
        var unitOfWork = new RecordingRuntimeUnitOfWork(stateStore);
        var strategyResolver = new RecordingStrategyFactoryResolver("must-not-run");
        var unavailableFact = new ProcessHostCapabilityFact(
            ProcessHostCapabilityIds.PythonRuntime,
            ProcessHostCapabilityAvailability.Unavailable,
            ProcessHostCapabilityReason.DependencyMissing,
            ProcessHostExecutionPort.None);
        var service = new ProcessRuntimeDispatchApplicationService(
            new TestProcessProjectionClock(observedAtUtc),
            stateStore,
            unitOfWork,
            new InMemoryPlanStore(plan),
            new InMemoryAssignmentStore([]),
            strategyResolver,
            NewNoOpCatchupService(),
            hostCapabilitySnapshotProvider: new StaticHostCapabilitySnapshotProvider(
                new ProcessHostCapabilitySnapshot(
                    new ProcessHostProfileId("linux-dispatch"),
                    [unavailableFact])));

        var result = await service.ExecuteReadyAsync(RunId, "unit-test");

        Assert.Equal(ProcessRuntimeStatus.Blocked, result.Status);
        Assert.Empty(strategyResolver.ExecutionContexts);
        var receipt = Assert.Single(stateStore.State.AppliedResults);
        Assert.Equal(new ProcessHostProfileId("linux-dispatch"), receipt.HostCapabilityEvidence?.ProfileId);
        Assert.Equal([unavailableFact], receipt.HostCapabilityEvidence?.Capabilities);
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
            ReceiptHash("producer-artifact"));
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
                NewStep(
                    producerStepId,
                    ProcessRuntimeStepStatus.Pending,
                    producedArtifacts: [producedArtifactSlotId]),
                NewStep(
                    consumerStepId,
                    ProcessRuntimeStepStatus.Pending,
                    dependencies: [producerStepId],
                    requiredArtifacts: [producedArtifactSlotId])
            ],
            [],
            [],
            new HashSet<ArtifactSlotId>(),
            observedAtUtc)
        {
            ConnectedInputArtifacts =
            [
                new ProcessRuntimeInputArtifactReceipt(
                    consumerStepId,
                    producedArtifactSlotId,
                    ProcessArtifactInputAvailability.Expected,
                    producerStepId,
                    ArtifactId: null,
                    ContentHash: string.Empty,
                    ConnectionHash: "sha256:producer-consumer-artifact")
            ]
        };
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
            strategyResolver.ExecutionContexts[0].StepContract.ExpectedProducedArtifacts,
            artifact => artifact.SlotId == producedArtifactSlotId);
        Assert.Contains(
            strategyResolver.ExecutionContexts[1].StepContract.RequiredArtifacts,
            artifact => artifact.SlotId == producedArtifactSlotId &&
                        artifact.Availability == ProcessArtifactInputAvailability.Available &&
                        artifact.ArtifactId == producedArtifact.ArtifactId);
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
    public async Task ExecuteReady_sanitizes_and_bounds_deferred_strategy_diagnostics()
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
        var sensitiveMessage = $"password=raw-secret at C:\\private\\dispatch\\receipt.txt {new string('x', 3_000)}";
        var service = new ProcessRuntimeDispatchApplicationService(
            new TestProcessProjectionClock(observedAtUtc),
            stateStore,
            new RecordingRuntimeUnitOfWork(stateStore),
            new InMemoryPlanStore(plan),
            new InMemoryAssignmentStore([]),
            new DeferredStrategyFactoryResolver(childRunId, sensitiveMessage),
            NewNoOpCatchupService());

        var result = await service.ExecuteReadyAsync(RunId, "unit-test");

        Assert.NotEmpty(result.Diagnostics);
        Assert.All(result.Diagnostics, diagnostic => Assert.True(
            ProcessPublicReceiptTextPolicy.IsSafe(
                diagnostic,
                ProcessPublicReceiptTextPolicy.MaximumPublicMessageLength)));
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Contains("raw-secret", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Contains(@"C:\private\dispatch", StringComparison.Ordinal));
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
    public async Task ExecuteReady_blocks_adapter_contract_violation_until_replay_scope_is_verified()
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
        var assignmentStore = new InMemoryAssignmentStore(
            [NewManagedArtifactAssignment(stepId, "review-architecture-design")]);
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

        Assert.Equal(ProcessLaunchStage.Blocked, result.Stage);
        Assert.Equal(ProcessRuntimeStatus.Blocked, result.Status);
        Assert.Equal(ProcessRuntimeStepStatus.Blocked, FindStep(stateStore.State, stepId).Status);
        Assert.Equal(1, strategyResolver.ExecutionCount);
        Assert.Contains(
            stateStore.State.AppliedResults,
            receipt => receipt.Outcome == StrategyOutcome.NeedsManager &&
                       receipt.AppliedStepStatus == ProcessRuntimeStepStatus.Blocked &&
                       receipt.RecoveryDecision is
                       {
                           DecisionKind: ProcessRecoveryDecisionKind.ManagerRequired,
                           RouteKind: ProcessRecoveryRouteKind.ManagerAction,
                           Policy: "process.current-step-replay-scope-review-required"
                       });
        Assert.Contains(
            "Runtime manager recovery instruction",
            assignmentStore.Assignments.Single().Prompt,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteReady_does_not_auto_rework_product_mutation_for_missing_tool_receipt()
    {
        var observedAtUtc = DateTimeOffset.UtcNow;
        var stepId = ProcessStepInstanceId.New();
        var plan = NewSingleStepPlan(stepId, "create-dotnet-project");
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
        var assignmentStore = new InMemoryAssignmentStore(
        [
            NewAssignment(
                stepId,
                "create-dotnet-project",
                launchVariables: CreateDotNetCreateProjectLaunchVariables())
        ]);
        var strategyResolver = new RetryableAdapterViolationThenSuccessStrategyFactoryResolver(
            "process.adapter.product_required_tool_receipt_missing",
            "Step 'create-dotnet-project' claimed completion but required current-run product tool receipt(s) are missing: workspace_pwsh_run_script.",
            "sha256:missing-pwsh-receipt");
        var service = new ProcessRuntimeDispatchApplicationService(
            new TestProcessProjectionClock(observedAtUtc),
            stateStore,
            unitOfWork,
            new InMemoryPlanStore(plan),
            assignmentStore,
            strategyResolver,
            NewNoOpCatchupService(),
            recoveryInstructionBuilder: CreateRecoveryInstructionBuilder());

        var result = await service.ExecuteReadyAsync(RunId, "unit-test");

        Assert.Equal(ProcessLaunchStage.Blocked, result.Stage);
        Assert.Equal(ProcessRuntimeStatus.Blocked, result.Status);
        Assert.Equal(1, strategyResolver.ExecutionCount);
        Assert.Contains(
            "Runtime manager recovery instruction",
            assignmentStore.Assignments.Single().Prompt,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "Runtime diagnostic rework instruction",
            assignmentStore.Assignments.Single().Prompt,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteReady_retries_branch_signal_application_when_runtime_state_changes()
    {
        var observedAtUtc = DateTimeOffset.UtcNow;
        var plan = WithBranchRoutes(
            NewPlan(
            [
                (ValidationStepId, "targeted-validation"),
                (HandoffStepId, "feature-handoff"),
                (HandoffAfterRepairStepId, "feature-handoff-after-repair")
            ]),
            ValidationStepId,
            "feature-accepted",
            "feature-repair");
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
        Assert.Equal(
            strategyResolver.ExecutionContexts.Count,
            strategyResolver.ExecutionContexts
                .Select(context => context.DispatchClaimIdentity)
                .Distinct()
                .Count());
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
    public async Task ExecuteReady_blocks_adapter_manager_result_after_existing_replay_scope_budget()
    {
        var observedAtUtc = DateTimeOffset.UtcNow;
        var stepId = ProcessStepInstanceId.New();
        var plan = NewSingleStepPlan(stepId, "targeted-validation");
        var retryHash = ReceiptHash("retryable-provider-timeout");
        var initialState = new ProcessRuntimeStateSnapshot(
            RunId,
            RunId,
            PlanId,
            "sha256:plan",
            ProcessRuntimeStatus.Active,
            [NewStep(stepId, ProcessRuntimeStepStatus.Ready, attemptNumber: 4)],
            [],
            NewSafeRetryReceipts(stepId, 4, retryHash),
            new HashSet<ArtifactSlotId>(),
            observedAtUtc);
        var stateStore = new InMemoryRuntimeStateStore(initialState);
        var unitOfWork = new RecordingRuntimeUnitOfWork(stateStore);
        var strategyResolver = new RepeatedAutomaticRetryStrategyFactoryResolver(retryHash);
        var service = new ProcessRuntimeDispatchApplicationService(
            new TestProcessProjectionClock(observedAtUtc),
            stateStore,
            unitOfWork,
            new InMemoryPlanStore(plan),
            new InMemoryAssignmentStore(
                [NewManagedArtifactAssignment(stepId, "targeted-validation")]),
            strategyResolver,
            NewNoOpCatchupService());

        var result = await service.ExecuteReadyAsync(RunId, "unit-test");

        Assert.Equal(ProcessLaunchStage.Blocked, result.Stage);
        Assert.Equal(ProcessRuntimeStatus.Blocked, result.Status);
        Assert.Equal(ProcessRuntimeStepStatus.Blocked, FindStep(stateStore.State, stepId).Status);
        Assert.Single(strategyResolver.ExecutionContexts);
        Assert.Contains(
            stateStore.State.AppliedResults,
            receipt => receipt.Outcome == StrategyOutcome.NeedsManager &&
                       receipt.AppliedStepStatus == ProcessRuntimeStepStatus.Ready &&
                       receipt.RecoveryDecision is
                       {
                           DecisionKind: ProcessRecoveryDecisionKind.SafeRetry,
                           RouteKind: ProcessRecoveryRouteKind.CurrentStepRetry
                       });
        Assert.Contains(
            stateStore.State.AppliedResults,
            receipt => receipt.Outcome == StrategyOutcome.NeedsManager &&
                       receipt.AppliedStepStatus == ProcessRuntimeStepStatus.Blocked &&
                       string.Equals(receipt.ResultHash, retryHash, StringComparison.Ordinal) &&
                       receipt.RecoveryDecision is
                       {
                           DecisionKind: ProcessRecoveryDecisionKind.ManagerRequired,
                           RouteKind: ProcessRecoveryRouteKind.ManagerAction,
                           Policy: "process.current-step-safe-retry-budget-exhausted"
                       });
        Assert.Contains(
            unitOfWork.Requests.SelectMany(request => request.Mutation.Events),
            runtimeEvent => runtimeEvent.EventType == ProcessRuntimeEventTypes.StepBlocked);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Contains(AdapterContractRetryDiagnosticSummary, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteReady_blocks_new_result_hash_after_existing_replay_scope_budget()
    {
        var observedAtUtc = DateTimeOffset.UtcNow;
        var stepId = ProcessStepInstanceId.New();
        var plan = NewSingleStepPlan(stepId, "targeted-validation");
        var retryHash = ReceiptHash("retryable-provider-timeout-new-hash");
        var initialState = new ProcessRuntimeStateSnapshot(
            RunId,
            RunId,
            PlanId,
            "sha256:plan",
            ProcessRuntimeStatus.Active,
            [NewStep(stepId, ProcessRuntimeStepStatus.Ready, attemptNumber: 4)],
            [],
            NewSafeRetryReceipts(stepId, 4, resultHash: null),
            new HashSet<ArtifactSlotId>(),
            observedAtUtc);
        var stateStore = new InMemoryRuntimeStateStore(initialState);
        var unitOfWork = new RecordingRuntimeUnitOfWork(stateStore);
        var strategyResolver = new RepeatedAutomaticRetryStrategyFactoryResolver(retryHash);
        var service = new ProcessRuntimeDispatchApplicationService(
            new TestProcessProjectionClock(observedAtUtc),
            stateStore,
            unitOfWork,
            new InMemoryPlanStore(plan),
            new InMemoryAssignmentStore(
                [NewManagedArtifactAssignment(stepId, "targeted-validation")]),
            strategyResolver,
            NewNoOpCatchupService());

        var result = await service.ExecuteReadyAsync(RunId, "unit-test");

        Assert.Equal(ProcessLaunchStage.Blocked, result.Stage);
        Assert.Equal(ProcessRuntimeStatus.Blocked, result.Status);
        Assert.Equal(ProcessRuntimeStepStatus.Blocked, FindStep(stateStore.State, stepId).Status);
        Assert.Single(strategyResolver.ExecutionContexts);
        Assert.Contains(
            stateStore.State.AppliedResults,
            receipt => receipt.Outcome == StrategyOutcome.NeedsManager &&
                       receipt.AppliedStepStatus == ProcessRuntimeStepStatus.Ready &&
                       receipt.RecoveryDecision is
                       {
                           DecisionKind: ProcessRecoveryDecisionKind.SafeRetry,
                           RouteKind: ProcessRecoveryRouteKind.CurrentStepRetry
                       });
        Assert.Contains(
            stateStore.State.AppliedResults,
            receipt => receipt.Outcome == StrategyOutcome.NeedsManager &&
                       receipt.AppliedStepStatus == ProcessRuntimeStepStatus.Blocked &&
                       string.Equals(receipt.ResultHash, retryHash, StringComparison.Ordinal) &&
                       receipt.RecoveryDecision is
                       {
                           DecisionKind: ProcessRecoveryDecisionKind.ManagerRequired,
                           RouteKind: ProcessRecoveryRouteKind.ManagerAction,
                           Policy: "process.current-step-safe-retry-budget-exhausted"
                       });
        Assert.Contains(
            unitOfWork.Requests.SelectMany(request => request.Mutation.Events),
            runtimeEvent => runtimeEvent.EventType == ProcessRuntimeEventTypes.StepBlocked);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Contains(AdapterContractRetryDiagnosticSummary, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteReady_blocks_transient_execution_manager_result_before_global_attempt_budget()
    {
        var observedAtUtc = DateTimeOffset.UtcNow;
        var stepId = ProcessStepInstanceId.New();
        var plan = NewSingleStepPlan(stepId, "capture-runtime-proof");
        var retryHash = ReceiptHash("transient-execution-timeout");
        var initialState = new ProcessRuntimeStateSnapshot(
            RunId,
            RunId,
            PlanId,
            "sha256:plan",
            ProcessRuntimeStatus.Active,
            [NewStep(stepId, ProcessRuntimeStepStatus.Ready, attemptNumber: 4)],
            [],
            NewSubmittedResults(stepId, 3, StrategyOutcome.NeedsManager, ProcessRuntimeStepStatus.Ready, retryHash),
            new HashSet<ArtifactSlotId>(),
            observedAtUtc);
        var stateStore = new InMemoryRuntimeStateStore(initialState);
        var unitOfWork = new RecordingRuntimeUnitOfWork(stateStore);
        var assignmentStore = new InMemoryAssignmentStore([NewAssignment(stepId, "capture-runtime-proof")]);
        var strategyResolver = new RetryableAdapterViolationThenSuccessStrategyFactoryResolver(
            AgentTransientExecutionRetryDiagnosticCode,
            AgentTransientExecutionRetryDiagnosticSummary,
            retryHash);
        var service = new ProcessRuntimeDispatchApplicationService(
            new TestProcessProjectionClock(observedAtUtc),
            stateStore,
            unitOfWork,
            new InMemoryPlanStore(plan),
            assignmentStore,
            strategyResolver,
            NewNoOpCatchupService());

        var result = await service.ExecuteReadyAsync(RunId, "unit-test");

        Assert.Equal(ProcessLaunchStage.Blocked, result.Stage);
        Assert.Equal(ProcessRuntimeStatus.Blocked, result.Status);
        Assert.Equal(ProcessRuntimeStepStatus.Blocked, FindStep(stateStore.State, stepId).Status);
        Assert.Equal(1, strategyResolver.ExecutionCount);
        Assert.Contains(
            stateStore.State.AppliedResults,
            receipt => receipt.Outcome == StrategyOutcome.NeedsManager &&
                       receipt.AppliedStepStatus == ProcessRuntimeStepStatus.Blocked &&
                       string.Equals(receipt.ResultHash, retryHash, StringComparison.Ordinal));
        Assert.Contains(
            unitOfWork.Requests.SelectMany(request => request.Mutation.Events),
            runtimeEvent => runtimeEvent.EventType == ProcessRuntimeEventTypes.StepBlocked);
    }

    [Fact]
    public async Task ExecuteReady_blocks_identical_transient_execution_retry_after_transient_budget()
    {
        var observedAtUtc = DateTimeOffset.UtcNow;
        var stepId = ProcessStepInstanceId.New();
        var plan = NewSingleStepPlan(stepId, "capture-runtime-proof");
        var retryHash = ReceiptHash("transient-execution-loop");
        var initialState = new ProcessRuntimeStateSnapshot(
            RunId,
            RunId,
            PlanId,
            "sha256:plan",
            ProcessRuntimeStatus.Active,
            [NewStep(stepId, ProcessRuntimeStepStatus.Ready, attemptNumber: 6)],
            [],
            NewSubmittedResults(
                stepId,
                TransientRetrySuppressionBudget,
                StrategyOutcome.NeedsManager,
                ProcessRuntimeStepStatus.Ready,
                retryHash),
            new HashSet<ArtifactSlotId>(),
            observedAtUtc);
        var stateStore = new InMemoryRuntimeStateStore(initialState);
        var unitOfWork = new RecordingRuntimeUnitOfWork(stateStore);
        var assignmentStore = new InMemoryAssignmentStore([NewAssignment(stepId, "capture-runtime-proof")]);
        var strategyResolver = new RetryableAdapterViolationThenSuccessStrategyFactoryResolver(
            AgentTransientExecutionRetryDiagnosticCode,
            AgentTransientExecutionRetryDiagnosticSummary,
            retryHash);
        var service = new ProcessRuntimeDispatchApplicationService(
            new TestProcessProjectionClock(observedAtUtc),
            stateStore,
            unitOfWork,
            new InMemoryPlanStore(plan),
            assignmentStore,
            strategyResolver,
            NewNoOpCatchupService());

        var result = await service.ExecuteReadyAsync(RunId, "unit-test");

        Assert.Equal(ProcessLaunchStage.Blocked, result.Stage);
        Assert.Equal(ProcessRuntimeStatus.Blocked, result.Status);
        Assert.Equal(ProcessRuntimeStepStatus.Blocked, FindStep(stateStore.State, stepId).Status);
        Assert.Equal(1, strategyResolver.ExecutionCount);
        Assert.Contains(
            stateStore.State.AppliedResults,
            receipt => receipt.Outcome == StrategyOutcome.NeedsManager &&
                       receipt.AppliedStepStatus == ProcessRuntimeStepStatus.Blocked &&
                       string.Equals(receipt.ResultHash, retryHash, StringComparison.Ordinal));
        Assert.Contains(
            unitOfWork.Requests.SelectMany(request => request.Mutation.Events),
            runtimeEvent => runtimeEvent.EventType == ProcessRuntimeEventTypes.StepBlocked);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Contains(AgentTransientExecutionRetryDiagnosticSummary, StringComparison.Ordinal));
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
    public async Task ExecuteReady_does_not_terminalize_timed_out_strategy_until_ignored_cancellation_execution_stops()
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
        var strategyResolver = new IgnoringCancellationStrategyFactoryResolver();
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
                DispatchLease = TimeSpan.FromMinutes(5),
                StepExecutionTimeout = TimeSpan.FromMilliseconds(100)
            });

        var dispatchTask = service.ExecuteReadyAsync(RunId, "unit-test");
        await strategyResolver.Started.WaitAsync(TimeSpan.FromSeconds(5));
        await strategyResolver.CancellationRequested.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            Assert.False(dispatchTask.IsCompleted);
            Assert.Equal(ProcessRuntimeStatus.Active, stateStore.State.Status);
            Assert.Equal(ProcessRuntimeStepStatus.Running, FindStep(stateStore.State, stepId).Status);
            Assert.Empty(stateStore.State.AppliedResults);
        }
        finally
        {
            strategyResolver.AllowCompletion();
        }

        var result = await dispatchTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(ProcessLaunchStage.Failed, result.Stage);
        Assert.Equal(ProcessRuntimeStatus.Failed, result.Status);
        Assert.True(strategyResolver.SideEffectCompleted);
        Assert.Equal(ProcessRuntimeStepStatus.Failed, FindStep(stateStore.State, stepId).Status);
        Assert.Contains(
            unitOfWork.Requests.SelectMany(request => request.Mutation.Events),
            runtimeEvent => runtimeEvent.EventType == ProcessRuntimeEventTypes.StepFailed);
        Assert.Contains(
            stateStore.State.AppliedResults,
            receipt => receipt.Outcome == StrategyOutcome.Failed &&
                       receipt.ResultHash.StartsWith("sha256:", StringComparison.Ordinal));
        var receipt = Assert.Single(stateStore.State.AppliedResults);
        var diagnostic = Assert.Single(receipt.Diagnostics);
        Assert.Equal("process.runtime.step_execution_timeout", diagnostic.Code);
        Assert.Equal(ProcessDiagnosticRetrySafety.UnsafeToRetry, diagnostic.RetrySafety);
        Assert.Equal(ProcessDiagnosticIdempotencyClassification.Unknown, diagnostic.Idempotency);
    }

    private static ProcessRuntimeStepState NewStep(
        ProcessStepInstanceId stepId,
        ProcessRuntimeStepStatus status,
        int attemptNumber = 0,
        IReadOnlyList<ProcessStepInstanceId>? dependencies = null,
        IReadOnlyList<ArtifactSlotId>? requiredArtifacts = null,
        IReadOnlyList<ArtifactSlotId>? producedArtifacts = null,
        IReadOnlyList<string>? requiredRuntimeToolNames = null,
        DispatchClaimToken? activeClaimToken = null)
    {
        return new ProcessRuntimeStepState(
            stepId,
            new ProcessStepDefinitionId(stepId.Value),
            status,
            true,
            attemptNumber,
            dependencies?.ToHashSet() ?? new HashSet<ProcessStepInstanceId>(),
            requiredArtifacts?.ToHashSet() ?? new HashSet<ArtifactSlotId>(),
            activeClaimToken,
            null)
        {
            ProducedArtifactSlots = producedArtifacts?.ToHashSet() ?? new HashSet<ArtifactSlotId>(),
            RequiredRuntimeToolNames = requiredRuntimeToolNames ?? []
        };
    }

    private static IReadOnlyList<StrategyResultReceipt> NewSubmittedResults(
        ProcessStepInstanceId stepId,
        int count)
        => NewSubmittedResults(
            stepId,
            count,
            StrategyOutcome.Failed,
            ProcessRuntimeStepStatus.Failed,
            null);

    private static IReadOnlyList<StrategyResultReceipt> NewSubmittedResults(
        ProcessStepInstanceId stepId,
        int count,
        StrategyOutcome outcome,
        ProcessRuntimeStepStatus appliedStepStatus,
        string? resultHash)
    {
        var receipts = new List<StrategyResultReceipt>(count);
        for (var index = 0; index < count; index++)
        {
            receipts.Add(new StrategyResultReceipt(
                stepId,
                Binding.StrategyId,
                StrategyResultIdempotencyKey.New(),
                outcome,
                appliedStepStatus,
                ReceiptHash(resultHash ?? $"previous-{index}")));
        }

        return receipts;
    }

    private static IReadOnlyList<StrategyResultReceipt> NewSafeRetryReceipts(
        ProcessStepInstanceId stepId,
        int count,
        string? resultHash)
    {
        return Enumerable.Range(0, count)
            .Select(index => new StrategyResultReceipt(
                stepId,
                Binding.StrategyId,
                StrategyResultIdempotencyKey.New(),
                StrategyOutcome.NeedsManager,
                ProcessRuntimeStepStatus.Ready,
                ReceiptHash(resultHash ?? $"previous-{index}"),
                recoveryDecision: new ProcessRecoveryDecisionReceipt(
                    ProcessFailureCategory.ProductCompletionGate,
                    ProcessRecoveryDecisionKind.SafeRetry,
                    AdapterContractRetryDiagnosticCode,
                    "process.current-step-replay-scope-verified",
                    "Test fixture for a previously verified replay-safe assignment.")
                {
                    RouteKind = ProcessRecoveryRouteKind.CurrentStepRetry,
                    ResponsibleStepInstanceId = stepId
                }))
            .ToArray();
    }

    private static ProcessRuntimeStepState FindStep(
        ProcessRuntimeStateSnapshot state,
        ProcessStepInstanceId stepId)
        => state.Steps.Single(step => step.StepInstanceId == stepId);

    private static string ReceiptHash(string value)
        => ProcessStrategyReceiptValuePolicy.IsSha256Digest(value)
            ? value
            : ProcessPlanHasher.ComputeContentHash(value);

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

    private static ProcessInstancePlan WithBranchRoutes(
        ProcessInstancePlan plan,
        ProcessStepInstanceId branchStepInstanceId,
        params string[] outcomeKeys)
    {
        var branchStepId = plan.Steps
            .Single(step => step.StepInstanceId == branchStepInstanceId)
            .StepDefinitionId;
        var familyId = new BranchFamilyId("test-branch");
        return plan with
        {
            Branches = new BranchRouteTable(outcomeKeys
                .Select(outcomeKey => new BranchRoutePlan(
                    branchStepId,
                    familyId,
                    new BranchOutcomeId(outcomeKey),
                    BranchOutcomeCategory.Continue,
                    new ProcessRouteTarget(ProcessRouteTargetKind.NextStep),
                    LoopBudget: null))
                .ToArray())
        };
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
        => new(
            stepId,
            new ProcessStepDefinitionId(stepId.Value),
            stepKey,
            ProcessStepKind.Activity,
            true,
            false,
            Binding);

    private static ProcessRuntimeStepAssignment NewAssignment(
        ProcessStepInstanceId stepId,
        string stepKey,
        ProcessRuntimeBranchGate? branchGate = null,
        IReadOnlyDictionary<string, string>? launchVariables = null)
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
            launchVariables ?? new Dictionary<string, string>(),
            branchGate,
            Now);
    }

    private static ProcessRuntimeStepAssignment NewManagedArtifactAssignment(
        ProcessStepInstanceId stepId,
        string stepKey)
    {
        return NewAssignment(stepId, stepKey) with
        {
            AllowedOperations =
            [
                nameof(ProcessDefinitionStepOperationKind.ReadProcessContext),
                nameof(ProcessDefinitionStepOperationKind.ReadUpstreamArtifacts),
                nameof(ProcessDefinitionStepOperationKind.WriteManagedProcessArtifacts),
                nameof(ProcessDefinitionStepOperationKind.RecoverArtifactsOnly)
            ],
            OperationTargetScope =
                nameof(ProcessDefinitionStepTargetScopeKind.ManagedProcessArtifactsOnly)
        };
    }

    private static IReadOnlyDictionary<string, string> CreateDotNetCreateProjectLaunchVariables()
    {
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceipts] = "workspace_pwsh_run_script",
            [ProcessRuntimeLaunchVariables.ProductCompletionRequiredFileContentChecks] =
                """[{"pathCandidates":["Calculator.slnx"],"requiredTextAnyGroups":[["src/Calculator/Calculator.csproj"]]}]""",
            ["DotNetCreateProjectScript"] = "$ErrorActionPreference = 'Stop'",
            ["DotNetCreateProjectScriptRef"] = $"artifacts/process-runs/{RunId.Value:D}/scripts/create-dotnet-project.ps1",
            ["DotNetCreateProjectSideEffectManifest"] = """{"version":1,"mode":"ProductMutation"}""",
            ["WorkspaceAlias"] = "external-target/C/repositories/calculator"
        };
    }

    private static ProcessStepRecoveryInstructionBuilder CreateRecoveryInstructionBuilder()
        => new([new GenericProcessRecoveryAdviceProvider()]);

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

    private sealed class StaticHostCapabilitySnapshotProvider(
        ProcessHostCapabilitySnapshot snapshot) : IProcessHostCapabilitySnapshotProvider
    {
        public ValueTask<ProcessHostCapabilitySnapshot> GetAsync(
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(snapshot);
    }

    private sealed class InMemoryRuntimeStateStore : IProcessRuntimeStateStore
    {
        private readonly ProcessRunId primaryRunId;
        private readonly Dictionary<ProcessRunId, ProcessRuntimeStateSnapshot> states;

        public InMemoryRuntimeStateStore(
            ProcessRuntimeStateSnapshot initialState,
            IReadOnlyList<ProcessRuntimeStateSnapshot>? additionalStates = null)
        {
            primaryRunId = initialState.RunId;
            states = (additionalStates ?? [])
                .Append(initialState)
                .ToDictionary(state => state.RunId);
        }

        public ProcessRuntimeStateSnapshot State
        {
            get => states[primaryRunId];
            set => states[value.RunId] = value;
        }

        public Task<ProcessRuntimeStateSnapshot?> LoadAsync(
            ProcessRunId runId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(
                states.TryGetValue(runId, out var state)
                    ? state
                    : null);
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

    private sealed class DeferredStrategyFactoryResolver(
        ProcessRunId childRunId,
        string? deferredMessage = null) : IProcessRuntimeStrategyFactoryResolver
    {
        public ValueTask<IProcessStrategyFactory> ResolveAsync(
            ProcessStrategyBindingSnapshot binding,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<IProcessStrategyFactory>(new DeferredStrategyFactory(
                binding,
                childRunId,
                deferredMessage));
        }
    }

    private sealed class IgnoringCancellationStrategyFactoryResolver : IProcessRuntimeStrategyFactoryResolver
    {
        private readonly TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource cancellationRequested = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource completionAllowed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int sideEffectCompleted;

        public Task Started => started.Task;

        public Task CancellationRequested => cancellationRequested.Task;

        public bool SideEffectCompleted => Volatile.Read(ref sideEffectCompleted) != 0;

        public void AllowCompletion()
            => completionAllowed.TrySetResult();

        public ValueTask<IProcessStrategyFactory> ResolveAsync(
            ProcessStrategyBindingSnapshot binding,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<IProcessStrategyFactory>(new IgnoringCancellationStrategyFactory(
                binding,
                started,
                cancellationRequested,
                completionAllowed,
                () => Interlocked.Exchange(ref sideEffectCompleted, 1)));
        }
    }

    private sealed class RetryableAdapterViolationThenSuccessStrategyFactoryResolver(
        string diagnosticCode = AdapterContractRetryDiagnosticCode,
        string diagnosticSummary = AdapterContractRetryDiagnosticSummary,
        string resultHash = "retryable-missing-evidence") : IProcessRuntimeStrategyFactoryResolver
    {
        public int ExecutionCount { get; private set; }

        public ValueTask<IProcessStrategyFactory> ResolveAsync(
            ProcessStrategyBindingSnapshot binding,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<IProcessStrategyFactory>(new RetryableAdapterViolationThenSuccessStrategyFactory(
                binding,
                () => ++ExecutionCount,
                diagnosticCode,
                diagnosticSummary,
                ReceiptHash(resultHash)));
        }
    }

    private sealed class RepeatedAutomaticRetryStrategyFactoryResolver(
        string resultHash,
        string diagnosticCode = AdapterContractRetryDiagnosticCode,
        string diagnosticSummary = AdapterContractRetryDiagnosticSummary) : IProcessRuntimeStrategyFactoryResolver
    {
        public List<ProcessStrategyExecutionContext> ExecutionContexts { get; } = [];

        public ValueTask<IProcessStrategyFactory> ResolveAsync(
            ProcessStrategyBindingSnapshot binding,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<IProcessStrategyFactory>(new RepeatedAutomaticRetryStrategyFactory(
                binding,
                resultHash,
                diagnosticCode,
                diagnosticSummary,
                ExecutionContexts));
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

        public void EnqueueOrDefer(ProcessRuntimeDispatchQueueRequest request)
        {
            Requests.Add(request);
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
        ProcessRunId childRunId,
        string? deferredMessage) : IProcessStrategyFactory
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
            return ValueTask.FromResult<IProcessStrategy>(new DeferredStrategy(childRunId, deferredMessage));
        }
    }

    private sealed class IgnoringCancellationStrategyFactory(
        ProcessStrategyBindingSnapshot binding,
        TaskCompletionSource started,
        TaskCompletionSource cancellationRequested,
        TaskCompletionSource completionAllowed,
        Action recordSideEffect) : IProcessStrategyFactory
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
            return ValueTask.FromResult<IProcessStrategy>(new IgnoringCancellationStrategy(
                started,
                cancellationRequested,
                completionAllowed,
                recordSideEffect));
        }
    }

    private sealed class RetryableAdapterViolationThenSuccessStrategyFactory(
        ProcessStrategyBindingSnapshot binding,
        Func<int> nextExecutionNumber,
        string diagnosticCode,
        string diagnosticSummary,
        string resultHash) : IProcessStrategyFactory
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
            return ValueTask.FromResult<IProcessStrategy>(new RetryableAdapterViolationThenSuccessStrategy(
                nextExecutionNumber,
                diagnosticCode,
                diagnosticSummary,
                resultHash));
        }
    }

    private sealed class RepeatedAutomaticRetryStrategyFactory(
        ProcessStrategyBindingSnapshot binding,
        string resultHash,
        string diagnosticCode,
        string diagnosticSummary,
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
            return ValueTask.FromResult<IProcessStrategy>(new RepeatedAutomaticRetryStrategy(
                resultHash,
                diagnosticCode,
                diagnosticSummary,
                executionContexts));
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
                        ReceiptHash(branchOutcomeKey),
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
                ReceiptHash(resultKey)));
        }
    }

    private sealed class DeferredStrategy(
        ProcessRunId childRunId,
        string? deferredMessage) : IProcessStrategy
    {
        public ValueTask<StrategyResultEnvelope> ExecuteAsync(
            ProcessStrategyExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new ProcessRuntimeDispatchDeferredException(
                deferredMessage ?? $"Step '{context.StepId}' is waiting for active child process run '{childRunId}'.",
                childRunId);
        }
    }

    private sealed class IgnoringCancellationStrategy(
        TaskCompletionSource started,
        TaskCompletionSource cancellationRequested,
        TaskCompletionSource completionAllowed,
        Action recordSideEffect) : IProcessStrategy
    {
        public async ValueTask<StrategyResultEnvelope> ExecuteAsync(
            ProcessStrategyExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            using var cancellationRegistration = cancellationToken.Register(
                static state => ((TaskCompletionSource)state!).TrySetResult(),
                cancellationRequested);
            started.TrySetResult();
            await completionAllowed.Task.ConfigureAwait(false);
            recordSideEffect();
            return new StrategyResultEnvelope(
                context.Binding.StrategyId,
                context.Binding.StrategyVersion,
                Guid.NewGuid(),
                StrategyOutcome.Succeeded,
                [],
                [],
                [],
                [],
                ReceiptHash("ignored-cancellation"));
        }
    }

    private sealed class RetryableAdapterViolationThenSuccessStrategy(
        Func<int> nextExecutionNumber,
        string diagnosticCode,
        string diagnosticSummary,
        string retryResultHash) : IProcessStrategy
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
                            new StrategyDiagnosticCode(diagnosticCode),
                            StrategyDiagnosticSensitivity.Normal,
                            retryResultHash,
                            diagnosticSummary,
                            RestrictedEvidenceReference: null,
                            ProcessDiagnosticRetrySafety.SafeToRetry,
                            ProcessDiagnosticIdempotencyClassification.Idempotent)
                    ],
                    [
                        new ManagerSignal(
                            new ManagerSignalCode(diagnosticCode),
                            retryResultHash,
                            diagnosticSummary)
                    ],
                    retryResultHash));
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
                ReceiptHash("retryable-success")));
        }
    }

    private sealed class RepeatedAutomaticRetryStrategy(
        string resultHash,
        string diagnosticCode,
        string diagnosticSummary,
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
                StrategyOutcome.NeedsManager,
                [],
                [],
                [
                    new StrategyDiagnosticRef(
                        new StrategyDiagnosticCode(diagnosticCode),
                        StrategyDiagnosticSensitivity.Normal,
                        resultHash,
                        diagnosticSummary,
                        RestrictedEvidenceReference: null,
                        ProcessDiagnosticRetrySafety.SafeToRetry,
                        ProcessDiagnosticIdempotencyClassification.Idempotent)
                ],
                [
                    new ManagerSignal(
                        new ManagerSignalCode(diagnosticCode),
                        resultHash,
                        diagnosticSummary)
                ],
                resultHash));
        }
    }

    private sealed class RecordingBlockedRunRecoveryCoordinator(
        ProcessBlockedRunRecoveryResult result) : IProcessBlockedRunRecoveryCoordinator
    {
        public List<(ProcessRunId RunId, string RequestedBy)> Requests { get; } = [];

        public Task<ProcessBlockedRunRecoveryResult> TryRecoverAsync(
            ProcessRunId runId,
            string requestedBy,
            CancellationToken cancellationToken = default)
        {
            Requests.Add((runId, requestedBy));
            return Task.FromResult(result);
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

        public Task<IReadOnlyList<ProcessProjectionSnapshot>> LoadSnapshotsAsync(
            ProcessProjectorName projectorName,
            IReadOnlyList<ProcessProjectionKey> projectionKeys,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProcessProjectionSnapshot>>([]);

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
