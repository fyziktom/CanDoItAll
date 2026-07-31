namespace CanDoItAll.AgentFramework.Core;

public interface IAgentToolFailure
{
    string ErrorCode { get; }

    string SafeMessage { get; }

    bool IsSafeToExpose { get; }

    bool CanRetryWithCorrectedInput { get; }
}

public sealed record AgentToolFailureResult(
    bool Succeeded,
    string ErrorCode,
    string Message,
    bool CanRetryWithCorrectedInput);
