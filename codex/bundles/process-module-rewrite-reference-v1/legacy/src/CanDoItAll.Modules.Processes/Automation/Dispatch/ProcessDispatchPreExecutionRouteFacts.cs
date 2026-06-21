namespace CanDoItAll.Modules.Processes;

internal sealed record ProcessDispatchPreExecutionRouteFacts(
    ProcessRouteRunSnapshot Run,
    ProcessRouteStepSnapshot StepRun,
    IReadOnlyList<ProcessRouteArtifactInput> ArtifactInputs)
{
    public static ProcessDispatchPreExecutionRouteFacts FromCandidate(ProcessRouteCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        return new ProcessDispatchPreExecutionRouteFacts(
            candidate.Run,
            candidate.StepRun,
            candidate.ArtifactInputs);
    }
}
