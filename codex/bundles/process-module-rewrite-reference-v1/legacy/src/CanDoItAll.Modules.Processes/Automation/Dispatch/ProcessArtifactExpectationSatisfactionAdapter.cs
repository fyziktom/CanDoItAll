namespace CanDoItAll.Modules.Processes;

internal static class ProcessArtifactExpectationSatisfactionAdapter
{
    public static bool SatisfiesArtifactExpectation(
        ProcessArtifactRecord artifact,
        ProcessArtifactExpectation expectation)
    {
        return global::CanDoItAll.Processes.Core.Artifacts.ProcessArtifactExpectationSatisfactionRules
            .SatisfiesArtifactExpectation(
                ProcessCoreArtifactModelAdapters.ToCoreArtifactRecordSnapshot(artifact),
                ProcessCoreArtifactModelAdapters.ToCoreExpectationSnapshot(expectation));
    }
}
