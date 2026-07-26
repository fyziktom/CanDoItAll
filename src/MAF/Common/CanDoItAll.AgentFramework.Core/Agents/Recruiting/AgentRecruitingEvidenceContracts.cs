using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public enum AgentRecruitingEvidenceFailureKind
{
    InvalidRequest,
    NotFound,
    Conflict
}

public sealed class AgentRecruitingEvidenceException(
    AgentRecruitingEvidenceFailureKind kind,
    string code,
    string message) : InvalidOperationException(message)
{
    public AgentRecruitingEvidenceFailureKind Kind { get; } = kind;

    public string Code { get; } = code;
}

public interface IAgentRecruitingEvidenceStore
{
    Task<AgentRecruitingInterview> CreateInterviewAsync(
        AgentRecruitingInterview interview,
        CancellationToken cancellationToken = default);

    Task<AgentRecruitingInterview?> GetInterviewAsync(
        Guid interviewId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AgentRecruitingInterview>> ListCandidateInterviewsAsync(
        Guid candidateAgentId,
        CancellationToken cancellationToken = default);

    Task<AgentRecruitingInterview> AppendAttemptAsync(
        Guid interviewId,
        AgentRecruitingAttempt attempt,
        CancellationToken cancellationToken = default);

    Task<AgentRecruitingInterview> AppendReviewAsync(
        Guid interviewId,
        AgentRecruitingHumanReview review,
        CancellationToken cancellationToken = default);
}

public interface IAgentRecruitingTargetResolver
{
    Task<AgentRecruitingTargetResolution> ResolveAsync(
        AgentRecruitingExecutionTarget target,
        CancellationToken cancellationToken = default);
}

public interface IAgentRecruitingEvidenceService
{
    Task<AgentRecruitingInterview> CreateInterviewAsync(
        CreateAgentRecruitingInterviewCommand command,
        CancellationToken cancellationToken = default);

    Task<AgentRecruitingInterview> AppendAttemptAsync(
        Guid interviewId,
        AppendAgentRecruitingAttemptCommand command,
        CancellationToken cancellationToken = default);

    Task<AgentRecruitingInterview> AppendReviewAsync(
        Guid interviewId,
        AppendAgentRecruitingReviewCommand command,
        CancellationToken cancellationToken = default);

    Task<AgentRecruitingInterview> GetInterviewAsync(
        Guid interviewId,
        CancellationToken cancellationToken = default);

    Task<AgentRecruitingCandidateReadiness> GetCandidateReadinessAsync(
        Guid candidateAgentId,
        CancellationToken cancellationToken = default);
}
