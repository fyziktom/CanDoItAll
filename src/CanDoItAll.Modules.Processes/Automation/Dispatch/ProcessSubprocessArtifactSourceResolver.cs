namespace CanDoItAll.Modules.Processes;

internal static class ProcessSubprocessArtifactSourceResolver
{
    public static ProcessArtifactRecord? ResolveSourceArtifact(
        IReadOnlyList<ProcessArtifactRecord> childArtifacts,
        IReadOnlyList<ProcessArtifactExpectation> parentExpectations,
        ProcessArtifactExpectation expectation,
        out string diagnostic)
    {
        return WorkflowSubprocessArtifactMapper.ResolveSubprocessSourceArtifact(
            childArtifacts,
            parentExpectations,
            expectation,
            out diagnostic);
    }

    public static IReadOnlyList<ProcessSubprocessOutputArtifactMapping> ResolveOutputArtifactMappings(
        IReadOnlyList<ProcessArtifactExpectation> parentExpectations)
    {
        return WorkflowSubprocessArtifactMapper.ResolveSubprocessOutputArtifactMappings(parentExpectations);
    }

    public static bool IsCompletionProjectionAllowed(ProcessArtifactExpectation expectation)
    {
        return WorkflowSubprocessArtifactMapper.IsSubprocessCompletionProjectionAllowed(expectation);
    }
}
