using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Builder;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessRuntimeEngineTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly ProcessRunId RunId = new(new Guid("16f34b15-355c-4b39-a495-c63c3fd38af8"));
    private static readonly ProcessInstancePlanId PlanId = new(new Guid("92dc150a-fb57-4922-a188-b354a3134bf2"));
    private static readonly ProcessStepDefinitionId StartDefinitionId = new(new Guid("16b15527-734f-4d64-8163-a8d150d1a2d4"));
    private static readonly ProcessStepDefinitionId ActivityDefinitionId = new(new Guid("476ff502-2703-42b7-b4e5-1b01b4fa8bbd"));
    private static readonly ProcessStepInstanceId StartStepId = new(new Guid("91534843-96bb-42f5-a151-754677094401"));
    private static readonly ProcessStepInstanceId ActivityStepId = new(new Guid("a5e6a611-c39a-4f06-831f-a1c3970f9ff3"));
    private static readonly ArtifactSlotId ArtifactSlotId = new(new Guid("f9da1a4f-3e2d-48f0-bb59-7e178892ef8a"));
    private static readonly ArtifactDefinitionId ArtifactDefinitionId = new(new Guid("9af80e3f-d0d7-4ff4-ac55-08104f292353"));
    private static readonly ArtifactInstanceId ArtifactInstanceId = new(new Guid("1d4b9d35-cea9-4fdc-92fa-942142860e3f"));
    private static readonly StrategyId StrategyId = new("strategy.execute");
    private static readonly DriverId DriverId = new("driver.runtime");
    private static readonly DispatcherOwnerId OwnerId = new("dispatcher.local");
    private static readonly ProcessStrategyBindingSnapshot Binding = new(
        DriverId,
        StrategyId,
        "1.0.0",
        "factory.1.0.0",
        "runtime.1",
        "runtime.1",
        "sha256:binding",
        []);

    [Fact]
    public async Task Activation_commits_runtime_event_and_outbox_message()
    {
        var unitOfWork = new RecordingUnitOfWork();
        var engine = new ProcessRuntimeEngine(unitOfWork);
        var state = NewState(ProcessRuntimeStatus.Created);

        var result = await engine.ActivateAsync(state, Context());

        Assert.True(result.Succeeded);
        Assert.Equal(ProcessRuntimeTransitionOutcome.Applied, result.Outcome);
        Assert.Equal(ProcessRuntimeStatus.Active, result.State.Status);
        var runtimeEvent = Assert.Single(result.Events);
        Assert.Equal(ProcessRuntimeEventTypes.ProcessRunActivated, runtimeEvent.EventType);
        Assert.Single(result.OutboxMessages);
        Assert.Single(unitOfWork.Requests);
    }

    [Fact]
    public async Task Terminal_run_rejects_later_activation_without_commit()
    {
        var unitOfWork = new RecordingUnitOfWork();
        var engine = new ProcessRuntimeEngine(unitOfWork);
        var state = NewState(ProcessRuntimeStatus.Completed);

        var result = await engine.ActivateAsync(state, Context());

        Assert.False(result.Succeeded);
        Assert.Equal(ProcessRuntimeTransitionOutcome.Rejected, result.Outcome);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "Runtime.TerminalRunImmutable");
        Assert.Empty(unitOfWork.Requests);
    }

    [Fact]
    public async Task Scheduler_marks_pending_step_ready_after_dependencies_and_artifacts_are_satisfied()
    {
        var unitOfWork = new RecordingUnitOfWork();
        var engine = new ProcessRuntimeEngine(unitOfWork);
        var state = NewState(
            ProcessRuntimeStatus.Active,
            NewStartStep(ProcessRuntimeStepStatus.Completed),
            NewActivityStep(
                ProcessRuntimeStepStatus.Pending,
                dependencies: new HashSet<ProcessStepInstanceId> { StartStepId },
                requiredArtifacts: new HashSet<ArtifactSlotId> { ArtifactSlotId }),
            availableArtifacts: new HashSet<ArtifactSlotId> { ArtifactSlotId });

        var result = await engine.ScheduleReadyAsync(state, Context());
        var readyStep = result.State.Steps.Single(step => step.StepInstanceId == ActivityStepId);
        var readyWork = new ProcessRuntimeScheduler().CalculateReadyWork(result.State, NewPlan(), Now);

        Assert.True(result.Succeeded);
        Assert.Equal(ProcessRuntimeStepStatus.Ready, readyStep.Status);
        Assert.Contains(result.Events, runtimeEvent => runtimeEvent.EventType == ProcessRuntimeEventTypes.StepReady);
        var workItem = Assert.Single(readyWork);
        Assert.Equal(ActivityStepId, workItem.StepInstanceId);
        Assert.Equal(Binding.StrategyId, workItem.StrategyBinding.StrategyId);
    }

    [Fact]
    public async Task Claim_lifecycle_renews_expires_and_reclaims_ready_work()
    {
        var unitOfWork = new RecordingUnitOfWork();
        var engine = new ProcessRuntimeEngine(unitOfWork);
        var state = NewState(
            ProcessRuntimeStatus.Active,
            NewStartStep(ProcessRuntimeStepStatus.Completed),
            NewActivityStep(ProcessRuntimeStepStatus.Ready));
        var workItem = new DispatchWorkItem(RunId, ActivityStepId, ActivityDefinitionId, Binding, 1);
        var firstToken = DispatchClaimToken.New();
        var create = await engine.CreateClaimAsync(
            state,
            Context(),
            new CreateDispatchClaimCommand(workItem, OwnerId, firstToken, Now.AddMinutes(5)));
        var renew = await engine.RenewClaimAsync(
            create.State,
            Context(Now.AddMinutes(1)),
            new RenewDispatchClaimCommand(ActivityStepId, OwnerId, firstToken, Now.AddMinutes(10)));
        var expire = await engine.ExpireClaimsAsync(
            renew.State,
            Context(Now.AddMinutes(11)),
            new ExpireDispatchClaimsCommand(Now.AddMinutes(11)));
        var secondToken = DispatchClaimToken.New();
        var reclaim = await engine.ReclaimClaimAsync(
            expire.State,
            Context(Now.AddMinutes(12)),
            new ReclaimDispatchClaimCommand(ActivityStepId, OwnerId, secondToken, Now.AddMinutes(17)));

        Assert.Equal(ProcessRuntimeStepStatus.Claimed, create.State.Steps.Single(step => step.StepInstanceId == ActivityStepId).Status);
        Assert.Equal(DispatchClaimStatus.LeaseRenewed, renew.State.Claims.Single(claim => claim.ClaimToken == firstToken).Status);
        Assert.Equal(DispatchClaimStatus.Expired, expire.State.Claims.Single(claim => claim.ClaimToken == firstToken).Status);
        Assert.Equal(ProcessRuntimeStepStatus.Ready, expire.State.Steps.Single(step => step.StepInstanceId == ActivityStepId).Status);
        Assert.Equal(DispatchClaimStatus.Reclaimed, reclaim.State.Claims.Single(claim => claim.ClaimToken == secondToken).Status);
        Assert.Equal(2, reclaim.State.Steps.Single(step => step.StepInstanceId == ActivityStepId).AttemptNumber);
    }

    [Fact]
    public async Task Strategy_result_completes_step_run_event_outbox_and_artifact_ledger()
    {
        var unitOfWork = new RecordingUnitOfWork();
        var engine = new ProcessRuntimeEngine(unitOfWork);
        var token = DispatchClaimToken.New();
        var state = NewState(
            ProcessRuntimeStatus.Active,
            NewStartStep(ProcessRuntimeStepStatus.Completed),
            NewActivityStep(ProcessRuntimeStepStatus.Running, activeClaimToken: token),
            claims:
            [
                new DispatchClaimState(
                    token,
                    ActivityStepId,
                    OwnerId,
                    DispatchClaimStatus.Claimed,
                    1,
                    Now,
                    Now.AddMinutes(5),
                    null,
                    null)
            ]);
        var resultKey = StrategyResultIdempotencyKey.New();

        var result = await engine.SubmitStrategyResultAsync(
            state,
            Context(Now.AddMinutes(1)),
            new SubmitStrategyResultCommand(ActivityStepId, OwnerId, token, resultKey, SucceededResult(producedArtifact: true)));

        Assert.True(result.Succeeded);
        Assert.Equal(ProcessRuntimeStatus.Completed, result.State.Status);
        Assert.Equal(ProcessRuntimeStepStatus.Completed, result.State.Steps.Single(step => step.StepInstanceId == ActivityStepId).Status);
        Assert.Contains(result.Events, runtimeEvent => runtimeEvent.EventType == ProcessRuntimeEventTypes.DispatchClaimCompleted);
        Assert.Contains(result.Events, runtimeEvent => runtimeEvent.EventType == ProcessRuntimeEventTypes.StepCompleted);
        Assert.Contains(result.Events, runtimeEvent => runtimeEvent.EventType == ProcessRuntimeEventTypes.ProcessRunCompleted);
        Assert.Equal(result.Events.Count, result.OutboxMessages.Count);
        var ledgerEvent = Assert.Single(result.ArtifactLedgerEvents);
        Assert.Equal(ArtifactSlotId, ledgerEvent.SlotId);
    }

    [Fact]
    public async Task Duplicate_strategy_result_returns_existing_state_without_second_commit()
    {
        var unitOfWork = new RecordingUnitOfWork();
        var engine = new ProcessRuntimeEngine(unitOfWork);
        var resultKey = StrategyResultIdempotencyKey.New();
        var state = NewState(
            ProcessRuntimeStatus.Active,
            NewStartStep(ProcessRuntimeStepStatus.Completed),
            NewActivityStep(ProcessRuntimeStepStatus.Completed),
            receipts:
            [
                new StrategyResultReceipt(
                    ActivityStepId,
                    StrategyId,
                    resultKey,
                    StrategyOutcome.Succeeded,
                    ProcessRuntimeStepStatus.Completed,
                    "sha256:result")
            ]);

        var result = await engine.SubmitStrategyResultAsync(
            state,
            Context(),
            new SubmitStrategyResultCommand(ActivityStepId, OwnerId, DispatchClaimToken.New(), resultKey, SucceededResult()));

        Assert.Equal(ProcessRuntimeTransitionOutcome.Duplicate, result.Outcome);
        Assert.Empty(result.Events);
        Assert.Empty(unitOfWork.Requests);
    }

    [Fact]
    public async Task Expired_or_lost_claim_rejects_strategy_result()
    {
        var unitOfWork = new RecordingUnitOfWork();
        var engine = new ProcessRuntimeEngine(unitOfWork);
        var token = DispatchClaimToken.New();
        var state = NewState(
            ProcessRuntimeStatus.Active,
            NewStartStep(ProcessRuntimeStepStatus.Completed),
            NewActivityStep(ProcessRuntimeStepStatus.Running, activeClaimToken: token),
            claims:
            [
                new DispatchClaimState(
                    token,
                    ActivityStepId,
                    OwnerId,
                    DispatchClaimStatus.Claimed,
                    1,
                    Now,
                    Now.AddMinutes(1),
                    null,
                    null)
            ]);

        var result = await engine.SubmitStrategyResultAsync(
            state,
            Context(Now.AddMinutes(2)),
            new SubmitStrategyResultCommand(ActivityStepId, OwnerId, token, StrategyResultIdempotencyKey.New(), SucceededResult()));

        Assert.False(result.Succeeded);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "Runtime.ClaimExpired");
        Assert.Empty(unitOfWork.Requests);
    }

    [Fact]
    public async Task Cancellation_without_open_claims_terminally_cancels_run_and_steps()
    {
        var unitOfWork = new RecordingUnitOfWork();
        var engine = new ProcessRuntimeEngine(unitOfWork);
        var state = NewState(
            ProcessRuntimeStatus.Active,
            NewStartStep(ProcessRuntimeStepStatus.Completed),
            NewActivityStep(ProcessRuntimeStepStatus.Ready));

        var result = await engine.RequestCancellationAsync(state, Context());

        Assert.True(result.Succeeded);
        Assert.Equal(ProcessRuntimeStatus.Cancelled, result.State.Status);
        Assert.Equal(ProcessRuntimeStepStatus.Cancelled, result.State.Steps.Single(step => step.StepInstanceId == ActivityStepId).Status);
        Assert.Contains(result.Events, runtimeEvent => runtimeEvent.EventType == ProcessRuntimeEventTypes.ProcessRunCancelled);
    }

    [Fact]
    public async Task Cancellation_with_open_claim_only_requests_cancel_until_claim_drains()
    {
        var unitOfWork = new RecordingUnitOfWork();
        var engine = new ProcessRuntimeEngine(unitOfWork);
        var token = DispatchClaimToken.New();
        var state = NewState(
            ProcessRuntimeStatus.Active,
            NewStartStep(ProcessRuntimeStepStatus.Completed),
            NewActivityStep(ProcessRuntimeStepStatus.Running, activeClaimToken: token),
            claims:
            [
                new DispatchClaimState(
                    token,
                    ActivityStepId,
                    OwnerId,
                    DispatchClaimStatus.Claimed,
                    1,
                    Now,
                    Now.AddMinutes(5),
                    null,
                    null)
            ]);

        var result = await engine.RequestCancellationAsync(state, Context());

        Assert.True(result.Succeeded);
        Assert.Equal(ProcessRuntimeStatus.CancelRequested, result.State.Status);
        Assert.Equal(ProcessRuntimeStepStatus.Running, result.State.Steps.Single(step => step.StepInstanceId == ActivityStepId).Status);
        Assert.Contains(result.Events, runtimeEvent => runtimeEvent.EventType == ProcessRuntimeEventTypes.ProcessRunCancelRequested);
    }

    [Fact]
    public async Task Failed_strategy_result_terminally_fails_run_and_emits_failure_event()
    {
        var unitOfWork = new RecordingUnitOfWork();
        var engine = new ProcessRuntimeEngine(unitOfWork);
        var token = DispatchClaimToken.New();
        var state = NewState(
            ProcessRuntimeStatus.Active,
            NewStartStep(ProcessRuntimeStepStatus.Completed),
            NewActivityStep(ProcessRuntimeStepStatus.Running, activeClaimToken: token),
            claims:
            [
                new DispatchClaimState(
                    token,
                    ActivityStepId,
                    OwnerId,
                    DispatchClaimStatus.Claimed,
                    1,
                    Now,
                    Now.AddMinutes(5),
                    null,
                    null)
            ]);

        var result = await engine.SubmitStrategyResultAsync(
            state,
            Context(Now.AddMinutes(1)),
            new SubmitStrategyResultCommand(
                ActivityStepId,
                OwnerId,
                token,
                StrategyResultIdempotencyKey.New(),
                FailedResult()));

        Assert.True(result.Succeeded);
        Assert.Equal(ProcessRuntimeStatus.Failed, result.State.Status);
        Assert.Contains(result.Events, runtimeEvent => runtimeEvent.EventType == ProcessRuntimeEventTypes.ProcessRunFailed);
    }

    [Fact]
    public async Task Strategy_dispatcher_invokes_factory_with_plan_binding_only()
    {
        var dispatcher = new ProcessStrategyDispatcher();
        var plan = NewPlan();
        var workItem = new DispatchWorkItem(RunId, ActivityStepId, ActivityDefinitionId, Binding, 1);
        var strategy = new RecordingStrategy(SucceededResult());
        var factory = new RecordingStrategyFactory(Binding.StrategyId, strategy);

        var result = await dispatcher.InvokeAsync(workItem, plan, factory);

        Assert.Equal(StrategyOutcome.Succeeded, result.Outcome);
        Assert.NotNull(strategy.Context);
        Assert.Equal(ActivityStepId, strategy.Context.StepId);
        Assert.Equal(Binding, strategy.Context.Binding);
    }

    private static RuntimeCommandContext Context(DateTimeOffset? now = null)
    {
        return new RuntimeCommandContext(
            RuntimeCommandId.New(),
            new ProcessEventActor(ProcessEventActorKind.System, new ProcessActorId("runtime")),
            new ProcessCorrelationId("correlation.runtime"),
            now ?? Now);
    }

    private static ProcessRuntimeStateSnapshot NewState(
        ProcessRuntimeStatus status,
        ProcessRuntimeStepState? firstStep = null,
        ProcessRuntimeStepState? secondStep = null,
        IReadOnlyList<DispatchClaimState>? claims = null,
        IReadOnlyList<StrategyResultReceipt>? receipts = null,
        IReadOnlySet<ArtifactSlotId>? availableArtifacts = null)
    {
        return new ProcessRuntimeStateSnapshot(
            RunId,
            RunId,
            PlanId,
            "sha256:plan",
            status,
            [firstStep ?? NewStartStep(ProcessRuntimeStepStatus.Pending), secondStep ?? NewActivityStep(ProcessRuntimeStepStatus.Pending)],
            claims ?? [],
            receipts ?? [],
            availableArtifacts ?? new HashSet<ArtifactSlotId>(),
            Now);
    }

    private static ProcessRuntimeStepState NewStartStep(ProcessRuntimeStepStatus status)
    {
        return new ProcessRuntimeStepState(
            StartStepId,
            StartDefinitionId,
            status,
            false,
            0,
            new HashSet<ProcessStepInstanceId>(),
            new HashSet<ArtifactSlotId>(),
            null,
            null);
    }

    private static ProcessRuntimeStepState NewActivityStep(
        ProcessRuntimeStepStatus status,
        IReadOnlySet<ProcessStepInstanceId>? dependencies = null,
        IReadOnlySet<ArtifactSlotId>? requiredArtifacts = null,
        DispatchClaimToken? activeClaimToken = null)
    {
        return new ProcessRuntimeStepState(
            ActivityStepId,
            ActivityDefinitionId,
            status,
            true,
            activeClaimToken is null ? 0 : 1,
            dependencies ?? new HashSet<ProcessStepInstanceId>(),
            requiredArtifacts ?? new HashSet<ArtifactSlotId>(),
            activeClaimToken,
            null);
    }

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
                new StepInstancePlan(StartStepId, StartDefinitionId, "start", ProcessStepKind.Start, false, false, null),
                new StepInstancePlan(ActivityStepId, ActivityDefinitionId, "activity", ProcessStepKind.Activity, true, false, Binding)
            ],
            new ArtifactPlan(
                [new ArtifactSlotPlan(ArtifactSlotId, "slot.output", ArtifactDefinitionId, ProcessArtifactRequirementMode.Produced, ProcessArtifactScope.Local)],
                []),
            new BranchRouteTable([]),
            [],
            new ManagerPlan("sha256:manager", null, [], []),
            new BudgetPlan([]),
            new MonitoringPlan(true, "sha256:monitoring"),
            new SecurityPlan("sha256:security", []),
            "sha256:plan");
    }

    private static StrategyResultEnvelope SucceededResult(bool producedArtifact = false)
    {
        return new StrategyResultEnvelope(
            StrategyId,
            "1.0.0",
            Guid.NewGuid(),
            StrategyOutcome.Succeeded,
            producedArtifact
                ? [new ProducedArtifactRef(ArtifactInstanceId, ArtifactSlotId, "sha256:artifact")]
                : [],
            [],
            [],
            [],
            "sha256:result");
    }

    private static StrategyResultEnvelope FailedResult()
    {
        return new StrategyResultEnvelope(
            StrategyId,
            "1.0.0",
            Guid.NewGuid(),
            StrategyOutcome.Failed,
            [],
            [],
            [],
            [],
            "sha256:failed-result");
    }

    private sealed class RecordingUnitOfWork : IProcessRuntimeUnitOfWork
    {
        public List<ProcessRuntimeCommitRequest> Requests { get; } = [];

        public Task<ProcessRuntimeCommitResult> CommitAsync(
            ProcessRuntimeCommitRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);

            return Task.FromResult(ProcessRuntimeCommitResult.FromMutation(request.Mutation));
        }
    }

    private sealed class RecordingStrategyFactory(
        StrategyId strategyId,
        IProcessStrategy strategy) : IProcessStrategyFactory
    {
        public ProcessStrategyDescriptor Descriptor { get; } = new(
            strategyId,
            "1.0.0",
            ProcessStrategyKind.StepExecution,
            new HashSet<CapabilityTag>());

        public ValueTask<IProcessStrategy> CreateAsync(
            ProcessStrategyBindingSnapshot binding,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(strategy);
        }
    }

    private sealed class RecordingStrategy(StrategyResultEnvelope result) : IProcessStrategy
    {
        public ProcessStrategyExecutionContext? Context { get; private set; }

        public ValueTask<StrategyResultEnvelope> ExecuteAsync(
            ProcessStrategyExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            Context = context;

            return ValueTask.FromResult(result);
        }
    }
}
