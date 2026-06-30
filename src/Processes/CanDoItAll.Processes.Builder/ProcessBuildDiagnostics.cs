namespace CanDoItAll.Processes.Builder;

public sealed record ProcessBuildDiagnostic(
    string Code,
    string Message,
    ProcessBuildDiagnosticSeverity Severity = ProcessBuildDiagnosticSeverity.Error,
    string? Source = null);

public enum ProcessBuildDiagnosticSeverity
{
    Info,
    Warning,
    Error
}

public sealed record ProcessPlanCompileResult(
    bool Succeeded,
    ProcessInstancePlan? Plan,
    IReadOnlyList<ProcessBuildDiagnostic> Diagnostics)
{
    public static ProcessPlanCompileResult Success(ProcessInstancePlan plan)
    {
        return new ProcessPlanCompileResult(true, plan, []);
    }

    public static ProcessPlanCompileResult Failure(IReadOnlyList<ProcessBuildDiagnostic> diagnostics)
    {
        return new ProcessPlanCompileResult(false, null, diagnostics);
    }
}
