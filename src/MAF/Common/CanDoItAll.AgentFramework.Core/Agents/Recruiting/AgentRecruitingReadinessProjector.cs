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

        var qualifying = attempts
            .Where(item => string.Equals(
                item.Interview.CandidateConfigurationVersion,
                currentConfigurationVersion,
                StringComparison.OrdinalIgnoreCase))
            .Where(item => item.Attempt.Completeness == AgentRecruitingEvidenceCompleteness.Complete)
            .Where(item => item.Attempt.AutomatedEvaluation?.Decision == AgentRecruitingAutomatedDecision.Passed)
            .Select(item => new
            {
                item.Interview,
                item.Attempt,
                Review = reviews
                    .Where(review => review.Review.AttemptId == item.Attempt.Id)
                    .Select(review => review.Review)
                    .LastOrDefault()
            })
            .Where(item => item.Review is
            {
                Decision: AgentRecruitingHumanDecision.Approved,
                QualifiesForReadiness: true
            })
            .OrderBy(item => item.Review!.ReviewedAtUtc)
            .ThenBy(item => item.Review!.Id)
            .LastOrDefault();
        if (qualifying is not null)
        {
            return new AgentRecruitingCandidateReadiness(
                candidateAgentId,
                currentConfigurationVersion,
                AgentRecruitingReadinessStatus.Ready,
                ReadyForProduction: true,
                ActivatesAgent: false,
                RequiresSeparateActivationAuthorization: true,
                qualifying.Interview.Id,
                qualifying.Attempt.Id,
                qualifying.Review!.Id,
                qualifying.Review.AuthorizationReference,
                qualifying.Review.AuthorizationEvidenceHash,
                ["Evidence is complete and has a qualifying human approval for the current candidate configuration."],
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

        var currentVersionAttemptIds = currentVersionAttempts
            .Select(item => item.Attempt.Id)
            .ToHashSet();
        var latestReview = reviews
            .Where(item => currentVersionAttemptIds.Contains(item.Review.AttemptId))
            .Select(item => item.Review)
            .LastOrDefault();
        if (latestReview?.Decision == AgentRecruitingHumanDecision.Rejected)
        {
            return NotReady(
                candidateAgentId,
                currentConfigurationVersion,
                AgentRecruitingReadinessStatus.Rejected,
                ["The latest human review rejected the available evidence."],
                history);
        }

        return NotReady(
            candidateAgentId,
            currentConfigurationVersion,
            AgentRecruitingReadinessStatus.AwaitingHumanApproval,
            ["Complete evidence exists, but it does not have a qualifying human approval."],
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
