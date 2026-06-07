using CanDoItAll.Processes.Drivers.Abstractions.Evidence;
using CanDoItAll.Processes.Drivers.Abstractions.Permissions;

namespace CanDoItAll.Processes.Drivers.Abstractions.Verification;

public sealed record ProcessDriverVerificationRequest(
    ProcessDriverPermissionMode PermissionMode,
    ProcessDriverCapabilityScope Scope,
    IReadOnlyList<ProcessDriverEvidenceReference> EvidenceReferences,
    IReadOnlyList<ProcessDriverOperation> RequestedOperations,
    string CallerContext,
    ProcessDriverContractVersion ContractVersion);
