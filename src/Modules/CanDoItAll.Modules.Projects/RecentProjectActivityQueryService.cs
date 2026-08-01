using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Projects;

public static class RecentProjectActivityQueryLimits
{
    public const int MaximumItemCount = 6;
}

public sealed record RecentProjectActivityItem(
    Guid Id,
    string Name,
    ProjectStatus Status,
    string CurrentPhase,
    DateTimeOffset UpdatedAtUtc);

public interface IRecentProjectActivityQueryService
{
    Task<IReadOnlyList<RecentProjectActivityItem>> ListAsync(
        int itemCount,
        CancellationToken cancellationToken = default);
}

public sealed class RecentProjectActivityQueryService(
    IDbContextFactory<AppDbContext> dbContextFactory) : IRecentProjectActivityQueryService
{
    public async Task<IReadOnlyList<RecentProjectActivityItem>> ListAsync(
        int itemCount,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(itemCount, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            itemCount,
            RecentProjectActivityQueryLimits.MaximumItemCount);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.Set<Project>()
            .AsNoTracking()
            .OrderByDescending(project => project.UpdatedAtUtc)
            .ThenBy(project => project.Id)
            .Select(project => new RecentProjectActivityItem(
                project.Id,
                project.Name,
                project.Status,
                project.CurrentPhase,
                project.UpdatedAtUtc))
            .Take(itemCount)
            .ToListAsync(cancellationToken);
    }
}
