using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessArtifactProjectionCandidateState
{
    public static bool TryApplyExpectedWriteOutcome(
        ProcessProjectionMutableCandidateState candidateState,
        ProcessArtifactExpectationSnapshot expectedArtifact,
        Result<ProcessArtifactProjectionWriteResult> writeResult,
        out string errorSummary)
    {
        return TryApplyWriteOutcome(
            candidateState,
            writeResult,
            expectedArtifact.Id,
            out errorSummary);
    }

    public static bool TryApplyWriteOutcome(
        ProcessProjectionMutableCandidateState candidateState,
        Result<ProcessArtifactProjectionWriteResult> writeResult,
        Guid? expectedArtifactId,
        out string errorSummary)
    {
        if (writeResult.IsFailure)
        {
            errorSummary = string.Join(" | ", writeResult.Errors.Select(error => error.Message));
            return false;
        }

        if (writeResult.Value is not { } writeOutcome)
        {
            errorSummary = "Coordinator completed without a projection write outcome.";
            return false;
        }

        if (!TryValidateArtifactExpectationId(expectedArtifactId, writeOutcome.ArtifactExpectationId, out errorSummary))
        {
            return false;
        }

        candidateState.AddProjection(writeOutcome.ExternalReferenceKey, writeOutcome.ArtifactExpectationId);
        errorSummary = string.Empty;
        return true;
    }

    public static bool TryApplyExpectedRecordOnlyOutcome(
        ProcessProjectionMutableCandidateState candidateState,
        ProcessArtifactExpectationSnapshot expectedArtifact,
        Result<ProcessArtifactProjectionRecordOnlyResult> recordResult,
        out string errorSummary)
    {
        if (recordResult.IsFailure)
        {
            errorSummary = string.Join(" | ", recordResult.Errors.Select(error => error.Message));
            return false;
        }

        if (recordResult.Value is not { } recordOutcome)
        {
            errorSummary = "Record-only coordinator completed without an artifact record outcome.";
            return false;
        }

        if (!TryValidateArtifactExpectationId(expectedArtifact.Id, recordOutcome.ArtifactExpectationId, out errorSummary))
        {
            return false;
        }

        candidateState.AddProjection(recordOutcome.ExternalReferenceKey, recordOutcome.ArtifactExpectationId);
        errorSummary = string.Empty;
        return true;
    }

    private static bool TryValidateArtifactExpectationId(
        Guid? expectedArtifactId,
        Guid? actualArtifactExpectationId,
        out string errorSummary)
    {
        if (expectedArtifactId.HasValue && !actualArtifactExpectationId.HasValue)
        {
            errorSummary = "Coordinator completed without an artifact expectation id.";
            return false;
        }

        if (expectedArtifactId is { } expectedId &&
            actualArtifactExpectationId is { } actualId &&
            actualId != expectedId)
        {
            errorSummary = $"Coordinator returned artifact expectation id '{actualId:D}' instead of '{expectedId:D}'.";
            return false;
        }

        if (expectedArtifactId is null &&
            actualArtifactExpectationId is { } unexpectedId)
        {
            errorSummary = $"Coordinator returned unexpected artifact expectation id '{unexpectedId:D}'.";
            return false;
        }

        errorSummary = string.Empty;
        return true;
    }
}
