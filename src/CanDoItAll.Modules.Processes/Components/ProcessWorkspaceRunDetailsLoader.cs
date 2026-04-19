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
        return runDetails with
        {
            ExecutionRuns = executionRuns
        };
    }

    public async Task<IReadOnlyList<ProcessActiveRunSummaryViewModel>> LoadActiveRunSummariesAsync(
        IReadOnlyList<ProcessRunListItem> runs,
        CancellationToken cancellationToken = default)
    {
        if (runs.Count == 0)
        {
            return [];
        }

        var agentsById = (await workspaceService.ListAgentsAsync(includeTemplates: false, cancellationToken))
            .ToDictionary(item => item.Id);
        var summaries = new List<ProcessActiveRunSummaryViewModel>();

        foreach (var run in runs.OrderByDescending(item => item.UpdatedAtUtc))
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
            if (activeExecutionRuns.Count == 0)
            {
                continue;
            }

            var stepTitlesById = await LoadStepTitlesByIdAsync(run.Id, activeExecutionRuns, cancellationToken);
            var activeAgents = activeExecutionRuns
                .Select(item => MapActiveAgent(item, stepTitlesById, agentsById))
                .ToList();

            summaries.Add(new ProcessActiveRunSummaryViewModel(
                run.Id,
                run.Name,
                run.Status,
                run.UpdatedAtUtc,
                activeAgents.Count,
                activeExecutionRuns.Sum(item => item.PendingApprovals.Count))
            {
                Agents = activeAgents
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
    IReadOnlyList<ProcessRunAssignmentViewModel> Assignments,
    IReadOnlyList<ProcessWorkBriefViewModel> WorkBriefs,
    IReadOnlyList<ProcessConformanceObservationViewModel> ConformanceObservations,
    IReadOnlyList<ProcessDirectMessageThreadViewModel> DirectMessageThreads)
{
    public IReadOnlyList<ProcessExecutionRunViewModel> ExecutionRuns { get; init; } = [];
}
