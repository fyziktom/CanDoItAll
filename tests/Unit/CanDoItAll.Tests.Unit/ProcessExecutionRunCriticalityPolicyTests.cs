using CanDoItAll.AgentFramework.Core.Execution;
using CanDoItAll.Modules.Processes;

namespace CanDoItAll.Tests.Unit.Processes;

/// <summary>
/// Direct unit coverage for <see cref="ProcessExecutionRunCriticalityPolicy"/> (SB13): the three-clause OR logic
/// ported verbatim from the deleted <c>AgentFrameworkWorkspaceExecutionService.IsGovernedMachineCriticalRun</c>
/// static helper.
/// </summary>
public sealed class ProcessExecutionRunCriticalityPolicyTests
{
    [Fact]
    public void IsMachineCritical_is_true_for_the_process_step_source_kind()
    {
        var policy = new ProcessExecutionRunCriticalityPolicy();

        Assert.True(policy.IsMachineCritical(new AgentExecutionRunCriticalitySnapshot("process-step", null, null)));
        Assert.True(policy.IsMachineCritical(new AgentExecutionRunCriticalitySnapshot("Process-Step", null, null)));
    }

    [Fact]
    public void IsMachineCritical_is_true_when_a_process_run_id_is_present_even_with_an_unrelated_source_kind()
    {
        var policy = new ProcessExecutionRunCriticalityPolicy();

        Assert.True(policy.IsMachineCritical(new AgentExecutionRunCriticalitySnapshot("manual", "run-001", null)));
    }

    [Fact]
    public void IsMachineCritical_is_true_when_a_process_step_id_is_present_even_with_an_unrelated_source_kind()
    {
        var policy = new ProcessExecutionRunCriticalityPolicy();

        Assert.True(policy.IsMachineCritical(new AgentExecutionRunCriticalitySnapshot("manual", null, "step-001")));
    }

    [Fact]
    public void IsMachineCritical_is_false_when_none_of_the_three_signals_are_present()
    {
        var policy = new ProcessExecutionRunCriticalityPolicy();

        Assert.False(policy.IsMachineCritical(new AgentExecutionRunCriticalitySnapshot("manual", null, null)));
        Assert.False(policy.IsMachineCritical(new AgentExecutionRunCriticalitySnapshot("manual", "", "  ")));
    }
}
