namespace CanDoItAll.Modules.Processes;

internal static class ProcessCompletionBlockerRules
{
    internal static ProcessCompletionBlockerSummary CreateSummary(
        string? missingUpstreamArtifactInputSummary,
        string? missingConcreteProofSummary,
        string? incompleteImplementationSummary,
        string? missingConcreteImplementationProofSummary,
        string? missingRunnableApplicationProofSummary,
        string? invalidBrowserProofSummary,
        string? invalidQualityValidationProofSummary,
        string? missingRequiredArtifactSummary,
        string? downgradedProjectStructureRequirementSummary,
        string? missingUpstreamArtifactInspectionSummary,
        string? outOfScopeExternalTargetReferenceSummary,
        string? shallowSharedManagedArtifactReferenceSummary)
    {
        return new ProcessCompletionBlockerSummary(
            NormalizeSummary(missingUpstreamArtifactInputSummary),
            NormalizeSummary(missingConcreteProofSummary),
            NormalizeSummary(incompleteImplementationSummary),
            NormalizeSummary(missingConcreteImplementationProofSummary),
            NormalizeSummary(missingRunnableApplicationProofSummary),
            NormalizeSummary(invalidBrowserProofSummary),
            NormalizeSummary(invalidQualityValidationProofSummary),
            NormalizeSummary(missingRequiredArtifactSummary),
            NormalizeSummary(downgradedProjectStructureRequirementSummary),
            NormalizeSummary(missingUpstreamArtifactInspectionSummary),
            NormalizeSummary(outOfScopeExternalTargetReferenceSummary),
            NormalizeSummary(shallowSharedManagedArtifactReferenceSummary));
    }

    private static string NormalizeSummary(string? value)
    {
        return value?.Trim() ?? string.Empty;
    }
}

internal sealed record ProcessCompletionBlockerSummary(
    string MissingUpstreamArtifactInput,
    string MissingConcreteProof,
    string IncompleteImplementation,
    string MissingConcreteImplementationProof,
    string MissingRunnableApplicationProof,
    string InvalidBrowserProof,
    string InvalidQualityValidationProof,
    string MissingRequiredArtifact,
    string DowngradedProjectStructureRequirement,
    string MissingUpstreamArtifactInspection,
    string OutOfScopeExternalTargetReference,
    string ShallowSharedManagedArtifactReference)
{
    internal bool HasAny =>
        !string.IsNullOrWhiteSpace(MissingUpstreamArtifactInput) ||
        !string.IsNullOrWhiteSpace(MissingConcreteProof) ||
        !string.IsNullOrWhiteSpace(IncompleteImplementation) ||
        !string.IsNullOrWhiteSpace(MissingConcreteImplementationProof) ||
        !string.IsNullOrWhiteSpace(MissingRunnableApplicationProof) ||
        !string.IsNullOrWhiteSpace(InvalidBrowserProof) ||
        !string.IsNullOrWhiteSpace(InvalidQualityValidationProof) ||
        !string.IsNullOrWhiteSpace(MissingRequiredArtifact) ||
        !string.IsNullOrWhiteSpace(DowngradedProjectStructureRequirement) ||
        !string.IsNullOrWhiteSpace(MissingUpstreamArtifactInspection) ||
        !string.IsNullOrWhiteSpace(OutOfScopeExternalTargetReference) ||
        !string.IsNullOrWhiteSpace(ShallowSharedManagedArtifactReference);
}
