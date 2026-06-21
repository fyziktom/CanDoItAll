using CanDoItAll.Processes.Drivers.Abstractions.Evidence;
using CanDoItAll.Processes.Drivers.Abstractions.Verification;

namespace CanDoItAll.Processes.Drivers.TranscriptVerification;

public sealed record TranscriptVerificationAlphaRequest(
    ProcessDriverVerificationRequest VerificationRequest,
    ProcessDriverTranscriptReference TranscriptReference,
    ProcessDriverSuppliedEvidenceContent SuppliedContent,
    string TranscriptText,
    DateTimeOffset RequestedAt);
