namespace CanDoItAll.Modules.Workbench;

internal sealed record ProjectStructureAgentAnalyticsEntry(
    string OperationName,
    Guid? ProjectId,
    string? NodeKey,
    ProjectStructureLeaseScopeKind? ScopeKind,
    bool Succeeded,
    long DurationMs,
    int WarningCount,
    string? ErrorCode,
    DateTimeOffset OccurredAtUtc);

internal sealed record ProjectStructureAgentAnalyticsResponse(
    IReadOnlyList<ProjectStructureAgentAnalyticsEntry> Entries);

internal static class ProjectStructureAgentAnalyticsBoundary
{
    internal const string OperationFailedErrorCode = "ProjectStructureOperationFailed";

    public static ProjectStructureAgentAnalyticsEntry Project(ProjectStructureAnalyticsEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return new ProjectStructureAgentAnalyticsEntry(
            entry.OperationName,
            entry.ProjectId,
            entry.NodeKey,
            entry.ScopeKind,
            entry.Succeeded,
            entry.DurationMs,
            entry.WarningCount,
            entry.Succeeded ? null : OperationFailedErrorCode,
            entry.OccurredAtUtc);
    }
}
