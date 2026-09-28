using System.Text.Json.Serialization;

namespace CanDoItAll.AgentFramework.Models;

/// <summary>
/// Bearer token scopes used by agent recruiting evidence.
/// </summary>
public static class AgentRecruitingAuthorizationScopes
{
    /// <summary>
    /// Scope a bearer token must include to append a human review to an agent recruiting interview. The general
    /// <c>api</c> scope does not replace it.
    /// </summary>
    public const string HumanReview = "agent-recruiting.review";
}

/// <summary>
/// Kind of run whose result an agent recruiting attempt records, as a JSON string: <c>agent-execution-run</c> (an
/// agent execution run; the candidate must be the run's agent), <c>workflow-run</c> (a workflow run; the candidate
/// must be the agent of a workflow node that executed in the run) or <c>process-run</c> (a process run; the candidate
/// must be recorded as a participant of the run). Requests may also send the integers 0, 1 and 2.
/// </summary>
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

/// <summary>
/// Outcome of the automated evaluation recorded with an agent recruiting attempt, as a JSON string: <c>Passed</c>
/// (the evaluator accepted the attempt; readiness requires it), <c>Failed</c> (the evaluator rejected the attempt) or
/// <c>NeedsHumanReview</c> (the evaluator left the decision to a human). The caller supplies it; the server does not
/// evaluate attempts. Requests may also send the integers 0, 1 and 2.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<AgentRecruitingAutomatedDecision>))]
public enum AgentRecruitingAutomatedDecision
{
    Passed,
    Failed,
    NeedsHumanReview
}

/// <summary>
/// Decision of a human reviewer on one agent recruiting attempt, as a JSON string: <c>Approved</c> (the reviewer
/// accepts the attempt; it counts for readiness only with an authorization reference and evidence hash) or
/// <c>Rejected</c> (the reviewer rejects the attempt; as the latest review of the latest attempt for the current
/// configuration it makes readiness <c>Rejected</c>). Requests may also send the integers 0 and 1.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<AgentRecruitingHumanDecision>))]
public enum AgentRecruitingHumanDecision
{
    Approved,
    Rejected
}

/// <summary>
/// Whether an agent recruiting attempt carries all evidence that readiness requires, as a JSON string:
/// <c>Complete</c> (nothing is missing) or <c>Incomplete</c> (the attempt's <c>missingEvidence</c> lists what is
/// missing). The server derives it when the attempt is appended; it never changes afterwards.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<AgentRecruitingEvidenceCompleteness>))]
public enum AgentRecruitingEvidenceCompleteness
{
    Complete,
    Incomplete
}

/// <summary>
/// Readiness of a candidate agent's current configuration, as a JSON string: <c>Ready</c> (the latest attempt for
/// the current configuration is complete, passed automated evaluation and has a qualifying human approval),
/// <c>NoInterviews</c> (no interview exists for the candidate in scope), <c>IncompleteEvidence</c> (no complete
/// attempt exists for the current configuration, or the latest one is incomplete), <c>AwaitingHumanApproval</c> (the
/// latest attempt is complete but has no qualifying approval or did not pass automated evaluation) or
/// <c>Rejected</c> (the latest review of the latest attempt is a rejection).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<AgentRecruitingReadinessStatus>))]
public enum AgentRecruitingReadinessStatus
{
    Ready,
    NoInterviews,
    IncompleteEvidence,
    AwaitingHumanApproval,
    Rejected
}

/// <summary>
/// Classification of the candidate in an agent recruiting analysis, as a JSON string: <c>StrongFit</c>,
/// <c>Suitable</c>, <c>NeedsTraining</c>, <c>NotSuitable</c> or <c>Inconclusive</c>. It is a label chosen by whoever
/// recorded the analysis; the server stores it for reviewers and does not interpret it. Requests may also send the
/// integers 1 to 5 in that order; other integers are rejected.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<AgentRecruitingAssessmentClassification>))]
public enum AgentRecruitingAssessmentClassification
{
    StrongFit = 1,
    Suitable = 2,
    NeedsTraining = 3,
    NotSuitable = 4,
    Inconclusive = 5
}

/// <summary>
/// Next step that an agent recruiting analysis proposes for the candidate, as a JSON string: <c>Advance</c>,
/// <c>RequestHumanReview</c>, <c>AssignTraining</c>, <c>Reassess</c>, <c>Hold</c> or <c>Reject</c>. It is a proposal
/// recorded for reviewers; the server does not act on it. Requests may also send the integers 1 to 6 in that order;
/// other integers are rejected.
/// </summary>
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

