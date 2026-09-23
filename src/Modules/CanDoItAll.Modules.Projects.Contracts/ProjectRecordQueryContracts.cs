namespace CanDoItAll.Modules.Projects;

public enum ProjectRecordScope
{
    All,
    Open,
    Active,
    Completed,
    Archived
}

public static class ProjectRecordQueryLimits
{
    public const int DefaultPageSize = 24;
    public const int MaximumPageSize = 100;
    public const int MaximumSearchLength = 200;
    public const int MaximumReferenceCount = 1024;
}

public sealed record ProjectRecordQuery(
    string SearchText = "",
    ProjectRecordScope Scope = ProjectRecordScope.All,
    int PageIndex = 0,
    int PageSize = ProjectRecordQueryLimits.DefaultPageSize);

public sealed record ProjectRecordQueryItem(
    Guid Id,
    string Name,
    ProjectStatus Status,
    string CurrentPhase,
    string Description,
    DateTimeOffset UpdatedAtUtc) {
    [System.Text.Json.Serialization.JsonIgnore]
    public Guid? LifetimeId { get; init; }
}

public sealed record ProjectRecordPage(
    IReadOnlyList<ProjectRecordQueryItem> Items,
    int PageIndex,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => TotalCount == 0
        ? 0
        : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public interface IProjectRecordQueryService
{
    Task<ProjectRecordQueryItem?> GetAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProjectRecordQueryItem>> GetManyAsync(
        IReadOnlyCollection<Guid> projectIds,
        CancellationToken cancellationToken = default);

    Task<ProjectRecordPage> SearchAsync(
        ProjectRecordQuery query,
        CancellationToken cancellationToken = default);
}
