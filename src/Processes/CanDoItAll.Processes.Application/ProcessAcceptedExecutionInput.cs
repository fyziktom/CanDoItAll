using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Application;

public sealed record ProcessAcceptedExecutionInput(ProcessLaunchAdmissionId AdmissionId, ProcessRunId RootRunId,
    string PreparationFingerprint, string InputFingerprint, ProcessLaunchAuthority Source, ProcessRuntimeStepAssignment Assignment) {
    public ProcessAcceptedStepRequirements? Requirements { get; init; }

    public ProcessAcceptedStepRequirements RequireRequirements() {
        var requirements = Requirements ?? throw new ProcessAcceptedStepRequirementsRequiredException(Assignment.RunId, Assignment.StepInstanceId);
        requirements.Validate();
        return requirements;
    }
}

public interface IProcessAcceptedExecutionInputReader {
    Task<ProcessAcceptedExecutionInput> ReadAsync(ProcessRunId runId, ProcessStepInstanceId stepId,
        CancellationToken cancellationToken = default);
}

public sealed class ProcessAcceptedExecutionInputRequiredException(ProcessRunId runId, ProcessStepInstanceId stepId)
    : InvalidOperationException($"Process run {runId}, step {stepId} requires captured original execution input before decision execution.") {
    public ProcessRunId RunId { get; } = runId;
    public ProcessStepInstanceId StepId { get; } = stepId;
}

public sealed class ProcessAcceptedStepRequirementsRequiredException(ProcessRunId runId, ProcessStepInstanceId stepId)
    : InvalidOperationException($"Process run {runId}, step {stepId} requires captured original step requirements before decision execution or review.") {
    public ProcessRunId RunId { get; } = runId;
    public ProcessStepInstanceId StepId { get; } = stepId;
}
