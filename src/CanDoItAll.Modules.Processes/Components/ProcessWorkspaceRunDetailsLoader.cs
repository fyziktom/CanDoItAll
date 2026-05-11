using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.Processes;

public sealed class ProcessWorkspaceRunDetailsLoader(
    ProcessesService processesService,
    IAgentFrameworkWorkspaceService workspaceService,
    IWorkflowCatalogService workflowCatalogService,
    IWorkflowRunStore workflowRunStore,
    IProcessEscalationService escalationService)
{
    private static readonly string RunLevelAutomationLabel = "Run-level automation";
    private const int ActiveRunExecutionRunsPerRun = 200;
    private const int ActiveRunExecutionRunScanMinimum = 200;
    private const int ActiveRunExecutionRunScanMaximum = 10000;

    public async Task<ProcessWorkspaceRunDetails> LoadAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        var runDetails = await processesService.GetRunDetailsAsync(runId, cancellationToken);
        var journalEscalations = await escalationService.ListAsync(runId, cancellationToken);
        var journalTimeline = await escalationService.ListJournalTimelineAsync(runId, cancellationToken);
        var executionRuns = await LoadExecutionRunsAsync(runId, runDetails.StepRuns, cancellationToken);
        var stepRuns = EnrichStepHealth(runDetails.StepRuns, executionRuns, runDetails.OutboxRecords);
        var operatorApprovals = BuildOperatorApprovals(runId, executionRuns);
        var workflowRuns = await EnrichWorkflowRunsAsync(runDetails.WorkflowRuns, cancellationToken);
        var escalations = MergeEscalations(
            journalEscalations,
            BuildOutboxEscalations(runId, runDetails.OutboxRecords, stepRuns));
        return runDetails with
        {
            StepRuns = stepRuns,
            ExecutionRuns = executionRuns,
            WorkflowRuns = workflowRuns,
            Health = BuildRunHealth(stepRuns, executionRuns, runDetails.OutboxRecords),
            Escalations = escalations,
            OperatorApprovals = operatorApprovals,
            AttemptTimeline = BuildAttemptTimeline(
                stepRuns,
                executionRuns,
                runDetails.OutboxRecords,
                escalations,
                operatorApprovals,
                journalTimeline)
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
        var runIds = activeRuns
            .Select(item => item.Id)
            .ToList();
        var runIdSet = runIds.ToHashSet();
        var activeExecutionRunsByRunId = (await workspaceService.ListExecutionRunsAsync(
                new ExecutionRunQuery(Take: ResolveActiveRunExecutionRunScanTake(activeRuns.Count)),
                cancellationToken))
            .Select(item => new ActiveRunExecutionRunMatch(TryParseProcessRunId(item.ProcessRunId), item))
            .Where(item =>
                item.ProcessRunId.HasValue &&
                runIdSet.Contains(item.ProcessRunId.Value) &&
                ExecutionRunBlocksSession(item.ExecutionRun))
            .GroupBy(item => item.ProcessRunId!.Value)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(item => item.ExecutionRun)
                    .OrderByDescending(item => item.UpdatedAtUtc)
                    .ToList());
        var healthMetricsByRunId = await processesService.GetActiveRunHealthMetricsAsync(runIds, cancellationToken);
        var summaries = new List<ProcessActiveRunSummaryViewModel>();

        foreach (var run in activeRuns)
        {
            var activeExecutionRuns = activeExecutionRunsByRunId.GetValueOrDefault(run.Id) ?? [];
            var healthMetrics = healthMetricsByRunId.GetValueOrDefault(run.Id) ??
                ProcessActiveRunHealthMetrics.Empty(run.Id);
            var activeAgents = activeExecutionRuns
                .Select(item => MapActiveAgent(item, healthMetrics.StepTitlesByStepRunId, agentsById))
                .ToList();

            summaries.Add(new ProcessActiveRunSummaryViewModel(
                run.Id,
                run.Name,
                run.Status,
                run.UpdatedAtUtc,
                activeAgents.Count,
                activeExecutionRuns.Sum(item => item.PendingApprovals.Count))
            {
                Agents = activeAgents,
                PendingOutboxCount = healthMetrics.PendingOutboxCount,
                DeadLetteredOutboxCount = healthMetrics.DeadLetteredOutboxCount,
                BlockedOrFailedStepCount = healthMetrics.BlockedOrFailedStepCount,
                HealthSummary = BuildActiveRunHealthSummary(
                    activeAgents.Count,
                    healthMetrics.PendingOutboxCount,
                    healthMetrics.DeadLetteredOutboxCount,
                    healthMetrics.BlockedOrFailedStepCount)
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

    private static IReadOnlyList<ProcessOperatorApprovalViewModel> BuildOperatorApprovals(
        Guid runId,
        IReadOnlyList<ProcessExecutionRunViewModel> executionRuns)
    {
        return executionRuns
            .SelectMany(executionRun => executionRun.Approvals
                .Select(approval => new ProcessOperatorApprovalViewModel(
                    ProcessOperatorApprovalKind.ExecutionTool,
                    runId,
                    executionRun.StepRunId,
                    executionRun.StepTitle,
                    executionRun.Id,
                    LaunchPlanId: null,
                    approval.ApprovalId,
                    string.IsNullOrWhiteSpace(approval.ToolName)
                        ? "Tool approval required"
                        : $"{approval.ToolName} approval required",
                    approval.Details,
                    string.IsNullOrWhiteSpace(executionRun.AgentName)
                        ? executionRun.Title
                        : $"{executionRun.AgentName} / {executionRun.Title}",
                    MapOperatorApprovalStatus(approval.Status),
                    approval.RequestedAtUtc,
                    approval.DecidedAtUtc,
                    approval.Status == ExecutionApprovalStatus.Pending)))
            .OrderBy(item => item.Status != ProcessOperatorApprovalStatus.Pending)
            .ThenByDescending(item => item.RequestedAtUtc)
            .ToList();
    }

    private static IReadOnlyList<ProcessEscalationViewModel> BuildOutboxEscalations(
        Guid runId,
        IReadOnlyList<ProcessOutboxRecordViewModel> outboxRecords,
        IReadOnlyList<ProcessStepRunViewModel> stepRuns)
    {
        var stepTitlesById = stepRuns.ToDictionary(item => item.Id, item => item.Title);
        return outboxRecords
            .Where(item => item.HealthStatus == ProcessOutboxHealthStatus.DeadLettered)
            .Select(item => new ProcessEscalationViewModel(
                item.Id,
                runId,
                item.StepRunId,
                item.StepRunId.HasValue ? stepTitlesById.GetValueOrDefault(item.StepRunId.Value, string.Empty) : string.Empty,
                ProcessEscalationKind.OutboxDeadLetter,
                ProcessEscalationSeverity.High,
                ProcessEscalationStatus.Open,
                ProcessEscalationSourceKind.OutboxRecord,
                "Dead-lettered automation dispatch",
                string.IsNullOrWhiteSpace(item.LastError) ? item.Trigger : item.LastError,
                Owner: string.Empty,
                Resolution: string.Empty,
                ReworkPacketId: null,
                SourceExecutionRunId: string.Empty,
                SourceApprovalId: string.Empty,
                SourceToolName: item.CommandKey,
                item.Id.ToString("N"),
                item.UpdatedAtUtc,
                item.UpdatedAtUtc,
                item.UpdatedAtUtc.AddHours(4),
                ResolvedAtUtc: null,
                UpdatedBy: "automation-outbox"))
            .ToList();
    }

    private static IReadOnlyList<ProcessEscalationViewModel> MergeEscalations(
        IReadOnlyList<ProcessEscalationViewModel> journalEscalations,
        IReadOnlyList<ProcessEscalationViewModel> outboxEscalations)
    {
        return journalEscalations
            .Concat(outboxEscalations)
            .OrderBy(item => item.Status == ProcessEscalationStatus.Resolved)
            .ThenByDescending(item => item.Severity)
            .ThenBy(item => item.DueAtUtc ?? DateTimeOffset.MaxValue)
            .ThenByDescending(item => item.UpdatedAtUtc)
            .ToList();
    }

    private static IReadOnlyList<ProcessAttemptTimelineEntryViewModel> BuildAttemptTimeline(
        IReadOnlyList<ProcessStepRunViewModel> stepRuns,
        IReadOnlyList<ProcessExecutionRunViewModel> executionRuns,
        IReadOnlyList<ProcessOutboxRecordViewModel> outboxRecords,
        IReadOnlyList<ProcessEscalationViewModel> escalations,
        IReadOnlyList<ProcessOperatorApprovalViewModel> operatorApprovals,
        IReadOnlyList<ProcessAttemptTimelineEntryViewModel> journalTimeline)
    {
        var stepTitlesById = stepRuns.ToDictionary(item => item.Id, item => item.Title);
        var timeline = new List<ProcessAttemptTimelineEntryViewModel>(journalTimeline);
        timeline.AddRange(executionRuns.Select(BuildExecutionTimelineEntry));
        timeline.AddRange(operatorApprovals.Select(BuildApprovalTimelineEntry));
        timeline.AddRange(outboxRecords.Select(item => BuildOutboxTimelineEntry(item, stepTitlesById)));
        timeline.AddRange(escalations
            .Where(item => item.SourceKind == ProcessEscalationSourceKind.OutboxRecord)
            .Select(BuildEscalationTimelineEntry));

        return timeline
            .OrderByDescending(item => item.OccurredAtUtc)
            .ThenBy(item => item.Kind)
            .ToList();
    }

    private static ProcessAttemptTimelineEntryViewModel BuildExecutionTimelineEntry(
        ProcessExecutionRunViewModel executionRun)
    {
        return new ProcessAttemptTimelineEntryViewModel(
            ProcessAttemptTimelineKind.ExecutionRun,
            executionRun.StepRunId,
            executionRun.StepTitle,
            executionRun.Id,
            OutboxRecordId: null,
            EscalationId: null,
            executionRun.Title,
            executionRun.StatusBadgeText,
            executionRun.StatusTone,
            string.IsNullOrWhiteSpace(executionRun.ResultSummary)
                ? executionRun.InputSummary
                : executionRun.ResultSummary,
            executionRun.ProviderName,
            executionRun.Model,
            $"{executionRun.ToolReceipts.Count} tool receipts / {executionRun.Artifacts.Count} artifacts / {executionRun.Checkpoints.Count} checkpoints",
            executionRun.Id.ToString("N"),
            executionRun.CompletedAtUtc ?? executionRun.UpdatedAtUtc);
    }

    private static ProcessAttemptTimelineEntryViewModel BuildApprovalTimelineEntry(
        ProcessOperatorApprovalViewModel approval)
    {
        return new ProcessAttemptTimelineEntryViewModel(
            ProcessAttemptTimelineKind.Approval,
            approval.StepRunId,
            approval.StepTitle,
            approval.ExecutionRunId,
            OutboxRecordId: null,
            EscalationId: null,
            approval.Title,
            approval.Status.ToString(),
            ResolveOperatorApprovalTone(approval.Status),
            string.IsNullOrWhiteSpace(approval.Details) ? approval.Source : approval.Details,
            ProviderName: string.Empty,
            Model: string.Empty,
            ProofSummary: approval.ExternalApprovalId,
            approval.ExecutionRunId?.ToString("N") ?? approval.ExternalApprovalId,
            approval.DecidedAtUtc ?? approval.RequestedAtUtc);
    }

    private static ProcessAttemptTimelineEntryViewModel BuildOutboxTimelineEntry(
        ProcessOutboxRecordViewModel outboxRecord,
        IReadOnlyDictionary<Guid, string> stepTitlesById)
    {
        return new ProcessAttemptTimelineEntryViewModel(
            ProcessAttemptTimelineKind.Outbox,
            outboxRecord.StepRunId,
            outboxRecord.StepRunId.HasValue ? stepTitlesById.GetValueOrDefault(outboxRecord.StepRunId.Value, string.Empty) : string.Empty,
            ExecutionRunId: null,
            outboxRecord.Id,
            EscalationId: null,
            outboxRecord.CommandKey,
            outboxRecord.HealthStatus.ToString(),
            ResolveOutboxTone(outboxRecord.HealthStatus),
            string.IsNullOrWhiteSpace(outboxRecord.LastError) ? outboxRecord.Trigger : outboxRecord.LastError,
            ProviderName: string.Empty,
            Model: string.Empty,
            ProofSummary: $"Attempts: {outboxRecord.AttemptCount}",
            outboxRecord.Id.ToString("N"),
            outboxRecord.LastAttemptAtUtc ?? outboxRecord.UpdatedAtUtc);
    }

    private static ProcessAttemptTimelineEntryViewModel BuildEscalationTimelineEntry(
        ProcessEscalationViewModel escalation)
    {
        return new ProcessAttemptTimelineEntryViewModel(
            ProcessAttemptTimelineKind.Escalation,
            escalation.StepRunId,
            escalation.StepTitle,
            ExecutionRunId: null,
            OutboxRecordId: escalation.SourceKind == ProcessEscalationSourceKind.OutboxRecord ? escalation.Id : null,
            escalation.Id,
            escalation.Title,
            escalation.Status.ToString(),
            ResolveEscalationStatusTone(escalation.Status, escalation.Severity),
            escalation.Reason,
            ProviderName: string.Empty,
            Model: string.Empty,
            ProofSummary: escalation.SourceKind.ToString(),
            escalation.CorrelationId,
            escalation.UpdatedAtUtc);
    }

    private static ProcessOperatorApprovalStatus MapOperatorApprovalStatus(ExecutionApprovalStatus status)
    {
        return status switch
        {
            ExecutionApprovalStatus.Approved => ProcessOperatorApprovalStatus.Approved,
            ExecutionApprovalStatus.Rejected => ProcessOperatorApprovalStatus.Rejected,
            _ => ProcessOperatorApprovalStatus.Pending
        };
    }

    private static string ResolveOperatorApprovalTone(ProcessOperatorApprovalStatus status)
    {
        return status switch
        {
            ProcessOperatorApprovalStatus.Approved => "mint",
            ProcessOperatorApprovalStatus.Rejected => "danger",
            ProcessOperatorApprovalStatus.ChangesRequested => "warning",
            _ => "warning"
        };
    }

    private static string ResolveEscalationStatusTone(
        ProcessEscalationStatus status,
        ProcessEscalationSeverity severity)
    {
        if (status == ProcessEscalationStatus.Resolved)
        {
            return "mint";
        }

        return severity switch
        {
            ProcessEscalationSeverity.Critical => "danger",
            ProcessEscalationSeverity.High => "danger",
            ProcessEscalationSeverity.Moderate => "warning",
            _ => "info"
        };
    }

    private static string ResolveOutboxTone(ProcessOutboxHealthStatus status)
    {
        return status switch
        {
            ProcessOutboxHealthStatus.Completed => "mint",
            ProcessOutboxHealthStatus.DeadLettered => "danger",
            ProcessOutboxHealthStatus.Leased => "info",
            ProcessOutboxHealthStatus.WaitingToRetry => "warning",
            _ => "neutral"
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

    private static int ResolveActiveRunExecutionRunScanTake(int activeRunCount)
    {
        return Math.Clamp(
            activeRunCount * ActiveRunExecutionRunsPerRun,
            ActiveRunExecutionRunScanMinimum,
            ActiveRunExecutionRunScanMaximum);
    }

    private static Guid? TryParseProcessRunId(string value)
    {
        return Guid.TryParse(value, out var parsedRunId)
            ? parsedRunId
            : null;
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
            executionRun.StartedAtUtc ?? executionRun.CreatedAtUtc,
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

    private async Task<IReadOnlyList<ProcessWorkflowRunViewModel>> EnrichWorkflowRunsAsync(
        IReadOnlyList<ProcessWorkflowRunViewModel> workflowRuns,
        CancellationToken cancellationToken)
    {
        if (workflowRuns.Count == 0)
        {
            return [];
        }

        var workflowNamesByKey = (await workflowCatalogService.ListDefinitionsAsync(cancellationToken))
            .ToDictionary(
                item => (item.Id.Value, item.VersionId.Value),
                item => item.Name);
        var enriched = new List<ProcessWorkflowRunViewModel>(workflowRuns.Count);
        foreach (var workflowRun in workflowRuns)
        {
            var runId = new WorkflowRunId(workflowRun.WorkflowRunId);
            var snapshot = await workflowRunStore.GetRunAsync(runId, cancellationToken);
            var artifacts = await workflowRunStore.ListArtifactsAsync(runId, cancellationToken);
            var pendingRequests = await workflowRunStore.ListPendingExternalRequestsAsync(runId, cancellationToken);
            enriched.Add(workflowRun with
            {
                WorkflowName = workflowNamesByKey.GetValueOrDefault(
                    (workflowRun.WorkflowDefinitionId, workflowRun.WorkflowVersionId),
                    workflowRun.WorkflowName),
                State = snapshot?.State ?? workflowRun.State,
                Summary = string.IsNullOrWhiteSpace(snapshot?.Summary)
                    ? workflowRun.Summary
                    : snapshot.Summary,
                ArtifactCount = artifacts.Count,
                PendingRequestCount = pendingRequests.Count,
                UpdatedAtUtc = snapshot?.UpdatedAtUtc ?? workflowRun.UpdatedAtUtc
            });
        }

        return enriched
            .OrderByDescending(item => item.UpdatedAtUtc)
            .ToList();
    }

    private static bool ExecutionRunBlocksSession(ExecutionRunRecord run)
    {
        return run.PendingApprovals.Count > 0 ||
               run.State is ExecutionState.Preparing or
                   ExecutionState.Running or
                   ExecutionState.WaitingOnTool or
                   ExecutionState.Persisting;
    }

    private sealed record ActiveRunExecutionRunMatch(
        Guid? ProcessRunId,
        ExecutionRunRecord ExecutionRun);
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

    public IReadOnlyList<ProcessWorkflowRunViewModel> WorkflowRuns { get; init; } = [];

    public ProcessRunHealthSummaryViewModel Health { get; init; } = ProcessRunHealthSummaryViewModel.Empty;

    public IReadOnlyList<ProcessEscalationViewModel> Escalations { get; init; } = [];

    public IReadOnlyList<ProcessOperatorApprovalViewModel> OperatorApprovals { get; init; } = [];

    public IReadOnlyList<ProcessAttemptTimelineEntryViewModel> AttemptTimeline { get; init; } = [];
}
