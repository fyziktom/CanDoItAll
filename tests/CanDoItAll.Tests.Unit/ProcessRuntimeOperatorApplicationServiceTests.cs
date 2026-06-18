using CanDoItAll.AgentFramework.Models;
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
            assignmentStore,
            new RecordingUnitOfWork(stateStore),
            dispatchQueue,
            new ProcessRuntimeProjectionCatchupService(
                new EmptyRuntimeEventReplayStore(),
                projectionStore,
                new ProcessRuntimeProjectionProjector(projectionStore, ProcessProjectionJsonCodec.Default, clock),
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
            assignmentStore,
            unitOfWork,
            dispatchQueue,
            new ProcessRuntimeProjectionCatchupService(
                new EmptyRuntimeEventReplayStore(),
                projectionStore,
                new ProcessRuntimeProjectionProjector(projectionStore, ProcessProjectionJsonCodec.Default, clock),
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

    private static ProcessRuntimeStepAssignment CreateAssignment(
        ProcessRunId runId,
        ProcessInstancePlanId planId,
        ProcessStepInstanceId stepId)
    {
        return new ProcessRuntimeStepAssignment(
            runId,
            planId,
            stepId,
            "feature-repair",
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
            new Dictionary<string, string>(),
            BranchGate: null,
            Now.AddMinutes(-10));
    }

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

    private sealed class InMemoryRuntimeStateStore(ProcessRuntimeStateSnapshot state) : IProcessRuntimeStateStore
    {
        public ProcessRuntimeStateSnapshot State { get; set; } = state;

        public Task<ProcessRuntimeStateSnapshot?> LoadAsync(
            ProcessRunId runId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<ProcessRuntimeStateSnapshot?>(State.RunId == runId ? State : null);
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