/// <summary>
/// The run whose result an agent recruiting attempt records. The run must exist in the current workspace and show
/// participation of the interview's candidate agent, as described for <c>kind</c>.
/// </summary>
/// <param name="Kind">
/// Kind of run that <c>id</c> identifies, as a JSON string: <c>agent-execution-run</c> (the candidate must be the
/// run's agent), <c>workflow-run</c> (the candidate must be the agent of a workflow node that executed in the run) or
/// <c>process-run</c> (the candidate must be a recorded participant of the run). The integers 0, 1 and 2 are also
/// accepted.
/// </param>
/// <param name="Id">
/// Identifier of the run, matching <c>kind</c>: the <c>executionRunId</c> of an agent execution run from the
/// <c>/api/agents</c> execution operations, a workflow run identifier from <c>/api/workflows</c> or the run
/// identifier of a process run from the process run operations. Must not be the empty GUID.
/// </param>
public sealed record AgentRecruitingExecutionTarget(
    AgentRecruitingTargetKind Kind,
    Guid Id);

/// <summary>
/// Automated evaluation of an agent recruiting attempt and the provenance of the evaluator that produced it. The
/// caller records it with the attempt; the server checks its format and references but does not rerun it. Without an
/// evaluator agent, provider profile, model or evaluation time the attempt is stored as incomplete.
/// </summary>
/// <param name="Decision">
/// Outcome of the evaluation, as a JSON string: <c>Passed</c>, <c>Failed</c> or <c>NeedsHumanReview</c> (the
/// integers 0, 1 and 2 are also accepted). Only <c>Passed</c> can make the candidate ready.
/// </param>
/// <param name="Score">
/// Score from 0 through 100 inclusive, or null when the evaluator produced none; other values are rejected with
/// <c>agent-recruiting.score-invalid</c>. It does not affect readiness.
/// </param>
/// <param name="EvaluatorAgentId">
/// Identifier of the agent that performed the evaluation. When set it must exist in the current workspace
/// (<c>agent-recruiting.evaluator-agent-not-found</c>); null leaves the attempt incomplete
/// (<c>evaluator-agent-id</c>).
/// </param>
/// <param name="ProviderProfileId">
/// Identifier of the workspace provider profile the evaluator used. When set it must exist in the current workspace
/// (<c>agent-recruiting.evaluator-provider-not-found</c>); null leaves the attempt incomplete
/// (<c>evaluator-provider-profile-id</c>).
/// </param>
/// <param name="Model">
/// Model the evaluator used, at most 200 characters, stored trimmed. Empty leaves the attempt incomplete
/// (<c>evaluator-model</c>).
/// </param>
/// <param name="RubricVersion">
/// Rubric version the evaluation applied, 1 to 100 characters. It must equal the attempt's <c>rubricVersion</c>
/// exactly (<c>agent-recruiting.rubric-version-conflict</c>).
/// </param>
/// <param name="Findings">
/// Findings of the evaluator as free text, stored trimmed without blank entries or duplicates; null is stored as an
/// empty list.
/// </param>
/// <param name="EvaluatedAtUtc">
/// Instant at which the evaluation was produced, with offset, stored as sent. The default value
/// <c>0001-01-01T00:00:00+00:00</c> leaves the attempt incomplete (<c>evaluation-timestamp</c>).
/// </param>
public sealed record AgentRecruitingAutomatedEvaluation(
    AgentRecruitingAutomatedDecision Decision,
    decimal? Score,
    Guid? EvaluatorAgentId,
    Guid? ProviderProfileId,
    string Model,
    string RubricVersion,
    IReadOnlyList<string> Findings,
    DateTimeOffset EvaluatedAtUtc);

