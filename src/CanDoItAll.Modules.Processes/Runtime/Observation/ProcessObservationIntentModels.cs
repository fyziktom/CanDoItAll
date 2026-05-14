namespace CanDoItAll.Modules.Processes;

public interface IProcessObservationIntentResolver
{
    Task<ProcessObservationIntentPlan> ResolveAsync(
        ProcessObservationIntent intent,
        CancellationToken cancellationToken = default);
}

public sealed record ProcessObservationIntent(
    Guid? ProjectId,
    Guid? ProcessDefinitionId,
    Guid? ProcessRunId,
    Guid? StepRunId,
    ProcessObservationFocusKind FocusKind,
    string? SearchText = null);

public sealed record ProcessObservationIntentPlan(
    ProcessObservationIntentResolutionStatus Status,
    ProcessObservationFocusKind FocusKind,
    Guid? ProcessDefinitionId,
    Guid? ProcessRunId,
    Guid? StepRunId,
    IReadOnlyList<ProcessObservationDialogDescriptor> DialogDescriptors,
    string Message)
{
    public static ProcessObservationIntentPlan Unsupported(string message)
    {
        return new ProcessObservationIntentPlan(
            ProcessObservationIntentResolutionStatus.Unsupported,
            ProcessObservationFocusKind.Dashboard,
            null,
            null,
            null,
            [],
            message);
    }

    public static ProcessObservationIntentPlan Ambiguous(string message)
    {
        return new ProcessObservationIntentPlan(
            ProcessObservationIntentResolutionStatus.Ambiguous,
            ProcessObservationFocusKind.Dashboard,
            null,
            null,
            null,
            [],
            message);
    }
}

public enum ProcessObservationIntentResolutionStatus
{
    Resolved,
    Ambiguous,
    Unsupported
}
