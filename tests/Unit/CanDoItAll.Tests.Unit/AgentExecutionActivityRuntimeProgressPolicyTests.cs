using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class AgentExecutionActivityRuntimeProgressPolicyTests
{
    [Theory]
    [InlineData(
        ExecutionState.Preparing,
        "Framework",
        AgentExecutionActivityPhase.PreparingRuntime)]
    [InlineData(
        ExecutionState.Running,
        "Streaming",
        AgentExecutionActivityPhase.Streaming)]
    [InlineData(
        ExecutionState.WaitingOnTool,
        "Tool",
        AgentExecutionActivityPhase.UsingTool)]
    [InlineData(
        ExecutionState.WaitingOnTool,
        "Approval",
        AgentExecutionActivityPhase.AwaitingApproval)]
    [InlineData(
        ExecutionState.Persisting,
        "Session",
        AgentExecutionActivityPhase.PersistingResult)]
    public void Runtime_progress_maps_tool_use_and_approval_to_distinct_activity_phases(
        ExecutionState state,
        string phase,
        AgentExecutionActivityPhase expectedPhase)
    {
        var activityPhase =
            AgentExecutionActivityRuntimeProgressPolicy.ResolvePhase(
                state,
                phase);

        Assert.Equal(expectedPhase, activityPhase);
    }

    [Theory]
    [InlineData(
        ExecutionState.Preparing,
        "Framework",
        "Composing the agent runtime.")]
    [InlineData(
        ExecutionState.Preparing,
        "MCP",
        "Preparing agent capabilities.")]
    [InlineData(
        ExecutionState.Running,
        "Streaming",
        "The agent is producing a response.")]
    [InlineData(
        ExecutionState.WaitingOnTool,
        "Tool",
        "The agent is using a tool.")]
    [InlineData(
        ExecutionState.WaitingOnTool,
        "Approval",
        "The agent is waiting for tool approval.")]
    [InlineData(
        ExecutionState.Persisting,
        "Session",
        "Persisting the agent result.")]
    public void Public_runtime_activity_uses_bounded_policy_text(
        ExecutionState state,
        string phase,
        string expectedMessage)
    {
        var message = AgentExecutionActivityRuntimeProgressPolicy.ResolveMessage(
            state,
            phase);

        Assert.Equal(expectedMessage, message);
    }

    [Fact]
    public void Public_runtime_activity_never_forwards_tool_arguments()
    {
        const string secretBearingRuntimeMessage =
            "Invoking tool 'workspace_write_file' with path=\"C:\\secret\\token.txt\", apiKey=\"sk-private\".";

        var message = AgentExecutionActivityRuntimeProgressPolicy.ResolveMessage(
            ExecutionState.WaitingOnTool,
            secretBearingRuntimeMessage);

        Assert.Equal("The agent is using a tool.", message);
        Assert.DoesNotContain("secret", message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("apiKey", message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sk-private", message, StringComparison.Ordinal);
    }
}