/// <summary>
/// Structured analysis of an agent recruiting attempt for human reviewers: a classification, a confidence, a summary,
/// a proposed next step and the observed strengths and gaps. The caller supplies it; it is validated and stored but
/// affects neither completeness nor readiness.
/// </summary>
/// <param name="Classification">
/// Classification of the candidate, as a JSON string: <c>StrongFit</c>, <c>Suitable</c>, <c>NeedsTraining</c>,
/// <c>NotSuitable</c> or <c>Inconclusive</c> (the integers 1 to 5 are also accepted).
/// </param>
/// <param name="Confidence">
/// Confidence in the classification as a number from 0 through 1 inclusive
/// (<c>agent-recruiting.analysis-confidence-invalid</c> otherwise).
/// </param>
/// <param name="Summary">Evidence-based summary of the analysis, 1 to 4000 characters, stored trimmed.</param>
/// <param name="ProposedNextStep">
/// Proposed next step for the candidate, as a JSON string: <c>Advance</c>, <c>RequestHumanReview</c>,
/// <c>AssignTraining</c>, <c>Reassess</c>, <c>Hold</c> or <c>Reject</c> (the integers 1 to 6 are also accepted).
/// </param>
/// <param name="Strengths">
/// Observed strengths: at most 20 entries, each non-blank and at most 500 characters, stored trimmed
/// (<c>agent-recruiting.analysis-items-invalid</c> otherwise). Send an empty array when there are none; null is
/// rejected.
/// </param>
/// <param name="Gaps">Observed gaps, with the same limits as <c>strengths</c>.</param>
public sealed record AgentRecruitingAssessmentAnalysis(
    AgentRecruitingAssessmentClassification Classification,
    decimal Confidence,
    string Summary,
    AgentRecruitingProposedNextStep ProposedNextStep,
    IReadOnlyList<string> Strengths,
    IReadOnlyList<string> Gaps);

/// <summary>
/// One recorded attempt of an agent recruiting interview: the run whose result is assessed, the challenge and rubric
/// used, integrity hashes, the automated evaluation and optional analysis, and the evidence completeness that the
/// server derived when the attempt was appended. Attempts are append-only and never change after they are recorded.
/// </summary>
/// <param name="Id">
/// Identifier of the attempt, issued by the server. Human reviews refer to it as <c>attemptId</c>.
/// </param>
/// <param name="InterviewId">Identifier of the interview the attempt belongs to.</param>
/// <param name="Sequence">Position of the attempt in its interview: 1 for the first, increasing by one.</param>
/// <param name="Target">The run whose result the attempt records.</param>
/// <param name="ChallengeKey">Caller-defined key of the challenge the candidate was given, stored trimmed.</param>
/// <param name="ChallengeVersion">Caller-defined version of that challenge, stored trimmed.</param>
/// <param name="RubricVersion">Version of the rubric used to evaluate the attempt, stored trimmed.</param>
/// <param name="InputHash">
/// SHA-256 of the challenge input as <c>sha256:</c> followed by 64 lowercase hexadecimal digits, or an empty string
/// when none was supplied (the attempt is then incomplete). Stored as supplied; the server does not recompute it.
/// </param>
/// <param name="OutputHash">SHA-256 of the candidate's output, in the same form as <c>inputHash</c>.</param>
/// <param name="StructuredOutputContractKey">
/// Key of the structured-output contract the candidate's output had to satisfy, or an empty string when none applied.
/// </param>
/// <param name="StructuredOutputSchemaHash">
/// SHA-256 of that contract's schema, in the same form as <c>inputHash</c>, or an empty string.
/// </param>
/// <param name="StructuredOutputValidationStatus">
/// Validation status reported for the structured output, as supplied and trimmed (for example <c>succeeded</c>), or
/// an empty string.
/// </param>
/// <param name="AutomatedEvaluation">
/// Automated evaluation recorded with the attempt, or null when none was supplied.
/// </param>
/// <param name="Completeness">
/// Whether the attempt carries all evidence that readiness requires, as a JSON string: <c>Complete</c> or
/// <c>Incomplete</c> (see <c>missingEvidence</c>). Derived when the attempt was appended.
/// </param>
/// <param name="MissingEvidence">
/// Names of the missing evidence, empty when complete: <c>terminal-execution-target</c> (the target run had not
/// finished), <c>input-hash</c>, <c>output-hash</c>, <c>automated-evaluation</c>, <c>evaluator-agent-id</c>,
/// <c>evaluator-provider-profile-id</c>, <c>evaluator-model</c>, <c>evaluation-timestamp</c> and
/// <c>successful-structured-output-validation</c> (a contract key was given but the validation status is not
/// <c>succeeded</c>).
/// </param>
/// <param name="CreatedAtUtc">UTC instant at which the server recorded the attempt.</param>
/// <param name="Analysis">Structured analysis recorded with the attempt, or null when none was supplied.</param>
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

