using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Persistence;

public sealed partial class FileSandboxWorkspaceStore
{
    public async Task<AgentRecruitingInterview> CreateInterviewAsync(
        AgentRecruitingInterview interview,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(interview);

        await gate.WaitAsync(cancellationToken);
        try
        {
            await using var workspaceLock = await crossProcessLock.AcquireAsync(cancellationToken);
            var path = layout.RecruitingInterviewPath(interview.Id);
            if (File.Exists(path))
            {
                throw new InvalidOperationException(
                    $"Agent recruiting interview '{interview.Id:D}' already exists.");
            }

            await jsonStore.WriteJsonAtomicallyAsync(path, interview, cancellationToken);
            return interview;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<AgentRecruitingInterview?> GetInterviewAsync(
        Guid interviewId,
        CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            await using var workspaceLock = await crossProcessLock.AcquireAsync(cancellationToken);
            return await LoadRecruitingInterviewCoreAsync(interviewId, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<IReadOnlyList<AgentRecruitingInterview>> ListCandidateInterviewsAsync(
        Guid candidateAgentId,
        CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            await using var workspaceLock = await crossProcessLock.AcquireAsync(cancellationToken);
            var interviews = await jsonStore.LoadRecordsFromDirectoryAsync<AgentRecruitingInterview>(
                layout.RecruitingEvidenceRoot,
                cancellationToken);
            return interviews
                .Where(item => item.CandidateAgentId == candidateAgentId)
                .OrderBy(item => item.CreatedAtUtc)
                .ThenBy(item => item.Id)
                .ToList();
        }
        finally
        {
            gate.Release();
        }
    }

    public Task<AgentRecruitingInterview> AppendAttemptAsync(
        Guid interviewId,
        AgentRecruitingAttempt attempt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(attempt);
        return UpdateRecruitingInterviewAsync(
            interviewId,
            interview =>
            {
                if (attempt.InterviewId != interview.Id ||
                    attempt.Sequence != interview.Attempts.Count + 1 ||
                    interview.Attempts.Any(item => item.Id == attempt.Id))
                {
                    throw new InvalidOperationException(
                        "Agent recruiting attempt does not satisfy append-only interview invariants.");
                }

                return interview with
                {
                    Attempts = [.. interview.Attempts, attempt]
                };
            },
            cancellationToken);
    }

    public Task<AgentRecruitingInterview> AppendReviewAsync(
        Guid interviewId,
        AgentRecruitingHumanReview review,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(review);
        return UpdateRecruitingInterviewAsync(
            interviewId,
            interview =>
            {
                if (review.InterviewId != interview.Id ||
                    interview.Attempts.All(item => item.Id != review.AttemptId) ||
                    interview.Reviews.Any(item => item.Id == review.Id))
                {
                    throw new InvalidOperationException(
                        "Agent recruiting review does not satisfy append-only interview invariants.");
                }

                return interview with
                {
                    Reviews = [.. interview.Reviews, review]
                };
            },
            cancellationToken);
    }

    private async Task<AgentRecruitingInterview> UpdateRecruitingInterviewAsync(
        Guid interviewId,
        Func<AgentRecruitingInterview, AgentRecruitingInterview> update,
        CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            await using var workspaceLock = await crossProcessLock.AcquireAsync(cancellationToken);
            var current = await LoadRecruitingInterviewCoreAsync(interviewId, cancellationToken)
                ?? throw new InvalidOperationException(
                    $"Agent recruiting interview '{interviewId:D}' was not found.");
            var updated = update(current);
            await jsonStore.WriteJsonAtomicallyAsync(
                layout.RecruitingInterviewPath(interviewId),
                updated,
                cancellationToken);
            return updated;
        }
        finally
        {
            gate.Release();
        }
    }

    private Task<AgentRecruitingInterview?> LoadRecruitingInterviewCoreAsync(
        Guid interviewId,
        CancellationToken cancellationToken)
        => jsonStore.ReadJsonAsync<AgentRecruitingInterview>(
            layout.RecruitingInterviewPath(interviewId),
            cancellationToken);
}
