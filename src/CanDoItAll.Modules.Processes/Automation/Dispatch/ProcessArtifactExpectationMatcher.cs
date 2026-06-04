namespace CanDoItAll.Modules.Processes;

internal static class ProcessArtifactExpectationMatcher
{
    public static Guid? MatchStrongExpectedArtifactId(
        IReadOnlyList<ProcessArtifactProjectionExpectation> expectedArtifacts,
        ProcessArtifactKind expectedKind,
        Func<ProcessArtifactProjectionExpectation, bool> matchesExpectedArtifact)
    {
        ArgumentNullException.ThrowIfNull(expectedArtifacts);
        ArgumentNullException.ThrowIfNull(matchesExpectedArtifact);

        var strongMatches = expectedArtifacts
            .Where(matchesExpectedArtifact)
            .ToList();
        if (strongMatches.Count == 1)
        {
            return strongMatches[0].Id;
        }

        if (strongMatches.Count <= 1)
        {
            return null;
        }

        var kindMatches = strongMatches
            .Where(item => item.ArtifactKind == expectedKind)
            .ToList();

        return kindMatches.Count == 1
            ? kindMatches[0].Id
            : null;
    }
}
