namespace CanDoItAll.Processes.Core.Artifacts;

public enum ProcessArtifactExpectationMatchReason
{
    StrongMatch,
    NoStrongMatch,
    AmbiguousStrongMatch,
    KindDisambiguated,
    AmbiguousKindMatch
}

public readonly record struct ProcessArtifactExpectationMatchDiagnostic(
    Guid? MatchedArtifactId,
    ProcessArtifactExpectationMatchReason Reason,
    int StrongMatchCount,
    int KindMatchCount);

public static class ProcessArtifactExpectationMatcher
{
    public static Guid? MatchStrongExpectedArtifactId(
        IReadOnlyList<ProcessArtifactExpectationSnapshot> expectedArtifacts,
        ProcessCoreArtifactKind expectedKind,
        Func<ProcessArtifactExpectationSnapshot, bool> matchesExpectedArtifact)
    {
        return DiagnoseStrongExpectedArtifactMatch(
            expectedArtifacts,
            expectedKind,
            matchesExpectedArtifact,
            static item => item.Id,
            static item => item.ArtifactKind)
            .MatchedArtifactId;
    }

    public static Guid? MatchStrongExpectedArtifactId<TExpectation>(
        IReadOnlyList<TExpectation> expectedArtifacts,
        ProcessCoreArtifactKind expectedKind,
        Func<TExpectation, bool> matchesExpectedArtifact,
        Func<TExpectation, Guid> resolveId,
        Func<TExpectation, ProcessCoreArtifactKind> resolveKind)
    {
        return DiagnoseStrongExpectedArtifactMatch(
            expectedArtifacts,
            expectedKind,
            matchesExpectedArtifact,
            resolveId,
            resolveKind)
            .MatchedArtifactId;
    }

    public static ProcessArtifactExpectationMatchDiagnostic DiagnoseStrongExpectedArtifactMatch(
        IReadOnlyList<ProcessArtifactExpectationSnapshot> expectedArtifacts,
        ProcessCoreArtifactKind expectedKind,
        Func<ProcessArtifactExpectationSnapshot, bool> matchesExpectedArtifact)
    {
        return DiagnoseStrongExpectedArtifactMatch(
            expectedArtifacts,
            expectedKind,
            matchesExpectedArtifact,
            static item => item.Id,
            static item => item.ArtifactKind);
    }

    public static ProcessArtifactExpectationMatchDiagnostic DiagnoseStrongExpectedArtifactMatch<TExpectation>(
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
            return new ProcessArtifactExpectationMatchDiagnostic(
                resolveId(strongMatches[0]),
                ProcessArtifactExpectationMatchReason.StrongMatch,
                strongMatches.Count,
                KindMatchCount: resolveKind(strongMatches[0]) == expectedKind ? 1 : 0);
        }

        if (strongMatches.Count <= 1)
        {
            return new ProcessArtifactExpectationMatchDiagnostic(
                null,
                ProcessArtifactExpectationMatchReason.NoStrongMatch,
                strongMatches.Count,
                KindMatchCount: 0);
        }

        var kindMatches = strongMatches
            .Where(item => resolveKind(item) == expectedKind)
            .ToList();

        if (kindMatches.Count == 1)
        {
            return new ProcessArtifactExpectationMatchDiagnostic(
                resolveId(kindMatches[0]),
                ProcessArtifactExpectationMatchReason.KindDisambiguated,
                strongMatches.Count,
                kindMatches.Count);
        }

        return new ProcessArtifactExpectationMatchDiagnostic(
            null,
            kindMatches.Count == 0
                ? ProcessArtifactExpectationMatchReason.AmbiguousStrongMatch
                : ProcessArtifactExpectationMatchReason.AmbiguousKindMatch,
            strongMatches.Count,
            kindMatches.Count);
    }
}
