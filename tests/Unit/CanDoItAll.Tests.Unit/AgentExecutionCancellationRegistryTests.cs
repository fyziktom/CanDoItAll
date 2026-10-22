using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class AgentExecutionCancellationRegistryTests
{
    [Fact]
    public void Exact_execution_stop_signals_only_the_selected_run_and_reports_disposal() {
        var registry = new AgentExecutionCancellationRegistry();
        var selected = CreateRun("shared-process");
        using var target = registry.Register(selected, default);
        using var sibling = registry.Register(CreateRun("shared-process"), default);
        Assert.True(registry.RequestCancellation(selected.Id));
        Assert.True(registry.RequestCancellation(selected.Id));
        Assert.True(target.Token.IsCancellationRequested);
        Assert.False(sibling.Token.IsCancellationRequested);
        target.Dispose();
        Assert.False(registry.RequestCancellation(selected.Id));
        Assert.False(registry.RequestCancellation(Guid.NewGuid()));
        Assert.Throws<ArgumentException>(() => registry.RequestCancellation(Guid.Empty));
    }

    [Fact]
    public void RequestCancellationByProcessRunIds_cancels_only_matching_active_execution()
    {
        var registry = new AgentExecutionCancellationRegistry();
        using var matching = registry.Register(CreateRun("process-run-001"), CancellationToken.None);
        using var unrelated = registry.Register(CreateRun("process-run-002"), CancellationToken.None);

        var cancelledCount = registry.RequestCancellationByProcessRunIds(
            ["process-run-001"],
            "unit-test",
            "Stop matching process run.");

        Assert.Equal(1, cancelledCount);
        Assert.True(matching.Token.IsCancellationRequested);
        Assert.False(unrelated.Token.IsCancellationRequested);
    }

    [Fact]
    public void RequestCancellationByProcessRunIds_ignores_disposed_registration()
    {
        var registry = new AgentExecutionCancellationRegistry();
        var registration = registry.Register(CreateRun("process-run-001"), CancellationToken.None);
        registration.Dispose();

        var cancelledCount = registry.RequestCancellationByProcessRunIds(
            ["process-run-001"],
            "unit-test",
            "Disposed registration should no longer be active.");

        Assert.Equal(0, cancelledCount);
    }

    private static ExecutionRunRecord CreateRun(string processRunId)
    {
        var now = DateTimeOffset.UtcNow;
        return new ExecutionRunRecord(
            Id: Guid.NewGuid(),
            AgentId: Guid.NewGuid(),
            ChatSessionId: null,
            Title: "Process step",
            SourceKind: "process-step",
            SourceId: "step-001",
            CorrelationId: processRunId,
            CausationId: Guid.NewGuid().ToString("D"),
            RequestedBy: "process-runtime",
            RequestedByKind: "system",
            MetadataJson: "{}",
            InputSummary: "Run process step.",
            ResultSummary: string.Empty,
            ProviderName: "OpenAI",
            Model: "gpt-5",
            State: ExecutionState.Running,
            Outcome: null,
            CreatedAtUtc: now,
            UpdatedAtUtc: now,
            StartedAtUtc: now,
            CompletedAtUtc: null,
            RuntimeSessionKey: "runtime-session-key",
            SerializedSessionStateJson: null,
            PendingApprovals: [],
            ProcessRunId: processRunId,
            ProcessStepId: Guid.NewGuid().ToString("D"));
    }
}
