namespace CanDoItAll.Processes.Core.Artifacts;

public static class ProcessArtifactRecordedSatisfactionRules
{
    public static bool HasRecordedExpectedArtifact(
        Guid expectedArtifactId,
        IReadOnlySet<Guid> recordedArtifactExpectationIds,
        IEnumerable<Guid?> executionArtifactExpectationIds)
    {
        ArgumentNullException.ThrowIfNull(recordedArtifactExpectationIds);
        ArgumentNullException.ThrowIfNull(executionArtifactExpectationIds);

        return recordedArtifactExpectationIds.Contains(expectedArtifactId) ||
               executionArtifactExpectationIds.Any(artifactExpectationId => artifactExpectationId == expectedArtifactId);
    }
}
