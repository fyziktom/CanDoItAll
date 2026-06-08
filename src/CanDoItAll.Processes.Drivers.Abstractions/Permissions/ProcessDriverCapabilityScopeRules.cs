namespace CanDoItAll.Processes.Drivers.Abstractions.Permissions;

public static class ProcessDriverCapabilityScopeRules
{
    public static bool IsReadonlyScope(ProcessDriverCapabilityScope scope)
    {
        return !scope.AllowsProcessMutation &&
            !scope.AllowsExternalCalls &&
            !scope.AllowsWorkspaceWrites &&
            !scope.AllowsStorageWrites;
    }

    public static bool IsDotNetRustTranscriptVerificationScope(
        ProcessDriverCapabilityScope scope,
        ProcessDriverPermissionMode permissionMode)
    {
        return scope.Kind == ProcessDriverCapabilityScopeKind.DotNetRustTranscriptVerification &&
            permissionMode == scope.RequiredPermissionMode &&
            IsReadonlyScope(scope);
    }

    public static bool IsRuntimeFactsReadScope(
        ProcessDriverCapabilityScope scope,
        ProcessDriverPermissionMode permissionMode)
    {
        return scope.Kind == ProcessDriverCapabilityScopeKind.RuntimeFactsRead &&
            permissionMode == scope.RequiredPermissionMode &&
            IsReadonlyScope(scope);
    }
}
