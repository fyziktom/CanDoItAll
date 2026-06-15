using System.Text;

namespace CanDoItAll.Processes.Drivers.Abstractions.Evidence;

public static class ProcessDriverSuppliedEvidenceContentRules
{
    public const string PlainTextContentType = "text/plain; charset=utf-8";
    public const string JsonContentType = "application/json";
    public const long MaxSuppliedEvidenceContentBytes = 1_048_576;

    public static ProcessDriverSuppliedEvidenceContent CreateTranscriptText(
        ProcessDriverEvidenceReference evidenceReference,
        string transcriptText)
    {
        ArgumentNullException.ThrowIfNull(evidenceReference);
        ArgumentNullException.ThrowIfNull(transcriptText);

        return Create(
            ProcessDriverSuppliedEvidenceContentKind.TranscriptText,
            evidenceReference,
            PlainTextContentType,
            transcriptText);
    }

    public static ProcessDriverSuppliedEvidenceContent CreateCoreDescriptorPayload(
        ProcessDriverEvidenceReference evidenceReference,
        string descriptorPayload)
    {
        ArgumentNullException.ThrowIfNull(evidenceReference);
        ArgumentNullException.ThrowIfNull(descriptorPayload);

        return Create(
            ProcessDriverSuppliedEvidenceContentKind.CoreDescriptorPayload,
            evidenceReference,
            JsonContentType,
            descriptorPayload);
    }

    public static ProcessDriverSuppliedEvidenceContent CreateOfficeEvidencePayload(
        ProcessDriverEvidenceReference evidenceReference,
        string officeEvidencePayload)
    {
        ArgumentNullException.ThrowIfNull(evidenceReference);
        ArgumentNullException.ThrowIfNull(officeEvidencePayload);

        return Create(
            ProcessDriverSuppliedEvidenceContentKind.OfficeEvidencePayload,
            evidenceReference,
            JsonContentType,
            officeEvidencePayload);
    }

    public static ProcessDriverSuppliedEvidenceContent CreateBusinessAnalysisPayload(
        ProcessDriverEvidenceReference evidenceReference,
        string businessAnalysisPayload)
    {
        ArgumentNullException.ThrowIfNull(evidenceReference);
        ArgumentNullException.ThrowIfNull(businessAnalysisPayload);

        return Create(
            ProcessDriverSuppliedEvidenceContentKind.BusinessAnalysisPayload,
            evidenceReference,
            JsonContentType,
            businessAnalysisPayload);
    }

    public static bool HasExpectedEnvelope(
        ProcessDriverSuppliedEvidenceContent suppliedContent,
        ProcessDriverSuppliedEvidenceContentKind expectedKind,
        string expectedContentType)
    {
        ArgumentNullException.ThrowIfNull(suppliedContent);

        return suppliedContent.Kind == expectedKind &&
            string.Equals(
                suppliedContent.ContentType.Trim(),
                expectedContentType,
                StringComparison.OrdinalIgnoreCase);
    }

    public static bool HasAllowedSize(ProcessDriverSuppliedEvidenceContent suppliedContent)
    {
        ArgumentNullException.ThrowIfNull(suppliedContent);

        return suppliedContent.SizeBytes is > 0 and <= MaxSuppliedEvidenceContentBytes;
    }

    public static bool HasValidContentHash(ProcessDriverSuppliedEvidenceContent suppliedContent)
    {
        ArgumentNullException.ThrowIfNull(suppliedContent);

        return ProcessDriverEvidencePolicy.IsSha256(suppliedContent.ContentHash);
    }

    public static bool HasEvidenceReferenceHashBinding(ProcessDriverSuppliedEvidenceContent suppliedContent)
    {
        ArgumentNullException.ThrowIfNull(suppliedContent);

        return ProcessDriverEvidencePolicy.IsSha256(suppliedContent.ContentHash) &&
            ProcessDriverEvidencePolicy.IsSha256(suppliedContent.EvidenceReference.ContentHash) &&
            ProcessDriverEvidencePolicy.NormalizeHash(suppliedContent.ContentHash) ==
            ProcessDriverEvidencePolicy.NormalizeHash(suppliedContent.EvidenceReference.ContentHash);
    }

    public static bool HashMatchesSuppliedPayload(
        ProcessDriverSuppliedEvidenceContent suppliedContent,
        string suppliedPayload)
    {
        ArgumentNullException.ThrowIfNull(suppliedContent);
        ArgumentNullException.ThrowIfNull(suppliedPayload);

        return ProcessDriverEvidencePolicy.IsSha256(suppliedContent.ContentHash) &&
            ProcessDriverEvidencePolicy.NormalizeHash(suppliedContent.ContentHash) ==
            ProcessDriverEvidencePolicy.ComputeSha256(suppliedPayload);
    }

    private static ProcessDriverSuppliedEvidenceContent Create(
        ProcessDriverSuppliedEvidenceContentKind kind,
        ProcessDriverEvidenceReference evidenceReference,
        string contentType,
        string content)
    {
        return new ProcessDriverSuppliedEvidenceContent(
            kind,
            evidenceReference,
            contentType,
            Encoding.UTF8.GetByteCount(content),
            ProcessDriverEvidencePolicy.ComputeSha256(content));
    }
}
