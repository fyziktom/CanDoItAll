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
            availableArtifacts: new HashSet<ArtifactSlotId> { ArtifactSlotId }) with
        {
            ConnectedInputArtifacts =
            [
                new ProcessRuntimeInputArtifactReceipt(
                    ActivityStepId,
                    ArtifactSlotId,
                    ProcessArtifactInputAvailability.Available,
                    StartStepId,
                    ArtifactInstanceId,
                    "sha256:artifact",
                    "sha256:start-to-activity-artifact")
            ]
        };

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
    public async Task Scheduler_keeps_pending_step_waiting_when_slot_exists_without_connected_input_receipt()
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

        Assert.True(result.Succeeded);
        Assert.Equal(
            ProcessRuntimeStepStatus.Pending,
            result.State.Steps.Single(step => step.StepInstanceId == ActivityStepId).Status);
        Assert.DoesNotContain(
            result.Events,
            runtimeEvent => runtimeEvent.EventType == ProcessRuntimeEventTypes.StepReady);
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
    public async Task Release_claim_closes_current_claim_and_returns_step_to_ready()
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

        var release = await engine.ReleaseClaimAsync(
            state,
            Context(Now.AddMinutes(1)),
            new ReleaseDispatchClaimCommand(ActivityStepId, OwnerId, token));
        var submit = await engine.SubmitStrategyResultAsync(
            release.State,
            Context(Now.AddMinutes(2)),
            new SubmitStrategyResultCommand(ActivityStepId, OwnerId, token, StrategyResultIdempotencyKey.New(), SucceededResult()));

        Assert.True(release.Succeeded);
        Assert.Equal(DispatchClaimStatus.Released, release.State.Claims.Single(claim => claim.ClaimToken == token).Status);
        var releasedStep = release.State.Steps.Single(step => step.StepInstanceId == ActivityStepId);
        Assert.Equal(ProcessRuntimeStepStatus.Ready, releasedStep.Status);
        Assert.Null(releasedStep.ActiveClaimToken);
        Assert.Equal(1, releasedStep.AttemptNumber);
        Assert.Contains(release.Events, runtimeEvent => runtimeEvent.EventType == ProcessRuntimeEventTypes.DispatchClaimReleased);
        Assert.False(submit.Succeeded);
        Assert.Contains(submit.Diagnostics, diagnostic => diagnostic.Code == "Runtime.LostLease");
        Assert.Single(unitOfWork.Requests);
    }

    [Fact]
    public async Task Defer_claim_closes_current_claim_and_pauses_step_without_requeueing()
    {
        var unitOfWork = new RecordingUnitOfWork();
        var engine = new ProcessRuntimeEngine(unitOfWork);
        var token = DispatchClaimToken.New();
        var childRunId = ProcessRunId.New();
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

        var defer = await engine.DeferClaimAsync(
            state,
            Context(Now.AddMinutes(1)),
            new DeferDispatchClaimCommand(ActivityStepId, OwnerId, token, childRunId));
        var submit = await engine.SubmitStrategyResultAsync(
            defer.State,
            Context(Now.AddMinutes(2)),
            new SubmitStrategyResultCommand(ActivityStepId, OwnerId, token, StrategyResultIdempotencyKey.New(), SucceededResult()));

        Assert.True(defer.Succeeded);
        Assert.Equal(DispatchClaimStatus.Released, defer.State.Claims.Single(claim => claim.ClaimToken == token).Status);
        var deferredStep = defer.State.Steps.Single(step => step.StepInstanceId == ActivityStepId);
        Assert.Equal(ProcessRuntimeStepStatus.Waiting, deferredStep.Status);
        Assert.Null(deferredStep.ActiveClaimToken);
        Assert.Equal(1, deferredStep.AttemptNumber);
        Assert.Contains(defer.Events, runtimeEvent => runtimeEvent.EventType == ProcessRuntimeEventTypes.StepWaiting);
        Assert.Contains(defer.Events, runtimeEvent => runtimeEvent.EventType == ProcessRuntimeEventTypes.DispatchClaimReleased);
        Assert.False(submit.Succeeded);
        Assert.Contains(submit.Diagnostics, diagnostic => diagnostic.Code == "Runtime.LostLease");
        Assert.Single(unitOfWork.Requests);
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
    public async Task Cancellation_with_open_claim_terminally_cancels_run_step_and_claim()
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
        Assert.Equal(ProcessRuntimeStatus.Cancelled, result.State.Status);
        Assert.Equal(ProcessRuntimeStepStatus.Cancelled, result.State.Steps.Single(step => step.StepInstanceId == ActivityStepId).Status);
        Assert.Null(result.State.Steps.Single(step => step.StepInstanceId == ActivityStepId).ActiveClaimToken);
        Assert.Equal(DispatchClaimStatus.Cancelled, result.State.Claims.Single().Status);
        Assert.Contains(result.Events, runtimeEvent => runtimeEvent.EventType == ProcessRuntimeEventTypes.ProcessRunCancelled);
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
    public async Task Non_completed_strategy_result_does_not_set_completed_result_key()
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
                NeedsManagerResult()));

        Assert.True(result.Succeeded);
        var step = result.State.Steps.Single(item => item.StepInstanceId == ActivityStepId);
        Assert.Equal(ProcessRuntimeStepStatus.Blocked, step.Status);
        Assert.Null(step.CompletedResultKey);
        Assert.Contains(
            result.State.AppliedResults,
            receipt => receipt.StepInstanceId == ActivityStepId &&
                       receipt.AppliedStepStatus == ProcessRuntimeStepStatus.Blocked);
    }

    [Fact]
    public async Task Blocked_strategy_result_preserves_safe_diagnostics_on_receipt()
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
                NeedsManagerResult()));

        var receipt = Assert.Single(result.State.AppliedResults);
        var diagnostic = Assert.Single(receipt.Diagnostics);
        Assert.Equal("process.runtime.test_needs_manager", diagnostic.Code);
        Assert.Equal("sha256:needs-manager-diagnostic", diagnostic.EvidenceHash);
        Assert.Equal("Unit test needs manager.", diagnostic.SafeSummary);
        Assert.Equal(ProcessDiagnosticRetrySafety.UnsafeToRetry, diagnostic.RetrySafety);
        Assert.Equal(ProcessDiagnosticIdempotencyClassification.Idempotent, diagnostic.Idempotency);
        Assert.NotNull(receipt.RecoveryDecision);
        Assert.Equal(ProcessRecoveryDecisionKind.ManagerRequired, receipt.RecoveryDecision.DecisionKind);
        Assert.Equal(ProcessFailureCategory.Unknown, receipt.RecoveryDecision.FailureCategory);
    }

    [Fact]
    public async Task Safe_idempotent_completion_gate_result_routes_to_current_step_retry()
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
                SafeCompletionGateNeedsManagerResult()));

        var receipt = Assert.Single(result.State.AppliedResults);
        var step = result.State.Steps.Single(item => item.StepInstanceId == ActivityStepId);
        Assert.Equal(ProcessRuntimeStatus.Active, result.State.Status);
        Assert.Equal(ProcessRuntimeStepStatus.Ready, step.Status);
        Assert.Equal(ProcessRuntimeStepStatus.Ready, receipt.AppliedStepStatus);
        Assert.NotNull(receipt.RecoveryDecision);
        Assert.Equal(ProcessFailureCategory.ProductCompletionGate, receipt.RecoveryDecision.FailureCategory);
        Assert.Equal(ProcessRecoveryDecisionKind.SafeRetry, receipt.RecoveryDecision.DecisionKind);
        Assert.Equal(ProcessRecoveryRouteKind.CurrentStepRetry, receipt.RecoveryDecision.RouteKind);
        Assert.Equal("process.current-step-safe-retry", receipt.RecoveryDecision.Policy);
        Assert.Equal(1, receipt.RecoveryDecision.AutomaticRetryAttempt);
        Assert.Equal(1, receipt.RecoveryDecision.SameDiagnosticFingerprintAttempt);
        Assert.Contains(result.Events, runtimeEvent => runtimeEvent.EventType == ProcessRuntimeEventTypes.StepReady);
        Assert.DoesNotContain(result.Events, runtimeEvent => runtimeEvent.EventType == ProcessRuntimeEventTypes.StepBlocked);
    }

    [Fact]
    public async Task Safe_idempotent_completion_gate_result_escalates_after_same_fingerprint_budget()
    {
        var unitOfWork = new RecordingUnitOfWork();
        var engine = new ProcessRuntimeEngine(unitOfWork);
        var firstToken = DispatchClaimToken.New();
        var state = NewState(
            ProcessRuntimeStatus.Active,
            NewStartStep(ProcessRuntimeStepStatus.Completed),
            NewActivityStep(ProcessRuntimeStepStatus.Running, activeClaimToken: firstToken),
            claims:
            [
                new DispatchClaimState(
                    firstToken,
                    ActivityStepId,
                    OwnerId,
                    DispatchClaimStatus.Claimed,
                    1,
                    Now,
                    Now.AddMinutes(5),
                    null,
                    null)
            ]);

        var first = await engine.SubmitStrategyResultAsync(
            state,
            Context(Now.AddMinutes(1)),
            new SubmitStrategyResultCommand(
                ActivityStepId,
                OwnerId,
                firstToken,
                StrategyResultIdempotencyKey.New(),
                SafeCompletionGateNeedsManagerResult()));

        var secondToken = DispatchClaimToken.New();
        var retryState = first.State with
        {
            Steps = first.State.Steps
                .Select(step => step.StepInstanceId == ActivityStepId
                    ? step with
                    {
                        Status = ProcessRuntimeStepStatus.Running,
                        AttemptNumber = 2,
                        ActiveClaimToken = secondToken
                    }
                    : step)
                .ToArray(),
            Claims =
            [
                .. first.State.Claims,
                new DispatchClaimState(
                    secondToken,
                    ActivityStepId,
                    OwnerId,
                    DispatchClaimStatus.Claimed,
                    2,
                    Now.AddMinutes(2),
                    Now.AddMinutes(7),
                    null,
                    null)
            ]
        };

        var second = await engine.SubmitStrategyResultAsync(
            retryState,
            Context(Now.AddMinutes(3)),
            new SubmitStrategyResultCommand(
                ActivityStepId,
                OwnerId,
                secondToken,
                StrategyResultIdempotencyKey.New(),
                SafeCompletionGateNeedsManagerResult()));

        var receipt = second.State.AppliedResults.Last();
        var step = second.State.Steps.Single(item => item.StepInstanceId == ActivityStepId);
        Assert.Equal(ProcessRuntimeStepStatus.Blocked, step.Status);
        Assert.Equal(ProcessRuntimeStepStatus.Blocked, receipt.AppliedStepStatus);
        Assert.NotNull(receipt.RecoveryDecision);
        Assert.Equal(ProcessRecoveryDecisionKind.ManagerRequired, receipt.RecoveryDecision.DecisionKind);
        Assert.Equal(ProcessRecoveryRouteKind.ManagerAction, receipt.RecoveryDecision.RouteKind);
        Assert.Equal("process.current-step-safe-retry-budget-exhausted", receipt.RecoveryDecision.Policy);
        Assert.Equal(2, receipt.RecoveryDecision.SameDiagnosticFingerprintAttempt);
        Assert.Contains("exhausted automatic current-step retry budget", receipt.RecoveryDecision.SafeReason, StringComparison.Ordinal);
        Assert.Contains(second.Events, runtimeEvent => runtimeEvent.EventType == ProcessRuntimeEventTypes.StepBlocked);
    }

    [Fact]
    public async Task Blocked_strategy_result_without_diagnostics_records_missing_diagnostic()
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
                NeedsManagerResultWithoutDiagnostics()));

        var receipt = Assert.Single(result.State.AppliedResults);
        var diagnostic = Assert.Single(receipt.Diagnostics);
        Assert.Equal("process.runtime.blocked_without_diagnostics", diagnostic.Code);
        Assert.Equal(ProcessDiagnosticRetrySafety.Unknown, diagnostic.RetrySafety);
        Assert.Contains("Step blocked without strategy diagnostics", diagnostic.SafeSummary, StringComparison.Ordinal);
        Assert.NotNull(receipt.RecoveryDecision);
        Assert.Equal(ProcessFailureCategory.MissingDiagnostics, receipt.RecoveryDecision.FailureCategory);
        Assert.Equal(ProcessRecoveryDecisionKind.ManagerRequired, receipt.RecoveryDecision.DecisionKind);
    }

    [Fact]
    public async Task Successful_result_missing_expected_output_blocks_with_finalization_diagnostic()
    {
        var unitOfWork = new RecordingUnitOfWork();
        var engine = new ProcessRuntimeEngine(unitOfWork);
        var token = DispatchClaimToken.New();
        var state = NewState(
            ProcessRuntimeStatus.Active,
            NewStartStep(ProcessRuntimeStepStatus.Completed),
            NewActivityStep(ProcessRuntimeStepStatus.Running, activeClaimToken: token) with
            {
                ProducedArtifactSlots = new HashSet<ArtifactSlotId> { ArtifactSlotId }
            },
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
                SucceededResult()));

        var receipt = Assert.Single(result.State.AppliedResults);
        Assert.True(result.Succeeded);
        Assert.Equal(ProcessRuntimeStepStatus.Blocked, result.State.Steps.Single(step => step.StepInstanceId == ActivityStepId).Status);
        Assert.Equal(StrategyOutcome.NeedsManager, receipt.Outcome);
        Assert.Equal(ProcessRuntimeStepStatus.Blocked, receipt.AppliedStepStatus);
        Assert.Contains(receipt.Diagnostics, diagnostic => diagnostic.Code == "process.runtime.missing_expected_output_artifact");
        Assert.NotNull(receipt.RecoveryDecision);
        Assert.Equal(ProcessFailureCategory.MissingArtifact, receipt.RecoveryDecision.FailureCategory);
        Assert.Equal(ProcessRecoveryRouteKind.ManagerAction, receipt.RecoveryDecision.RouteKind);
        Assert.Equal(ActivityStepId, receipt.RecoveryDecision.ResponsibleStepInstanceId);
    }

    [Fact]
    public async Task Missing_required_input_routes_recovery_to_upstream_producer()
    {
        var unitOfWork = new RecordingUnitOfWork();
        var engine = new ProcessRuntimeEngine(unitOfWork);
        var token = DispatchClaimToken.New();
        var state = NewState(
            ProcessRuntimeStatus.Active,
            NewStartStep(ProcessRuntimeStepStatus.Completed),
            NewActivityStep(
                ProcessRuntimeStepStatus.Running,
                requiredArtifacts: new HashSet<ArtifactSlotId> { ArtifactSlotId },
                activeClaimToken: token),
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
            ]) with
        {
            ConnectedInputArtifacts =
            [
                new ProcessRuntimeInputArtifactReceipt(
                    ActivityStepId,
                    ArtifactSlotId,
                    ProcessArtifactInputAvailability.Expected,
                    StartStepId,
                    ArtifactId: null,
                    ContentHash: string.Empty,
                    ConnectionHash: "sha256:start-to-activity-expected")
            ]
        };

        var result = await engine.SubmitStrategyResultAsync(
            state,
            Context(Now.AddMinutes(1)),
            new SubmitStrategyResultCommand(
                ActivityStepId,
                OwnerId,
                token,
                StrategyResultIdempotencyKey.New(),
                MissingInputNeedsManagerResult()));

        var receipt = Assert.Single(result.State.AppliedResults);
        Assert.True(result.Succeeded);
        Assert.NotNull(receipt.RecoveryDecision);
        Assert.Equal(ProcessFailureCategory.MissingArtifact, receipt.RecoveryDecision.FailureCategory);
        Assert.Equal(ProcessRecoveryRouteKind.UpstreamStepRework, receipt.RecoveryDecision.RouteKind);
        Assert.Equal(StartStepId, receipt.RecoveryDecision.ResponsibleStepInstanceId);
    }

    [Fact]
    public async Task Rework_request_reactivates_failed_run_and_requeues_failed_step()
    {
        var unitOfWork = new RecordingUnitOfWork();
        var engine = new ProcessRuntimeEngine(unitOfWork);
        var resultKey = StrategyResultIdempotencyKey.New();
        var state = NewState(
            ProcessRuntimeStatus.Failed,
            NewStartStep(ProcessRuntimeStepStatus.Completed),
            NewActivityStep(ProcessRuntimeStepStatus.Failed) with
            {
                AttemptNumber = 2,
                CompletedResultKey = resultKey
            },
            receipts:
            [
                new StrategyResultReceipt(
                    ActivityStepId,
                    StrategyId,
                    resultKey,
                    StrategyOutcome.Failed,
                    ProcessRuntimeStepStatus.Failed,
                    "sha256:failed-result")
            ]);

        var result = await engine.RequestStepReworkAsync(
            state,
            Context(Now.AddMinutes(2)),
            new RequestStepReworkCommand(ActivityStepId, "Unit test retry after failed child handoff."));

        Assert.True(result.Succeeded);
        Assert.Equal(ProcessRuntimeStatus.Active, result.State.Status);
        var step = result.State.Steps.Single(item => item.StepInstanceId == ActivityStepId);
        Assert.Equal(ProcessRuntimeStepStatus.Ready, step.Status);
        Assert.Equal(0, step.AttemptNumber);
        Assert.Null(step.ActiveClaimToken);
        Assert.Null(step.CompletedResultKey);
        Assert.Empty(result.State.AppliedResults);
        Assert.Contains(result.Events, runtimeEvent => runtimeEvent.EventType == ProcessRuntimeEventTypes.StepReworkRequested);
        Assert.Contains(result.Events, runtimeEvent => runtimeEvent.EventType == ProcessRuntimeEventTypes.ProcessRunReactivated);
        Assert.Contains(result.Events, runtimeEvent => runtimeEvent.EventType == ProcessRuntimeEventTypes.StepReady);
        Assert.Single(unitOfWork.Requests);
    }

    [Fact]
    public async Task Rework_request_requeues_waiting_step_after_child_process_completes()
    {
        var unitOfWork = new RecordingUnitOfWork();
        var engine = new ProcessRuntimeEngine(unitOfWork);
        var state = NewState(
            ProcessRuntimeStatus.Active,
            NewStartStep(ProcessRuntimeStepStatus.Completed),
            NewActivityStep(ProcessRuntimeStepStatus.Waiting) with
            {
                AttemptNumber = 3
            });

        var result = await engine.RequestStepReworkAsync(
            state,
            Context(Now.AddMinutes(2)),
            new RequestStepReworkCommand(ActivityStepId, "Child process completed."));

        Assert.True(result.Succeeded);
        Assert.Equal(ProcessRuntimeStatus.Active, result.State.Status);
        var step = result.State.Steps.Single(item => item.StepInstanceId == ActivityStepId);
        Assert.Equal(ProcessRuntimeStepStatus.Ready, step.Status);
        Assert.Equal(3, step.AttemptNumber);
        Assert.Null(step.ActiveClaimToken);
        Assert.Contains(result.Events, runtimeEvent => runtimeEvent.EventType == ProcessRuntimeEventTypes.StepReworkRequested);
        Assert.Contains(result.Events, runtimeEvent => runtimeEvent.EventType == ProcessRuntimeEventTypes.StepReady);
        Assert.DoesNotContain(result.Events, runtimeEvent => runtimeEvent.EventType == ProcessRuntimeEventTypes.ProcessRunReactivated);
        Assert.Single(unitOfWork.Requests);
    }

    [Fact]
    public async Task Rework_request_rejects_blocked_step_with_unresolved_dependency()
    {
        var engine = new ProcessRuntimeEngine(new RecordingUnitOfWork());
        var state = NewState(
            ProcessRuntimeStatus.Active,
            NewStartStep(ProcessRuntimeStepStatus.Running),
            NewActivityStep(
                ProcessRuntimeStepStatus.Blocked,
                dependencies: new HashSet<ProcessStepInstanceId> { StartStepId }));

        var result = await engine.RequestStepReworkAsync(
            state,
            Context(Now.AddMinutes(2)),
            new RequestStepReworkCommand(ActivityStepId, "Blocked downstream branch is not actionable yet."));

        Assert.Equal(ProcessRuntimeTransitionOutcome.Rejected, result.Outcome);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "Runtime.BlockedStepNotActionable");
        Assert.Equal(ProcessRuntimeStepStatus.Blocked, result.State.Steps.Single(step => step.StepInstanceId == ActivityStepId).Status);
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

    private static StrategyResultEnvelope NeedsManagerResult()
    {
        return new StrategyResultEnvelope(
            StrategyId,
            "1.0.0",
            Guid.NewGuid(),
            StrategyOutcome.NeedsManager,
            [],
            [],
            [
                new StrategyDiagnosticRef(
                    new StrategyDiagnosticCode("process.runtime.test_needs_manager"),
                    StrategyDiagnosticSensitivity.Normal,
                    "sha256:needs-manager-diagnostic",
                    "Unit test needs manager.",
                    RestrictedEvidenceReference: null,
                    ProcessDiagnosticRetrySafety.UnsafeToRetry,
                    ProcessDiagnosticIdempotencyClassification.Idempotent)
            ],
            [],
            "sha256:needs-manager-result");
    }

    private static StrategyResultEnvelope SafeCompletionGateNeedsManagerResult()
    {
        return new StrategyResultEnvelope(
            StrategyId,
            "1.0.0",
            Guid.NewGuid(),
            StrategyOutcome.NeedsManager,
            [],
            [],
            [
                new StrategyDiagnosticRef(
                    new StrategyDiagnosticCode("process.adapter.product_required_tool_receipt_missing"),
                    StrategyDiagnosticSensitivity.Normal,
                    "sha256:missing-workspace-pwsh-run-script",
                    "Required current-run product tool receipt is missing: workspace_pwsh_run_script.",
                    RestrictedEvidenceReference: null,
                    ProcessDiagnosticRetrySafety.SafeToRetry,
                    ProcessDiagnosticIdempotencyClassification.Idempotent)
            ],
            [],
            "sha256:completion-gate-safe-retry");
    }

    private static StrategyResultEnvelope NeedsManagerResultWithoutDiagnostics()
    {
        return new StrategyResultEnvelope(
            StrategyId,
            "1.0.0",
            Guid.NewGuid(),
            StrategyOutcome.NeedsManager,
            [],
            [],
            [],
            [],
            "sha256:needs-manager-result");
    }

    private static StrategyResultEnvelope MissingInputNeedsManagerResult()
    {
        return new StrategyResultEnvelope(
            StrategyId,
            "1.0.0",
            Guid.NewGuid(),
            StrategyOutcome.NeedsManager,
            [],
            [new RequestedArtifactRef(ArtifactSlotId, "sha256:requested-input")],
            [
                new StrategyDiagnosticRef(
                    new StrategyDiagnosticCode("process.runtime.required_artifact_missing"),
                    StrategyDiagnosticSensitivity.Normal,
                    "sha256:missing-input",
                    "Required input artifact is missing.",
                    RestrictedEvidenceReference: null,
                    ProcessDiagnosticRetrySafety.UnsafeToRetry,
                    ProcessDiagnosticIdempotencyClassification.Idempotent)
            ],
            [
                new ManagerSignal(
                    new ManagerSignalCode("process.runtime.required_artifact_missing"),
                    "sha256:missing-input",
                    "Required input artifact is missing.")
            ],
            "sha256:missing-input-result");
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
