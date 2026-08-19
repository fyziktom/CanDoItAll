using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Workbench;
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

namespace CanDoItAll.Tests.Unit.Processes;

public sealed class ProcessRuntimeOperatorApplicationServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Request_rework_appends_operator_reason_to_assignment_prompt_and_enqueues_dispatch()
    {
        var runId = ProcessRunId.New();
        var stepId = ProcessStepInstanceId.New();
        var planId = ProcessInstancePlanId.New();
        var assignmentStore = new RecordingAssignmentStore(CreateAssignment(runId, planId, stepId));
        var dispatchQueue = new RecordingDispatchQueue();
        await using var dbContext = CreateDbContext();
        var projectionStore = new EfProcessProjectionStore(dbContext);
        var clock = new FixedProcessProjectionClock(Now);
        var stateStore = new InMemoryRuntimeStateStore(CreateFailedState(runId, planId, stepId));
        var service = new ProcessRuntimeOperatorApplicationService(
            clock,
            stateStore,
            stateStore,
            assignmentStore,
            new RecordingUnitOfWork(stateStore),
            dispatchQueue,
            new ProcessRuntimeProjectionCatchupService(
                new EmptyRuntimeEventReplayStore(),
                projectionStore,
                new ProcessRuntimeProjectionProjector(
                    projectionStore,
                    ProcessProjectionJsonCodec.Default,
                    clock,
                    new EfProcessRunRecordStore(dbContext)),
                clock),
            []);

        var result = await service.ExecuteAsync(new ProcessRuntimeOperatorActionCommand(
            runId,
            stepId,
            ProcessRuntimeOperatorActionKind.RequestRework,
            "unit-test",
            "Add the standard CSS rule that hides #blazor-error-ui before browser validation."));

        Assert.True(result.Succeeded);
        var saved = Assert.Single(assignmentStore.SavedAssignments);
        Assert.Contains("Operator rework instruction:", saved.Prompt, StringComparison.Ordinal);
        Assert.Contains("hides #blazor-error-ui", saved.Prompt, StringComparison.Ordinal);
        var queued = Assert.Single(dispatchQueue.Requests);
        Assert.Equal(runId, queued.RunId);
    }

    [Fact]
    public async Task Request_rework_appends_diagnostic_specific_packet_from_runtime_receipt()
    {
        var runId = ProcessRunId.New();
        var stepId = ProcessStepInstanceId.New();
        var planId = ProcessInstancePlanId.New();
        var assignmentStore = new RecordingAssignmentStore(CreateAssignment(
            runId,
            planId,
            stepId,
            "create-dotnet-project",
            CreateDotNetCreateProjectLaunchVariables(runId)));
        var dispatchQueue = new RecordingDispatchQueue();
        await using var dbContext = CreateDbContext();
        var projectionStore = new EfProcessProjectionStore(dbContext);
        var clock = new FixedProcessProjectionClock(Now);
        var stateStore = new InMemoryRuntimeStateStore(CreateFailedStateWithIncidentReceipt(runId, planId, stepId));
        var service = new ProcessRuntimeOperatorApplicationService(
            clock,
            stateStore,
            stateStore,
            assignmentStore,
            new RecordingUnitOfWork(stateStore),
            dispatchQueue,
            new ProcessRuntimeProjectionCatchupService(
                new EmptyRuntimeEventReplayStore(),
                projectionStore,
                new ProcessRuntimeProjectionProjector(
                    projectionStore,
                    ProcessProjectionJsonCodec.Default,
                    clock,
                    new EfProcessRunRecordStore(dbContext)),
                clock),
            [],
            recoveryInstructionBuilder: CreateRecoveryInstructionBuilder());

        var result = await service.ExecuteAsync(new ProcessRuntimeOperatorActionCommand(
            runId,
            stepId,
            ProcessRuntimeOperatorActionKind.RequestRework,
            "unit-test",
            "Retry after fixing the missing solution wiring receipt."));

        Assert.True(result.Succeeded);
        var prompt = Assert.Single(assignmentStore.SavedAssignments).Prompt;
        Assert.Contains("Operator rework instruction:", prompt, StringComparison.Ordinal);
        Assert.Contains("Diagnostic recovery packet:", prompt, StringComparison.Ordinal);
        Assert.Contains("workspace_pwsh_run_script", prompt, StringComparison.Ordinal);
        Assert.Contains("Generic receipt recovery", prompt, StringComparison.Ordinal);
        Assert.Contains("missing current-run receipt contract", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("{CurrentProcessRunId}", prompt, StringComparison.Ordinal);
        Assert.Single(dispatchQueue.Requests);
    }

    [Fact]
    public async Task Request_rework_removes_prior_recovery_prompt_blocks_before_appending_fresh_instruction()
    {
        var runId = ProcessRunId.New();
        var stepId = ProcessStepInstanceId.New();
        var planId = ProcessInstancePlanId.New();
        var stalePrompt = """
            Repair the generated app and return structured process output.

            Runtime manager recovery instruction:
            The previous result blocked the step and must be reviewed by the process manager before any rework is dispatched.
            Diagnostics:
            - process.adapter.runtime_tool_preflight_failed: browser tools were unavailable in an old attempt.

            Operator rework instruction:
            Old operator note that is no longer the current repair instruction.

            Runtime manager recovery instruction:
            Diagnostics:
            - process.adapter.product_required_tool_receipt_blocked_retry: Original reason: The runtime preflight explicitly reported browser tools unavailable.
            Requested artifacts:
            - old-slot: sha256:old
            """;
        var assignmentStore = new RecordingAssignmentStore(CreateAssignment(runId, planId, stepId) with
        {
            Prompt = stalePrompt
        });
        var dispatchQueue = new RecordingDispatchQueue();
        await using var dbContext = CreateDbContext();
        var projectionStore = new EfProcessProjectionStore(dbContext);
        var clock = new FixedProcessProjectionClock(Now);
        var stateStore = new InMemoryRuntimeStateStore(CreateFailedState(runId, planId, stepId));
        var service = new ProcessRuntimeOperatorApplicationService(
            clock,
            stateStore,
            stateStore,
            assignmentStore,
            new RecordingUnitOfWork(stateStore),
            dispatchQueue,
            new ProcessRuntimeProjectionCatchupService(
                new EmptyRuntimeEventReplayStore(),
                projectionStore,
                new ProcessRuntimeProjectionProjector(
                    projectionStore,
                    ProcessProjectionJsonCodec.Default,
                    clock,
                    new EfProcessRunRecordStore(dbContext)),
                clock),
            []);

        var result = await service.ExecuteAsync(new ProcessRuntimeOperatorActionCommand(
            runId,
            stepId,
            ProcessRuntimeOperatorActionKind.RequestRework,
            "unit-test",
            "Runtime preflight is now satisfied; invoke the current browser and validation tools."));

        Assert.True(result.Succeeded);
        var saved = Assert.Single(assignmentStore.SavedAssignments);
        Assert.StartsWith("Repair the generated app", saved.Prompt, StringComparison.Ordinal);
        Assert.Contains("Operator rework instruction:", saved.Prompt, StringComparison.Ordinal);
        Assert.Contains("Runtime preflight is now satisfied", saved.Prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("Runtime manager recovery instruction:", saved.Prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("Old operator note", saved.Prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("browser tools were unavailable in an old attempt", saved.Prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("old-slot", saved.Prompt, StringComparison.Ordinal);
        Assert.Single(dispatchQueue.Requests);
    }

    [Fact]
    public async Task Request_rework_expires_stale_active_claim_and_enqueues_dispatch()
    {
        var runId = ProcessRunId.New();
        var stepId = ProcessStepInstanceId.New();
        var planId = ProcessInstancePlanId.New();
        var claimToken = DispatchClaimToken.New();
        var assignmentStore = new RecordingAssignmentStore(CreateAssignment(runId, planId, stepId));
        var dispatchQueue = new RecordingDispatchQueue();
        await using var dbContext = CreateDbContext();
        var projectionStore = new EfProcessProjectionStore(dbContext);
        var clock = new FixedProcessProjectionClock(Now);
        var stateStore = new InMemoryRuntimeStateStore(CreateExpiredRunningState(runId, planId, stepId, claimToken));
        var unitOfWork = new RecordingUnitOfWork(stateStore);
        var service = new ProcessRuntimeOperatorApplicationService(
            clock,
            stateStore,
            stateStore,
            assignmentStore,
            unitOfWork,
            dispatchQueue,
            new ProcessRuntimeProjectionCatchupService(
                new EmptyRuntimeEventReplayStore(),
                projectionStore,
                new ProcessRuntimeProjectionProjector(
                    projectionStore,
                    ProcessProjectionJsonCodec.Default,
                    clock,
                    new EfProcessRunRecordStore(dbContext)),
                clock),
            []);

        var result = await service.ExecuteAsync(new ProcessRuntimeOperatorActionCommand(
            runId,
            stepId,
            ProcessRuntimeOperatorActionKind.RequestRework,
            "unit-test",
            "Retry the expired architecture step and preserve any managed artifacts already written."));

        Assert.True(result.Succeeded);
        Assert.Equal(ProcessRuntimeStepStatus.Ready, stateStore.State.Steps.Single().Status);
        Assert.Null(stateStore.State.Steps.Single().ActiveClaimToken);
        Assert.Equal(DispatchClaimStatus.Expired, stateStore.State.Claims.Single().Status);
        Assert.Contains(
            unitOfWork.Requests.SelectMany(request => request.Mutation.Events),
            runtimeEvent => runtimeEvent.EventType == ProcessRuntimeEventTypes.DispatchClaimExpired);
        Assert.Single(dispatchQueue.Requests);
        var saved = Assert.Single(assignmentStore.SavedAssignments);
        Assert.Contains("expired architecture step", saved.Prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Authorized_upstream_rework_of_completed_producer_enqueues_dispatch()
    {
        var runId = ProcessRunId.New();
        var planId = ProcessInstancePlanId.New();
        var producerStepId = ProcessStepInstanceId.New();
        var blockedStepId = ProcessStepInstanceId.New();
        var sourceResultKey = StrategyResultIdempotencyKey.New();
        var state = CreateBlockedUpstreamRecoveryState(
            runId,
            planId,
            producerStepId,
            blockedStepId,
            sourceResultKey);
        var assignmentStore = new RecordingAssignmentStore(
            CreateAssignment(runId, planId, producerStepId, "produce-application-skeleton"));
        var dispatchQueue = new RecordingDispatchQueue();
        var stateStore = new InMemoryRuntimeStateStore(state);
        var unitOfWork = new RecordingUnitOfWork(stateStore);
        await using var dbContext = CreateDbContext();
        var projectionStore = new EfProcessProjectionStore(dbContext);
        var clock = new FixedProcessProjectionClock(Now);
        var service = new ProcessRuntimeOperatorApplicationService(
            clock,
            stateStore,
            stateStore,
            assignmentStore,
            unitOfWork,
            dispatchQueue,
            new ProcessRuntimeProjectionCatchupService(
                new EmptyRuntimeEventReplayStore(),
                projectionStore,
                new ProcessRuntimeProjectionProjector(
                    projectionStore,
                    ProcessProjectionJsonCodec.Default,
                    clock,
                    new EfProcessRunRecordStore(dbContext)),
                clock),
            []);

        var executor = new ProcessBlockedRunRecoveryCommandExecutor(service);
        var result = await executor.ExecuteAsync(new ProcessBlockedRunRecoveryCommand(
            runId,
            blockedStepId,
            producerStepId,
            ProcessBlockedRunRecoveryActionKind.UpstreamStepRework,
            ProcessBlockedRunRecoveryPolicy.MissingInputProducerRework,
            sourceResultKey,
            "sha256:missing-input-fingerprint",
            ProcessRecoveryRouteKind.UpstreamStepRework,
            producerStepId,
            ProcessRuntimeBlockedRecoveryPhase.UpstreamProducer,
            state.UpdatedAtUtc),
            "blocked-run-recovery",
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(ProcessRuntimeStatus.Active, result.Status);
        Assert.Equal(
            ProcessRuntimeStepStatus.Ready,
            stateStore.State.Steps.Single(step => step.StepInstanceId == producerStepId).Status);
        Assert.Equal(2, stateStore.State.AppliedResults.Count);
        Assert.Single(unitOfWork.Requests);
        Assert.Single(assignmentStore.SavedAssignments);
        Assert.Single(dispatchQueue.Requests);
    }

    [Theory]
    [InlineData(BlockedRecoveryAuthorizationMismatch.StaleState)]
    [InlineData(BlockedRecoveryAuthorizationMismatch.ResultKey)]
    [InlineData(BlockedRecoveryAuthorizationMismatch.ResponsibleTarget)]
    [InlineData(BlockedRecoveryAuthorizationMismatch.UnrelatedTarget)]
    public async Task Blocked_recovery_precondition_mismatch_rejects_without_mutation_or_dispatch(
        BlockedRecoveryAuthorizationMismatch mismatch)
    {
        var runId = ProcessRunId.New();
        var planId = ProcessInstancePlanId.New();
        var producerStepId = ProcessStepInstanceId.New();
        var blockedStepId = ProcessStepInstanceId.New();
        var sourceResultKey = StrategyResultIdempotencyKey.New();
        var state = CreateBlockedUpstreamRecoveryState(
            runId,
            planId,
            producerStepId,
            blockedStepId,
            sourceResultKey);
        var authorization = CreateBlockedRecoveryAuthorization(
            state,
            blockedStepId,
            sourceResultKey,
            producerStepId);
        authorization = mismatch switch
        {
            BlockedRecoveryAuthorizationMismatch.StaleState => authorization with
            {
                ExpectedStateUpdatedAtUtc = authorization.ExpectedStateUpdatedAtUtc.AddMilliseconds(-1)
            },
            BlockedRecoveryAuthorizationMismatch.ResultKey => authorization with
            {
                SourceResultIdempotencyKey = StrategyResultIdempotencyKey.New()
            },
            BlockedRecoveryAuthorizationMismatch.ResponsibleTarget => authorization with
            {
                ResponsibleTargetStepInstanceId = ProcessStepInstanceId.New()
            },
            BlockedRecoveryAuthorizationMismatch.UnrelatedTarget => authorization,
            _ => throw new ArgumentOutOfRangeException(nameof(mismatch), mismatch, null)
        };
        var commandTargetStepId = mismatch == BlockedRecoveryAuthorizationMismatch.UnrelatedTarget
            ? ProcessStepInstanceId.New()
            : producerStepId;
        var assignmentStore = new RecordingAssignmentStore(
            CreateAssignment(runId, planId, producerStepId, "produce-application-skeleton"));
        var dispatchQueue = new RecordingDispatchQueue();
        var stateStore = new InMemoryRuntimeStateStore(state);
        var unitOfWork = new RecordingUnitOfWork(stateStore);
        await using var dbContext = CreateDbContext();
        var projectionStore = new EfProcessProjectionStore(dbContext);
        var clock = new FixedProcessProjectionClock(Now);
        var service = new ProcessRuntimeOperatorApplicationService(
            clock,
            stateStore,
            stateStore,
            assignmentStore,
            unitOfWork,
            dispatchQueue,
            new ProcessRuntimeProjectionCatchupService(
                new EmptyRuntimeEventReplayStore(),
                projectionStore,
                new ProcessRuntimeProjectionProjector(
                    projectionStore,
                    ProcessProjectionJsonCodec.Default,
                    clock,
                    new EfProcessRunRecordStore(dbContext)),
                clock),
            []);

        var result = await service.ExecuteAsync(new ProcessRuntimeOperatorActionCommand(
            runId,
            commandTargetStepId,
            ProcessRuntimeOperatorActionKind.RequestRework,
            "blocked-run-recovery",
            "This command must not mutate stale blocked state.")
        {
            BlockedRecoveryAuthorization = authorization
        });

        Assert.Equal(ProcessRuntimeTransitionOutcome.Rejected, result.Outcome);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Contains("Runtime.BlockedRecoveryAuthorizationRejected", StringComparison.Ordinal));
        Assert.Equal(
            ProcessRuntimeStepStatus.Completed,
            stateStore.State.Steps.Single(step => step.StepInstanceId == producerStepId).Status);
        Assert.Empty(unitOfWork.Requests);
        Assert.Empty(assignmentStore.SavedAssignments);
        Assert.Empty(dispatchQueue.Requests);
    }

    [Fact]
    public async Task Authorized_restored_input_rework_targets_blocked_consumer_not_responsible_producer()
    {
        var runId = ProcessRunId.New();
        var planId = ProcessInstancePlanId.New();
        var producerStepId = ProcessStepInstanceId.New();
        var blockedStepId = ProcessStepInstanceId.New();
        var sourceResultKey = StrategyResultIdempotencyKey.New();
        var state = CreateBlockedUpstreamRecoveryState(
            runId,
            planId,
            producerStepId,
            blockedStepId,
            sourceResultKey,
            inputRestored: true);
        var assignmentStore = new RecordingAssignmentStore(
            CreateAssignment(runId, planId, blockedStepId, "consume-application-skeleton"));
        var dispatchQueue = new RecordingDispatchQueue();
        var stateStore = new InMemoryRuntimeStateStore(state);
        await using var dbContext = CreateDbContext();
        var projectionStore = new EfProcessProjectionStore(dbContext);
        var clock = new FixedProcessProjectionClock(Now);
        var service = new ProcessRuntimeOperatorApplicationService(
            clock,
            stateStore,
            stateStore,
            assignmentStore,
            new RecordingUnitOfWork(stateStore),
            dispatchQueue,
            new ProcessRuntimeProjectionCatchupService(
                new EmptyRuntimeEventReplayStore(),
                projectionStore,
                new ProcessRuntimeProjectionProjector(
                    projectionStore,
                    ProcessProjectionJsonCodec.Default,
                    clock,
                    new EfProcessRunRecordStore(dbContext)),
                clock),
            []);

        var result = await service.ExecuteAsync(new ProcessRuntimeOperatorActionCommand(
            runId,
            blockedStepId,
            ProcessRuntimeOperatorActionKind.RequestRework,
            "blocked-run-recovery",
            "The upstream input artifact is now restored.")
        {
            BlockedRecoveryAuthorization = CreateBlockedRecoveryAuthorization(
                state,
                blockedStepId,
                sourceResultKey,
                producerStepId,
                ProcessRuntimeBlockedRecoveryPhase.RestoredConsumer)
        });

        Assert.True(result.Succeeded);
        Assert.Equal(
            ProcessRuntimeStepStatus.Ready,
            stateStore.State.Steps.Single(step => step.StepInstanceId == blockedStepId).Status);
        Assert.Equal(
            ProcessRuntimeStepStatus.Completed,
            stateStore.State.Steps.Single(step => step.StepInstanceId == producerStepId).Status);
        Assert.Single(dispatchQueue.Requests);
    }

    [Fact]
    public async Task Coordinator_recovers_producer_then_restored_consumer_and_rejects_replay()
    {
        var runId = ProcessRunId.New();
        var planId = ProcessInstancePlanId.New();
        var producerStepId = ProcessStepInstanceId.New();
        var blockedStepId = ProcessStepInstanceId.New();
        var sourceResultKey = StrategyResultIdempotencyKey.New();
        var initialState = CreateBlockedUpstreamRecoveryState(
            runId,
            planId,
            producerStepId,
            blockedStepId,
            sourceResultKey);
        var assignmentStore = new RecordingAssignmentStore(
            CreateArtifactRecoveryAssignment(
                runId,
                planId,
                producerStepId,
                "produce-application-skeleton"),
            CreateArtifactRecoveryAssignment(
                runId,
                planId,
                blockedStepId,
                "consume-application-skeleton"));
        var dispatchQueue = new RecordingDispatchQueue();
        var stateStore = new InMemoryRuntimeStateStore(initialState);
        var unitOfWork = new RecordingUnitOfWork(stateStore);
        await using var dbContext = CreateDbContext();
        var projectionStore = new EfProcessProjectionStore(dbContext);
        var clock = new FixedProcessProjectionClock(Now);
        var operatorService = new ProcessRuntimeOperatorApplicationService(
            clock,
            stateStore,
            stateStore,
            assignmentStore,
            unitOfWork,
            dispatchQueue,
            new ProcessRuntimeProjectionCatchupService(
                new EmptyRuntimeEventReplayStore(),
                projectionStore,
                new ProcessRuntimeProjectionProjector(
                    projectionStore,
                    ProcessProjectionJsonCodec.Default,
                    clock,
                    new EfProcessRunRecordStore(dbContext)),
                clock),
            []);
        var coordinator = new ProcessBlockedRunRecoveryCoordinator(
            stateStore,
            new StubPlanStore(CreateSimpleAppPlan(planId)),
            assignmentStore,
            new ProcessBlockedRunRecoveryCommandExecutor(operatorService),
            new ProcessBlockedRunRecoveryPolicyCatalog());

        var producerRecovery = await coordinator.TryRecoverAsync(runId, "blocked-run-recovery");

        Assert.Equal(ProcessBlockedRunRecoveryOutcome.Recovered, producerRecovery.Outcome);
        Assert.Equal(
            ProcessBlockedRunRecoveryActionKind.UpstreamStepRework,
            producerRecovery.ActionKind);
        var producerAction = Assert.Single(stateStore.State.BlockedRecoveryActions);
        Assert.Equal(ProcessRuntimeBlockedRecoveryPhase.UpstreamProducer, producerAction.Phase);
        Assert.Equal(sourceResultKey, producerAction.SourceResultIdempotencyKey);
        Assert.Equal(producerStepId, producerAction.TargetStepInstanceId);

        var expectedInput = Assert.Single(stateStore.State.ConnectedInputArtifacts);
        var producerResultKey = StrategyResultIdempotencyKey.New();
        stateStore.State = stateStore.State with
        {
            Status = ProcessRuntimeStatus.Blocked,
            Steps = stateStore.State.Steps
                .Select(step => step.StepInstanceId == producerStepId
                    ? step with
                    {
                        Status = ProcessRuntimeStepStatus.Completed,
                        AttemptNumber = 1,
                        CompletedResultKey = producerResultKey
                    }
                    : step with
                    {
                        Status = ProcessRuntimeStepStatus.Blocked,
                        ActiveClaimToken = null,
                        CompletedResultKey = null
                    })
                .ToArray(),
            AvailableArtifactSlots = new HashSet<ArtifactSlotId> { expectedInput.RequiredSlotId },
            ConnectedInputArtifacts =
            [
                expectedInput with
                {
                    Availability = ProcessArtifactInputAvailability.Available,
                    ArtifactId = ArtifactInstanceId.New(),
                    ContentHash = "sha256:restored-input"
                }
            ],
            UpdatedAtUtc = Now
        };

        var consumerRecovery = await coordinator.TryRecoverAsync(runId, "blocked-run-recovery");

        Assert.Equal(ProcessBlockedRunRecoveryOutcome.Recovered, consumerRecovery.Outcome);
        Assert.Equal(
            ProcessBlockedRunRecoveryActionKind.CurrentStepRework,
            consumerRecovery.ActionKind);
        Assert.Collection(
            stateStore.State.BlockedRecoveryActions,
            action => Assert.Equal(ProcessRuntimeBlockedRecoveryPhase.UpstreamProducer, action.Phase),
            action => Assert.Equal(ProcessRuntimeBlockedRecoveryPhase.RestoredConsumer, action.Phase));
        Assert.All(
            stateStore.State.BlockedRecoveryActions,
            action => Assert.Equal(sourceResultKey, action.SourceResultIdempotencyKey));

        stateStore.State = stateStore.State with
        {
            Status = ProcessRuntimeStatus.Blocked,
            Steps = stateStore.State.Steps
                .Select(step => step.StepInstanceId == blockedStepId
                    ? step with
                    {
                        Status = ProcessRuntimeStepStatus.Blocked,
                        ActiveClaimToken = null,
                        CompletedResultKey = null
                    }
                    : step)
                .ToArray(),
            UpdatedAtUtc = Now
        };

        var replay = await coordinator.TryRecoverAsync(runId, "blocked-run-recovery");

        Assert.Equal(ProcessBlockedRunRecoveryOutcome.RequiresAttention, replay.Outcome);
        Assert.Contains(
            replay.Diagnostics,
            diagnostic => diagnostic.Contains("budget", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(2, stateStore.State.BlockedRecoveryActions.Count);
        Assert.Equal(2, unitOfWork.Requests.Count);
        Assert.Equal(2, dispatchQueue.Requests.Count);
    }

    [Fact]
    public async Task Authorized_manager_action_with_null_responsible_target_reworks_source_step()
    {
        var runId = ProcessRunId.New();
        var planId = ProcessInstancePlanId.New();
        var blockedStepId = ProcessStepInstanceId.New();
        var sourceResultKey = StrategyResultIdempotencyKey.New();
        var state = CreateBlockedManagerActionState(runId, planId, blockedStepId, sourceResultKey);
        var assignmentStore = new RecordingAssignmentStore(
            CreateAssignment(runId, planId, blockedStepId, "produce-application-output"));
        var dispatchQueue = new RecordingDispatchQueue();
        var stateStore = new InMemoryRuntimeStateStore(state);
        await using var dbContext = CreateDbContext();
        var projectionStore = new EfProcessProjectionStore(dbContext);
        var clock = new FixedProcessProjectionClock(Now);
        var service = new ProcessRuntimeOperatorApplicationService(
            clock,
            stateStore,
            stateStore,
            assignmentStore,
            new RecordingUnitOfWork(stateStore),
            dispatchQueue,
            new ProcessRuntimeProjectionCatchupService(
                new EmptyRuntimeEventReplayStore(),
                projectionStore,
                new ProcessRuntimeProjectionProjector(
                    projectionStore,
                    ProcessProjectionJsonCodec.Default,
                    clock,
                    new EfProcessRunRecordStore(dbContext)),
                clock),
            []);
        var executor = new ProcessBlockedRunRecoveryCommandExecutor(service);

        var result = await executor.ExecuteAsync(
            new ProcessBlockedRunRecoveryCommand(
                runId,
                blockedStepId,
                blockedStepId,
                ProcessBlockedRunRecoveryActionKind.CurrentStepRework,
                ProcessBlockedRunRecoveryPolicy.MissingOutputRework,
                sourceResultKey,
                "sha256:missing-output-fingerprint",
                ProcessRecoveryRouteKind.ManagerAction,
                ResponsibleStepInstanceId: null,
                ProcessRuntimeBlockedRecoveryPhase.CurrentStep,
                ExpectedStateUpdatedAtUtc: state.UpdatedAtUtc),
            "blocked-run-recovery");

        Assert.True(result.Succeeded);
        Assert.Equal(ProcessRuntimeStatus.Active, result.Status);
        Assert.Equal(ProcessRuntimeStepStatus.Ready, stateStore.State.Steps.Single().Status);
        Assert.Single(dispatchQueue.Requests);
    }

    [Fact]
    public async Task Request_cancellation_cancels_run_without_assignment_edit_or_dispatch()
    {
        var runId = ProcessRunId.New();
        var stepId = ProcessStepInstanceId.New();
        var planId = ProcessInstancePlanId.New();
        var assignmentStore = new RecordingAssignmentStore(CreateAssignment(runId, planId, stepId));
        var dispatchQueue = new RecordingDispatchQueue();
        await using var dbContext = CreateDbContext();
        var projectionStore = new EfProcessProjectionStore(dbContext);
        var clock = new FixedProcessProjectionClock(Now);
        var stateStore = new InMemoryRuntimeStateStore(CreateActiveReadyState(runId, planId, stepId));
        var service = new ProcessRuntimeOperatorApplicationService(
            clock,
            stateStore,
            stateStore,
            assignmentStore,
            new RecordingUnitOfWork(stateStore),
            dispatchQueue,
            new ProcessRuntimeProjectionCatchupService(
                new EmptyRuntimeEventReplayStore(),
                projectionStore,
                new ProcessRuntimeProjectionProjector(
                    projectionStore,
                    ProcessProjectionJsonCodec.Default,
                    clock,
                    new EfProcessRunRecordStore(dbContext)),
                clock),
            []);

        var result = await service.RequestCancellationAsync(new ProcessRuntimeRunCancellationCommand(
            runId,
            "unit-test",
            "Stop stale test run before starting a fresh E2E pass."));

        Assert.True(result.Succeeded);
        Assert.Equal(ProcessRuntimeOperatorActionKind.CancelRun, result.Kind);
        Assert.Equal(ProcessRuntimeStatus.Cancelled, result.Status);
        Assert.Equal(ProcessRuntimeStatus.Cancelled, stateStore.State.Status);
        Assert.Equal(ProcessRuntimeStepStatus.Cancelled, stateStore.State.Steps.Single().Status);
        Assert.Empty(dispatchQueue.Requests);
        Assert.Empty(assignmentStore.SavedAssignments);
    }

    [Fact]
    public async Task Request_cancellation_with_open_claim_does_not_enqueue_more_work()
    {
        var runId = ProcessRunId.New();
        var stepId = ProcessStepInstanceId.New();
        var planId = ProcessInstancePlanId.New();
        var claimToken = DispatchClaimToken.New();
        var assignmentStore = new RecordingAssignmentStore(CreateAssignment(runId, planId, stepId));
        var dispatchQueue = new RecordingDispatchQueue();
        await using var dbContext = CreateDbContext();
        var projectionStore = new EfProcessProjectionStore(dbContext);
        var clock = new FixedProcessProjectionClock(Now);
        var stateStore = new InMemoryRuntimeStateStore(CreateExpiredRunningState(runId, planId, stepId, claimToken));
        var service = new ProcessRuntimeOperatorApplicationService(
            clock,
            stateStore,
            stateStore,
            assignmentStore,
            new RecordingUnitOfWork(stateStore),
            dispatchQueue,
            new ProcessRuntimeProjectionCatchupService(
                new EmptyRuntimeEventReplayStore(),
                projectionStore,
                new ProcessRuntimeProjectionProjector(
                    projectionStore,
                    ProcessProjectionJsonCodec.Default,
                    clock,
                    new EfProcessRunRecordStore(dbContext)),
                clock),
            []);

        var result = await service.RequestCancellationAsync(new ProcessRuntimeRunCancellationCommand(
            runId,
            "unit-test",
            "Stop stale claimed test run before starting a fresh E2E pass."));

        Assert.True(result.Succeeded);
        Assert.Equal(ProcessRuntimeStatus.Cancelled, result.Status);
        Assert.Equal(ProcessRuntimeStatus.Cancelled, stateStore.State.Status);
        Assert.Equal(ProcessRuntimeStepStatus.Cancelled, stateStore.State.Steps.Single().Status);
        Assert.Equal(DispatchClaimStatus.Cancelled, stateStore.State.Claims.Single().Status);
        Assert.Empty(dispatchQueue.Requests);
        Assert.Empty(assignmentStore.SavedAssignments);
    }

    [Fact]
    public async Task Request_cancellation_on_root_cascades_to_cancellable_descendants()
    {
        var rootRunId = ProcessRunId.New();
        var childRunId = ProcessRunId.New();
        var rootStepId = ProcessStepInstanceId.New();
        var childStepId = ProcessStepInstanceId.New();
        var planId = ProcessInstancePlanId.New();
        var assignmentStore = new RecordingAssignmentStore(CreateAssignment(rootRunId, planId, rootStepId));
        var dispatchQueue = new RecordingDispatchQueue();
        await using var dbContext = CreateDbContext();
        var projectionStore = new EfProcessProjectionStore(dbContext);
        var clock = new FixedProcessProjectionClock(Now);
        var stateStore = new InMemoryRuntimeStateStore(
            CreateActiveReadyState(rootRunId, planId, rootStepId),
            CreateActiveReadyState(childRunId, planId, childStepId, rootRunId));
        var service = new ProcessRuntimeOperatorApplicationService(
            clock,
            stateStore,
            stateStore,
            assignmentStore,
            new RecordingUnitOfWork(stateStore),
            dispatchQueue,
            new ProcessRuntimeProjectionCatchupService(
                new EmptyRuntimeEventReplayStore(),
                projectionStore,
                new ProcessRuntimeProjectionProjector(
                    projectionStore,
                    ProcessProjectionJsonCodec.Default,
                    clock,
                    new EfProcessRunRecordStore(dbContext)),
                clock),
            []);

        var result = await service.RequestCancellationAsync(new ProcessRuntimeRunCancellationCommand(
            rootRunId,
            "unit-test",
            "Stop root and active children before starting a clean E2E pass."));

        Assert.True(result.Succeeded);
        Assert.Equal(ProcessRuntimeStatus.Cancelled, result.Status);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Contains(childRunId.Value.ToString("D"), StringComparison.Ordinal));
        Assert.Equal(ProcessRuntimeStatus.Cancelled, stateStore.GetState(rootRunId).Status);
        Assert.Equal(ProcessRuntimeStatus.Cancelled, stateStore.GetState(childRunId).Status);
        Assert.Empty(dispatchQueue.Requests);
        Assert.Empty(assignmentStore.SavedAssignments);
    }

    [Fact]
    public async Task Request_cancellation_on_root_discovers_child_created_after_barrier_and_emits_root_terminal_event_last()
    {
        var rootRunId = ProcessRunId.New();
        var childRunId = ProcessRunId.New();
        var rootStepId = ProcessStepInstanceId.New();
        var childStepId = ProcessStepInstanceId.New();
        var planId = ProcessInstancePlanId.New();
        var childState = CreateActiveReadyState(childRunId, planId, childStepId, rootRunId);
        var assignmentStore = new RecordingAssignmentStore(CreateAssignment(rootRunId, planId, rootStepId));
        var dispatchQueue = new RecordingDispatchQueue();
        await using var dbContext = CreateDbContext();
        var projectionStore = new EfProcessProjectionStore(dbContext);
        var clock = new FixedProcessProjectionClock(Now);
        var stateStore = new InMemoryRuntimeStateStore(CreateActiveReadyState(rootRunId, planId, rootStepId));
        var unitOfWork = new BarrierInjectingUnitOfWork(stateStore, childState);
        var service = new ProcessRuntimeOperatorApplicationService(
            clock,
            stateStore,
            stateStore,
            assignmentStore,
            unitOfWork,
            dispatchQueue,
            new ProcessRuntimeProjectionCatchupService(
                new EmptyRuntimeEventReplayStore(),
                projectionStore,
                new ProcessRuntimeProjectionProjector(
                    projectionStore,
                    ProcessProjectionJsonCodec.Default,
                    clock,
                    new EfProcessRunRecordStore(dbContext)),
                clock),
            []);

        var result = await service.RequestCancellationAsync(new ProcessRuntimeRunCancellationCommand(
            rootRunId,
            "unit-test",
            "Exercise the cancellation/subprocess barrier."));

        Assert.True(result.Succeeded);
        Assert.Equal(ProcessRuntimeStatus.Cancelled, stateStore.GetState(rootRunId).Status);
        Assert.Equal(ProcessRuntimeStatus.Cancelled, stateStore.GetState(childRunId).Status);
        var events = unitOfWork.Requests.SelectMany(request => request.Mutation.Events).ToArray();
        Assert.Equal(ProcessRuntimeEventTypes.ProcessRunCancelRequested, events[0].EventType);
        Assert.Equal(childRunId, events[^2].RunId);
        Assert.Equal(ProcessRuntimeEventTypes.ProcessRunCancelled, events[^2].EventType);
        Assert.Equal(rootRunId, events[^1].RunId);
        Assert.Equal(ProcessRuntimeEventTypes.ProcessRunCancelled, events[^1].EventType);
    }

    [Fact]
    public async Task Request_cancellation_notifies_observers_with_cancelled_root_and_descendants()
    {
        var rootRunId = ProcessRunId.New();
        var childRunId = ProcessRunId.New();
        var rootStepId = ProcessStepInstanceId.New();
        var childStepId = ProcessStepInstanceId.New();
        var planId = ProcessInstancePlanId.New();
        var assignmentStore = new RecordingAssignmentStore(CreateAssignment(rootRunId, planId, rootStepId));
        var dispatchQueue = new RecordingDispatchQueue();
        var observer = new RecordingCancellationObserver();
        await using var dbContext = CreateDbContext();
        var projectionStore = new EfProcessProjectionStore(dbContext);
        var clock = new FixedProcessProjectionClock(Now);
        var stateStore = new InMemoryRuntimeStateStore(
            CreateActiveReadyState(rootRunId, planId, rootStepId),
            CreateActiveReadyState(childRunId, planId, childStepId, rootRunId));
        var service = new ProcessRuntimeOperatorApplicationService(
            clock,
            stateStore,
            stateStore,
            assignmentStore,
            new RecordingUnitOfWork(stateStore),
            dispatchQueue,
            new ProcessRuntimeProjectionCatchupService(
                new EmptyRuntimeEventReplayStore(),
                projectionStore,
                new ProcessRuntimeProjectionProjector(
                    projectionStore,
                    ProcessProjectionJsonCodec.Default,
                    clock,
                    new EfProcessRunRecordStore(dbContext)),
                clock),
            [],
            [observer]);

        var result = await service.RequestCancellationAsync(new ProcessRuntimeRunCancellationCommand(
            rootRunId,
            "unit-test",
            "Stop root and active children before starting a clean E2E pass."));

        Assert.True(result.Succeeded);
        Assert.Equal(2, observer.Observations.Count);
        Assert.Equal(rootRunId, observer.Observations[0].RequestedRunId);
        Assert.Equal([rootRunId], observer.Observations[0].CancelledRunIds);
        Assert.Equal([childRunId], observer.Observations[1].CancelledRunIds);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic == RecordingCancellationObserver.Diagnostic);
    }

    [Fact]
    public async Task Request_cancellation_does_not_expose_observer_exception_details()
    {
        var runId = ProcessRunId.New();
        var stepId = ProcessStepInstanceId.New();
        var planId = ProcessInstancePlanId.New();
        var assignmentStore = new RecordingAssignmentStore(CreateAssignment(runId, planId, stepId));
        await using var dbContext = CreateDbContext();
        var projectionStore = new EfProcessProjectionStore(dbContext);
        var clock = new FixedProcessProjectionClock(Now);
        var stateStore = new InMemoryRuntimeStateStore(CreateActiveReadyState(runId, planId, stepId));
        var service = new ProcessRuntimeOperatorApplicationService(
            clock,
            stateStore,
            stateStore,
            assignmentStore,
            new RecordingUnitOfWork(stateStore),
            new RecordingDispatchQueue(),
            new ProcessRuntimeProjectionCatchupService(
                new EmptyRuntimeEventReplayStore(),
                projectionStore,
                new ProcessRuntimeProjectionProjector(
                    projectionStore,
                    ProcessProjectionJsonCodec.Default,
                    clock,
                    new EfProcessRunRecordStore(dbContext)),
                clock),
            [],
            [new ThrowingCancellationObserver()]);

        var result = await service.RequestCancellationAsync(new ProcessRuntimeRunCancellationCommand(
            runId,
            "unit-test",
            "Cancel the run."));

        Assert.True(result.Succeeded);
        Assert.NotEmpty(result.Diagnostics);
        Assert.All(result.Diagnostics, diagnostic => Assert.True(
            ProcessPublicReceiptTextPolicy.IsSafe(
                diagnostic,
                ProcessPublicReceiptTextPolicy.MaximumPublicMessageLength)));
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Contains("observer-secret", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Contains(@"C:\private\operator", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Contains(nameof(ThrowingCancellationObserver), StringComparison.Ordinal));
    }

    [Fact]
    public async Task Request_cancellation_rejection_leaves_root_pending_and_notifies_already_cancelled_runs()
    {
        var rootRunId = ProcessRunId.New();
        var cancelledChildRunId = ProcessRunId.New();
        var stuckChildRunId = ProcessRunId.New();
        var planId = ProcessInstancePlanId.New();
        var observer = new RecordingCancellationObserver();
        var stateStore = new InMemoryRuntimeStateStore(
            CreateActiveReadyState(rootRunId, planId, ProcessStepInstanceId.New()),
            CreateActiveReadyState(cancelledChildRunId, planId, ProcessStepInstanceId.New(), rootRunId),
            CreateActiveReadyState(stuckChildRunId, planId, ProcessStepInstanceId.New(), rootRunId));
        var unitOfWork = new RejectingDescendantCancellationUnitOfWork(stateStore, stuckChildRunId);
        await using var dbContext = CreateDbContext();
        var projectionStore = new EfProcessProjectionStore(dbContext);
        var clock = new FixedProcessProjectionClock(Now);
        var service = new ProcessRuntimeOperatorApplicationService(
            clock,
            stateStore,
            stateStore,
            new RecordingAssignmentStore(CreateAssignment(rootRunId, planId, stateStore.GetState(rootRunId).Steps.Single().StepInstanceId)),
            unitOfWork,
            new RecordingDispatchQueue(),
            new ProcessRuntimeProjectionCatchupService(
                new EmptyRuntimeEventReplayStore(),
                projectionStore,
                new ProcessRuntimeProjectionProjector(
                    projectionStore,
                    ProcessProjectionJsonCodec.Default,
                    clock,
                    new EfProcessRunRecordStore(dbContext)),
                clock),
            [],
            [observer]);

        var result = await service.RequestCancellationAsync(new ProcessRuntimeRunCancellationCommand(
            rootRunId,
            "unit-test",
            "Keep the barrier pending when one descendant cannot be cancelled."));

        Assert.False(result.Succeeded);
        Assert.Equal(ProcessRuntimeTransitionOutcome.Rejected, result.Outcome);
        Assert.Equal(ProcessRuntimeStatus.CancelRequested, result.Status);
        Assert.Equal(ProcessRuntimeStatus.CancelRequested, stateStore.GetState(rootRunId).Status);
        Assert.Equal(ProcessRuntimeStatus.Cancelled, stateStore.GetState(cancelledChildRunId).Status);
        Assert.Equal(ProcessRuntimeStatus.Active, stateStore.GetState(stuckChildRunId).Status);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Contains(stuckChildRunId.Value.ToString("D"), StringComparison.Ordinal));
        var observedRunIds = observer.Observations.SelectMany(observation => observation.CancelledRunIds).ToArray();
        Assert.Contains(rootRunId, observedRunIds);
        Assert.Contains(cancelledChildRunId, observedRunIds);
        Assert.DoesNotContain(stuckChildRunId, observedRunIds);
        Assert.DoesNotContain(
            unitOfWork.Requests.SelectMany(request => request.Mutation.Events),
            runtimeEvent =>
                runtimeEvent.RunId == rootRunId &&
                runtimeEvent.EventType == ProcessRuntimeEventTypes.ProcessRunCancelled);
    }

    [Fact]
    public async Task Request_cancellation_finalizes_root_when_authoritative_final_query_finds_failed_descendant_terminal()
    {
        var rootRunId = ProcessRunId.New();
        var childRunId = ProcessRunId.New();
        var rootStepId = ProcessStepInstanceId.New();
        var planId = ProcessInstancePlanId.New();
        var stateStore = new InMemoryRuntimeStateStore(
            CreateActiveReadyState(rootRunId, planId, rootStepId),
            CreateActiveReadyState(childRunId, planId, ProcessStepInstanceId.New(), rootRunId));
        var hierarchyStore = new CompletingOnFinalHierarchyQueryStore(stateStore, childRunId);
        var unitOfWork = new RejectingDescendantCancellationUnitOfWork(stateStore, childRunId);
        await using var dbContext = CreateDbContext();
        var projectionStore = new EfProcessProjectionStore(dbContext);
        var clock = new FixedProcessProjectionClock(Now);
        var service = new ProcessRuntimeOperatorApplicationService(
            clock,
            stateStore,
            hierarchyStore,
            new RecordingAssignmentStore(CreateAssignment(rootRunId, planId, rootStepId)),
            unitOfWork,
            new RecordingDispatchQueue(),
            new ProcessRuntimeProjectionCatchupService(
                new EmptyRuntimeEventReplayStore(),
                projectionStore,
                new ProcessRuntimeProjectionProjector(
                    projectionStore,
                    ProcessProjectionJsonCodec.Default,
                    clock,
                    new EfProcessRunRecordStore(dbContext)),
                clock),
            []);

        var result = await service.RequestCancellationAsync(new ProcessRuntimeRunCancellationCommand(
            rootRunId,
            "unit-test",
            "Use the final hierarchy postcondition."));

        Assert.True(result.Succeeded);
        Assert.Equal(ProcessRuntimeStatus.Cancelled, stateStore.GetState(rootRunId).Status);
        Assert.Equal(ProcessRuntimeStatus.Completed, stateStore.GetState(childRunId).Status);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Contains(childRunId.Value.ToString("D"), StringComparison.Ordinal));
        var lastEvent = unitOfWork.Requests.SelectMany(request => request.Mutation.Events).Last();
        Assert.Equal(rootRunId, lastEvent.RunId);
        Assert.Equal(ProcessRuntimeEventTypes.ProcessRunCancelled, lastEvent.EventType);
    }

    private static ProcessRuntimeStateSnapshot CreateFailedState(
        ProcessRunId runId,
        ProcessInstancePlanId planId,
        ProcessStepInstanceId stepId)
    {
        var resultKey = StrategyResultIdempotencyKey.New();
        return new ProcessRuntimeStateSnapshot(
            runId,
            runId,
            planId,
            "sha256:plan",
            ProcessRuntimeStatus.Failed,
            [
                new ProcessRuntimeStepState(
                    stepId,
                    ProcessStepDefinitionId.New(),
                    ProcessRuntimeStepStatus.Failed,
                    IsExecutable: true,
                    AttemptNumber: 2,
                    DependencyStepIds: new HashSet<ProcessStepInstanceId>(),
                    RequiredArtifactSlots: new HashSet<ArtifactSlotId>(),
                    ActiveClaimToken: null,
                    resultKey)
            ],
            [],
            [
                new StrategyResultReceipt(
                    stepId,
                    new StrategyId("strategy.adapter.workflow.execute"),
                    resultKey,
                    StrategyOutcome.Failed,
                    ProcessRuntimeStepStatus.Failed,
                    "sha256:failed")
            ],
            new HashSet<ArtifactSlotId>(),
            Now.AddMinutes(-5));
    }

    private static ProcessRuntimeStateSnapshot CreateFailedStateWithIncidentReceipt(
        ProcessRunId runId,
        ProcessInstancePlanId planId,
        ProcessStepInstanceId stepId)
    {
        var resultKey = StrategyResultIdempotencyKey.New();
        return new ProcessRuntimeStateSnapshot(
            runId,
            runId,
            planId,
            "sha256:plan",
            ProcessRuntimeStatus.Failed,
            [
                new ProcessRuntimeStepState(
                    stepId,
                    ProcessStepDefinitionId.New(),
                    ProcessRuntimeStepStatus.Failed,
                    IsExecutable: true,
                    AttemptNumber: 2,
                    DependencyStepIds: new HashSet<ProcessStepInstanceId>(),
                    RequiredArtifactSlots: new HashSet<ArtifactSlotId>(),
                    ActiveClaimToken: null,
                    resultKey)
            ],
            [],
            [
                new StrategyResultReceipt(
                    stepId,
                    new StrategyId("strategy.adapter.workflow.execute"),
                    resultKey,
                    StrategyOutcome.NeedsManager,
                    ProcessRuntimeStepStatus.Blocked,
                    "sha256:incident",
                    [
                        new StrategyResultDiagnosticReceipt(
                            "process.adapter.product_required_tool_receipt_missing",
                            StrategyDiagnosticSensitivity.Normal,
                            "sha256:receipt",
                            "Step 'create-dotnet-project' claimed completion but required current-run product tool receipt(s) are missing: workspace_pwsh_run_script.",
                            RestrictedEvidenceReference: null,
                            ProcessDiagnosticRetrySafety.SafeToRetry,
                            ProcessDiagnosticIdempotencyClassification.Idempotent),
                        new StrategyResultDiagnosticReceipt(
                            "process.adapter.product_required_file_content_missing",
                            StrategyDiagnosticSensitivity.Normal,
                            "sha256:readback",
                            "Step 'create-dotnet-project' claimed completion but required product file content/readback check(s) failed: Calculator.slnx does not contain src/Calculator/Calculator.csproj.",
                            RestrictedEvidenceReference: null,
                            ProcessDiagnosticRetrySafety.SafeToRetry,
                            ProcessDiagnosticIdempotencyClassification.Idempotent)
                    ])
            ],
            new HashSet<ArtifactSlotId>(),
            Now.AddMinutes(-5));
    }

    private static ProcessRuntimeStateSnapshot CreateBlockedUpstreamRecoveryState(
        ProcessRunId runId,
        ProcessInstancePlanId planId,
        ProcessStepInstanceId producerStepId,
        ProcessStepInstanceId blockedStepId,
        StrategyResultIdempotencyKey sourceResultKey,
        bool inputRestored = false)
    {
        var artifactSlotId = ArtifactSlotId.New();
        var producerResultKey = StrategyResultIdempotencyKey.New();
        var state = new ProcessRuntimeStateSnapshot(
            runId,
            runId,
            planId,
            "sha256:plan",
            ProcessRuntimeStatus.Blocked,
            [
                new ProcessRuntimeStepState(
                    producerStepId,
                    ProcessStepDefinitionId.New(),
                    ProcessRuntimeStepStatus.Completed,
                    IsExecutable: true,
                    AttemptNumber: 1,
                    DependencyStepIds: new HashSet<ProcessStepInstanceId>(),
                    RequiredArtifactSlots: new HashSet<ArtifactSlotId>(),
                    ActiveClaimToken: null,
                    producerResultKey),
                new ProcessRuntimeStepState(
                    blockedStepId,
                    ProcessStepDefinitionId.New(),
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
                    new StrategyId("strategy.adapter.workflow.execute"),
                    producerResultKey,
                    StrategyOutcome.Succeeded,
                    ProcessRuntimeStepStatus.Completed,
                    "sha256:producer-result"),
                new StrategyResultReceipt(
                    blockedStepId,
                    new StrategyId("strategy.adapter.workflow.execute"),
                    sourceResultKey,
                    StrategyOutcome.NeedsManager,
                    ProcessRuntimeStepStatus.Blocked,
                    "sha256:missing-input-result",
                    [
                        new StrategyResultDiagnosticReceipt(
                            ProcessRuntimeDiagnosticCodes.MissingRequiredInputArtifact,
                            StrategyDiagnosticSensitivity.Normal,
                            "sha256:missing-input",
                            "Required input artifact is missing.",
                            RestrictedEvidenceReference: null,
                            ProcessDiagnosticRetrySafety.UnsafeToRetry,
                            ProcessDiagnosticIdempotencyClassification.Idempotent)
                    ],
                    recoveryDecision: new ProcessRecoveryDecisionReceipt(
                        ProcessFailureCategory.MissingArtifact,
                        ProcessRecoveryDecisionKind.ManagerRequired,
                        ProcessRuntimeDiagnosticCodes.MissingRequiredInputArtifact,
                        "process.upstream-artifact-rework-required",
                        "Rework the responsible upstream producer.")
                    {
                        RouteKind = ProcessRecoveryRouteKind.UpstreamStepRework,
                        ResponsibleStepInstanceId = producerStepId,
                        DiagnosticFingerprint = "sha256:missing-input-fingerprint"
                    })
            ],
            inputRestored
                ? new HashSet<ArtifactSlotId> { artifactSlotId }
                : new HashSet<ArtifactSlotId>(),
            Now.AddMinutes(-5));
        return state with
        {
            ConnectedInputArtifacts =
            [
                new ProcessRuntimeInputArtifactReceipt(
                    blockedStepId,
                    artifactSlotId,
                    inputRestored
                        ? ProcessArtifactInputAvailability.Available
                        : ProcessArtifactInputAvailability.Expected,
                    producerStepId,
                    inputRestored ? ArtifactInstanceId.New() : null,
                    inputRestored ? "sha256:restored-input" : string.Empty,
                    "sha256:producer-to-consumer")
            ]
        };
    }

    private static ProcessRuntimeStateSnapshot CreateBlockedManagerActionState(
        ProcessRunId runId,
        ProcessInstancePlanId planId,
        ProcessStepInstanceId blockedStepId,
        StrategyResultIdempotencyKey sourceResultKey)
    {
        return new ProcessRuntimeStateSnapshot(
            runId,
            runId,
            planId,
            "sha256:plan",
            ProcessRuntimeStatus.Blocked,
            [
                new ProcessRuntimeStepState(
                    blockedStepId,
                    ProcessStepDefinitionId.New(),
                    ProcessRuntimeStepStatus.Blocked,
                    IsExecutable: true,
                    AttemptNumber: 1,
                    DependencyStepIds: new HashSet<ProcessStepInstanceId>(),
                    RequiredArtifactSlots: new HashSet<ArtifactSlotId>(),
                    ActiveClaimToken: null,
                    CompletedResultKey: null)
            ],
            [],
            [
                new StrategyResultReceipt(
                    blockedStepId,
                    new StrategyId("strategy.adapter.workflow.execute"),
                    sourceResultKey,
                    StrategyOutcome.NeedsManager,
                    ProcessRuntimeStepStatus.Blocked,
                    "sha256:missing-output-result",
                    [
                        new StrategyResultDiagnosticReceipt(
                            "process.runtime.missing_expected_output_artifact",
                            StrategyDiagnosticSensitivity.Normal,
                            "sha256:missing-output",
                            "Expected output artifact is missing.",
                            RestrictedEvidenceReference: null,
                            ProcessDiagnosticRetrySafety.UnsafeToRetry,
                            ProcessDiagnosticIdempotencyClassification.Idempotent)
                    ],
                    recoveryDecision: new ProcessRecoveryDecisionReceipt(
                        ProcessFailureCategory.MissingArtifact,
                        ProcessRecoveryDecisionKind.ManagerRequired,
                        "process.runtime.missing_expected_output_artifact",
                        "process.manager-review-required",
                        "Retry the source step.")
                    {
                        RouteKind = ProcessRecoveryRouteKind.ManagerAction,
                        ResponsibleStepInstanceId = null,
                        DiagnosticFingerprint = "sha256:missing-output-fingerprint"
                    })
            ],
            new HashSet<ArtifactSlotId>(),
            Now.AddMinutes(-5));
    }

    private static ProcessRuntimeBlockedRecoveryAuthorization CreateBlockedRecoveryAuthorization(
        ProcessRuntimeStateSnapshot state,
        ProcessStepInstanceId blockedStepId,
        StrategyResultIdempotencyKey sourceResultKey,
        ProcessStepInstanceId responsibleTargetStepId,
        ProcessRuntimeBlockedRecoveryPhase phase =
            ProcessRuntimeBlockedRecoveryPhase.UpstreamProducer)
    {
        return new ProcessRuntimeBlockedRecoveryAuthorization(
            state.UpdatedAtUtc,
            ProcessRuntimeStatus.Blocked,
            blockedStepId,
            sourceResultKey,
            "sha256:missing-input-fingerprint",
            ProcessRecoveryRouteKind.UpstreamStepRework,
            responsibleTargetStepId,
            phase);
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
                "sha256:definition",
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
            "sha256:plan");
    }

    private static ProcessRuntimeStateSnapshot CreateExpiredRunningState(
        ProcessRunId runId,
        ProcessInstancePlanId planId,
        ProcessStepInstanceId stepId,
        DispatchClaimToken claimToken)
    {
        return new ProcessRuntimeStateSnapshot(
            runId,
            runId,
            planId,
            "sha256:plan",
            ProcessRuntimeStatus.Active,
            [
                new ProcessRuntimeStepState(
                    stepId,
                    ProcessStepDefinitionId.New(),
                    ProcessRuntimeStepStatus.Running,
                    IsExecutable: true,
                    AttemptNumber: 1,
                    DependencyStepIds: new HashSet<ProcessStepInstanceId>(),
                    RequiredArtifactSlots: new HashSet<ArtifactSlotId>(),
                    claimToken,
                    CompletedResultKey: null)
            ],
            [
                new DispatchClaimState(
                    claimToken,
                    stepId,
                    new DispatcherOwnerId("process-runtime-dispatcher"),
                    DispatchClaimStatus.Claimed,
                    AttemptNumber: 1,
                    CreatedAtUtc: Now.AddMinutes(-10),
                    ExpiresAtUtc: Now.AddMinutes(-1),
                    RenewedAtUtc: null,
                    ResultIdempotencyKey: null)
            ],
            [],
            new HashSet<ArtifactSlotId>(),
            Now.AddMinutes(-1));
    }

    private static ProcessRuntimeStateSnapshot CreateActiveReadyState(
        ProcessRunId runId,
        ProcessInstancePlanId planId,
        ProcessStepInstanceId stepId,
        ProcessRunId? rootRunId = null)
    {
        return new ProcessRuntimeStateSnapshot(
            rootRunId ?? runId,
            runId,
            planId,
            "sha256:plan",
            ProcessRuntimeStatus.Active,
            [
                new ProcessRuntimeStepState(
                    stepId,
                    ProcessStepDefinitionId.New(),
                    ProcessRuntimeStepStatus.Ready,
                    IsExecutable: true,
                    AttemptNumber: 1,
                    DependencyStepIds: new HashSet<ProcessStepInstanceId>(),
                    RequiredArtifactSlots: new HashSet<ArtifactSlotId>(),
                    ActiveClaimToken: null,
                    CompletedResultKey: null)
            ],
            [],
            [],
            new HashSet<ArtifactSlotId>(),
            Now.AddMinutes(-1));
    }

    private static ProcessRuntimeStepAssignment CreateAssignment(
        ProcessRunId runId,
        ProcessInstancePlanId planId,
        ProcessStepInstanceId stepId,
        string stepKey = "feature-repair",
        IReadOnlyDictionary<string, string>? launchVariables = null)
    {
        return new ProcessRuntimeStepAssignment(
            runId,
            planId,
            stepId,
            stepKey,
            "software-engineer",
            "software-engineer",
            ".NET feature implementer",
            ProcessLaunchExecutorKinds.Agent,
            Guid.NewGuid().ToString("D"),
            ".NET Application Developer",
            "Repair the generated app and return structured process output.",
            "sha256:readiness",
            "Matched role and tool readiness.",
            [],
            [],
            [ProcessOperationContractNames.MutateProductTarget, ProcessOperationContractNames.CaptureRuntimeProof],
            ProcessOperationContractNames.ExternalProductTargetMutable,
            launchVariables ?? new Dictionary<string, string>(),
            BranchGate: null,
            Now.AddMinutes(-10));
    }

    private static ProcessRuntimeStepAssignment CreateArtifactRecoveryAssignment(
        ProcessRunId runId,
        ProcessInstancePlanId planId,
        ProcessStepInstanceId stepId,
        string stepKey)
    {
        return CreateAssignment(runId, planId, stepId, stepKey) with
        {
            AllowedOperations =
            [
                nameof(ProcessDefinitionStepOperationKind.ReadUpstreamArtifacts),
                nameof(ProcessDefinitionStepOperationKind.WriteManagedProcessArtifacts),
                nameof(ProcessDefinitionStepOperationKind.RecoverArtifactsOnly)
            ],
            OperationTargetScope =
                nameof(ProcessDefinitionStepTargetScopeKind.ManagedProcessArtifactsOnly)
        };
    }

    private static IReadOnlyDictionary<string, string> CreateDotNetCreateProjectLaunchVariables(ProcessRunId runId)
    {
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceipts] = "workspace_pwsh_run_script",
            [ProcessRuntimeLaunchVariables.ProductCompletionRequiredFileContentChecks] =
                """[{"pathCandidates":["Calculator.slnx"],"requiredTextAnyGroups":[["src/Calculator/Calculator.csproj"]]}]""",
            ["DotNetCreateProjectScript"] = "$ErrorActionPreference = 'Stop'",
            ["DotNetCreateProjectScriptRef"] = $"artifacts/process-runs/{runId.Value:D}/scripts/create-dotnet-project.ps1",
            ["DotNetCreateProjectSideEffectManifest"] = """{"version":1,"mode":"ProductMutation"}""",
            ["WorkspaceAlias"] = "external-target/C/repositories/calculator"
        };
    }

    private static ProcessStepRecoveryInstructionBuilder CreateRecoveryInstructionBuilder()
        => new([new GenericProcessRecoveryAdviceProvider()]);

    private static ProcessPersistenceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ProcessPersistenceDbContext>()
            .UseInMemoryDatabase($"process-operator-{Guid.NewGuid():N}")
            .Options;
        return new ProcessPersistenceDbContext(options);
    }

    private sealed class FixedProcessProjectionClock(DateTimeOffset utcNow) : IProcessProjectionClock
    {
        public DateTimeOffset GetUtcNow() => utcNow;
    }

    public enum BlockedRecoveryAuthorizationMismatch
    {
        StaleState,
        ResultKey,
        ResponsibleTarget,
        UnrelatedTarget
    }

    private sealed class InMemoryRuntimeStateStore(params ProcessRuntimeStateSnapshot[] states) :
        IProcessRuntimeStateStore,
        IProcessRuntimeRunHierarchyStore
    {
        private readonly Dictionary<ProcessRunId, ProcessRuntimeStateSnapshot> statesByRunId =
            states.ToDictionary(state => state.RunId);

        public ProcessRuntimeStateSnapshot State
        {
            get => statesByRunId.Values.Single();
            set => statesByRunId[value.RunId] = value;
        }

        public ProcessRuntimeStateSnapshot GetState(ProcessRunId runId)
            => statesByRunId[runId];

        public void AddState(ProcessRuntimeStateSnapshot state)
            => statesByRunId.Add(state.RunId, state);

        public Task<ProcessRuntimeStateSnapshot?> LoadAsync(
            ProcessRunId runId,
            CancellationToken cancellationToken = default)
        {
            statesByRunId.TryGetValue(runId, out var state);
            return Task.FromResult<ProcessRuntimeStateSnapshot?>(state);
        }

        public Task<IReadOnlyList<ProcessRunId>> FindCancellableDescendantRunIdsAsync(
            ProcessRunId rootRunId,
            CancellationToken cancellationToken = default)
        {
            var descendants = statesByRunId.Values
                .Where(state =>
                    state.RootRunId == rootRunId &&
                    state.RunId != rootRunId &&
                    state.Status is not ProcessRuntimeStatus.Completed and
                        not ProcessRuntimeStatus.Failed and
                        not ProcessRuntimeStatus.Cancelled and
                        not ProcessRuntimeStatus.CancelRequested)
                .OrderByDescending(state => state.UpdatedAtUtc)
                .Select(state => state.RunId)
                .ToArray();

            return Task.FromResult<IReadOnlyList<ProcessRunId>>(descendants);
        }
    }

    private sealed class RecordingUnitOfWork(InMemoryRuntimeStateStore? stateStore = null) : IProcessRuntimeUnitOfWork
    {
        public List<ProcessRuntimeCommitRequest> Requests { get; } = [];

        public Task<ProcessRuntimeCommitResult> CommitAsync(
            ProcessRuntimeCommitRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            if (stateStore is not null)
            {
                stateStore.State = request.Mutation.State;
            }

            return Task.FromResult(ProcessRuntimeCommitResult.FromMutation(request.Mutation));
        }
    }

    private sealed class BarrierInjectingUnitOfWork(
        InMemoryRuntimeStateStore stateStore,
        ProcessRuntimeStateSnapshot childState) : IProcessRuntimeUnitOfWork
    {
        private bool childInjected;

        public List<ProcessRuntimeCommitRequest> Requests { get; } = [];

        public Task<ProcessRuntimeCommitResult> CommitAsync(
            ProcessRuntimeCommitRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            stateStore.State = request.Mutation.State;
            if (!childInjected &&
                request.Mutation.Events.Any(runtimeEvent =>
                    runtimeEvent.EventType == ProcessRuntimeEventTypes.ProcessRunCancelRequested))
            {
                childInjected = true;
                stateStore.AddState(childState);
            }

            return Task.FromResult(ProcessRuntimeCommitResult.FromMutation(request.Mutation));
        }
    }

    private sealed class RejectingDescendantCancellationUnitOfWork(
        InMemoryRuntimeStateStore stateStore,
        ProcessRunId rejectedRunId) : IProcessRuntimeUnitOfWork
    {
        public List<ProcessRuntimeCommitRequest> Requests { get; } = [];

        public Task<ProcessRuntimeCommitResult> CommitAsync(
            ProcessRuntimeCommitRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            if (request.Mutation.State.RunId == rejectedRunId &&
                request.Mutation.Events.Any(runtimeEvent =>
                    runtimeEvent.EventType == ProcessRuntimeEventTypes.ProcessRunCancelled))
            {
                return Task.FromResult(ProcessRuntimeCommitResult.FromMutation(
                    ProcessRuntimeMutation.Rejected(
                        request.OriginalState,
                        "Runtime.TestCancellationRejected",
                        "The test descendant remains cancellable.")));
            }

            stateStore.State = request.Mutation.State;
            return Task.FromResult(ProcessRuntimeCommitResult.FromMutation(request.Mutation));
        }
    }

    private sealed class CompletingOnFinalHierarchyQueryStore(
        InMemoryRuntimeStateStore stateStore,
        ProcessRunId childRunId) : IProcessRuntimeRunHierarchyStore
    {
        private int queryCount;

        public Task<IReadOnlyList<ProcessRunId>> FindCancellableDescendantRunIdsAsync(
            ProcessRunId rootRunId,
            CancellationToken cancellationToken = default)
        {
            queryCount++;
            if (queryCount == 2)
            {
                var childState = stateStore.GetState(childRunId);
                stateStore.State = childState with
                {
                    Status = ProcessRuntimeStatus.Completed,
                    UpdatedAtUtc = childState.UpdatedAtUtc.AddMilliseconds(1)
                };
            }

            return stateStore.FindCancellableDescendantRunIdsAsync(rootRunId, cancellationToken);
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

    private sealed class RecordingAssignmentStore(params ProcessRuntimeStepAssignment[] initialAssignments) :
        IProcessRuntimeStepAssignmentStore
    {
        private readonly Dictionary<(ProcessRunId RunId, ProcessStepInstanceId StepInstanceId), ProcessRuntimeStepAssignment> assignments =
            initialAssignments.ToDictionary(
                assignment => (assignment.RunId, assignment.StepInstanceId));

        public List<ProcessRuntimeStepAssignment> SavedAssignments { get; } = [];

        public ValueTask SaveAsync(
            IReadOnlyList<ProcessRuntimeStepAssignment> assignments,
            CancellationToken cancellationToken = default)
        {
            foreach (var assignment in assignments)
            {
                this.assignments[(assignment.RunId, assignment.StepInstanceId)] = assignment;
                SavedAssignments.Add(assignment);
            }

            return ValueTask.CompletedTask;
        }

        public ValueTask<IReadOnlyList<ProcessRuntimeStepAssignment>> LoadByRunAsync(
            ProcessRunId runId,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(assignments.Values.Where(assignment => assignment.RunId == runId).ToArray() as IReadOnlyList<ProcessRuntimeStepAssignment>);

        public ValueTask<IReadOnlyList<ProcessRuntimeStepAssignment>> FindByLaunchVariablesAsync(
            IReadOnlyDictionary<string, string> requiredVariables,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IReadOnlyList<ProcessRuntimeStepAssignment>>([]);

        public ValueTask<ProcessRuntimeStepAssignment?> LoadAsync(
            ProcessRunId runId,
            ProcessStepInstanceId stepInstanceId,
            CancellationToken cancellationToken = default)
        {
            assignments.TryGetValue((runId, stepInstanceId), out var assignment);
            return ValueTask.FromResult(assignment);
        }
    }

    private sealed class StubPlanStore(ProcessInstancePlan plan) : IProcessInstancePlanStore
    {
        public ValueTask<PersistedProcessInstancePlan> PersistAsync(
            ProcessInstancePlan processPlan,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(
                new PersistedProcessInstancePlan(processPlan.Header.PlanId, processPlan.PlanHash));
        }

        public ValueTask<ProcessInstancePlan?> LoadAsync(
            ProcessInstancePlanId planId,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult<ProcessInstancePlan?>(
                planId == plan.Header.PlanId ? plan : null);
        }
    }

    private sealed class RecordingCancellationObserver : IProcessRuntimeRunCancellationObserver
    {
        public const string Diagnostic = "Cancellation observer ran.";

        public List<ProcessRuntimeRunCancellationObservation> Observations { get; } = [];

        public ValueTask<ProcessRuntimeRunCancellationObservationResult> OnRunsCancelledAsync(
            ProcessRuntimeRunCancellationObservation observation,
            CancellationToken cancellationToken = default)
        {
            Observations.Add(observation);
            return ValueTask.FromResult(new ProcessRuntimeRunCancellationObservationResult([Diagnostic]));
        }
    }

    private sealed class ThrowingCancellationObserver : IProcessRuntimeRunCancellationObserver
    {
        public ValueTask<ProcessRuntimeRunCancellationObservationResult> OnRunsCancelledAsync(
            ProcessRuntimeRunCancellationObservation observation,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException(
                $"password=observer-secret at C:\\private\\operator\\observer.log {new string('x', 3_000)}");
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
}
