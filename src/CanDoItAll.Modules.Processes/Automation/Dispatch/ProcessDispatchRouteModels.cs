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

internal sealed record ProcessRouteExecutionRunSnapshot(
    Guid Id);

internal sealed record ProcessRouteRunSnapshot(
    Guid Id,
    ProcessRunStatus Status,
    ProcessOperatingMode OperatingMode,
    Guid ProcessDefinitionVersionId);

internal sealed record ProcessRouteStepSnapshot(
    Guid Id,
    Guid StepDefinitionId,
    string Title,
    ProcessStepRunStatus Status,
    ProcessStepKind StepKind,
    Guid ConcurrencyToken,
    DateTimeOffset? StartedAtUtc);

internal sealed record ProcessRouteArtifactInput(
    string SourceStepTitle,
    string ExpectedArtifactTitle,
    Guid ArtifactExpectationId,
    Guid SourceStepDefinitionId,
    Guid? SourceStepRunId,
    Guid? SourceStepRunConcurrencyToken,
    ProcessStepRunStatus? SourceStepRunStatus,
    bool SourceStepHasAgentExecutor,
    IReadOnlyList<ProcessRouteArtifactReference> Artifacts);

internal sealed record ProcessRouteArtifactReference(
    string Title,
    string ArtifactKind,
    string ManagedStoragePath,
    string ReviewSummary,
    string ProvenanceSummary);

internal sealed record ProcessRouteCandidate(
    ProcessRouteRunSnapshot Run,
    ProcessRouteStepSnapshot StepRun,
    Guid TechnicalAgentId,
    Guid? RecoveryExecutionRunId,
    IReadOnlyList<ProcessRouteArtifactInput> ArtifactInputs,
    IProcessRouteCandidateSource Source);

internal sealed record ProcessRouteDispatchClaim(
    Guid StepRunId,
    string ClaimToken,
    IProcessRouteDispatchClaimSource Source);

internal sealed record ProcessRouteExecutionOutcome(
    ProcessRouteExecutionRunSnapshot ExecutionRun,
    string ResponseText,
    ProcessStepRunStatus CompletionStatus,
    string CompletionReason,
    IReadOnlyList<string> MissingRequiredTools,
    int AttemptNumber,
    Guid? SelectedBranchOutcomeId,
    IProcessRouteExecutionOutcomeSource Source);
