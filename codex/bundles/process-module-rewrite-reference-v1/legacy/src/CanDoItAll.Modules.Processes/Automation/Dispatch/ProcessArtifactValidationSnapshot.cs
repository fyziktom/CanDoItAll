namespace CanDoItAll.Modules.Processes;

internal sealed record ProcessArtifactValidationSnapshot(
    IReadOnlyList<ProcessArtifactExpectationSnapshot> ExpectedArtifacts,
    string ProjectStructureContractText)
{
    public static ProcessArtifactValidationSnapshot Empty { get; } = new([], string.Empty);
}

internal sealed record ProcessArtifactExpectationSnapshot(
    Guid Id,
    ProcessArtifactKind ArtifactKind,
    string Title,
    bool IsRequired,
    ProcessArtifactTrustRequirement TrustRequirement,
    ProcessSensitivityLevel SensitivityLevel,
    string ValidationRequirementSummary,
    string AllowedFutureUsageSummary);
