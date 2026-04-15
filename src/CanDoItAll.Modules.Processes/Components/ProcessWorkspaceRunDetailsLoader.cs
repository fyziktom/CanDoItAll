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
        var agentsById = (await workspaceService.ListAgentsAsync(includeTemplates: false, cancellationToken))
            .ToDictionary(item => item.Id);
        var mappedRuns = new List<ProcessExecutionRunViewModel>(executionRuns.Count);

        foreach (var executionRun in executionRuns.OrderByDescending(item => item.CreatedAtUtc))
        {
            var detail = await workspaceService.GetExecutionRunDetailAsync(executionRun.Id, cancellationToken);
            mappedRuns.Add(MapExecutionRun(detail, stepTitlesById, agentsById));
        }

        return mappedRuns;
    }

    private static ProcessExecutionRunViewModel MapExecutionRun(
        ExecutionRunDetail detail,
        IReadOnlyDictionary<Guid, string> stepTitlesById,
        IReadOnlyDictionary<Guid, AgentDefinition> agentsById)
    {
        var stepRunId = Guid.TryParse(detail.Run.ProcessStepId, out var parsedStepRunId)
            ? parsedStepRunId
            : (Guid?)null;
        var stepTitle = stepRunId.HasValue && stepTitlesById.TryGetValue(stepRunId.Value, out var resolvedStepTitle)
            ? resolvedStepTitle
            : string.Empty;
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
