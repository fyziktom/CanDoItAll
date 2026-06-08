namespace CanDoItAll.Processes.Drivers.Abstractions.Permissions;

public static class ProcessDriverOperationRules
{
    public static bool IsReadonlyVerificationOperation(ProcessDriverOperation operation)
    {
        return operation is
            ProcessDriverOperation.InspectExistingEvidence or
            ProcessDriverOperation.ReturnDiagnostics or
            ProcessDriverOperation.ReadProcessFacts or
            ProcessDriverOperation.ExplainDenial;
    }

    public static bool IsSideEffectOperation(ProcessDriverOperation operation)
    {
        return operation is
            ProcessDriverOperation.MutateProcessState or
            ProcessDriverOperation.ExecuteCommand or
            ProcessDriverOperation.RestorePackage or
            ProcessDriverOperation.WriteArtifact or
            ProcessDriverOperation.WriteWorkspaceStorage or
            ProcessDriverOperation.CallOfficeGraph or
            ProcessDriverOperation.MutateEmailCategory or
            ProcessDriverOperation.CreateTask or
            ProcessDriverOperation.MutateBusinessRecord or
            ProcessDriverOperation.ApplyTransition or
            ProcessDriverOperation.ClaimDispatch or
            ProcessDriverOperation.ApplyFinalizer or
            ProcessDriverOperation.ScheduleRetry;
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
