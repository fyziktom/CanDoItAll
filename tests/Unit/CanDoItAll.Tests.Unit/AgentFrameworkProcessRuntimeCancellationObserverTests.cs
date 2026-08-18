using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class AgentFrameworkProcessRuntimeCancellationObserverTests
{
    [Fact]
    public async Task OnRunsCancelled_signals_active_execution_and_marks_execution_record_cancelled()
    {
        var processRunId = ProcessRunId.New();
        var run = CreateRun(processRunId);
        var store = new RecordingExecutionRunStore(new ExecutionRunDetail(run, null, [], []));
        var registry = new AgentExecutionCancellationRegistry();
        var processLeaseCleaner = new RecordingProcessLeaseCleaner();
        using var registration = registry.Register(run, CancellationToken.None);
        var observer = new AgentFrameworkProcessRuntimeCancellationObserver(
            registry,
            store,
            processLeaseCleaner,
            NullLogger<AgentFrameworkProcessRuntimeCancellationObserver>.Instance);

        var result = await observer.OnRunsCancelledAsync(new ProcessRuntimeRunCancellationObservation(
            processRunId,
            [processRunId],
            "unit-test",
            "Stop process run.",
            DateTimeOffset.UtcNow));

        var saved = await store.GetExecutionRunDetailAsync(run.Id);
        Assert.NotNull(saved);
        Assert.True(registration.Token.IsCancellationRequested);
        Assert.Equal(ExecutionState.Failed, saved.Run.State);
        Assert.Equal(RunOutcome.Cancelled, saved.Run.Outcome);
        Assert.Equal(string.Empty, saved.Run.RuntimeSessionKey);
        Assert.Null(saved.Run.SerializedSessionStateJson);
        Assert.Empty(saved.Run.PendingApprovals);
        Assert.Equal([run.Id], processLeaseCleaner.CleanedExecutionRunIds);
        Assert.Contains(saved.ExecutionLog, entry => entry.Phase == "process-cancellation");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Contains("Signaled cancellation", StringComparison.Ordinal));
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Contains("Marked 1 AgentFramework execution run", StringComparison.Ordinal));
    }

    [Fact]
    public async Task OnRunsCancelled_preserves_terminal_cancellation_when_process_cleanup_throws()
    {
        var processRunId = ProcessRunId.New();
        var run = CreateRun(processRunId);
        var store = new RecordingExecutionRunStore(new ExecutionRunDetail(run, null, [], []));
        var registry = new AgentExecutionCancellationRegistry();
        var processLeaseCleaner = new RecordingProcessLeaseCleaner(throwOnCleanup: true);
        var observer = new AgentFrameworkProcessRuntimeCancellationObserver(
            registry,
            store,
            processLeaseCleaner,
            NullLogger<AgentFrameworkProcessRuntimeCancellationObserver>.Instance);

        var result = await observer.OnRunsCancelledAsync(new ProcessRuntimeRunCancellationObservation(
            processRunId,
            [processRunId],
            "unit-test",
            "Stop process run.",
            DateTimeOffset.UtcNow));

        var saved = await store.GetExecutionRunDetailAsync(run.Id);
        Assert.NotNull(saved);
        Assert.Equal(ExecutionState.Failed, saved.Run.State);
        Assert.Equal(RunOutcome.Cancelled, saved.Run.Outcome);
        Assert.Equal([run.Id], processLeaseCleaner.CleanedExecutionRunIds);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Contains(
                "Marked 1 AgentFramework execution run",
                StringComparison.Ordinal));
    }

    private static ExecutionRunRecord CreateRun(ProcessRunId processRunId)
    {
        var now = DateTimeOffset.UtcNow;
        return new ExecutionRunRecord(
            Id: Guid.NewGuid(),
            AgentId: Guid.NewGuid(),
            ChatSessionId: null,
            Title: "Process step",
            SourceKind: "process-step",
            SourceId: "feature-intake",
            CorrelationId: processRunId.Value.ToString("D"),
            CausationId: Guid.NewGuid().ToString("D"),
            RequestedBy: "process-runtime",
            RequestedByKind: "system",
            MetadataJson: "{}",
            InputSummary: "Run process step.",
            ResultSummary: string.Empty,
            ProviderName: "OpenAI",
            Model: "gpt-5",
            State: ExecutionState.WaitingOnTool,
            Outcome: null,
            CreatedAtUtc: now,
            UpdatedAtUtc: now,
            StartedAtUtc: now,
            CompletedAtUtc: null,
            RuntimeSessionKey: "runtime-session-key",
            SerializedSessionStateJson: """{"kind":"partial"}""",
            PendingApprovals: [],
            ProcessRunId: processRunId.Value.ToString("D"),
            ProcessStepId: Guid.NewGuid().ToString("D"));
    }

    private sealed class RecordingProcessLeaseCleaner(bool throwOnCleanup = false)
        : IWorkspaceExecutionRunProcessLeaseCleaner
    {
        public List<Guid> CleanedExecutionRunIds { get; } = [];

        public Task<WorkspaceExecutionRunProcessCleanupResult> CleanupAsync(Guid executionRunId)
        {
            CleanedExecutionRunIds.Add(executionRunId);
            if (throwOnCleanup)
            {
                throw new InvalidOperationException("simulated cleanup failure");
            }

            return Task.FromResult(
                WorkspaceExecutionRunProcessCleanupResult.Empty(executionRunId));
        }
    }

    private sealed class RecordingExecutionRunStore(ExecutionRunDetail detail) : ISandboxWorkspaceExecutionRunStore
    {
        private readonly Dictionary<Guid, ExecutionRunDetail> details = new()
        {
            [detail.Run.Id] = detail
        };

        public Task<IReadOnlyList<ExecutionRunRecord>> ListExecutionRunsAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ExecutionRunRecord>>(
                details.Values.Select(item => item.Run).ToArray());
        }

        public Task<ExecutionRunRecord?> GetExecutionRunAsync(
            Guid executionRunId,
            CancellationToken cancellationToken = default)
        {
            details.TryGetValue(executionRunId, out var saved);
            return Task.FromResult(saved?.Run);
        }

        public Task<ExecutionRunDetail?> GetExecutionRunDetailAsync(
            Guid executionRunId,
            CancellationToken cancellationToken = default)
        {
            details.TryGetValue(executionRunId, out var saved);
            return Task.FromResult(saved);
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
