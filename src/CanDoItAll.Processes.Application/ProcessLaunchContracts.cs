using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Builder;
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
    string AssignmentReason);

public sealed record ProcessLaunchResult(
    ProcessDefinitionId DefinitionId,
    ProcessInstancePlanId LaunchPlanId,
    ProcessRunId? RunId,
    ProcessLaunchStage Stage,
    string Route,
    ProcessLaunchPlanView LaunchPlan,
    IReadOnlyList<string> Warnings);

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
    ProcessRuntimeBranchGate? BranchGate);

public sealed record ProcessLaunchReadinessFinding(
    ProcessLaunchReadinessSeverity Severity,
    string Code,
    string Message,
    string? StepKey = null,
    string? RoleKey = null);

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
    IReadOnlyDictionary<string, string> Variables);

public sealed record ProcessLaunchExecutorResolution(
    IReadOnlyList<ProcessLaunchExecutorBinding> Bindings,
    IReadOnlyList<ProcessLaunchReadinessFinding> Findings);

public sealed record ProcessLaunchExecutorBinding(
    string StepKey,
    string RoleKey,
    string ExecutorKind,
    string ExecutorId,
    string ExecutorDisplayName,
    string ReadinessHash,
    string AssignmentReason);

public interface IProcessLaunchExecutorResolver
{
    ValueTask<ProcessLaunchExecutorResolution> ResolveAsync(
        ProcessLaunchExecutorResolutionRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ProcessLaunchDriverCatalog(
    ProcessDriverCatalog DriverCatalog,
    StrategyId StepExecutionStrategyId,
    IReadOnlySet<CapabilityTag> RequiredCapabilityTags);

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
