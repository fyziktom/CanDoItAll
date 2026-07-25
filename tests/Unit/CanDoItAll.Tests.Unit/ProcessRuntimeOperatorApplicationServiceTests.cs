using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Unit;

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
    }

    private sealed class RecordingAssignmentStore(ProcessRuntimeStepAssignment assignment) : IProcessRuntimeStepAssignmentStore
    {
        private readonly Dictionary<(ProcessRunId RunId, ProcessStepInstanceId StepInstanceId), ProcessRuntimeStepAssignment> assignments =
            new()
            {
                [(assignment.RunId, assignment.StepInstanceId)] = assignment
            };

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
