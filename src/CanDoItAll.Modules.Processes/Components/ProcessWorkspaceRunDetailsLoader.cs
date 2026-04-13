namespace CanDoItAll.Modules.Processes;

public sealed class ProcessWorkspaceRunDetailsLoader(ProcessesService processesService)
{
    public async Task<ProcessWorkspaceRunDetails> LoadAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        var stepRuns = await processesService.ListStepRunsAsync(runId, cancellationToken);
        var decisions = await processesService.ListDecisionRecordsAsync(runId, cancellationToken);
        var artifacts = await processesService.ListArtifactsAsync(runId, cancellationToken);
        var assignments = await processesService.ListAssignmentsAsync(runId, cancellationToken);
        var workBriefs = await processesService.ListWorkBriefsAsync(runId, cancellationToken);
        var conformanceObservations = await processesService.ListConformanceObservationsAsync(runId, cancellationToken);

        return new ProcessWorkspaceRunDetails(
            stepRuns,
            decisions,
            artifacts,
            assignments,
            workBriefs,
            conformanceObservations);
    }
}

public sealed record ProcessWorkspaceRunDetails(
    IReadOnlyList<ProcessStepRunViewModel> StepRuns,
    IReadOnlyList<ProcessDecisionViewModel> Decisions,
    IReadOnlyList<ProcessArtifactViewModel> Artifacts,
    IReadOnlyList<ProcessRunAssignmentViewModel> Assignments,
    IReadOnlyList<ProcessWorkBriefViewModel> WorkBriefs,
    IReadOnlyList<ProcessConformanceObservationViewModel> ConformanceObservations);
