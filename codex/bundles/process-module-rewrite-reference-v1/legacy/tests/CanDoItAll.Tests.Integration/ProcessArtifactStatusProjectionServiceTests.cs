using CanDoItAll.Modules.Processes;

namespace CanDoItAll.Tests.Integration;

public sealed class ProcessArtifactStatusProjectionServiceTests {
    [Theory]
    [InlineData(
        nameof(ProcessRunAutomationDispatchService.ProcessArtifactValidationStatus.Satisfied),
        ProcessArtifactExpectationSatisfactionStatus.Satisfied,
        ProcessArtifactExpectationValidationStatus.Satisfied)]
    [InlineData(
        nameof(ProcessRunAutomationDispatchService.ProcessArtifactValidationStatus.Missing),
        ProcessArtifactExpectationSatisfactionStatus.Missing,
        ProcessArtifactExpectationValidationStatus.Missing)]
    [InlineData(
        nameof(ProcessRunAutomationDispatchService.ProcessArtifactValidationStatus.InvalidFormat),
        ProcessArtifactExpectationSatisfactionStatus.InvalidFormat,
        ProcessArtifactExpectationValidationStatus.InvalidFormat)]
    [InlineData(
        nameof(ProcessRunAutomationDispatchService.ProcessArtifactValidationStatus.InsufficientEvidence),
        ProcessArtifactExpectationSatisfactionStatus.InsufficientEvidence,
        ProcessArtifactExpectationValidationStatus.InsufficientEvidence)]
    [InlineData(
        nameof(ProcessRunAutomationDispatchService.ProcessArtifactValidationStatus.StaleOrWrongRun),
        ProcessArtifactExpectationSatisfactionStatus.StaleOrWrongRun,
        ProcessArtifactExpectationValidationStatus.StaleOrWrongRun)]
    [InlineData(
        nameof(ProcessRunAutomationDispatchService.ProcessArtifactValidationStatus.WrongProducerMode),
        ProcessArtifactExpectationSatisfactionStatus.WrongProducerMode,
        ProcessArtifactExpectationValidationStatus.WrongProducerMode)]
    [InlineData(
        nameof(ProcessRunAutomationDispatchService.ProcessArtifactValidationStatus.PlaceholderOnly),
        ProcessArtifactExpectationSatisfactionStatus.PlaceholderOnly,
        ProcessArtifactExpectationValidationStatus.PlaceholderOnly)]
    [InlineData(
        nameof(ProcessRunAutomationDispatchService.ProcessArtifactValidationStatus.ContentUnavailable),
        ProcessArtifactExpectationSatisfactionStatus.ContentUnavailable,
        ProcessArtifactExpectationValidationStatus.ContentUnavailable)]
    [InlineData(
        nameof(ProcessRunAutomationDispatchService.ProcessArtifactValidationStatus.ContentHashMismatch),
        ProcessArtifactExpectationSatisfactionStatus.ContentHashMismatch,
        ProcessArtifactExpectationValidationStatus.ContentHashMismatch)]
    public void Finalizer_status_maps_to_read_model_statuses(
        string finalizerStatusName,
        ProcessArtifactExpectationSatisfactionStatus expectedSatisfactionStatus,
        ProcessArtifactExpectationValidationStatus expectedValidationStatus) {
        var finalizerStatus = Enum.Parse<ProcessRunAutomationDispatchService.ProcessArtifactValidationStatus>(finalizerStatusName);

        Assert.Equal(
            expectedSatisfactionStatus,
            ProcessArtifactStatusProjectionService.MapFinalizerStatusToSatisfactionStatus(finalizerStatus));
        Assert.Equal(
            expectedValidationStatus,
            ProcessArtifactStatusProjectionService.MapFinalizerStatusToValidationStatus(finalizerStatus));
    }

    [Theory]
    [InlineData(ProcessArtifactExpectationSatisfactionStatus.Expected, false)]
    [InlineData(ProcessArtifactExpectationSatisfactionStatus.Satisfied, false)]
    [InlineData(ProcessArtifactExpectationSatisfactionStatus.AutoProjected, false)]
    [InlineData(ProcessArtifactExpectationSatisfactionStatus.NotApplicable, false)]
    [InlineData(ProcessArtifactExpectationSatisfactionStatus.Missing, true)]
    [InlineData(ProcessArtifactExpectationSatisfactionStatus.ProjectionFailed, true)]
    [InlineData(ProcessArtifactExpectationSatisfactionStatus.ContentUnavailable, true)]
    [InlineData(ProcessArtifactExpectationSatisfactionStatus.InvalidFormat, true)]
    [InlineData(ProcessArtifactExpectationSatisfactionStatus.InsufficientEvidence, true)]
    [InlineData(ProcessArtifactExpectationSatisfactionStatus.StaleOrWrongRun, true)]
    [InlineData(ProcessArtifactExpectationSatisfactionStatus.WrongProducerMode, true)]
    [InlineData(ProcessArtifactExpectationSatisfactionStatus.PlaceholderOnly, true)]
    [InlineData(ProcessArtifactExpectationSatisfactionStatus.ContentHashMismatch, true)]
    public void Unsatisfied_required_status_matrix_is_shared(
        ProcessArtifactExpectationSatisfactionStatus status,
        bool expected) {
        Assert.Equal(expected, ProcessArtifactStatusProjectionService.IsUnsatisfiedRequiredStatus(status));
    }
}
