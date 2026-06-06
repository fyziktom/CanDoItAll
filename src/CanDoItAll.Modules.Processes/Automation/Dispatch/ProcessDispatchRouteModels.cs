using CanDoItAll.Processes.Contracts;

namespace CanDoItAll.Modules.Processes;

internal interface IProcessRouteCandidateSource
{
}

internal interface IProcessRouteDispatchClaimSource
{
}

internal interface IProcessRouteExecutionOutcomeSource
{
}

internal sealed record ProcessRouteRunSnapshot(
    Guid Id,
    ProcessRunStatus Status);

internal sealed record ProcessRouteStepSnapshot(
    Guid Id,
    Guid StepDefinitionId,
    ProcessStepRunStatus Status,
    ProcessStepKind StepKind,
    Guid ConcurrencyToken,
    DateTimeOffset? StartedAtUtc);

internal sealed record ProcessRouteCandidate(
    ProcessRouteRunSnapshot Run,
    ProcessRouteStepSnapshot StepRun,
    Guid TechnicalAgentId,
    Guid? RecoveryExecutionRunId,
    IProcessRouteCandidateSource Source);

internal sealed record ProcessRouteDispatchClaim(
    Guid StepRunId,
    string ClaimToken,
    IProcessRouteDispatchClaimSource Source);

internal sealed record ProcessRouteExecutionOutcome(
    ProcessAutomationExecutionRunDetail Detail,
    string ResponseText,
    ProcessStepRunStatus CompletionStatus,
    string CompletionReason,
    IReadOnlyList<string> MissingRequiredTools,
    int AttemptNumber,
    Guid? SelectedBranchOutcomeId,
    IProcessRouteExecutionOutcomeSource Source);
