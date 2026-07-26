using CanDoItAll.AgentFramework.Models;
using static CanDoItAll.AgentFramework.Core.AgentRecruitingEvidenceValidation;

namespace CanDoItAll.AgentFramework.Core;

public sealed class AgentRecruitingEvidenceService(
    ISandboxWorkspaceCatalogStore catalogStore,
    IAgentRecruitingEvidenceStore evidenceStore,
    IAgentRecruitingTargetResolver targetResolver,
    TimeProvider timeProvider) : IAgentRecruitingEvidenceService
{
    public async Task<AgentRecruitingInterview> CreateInterviewAsync(
        CreateAgentRecruitingInterviewCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        EnsureNonEmpty(command.CandidateAgentId, "candidateAgentId");
        EnsureText(command.CandidateConfigurationVersion, "candidateConfigurationVersion", 128);
        EnsureText(command.Purpose, "purpose", 500);

        var candidate = await GetCandidateAsync(command.CandidateAgentId, cancellationToken);
        var currentVersion = AgentConfigurationVersion.Create(candidate);
        if (!string.Equals(
                currentVersion,
                command.CandidateConfigurationVersion.Trim(),
                StringComparison.OrdinalIgnoreCase))
        {
            throw Failure(
                AgentRecruitingEvidenceFailureKind.Conflict,
                "agent-recruiting.candidate-version-conflict",
                "The candidate configuration version does not match the current workspace agent.");
        }

        var now = timeProvider.GetUtcNow();
        var interview = new AgentRecruitingInterview(
            Guid.NewGuid(),
            candidate.Id,
            currentVersion,
            candidate.Name,
            candidate.Model,
            command.Purpose.Trim(),
            now,
            [],
            []);
        return await evidenceStore.CreateInterviewAsync(interview, cancellationToken);
    }

    public async Task<AgentRecruitingInterview> AppendAttemptAsync(
        Guid interviewId,
        AppendAgentRecruitingAttemptCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        EnsureNonEmpty(interviewId, "interviewId");
        EnsureTarget(command.Target);
        EnsureText(command.ChallengeKey, "challengeKey", 200);
        EnsureText(command.ChallengeVersion, "challengeVersion", 100);
        EnsureText(command.RubricVersion, "rubricVersion", 100);
        EnsureOptionalText(command.StructuredOutputContractKey, "structuredOutputContractKey", 200);
        EnsureOptionalHash(command.StructuredOutputSchemaHash, "structuredOutputSchemaHash");
        EnsureOptionalText(command.StructuredOutputValidationStatus, "structuredOutputValidationStatus", 100);

        var interview = await GetInterviewAsync(interviewId, cancellationToken);
        var target = await targetResolver.ResolveAsync(command.Target, cancellationToken);
        if (!target.Found)
        {
            throw Failure(
                AgentRecruitingEvidenceFailureKind.NotFound,
                "agent-recruiting.target-not-found",
                "The evidence target was not found in the current workspace.");
        }

        if (command.Target.Kind == AgentRecruitingTargetKind.AgentExecutionRun &&
            target.ExecutedAgentId != interview.CandidateAgentId)
        {
            throw Failure(
                AgentRecruitingEvidenceFailureKind.Conflict,
                "agent-recruiting.target-candidate-conflict",
                "The agent execution target does not belong to the interview candidate.");
        }

        await ValidateAutomatedEvaluationAsync(
            command.AutomatedEvaluation,
            command.RubricVersion,
            cancellationToken);

        var missing = CollectAttemptMissingEvidence(command, target);
        var attempt = new AgentRecruitingAttempt(
            Guid.NewGuid(),
            interview.Id,
            interview.Attempts.Count + 1,
            command.Target,
            command.ChallengeKey.Trim(),
            command.ChallengeVersion.Trim(),
            command.RubricVersion.Trim(),
            NormalizeOptionalHash(command.InputHash),
            NormalizeOptionalHash(command.OutputHash),
            NormalizeText(command.StructuredOutputContractKey),
            NormalizeOptionalHash(command.StructuredOutputSchemaHash),
            NormalizeText(command.StructuredOutputValidationStatus),
            NormalizeEvaluation(command.AutomatedEvaluation),
            missing.Count == 0
                ? AgentRecruitingEvidenceCompleteness.Complete
                : AgentRecruitingEvidenceCompleteness.Incomplete,
            missing,
            timeProvider.GetUtcNow());
        try
        {
            return await evidenceStore.AppendAttemptAsync(interviewId, attempt, cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            throw Failure(
                AgentRecruitingEvidenceFailureKind.Conflict,
                "agent-recruiting.concurrent-append-conflict",
                $"The interview changed before the attempt could be appended. {exception.Message}");
        }
    }

    public async Task<AgentRecruitingInterview> AppendReviewAsync(
        Guid interviewId,
        AppendAgentRecruitingReviewCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        EnsureNonEmpty(interviewId, "interviewId");
        EnsureNonEmpty(command.AttemptId, "attemptId");
        EnsureText(command.ReviewerActorId, "reviewerActorId", 200);
        EnsureOptionalText(command.ReviewerDisplayName, "reviewerDisplayName", 200);
        EnsureOptionalText(command.AuthorizationReference, "authorizationReference", 500);
        EnsureOptionalHash(command.AuthorizationEvidenceHash, "authorizationEvidenceHash");
        EnsureOptionalText(command.Notes, "notes", 4000);

        var interview = await GetInterviewAsync(interviewId, cancellationToken);
        if (interview.Attempts.All(item => item.Id != command.AttemptId))
        {
            throw Failure(
                AgentRecruitingEvidenceFailureKind.NotFound,
                "agent-recruiting.attempt-not-found",
                "The reviewed attempt was not found on this interview.");
        }

        var missing = new List<string>();
        if (command.Decision == AgentRecruitingHumanDecision.Approved)
        {
            if (string.IsNullOrWhiteSpace(command.AuthorizationReference))
            {
                missing.Add("human-authorization-reference");
            }

            if (string.IsNullOrWhiteSpace(command.AuthorizationEvidenceHash))
            {
                missing.Add("human-authorization-evidence-hash");
            }
        }

        var review = new AgentRecruitingHumanReview(
            Guid.NewGuid(),
            interview.Id,
            command.AttemptId,
            command.Decision,
            command.ReviewerActorId.Trim(),
            NormalizeText(command.ReviewerDisplayName),
            NormalizeText(command.AuthorizationReference),
            NormalizeOptionalHash(command.AuthorizationEvidenceHash),
            NormalizeText(command.Notes),
            command.Decision == AgentRecruitingHumanDecision.Approved && missing.Count == 0,
            missing,
            timeProvider.GetUtcNow());
        return await evidenceStore.AppendReviewAsync(interviewId, review, cancellationToken);
    }

    public async Task<AgentRecruitingInterview> GetInterviewAsync(
        Guid interviewId,
        CancellationToken cancellationToken = default)
    {
        EnsureNonEmpty(interviewId, "interviewId");
        return await evidenceStore.GetInterviewAsync(interviewId, cancellationToken)
            ?? throw Failure(
                AgentRecruitingEvidenceFailureKind.NotFound,
                "agent-recruiting.interview-not-found",
                "The interview was not found in the current workspace.");
    }

    public async Task<AgentRecruitingCandidateReadiness> GetCandidateReadinessAsync(
        Guid candidateAgentId,
        CancellationToken cancellationToken = default)
    {
        EnsureNonEmpty(candidateAgentId, "candidateAgentId");
        var candidate = await GetCandidateAsync(candidateAgentId, cancellationToken);
        var currentVersion = AgentConfigurationVersion.Create(candidate);
        var interviews = await evidenceStore.ListCandidateInterviewsAsync(
            candidateAgentId,
            cancellationToken);
        return AgentRecruitingReadinessProjector.Project(
            candidateAgentId,
            currentVersion,
            interviews);
    }

    private async Task<AgentDefinition> GetCandidateAsync(
        Guid candidateAgentId,
        CancellationToken cancellationToken)
    {
        var catalog = await catalogStore.LoadCatalogAsync(cancellationToken);
        return catalog.Agents.FirstOrDefault(item => item.Id == candidateAgentId)
            ?? throw Failure(
                AgentRecruitingEvidenceFailureKind.NotFound,
                "agent-recruiting.candidate-not-found",
                "The candidate agent was not found in the current workspace.");
    }

    private async Task ValidateAutomatedEvaluationAsync(
        AgentRecruitingAutomatedEvaluation? evaluation,
        string rubricVersion,
        CancellationToken cancellationToken)
    {
        if (evaluation is null)
        {
            return;
        }

        EnsureOptionalText(evaluation.Model, "automatedEvaluation.model", 200);
        EnsureText(evaluation.RubricVersion, "automatedEvaluation.rubricVersion", 100);
        if (!string.Equals(
                evaluation.RubricVersion.Trim(),
                rubricVersion.Trim(),
                StringComparison.Ordinal))
        {
            throw Failure(
                AgentRecruitingEvidenceFailureKind.Conflict,
                "agent-recruiting.rubric-version-conflict",
                "The automated evaluation rubric version must match the attempt rubric version.");
        }

        if (evaluation.Score is < 0m or > 100m)
        {
            throw Failure(
                AgentRecruitingEvidenceFailureKind.InvalidRequest,
                "agent-recruiting.score-invalid",
                "The automated evaluation score must be between 0 and 100.");
        }

        var catalog = await catalogStore.LoadCatalogAsync(cancellationToken);
        if (evaluation.EvaluatorAgentId.HasValue &&
            catalog.Agents.All(item => item.Id != evaluation.EvaluatorAgentId.Value))
        {
            throw Failure(
                AgentRecruitingEvidenceFailureKind.NotFound,
                "agent-recruiting.evaluator-agent-not-found",
                "The evaluator agent was not found in the current workspace.");
        }

        if (evaluation.ProviderProfileId.HasValue &&
            catalog.Providers.All(item => item.Id != evaluation.ProviderProfileId.Value))
        {
            throw Failure(
                AgentRecruitingEvidenceFailureKind.NotFound,
                "agent-recruiting.evaluator-provider-not-found",
                "The evaluator provider was not found in the current workspace.");
        }
    }

}
