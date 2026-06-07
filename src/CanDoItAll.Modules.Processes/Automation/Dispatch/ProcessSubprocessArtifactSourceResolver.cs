namespace CanDoItAll.Modules.Processes;

internal static class ProcessSubprocessArtifactSourceResolver
{
    public static ProcessArtifactRecord? ResolveSourceArtifact(
        IReadOnlyList<ProcessArtifactRecord> childArtifacts,
        IReadOnlyList<ProcessArtifactExpectation> parentExpectations,
        ProcessArtifactExpectation expectation,
        out string diagnostic)
    {
        var sourceArtifact = global::CanDoItAll.Processes.Core.Artifacts.ProcessSubprocessArtifactSourceResolver.ResolveSourceArtifact(
            childArtifacts.Select(ProcessCoreArtifactModelAdapters.ToCoreArtifactRecordSnapshot).ToList(),
            parentExpectations.Select(ProcessCoreArtifactModelAdapters.ToCoreExpectationSnapshot).ToList(),
            ProcessCoreArtifactModelAdapters.ToCoreExpectationSnapshot(expectation),
            out diagnostic);

        return sourceArtifact is null
            ? null
            : childArtifacts.FirstOrDefault(artifact => artifact.Id == sourceArtifact.Id);
    }

    public static IReadOnlyList<ProcessSubprocessOutputArtifactMapping> ResolveOutputArtifactMappings(
        IReadOnlyList<ProcessArtifactExpectation> parentExpectations)
    {
        return global::CanDoItAll.Processes.Core.Artifacts.ProcessSubprocessArtifactSourceResolver
            .ResolveOutputArtifactMappings(parentExpectations.Select(ProcessCoreArtifactModelAdapters.ToCoreExpectationSnapshot).ToList())
            .Select(mapping => new ProcessSubprocessOutputArtifactMapping(
                mapping.ParentExpectationId,
                mapping.ChildExpectationId,
                mapping.IsLegacyTextMapping))
            .ToList();
    }

    public static bool IsCompletionProjectionAllowed(ProcessArtifactExpectation expectation)
    {
        return global::CanDoItAll.Processes.Core.Artifacts.ProcessSubprocessArtifactSourceResolver.IsCompletionProjectionAllowed(
            ProcessCoreArtifactModelAdapters.ToCoreExpectationSnapshot(expectation));
    }
}

internal sealed record ProcessSubprocessOutputArtifactMapping(
    Guid ParentExpectationId,
    Guid ChildExpectationId,
    bool IsLegacyTextMapping);
