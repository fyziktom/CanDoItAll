namespace CanDoItAll.Modules.Processes;

internal static class ProcessArtifactExpectationMatcher
{
    public static Guid? MatchStrongExpectedArtifactId(
        IReadOnlyList<ProcessArtifactExpectationSnapshot> expectedArtifacts,
        ProcessArtifactKind expectedKind,
        Func<ProcessArtifactExpectationSnapshot, bool> matchesExpectedArtifact)
    {
        return global::CanDoItAll.Processes.Core.Artifacts.ProcessArtifactExpectationMatcher.MatchStrongExpectedArtifactId(
            expectedArtifacts,
            ProcessCoreArtifactModelAdapters.ToCoreArtifactKind(expectedKind),
            matchesExpectedArtifact,
            static item => item.Id,
            static item => ProcessCoreArtifactModelAdapters.ToCoreArtifactKind(item.ArtifactKind));
    }
}
