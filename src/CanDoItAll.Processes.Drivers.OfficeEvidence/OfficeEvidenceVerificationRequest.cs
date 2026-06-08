using CanDoItAll.Processes.Drivers.Abstractions.Evidence;
using CanDoItAll.Processes.Drivers.Abstractions.Verification;

namespace CanDoItAll.Processes.Drivers.OfficeEvidence;

public sealed record OfficeEvidenceVerificationRequest(
    ProcessDriverVerificationRequest VerificationRequest,
    ProcessDriverSuppliedEvidenceContent SuppliedContent,
    IReadOnlyList<OfficeEvidenceItem> Items,
    DateTimeOffset RequestedAt);
