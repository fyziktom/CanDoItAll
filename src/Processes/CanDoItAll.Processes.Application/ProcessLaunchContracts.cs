using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Builder;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Processes.Templates;

namespace CanDoItAll.Processes.Application;

public sealed record ProcessLaunchRequest(
    string? DefinitionKey,
    ProcessDefinitionId? ProcessDefinitionId,
    string? LiveRunProfileKey,
    Guid? ProjectId,
    string? ProjectNodeId,
    string RequestedBy,
    IReadOnlyDictionary<string, string> Variables,
    bool RunReadiness,
    bool Execute)
{
    public IReadOnlyList<ProcessLaunchExecutorOverride> ExecutorOverrides { get; init; } = [];
    public ProcessRunId? RootRunIdOverride { get; init; }
}

public sealed record ProcessExistingLaunchLookupRequest(
    string DefinitionKey,
    string? LiveRunProfileKey,
    Guid? ProjectId,
    IReadOnlyDictionary<string, string> RequiredLaunchVariables);

public sealed record ProcessLaunchExecutorOverride(
    string StepKey,
    string RoleKey,
    string ExecutorKind,
    string ExecutorId,
    string ExecutorDisplayName,
    string AssignmentReason)
{
    public ProcessWorkflowExecutorBinding? WorkflowBinding { get; init; }
}

public sealed record ProcessLaunchResult
{
    public ProcessLaunchResult(
        ProcessDefinitionId DefinitionId,
        ProcessInstancePlanId LaunchPlanId,
        ProcessRunId? RunId,
        ProcessLaunchStage Stage,
        string Route,
        ProcessLaunchPlanView LaunchPlan,
        IReadOnlyList<string> Warnings)
    {
        this.DefinitionId = DefinitionId;
        this.LaunchPlanId = LaunchPlanId;
        this.RunId = RunId;
        this.Stage = Stage;
        this.Route = Route;
        this.LaunchPlan = LaunchPlan;
        this.Warnings = ProcessPublicReceiptTextPolicy.NormalizePublicMessages(Warnings);
    }

    public ProcessDefinitionId DefinitionId { get; }

    public ProcessInstancePlanId LaunchPlanId { get; }

    public ProcessRunId? RunId { get; }

    public ProcessLaunchStage Stage { get; }

    public string Route { get; }

    public ProcessLaunchPlanView LaunchPlan { get; }

    public IReadOnlyList<string> Warnings { get; }
}

public enum ProcessLaunchStage
{
    Planned,
    Blocked,
    Running,
    Completed,
    Failed
}

public sealed record ProcessLaunchPlanView(
    ProcessInstancePlanId PlanId,
    ProcessDefinitionId DefinitionId,
    ProcessDefinitionVersionId DefinitionVersionId,
    string DefinitionKey,
    string DefinitionName,
    string DefinitionSummary,
    string? LiveRunProfileKey,
    string PlanHash,
    IReadOnlyList<ProcessLaunchStepView> Steps,
    IReadOnlyList<ProcessLaunchReadinessFinding> ReadinessFindings);

public sealed record ProcessLaunchStepView(
    ProcessStepInstanceId StepInstanceId,
    string StepKey,
    string Title,
    string RoleKey,
    string ExecutorKind,
    string ExecutorId,
    string ExecutorDisplayName,
    bool IsBlocked,
    string? BlockedReason,
    ProcessRuntimeBranchGate? BranchGate)
{
    public ProcessWorkflowExecutorBinding? WorkflowBinding { get; init; }

    public IReadOnlyList<string> AllowedOperations { get; init; } = [];

    public string OperationTargetScope { get; init; } = string.Empty;

    public string RoleResourceKey { get; init; } = string.Empty;

    public string RoleDisplayName { get; init; } = string.Empty;

    public ProcessCapabilityScope CapabilityScope { get; init; } = ProcessCapabilityScope.Empty;

    public IReadOnlyList<string> RequiredRuntimeToolNames { get; init; } = [];
}

public sealed record ProcessLaunchReadinessFinding
{
    public ProcessLaunchReadinessFinding(
        ProcessLaunchReadinessSeverity severity,
        string code,
        string message,
        string? stepKey = null,
        string? roleKey = null)
    {
        Severity = severity;
        Code = ProcessPublicReceiptTextPolicy.NormalizePublicToken(
            code,
            "process.launch.readiness_invalid");
        Message = ProcessPublicReceiptTextPolicy.NormalizePublicMessage(
            message,
            "Process launch readiness could not be established.");
        StepKey = ProcessPublicReceiptTextPolicy.NormalizeOptionalPublicToken(stepKey);
        RoleKey = ProcessPublicReceiptTextPolicy.NormalizeOptionalPublicToken(roleKey);
    }

