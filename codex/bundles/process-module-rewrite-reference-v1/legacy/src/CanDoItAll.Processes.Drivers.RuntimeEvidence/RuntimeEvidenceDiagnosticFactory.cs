using CanDoItAll.Processes.Drivers.Abstractions.Audit;
using CanDoItAll.Processes.Drivers.Abstractions.Evidence;
using CanDoItAll.Processes.Drivers.Abstractions.Verification;

namespace CanDoItAll.Processes.Drivers.RuntimeEvidence;

internal static class RuntimeEvidenceDiagnosticFactory
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
