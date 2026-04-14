namespace CanDoItAll.Modules.Processes;

public sealed class ProcessWorkspaceRunDetailsLoader(ProcessesService processesService)
{
    public Task<ProcessWorkspaceRunDetails> LoadAsync(Guid runId, CancellationToken cancellationToken = default)
        => processesService.GetRunDetailsAsync(runId, cancellationToken);
}

public sealed record ProcessWorkspaceRunDetails(
    IReadOnlyList<ProcessStepRunViewModel> StepRuns,
    IReadOnlyList<ProcessDecisionViewModel> Decisions,
    IReadOnlyList<ProcessArtifactViewModel> Artifacts,
    IReadOnlyList<ProcessRunAssignmentViewModel> Assignments,
    IReadOnlyList<ProcessWorkBriefViewModel> WorkBriefs,
    IReadOnlyList<ProcessConformanceObservationViewModel> ConformanceObservations,
    IReadOnlyList<ProcessDirectMessageThreadViewModel> DirectMessageThreads);