/// <summary>
/// One human review of an agent recruiting attempt: the decision, the reviewer identity taken from the authenticated
/// bearer token, the authorization evidence that makes an approval count, and notes. Reviews are append-only; the
/// latest review of an attempt is the one that readiness uses.
/// </summary>
/// <param name="Id">Identifier of the review, issued by the server.</param>
/// <param name="InterviewId">Identifier of the interview that contains the reviewed attempt.</param>
/// <param name="AttemptId">Identifier of the reviewed attempt.</param>
/// <param name="Decision">Decision of the reviewer, as a JSON string: <c>Approved</c> or <c>Rejected</c>.</param>
/// <param name="ReviewerActorId">
/// Subject of the bearer token that appended the review (the subject the token was issued for); not a CRM/HR party
/// identifier.
/// </param>
/// <param name="ReviewerDisplayName">
/// Name of the reviewer from the token's name claim, or the name supplied with the review when the token has none;
/// empty when neither was available.
/// </param>
/// <param name="AuthorizationReference">
/// Caller-supplied reference to the authorization behind an approval, stored trimmed; empty when none was supplied.
/// </param>
/// <param name="AuthorizationEvidenceHash">
/// SHA-256 of the authorization evidence as <c>sha256:</c> followed by 64 lowercase hexadecimal digits, or an empty
/// string. Stored as supplied (format check only).
/// </param>
/// <param name="Notes">Notes of the reviewer, stored trimmed; empty when none were supplied.</param>
/// <param name="QualifiesForReadiness">
/// True only for an approval that carries both an authorization reference and an authorization evidence hash.
/// </param>
/// <param name="MissingEvidence">
/// What an approval lacks to qualify: <c>human-authorization-reference</c> and
/// <c>human-authorization-evidence-hash</c>. Always empty for a rejection.
/// </param>
/// <param name="ReviewedAtUtc">UTC instant at which the server recorded the review.</param>
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

/// <summary>
/// An agent recruiting interview: the assessment of one candidate agent, fenced to the configuration version that was
/// current when it was created, with its append-only attempts and human reviews. Every agent recruiting write
/// returns the whole updated interview. It is not a CRM/HR recruitment interview of a person.
/// </summary>
/// <param name="Id">
/// Identifier of the interview, issued by the server; use it in the <c>/api/agent-recruiting/interviews</c> routes.
/// </param>
/// <param name="CandidateAgentId">Identifier of the assessed candidate agent.</param>
/// <param name="CandidateConfigurationVersion">
/// Configuration version of the candidate when the interview was created: 64 uppercase hexadecimal digits, a hash of
/// the agent's configuration. Readiness counts the interview's attempts only while this equals the candidate's
/// current version (compared case-insensitively).
/// </param>
/// <param name="CandidateNameSnapshot">Name of the candidate agent when the interview was created.</param>
/// <param name="CandidateModelSnapshot">
/// Model configured on the candidate agent when the interview was created; may be empty.
/// </param>
/// <param name="Purpose">What the assessment must establish, as supplied at creation (trimmed).</param>
/// <param name="CreatedAtUtc">UTC instant at which the interview was created.</param>
/// <param name="Attempts">Attempts in append order (by <c>sequence</c>); empty for a new interview.</param>
/// <param name="Reviews">Human reviews in append order; empty for a new interview.</param>
/// <param name="RecruitmentApplicationId">
/// Identifier of the CRM/HR recruitment application the interview supports, as supplied at creation; null when none.
/// Its existence is not checked.
/// </param>
/// <param name="ProjectId">
/// Identifier of a project the interview relates to, as supplied at creation; null when none. Its existence is not
/// checked.
/// </param>
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

/// <summary>
/// Summary of one attempt in a candidate's readiness history. The history covers every attempt of the candidate's
/// interviews in scope, including attempts for earlier configurations.
/// </summary>
/// <param name="AttemptId">Identifier of the attempt.</param>
/// <param name="Sequence">Position of the attempt within its own interview, starting at 1.</param>
/// <param name="CreatedAtUtc">UTC instant at which the attempt was recorded.</param>
/// <param name="Completeness">
/// Whether the attempt carried all evidence that readiness requires, as a JSON string: <c>Complete</c> or
/// <c>Incomplete</c>.
/// </param>
/// <param name="AutomatedDecision">
/// Decision of the attempt's automated evaluation, as a JSON string: <c>Passed</c>, <c>Failed</c> or
/// <c>NeedsHumanReview</c>; null when the attempt has no evaluation.
/// </param>
/// <param name="Score">Score of the automated evaluation from 0 through 100; null when none was recorded.</param>
/// <param name="HumanDecision">
/// Decision of the latest human review of the attempt, as a JSON string: <c>Approved</c> or <c>Rejected</c>; null
/// when the attempt has not been reviewed.
/// </param>
public sealed record AgentRecruitingAttemptComparison(
    Guid AttemptId,
    int Sequence,
    DateTimeOffset CreatedAtUtc,
    AgentRecruitingEvidenceCompleteness Completeness,
    AgentRecruitingAutomatedDecision? AutomatedDecision,
    decimal? Score,
    AgentRecruitingHumanDecision? HumanDecision);

