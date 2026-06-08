using CanDoItAll.Processes.Drivers.Abstractions.Audit;
using CanDoItAll.Processes.Drivers.Abstractions.Evidence;
using CanDoItAll.Processes.Drivers.Abstractions.Verification;

namespace CanDoItAll.Processes.Drivers.TranscriptVerification;

internal static class TranscriptVerificationDiagnosticFactory
{
    public static ProcessDriverDiagnostic Create(
        ProcessDriverDiagnosticSeverity severity,
        ProcessDriverDiagnosticCategory category,
        string message,
        ProcessDriverEvidenceReference evidence,
        ProcessDriverRedactionResult redaction)
    {
        var redactedMessage = ProcessDriverRedactionPolicy.RedactDiagnosticSummary(message).RedactedText;

        return new ProcessDriverDiagnostic(
            severity,
            category,
            redactedMessage,
            evidence);
    }

    public static ProcessDriverDiagnostic CreateWithoutRedaction(
        ProcessDriverDiagnosticSeverity severity,
        ProcessDriverDiagnosticCategory category,
        string message,
        ProcessDriverEvidenceReference evidence)
    {
        return new ProcessDriverDiagnostic(
            severity,
            category,
            message,
            evidence);
    }
}
