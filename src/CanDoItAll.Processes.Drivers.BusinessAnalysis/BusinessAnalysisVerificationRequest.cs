using CanDoItAll.Processes.Drivers.Abstractions.Evidence;
using CanDoItAll.Processes.Drivers.Abstractions.Verification;

namespace CanDoItAll.Processes.Drivers.BusinessAnalysis;

public sealed record BusinessAnalysisVerificationRequest(
    ProcessDriverVerificationRequest VerificationRequest,
    ProcessDriverSuppliedEvidenceContent SuppliedContent,
    IReadOnlyList<BusinessAnalysisEvidenceItem> Items,
    DateTimeOffset RequestedAt);
