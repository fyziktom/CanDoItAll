using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Processes;

namespace CanDoItAll.Tests.Integration;

public sealed class ProcessExecutionRunDisplayProjectorTests
{
    [Fact]
    public void Resolve_uses_governed_process_status_for_latest_mismatched_attempt()
    {
        var run = CreateExecutionRun(ExecutionState.Completed, RunOutcome.Succeeded);
        var stepRun = CreateStepRun(ProcessStepRunStatus.Failed);

        var projection = ProcessExecutionRunDisplayProjector.Resolve(run, stepRun, isLatestRunForStep: true);

        Assert.Equal("Process failed", projection.StatusBadgeText);
        Assert.Equal("danger", projection.StatusTone);
        Assert.Equal("Raw Completed / Succeeded", projection.RawStatusBadgeText);
        Assert.Contains("process evaluation failed", projection.StatusDetail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Resolve_keeps_raw_agentframework_status_for_older_attempts()
    {
        var run = CreateExecutionRun(ExecutionState.Completed, RunOutcome.Succeeded);
        var stepRun = CreateStepRun(ProcessStepRunStatus.Failed);

        var projection = ProcessExecutionRunDisplayProjector.Resolve(run, stepRun, isLatestRunForStep: false);

        Assert.Equal("Completed / Succeeded", projection.StatusBadgeText);
        Assert.Equal("mint", projection.StatusTone);
        Assert.Equal(string.Empty, projection.RawStatusBadgeText);
        Assert.Equal(string.Empty, projection.StatusDetail);
    }

    [Fact]
    public void Resolve_keeps_raw_agentframework_status_when_governed_and_raw_states_match()
    {
        var run = CreateExecutionRun(ExecutionState.Completed, RunOutcome.Succeeded);
        var stepRun = CreateStepRun(ProcessStepRunStatus.Completed);

        var projection = ProcessExecutionRunDisplayProjector.Resolve(run, stepRun, isLatestRunForStep: true);

        Assert.Equal("Completed / Succeeded", projection.StatusBadgeText);
        Assert.Equal("mint", projection.StatusTone);
        Assert.Equal(string.Empty, projection.RawStatusBadgeText);
        Assert.Equal(string.Empty, projection.StatusDetail);
    }

    private static ExecutionRunRecord CreateExecutionRun(ExecutionState state, RunOutcome? outcome)
    {
        var now = DateTimeOffset.UtcNow;
        return new ExecutionRunRecord(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            "Units converter implementation",
            "process-step",
            "step-1",
            "corr-1",
            "cause-1",
            "process-automation-dispatch",
            "system",
            "{}",
            "Prompt",
            "Result",
            "OpenAI chat completions",
            "gpt-5.4",
            state,
            outcome,
            now,
            now,
            now,
            state is ExecutionState.Completed or ExecutionState.Failed ? now : null,
            string.Empty,
            null,
            []);
    }

    private static ProcessStepRunViewModel CreateStepRun(ProcessStepRunStatus status)
    {
        return new ProcessStepRunViewModel(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            0,
            "Implement the units converter",
            ProcessStepKind.Work,
            status,
            "Programming Workspace Analyst",
            string.Empty,
            string.Empty,
            string.Empty,
            null,
            string.Empty,
            0,
            0,
            0,
            0,
            ProcessCapabilityGapSeverity.None,
            []);
    }
}
