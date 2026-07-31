using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Unit;

public sealed class AgentFrameworkExecutionRecoveryServiceTests
{
    [Fact]
    public async Task RecoverInterruptedRuns_reconciles_existing_and_new_terminal_execution_leases()
    {
        var terminalRun = CreateRun(ExecutionState.Completed, DateTimeOffset.UtcNow.AddMinutes(-10));
        var interruptedRun = CreateRun(ExecutionState.Running, DateTimeOffset.UtcNow.AddMinutes(-5));
        var waitingRun = CreateRun(ExecutionState.WaitingOnTool, DateTimeOffset.UtcNow);
        var store = new RecordingExecutionRunStore(
        [
            CreateDetail(terminalRun),
            CreateDetail(interruptedRun),
            CreateDetail(waitingRun)
        ]);
        var cleaner = new RecordingProcessLeaseCleaner();
        var service = new AgentFrameworkExecutionRecoveryService(
            store,
            cleaner,
            [],
            NullLogger<AgentFrameworkExecutionRecoveryService>.Instance);

        var recoveredCount = await service.RecoverInterruptedRunsAsync(
            DateTimeOffset.UtcNow.AddMinutes(-1));

        Assert.Equal(1, recoveredCount);
        Assert.Equal(
            [terminalRun.Id, interruptedRun.Id],
            cleaner.ExecutionRunIds);
        var recovered = await store.GetExecutionRunAsync(interruptedRun.Id);
        Assert.NotNull(recovered);
        Assert.Equal(ExecutionState.Failed, recovered.State);
        Assert.Equal(RunOutcome.Cancelled, recovered.Outcome);
        var stillWaiting = await store.GetExecutionRunAsync(waitingRun.Id);
        Assert.NotNull(stillWaiting);
        Assert.Equal(ExecutionState.WaitingOnTool, stillWaiting.State);
    }

    [Fact]
    public async Task RecoverInterruptedRuns_continues_reconciliation_when_one_cleanup_throws()
    {
        var firstRun = CreateRun(ExecutionState.Completed, DateTimeOffset.UtcNow.AddMinutes(-10));
        var secondRun = CreateRun(ExecutionState.Failed, DateTimeOffset.UtcNow.AddMinutes(-9));
        var store = new RecordingExecutionRunStore(
        [
            CreateDetail(firstRun),
            CreateDetail(secondRun)
        ]);
        var cleaner = new RecordingProcessLeaseCleaner(firstRun.Id);
        var service = new AgentFrameworkExecutionRecoveryService(
            store,
            cleaner,
            [],
            NullLogger<AgentFrameworkExecutionRecoveryService>.Instance);

        var recoveredCount = await service.RecoverInterruptedRunsAsync(
            DateTimeOffset.UtcNow);

        Assert.Equal(0, recoveredCount);
        Assert.Equal(
            [firstRun.Id, secondRun.Id],
            cleaner.ExecutionRunIds);
    }

    private static ExecutionRunDetail CreateDetail(ExecutionRunRecord run)
        => new(run, null, [], []);

    private static ExecutionRunRecord CreateRun(
        ExecutionState state,
        DateTimeOffset createdAtUtc)
    {
        var isTerminal = state is ExecutionState.Completed or ExecutionState.Failed;
        return new ExecutionRunRecord(
            Id: Guid.NewGuid(),
            AgentId: Guid.NewGuid(),
            ChatSessionId: null,
            Title: "Recovery test",
            SourceKind: "process-step",
            SourceId: "step-001",
            CorrelationId: Guid.NewGuid().ToString("N"),
            CausationId: string.Empty,
            RequestedBy: "unit-test",
            RequestedByKind: "system",
            MetadataJson: "{}",
            InputSummary: string.Empty,
            ResultSummary: string.Empty,
            ProviderName: "test",
            Model: "test",
            State: state,
            Outcome: state switch
            {
                ExecutionState.Completed => RunOutcome.Succeeded,
                ExecutionState.Failed => RunOutcome.Failed,
                _ => null
            },
            CreatedAtUtc: createdAtUtc,
            UpdatedAtUtc: createdAtUtc,
            StartedAtUtc: createdAtUtc,
            CompletedAtUtc: isTerminal ? createdAtUtc : null,
            RuntimeSessionKey: isTerminal ? string.Empty : "runtime-session",
            SerializedSessionStateJson: null,
            PendingApprovals: []);
    }

    private sealed class RecordingProcessLeaseCleaner(Guid? throwingRunId = null)
        : IWorkspaceExecutionRunProcessLeaseCleaner
    {
        public List<Guid> ExecutionRunIds { get; } = [];

        public Task<WorkspaceExecutionRunProcessCleanupResult> CleanupAsync(
            Guid executionRunId)
        {
            ExecutionRunIds.Add(executionRunId);
            if (executionRunId == throwingRunId)
            {
                throw new InvalidOperationException("simulated cleanup failure");
            }

            return Task.FromResult(
                WorkspaceExecutionRunProcessCleanupResult.Empty(executionRunId));
        }
    }

    private sealed class RecordingExecutionRunStore(
        IReadOnlyList<ExecutionRunDetail> executionRuns)
        : ISandboxWorkspaceExecutionRunStore
    {
        private readonly Dictionary<Guid, ExecutionRunDetail> details =
            executionRuns.ToDictionary(detail => detail.Run.Id);

        public Task<IReadOnlyList<ExecutionRunRecord>> ListExecutionRunsAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ExecutionRunRecord>>(
                details.Values
                    .Select(detail => detail.Run)
                    .OrderBy(run => run.CreatedAtUtc)
                    .ToArray());

        public Task<ExecutionRunRecord?> GetExecutionRunAsync(
            Guid executionRunId,
            CancellationToken cancellationToken = default)
        {
            details.TryGetValue(executionRunId, out var detail);
            return Task.FromResult(detail?.Run);
        }

        public Task<ExecutionRunDetail?> GetExecutionRunDetailAsync(
            Guid executionRunId,
            CancellationToken cancellationToken = default)
        {
            details.TryGetValue(executionRunId, out var detail);
            return Task.FromResult(detail);
        }

        public Task<ExecutionRunDetail> SaveExecutionRunDetailAsync(
            ExecutionRunDetail detail,
            CancellationToken cancellationToken = default)
        {
            details[detail.Run.Id] = detail;
            return Task.FromResult(detail);
        }
    }
}
