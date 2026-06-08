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
    CompatibilityWarning = 9,
    CargoTestFailure = 10,
    CompileError = 11,
    ClippyWarning = 12,
    MissingCargoArtifact = 13,
    UnsupportedToolchain = 14,
    PanicDetected = 15,
    TranscriptMissing = 16,
    TranscriptUntrusted = 17,
    EvidenceHashMismatch = 18,
    InsufficientProof = 19,
    NoIssueDetected = 20,
    UnsupportedTranscriptFormat = 21,
    AnalyzerWarning = 22,
    NullableWarning = 23,
    PlatformCompatibilityWarning = 24,
    RuntimeEvidenceInconsistent = 25,
    RetryContradiction = 26,
    FinalizerContradiction = 27,
    ProjectionOrderDrift = 28,
    ProviderRepairInconsistent = 29,
    NoProgressFingerprintMissing = 30,
    UnsupportedContractVersion = 31
}
