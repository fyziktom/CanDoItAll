using CanDoItAll.Processes.Drivers.Abstractions.Evidence;
using CanDoItAll.Processes.Drivers.Abstractions.Permissions;

namespace CanDoItAll.Processes.Drivers.Abstractions.Audit;

public sealed record ProcessDriverAuditFact(
    Guid Id,
    DateTimeOffset CreatedAt,
    ProcessDriverAuditFactKind Kind,
    string CallerContext,
    ProcessDriverPermissionMode PermissionMode,
    ProcessDriverCapabilityScope Scope,
    ProcessDriverCapabilityScopeKind Lane,
    ProcessDriverOperation RequestedOperation,
    IReadOnlyList<ProcessDriverEvidenceReference> EvidenceReferences,
    ProcessDriverDenialReason DenialReason,
    ProcessDriverRedactionDescriptor Redaction,
    string DiagnosticSummary,
    string OutputHash);

public enum ProcessDriverAuditFactKind
{
    PermissionEvaluated = 1,
    OperationDenied = 2,
    EvidenceInspected = 3,
    DiagnosticReturned = 4
}
