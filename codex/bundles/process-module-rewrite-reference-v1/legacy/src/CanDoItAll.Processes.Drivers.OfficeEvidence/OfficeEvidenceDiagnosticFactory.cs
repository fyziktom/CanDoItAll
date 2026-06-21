using CanDoItAll.Processes.Drivers.Abstractions.Audit;
using CanDoItAll.Processes.Drivers.Abstractions.Evidence;
using CanDoItAll.Processes.Drivers.Abstractions.Verification;

namespace CanDoItAll.Processes.Drivers.OfficeEvidence;

internal static class OfficeEvidenceDiagnosticFactory
{
    public static ProcessDriverDiagnostic Create(
        ProcessDriverDiagnosticSeverity severity,
        ProcessDriverDiagnosticCategory category,
        string message,
        ProcessDriverEvidenceReference evidence)
    {
        return new ProcessDriverDiagnostic(
            severity,
            category,
            ProcessDriverRedactionPolicy.RedactDiagnosticSummary(message).RedactedText,
            evidence);
    }
}
