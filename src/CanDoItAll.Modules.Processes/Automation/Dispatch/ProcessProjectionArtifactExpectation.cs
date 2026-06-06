namespace CanDoItAll.Modules.Processes;

internal sealed record ProcessProjectionArtifactExpectation(
    Guid Id,
    ProcessArtifactKind ArtifactKind,
    string Title,
    bool IsRequired,
    ProcessArtifactTrustRequirement TrustRequirement,
    ProcessSensitivityLevel SensitivityLevel,
    string ValidationRequirementSummary,
    string AllowedFutureUsageSummary);
