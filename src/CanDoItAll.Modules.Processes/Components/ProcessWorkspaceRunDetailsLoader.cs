using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.Processes;

public sealed class ProcessWorkspaceRunDetailsLoader(
    ProcessesService processesService,
    IAgentFrameworkWorkspaceService workspaceService)
{
    public async Task<ProcessWorkspaceRunDetails> LoadAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        var runDetails = await processesService.GetRunDetailsAsync(runId, cancellationToken);
        var executionRuns = await LoadExecutionRunsAsync(runId, runDetails.StepRuns, cancellationToken);
        return runDetails with
        {
            ExecutionRuns = executionRuns
        };
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

    private static bool HasBrowserEvidenceToolInvocation(IReadOnlyList<ExecutionLogEntry> executionLog)
    {
        return executionLog.Any(item =>
            item.Message.Contains("Invoking tool 'browser_navigate'", StringComparison.OrdinalIgnoreCase) ||
            item.Message.Contains("Invoking tool 'browser_snapshot'", StringComparison.OrdinalIgnoreCase) ||
            item.Message.Contains("Invoking tool 'browser_take_screenshot'", StringComparison.OrdinalIgnoreCase));
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
