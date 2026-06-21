namespace CanDoItAll.Processes.Drivers.Abstractions.Permissions;

public sealed record ProcessDriverDeniedOperation(
    ProcessDriverOperation Operation,
    ProcessDriverDenialReason Reason,
    ProcessDriverPermissionMode RequestedMode,
    ProcessDriverCapabilityScope Scope);