    public ProcessLaunchReadinessSeverity Severity { get; }

    public string Code { get; }

    public string Message { get; }

    public string? StepKey { get; }

    public string? RoleKey { get; }

    public bool MustBlockLaunch { get; init; }
}

public enum ProcessLaunchReadinessSeverity
{
    Info,
    Warning,
    Error
}

public sealed record ProcessLaunchExecutorResolutionRequest(
    ProcessTemplateDefinitionDocument Definition,
    ProcessInstancePlan Plan,
    ProcessTemplateLiveRunProfileDocument? LiveRunProfile,
    IReadOnlyDictionary<string, string> Variables)
{
    public IReadOnlyList<ProcessLaunchExecutorOverride> ExecutorOverrides { get; init; } = [];

    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> StepVariablesByKey { get; init; } =
        new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
}

public sealed record ProcessLaunchExecutorResolution(
    IReadOnlyList<ProcessLaunchExecutorBinding> Bindings,
    IReadOnlyList<ProcessLaunchReadinessFinding> Findings)
{
    public IReadOnlyDictionary<string, IReadOnlySet<ProcessHostCapabilityId>> EffectiveHostCapabilitiesByStep { get; init; } =
        new Dictionary<string, IReadOnlySet<ProcessHostCapabilityId>>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, IReadOnlyList<string>> EffectiveRuntimeToolNamesByStep { get; init; } =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);

    public ProcessHostCapabilitySnapshot? HostCapabilities { get; init; }
}

public sealed record ProcessLaunchExecutorBinding(
    string StepKey,
    string RoleKey,
    string ExecutorKind,
    string ExecutorId,
    string ExecutorDisplayName,
    string ReadinessHash,
    string AssignmentReason)
{
    public ProcessWorkflowExecutorBinding? WorkflowBinding { get; init; }
}

public interface IProcessLaunchExecutorResolver
{
    ValueTask<ProcessLaunchExecutorResolution> ResolveAsync(
        ProcessLaunchExecutorResolutionRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ProcessLaunchDriverCatalog(
    ProcessDriverCatalog DriverCatalog,
    StrategyId StepExecutionStrategyId,
    IReadOnlySet<CapabilityTag> RequiredCapabilityTags)
{
    public ProcessHostCapabilitySnapshot HostCapabilities { get; init; } = ProcessHostCapabilitySnapshot.Unknown;
}

public interface IProcessLaunchDriverCatalogProvider
{
    ValueTask<ProcessLaunchDriverCatalog> LoadAsync(
        CancellationToken cancellationToken = default);
}

public interface IProcessRuntimeStrategyFactoryResolver
{
    ValueTask<IProcessStrategyFactory> ResolveAsync(
        ProcessStrategyBindingSnapshot binding,
        CancellationToken cancellationToken = default);
}

public static class ProcessLaunchExecutorKinds
{
    public const string Agent = "agent";
    public const string AiAgent = "ai agent";
    public const string Person = "person";
    public const string PersonOrAgent = "person-or-agent";
    public const string Workflow = "workflow";

    public static bool CanResolveAsAgent(string executorKind)
    {
        var normalized = NormalizeExecutorKind(executorKind);
        return normalized is "agent" or "aiagent" or "personoragent";
    }

    public static bool IsWorkflow(string executorKind)
        => string.Equals(
            NormalizeExecutorKind(executorKind),
            Workflow,
            StringComparison.Ordinal);

    private static string NormalizeExecutorKind(string executorKind)
        => string.IsNullOrWhiteSpace(executorKind)
            ? string.Empty
            : new string(executorKind.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
}

public static class ProcessBranchSignalCodes
{
    public const string OutcomePrefix = "process.branch.outcome:";

    public static ManagerSignalCode Outcome(string outcomeKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outcomeKey);
        return new ManagerSignalCode(OutcomePrefix + outcomeKey.Trim());
    }

    public static bool TryReadOutcome(ManagerSignal signal, out string outcomeKey)
    {
        ArgumentNullException.ThrowIfNull(signal);

        if (signal.Code.Value.StartsWith(OutcomePrefix, StringComparison.Ordinal))
        {
            outcomeKey = signal.Code.Value[OutcomePrefix.Length..].Trim();
            return !string.IsNullOrWhiteSpace(outcomeKey);
        }

        outcomeKey = string.Empty;
        return false;
    }
}
