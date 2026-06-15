namespace CanDoItAll.Processes.Drivers.Abstractions.Evidence;

public sealed record ProcessDriverSuppliedEvidenceContent(
    ProcessDriverSuppliedEvidenceContentKind Kind,
    ProcessDriverEvidenceReference EvidenceReference,
    string ContentType,
    long SizeBytes,
    string ContentHash);

public enum ProcessDriverSuppliedEvidenceContentKind
{
    TranscriptText = 1,
    CoreDescriptorPayload = 2,
    OfficeEvidencePayload = 3,
    BusinessAnalysisPayload = 4
}
