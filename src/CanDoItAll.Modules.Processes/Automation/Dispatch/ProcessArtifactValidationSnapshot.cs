namespace CanDoItAll.Modules.Processes;

internal sealed record ProcessArtifactValidationSnapshot(
    IReadOnlyList<ProcessArtifactValidationExpectation> ExpectedArtifacts,
    string ProjectStructureContractText)
{
    public static ProcessArtifactValidationSnapshot Empty { get; } = new([], string.Empty);
}

internal sealed record ProcessArtifactValidationExpectation(
    Guid Id,
    ProcessArtifactKind ArtifactKind,
    string Title,
    bool IsRequired,
    ProcessArtifactTrustRequirement TrustRequirement,
    ProcessSensitivityLevel SensitivityLevel,
    string ValidationRequirementSummary,
    string AllowedFutureUsageSummary)
{
    public ProcessProjectionArtifactExpectation ToProjectionExpectation()
        => new(
            Id,
            ArtifactKind,
            Title,
            IsRequired,
            TrustRequirement,
            SensitivityLevel,
            ValidationRequirementSummary,
            AllowedFutureUsageSummary);
}
