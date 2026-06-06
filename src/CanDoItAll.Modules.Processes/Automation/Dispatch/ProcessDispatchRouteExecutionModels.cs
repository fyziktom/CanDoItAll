namespace CanDoItAll.Modules.Processes;

internal enum ProcessClaimedDispatchResult
{
    DispatchComplete,
    ContinueCandidates
}

internal sealed class ProcessRouteExecutionContext
{
    public ProcessRouteExecutionContext(
        Guid processRunId,
        Guid? triggerStepRunId,
        string trigger,
        ProcessRouteDispatchClaim dispatchClaim,
        Func<CancellationToken, Task> dispatchRenewLeaseAsync,
        CancellationToken rootCancellationToken)
    {
        ProcessRunId = processRunId;
        TriggerStepRunId = triggerStepRunId;
        Trigger = trigger;
        DispatchClaim = dispatchClaim;
        DispatchRenewLeaseAsync = dispatchRenewLeaseAsync;
        RootCancellationToken = rootCancellationToken;
        DispatchCancellationToken = rootCancellationToken;
    }

    public Guid ProcessRunId { get; }

    public Guid? TriggerStepRunId { get; }

    public string Trigger { get; }

    public ProcessRouteDispatchClaim DispatchClaim { get; }

    public Func<CancellationToken, Task> DispatchRenewLeaseAsync { get; }

    public CancellationToken RootCancellationToken { get; }

    public CancellationToken DispatchCancellationToken { get; set; }

    public ProcessDispatchLeaseHeartbeat? DispatchHeartbeat { get; set; }

    public ProcessRouteCandidate? Candidate { get; set; }
}

internal enum ProcessDispatchRouteHandlerResultKind
{
    NotHandled,
    DispatchComplete,
    ContinueCandidates
}

internal readonly record struct ProcessDispatchRouteHandlerResult(
    ProcessDispatchRouteHandlerResultKind Kind)
{
    public static ProcessDispatchRouteHandlerResult NotHandled { get; } = new(
        ProcessDispatchRouteHandlerResultKind.NotHandled);

    public static ProcessDispatchRouteHandlerResult DispatchComplete { get; } = new(
        ProcessDispatchRouteHandlerResultKind.DispatchComplete);

    public static ProcessDispatchRouteHandlerResult ContinueCandidates { get; } = new(
        ProcessDispatchRouteHandlerResultKind.ContinueCandidates);

    public bool Handled => Kind != ProcessDispatchRouteHandlerResultKind.NotHandled;

    public ProcessClaimedDispatchResult ToClaimedDispatchResult()
    {
        return Kind switch
        {
            ProcessDispatchRouteHandlerResultKind.DispatchComplete => ProcessClaimedDispatchResult.DispatchComplete,
            ProcessDispatchRouteHandlerResultKind.ContinueCandidates => ProcessClaimedDispatchResult.ContinueCandidates,
            _ => throw new InvalidOperationException("A route handler result must be handled before it can be converted to a dispatch result.")
        };
    }
}

internal sealed class ProcessDispatchRouteContext(
    ProcessRouteExecutionContext execution,
    ProcessRouteCandidate candidate)
{
    public ProcessRouteExecutionContext Execution { get; } = execution;

    public ProcessRouteCandidate Candidate { get; private set; } = candidate;

    public ProcessRouteExecutionOutcome? DirectAgentExecutionOutcome { get; private set; }

    public ProcessDispatchRouteSnapshot CreateRouteSnapshot()
    {
        return ProcessDispatchRouteSnapshot.Create(
            Candidate,
            Execution.Trigger,
            Execution.TriggerStepRunId);
    }

    public void UpdateCandidate(ProcessRouteCandidate candidate)
    {
        Candidate = candidate;
        Execution.Candidate = candidate;
    }

    public void SetDirectAgentExecutionOutcome(ProcessRouteExecutionOutcome executionOutcome)
    {
        DirectAgentExecutionOutcome = executionOutcome;
    }

    public ProcessRouteExecutionOutcome GetRequiredDirectAgentExecutionOutcome(ProcessDispatchRouteStage stage)
    {
        return DirectAgentExecutionOutcome ??
            throw new InvalidOperationException($"Route stage {stage} requires a direct-agent execution outcome.");
    }
}