/// <summary>
/// Readiness of a candidate agent's current configuration, derived on each read from its agent recruiting evidence.
/// It is evidence for a separate activation decision: reading it never activates or changes the agent.
/// </summary>
/// <param name="CandidateAgentId">Identifier of the candidate agent.</param>
/// <param name="CurrentConfigurationVersion">
/// The candidate's configuration version computed for this read: 64 uppercase hexadecimal digits. Send it as
/// <c>candidateConfigurationVersion</c> when creating an interview.
/// </param>
/// <param name="Status">
/// Readiness, as a JSON string: <c>Ready</c>, <c>NoInterviews</c>, <c>IncompleteEvidence</c>,
/// <c>AwaitingHumanApproval</c> or <c>Rejected</c>, decided as described for the readiness operation.
/// </param>
/// <param name="ReadyForProduction">True exactly when <c>status</c> is <c>Ready</c>.</param>
/// <param name="ActivatesAgent">Always false: readiness never activates the agent.</param>
/// <param name="RequiresSeparateActivationAuthorization">
/// Always true: activating the agent requires its own authorization outside agent recruiting.
/// </param>
/// <param name="QualifyingInterviewId">Interview of the qualifying attempt when ready; otherwise null.</param>
/// <param name="QualifyingAttemptId">The qualifying attempt when ready; otherwise null.</param>
/// <param name="QualifyingReviewId">The qualifying human approval when ready; otherwise null.</param>
/// <param name="HumanAuthorizationReference">
/// Authorization reference of the qualifying approval when ready; otherwise an empty string.
/// </param>
/// <param name="HumanAuthorizationEvidenceHash">
/// Authorization evidence hash of the qualifying approval when ready; otherwise an empty string.
/// </param>
/// <param name="Reasons">
/// Human-readable explanations of the status. Their wording can change; branch on <c>status</c>.
/// </param>
/// <param name="AttemptHistory">
/// Every attempt of the candidate's interviews in scope, oldest first, including attempts for earlier
/// configurations.
/// </param>
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

/// <summary>
/// Server-side result of resolving an agent recruiting target run; not part of the HTTP contract.
/// </summary>
/// <param name="Found">True when the run exists in the current workspace.</param>
/// <param name="State">State name of the run as reported by its owner.</param>
/// <param name="IsTerminal">True when the run has finished.</param>
/// <param name="ParticipatingAgentIds">Agents with verifiable participation in the run.</param>
public sealed record AgentRecruitingTargetResolution(
    bool Found,
    string State,
    bool IsTerminal,
    IReadOnlyList<Guid>? ParticipatingAgentIds = null);

/// <summary>
/// Request to open an agent recruiting interview for a candidate agent. The interview is fenced to the candidate's
/// current configuration, so the request must name that configuration version.
/// </summary>
/// <param name="CandidateAgentId">
/// Identifier of the candidate agent in the current workspace, as listed by <c>GET /api/agents</c>; not the empty
/// GUID.
/// </param>
/// <param name="CandidateConfigurationVersion">
/// The candidate's current configuration version, as returned in <c>currentConfigurationVersion</c> by
/// <c>GET /api/agent-recruiting/candidates/{agentId}/readiness</c> (64 hexadecimal digits, compared
/// case-insensitively). Any other value is rejected with <c>agent-recruiting.candidate-version-conflict</c>.
/// </param>
/// <param name="Purpose">What the assessment must establish, 1 to 500 characters, stored trimmed.</param>
/// <param name="RecruitmentApplicationId">
/// Optional identifier of the CRM/HR recruitment application the interview supports, from
/// <c>GET /api/crm-hr/recruiting/applications</c>. Stored as sent without an existence check; the candidate interview
/// list and readiness can be filtered by it. Omit it or send null when there is none; the empty GUID is rejected.
/// </param>
/// <param name="ProjectId">
/// Optional identifier of a project the interview relates to, stored as sent without an existence check. Omit it or
/// send null when there is none; the empty GUID is rejected.
/// </param>
public sealed record CreateAgentRecruitingInterviewCommand(
    Guid CandidateAgentId,
    string CandidateConfigurationVersion,
    string Purpose,
    Guid? RecruitmentApplicationId = null,
    Guid? ProjectId = null);

