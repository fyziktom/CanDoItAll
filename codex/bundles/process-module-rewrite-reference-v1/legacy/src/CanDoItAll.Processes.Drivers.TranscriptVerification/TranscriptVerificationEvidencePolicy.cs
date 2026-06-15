using CanDoItAll.Processes.Drivers.Abstractions.Audit;
using CanDoItAll.Processes.Drivers.Abstractions.Evidence;

namespace CanDoItAll.Processes.Drivers.TranscriptVerification;

internal sealed record TranscriptVerificationEvidenceContext(
    IReadOnlyList<ProcessDriverEvidenceReference> NormalizedEvidenceReferences,
    ProcessDriverEvidenceReference TranscriptEvidenceReference,
    ProcessDriverEvidenceReference PrimaryEvidenceReference,
    ProcessDriverRedactionResult Redaction)
{
    public IReadOnlyList<ProcessDriverEvidenceReference> CreateResponseEvidenceReferences()
    {
        return NormalizedEvidenceReferences.Count == 0
            ? [TranscriptEvidenceReference]
            : NormalizedEvidenceReferences;
    }
}

internal static class TranscriptVerificationEvidencePolicy
{
    public static TranscriptVerificationEvidenceContext CreateContext(
        TranscriptVerificationAlphaRequest request)
    {
        var normalizedReferences = ProcessDriverEvidencePolicy.NormalizeEvidenceReferences(
            request.VerificationRequest.EvidenceReferences);
        var transcriptEvidence = request.SuppliedContent.EvidenceReference;
        var primaryEvidence = normalizedReferences.FirstOrDefault() ?? transcriptEvidence;

        return new TranscriptVerificationEvidenceContext(
            normalizedReferences,
            transcriptEvidence,
            primaryEvidence,
            TranscriptVerificationRedaction.RedactTranscript(request.TranscriptText));
    }
}

internal static class TranscriptVerificationRedaction
{
    public static ProcessDriverRedactionResult RedactTranscript(string transcriptText)
    {
        return ProcessDriverRedactionPolicy.Redact(transcriptText);
    }
}
