namespace CanDoItAll.AgentFramework.Models;

public enum AgentToolInvocationOutcome
{
    Unknown = 0,
    Succeeded,
    Failed,
    Cancelled
}

public enum AgentToolEffectState
{
    Unknown = 0,
    None,
    NotCommitted,
    Committed
}

public interface IAgentToolInvocationResultEvidence
{
    AgentToolInvocationOutcome Outcome { get; }

    AgentToolEffectState EffectState { get; }

    string FailureCode { get; }

    string SafeMessage { get; }

    bool CanRetryWithCorrectedInput { get; }
}