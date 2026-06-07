namespace CanDoItAll.Processes.Core.Artifacts;

public enum ProcessCoreArtifactKind
{
    Brief = 0,
    Evidence = 1,
    Decision = 2,
    Deliverable = 3,
    Transcript = 4,
    Checklist = 5,
    Prompt = 6,
    Dataset = 7,
    Other = 8,
    DecisionRecord = 9
}

public enum ProcessCoreArtifactTrustRequirement
{
    None = 0,
    ReviewRequired = 1,
    HumanApproved = 2,
    TrustedSource = 3,
    ApprovalRequired = 4
}

public enum ProcessCoreArtifactTrustStatus
{
    Draft = 0,
    ReviewRequired = 1,
    Approved = 2,
    Rejected = 3,
    TrustedSource = 4
}

public enum ProcessCoreSensitivityLevel
{
    Public = 0,
    Internal = 1,
    Confidential = 2,
    Restricted = 3
}

public sealed record ProcessArtifactValidationSnapshot(
    IReadOnlyList<ProcessArtifactExpectationSnapshot> ExpectedArtifacts,
    string ProjectStructureContractText)
{
    public static ProcessArtifactValidationSnapshot Empty { get; } = new([], string.Empty);
}

public sealed record ProcessArtifactExpectationSnapshot(
    Guid Id,
    ProcessCoreArtifactKind ArtifactKind,
    string Title,
    bool IsRequired,
    ProcessCoreArtifactTrustRequirement TrustRequirement,
    ProcessCoreSensitivityLevel SensitivityLevel,
    string ValidationRequirementSummary,
    string AllowedFutureUsageSummary,
    Guid? SubprocessChildArtifactExpectationId = null);

public sealed record ProcessArtifactRecordSnapshot(
    Guid Id,
    Guid? ArtifactExpectationId,
    ProcessCoreArtifactKind ArtifactKind,
    string Title,
    ProcessCoreArtifactTrustStatus TrustStatus,
    ProcessCoreSensitivityLevel SensitivityLevel,
    DateTimeOffset CreatedAtUtc);
