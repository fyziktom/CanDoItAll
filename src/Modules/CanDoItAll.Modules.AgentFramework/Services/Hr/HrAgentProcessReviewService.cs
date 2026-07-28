using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class HrAgentProcessReviewService(
    ISandboxWorkspaceExecutionStore executionStore,
    ISandboxWorkspaceExecutionRunStore executionRunStore,
    IAgentFrameworkWorkspaceService workspaceService,
    ILogger<HrAgentProcessReviewService> logger)
{
    private const int MaximumHistoryTake = 20;
    private const int MaximumAttemptsPerRun = 25;
    private const int MaximumParticipantsPerRun = 25;
    private const int MaximumQuestionLength = 2_000;
    private const string ManagerReviewRequestedByKind = "managed-hr-agent";

    public async Task<HrAgentProcessHistoryResult> GetHistoryAsync(
        HrAgentProcessHistoryInput input,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.AgentId == Guid.Empty)
        {
            throw new ArgumentException("Agent id cannot be empty.", nameof(input));
        }

        if (input.Take is < 1 or > MaximumHistoryTake)
        {
            throw new ArgumentOutOfRangeException(nameof(input), $"Take must be between 1 and {MaximumHistoryTake}.");
        }

        if (input.FromUtc.HasValue && input.ToUtc.HasValue && input.FromUtc > input.ToUtc)
        {
            throw new InvalidOperationException("FromUtc cannot be later than ToUtc.");
        }

        var agents = await workspaceService.ListAgentsAsync(includeTemplates: true, cancellationToken);
        var agent = agents.FirstOrDefault(item => item.Id == input.AgentId)
            ?? throw new InvalidOperationException($"Agent '{input.AgentId:D}' was not found.");
        var state = await executionStore.LoadExecutionAsync(cancellationToken);
        var targetRuns = state.ExecutionRuns
            .Where(run => run.AgentId == input.AgentId)
            .Where(HrAgentExecutionLineage.IsProcessStep)
            .Where(run => IsWithinWindow(run.CreatedAtUtc, input.FromUtc, input.ToUtc))
            .ToArray();
        var grouped = targetRuns
            .GroupBy(run => ParseProcessRunId(run.ProcessRunId))
            .OrderByDescending(group => group.Max(run => run.UpdatedAtUtc))
            .ToArray();
        var reviews = grouped
            .Take(input.Take)
            .Select(group => BuildRunReview(group.Key, group.ToArray(), state, agents, input.AgentId))
            .ToArray();

        return new HrAgentProcessHistoryResult(
            agent.Id,
            agent.Name,
            reviews,
            reviews.Length,
            grouped.Length > reviews.Length);
    }

    public async Task<HrAgentManagerReviewRequestResult> RequestManagerReviewAsync(
        Guid actorAgentId,
        HrAgentManagerReviewRequestInput input,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        var agents = await workspaceService.ListAgentsAsync(includeTemplates: true, cancellationToken);
        EnsureAuthorizedActor(actorAgentId, agents);
        if (input.ProcessRunId == Guid.Empty ||
            input.TargetAgentId == Guid.Empty ||
            input.ManagerAgentId == Guid.Empty)
        {
            throw new InvalidOperationException("Process run, target agent, and manager agent IDs are required.");
        }

        if (input.TargetAgentId == input.ManagerAgentId ||
            input.ManagerAgentId == actorAgentId ||
            input.TargetAgentId == actorAgentId)
        {
            throw new InvalidOperationException("The HR agent, target agent, and manager agent must be distinct participants.");
        }

        if (string.IsNullOrWhiteSpace(input.Question) || input.Question.Trim().Length > MaximumQuestionLength)
        {
            throw new InvalidOperationException($"Question is required and cannot exceed {MaximumQuestionLength} characters.");
        }

        var state = await executionStore.LoadExecutionAsync(cancellationToken);
        var processRuns = state.ExecutionRuns
            .Where(HrAgentExecutionLineage.IsProcessStep)
            .Where(run => TryParseProcessRunId(run.ProcessRunId, out var processRunId) && processRunId == input.ProcessRunId)
            .ToArray();
        if (processRuns.Length == 0)
        {
            throw new InvalidOperationException($"Process run '{input.ProcessRunId:D}' has no agent execution lineage.");
        }

        if (processRuns.All(run => run.AgentId != input.TargetAgentId))
        {
            throw new InvalidOperationException($"Target agent '{input.TargetAgentId:D}' did not participate in process run '{input.ProcessRunId:D}'.");
        }

        if (processRuns.All(run => run.AgentId != input.ManagerAgentId))
        {
            throw new InvalidOperationException($"Selected manager agent '{input.ManagerAgentId:D}' did not participate in process run '{input.ProcessRunId:D}'.");
        }

        var target = agents.FirstOrDefault(agent => agent.Id == input.TargetAgentId)
            ?? throw new InvalidOperationException($"Target agent '{input.TargetAgentId:D}' was not found.");
        var manager = agents.FirstOrDefault(agent => agent.Id == input.ManagerAgentId)
            ?? throw new InvalidOperationException($"Manager agent '{input.ManagerAgentId:D}' was not found.");
        if (!IsEligibleReviewManager(manager, target.Id))
        {
            throw new InvalidOperationException(
                "The selected manager must be a non-template Active participant with a configured provider and permission to observe other agents.");
        }

        var targetRuns = processRuns
            .Where(run => run.AgentId == target.Id)
            .OrderBy(run => run.CreatedAtUtc)
            .ToArray();
        var prompt = BuildManagerPrompt(input, target, targetRuns, state.ExecutionLog);
        var metadataJson = JsonSerializer.Serialize(new
        {
            targetAgentId = target.Id,
            managerAgentId = manager.Id
        });
        metadataJson = ExecutionInvocationMetadata.ApplyRuntimeToolProvidersEnabled(
            metadataJson,
            enabled: false);
        metadataJson = ExecutionInvocationMetadata.ApplyWorkspaceToolsEnabled(
            metadataJson,
            enabled: false);
        metadataJson = ExecutionInvocationMetadata.ApplyToolCapabilitiesEnabled(
            metadataJson,
            enabled: false);
        var invocationContext = new ExecutionInvocationContext(
            SourceKind: HrAgentExecutionLineage.ManagerReviewSourceKind,
            SourceId: input.ProcessRunId.ToString("D"),
            CorrelationId: input.ProcessRunId.ToString("D"),
            CausationId: Guid.NewGuid().ToString("D"),
            RequestedBy: actorAgentId.ToString("D"),
            RequestedByKind: ManagerReviewRequestedByKind,
            MetadataJson: metadataJson,
            ProcessRunId: input.ProcessRunId.ToString("D"));
        ExecutionRunResult result;
        Guid? reviewExecutionRunId = null;
        try
        {
            result = await workspaceService.ExecuteRunAsync(
                new ExecutionRunRequest(
                    manager.Id,
                    prompt,
                    AgentExecutionOperationId.New(),
                    Context: invocationContext),
                cancellationToken);
            reviewExecutionRunId = result.ExecutionRunId;
            var reviewDetail = await workspaceService.GetExecutionRunDetailAsync(
                result.ExecutionRunId,
                cancellationToken);
            EnsureManagerReviewCompleted(
                reviewDetail,
                manager.Id,
                input.ProcessRunId);
            await ProtectManagerReviewEvidenceAsync(reviewDetail, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            await TryProtectManagerReviewEvidenceAsync(
                reviewExecutionRunId,
                invocationContext.CausationId,
                CancellationToken.None);
            logger.LogWarning(
                "HR manager review was cancelled. ReviewExecutionRunId={ReviewExecutionRunId} ProcessRunId={ProcessRunId}.",
                reviewExecutionRunId,
                input.ProcessRunId);
            throw;
        }
        catch (Exception exception)
        {
            await TryProtectManagerReviewEvidenceAsync(
                reviewExecutionRunId,
                invocationContext.CausationId,
                CancellationToken.None);
            logger.LogError(
                exception,
                "HR manager review failed. ReviewExecutionRunId={ReviewExecutionRunId} ProcessRunId={ProcessRunId}.",
                reviewExecutionRunId,
                input.ProcessRunId);
            throw;
        }
        logger.LogInformation(
            "HR agent {ActorAgentId} requested process review from manager agent {ManagerAgentId} for target agent {TargetAgentId} and process run {ProcessRunId}. ReviewExecutionRunId={ExecutionRunId}",
            actorAgentId,
            manager.Id,
            target.Id,
            input.ProcessRunId,
            result.ExecutionRunId);

        return new HrAgentManagerReviewRequestResult(
            input.ProcessRunId,
            target.Id,
            manager.Id,
            result.ChatSessionId,
            result.ExecutionRunId,
            result.ResponseText,
            "This is a requested review from an explicitly selected participating observer; the system does not claim a persisted canonical run-manager binding.");
    }

    private async Task TryProtectManagerReviewEvidenceAsync(
        Guid? executionRunId,
        string causationId,
        CancellationToken cancellationToken)
    {
        try
        {
            var detail = executionRunId.HasValue
                ? await executionRunStore.GetExecutionRunDetailAsync(executionRunId.Value, cancellationToken)
                : await FindManagerReviewDetailAsync(causationId, cancellationToken);
            if (detail is not null)
            {
                await ProtectManagerReviewEvidenceAsync(detail, cancellationToken);
            }
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Failed to redact retained HR manager-review evidence. ReviewExecutionRunId={ReviewExecutionRunId} CausationId={CausationId}.",
                executionRunId,
                causationId);
        }
    }

    private async Task<ExecutionRunDetail?> FindManagerReviewDetailAsync(
        string causationId,
        CancellationToken cancellationToken)
    {
        var run = (await executionRunStore.ListExecutionRunsAsync(cancellationToken))
            .Where(HrAgentExecutionLineage.IsManagerReview)
            .Where(item => string.Equals(item.CausationId, causationId, StringComparison.Ordinal))
            .OrderByDescending(item => item.CreatedAtUtc)
            .FirstOrDefault();
        return run is null
            ? null
            : await executionRunStore.GetExecutionRunDetailAsync(run.Id, cancellationToken);
    }

    private async Task ProtectManagerReviewEvidenceAsync(
        ExecutionRunDetail detail,
        CancellationToken cancellationToken)
    {
        const string ProtectedTitle = "HR manager review";
        const string ProtectedInput = "Sensitive HR manager-review request redacted.";
        const string ProtectedResult = "Sensitive HR manager-review response redacted.";
        const string ProtectedLogMessage = "Sensitive HR manager-review execution event; typed state and phase retained.";

        var protectedRun = detail.Run with
        {
            Title = ProtectedTitle,
            InputSummary = ProtectedInput,
            ResultSummary = ProtectedResult,
            RuntimeSessionKey = string.Empty,
            SerializedSessionStateJson = null,
            PendingApprovals = detail.Run.PendingApprovals
                .Select(ProtectManagerReviewApproval)
                .ToArray(),
            Revision = detail.Run.Revision + 1L
        };
        var protectedDetail = new ExecutionRunDetail(
            protectedRun,
            ChatSession: null,
            detail.ExecutionLog
                .Select(entry => entry with { Message = ProtectedLogMessage })
                .ToArray(),
            detail.Metrics)
        {
            Approvals = detail.Approvals
                .Select(ProtectManagerReviewApproval)
                .ToArray(),
            Artifacts = detail.Artifacts,
            Checkpoints = detail.Checkpoints,
            ToolReceipts = detail.ToolReceipts,
            UsageObservations = detail.UsageObservations
        };
        await executionRunStore.SaveExecutionRunDetailAsync(protectedDetail, cancellationToken);
    }

    private static PendingToolApprovalRecord ProtectManagerReviewApproval(
        PendingToolApprovalRecord approval)
    {
        return approval with
        {
            Details = HrAgentExecutionRetention.ManagerReviewApprovalDetails,
            ArgumentsJson = HrAgentExecutionRetention.ManagerReviewApprovalArgumentsJson
        };
    }

    private static ExecutionApprovalRecord ProtectManagerReviewApproval(
        ExecutionApprovalRecord approval)
    {
        return approval with
        {
            Details = HrAgentExecutionRetention.ManagerReviewApprovalDetails,
            ArgumentsJson = HrAgentExecutionRetention.ManagerReviewApprovalArgumentsJson
        };
    }

    private static void EnsureManagerReviewCompleted(
        ExecutionRunDetail detail,
        Guid managerAgentId,
        Guid processRunId)
    {
        ArgumentNullException.ThrowIfNull(detail);

        var run = detail.Run;
        var pendingApprovalCount = run.PendingApprovals.Count +
                                   detail.Approvals.Count(approval => approval.Status == ExecutionApprovalStatus.Pending);
        var hasExpectedLineage = run.AgentId == managerAgentId &&
                                 HrAgentExecutionLineage.IsManagerReview(run) &&
                                 !run.ChatSessionId.HasValue &&
                                 string.IsNullOrWhiteSpace(run.ProcessStepId) &&
                                 TryParseProcessRunId(run.ProcessRunId, out var actualProcessRunId) &&
                                 actualProcessRunId == processRunId;
        if (hasExpectedLineage &&
            run.State == ExecutionState.Completed &&
            run.Outcome == RunOutcome.Succeeded &&
            pendingApprovalCount == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Manager review execution run '{run.Id:D}' did not complete successfully with the expected process lineage. " +
            $"State={run.State}; Outcome={run.Outcome?.ToString() ?? "unknown"}; PendingApprovals={pendingApprovalCount}; " +
            $"ExpectedManagerAgentId={managerAgentId:D}; ActualManagerAgentId={run.AgentId:D}; " +
            $"ExpectedProcessRunId={processRunId:D}; ActualProcessRunId={run.ProcessRunId}; " +
            $"SourceKind={run.SourceKind}; ProcessStepId={run.ProcessStepId}; ChatSessionId={run.ChatSessionId?.ToString("D") ?? "none"}.");
    }

    private static HrAgentProcessRunReview BuildRunReview(
        Guid processRunId,
        IReadOnlyList<ExecutionRunRecord> targetRuns,
        SandboxWorkspaceExecutionState state,
        IReadOnlyList<AgentDefinition> agents,
        Guid targetAgentId)
    {
        var orderedRuns = targetRuns
            .OrderBy(run => run.CreatedAtUtc)
            .ThenBy(run => run.Id)
            .ToArray();
        var attemptNumbers = orderedRuns
            .GroupBy(run => run.ProcessStepId, StringComparer.OrdinalIgnoreCase)
            .SelectMany(group => group.Select((run, index) => (run.Id, AttemptNumber: index + 1)))
            .ToDictionary(item => item.Id, item => item.AttemptNumber);
        var attempts = orderedRuns
            .Take(MaximumAttemptsPerRun)
            .Select(run => new HrAgentProcessAttempt(
                run.Id,
                run.ProcessStepId,
                attemptNumbers[run.Id],
                run.State,
                run.Outcome,
                run.CreatedAtUtc,
                run.UpdatedAtUtc,
                BuildFailureEvidence(run, state.ExecutionLog)))
            .ToArray();
        var processRuns = state.ExecutionRuns
            .Where(HrAgentExecutionLineage.IsProcessStep)
            .Where(run => TryParseProcessRunId(run.ProcessRunId, out var parsedRunId) && parsedRunId == processRunId)
            .ToArray();
        var agentLookup = agents.ToDictionary(agent => agent.Id);
        var allParticipants = processRuns
            .Select(run => run.AgentId)
            .Distinct()
            .Select(agentId => agentLookup.TryGetValue(agentId, out var agent)
                ? new HrAgentProcessParticipant(
                    agent.Id,
                    agent.Name,
                    agent.Permissions.CanObserveOtherAgents,
                    agent.Id != targetAgentId &&
                    agent.Id != HrAgentIdentity.AgentId &&
                    IsEligibleReviewManager(agent, targetAgentId))
                : new HrAgentProcessParticipant(agentId, "Unknown agent", false, false))
            .OrderBy(participant => participant.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var participants = allParticipants
            .Take(MaximumParticipantsPerRun)
            .ToArray();

        return new HrAgentProcessRunReview(
            processRunId,
            orderedRuns.Length,
            attempts.Length,
            orderedRuns.Length > attempts.Length,
            orderedRuns
                .GroupBy(run => run.ProcessStepId, StringComparer.OrdinalIgnoreCase)
                .Count(group => group.Count() > 1),
            orderedRuns.Count(run => run.Outcome == RunOutcome.Failed || run.State == ExecutionState.Failed),
            orderedRuns.Count(run => run.Outcome == RunOutcome.Succeeded),
            orderedRuns.Min(run => run.CreatedAtUtc),
            orderedRuns.Max(run => run.UpdatedAtUtc),
            attempts,
            allParticipants.Length,
            participants.Length,
            allParticipants.Length > participants.Length,
            participants);
    }

    private static string BuildFailureEvidence(
        ExecutionRunRecord run,
        IReadOnlyList<ExecutionLogEntry> executionLog)
    {
        if (run.Outcome != RunOutcome.Failed && run.State != ExecutionState.Failed)
        {
            return string.Empty;
        }

        var hasFailedLog = executionLog.Any(entry =>
            entry.ExecutionRunId == run.Id &&
            entry.State == ExecutionState.Failed);
        return $"Failure persisted for execution run {run.Id:D}; state={run.State}; outcome={run.Outcome?.ToString() ?? "unknown"}; failedLogRecorded={hasFailedLog}.";
    }

    private static string BuildManagerPrompt(
        HrAgentManagerReviewRequestInput input,
        AgentDefinition target,
        IReadOnlyList<ExecutionRunRecord> targetRuns,
        IReadOnlyList<ExecutionLogEntry> executionLog)
    {
        var failed = targetRuns.Count(run => run.Outcome == RunOutcome.Failed || run.State == ExecutionState.Failed);
        var succeeded = targetRuns.Count(run => run.Outcome == RunOutcome.Succeeded);
        var repeatedSteps = targetRuns
            .GroupBy(run => run.ProcessStepId, StringComparer.OrdinalIgnoreCase)
            .Count(group => group.Count() > 1);
        var failures = targetRuns
            .Select(run => BuildFailureEvidence(run, executionLog))
            .Where(evidence => !string.IsNullOrWhiteSpace(evidence))
            .Distinct(StringComparer.Ordinal)
            .Take(5)
            .ToArray();

        return $"""
            HR requested an evidence-grounded review of agent {target.Id:D} in process run {input.ProcessRunId:D}.

            Persisted execution facts:
            - execution attempts: {targetRuns.Count}
            - succeeded attempts: {succeeded}
            - failed attempts: {failed}
            - step IDs with more than one attempt: {repeatedSteps}
            - failure evidence: {(failures.Length == 0 ? "none persisted" : string.Join(" | ", failures))}

            Review question:
            {input.Question.Trim()}

            Separate observed facts from assessment. Do not infer protected personal traits. Runtime and workspace tools are disabled for this review; answer only from the supplied persisted evidence. If the evidence is insufficient, say so.
            """;
    }

    private static Guid ParseProcessRunId(string value)
    {
        return TryParseProcessRunId(value, out var processRunId)
            ? processRunId
            : throw new InvalidOperationException($"Persisted process run id '{value}' is invalid.");
    }

    private static bool TryParseProcessRunId(string? value, out Guid processRunId)
    {
        return Guid.TryParse(value, out processRunId) && processRunId != Guid.Empty;
    }

    private static bool IsWithinWindow(
        DateTimeOffset value,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc)
    {
        return (!fromUtc.HasValue || value >= fromUtc.Value) &&
               (!toUtc.HasValue || value <= toUtc.Value);
    }

    private static void EnsureAuthorizedActor(
        Guid actorAgentId,
        IReadOnlyList<AgentDefinition> agents)
    {
        if (actorAgentId != HrAgentIdentity.AgentId ||
            !HrAgentIdentity.Matches(agents.FirstOrDefault(agent => agent.Id == actorAgentId)))
        {
            throw new UnauthorizedAccessException("Only the managed HR agent can request manager reviews.");
        }
    }

    private static bool IsEligibleReviewManager(
        AgentDefinition agent,
        Guid targetAgentId)
    {
        return agent.Id != targetAgentId &&
               agent.Id != HrAgentIdentity.AgentId &&
               !agent.IsTemplate &&
               agent.Status == AgentLifecycleStatus.Active &&
               agent.ProviderProfileId.HasValue &&
               agent.Permissions.CanObserveOtherAgents;
    }
}
