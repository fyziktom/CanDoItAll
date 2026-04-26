using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.Processes;

public sealed class ProcessWorkspaceRunDetailsLoader(
    ProcessesService processesService,
    IAgentFrameworkWorkspaceService workspaceService)
{
    private static readonly string RunLevelAutomationLabel = "Run-level automation";

    public async Task<ProcessWorkspaceRunDetails> LoadAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        var runDetails = await processesService.GetRunDetailsAsync(runId, cancellationToken);
        var executionRuns = await LoadExecutionRunsAsync(runId, runDetails.StepRuns, cancellationToken);
        var stepRuns = EnrichStepHealth(runDetails.StepRuns, executionRuns, runDetails.OutboxRecords);
        return runDetails with
        {
            StepRuns = stepRuns,
            ExecutionRuns = executionRuns,
            Health = BuildRunHealth(stepRuns, executionRuns, runDetails.OutboxRecords)
        };
    }

    public async Task<IReadOnlyList<ProcessActiveRunSummaryViewModel>> LoadActiveRunSummariesAsync(
        IReadOnlyList<ProcessRunListItem> runs,
        CancellationToken cancellationToken = default)
    {
        var activeRuns = runs
            .Where(run => run is not null && run.Status == ProcessRunStatus.Active)
            .OrderByDescending(item => item.UpdatedAtUtc)
            .ToList();
        if (activeRuns.Count == 0)
        {
            return [];
        }

        var agentsById = (await workspaceService.ListAgentsAsync(includeTemplates: false, cancellationToken))
            .ToDictionary(item => item.Id);
        var summaries = new List<ProcessActiveRunSummaryViewModel>();

        foreach (var run in activeRuns)
        {
            var executionRuns = await workspaceService.ListExecutionRunsAsync(
                new ExecutionRunQuery(
                    ProcessRunId: run.Id.ToString("D"),
                    Take: 200),
                cancellationToken);
            var activeExecutionRuns = executionRuns
                .Where(ExecutionRunBlocksSession)
                .OrderByDescending(item => item.UpdatedAtUtc)
                .ToList();
            var runDetails = await processesService.GetRunDetailsAsync(run.Id, cancellationToken);

            var stepTitlesById = await LoadStepTitlesByIdAsync(run.Id, activeExecutionRuns, cancellationToken);
            var activeAgents = activeExecutionRuns
                .Select(item => MapActiveAgent(item, stepTitlesById, agentsById))
                .ToList();
            var outboxRecords = runDetails.OutboxRecords;
            var deadLetteredOutboxCount = outboxRecords.Count(item => item.HealthStatus == ProcessOutboxHealthStatus.DeadLettered);
            var pendingOutboxCount = outboxRecords.Count(item => item.HealthStatus is ProcessOutboxHealthStatus.Pending or ProcessOutboxHealthStatus.Leased or ProcessOutboxHealthStatus.WaitingToRetry);
            var blockedOrFailedStepCount = runDetails.StepRuns.Count(item => item.Status is ProcessStepRunStatus.Blocked or ProcessStepRunStatus.Failed);

            summaries.Add(new ProcessActiveRunSummaryViewModel(
                run.Id,
                run.Name,
                run.Status,
                run.UpdatedAtUtc,
                activeAgents.Count,
                activeExecutionRuns.Sum(item => item.PendingApprovals.Count))
            {
                Agents = activeAgents,
                PendingOutboxCount = pendingOutboxCount,
                DeadLetteredOutboxCount = deadLetteredOutboxCount,
                BlockedOrFailedStepCount = blockedOrFailedStepCount,
                HealthSummary = BuildActiveRunHealthSummary(activeAgents.Count, pendingOutboxCount, deadLetteredOutboxCount, blockedOrFailedStepCount)
            });
        }

        return summaries
            .OrderByDescending(item => item.UpdatedAtUtc)
            .ToList();
    }

    private async Task<IReadOnlyList<ProcessExecutionRunViewModel>> LoadExecutionRunsAsync(
        Guid runId,
        IReadOnlyList<ProcessStepRunViewModel> stepRuns,
        CancellationToken cancellationToken)
    {
        var executionRuns = await workspaceService.ListExecutionRunsAsync(
            new(
                ProcessRunId: runId.ToString("D"),
                Take: 200),
            cancellationToken);
        if (executionRuns.Count == 0)
        {
            return [];
        }

        var stepTitlesById = stepRuns.ToDictionary(item => item.Id, item => item.Title);
        var stepRunsById = stepRuns.ToDictionary(item => item.Id);
        var latestExecutionRunIdsByStepId = executionRuns
            .Select(
                item => new
                {
                    RunId = item.Id,
                    StepRunId = Guid.TryParse(item.ProcessStepId, out var parsedStepRunId)
                        ? parsedStepRunId
                        : (Guid?)null,
                    SortAtUtc = item.CompletedAtUtc ?? item.UpdatedAtUtc
                })
            .Where(item => item.StepRunId.HasValue)
            .GroupBy(item => item.StepRunId!.Value)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(item => item.SortAtUtc)
                    .ThenByDescending(item => item.RunId)
                    .Select(item => item.RunId)
                    .First());
        var agentsById = (await workspaceService.ListAgentsAsync(includeTemplates: false, cancellationToken))
            .ToDictionary(item => item.Id);
        var mappedRuns = new List<ProcessExecutionRunViewModel>(executionRuns.Count);

        foreach (var executionRun in executionRuns.OrderByDescending(item => item.CreatedAtUtc))
        {
            var detail = await workspaceService.GetExecutionRunDetailAsync(executionRun.Id, cancellationToken);
            mappedRuns.Add(MapExecutionRun(detail, stepTitlesById, stepRunsById, latestExecutionRunIdsByStepId, agentsById));
        }

        return mappedRuns;
    }

    private async Task<IReadOnlyDictionary<Guid, string>> LoadStepTitlesByIdAsync(
        Guid runId,
        IReadOnlyCollection<ExecutionRunRecord> executionRuns,
        CancellationToken cancellationToken)
    {
        if (!executionRuns.Any(item => Guid.TryParse(item.ProcessStepId, out _)))
        {
            return new Dictionary<Guid, string>();
        }

        return (await processesService.ListStepRunsAsync(runId, cancellationToken))
            .ToDictionary(item => item.Id, item => item.Title);
    }

    private static ProcessExecutionRunViewModel MapExecutionRun(
        ExecutionRunDetail detail,
        IReadOnlyDictionary<Guid, string> stepTitlesById,
        IReadOnlyDictionary<Guid, ProcessStepRunViewModel> stepRunsById,
        IReadOnlyDictionary<Guid, Guid> latestExecutionRunIdsByStepId,
        IReadOnlyDictionary<Guid, AgentDefinition> agentsById)
    {
        var stepRunId = Guid.TryParse(detail.Run.ProcessStepId, out var parsedStepRunId)
            ? parsedStepRunId
            : (Guid?)null;
        var stepTitle = stepRunId.HasValue && stepTitlesById.TryGetValue(stepRunId.Value, out var resolvedStepTitle)
            ? resolvedStepTitle
            : string.Empty;
        var stepRun = stepRunId.HasValue && stepRunsById.TryGetValue(stepRunId.Value, out var resolvedStepRun)
            ? resolvedStepRun
            : null;
        var isLatestRunForStep = stepRunId.HasValue &&
                                 latestExecutionRunIdsByStepId.TryGetValue(stepRunId.Value, out var latestExecutionRunId) &&
                                 latestExecutionRunId == detail.Run.Id;
        var displayProjection = ProcessExecutionRunDisplayProjector.Resolve(detail.Run, stepRun, isLatestRunForStep);
        agentsById.TryGetValue(detail.Run.AgentId, out var agent);

        return new ProcessExecutionRunViewModel(
            detail.Run.Id,
            detail.Run.AgentId,
            stepRunId,
            stepTitle,
            agent?.Name ?? detail.Run.AgentId.ToString("D"),
            agent?.RoleTitle ?? string.Empty,
            string.IsNullOrWhiteSpace(detail.Run.Title)
                ? string.IsNullOrWhiteSpace(stepTitle)
                    ? "Technical execution"
                    : stepTitle
                : detail.Run.Title,
            detail.Run.ProviderName,
            detail.Run.Model,
            detail.Run.State,
            detail.Run.Outcome,
            detail.Run.InputSummary,
            detail.Run.ResultSummary,
            detail.Run.CreatedAtUtc,
            detail.Run.UpdatedAtUtc,
            detail.Run.StartedAtUtc,
            detail.Run.CompletedAtUtc,
            detail.ExecutionLog.Count)
        {
            StatusBadgeText = displayProjection.StatusBadgeText,
            StatusTone = displayProjection.StatusTone,
            RawStatusBadgeText = displayProjection.RawStatusBadgeText,
            StatusDetail = displayProjection.StatusDetail,
            HasBrowserEvidenceToolInvocation = HasBrowserEvidenceToolInvocation(detail.ExecutionLog),
            Approvals = detail.Approvals
                .OrderByDescending(item => item.RequestedAtUtc)
                .Select(
                    item => new ProcessExecutionApprovalViewModel(
                        item.ApprovalId,
                        item.ToolName,
                        item.ToolKind,
                        item.Status,
                        item.Details,
                        item.RequestedAtUtc,
                        item.DecidedAtUtc,
                        item.DecisionNotes))
                .ToList(),
            Artifacts = detail.Artifacts
                .OrderByDescending(item => item.CreatedAtUtc)
                .Select(
                    item => new ProcessExecutionArtifactViewModel(
                        item.Id,
                        item.ArtifactKind,
                        item.DisplayName,
                        item.RelativePath,
                        item.ContentType,
                        item.ProducedBy,
                        item.Summary,
                        item.CreatedAtUtc))
                .ToList(),
            Checkpoints = detail.Checkpoints
                .OrderByDescending(item => item.CapturedAtUtc)
                .Select(
                    item => new ProcessExecutionCheckpointViewModel(
                        item.Id,
                        item.CheckpointKind,
                        item.RunState,
                        item.PendingApprovalIds.Count,
                        item.CapturedAtUtc,
                        item.ResumedAtUtc))
                .ToList(),
            ToolReceipts = detail.ToolReceipts
                .OrderByDescending(item => item.StartedAtUtc)
                .Select(
                    item => new ProcessExecutionToolReceiptViewModel(
                        item.Id,
                        item.ToolFamily,
                        item.ToolName,
                        item.RiskClass,
                        item.ApprovalMode,
                        item.IsolationGuarantee,
                        item.RequestSummary,
                        item.WorkingDirectory,
                        item.ExitSummary,
                        item.StartedAtUtc,
                        item.CompletedAtUtc))
                .ToList()
        };
    }

    private static IReadOnlyList<ProcessStepRunViewModel> EnrichStepHealth(
        IReadOnlyList<ProcessStepRunViewModel> stepRuns,
        IReadOnlyList<ProcessExecutionRunViewModel> executionRuns,
        IReadOnlyList<ProcessOutboxRecordViewModel> outboxRecords)
    {
        var attemptsByStepRunId = executionRuns
            .Where(item => item.StepRunId.HasValue)
            .GroupBy(item => item.StepRunId!.Value)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(item => item.CompletedAtUtc ?? item.UpdatedAtUtc)
                    .ThenByDescending(item => item.CreatedAtUtc)
                    .Select((item, index) => new ProcessStepExecutionAttemptViewModel(
                        item.Id,
                        item.StatusBadgeText,
                        item.StatusTone,
                        item.RawStatusBadgeText,
                        item.State,
                        item.Outcome,
                        item.CreatedAtUtc,
                        item.UpdatedAtUtc,
                        item.CompletedAtUtc,
                        index == 0))
                    .ToList());

        var outboxRecordsByStepRunId = outboxRecords
            .Where(item => item.StepRunId.HasValue)
            .GroupBy(item => item.StepRunId!.Value)
            .ToDictionary(group => group.Key, group => group.ToList());

        return stepRuns
            .Select(stepRun =>
            {
                attemptsByStepRunId.TryGetValue(stepRun.Id, out var attempts);
                attempts ??= [];
                outboxRecordsByStepRunId.TryGetValue(stepRun.Id, out var stepOutboxRecords);
                stepOutboxRecords ??= [];
                var latestAttempt = attempts.FirstOrDefault(item => item.IsLatest);
                var pendingApprovals = executionRuns
                    .Where(item => item.StepRunId == stepRun.Id)
                    .Sum(item => item.Approvals.Count(approval => approval.Status == ExecutionApprovalStatus.Pending));
                var pendingOutboxCount = stepOutboxRecords.Count(item => item.HealthStatus is ProcessOutboxHealthStatus.Pending or ProcessOutboxHealthStatus.Leased or ProcessOutboxHealthStatus.WaitingToRetry);
                var deadLetteredOutboxCount = stepOutboxRecords.Count(item => item.HealthStatus == ProcessOutboxHealthStatus.DeadLettered);
                var recoveryClassification = ResolveStepRecoveryClassification(stepRun, latestAttempt, pendingOutboxCount, deadLetteredOutboxCount);

                return stepRun with
                {
                    Health = stepRun.Health with
                    {
                        AttemptCount = attempts.Count,
                        LatestAttemptStatus = latestAttempt?.StatusBadgeText ?? string.Empty,
                        LatestAttemptTone = latestAttempt?.StatusTone ?? "neutral",
                        PendingApprovalCount = pendingApprovals,
                        RecoveryClassification = recoveryClassification,
                        ActionableReason = BuildStepActionableReason(stepRun, latestAttempt, deadLetteredOutboxCount),
                        PendingOutboxCount = pendingOutboxCount,
                        DeadLetteredOutboxCount = deadLetteredOutboxCount,
                        Attempts = attempts
                    }
                };
            })
            .ToList();
    }

    private static ProcessRecoveryClassification ResolveStepRecoveryClassification(
        ProcessStepRunViewModel stepRun,
        ProcessStepExecutionAttemptViewModel? latestAttempt,
        int pendingOutboxCount,
        int deadLetteredOutboxCount)
    {
        if (deadLetteredOutboxCount > 0)
        {
            return ProcessRecoveryClassification.OutboxDeadLetter;
        }

        if (stepRun.Health.RecoveryClassification != ProcessRecoveryClassification.None)
        {
            return stepRun.Health.RecoveryClassification;
        }

        if (pendingOutboxCount > 0 && stepRun.Status is ProcessStepRunStatus.Ready or ProcessStepRunStatus.InProgress)
        {
            return ProcessRecoveryClassification.AutomaticRetry;
        }

        if (latestAttempt is { State: ExecutionState.Failed })
        {
            return ProcessRecoveryClassification.CrashRecovery;
        }

        return ProcessRecoveryClassification.None;
    }

    private static string BuildStepActionableReason(
        ProcessStepRunViewModel stepRun,
        ProcessStepExecutionAttemptViewModel? latestAttempt,
        int deadLetteredOutboxCount)
    {
        if (deadLetteredOutboxCount > 0)
        {
            return "Automation dispatch is dead-lettered for this step. Inspect the outbox error before rerunning.";
        }

        if (!string.IsNullOrWhiteSpace(stepRun.Health.ActionableReason))
        {
            return stepRun.Health.ActionableReason;
        }

        if (latestAttempt is not null &&
            latestAttempt.State == ExecutionState.Failed)
        {
            return "The latest AgentFramework execution failed. Rerun with recovery instructions when the process contract still requires this work.";
        }

        return string.Empty;
    }

    private static ProcessRunHealthSummaryViewModel BuildRunHealth(
        IReadOnlyList<ProcessStepRunViewModel> stepRuns,
        IReadOnlyList<ProcessExecutionRunViewModel> executionRuns,
        IReadOnlyList<ProcessOutboxRecordViewModel> outboxRecords)
    {
        var missingArtifactCount = stepRuns
            .SelectMany(item => item.ArtifactExpectations)
            .Count(item => item.Status is ProcessArtifactExpectationSatisfactionStatus.Missing or ProcessArtifactExpectationSatisfactionStatus.ProjectionFailed);
        var deadLetteredOutboxCount = outboxRecords.Count(item => item.HealthStatus == ProcessOutboxHealthStatus.DeadLettered);
        var pendingOutboxCount = outboxRecords.Count(item => item.HealthStatus is ProcessOutboxHealthStatus.Pending or ProcessOutboxHealthStatus.Leased or ProcessOutboxHealthStatus.WaitingToRetry);
        var activeExecutionCount = executionRuns.Count(item =>
            item.Approvals.Any(approval => approval.Status == ExecutionApprovalStatus.Pending) ||
            item.State is ExecutionState.Preparing or
                ExecutionState.Running or
                ExecutionState.WaitingOnTool or
                ExecutionState.Persisting);
        var recoveryClassification = ResolveRunRecoveryClassification(stepRuns, deadLetteredOutboxCount, missingArtifactCount);
        return new ProcessRunHealthSummaryViewModel(
            activeExecutionCount,
            executionRuns.Count,
            executionRuns.Sum(item => item.Approvals.Count(approval => approval.Status == ExecutionApprovalStatus.Pending)),
            stepRuns.Count(item => item.Status == ProcessStepRunStatus.Blocked),
            stepRuns.Count(item => item.Status == ProcessStepRunStatus.Failed),
            stepRuns.Count(item => item.Status == ProcessStepRunStatus.WaitingApproval),
            missingArtifactCount,
            pendingOutboxCount,
            deadLetteredOutboxCount,
            recoveryClassification,
            BuildRunActionableReason(stepRuns, deadLetteredOutboxCount, missingArtifactCount));
    }

    private static ProcessRecoveryClassification ResolveRunRecoveryClassification(
        IReadOnlyList<ProcessStepRunViewModel> stepRuns,
        int deadLetteredOutboxCount,
        int missingArtifactCount)
    {
        if (deadLetteredOutboxCount > 0)
        {
            return ProcessRecoveryClassification.OutboxDeadLetter;
        }

        if (missingArtifactCount > 0)
        {
            return ProcessRecoveryClassification.MissingArtifact;
        }

        return stepRuns
            .Select(item => item.Health.RecoveryClassification)
            .FirstOrDefault(item => item != ProcessRecoveryClassification.None);
    }

    private static string BuildRunActionableReason(
        IReadOnlyList<ProcessStepRunViewModel> stepRuns,
        int deadLetteredOutboxCount,
        int missingArtifactCount)
    {
        if (deadLetteredOutboxCount > 0)
        {
            return "One or more automation dispatch records are dead-lettered.";
        }

        if (missingArtifactCount > 0)
        {
            return "One or more required artifact obligations are still missing.";
        }

        return stepRuns
            .Select(item => item.Health.ActionableReason)
            .FirstOrDefault(item => !string.IsNullOrWhiteSpace(item)) ?? string.Empty;
    }

    private static string BuildActiveRunHealthSummary(
        int activeAgentCount,
        int pendingOutboxCount,
        int deadLetteredOutboxCount,
        int blockedOrFailedStepCount)
    {
        if (deadLetteredOutboxCount > 0)
        {
            return $"{deadLetteredOutboxCount} dead-lettered automation records need attention.";
        }

        if (blockedOrFailedStepCount > 0)
        {
            return $"{blockedOrFailedStepCount} blocked or failed steps need operator review.";
        }

        if (activeAgentCount > 0)
        {
            return $"{activeAgentCount} active AgentFramework executions are attached.";
        }

        if (pendingOutboxCount > 0)
        {
            return $"{pendingOutboxCount} automation records are pending or retrying.";
        }

        return "Run is active and waiting for the next runtime handoff.";
    }

    private static ProcessActiveAgentViewModel MapActiveAgent(
        ExecutionRunRecord executionRun,
        IReadOnlyDictionary<Guid, string> stepTitlesById,
        IReadOnlyDictionary<Guid, AgentDefinition> agentsById)
    {
        Guid? stepRunId = null;
        if (Guid.TryParse(executionRun.ProcessStepId, out var parsedStepRunId))
        {
            stepRunId = parsedStepRunId;
        }

        var stepTitle = stepRunId.HasValue &&
                        stepTitlesById.TryGetValue(stepRunId.Value, out var resolvedStepTitle)
            ? resolvedStepTitle
            : RunLevelAutomationLabel;
        agentsById.TryGetValue(executionRun.AgentId, out var agent);

        return new ProcessActiveAgentViewModel(
            executionRun.Id,
            executionRun.AgentId,
            agent?.Name ?? executionRun.AgentId.ToString("D"),
            agent?.RoleTitle ?? string.Empty,
            stepTitle,
            executionRun.State,
            executionRun.Outcome,
            executionRun.UpdatedAtUtc)
        {
            StatusBadgeText = ProcessExecutionRunDisplayProjector.BuildRawBadge(executionRun.State, executionRun.Outcome),
            StatusTone = ProcessExecutionRunDisplayProjector.ResolveRawTone(executionRun.State, executionRun.Outcome)
        };
    }

    private static bool HasBrowserEvidenceToolInvocation(IReadOnlyList<ExecutionLogEntry> executionLog)
    {
        return executionLog.Any(item =>
            item.Message.Contains("Invoking tool 'browser_navigate'", StringComparison.OrdinalIgnoreCase) ||
            item.Message.Contains("Invoking tool 'browser_snapshot'", StringComparison.OrdinalIgnoreCase) ||
            item.Message.Contains("Invoking tool 'browser_take_screenshot'", StringComparison.OrdinalIgnoreCase));
    }

    private static bool ExecutionRunBlocksSession(ExecutionRunRecord run)
    {
        return run.PendingApprovals.Count > 0 ||
               run.State is ExecutionState.Preparing or
                   ExecutionState.Running or
                   ExecutionState.WaitingOnTool or
                   ExecutionState.Persisting;
    }
}

public sealed record ProcessWorkspaceRunDetails(
    IReadOnlyList<ProcessStepRunViewModel> StepRuns,
    IReadOnlyList<ProcessDecisionViewModel> Decisions,
    IReadOnlyList<ProcessArtifactViewModel> Artifacts,
    IReadOnlyList<ProcessOutboxRecordViewModel> OutboxRecords,
    IReadOnlyList<ProcessRunAssignmentViewModel> Assignments,
    IReadOnlyList<ProcessWorkBriefViewModel> WorkBriefs,
    IReadOnlyList<ProcessConformanceObservationViewModel> ConformanceObservations,
    IReadOnlyList<ProcessDirectMessageThreadViewModel> DirectMessageThreads)
{
    public IReadOnlyList<ProcessExecutionRunViewModel> ExecutionRuns { get; init; } = [];

    public ProcessRunHealthSummaryViewModel Health { get; init; } = ProcessRunHealthSummaryViewModel.Empty;
}
