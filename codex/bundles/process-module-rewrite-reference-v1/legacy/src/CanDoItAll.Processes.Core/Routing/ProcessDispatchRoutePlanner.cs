namespace CanDoItAll.Processes.Core.Routing;

public enum ProcessDispatchRouteKind
{
    Continue,
    DatabaseRequirement,
    UpstreamMaterialization,
    StrandedRecovery,
    Subprocess,
    Workflow,
    AgentExecution
}

public readonly record struct ProcessDispatchRouteDecision(ProcessDispatchRouteKind Kind)
{
    public static ProcessDispatchRouteDecision Continue { get; } = new(ProcessDispatchRouteKind.Continue);

    public static ProcessDispatchRouteDecision DatabaseRequirement { get; } = new(ProcessDispatchRouteKind.DatabaseRequirement);

    public static ProcessDispatchRouteDecision UpstreamMaterialization { get; } = new(ProcessDispatchRouteKind.UpstreamMaterialization);

    public static ProcessDispatchRouteDecision StrandedRecovery { get; } = new(ProcessDispatchRouteKind.StrandedRecovery);

    public static ProcessDispatchRouteDecision Subprocess { get; } = new(ProcessDispatchRouteKind.Subprocess);

    public static ProcessDispatchRouteDecision Workflow { get; } = new(ProcessDispatchRouteKind.Workflow);

    public static ProcessDispatchRouteDecision AgentExecution { get; } = new(ProcessDispatchRouteKind.AgentExecution);
}

public enum ProcessDispatchRouteDecisionReason
{
    DatabaseRequirementFailure,
    DatabaseRequirementNotFailed,
    DatabaseRequirementIgnoredForSubprocess,
    UpstreamMaterializationRequested,
    UpstreamMaterializationNotRequested,
    StrandedRecoveryCompleted,
    StrandedRecoveryNotCompleted,
    SubprocessStep,
    NotSubprocessStep,
    WorkflowHandled,
    WorkflowNotHandled
}

public readonly record struct ProcessDispatchRouteDiagnostic(
    ProcessDispatchRouteDecision Decision,
    ProcessDispatchRouteDecisionReason Reason);

public static class ProcessDispatchRoutePlanner
{
    public static ProcessDispatchRouteDecision ResolveDatabaseRequirement(
        ProcessDispatchRouteSnapshot routeSnapshot,
        bool hasDatabaseRequirementFailure)
    {
        return routeSnapshot.UsesAgentAutomation && hasDatabaseRequirementFailure
            ? ProcessDispatchRouteDecision.DatabaseRequirement
            : ProcessDispatchRouteDecision.Continue;
    }

    public static ProcessDispatchRouteDiagnostic DiagnoseDatabaseRequirement(
        ProcessDispatchRouteSnapshot routeSnapshot,
        bool hasDatabaseRequirementFailure)
    {
        var decision = ResolveDatabaseRequirement(routeSnapshot, hasDatabaseRequirementFailure);
        var reason = decision.Kind == ProcessDispatchRouteKind.DatabaseRequirement
            ? ProcessDispatchRouteDecisionReason.DatabaseRequirementFailure
            : routeSnapshot.IsSubprocess && hasDatabaseRequirementFailure
                ? ProcessDispatchRouteDecisionReason.DatabaseRequirementIgnoredForSubprocess
                : ProcessDispatchRouteDecisionReason.DatabaseRequirementNotFailed;

        return new ProcessDispatchRouteDiagnostic(decision, reason);
    }

    public static ProcessDispatchRouteDecision ResolveUpstreamMaterialization(bool materializationRequested)
    {
        return materializationRequested
            ? ProcessDispatchRouteDecision.UpstreamMaterialization
            : ProcessDispatchRouteDecision.Continue;
    }

    public static ProcessDispatchRouteDiagnostic DiagnoseUpstreamMaterialization(bool materializationRequested)
    {
        return new ProcessDispatchRouteDiagnostic(
            ResolveUpstreamMaterialization(materializationRequested),
            materializationRequested
                ? ProcessDispatchRouteDecisionReason.UpstreamMaterializationRequested
                : ProcessDispatchRouteDecisionReason.UpstreamMaterializationNotRequested);
    }

    public static ProcessDispatchRouteDecision ResolveStrandedRecovery(bool recoveryCompleted)
    {
        return recoveryCompleted
            ? ProcessDispatchRouteDecision.StrandedRecovery
            : ProcessDispatchRouteDecision.Continue;
    }

    public static ProcessDispatchRouteDiagnostic DiagnoseStrandedRecovery(bool recoveryCompleted)
    {
        return new ProcessDispatchRouteDiagnostic(
            ResolveStrandedRecovery(recoveryCompleted),
            recoveryCompleted
                ? ProcessDispatchRouteDecisionReason.StrandedRecoveryCompleted
                : ProcessDispatchRouteDecisionReason.StrandedRecoveryNotCompleted);
    }

    public static ProcessDispatchRouteDecision ResolveSubprocess(ProcessDispatchRouteSnapshot routeSnapshot)
    {
        return routeSnapshot.IsSubprocess
            ? ProcessDispatchRouteDecision.Subprocess
            : ProcessDispatchRouteDecision.Continue;
    }

    public static ProcessDispatchRouteDiagnostic DiagnoseSubprocess(ProcessDispatchRouteSnapshot routeSnapshot)
    {
        return new ProcessDispatchRouteDiagnostic(
            ResolveSubprocess(routeSnapshot),
            routeSnapshot.IsSubprocess
                ? ProcessDispatchRouteDecisionReason.SubprocessStep
                : ProcessDispatchRouteDecisionReason.NotSubprocessStep);
    }

    public static ProcessDispatchRouteDecision ResolveWorkflow(bool workflowHandled)
    {
        return workflowHandled
            ? ProcessDispatchRouteDecision.Workflow
            : ProcessDispatchRouteDecision.AgentExecution;
    }

    public static ProcessDispatchRouteDiagnostic DiagnoseWorkflow(bool workflowHandled)
    {
        return new ProcessDispatchRouteDiagnostic(
            ResolveWorkflow(workflowHandled),
            workflowHandled
                ? ProcessDispatchRouteDecisionReason.WorkflowHandled
                : ProcessDispatchRouteDecisionReason.WorkflowNotHandled);
    }
}
