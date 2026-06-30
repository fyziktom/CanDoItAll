using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public interface IAgentExecutionRecoveryObserver
{
    Task OnExecutionRecoveredAsync(
        AgentExecutionRecoveryObservation observation,
        CancellationToken cancellationToken = default);
}

public sealed record AgentExecutionRecoveryObservation(
    Guid ExecutionRunId,
    string SourceKind,
    string ProcessRunId,
    string ProcessStepId,
    ExecutionState State,
    RunOutcome? Outcome,
    string ResultSummary,
    DateTimeOffset RecoveredAtUtc);
