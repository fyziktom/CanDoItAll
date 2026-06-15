using CanDoItAll.Processes.Drivers.Abstractions.Evidence;

namespace CanDoItAll.Processes.Drivers.RuntimeEvidence;

internal sealed record RuntimeEvidenceDescriptorContext(
    IReadOnlyList<ProcessDriverEvidenceReference> NormalizedEvidenceReferences,
    ProcessDriverEvidenceReference PrimaryEvidenceReference)
{
    public IReadOnlyList<ProcessDriverEvidenceReference> CreateResponseEvidenceReferences()
    {
        return NormalizedEvidenceReferences.Count == 0
            ? [PrimaryEvidenceReference]
            : NormalizedEvidenceReferences;
    }
}

internal static class RuntimeEvidenceDescriptorNormalizer
{
    public static RuntimeEvidenceDescriptorContext CreateContext(
        RuntimeEvidenceConsistencyVerificationRequest request)
    {
        var evidenceReferences = ProcessDriverEvidencePolicy.NormalizeEvidenceReferences(
            request.VerificationRequest.EvidenceReferences);
        var primaryEvidence = evidenceReferences.FirstOrDefault() ?? request.SuppliedContent.EvidenceReference;

        return new RuntimeEvidenceDescriptorContext(evidenceReferences, primaryEvidence);
    }
}
