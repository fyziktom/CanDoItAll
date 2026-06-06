namespace CanDoItAll.Modules.Processes;

internal static class ProcessArtifactValidationSnapshotBuilder
{
    public static ProcessArtifactValidationSnapshot FromDispatchExpectations(
        IReadOnlyList<ProcessRunAutomationDispatchService.DispatchArtifactExpectation> expectedArtifacts,
        string? projectStructureContractText)
    {
        ArgumentNullException.ThrowIfNull(expectedArtifacts);

        return new ProcessArtifactValidationSnapshot(
            expectedArtifacts.Select(FromDispatchExpectation).ToList(),
            projectStructureContractText ?? string.Empty);
    }

    public static ProcessArtifactValidationExpectation FromDispatchExpectation(
        ProcessRunAutomationDispatchService.DispatchArtifactExpectation expectedArtifact)
        => new(
            expectedArtifact.Id,
            expectedArtifact.ArtifactKind,
            expectedArtifact.Title,
            expectedArtifact.IsRequired,
            expectedArtifact.TrustRequirement,
            expectedArtifact.SensitivityLevel,
            expectedArtifact.ValidationRequirementSummary,
            expectedArtifact.AllowedFutureUsageSummary);

    public static ProcessArtifactValidationExpectation FromProjectionExpectation(
        ProcessProjectionArtifactExpectation expectedArtifact)
        => new(
            expectedArtifact.Id,
            expectedArtifact.ArtifactKind,
            expectedArtifact.Title,
            expectedArtifact.IsRequired,
            expectedArtifact.TrustRequirement,
            expectedArtifact.SensitivityLevel,
            expectedArtifact.ValidationRequirementSummary,
            expectedArtifact.AllowedFutureUsageSummary);

    public static ProcessProjectionArtifactExpectation ToProjectionExpectation(
        ProcessRunAutomationDispatchService.DispatchArtifactExpectation expectedArtifact)
        => FromDispatchExpectation(expectedArtifact).ToProjectionExpectation();
}
