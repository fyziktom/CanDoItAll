using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public static class AgentRecruitingReadinessProjector
{
    public static AgentRecruitingCandidateReadiness Project(
        Guid candidateAgentId,
        string currentConfigurationVersion,
        IReadOnlyList<AgentRecruitingInterview> interviews)
    {
        ArgumentNullException.ThrowIfNull(interviews);
        var attempts = interviews
            .SelectMany(interview => interview.Attempts.Select(attempt => (Interview: interview, Attempt: attempt)))
            .OrderBy(item => item.Attempt.CreatedAtUtc)
            .ThenBy(item => item.Interview.CreatedAtUtc)
            .ThenBy(item => item.Interview.Id)
            .ThenBy(item => item.Attempt.Sequence)
            .ThenBy(item => item.Attempt.Id)
            .ToList();
        var reviews = interviews
            .SelectMany(interview => interview.Reviews.Select(review => (Interview: interview, Review: review)))
            .OrderBy(item => item.Review.ReviewedAtUtc)
            .ThenBy(item => item.Review.Id)
            .ToList();
        var history = attempts
            .Select(item =>
            {
                var latestReview = reviews
                    .Where(review => review.Review.AttemptId == item.Attempt.Id)
                    .Select(review => review.Review)
                    .LastOrDefault();
                return new AgentRecruitingAttemptComparison(
                    item.Attempt.Id,
                    item.Attempt.Sequence,
                    item.Attempt.CreatedAtUtc,
                    item.Attempt.Completeness,
                    item.Attempt.AutomatedEvaluation?.Decision,
                    item.Attempt.AutomatedEvaluation?.Score,
                    latestReview?.Decision);
            })
            .ToList();

        if (interviews.Count == 0)
        {
            return NotReady(
                candidateAgentId,
                currentConfigurationVersion,
                AgentRecruitingReadinessStatus.NoInterviews,
                ["No interview evidence exists for this candidate."],
                history);
        }

        var currentVersionAttempts = attempts
            .Where(item => string.Equals(
                item.Interview.CandidateConfigurationVersion,
                currentConfigurationVersion,
                StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (currentVersionAttempts.Count == 0 ||
            currentVersionAttempts.All(item =>
                item.Attempt.Completeness == AgentRecruitingEvidenceCompleteness.Incomplete))
        {
            return NotReady(
                candidateAgentId,
                currentConfigurationVersion,
                AgentRecruitingReadinessStatus.IncompleteEvidence,
                ["No complete attempt exists for the current candidate configuration."],
                history);
        }

        var latestAttempt = currentVersionAttempts[^1];
        var latestReview = reviews
            .Where(item => item.Review.AttemptId == latestAttempt.Attempt.Id)
            .Select(item => item.Review)
            .LastOrDefault();
        if (latestReview?.Decision == AgentRecruitingHumanDecision.Rejected)
        {
            return NotReady(
                candidateAgentId,
                currentConfigurationVersion,
                AgentRecruitingReadinessStatus.Rejected,
                ["The latest assessment attempt was rejected by human review."],
                history);
        }

        if (latestAttempt.Attempt.Completeness == AgentRecruitingEvidenceCompleteness.Incomplete)
        {
            return NotReady(
                candidateAgentId,
                currentConfigurationVersion,
                AgentRecruitingReadinessStatus.IncompleteEvidence,
                ["The latest assessment attempt is incomplete."],
                history);
        }

        if (latestAttempt.Attempt.AutomatedEvaluation?.Decision ==
                AgentRecruitingAutomatedDecision.Passed &&
            latestReview is
            {
                Decision: AgentRecruitingHumanDecision.Approved,
                QualifiesForReadiness: true
            })
        {
            return new AgentRecruitingCandidateReadiness(
                candidateAgentId,
                currentConfigurationVersion,
                AgentRecruitingReadinessStatus.Ready,
                ReadyForProduction: true,
                ActivatesAgent: false,
                RequiresSeparateActivationAuthorization: true,
                latestAttempt.Interview.Id,
                latestAttempt.Attempt.Id,
                latestReview.Id,
                latestReview.AuthorizationReference,
                latestReview.AuthorizationEvidenceHash,
                ["The latest assessment attempt is complete and has a qualifying human approval for the current candidate configuration."],
                history);
        }

        return NotReady(
            candidateAgentId,
            currentConfigurationVersion,
            AgentRecruitingReadinessStatus.AwaitingHumanApproval,
            ["The latest complete assessment attempt does not have a qualifying human approval."],
            history);
    }

    private static AgentRecruitingCandidateReadiness NotReady(
        Guid candidateAgentId,
        string currentConfigurationVersion,
        AgentRecruitingReadinessStatus status,
        IReadOnlyList<string> reasons,
        IReadOnlyList<AgentRecruitingAttemptComparison> history)
        => new(
            candidateAgentId,
            currentConfigurationVersion,
            status,
            ReadyForProduction: false,
            ActivatesAgent: false,
            RequiresSeparateActivationAuthorization: true,
            QualifyingInterviewId: null,
            QualifyingAttemptId: null,
            QualifyingReviewId: null,
            HumanAuthorizationReference: string.Empty,
            HumanAuthorizationEvidenceHash: string.Empty,
            reasons,
            history);
}
