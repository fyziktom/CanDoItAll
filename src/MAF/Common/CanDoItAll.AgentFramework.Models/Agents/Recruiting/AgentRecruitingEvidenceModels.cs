using System.Text.Json.Serialization;

namespace CanDoItAll.AgentFramework.Models;

[JsonConverter(typeof(JsonStringEnumConverter<AgentRecruitingTargetKind>))]
public enum AgentRecruitingTargetKind
{
    [JsonStringEnumMemberName("agent-execution-run")]
    AgentExecutionRun,

    [JsonStringEnumMemberName("workflow-run")]
    WorkflowRun,

    [JsonStringEnumMemberName("process-run")]
    ProcessRun
}

[JsonConverter(typeof(JsonStringEnumConverter<AgentRecruitingAutomatedDecision>))]
public enum AgentRecruitingAutomatedDecision
{
    Passed,
    Failed,
    NeedsHumanReview
}

[JsonConverter(typeof(JsonStringEnumConverter<AgentRecruitingHumanDecision>))]
public enum AgentRecruitingHumanDecision
{
    Approved,
    Rejected
}

[JsonConverter(typeof(JsonStringEnumConverter<AgentRecruitingEvidenceCompleteness>))]
public enum AgentRecruitingEvidenceCompleteness
{
    Complete,
    Incomplete
}

[JsonConverter(typeof(JsonStringEnumConverter<AgentRecruitingReadinessStatus>))]
public enum AgentRecruitingReadinessStatus
{
    Ready,
    NoInterviews,
    IncompleteEvidence,
    AwaitingHumanApproval,
    Rejected
}

public sealed record AgentRecruitingExecutionTarget(
    AgentRecruitingTargetKind Kind,
    Guid Id);

public sealed record AgentRecruitingAutomatedEvaluation(
    AgentRecruitingAutomatedDecision Decision,
    decimal? Score,
    Guid? EvaluatorAgentId,
    Guid? ProviderProfileId,
    string Model,
    string RubricVersion,
    IReadOnlyList<string> Findings,
    DateTimeOffset EvaluatedAtUtc);

public sealed record AgentRecruitingAttempt(
    Guid Id,
    Guid InterviewId,
    int Sequence,
    AgentRecruitingExecutionTarget Target,
    string ChallengeKey,
    string ChallengeVersion,
    string RubricVersion,
    string InputHash,
    string OutputHash,
    string StructuredOutputContractKey,
    string StructuredOutputSchemaHash,
    string StructuredOutputValidationStatus,
    AgentRecruitingAutomatedEvaluation? AutomatedEvaluation,
    AgentRecruitingEvidenceCompleteness Completeness,
    IReadOnlyList<string> MissingEvidence,
    DateTimeOffset CreatedAtUtc);

public sealed record AgentRecruitingHumanReview(
    Guid Id,
    Guid InterviewId,
    Guid AttemptId,
    AgentRecruitingHumanDecision Decision,
    string ReviewerActorId,
    string ReviewerDisplayName,
    string AuthorizationReference,
    string AuthorizationEvidenceHash,
    string Notes,
    bool QualifiesForReadiness,
    IReadOnlyList<string> MissingEvidence,
    DateTimeOffset ReviewedAtUtc);

public sealed record AgentRecruitingInterview(
    Guid Id,
    Guid CandidateAgentId,
    string CandidateConfigurationVersion,
    string CandidateNameSnapshot,
    string CandidateModelSnapshot,
    string Purpose,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<AgentRecruitingAttempt> Attempts,
    IReadOnlyList<AgentRecruitingHumanReview> Reviews);

public sealed record AgentRecruitingAttemptComparison(
    Guid AttemptId,
    int Sequence,
    DateTimeOffset CreatedAtUtc,
    AgentRecruitingEvidenceCompleteness Completeness,
    AgentRecruitingAutomatedDecision? AutomatedDecision,
    decimal? Score,
    AgentRecruitingHumanDecision? HumanDecision);

public sealed record AgentRecruitingCandidateReadiness(
    Guid CandidateAgentId,
    string CurrentConfigurationVersion,
    AgentRecruitingReadinessStatus Status,
    bool ReadyForProduction,
    bool ActivatesAgent,
    bool RequiresSeparateActivationAuthorization,
    Guid? QualifyingInterviewId,
    Guid? QualifyingAttemptId,
    Guid? QualifyingReviewId,
    string HumanAuthorizationReference,
    string HumanAuthorizationEvidenceHash,
    IReadOnlyList<string> Reasons,
    IReadOnlyList<AgentRecruitingAttemptComparison> AttemptHistory);

public sealed record AgentRecruitingTargetResolution(
    bool Found,
    string State,
    bool IsTerminal,
    Guid? ExecutedAgentId = null);

public sealed record CreateAgentRecruitingInterviewCommand(
    Guid CandidateAgentId,
    string CandidateConfigurationVersion,
    string Purpose);

public sealed record AppendAgentRecruitingAttemptCommand(
    AgentRecruitingExecutionTarget Target,
    string ChallengeKey,
    string ChallengeVersion,
    string RubricVersion,
    string InputHash,
    string OutputHash,
    string StructuredOutputContractKey,
    string StructuredOutputSchemaHash,
    string StructuredOutputValidationStatus,
    AgentRecruitingAutomatedEvaluation? AutomatedEvaluation);

public sealed record AppendAgentRecruitingReviewCommand(
    Guid AttemptId,
    AgentRecruitingHumanDecision Decision,
    string ReviewerActorId,
    string ReviewerDisplayName,
    string AuthorizationReference,
    string AuthorizationEvidenceHash,
    string Notes);
