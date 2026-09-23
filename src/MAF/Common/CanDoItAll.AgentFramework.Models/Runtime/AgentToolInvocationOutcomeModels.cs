namespace CanDoItAll.AgentFramework.Models;

/// <summary>
/// Outcome of one tool call, as a JSON integer: 0 Unknown (not recorded), 1 Succeeded, 2 Failed, 3 Cancelled.
/// </summary>
public enum AgentToolInvocationOutcome
{
    Unknown = 0,
    Succeeded,
    Failed,
    Cancelled
}

/// <summary>
/// What is known about the effect of one tool call, as a JSON integer: 0 Unknown (not confirmed or not recorded; the
/// call may have changed something), 1 None (the call changes nothing, for example a read), 2 NotCommitted (no change
/// was made), 3 Committed (the change was made).
/// </summary>
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