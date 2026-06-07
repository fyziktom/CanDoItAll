namespace CanDoItAll.Processes.Core.Artifacts;

public static class ProcessArtifactExpectationMatcher
{
    public static Guid? MatchStrongExpectedArtifactId(
        IReadOnlyList<ProcessArtifactExpectationSnapshot> expectedArtifacts,
        ProcessCoreArtifactKind expectedKind,
        Func<ProcessArtifactExpectationSnapshot, bool> matchesExpectedArtifact)
    {
        return MatchStrongExpectedArtifactId(
            expectedArtifacts,
            expectedKind,
            matchesExpectedArtifact,
            static item => item.Id,
            static item => item.ArtifactKind);
    }

    public static Guid? MatchStrongExpectedArtifactId<TExpectation>(
        IReadOnlyList<TExpectation> expectedArtifacts,
        ProcessCoreArtifactKind expectedKind,
        Func<TExpectation, bool> matchesExpectedArtifact,
        Func<TExpectation, Guid> resolveId,
        Func<TExpectation, ProcessCoreArtifactKind> resolveKind)
    {
        ArgumentNullException.ThrowIfNull(expectedArtifacts);
        ArgumentNullException.ThrowIfNull(matchesExpectedArtifact);
        ArgumentNullException.ThrowIfNull(resolveId);
        ArgumentNullException.ThrowIfNull(resolveKind);

        var strongMatches = expectedArtifacts
            .Where(matchesExpectedArtifact)
            .ToList();
        if (strongMatches.Count == 1)
        {
            return resolveId(strongMatches[0]);
        }

        if (strongMatches.Count <= 1)
        {
            return null;
        }

        var kindMatches = strongMatches
            .Where(item => resolveKind(item) == expectedKind)
            .ToList();

        return kindMatches.Count == 1
            ? resolveId(kindMatches[0])
            : null;
    }
}
