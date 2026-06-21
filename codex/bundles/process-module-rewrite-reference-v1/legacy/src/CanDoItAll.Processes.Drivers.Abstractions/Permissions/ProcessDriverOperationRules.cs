namespace CanDoItAll.Processes.Drivers.Abstractions.Permissions;

public static class ProcessDriverOperationRules
{
    public static IReadOnlyList<ProcessDriverOperation> ReadonlyVerificationOperations { get; } =
    [
        ProcessDriverOperation.InspectExistingEvidence,
        ProcessDriverOperation.ReturnDiagnostics,
        ProcessDriverOperation.ReadProcessFacts,
        ProcessDriverOperation.ExplainDenial
    ];

    public static IReadOnlyList<ProcessDriverOperation> SideEffectOperations { get; } =
    [
        ProcessDriverOperation.MutateProcessState,
        ProcessDriverOperation.ExecuteCommand,
        ProcessDriverOperation.RestorePackage,
        ProcessDriverOperation.WriteArtifact,
        ProcessDriverOperation.WriteWorkspaceStorage,
        ProcessDriverOperation.CallOfficeGraph,
        ProcessDriverOperation.MutateEmailCategory,
        ProcessDriverOperation.CreateTask,
        ProcessDriverOperation.MutateBusinessRecord,
        ProcessDriverOperation.ApplyTransition,
        ProcessDriverOperation.ClaimDispatch,
        ProcessDriverOperation.ApplyFinalizer,
        ProcessDriverOperation.ScheduleRetry
    ];

    public static bool IsReadonlyVerificationOperation(ProcessDriverOperation operation)
    {
        return ReadonlyVerificationOperations.Contains(operation);
    }

    public static bool IsSideEffectOperation(ProcessDriverOperation operation)
    {
        return SideEffectOperations.Contains(operation);
    }

    public static ProcessDriverDenialReason ResolveReadonlyDenialReason(ProcessDriverOperation operation)
    {
        return operation switch
        {
            ProcessDriverOperation.ExecuteCommand or ProcessDriverOperation.RestorePackage => ProcessDriverDenialReason.UnsafeCommand,
            ProcessDriverOperation.CallOfficeGraph => ProcessDriverDenialReason.ExternalCallDenied,
            _ when IsSideEffectOperation(operation) => ProcessDriverDenialReason.MutationDenied,
            _ => ProcessDriverDenialReason.UnsupportedOperation
        };
    }
}