/// <summary>
/// Request to append an attempt to an agent recruiting interview: the finished run whose result is assessed, the
/// challenge and rubric, integrity hashes, the automated evaluation and an optional analysis. Missing evidence does
/// not reject the request; the attempt is then stored as incomplete and lists what is missing. Send every member
/// except <c>analysis</c>, using an empty string or null where a value is not available.
/// </summary>
/// <param name="Target">
/// The run to record. It must exist in the current workspace and show participation of the interview's candidate.
/// </param>
/// <param name="ChallengeKey">Caller-defined key of the challenge given to the candidate, 1 to 200 characters.</param>
/// <param name="ChallengeVersion">Caller-defined version of the challenge, 1 to 100 characters.</param>
/// <param name="RubricVersion">Version of the rubric used to evaluate the attempt, 1 to 100 characters.</param>
/// <param name="InputHash">
/// SHA-256 of the challenge input: 64 hexadecimal digits, optionally prefixed with <c>sha256:</c>. An empty string or
/// null leaves the attempt incomplete (<c>input-hash</c>); any other value is rejected with
/// <c>agent-recruiting.hash-invalid</c>.
/// </param>
/// <param name="OutputHash">SHA-256 of the candidate's output, in the same form as <c>inputHash</c>.</param>
/// <param name="StructuredOutputContractKey">
/// Key of the structured-output contract the output had to satisfy, at most 200 characters; an empty string or null
/// when none applies. When set, the attempt is complete only if <c>structuredOutputValidationStatus</c> is
/// <c>succeeded</c>.
/// </param>
/// <param name="StructuredOutputSchemaHash">
/// SHA-256 of that contract's schema, in the same form as <c>inputHash</c>; an empty string or null when none.
/// </param>
/// <param name="StructuredOutputValidationStatus">
/// Validation status of the structured output, at most 100 characters; <c>succeeded</c> (compared
/// case-insensitively) marks a successful validation. An empty string or null when no contract applies.
/// </param>
/// <param name="AutomatedEvaluation">
/// Automated evaluation of the attempt. Send null when there is none; the attempt is then incomplete
/// (<c>automated-evaluation</c>).
/// </param>
/// <param name="Analysis">Optional structured analysis for reviewers; omit it or send null when there is none.</param>
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

/// <summary>
/// Request to append a human review to one attempt of an agent recruiting interview. The stored reviewer identity is
/// always taken from the authenticated bearer token. Send every member, using an empty string where a value is not
/// available.
/// </summary>
/// <param name="AttemptId">Identifier of the reviewed attempt; it must belong to the interview in the route.</param>
/// <param name="Decision">
/// Decision of the reviewer, as a JSON string: <c>Approved</c> or <c>Rejected</c> (the integers 0 and 1 are also
/// accepted).
/// </param>
/// <param name="ReviewerActorId">
/// Optional check: when not blank, it must equal the bearer token's subject exactly
/// (<c>agent-recruiting.reviewer-identity-conflict</c>). The stored reviewer is always the token subject.
/// </param>
/// <param name="ReviewerDisplayName">
/// Reviewer name, at most 200 characters, used only when the token carries no name claim.
/// </param>
/// <param name="AuthorizationReference">
/// Reference to the authorization behind an approval, at most 500 characters. An approval without it is stored but
/// does not qualify for readiness.
/// </param>
/// <param name="AuthorizationEvidenceHash">
/// SHA-256 of the authorization evidence: 64 hexadecimal digits, optionally prefixed with <c>sha256:</c>. An approval
/// without it is stored but does not qualify for readiness; any other non-empty value is rejected with
/// <c>agent-recruiting.hash-invalid</c>.
/// </param>
/// <param name="Notes">Notes of the reviewer, at most 4000 characters.</param>
public sealed record AppendAgentRecruitingReviewCommand(
    Guid AttemptId,
    AgentRecruitingHumanDecision Decision,
    string ReviewerActorId,
    string ReviewerDisplayName,
    string AuthorizationReference,
    string AuthorizationEvidenceHash,
    string Notes);
