using CanDoItAll.Processes.Drivers.Abstractions.Evidence;
using CanDoItAll.Processes.Drivers.Abstractions.Permissions;

namespace CanDoItAll.Processes.Drivers.Abstractions.Gateway;

public sealed record ProcessDriverVerificationGatewayLaneDescriptor(
    ProcessDriverVerificationGatewayLane Lane,
    ProcessDriverCapabilityScopeKind RequiredScopeKind,
    ProcessDriverPermissionMode RequiredPermissionMode,
    ProcessDriverEvidenceReferenceKind PrimaryEvidenceKind,
    ProcessDriverCoreDescriptorFamily? CoreDescriptorFamily,
    IReadOnlyList<ProcessDriverOperation> AllowedOperations);
