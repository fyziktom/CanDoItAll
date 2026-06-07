namespace CanDoItAll.Processes.Core.Artifacts;

public enum ProcessArtifactExpectationSatisfactionReason
{
    Satisfied,
    ArtifactKindMismatch,
    SensitivityTooLow,
    TrustRequirementNotSatisfied,
    ExpectationIdMismatch,
    TitleMismatch
}

public readonly record struct ProcessArtifactExpectationSatisfactionDiagnostic(
    bool IsSatisfied,
    ProcessArtifactExpectationSatisfactionReason Reason);

public static class ProcessArtifactExpectationSatisfactionRules
{
    public static ProcessArtifactExpectationSatisfactionDiagnostic Diagnose(
        ProcessArtifactRecordSnapshot artifact,
        ProcessArtifactExpectationSnapshot expectation)
    {
        if (artifact.ArtifactKind != expectation.ArtifactKind)
        {
            return new ProcessArtifactExpectationSatisfactionDiagnostic(
                IsSatisfied: false,
                ProcessArtifactExpectationSatisfactionReason.ArtifactKindMismatch);
        }

        if (artifact.SensitivityLevel < expectation.SensitivityLevel)
        {
            return new ProcessArtifactExpectationSatisfactionDiagnostic(
                IsSatisfied: false,
                ProcessArtifactExpectationSatisfactionReason.SensitivityTooLow);
        }

        if (!SatisfiesTrustRequirement(artifact.TrustStatus, expectation.TrustRequirement))
        {
            return new ProcessArtifactExpectationSatisfactionDiagnostic(
                IsSatisfied: false,
                ProcessArtifactExpectationSatisfactionReason.TrustRequirementNotSatisfied);
        }

        if (artifact.ArtifactExpectationId.HasValue)
        {
            return artifact.ArtifactExpectationId.Value == expectation.Id
                ? Satisfied()
                : new ProcessArtifactExpectationSatisfactionDiagnostic(
                    IsSatisfied: false,
                    ProcessArtifactExpectationSatisfactionReason.ExpectationIdMismatch);
        }

        return string.Equals(artifact.Title, expectation.Title, StringComparison.OrdinalIgnoreCase)
            ? Satisfied()
            : new ProcessArtifactExpectationSatisfactionDiagnostic(
                IsSatisfied: false,
                ProcessArtifactExpectationSatisfactionReason.TitleMismatch);
    }

    public static bool SatisfiesArtifactExpectation(
        ProcessArtifactRecordSnapshot artifact,
        ProcessArtifactExpectationSnapshot expectation)
    {
        return Diagnose(artifact, expectation).IsSatisfied;
    }

    public static bool SatisfiesTrustRequirement(
        ProcessCoreArtifactTrustStatus trustStatus,
        ProcessCoreArtifactTrustRequirement trustRequirement)
    {
        return trustRequirement switch
        {
            ProcessCoreArtifactTrustRequirement.None => true,
            ProcessCoreArtifactTrustRequirement.ReviewRequired => trustStatus is
                ProcessCoreArtifactTrustStatus.ReviewRequired or
                ProcessCoreArtifactTrustStatus.Approved or
                ProcessCoreArtifactTrustStatus.TrustedSource,
            ProcessCoreArtifactTrustRequirement.HumanApproved => trustStatus == ProcessCoreArtifactTrustStatus.Approved,
            ProcessCoreArtifactTrustRequirement.ApprovalRequired => trustStatus == ProcessCoreArtifactTrustStatus.Approved,
            ProcessCoreArtifactTrustRequirement.TrustedSource => trustStatus == ProcessCoreArtifactTrustStatus.TrustedSource,
            _ => false
        };
    }

    private static ProcessArtifactExpectationSatisfactionDiagnostic Satisfied()
    {
        return new ProcessArtifactExpectationSatisfactionDiagnostic(
            IsSatisfied: true,
            ProcessArtifactExpectationSatisfactionReason.Satisfied);
    }
}
