using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

internal static class AgentExecutionActivityRuntimeProgressPolicy
{
    public static string ResolveMessage(
        ExecutionState state,
        string phase)
    {
        return state switch
        {
            ExecutionState.Preparing => ResolvePreparingMessage(phase),
            ExecutionState.Running => "The agent is producing a response.",
            ExecutionState.WaitingOnTool
                when string.Equals(
                    phase,
                    "Approval",
                    StringComparison.OrdinalIgnoreCase)
                => "The agent is waiting for tool approval.",
            ExecutionState.WaitingOnTool => "The agent is using a tool.",
            ExecutionState.Persisting => "Persisting the agent result.",
            _ => "The agent runtime is progressing."
        };
    }

    private static string ResolvePreparingMessage(string phase)
    {
        return phase switch
        {
            "Framework" => "Composing the agent runtime.",
            "Provider" => "Preparing the selected provider.",
            "Session" => "Preparing the conversation session.",
            "MCP" or
            "Skills" or
            "Tools" or
            "Workspace tools" or
            "Memory"
                => "Preparing agent capabilities.",
            _ => "Preparing the agent runtime."
        };
    }
}
