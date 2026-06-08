using CanDoItAll.Processes.Core.Artifacts;
using CanDoItAll.Processes.Core.Diagnostics;
using CanDoItAll.Processes.Core.Execution;
using CanDoItAll.Processes.Core.Finalization;
using CanDoItAll.Processes.Drivers.Abstractions.Verification;

namespace CanDoItAll.Processes.Drivers.RuntimeEvidence;

public sealed record RuntimeEvidenceConsistencyVerificationRequest(
    ProcessDriverVerificationRequest VerificationRequest,
    ProcessExecutionEvidenceDescriptor? ExecutionEvidence,
    ProcessFinalizerEvidenceDescriptor? FinalizerEvidence,
    ProcessRetryDiagnosticDescriptor? RetryDiagnostic,
    ProcessNoProgressRetryDiagnosticDescriptor? NoProgressDiagnostic,
    ProcessProviderRepairDiagnosticDescriptor? ProviderRepairDiagnostic,
    IReadOnlyList<ProcessArtifactProjectionSourceOrderDescriptor> ProjectionSourceOrder,
    DateTimeOffset RequestedAt);
