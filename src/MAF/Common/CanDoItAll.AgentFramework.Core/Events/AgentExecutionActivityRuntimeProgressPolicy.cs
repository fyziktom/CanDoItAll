using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

internal static class AgentExecutionActivityRuntimeProgressPolicy
{
    private const string ApprovalPhase = "Approval";

    public static AgentExecutionActivityPhase? ResolvePhase(
        ExecutionState state,
        string phase)
    {
        return state switch
        {
            ExecutionState.Preparing => AgentExecutionActivityPhase.PreparingRuntime,
            ExecutionState.Running => AgentExecutionActivityPhase.Streaming,
            ExecutionState.WaitingOnTool when IsApprovalPhase(phase)
                => AgentExecutionActivityPhase.AwaitingApproval,
            ExecutionState.WaitingOnTool => AgentExecutionActivityPhase.UsingTool,
            ExecutionState.Persisting => AgentExecutionActivityPhase.PersistingResult,
            _ => null
        };
    }

    public static string ResolveMessage(
        ExecutionState state,
        string phase)
    {
        return state switch
        {
            ExecutionState.Preparing => ResolvePreparingMessage(phase),
            ExecutionState.Running => "The agent is producing a response.",
            ExecutionState.WaitingOnTool
                when IsApprovalPhase(phase)
                => "The agent is waiting for tool approval.",
            ExecutionState.WaitingOnTool => "The agent is using a tool.",
            ExecutionState.Persisting => "Persisting the agent result.",
            _ => "The agent runtime is progressing."
        };
    }

    private static bool IsApprovalPhase(string phase)
        => string.Equals(
            phase,
            ApprovalPhase,
            StringComparison.OrdinalIgnoreCase);

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
