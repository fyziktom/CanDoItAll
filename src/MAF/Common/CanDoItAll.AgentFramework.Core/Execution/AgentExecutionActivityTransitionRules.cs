using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

internal static class AgentExecutionActivityTransitionRules
{
    public static void EnsureProgressTransition(
        AgentExecutionActivityPhase current,
        AgentExecutionActivityPhase next)
    {
        if (!IsProgressTransitionAllowed(current, next))
        {
            throw new InvalidOperationException(
                $"Agent execution activity cannot transition from '{current}' to '{next}'.");
        }
    }

    public static void EnsureTerminalTransition(
        AgentExecutionActivityPhase current,
        AgentExecutionActivityTerminalOutcome outcome)
    {
        var allowed = outcome switch
        {
            AgentExecutionActivityTerminalOutcome.Succeeded =>
                current == AgentExecutionActivityPhase.PersistingResult,
            AgentExecutionActivityTerminalOutcome.Suspended =>
                current is AgentExecutionActivityPhase.AwaitingApproval or
                    AgentExecutionActivityPhase.PersistingResult,
            AgentExecutionActivityTerminalOutcome.Failed or
                AgentExecutionActivityTerminalOutcome.Cancelled =>
                IsProgressPhase(current),
            _ => false
        };

        if (!allowed)
        {
            throw new InvalidOperationException(
                $"Agent execution activity at phase '{current}' cannot end as '{outcome}'.");
        }
    }

    private static bool IsProgressTransitionAllowed(
        AgentExecutionActivityPhase current,
        AgentExecutionActivityPhase next)
    {
        if (!IsProgressPhase(current) ||
            !IsReportableProgressPhase(next))
        {
            return false;
        }

        if (current == next)
        {
            return true;
        }

        if (current is AgentExecutionActivityPhase.Streaming or
                AgentExecutionActivityPhase.UsingTool &&
            next is AgentExecutionActivityPhase.Streaming or
                AgentExecutionActivityPhase.UsingTool or
                AgentExecutionActivityPhase.WaitingForProvider or
                AgentExecutionActivityPhase.PreparingRuntime)
        {
            return true;
        }

        if (current == AgentExecutionActivityPhase.WaitingForProvider &&
            next == AgentExecutionActivityPhase.PreparingRuntime)
        {
            return true;
        }

        if (current == AgentExecutionActivityPhase.CreatingExecution &&
            next == AgentExecutionActivityPhase.ResolvingPreparation)
        {
            return true;
        }

        if (current == AgentExecutionActivityPhase.PersistingResult &&
            next is AgentExecutionActivityPhase.AwaitingApproval or
                AgentExecutionActivityPhase.PreparingRuntime)
        {
            return true;
        }

        if (current == AgentExecutionActivityPhase.AwaitingApproval &&
            next == AgentExecutionActivityPhase.PreparingRuntime)
        {
            return true;
        }

        return GetProgressStage(next) > GetProgressStage(current);
    }

    private static bool IsProgressPhase(AgentExecutionActivityPhase phase)
    {
        return phase is not AgentExecutionActivityPhase.Completed and
            not AgentExecutionActivityPhase.Failed and
            not AgentExecutionActivityPhase.Cancelled;
    }

    private static bool IsReportableProgressPhase(
        AgentExecutionActivityPhase phase)
    {
        return phase is not AgentExecutionActivityPhase.Accepted and
            not AgentExecutionActivityPhase.Completed and
            not AgentExecutionActivityPhase.Failed and
            not AgentExecutionActivityPhase.Cancelled;
    }

    private static int GetProgressStage(AgentExecutionActivityPhase phase)
    {
        return phase switch
        {
            AgentExecutionActivityPhase.Accepted => 0,
            AgentExecutionActivityPhase.CapturingContext => 1,
            AgentExecutionActivityPhase.ResolvingSession => 2,
            AgentExecutionActivityPhase.PreparingInput => 3,
            AgentExecutionActivityPhase.ResolvingPreparation => 4,
            AgentExecutionActivityPhase.ResolvingProvider => 5,
            AgentExecutionActivityPhase.CreatingExecution => 6,
            AgentExecutionActivityPhase.PreparingCapabilities => 7,
            AgentExecutionActivityPhase.PreparingRuntime => 8,
            AgentExecutionActivityPhase.WaitingForProvider => 9,
            AgentExecutionActivityPhase.Streaming or
                AgentExecutionActivityPhase.UsingTool => 10,
            AgentExecutionActivityPhase.AwaitingApproval => 11,
            AgentExecutionActivityPhase.PersistingResult => 12,
            _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, null)
        };
    }
}
