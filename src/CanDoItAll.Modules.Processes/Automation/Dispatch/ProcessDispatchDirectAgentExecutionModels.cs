namespace CanDoItAll.Modules.Processes;

internal sealed record ProcessDispatchDirectAgentExecutionInput(
    ProcessRouteCandidate Candidate,
    string Trigger,
    Func<CancellationToken, Task> RenewLeaseAsync)
{
    public ProcessRouteRunSnapshot Run => Candidate.Run;

    public ProcessRouteStepSnapshot StepRun => Candidate.StepRun;
}
