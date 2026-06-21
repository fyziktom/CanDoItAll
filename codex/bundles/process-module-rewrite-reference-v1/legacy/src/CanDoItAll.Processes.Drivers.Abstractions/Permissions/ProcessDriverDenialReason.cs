namespace CanDoItAll.Processes.Drivers.Abstractions.Permissions;

public enum ProcessDriverDenialReason
{
    None = 0,
    MissingPermissionMode = 1,
    UnsupportedMode = 2,
    UnsupportedOperation = 3,
    UnsafeCommand = 4,
    ExternalCallDenied = 5,
    MutationDenied = 6,
    MissingEvidence = 7,
    CapabilityScopeDenied = 8
}
