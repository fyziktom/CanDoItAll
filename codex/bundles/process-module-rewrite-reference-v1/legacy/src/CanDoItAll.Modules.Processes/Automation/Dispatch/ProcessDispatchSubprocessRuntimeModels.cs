namespace CanDoItAll.Modules.Processes;

internal sealed record ProcessDispatchSubprocessRuntimeInput(
    ProcessRouteCandidate Candidate,
    string Trigger,
    Guid? TriggerStepRunId,
    ProcessRouteDispatchClaim DispatchClaim)
{
    public ProcessRouteRunSnapshot Run => Candidate.Run;

    public ProcessRouteStepSnapshot StepRun => Candidate.StepRun;
}
