using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit;

public sealed class AgentExecutionActivityRuntimeProgressPolicyTests
{
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
