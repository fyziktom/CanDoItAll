using CanDoItAll.Processes.Drivers.Abstractions.Evidence;

namespace CanDoItAll.Processes.Drivers.Abstractions.Verification;

public sealed record ProcessDriverDiagnostic(
    ProcessDriverDiagnosticSeverity Severity,
    ProcessDriverDiagnosticCategory Category,
    string Message,
    ProcessDriverEvidenceReference EvidenceReference);

public enum ProcessDriverDiagnosticSeverity
{
    Info = 1,
    Warning = 2,
    Error = 3
}

public enum ProcessDriverDiagnosticCategory
{
    BuildWarning = 1,
    BuildError = 2,
    TestFailure = 3,
    MissingArtifact = 4,
    UnsupportedTargetFramework = 5,
    RuntimeProofGap = 6,
    MutationAttemptDenied = 7,
    RedactionApplied = 8,
    CompatibilityWarning = 9
}
