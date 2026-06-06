using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.Processes;

internal sealed partial class ProcessRunAutomationDispatchService
{
    internal enum ProcessStepCompletionExecutorKind
    {
        DirectAgent,
        WorkflowBackedRole,
        SubprocessParent,
        ManagerArtifactRecovery,
        Manual
    }

    internal enum ProcessArtifactExpectationMode
    {
        Narrative,
        Decision,
        Evidence,
        Deliverable,
        RuntimeProof,
        RecoveryDiagnostic
    }

    internal enum ProcessArtifactValidationStatus
    {
        Satisfied,
        Missing,
        InvalidFormat,
        InsufficientEvidence,
        StaleOrWrongRun,
        WrongProducerMode,
        PlaceholderOnly,
        ContentUnavailable,
        ContentHashMismatch
    }

    internal enum ProcessArtifactFailureOwnership
    {
        OwnOutput,
        UpstreamInput,
        RuntimeEvidence,
        ReviewDisposition
    }

    internal enum ProcessArtifactProducerKind
    {
        Unknown,
        AgentExecutionArtifact,
        WorkspaceWrite,
        ExistingManagedFile,
        AssistantResponse,
        CompletedDecision,
        ProcessMock,
        ProviderNativeBrowser,
        WorkflowRun,
        WorkflowArtifact,
        SubprocessArtifact,
        ManagerRecovery,
        Manual
    }

    internal sealed record ProcessArtifactExpectationValidationResult(
        Guid ExpectationId,
        string ExpectationTitle,
        ProcessArtifactExpectationMode Mode,
        ProcessArtifactValidationStatus Status,
        ProcessArtifactProducerKind ProducerKind,
        Guid? ArtifactRecordId,
        string AttemptedPath,
        string Diagnostic,
        string SuggestedAction,
        string Fingerprint,
        ProcessArtifactFailureOwnership FailureOwnership = ProcessArtifactFailureOwnership.OwnOutput)
    {
        public bool IsSatisfied => Status == ProcessArtifactValidationStatus.Satisfied;
    }

    internal sealed record ProcessStepCompletionFinalizerContext(
        ProcessStepCompletionExecutorKind ExecutorKind,
        DispatchCandidate Candidate,
        ProcessStepRunStatus CompletionStatus,
        string CompletionReason,
        Guid? SelectedBranchOutcomeId,
        ProcessAutomationExecutionRunDetail? ExecutionDetail,
        Guid? WorkflowRunId,
        Guid? SubprocessRunId,
        string ResponseText,
        bool ProjectExecutionArtifacts,
        bool AllowManagerArtifactRecovery,
        string Trigger,
        Func<CancellationToken, Task>? RenewLeaseAsync,
        Guid? RecoveryExecutionRunId = null,
        Guid? RecoveredForExecutionRunId = null);

    internal sealed record ProcessStepCompletionFinalizerResult(
        ProcessStepRunStatus CompletionStatus,
        string CompletionReason,
        ProcessStepBlockCause? BlockCause,
        Guid? SelectedBranchOutcomeId,
        Guid StepRunConcurrencyToken,
        IReadOnlyList<ProcessArtifactExpectationValidationResult> ArtifactValidationResults,
        ProcessStepTransitionArtifactValidationContext ArtifactValidationContext);

    internal sealed record ProcessStepTransitionArtifactValidationContext(
        ProcessStepCompletionExecutorKind ExecutorKind,
        Guid? ExecutionRunId,
        Guid? WorkflowRunId,
        Guid? SubprocessRunId,
        Guid? RecoveryExecutionRunId,
        Guid? RecoveredForExecutionRunId);

    private sealed record RuntimeInvariantViolation(
        ProcessConformanceSeverity Severity,
        string Code,
        string Observation,
        string DeviationReason);

    private sealed record ProcessArtifactValidationDiagnosticPayload(
        Guid ProcessRunId,
        Guid StepRunId,
        Guid ExpectationId,
        string ExpectationTitle,
        ProcessArtifactExpectationMode Mode,
        ProcessArtifactValidationStatus Status,
        ProcessArtifactProducerKind ProducerKind,
        ProcessArtifactFailureOwnership FailureOwnership,
        Guid? ArtifactRecordId,
        string AttemptedPath,
        string Diagnostic,
        string SuggestedAction,
        string Fingerprint,
        ProcessStepCompletionExecutorKind ExecutorKind,
        Guid? ExecutionRunId,
        Guid? WorkflowRunId,
        Guid? SubprocessRunId,
        DateTimeOffset CreatedAtUtc);
}
