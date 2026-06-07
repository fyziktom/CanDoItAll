namespace CanDoItAll.Processes.Drivers.Abstractions.Permissions;

public sealed record ProcessDriverCapabilityScope(
    ProcessDriverCapabilityScopeKind Kind,
    ProcessDriverPermissionMode RequiredPermissionMode,
    bool AllowsProcessMutation,
    bool AllowsExternalCalls,
    bool AllowsWorkspaceWrites,
    bool AllowsStorageWrites);

public enum ProcessDriverCapabilityScopeKind
{
    DotNetRustTranscriptVerification = 1,
    RuntimeFactsRead = 2,
    OfficeEvidenceRead = 3,
    BusinessAnalysisRead = 4
}
