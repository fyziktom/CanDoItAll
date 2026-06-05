namespace CanDoItAll.Modules.Processes;

internal enum ProcessDispatchRouteKind
{
    Continue,
    DatabaseRequirement,
    UpstreamMaterialization,
    StrandedRecovery,
    Subprocess,
    Workflow,
    AgentExecution
}

internal readonly record struct ProcessDispatchRouteDecision(ProcessDispatchRouteKind Kind)
{
    public static ProcessDispatchRouteDecision Continue { get; } = new(ProcessDispatchRouteKind.Continue);

    public static ProcessDispatchRouteDecision DatabaseRequirement { get; } = new(ProcessDispatchRouteKind.DatabaseRequirement);

    public static ProcessDispatchRouteDecision UpstreamMaterialization { get; } = new(ProcessDispatchRouteKind.UpstreamMaterialization);

    public static ProcessDispatchRouteDecision StrandedRecovery { get; } = new(ProcessDispatchRouteKind.StrandedRecovery);

    public static ProcessDispatchRouteDecision Subprocess { get; } = new(ProcessDispatchRouteKind.Subprocess);

    public static ProcessDispatchRouteDecision Workflow { get; } = new(ProcessDispatchRouteKind.Workflow);

    public static ProcessDispatchRouteDecision AgentExecution { get; } = new(ProcessDispatchRouteKind.AgentExecution);
}

internal static class ProcessDispatchRoutePlanner
{
    public static ProcessDispatchRouteDecision ResolveDatabaseRequirement(
        ProcessDispatchRouteSnapshot routeSnapshot,
        bool hasDatabaseRequirementFailure)
    {
        return routeSnapshot.UsesAgentAutomation && hasDatabaseRequirementFailure
            ? ProcessDispatchRouteDecision.DatabaseRequirement
            : ProcessDispatchRouteDecision.Continue;
    }

    public static ProcessDispatchRouteDecision ResolveUpstreamMaterialization(bool materializationRequested)
    {
        return materializationRequested
            ? ProcessDispatchRouteDecision.UpstreamMaterialization
            : ProcessDispatchRouteDecision.Continue;
    }

    public static ProcessDispatchRouteDecision ResolveStrandedRecovery(bool recoveryCompleted)
    {
        return recoveryCompleted
            ? ProcessDispatchRouteDecision.StrandedRecovery
            : ProcessDispatchRouteDecision.Continue;
    }

    public static ProcessDispatchRouteDecision ResolveSubprocess(ProcessDispatchRouteSnapshot routeSnapshot)
    {
        return routeSnapshot.IsSubprocess
            ? ProcessDispatchRouteDecision.Subprocess
            : ProcessDispatchRouteDecision.Continue;
    }

    public static ProcessDispatchRouteDecision ResolveWorkflow(bool workflowHandled)
    {
        return workflowHandled
            ? ProcessDispatchRouteDecision.Workflow
            : ProcessDispatchRouteDecision.AgentExecution;
    }
}
