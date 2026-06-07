using CanDoItAll.Processes.Core.Artifacts;
using CoreArtifactExpectationSnapshot = CanDoItAll.Processes.Core.Artifacts.ProcessArtifactExpectationSnapshot;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessCoreArtifactModelAdapters
{
    public static CoreArtifactExpectationSnapshot ToCoreExpectationSnapshot(
        ProcessArtifactExpectation expectation)
    {
        ArgumentNullException.ThrowIfNull(expectation);

        return new CoreArtifactExpectationSnapshot(
            expectation.Id,
            ToCoreArtifactKind(expectation.ArtifactKind),
            expectation.Title,
            expectation.IsRequired,
            ToCoreTrustRequirement(expectation.TrustRequirement),
            ToCoreSensitivityLevel(expectation.SensitivityLevel),
            expectation.ValidationRequirementSummary,
            expectation.AllowedFutureUsageSummary,
            expectation.SubprocessChildArtifactExpectationId);
    }

    public static CoreArtifactExpectationSnapshot ToCoreExpectationSnapshot(
        CanDoItAll.Modules.Processes.ProcessArtifactExpectationSnapshot expectation)
    {
        return new CoreArtifactExpectationSnapshot(
            expectation.Id,
            ToCoreArtifactKind(expectation.ArtifactKind),
            expectation.Title,
            expectation.IsRequired,
            ToCoreTrustRequirement(expectation.TrustRequirement),
            ToCoreSensitivityLevel(expectation.SensitivityLevel),
            expectation.ValidationRequirementSummary,
            expectation.AllowedFutureUsageSummary);
    }

    public static ProcessArtifactRecordSnapshot ToCoreArtifactRecordSnapshot(
        ProcessArtifactRecord artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);

        return new ProcessArtifactRecordSnapshot(
            artifact.Id,
            artifact.ArtifactExpectationId,
            ToCoreArtifactKind(artifact.ArtifactKind),
            artifact.Title,
            ToCoreTrustStatus(artifact.TrustStatus),
            ToCoreSensitivityLevel(artifact.SensitivityLevel),
            artifact.CreatedAtUtc);
    }

    public static ProcessCoreArtifactKind ToCoreArtifactKind(ProcessArtifactKind kind)
    {
        return kind switch
        {
            ProcessArtifactKind.Brief => ProcessCoreArtifactKind.Brief,
            ProcessArtifactKind.Evidence => ProcessCoreArtifactKind.Evidence,
            ProcessArtifactKind.Decision => ProcessCoreArtifactKind.Decision,
            ProcessArtifactKind.Deliverable => ProcessCoreArtifactKind.Deliverable,
            ProcessArtifactKind.Transcript => ProcessCoreArtifactKind.Transcript,
            ProcessArtifactKind.Checklist => ProcessCoreArtifactKind.Checklist,
            ProcessArtifactKind.Prompt => ProcessCoreArtifactKind.Prompt,
            ProcessArtifactKind.Dataset => ProcessCoreArtifactKind.Dataset,
            ProcessArtifactKind.Other => ProcessCoreArtifactKind.Other,
            ProcessArtifactKind.DecisionRecord => ProcessCoreArtifactKind.DecisionRecord,
            _ => ProcessCoreArtifactKind.Other
        };
    }

    private static ProcessCoreArtifactTrustRequirement ToCoreTrustRequirement(
        ProcessArtifactTrustRequirement trustRequirement)
    {
        return trustRequirement switch
        {
            ProcessArtifactTrustRequirement.None => ProcessCoreArtifactTrustRequirement.None,
            ProcessArtifactTrustRequirement.ReviewRequired => ProcessCoreArtifactTrustRequirement.ReviewRequired,
            ProcessArtifactTrustRequirement.HumanApproved => ProcessCoreArtifactTrustRequirement.HumanApproved,
            ProcessArtifactTrustRequirement.TrustedSource => ProcessCoreArtifactTrustRequirement.TrustedSource,
            ProcessArtifactTrustRequirement.ApprovalRequired => ProcessCoreArtifactTrustRequirement.ApprovalRequired,
            _ => ProcessCoreArtifactTrustRequirement.ReviewRequired
        };
    }

    private static ProcessCoreArtifactTrustStatus ToCoreTrustStatus(ProcessArtifactTrustStatus trustStatus)
    {
        return trustStatus switch
        {
            ProcessArtifactTrustStatus.Draft => ProcessCoreArtifactTrustStatus.Draft,
            ProcessArtifactTrustStatus.ReviewRequired => ProcessCoreArtifactTrustStatus.ReviewRequired,
            ProcessArtifactTrustStatus.Approved => ProcessCoreArtifactTrustStatus.Approved,
            ProcessArtifactTrustStatus.Rejected => ProcessCoreArtifactTrustStatus.Rejected,
            ProcessArtifactTrustStatus.TrustedSource => ProcessCoreArtifactTrustStatus.TrustedSource,
            _ => ProcessCoreArtifactTrustStatus.Draft
        };
    }

    private static ProcessCoreSensitivityLevel ToCoreSensitivityLevel(ProcessSensitivityLevel sensitivityLevel)
    {
        return sensitivityLevel switch
        {
            ProcessSensitivityLevel.Public => ProcessCoreSensitivityLevel.Public,
            ProcessSensitivityLevel.Internal => ProcessCoreSensitivityLevel.Internal,
            ProcessSensitivityLevel.Confidential => ProcessCoreSensitivityLevel.Confidential,
            ProcessSensitivityLevel.Restricted => ProcessCoreSensitivityLevel.Restricted,
            _ => ProcessCoreSensitivityLevel.Internal
        };
    }
}
