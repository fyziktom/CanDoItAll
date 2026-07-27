using System.Text.Json.Serialization;

namespace CanDoItAll.AgentFramework.Models;

public static class AgentRecruitingAuthorizationScopes
{
    public const string HumanReview = "agent-recruiting.review";
}

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

[JsonConverter(typeof(JsonStringEnumConverter<AgentRecruitingAssessmentClassification>))]
public enum AgentRecruitingAssessmentClassification
{
    StrongFit = 1,
    Suitable = 2,
    NeedsTraining = 3,
    NotSuitable = 4,
    Inconclusive = 5
}

[JsonConverter(typeof(JsonStringEnumConverter<AgentRecruitingProposedNextStep>))]
public enum AgentRecruitingProposedNextStep
{
    Advance = 1,
    RequestHumanReview = 2,
    AssignTraining = 3,
    Reassess = 4,
    Hold = 5,
    Reject = 6
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

public sealed record AgentRecruitingAssessmentAnalysis(
    AgentRecruitingAssessmentClassification Classification,
    decimal Confidence,
    string Summary,
    AgentRecruitingProposedNextStep ProposedNextStep,
    IReadOnlyList<string> Strengths,
    IReadOnlyList<string> Gaps);

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
    DateTimeOffset CreatedAtUtc,
    AgentRecruitingAssessmentAnalysis? Analysis = null);

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
    IReadOnlyList<AgentRecruitingHumanReview> Reviews,
    Guid? RecruitmentApplicationId = null,
    Guid? ProjectId = null);

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
    IReadOnlyList<Guid>? ParticipatingAgentIds = null);

public sealed record CreateAgentRecruitingInterviewCommand(
    Guid CandidateAgentId,
    string CandidateConfigurationVersion,
    string Purpose,
    Guid? RecruitmentApplicationId = null,
    Guid? ProjectId = null);

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
    AgentRecruitingAutomatedEvaluation? AutomatedEvaluation,
    AgentRecruitingAssessmentAnalysis? Analysis = null);

public sealed record AppendAgentRecruitingReviewCommand(
    Guid AttemptId,
    AgentRecruitingHumanDecision Decision,
    string ReviewerActorId,
    string ReviewerDisplayName,
    string AuthorizationReference,
    string AuthorizationEvidenceHash,
    string Notes);
