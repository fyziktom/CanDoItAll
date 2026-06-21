namespace CanDoItAll.Modules.Processes;

internal static class ProcessArtifactStatusProjectionService {
    public static ProcessArtifactExpectationSatisfactionStatus MapFinalizerStatusToSatisfactionStatus(
        ProcessRunAutomationDispatchService.ProcessArtifactValidationStatus status) {
        return status switch
        {
            ProcessRunAutomationDispatchService.ProcessArtifactValidationStatus.Satisfied =>
                ProcessArtifactExpectationSatisfactionStatus.Satisfied,
            ProcessRunAutomationDispatchService.ProcessArtifactValidationStatus.Missing =>
                ProcessArtifactExpectationSatisfactionStatus.Missing,
            ProcessRunAutomationDispatchService.ProcessArtifactValidationStatus.InvalidFormat =>
                ProcessArtifactExpectationSatisfactionStatus.InvalidFormat,
            ProcessRunAutomationDispatchService.ProcessArtifactValidationStatus.InsufficientEvidence =>
                ProcessArtifactExpectationSatisfactionStatus.InsufficientEvidence,
            ProcessRunAutomationDispatchService.ProcessArtifactValidationStatus.StaleOrWrongRun =>
                ProcessArtifactExpectationSatisfactionStatus.StaleOrWrongRun,
            ProcessRunAutomationDispatchService.ProcessArtifactValidationStatus.WrongProducerMode =>
                ProcessArtifactExpectationSatisfactionStatus.WrongProducerMode,
            ProcessRunAutomationDispatchService.ProcessArtifactValidationStatus.PlaceholderOnly =>
                ProcessArtifactExpectationSatisfactionStatus.PlaceholderOnly,
            ProcessRunAutomationDispatchService.ProcessArtifactValidationStatus.ContentUnavailable =>
                ProcessArtifactExpectationSatisfactionStatus.ContentUnavailable,
            ProcessRunAutomationDispatchService.ProcessArtifactValidationStatus.ContentHashMismatch =>
                ProcessArtifactExpectationSatisfactionStatus.ContentHashMismatch,
            _ => throw new InvalidOperationException($"Unsupported artifact validation status '{status}'.")
        };
    }

    public static ProcessArtifactExpectationValidationStatus MapFinalizerStatusToValidationStatus(
        ProcessRunAutomationDispatchService.ProcessArtifactValidationStatus status) {
        return status switch
        {
            ProcessRunAutomationDispatchService.ProcessArtifactValidationStatus.Satisfied =>
                ProcessArtifactExpectationValidationStatus.Satisfied,
            ProcessRunAutomationDispatchService.ProcessArtifactValidationStatus.Missing =>
                ProcessArtifactExpectationValidationStatus.Missing,
            ProcessRunAutomationDispatchService.ProcessArtifactValidationStatus.InvalidFormat =>
                ProcessArtifactExpectationValidationStatus.InvalidFormat,
            ProcessRunAutomationDispatchService.ProcessArtifactValidationStatus.InsufficientEvidence =>
                ProcessArtifactExpectationValidationStatus.InsufficientEvidence,
            ProcessRunAutomationDispatchService.ProcessArtifactValidationStatus.StaleOrWrongRun =>
                ProcessArtifactExpectationValidationStatus.StaleOrWrongRun,
            ProcessRunAutomationDispatchService.ProcessArtifactValidationStatus.WrongProducerMode =>
                ProcessArtifactExpectationValidationStatus.WrongProducerMode,
            ProcessRunAutomationDispatchService.ProcessArtifactValidationStatus.PlaceholderOnly =>
                ProcessArtifactExpectationValidationStatus.PlaceholderOnly,
            ProcessRunAutomationDispatchService.ProcessArtifactValidationStatus.ContentUnavailable =>
                ProcessArtifactExpectationValidationStatus.ContentUnavailable,
            ProcessRunAutomationDispatchService.ProcessArtifactValidationStatus.ContentHashMismatch =>
                ProcessArtifactExpectationValidationStatus.ContentHashMismatch,
            _ => throw new InvalidOperationException($"Unsupported artifact validation status '{status}'.")
        };
    }

    public static bool IsUnsatisfiedRequiredStatus(ProcessArtifactExpectationSatisfactionStatus status) {
        return status is
            ProcessArtifactExpectationSatisfactionStatus.Missing or
            ProcessArtifactExpectationSatisfactionStatus.ProjectionFailed or
            ProcessArtifactExpectationSatisfactionStatus.ContentUnavailable or
            ProcessArtifactExpectationSatisfactionStatus.InvalidFormat or
            ProcessArtifactExpectationSatisfactionStatus.InsufficientEvidence or
            ProcessArtifactExpectationSatisfactionStatus.StaleOrWrongRun or
            ProcessArtifactExpectationSatisfactionStatus.WrongProducerMode or
            ProcessArtifactExpectationSatisfactionStatus.PlaceholderOnly or
            ProcessArtifactExpectationSatisfactionStatus.ContentHashMismatch;
    }
}
