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

    public static bool IsOfficeEvidenceReadScope(
        ProcessDriverCapabilityScope scope,
        ProcessDriverPermissionMode permissionMode)
    {
        return scope.Kind == ProcessDriverCapabilityScopeKind.OfficeEvidenceRead &&
            permissionMode == scope.RequiredPermissionMode &&
            IsReadonlyScope(scope);
    }

    public static bool IsBusinessAnalysisReadScope(
        ProcessDriverCapabilityScope scope,
        ProcessDriverPermissionMode permissionMode)
    {
        return scope.Kind == ProcessDriverCapabilityScopeKind.BusinessAnalysisRead &&
            permissionMode == scope.RequiredPermissionMode &&
            IsReadonlyScope(scope);
    }

    public static bool IsArtifactEvidenceReadScope(
        ProcessDriverCapabilityScope scope,
        ProcessDriverPermissionMode permissionMode)
    {
        return scope.Kind == ProcessDriverCapabilityScopeKind.ArtifactEvidenceRead &&
            permissionMode == scope.RequiredPermissionMode &&
            IsReadonlyScope(scope);
    }
}
