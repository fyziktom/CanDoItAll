namespace CanDoItAll.Modules.Processes;

internal static class ProcessArtifactSatisfactionBlockerSummaryBuilder
{
    public static string BuildMissingRequiredArtifactSummary(
        IReadOnlyList<ProcessRunAutomationDispatchService.DispatchArtifactExpectation> expectedArtifacts,
        Func<ProcessRunAutomationDispatchService.DispatchArtifactExpectation, bool> isSatisfied)
    {
        var missingRequiredArtifacts = expectedArtifacts
            .Where(item => item.IsRequired)
            .Where(item => !isSatisfied(item))
            .Select(item => item.Title.Trim())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToList();

        return missingRequiredArtifacts.Count == 0
            ? string.Empty
            : string.Join(", ", missingRequiredArtifacts);
    }
}

