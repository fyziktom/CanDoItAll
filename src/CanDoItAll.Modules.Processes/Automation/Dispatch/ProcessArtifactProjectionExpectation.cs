namespace CanDoItAll.Modules.Processes;

internal sealed record ProcessArtifactProjectionExpectation(
    Guid Id,
    ProcessArtifactKind ArtifactKind,
    string Title,
    bool IsRequired,
    ProcessArtifactTrustRequirement TrustRequirement,
    ProcessSensitivityLevel SensitivityLevel,
    string ValidationRequirementSummary,
    string AllowedFutureUsageSummary);
