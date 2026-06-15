namespace CanDoItAll.Processes.Drivers.Abstractions.Permissions;

public enum ProcessDriverPermissionMode
{
    Unspecified = 0,
    VerificationOnly = 1,
    ManagerReadonly = 2,
    ExecutionCapableFuture = 3
}
