using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

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
    DateTimeOffset UpdatedAtUtc);

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

public sealed class ProjectRecordQueryService(
    IDbContextFactory<AppDbContext> dbContextFactory) : IProjectRecordQueryService
{
    public async Task<ProjectRecordQueryItem?> GetAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        if (projectId == Guid.Empty)
        {
            throw new ArgumentException("A project is required.", nameof(projectId));
        }

        return (await GetManyAsync([projectId], cancellationToken)).SingleOrDefault();
    }

    public async Task<IReadOnlyList<ProjectRecordQueryItem>> GetManyAsync(
        IReadOnlyCollection<Guid> projectIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(projectIds);
        if (projectIds.Any(projectId => projectId == Guid.Empty))
        {
            throw new ArgumentException("Project identifiers cannot be empty.", nameof(projectIds));
        }

        var distinctProjectIds = projectIds.Distinct().ToList();
        if (distinctProjectIds.Count == 0)
        {
            return [];
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Set<Project>()
            .AsNoTracking()
            .Where(project => distinctProjectIds.Contains(project.Id))
            .OrderBy(project => project.Name)
            .ThenBy(project => project.Id)
            .Select(project => new ProjectRecordQueryItem(
                project.Id,
                project.Name,
                project.Status,
                project.CurrentPhase,
                project.Description,
                project.UpdatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<ProjectRecordPage> SearchAsync(
        ProjectRecordQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var normalized = Normalize(query);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        IQueryable<Project> candidates = dbContext.Set<Project>().AsNoTracking();
        candidates = ApplyScope(candidates, normalized.Scope);

        if (!string.IsNullOrEmpty(normalized.SearchText))
        {
            var search = normalized.SearchText.ToUpperInvariant();
            candidates = candidates.Where(project =>
                project.Name.ToUpper().Contains(search) ||
                project.Description.ToUpper().Contains(search) ||
                project.Objective.ToUpper().Contains(search) ||
                project.CurrentPhase.ToUpper().Contains(search));
        }

        var totalCount = await candidates.CountAsync(cancellationToken);
        var items = await candidates
            .OrderBy(project => project.Name)
            .ThenBy(project => project.Id)
            .Skip(normalized.PageIndex * normalized.PageSize)
            .Take(normalized.PageSize)
            .Select(project => new ProjectRecordQueryItem(
                project.Id,
                project.Name,
                project.Status,
                project.CurrentPhase,
                project.Description,
                project.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

        return new ProjectRecordPage(
            items,
            normalized.PageIndex,
            normalized.PageSize,
            totalCount);
    }

    private static ProjectRecordQuery Normalize(ProjectRecordQuery query)
    {
        if (query.PageIndex < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                query.PageIndex,
                "Project record page index cannot be negative.");
        }

        if (query.PageSize is < 1 or > ProjectRecordQueryLimits.MaximumPageSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                query.PageSize,
                $"Project record page size must be between 1 and {ProjectRecordQueryLimits.MaximumPageSize}.");
        }

        if (query.PageIndex > int.MaxValue / query.PageSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                query.PageIndex,
                "Project record page offset is too large.");
        }

        var searchText = query.SearchText?.Trim() ?? string.Empty;
        if (searchText.Length > ProjectRecordQueryLimits.MaximumSearchLength)
        {
            throw new ArgumentException(
                $"Project record search cannot exceed {ProjectRecordQueryLimits.MaximumSearchLength} characters.",
                nameof(query));
        }

        if (!Enum.IsDefined(query.Scope))
        {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                query.Scope,
                "Project record scope is not supported.");
        }

        return query with { SearchText = searchText };
    }

    private static IQueryable<Project> ApplyScope(
        IQueryable<Project> candidates,
        ProjectRecordScope scope)
    {
        return scope switch
        {
            ProjectRecordScope.All => candidates,
            ProjectRecordScope.Open => candidates.Where(project =>
                project.Status == ProjectStatus.Draft ||
                project.Status == ProjectStatus.Active ||
                project.Status == ProjectStatus.OnHold),
            ProjectRecordScope.Active => candidates.Where(project =>
                project.Status == ProjectStatus.Active),
            ProjectRecordScope.Completed => candidates.Where(project =>
                project.Status == ProjectStatus.Completed),
            ProjectRecordScope.Archived => candidates.Where(project =>
                project.Status == ProjectStatus.Archived),
            _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "Project record scope is not supported.")
        };
    }
}
