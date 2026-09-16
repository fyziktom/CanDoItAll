using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Prompts;

public sealed record PromptArtifactProjectionFact(Guid Id, Guid? ProjectId, PromptGalleryItemKind Kind,
    string Phase, string Title, string Summary, PromptArtifactStatus Status,
    DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);

public sealed record PromptArtifactBindingFact(Guid Id, Guid? ProjectId, PromptGalleryItemKind Kind);

public sealed record PromptSearchProjectionState(bool IsProjectable, DateTimeOffset UpdatedAtUtc);

public interface IPromptArtifactProjectionQueryService {
    Task<IReadOnlyList<PromptArtifactProjectionFact>> ListProjectFactsAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PromptArtifactProjectionFact>> ListProjectFactsForMutationAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<PromptArtifactBindingFact?> GetBindingFactAsync(Guid promptId, CancellationToken cancellationToken = default);
    Task<PromptArtifactBindingFact?> GetBindingFactForMutationAsync(Guid promptId, CancellationToken cancellationToken = default);
    Task<PromptSearchProjectionState?> GetSearchStateForMutationAsync(Guid promptId, CancellationToken cancellationToken = default);
}

public sealed class PromptArtifactProjectionQueryService(
    IDbContextFactory<PromptsDbContext> factory,
    DbContextOptions<PromptsDbContext> options,
    CoordinatedDatabaseTransaction transactions) : IPromptArtifactProjectionQueryService {
    public async Task<IReadOnlyList<PromptArtifactProjectionFact>> ListProjectFactsAsync(Guid projectId,
        CancellationToken cancellationToken = default) {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        return await ReadFactsAsync(context, projectId, cancellationToken);
    }

    public async Task<IReadOnlyList<PromptArtifactProjectionFact>> ListProjectFactsForMutationAsync(Guid projectId,
        CancellationToken cancellationToken = default) {
        await using var context = await transactions.CreateEnlistedAsync(options, static value => new PromptsDbContext(value), cancellationToken);
        return await ReadFactsAsync(context, projectId, cancellationToken);
    }

    public async Task<PromptArtifactBindingFact?> GetBindingFactAsync(Guid promptId,
        CancellationToken cancellationToken = default) {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        return await ReadBindingAsync(context, promptId, cancellationToken);
    }

    public async Task<PromptArtifactBindingFact?> GetBindingFactForMutationAsync(Guid promptId,
        CancellationToken cancellationToken = default) {
        await using var context = await transactions.CreateEnlistedAsync(options, static value => new PromptsDbContext(value), cancellationToken);
        return await ReadBindingAsync(context, promptId, cancellationToken);
    }

    public async Task<PromptSearchProjectionState?> GetSearchStateForMutationAsync(Guid promptId,
        CancellationToken cancellationToken = default) {
        await using var context = await transactions.CreateEnlistedAsync(options, static value => new PromptsDbContext(value), cancellationToken);
        var artifacts = context.Database.IsNpgsql()
            ? context.Set<PromptArtifact>().FromSqlInterpolated($"""
                SELECT * FROM "Prompts_PromptArtifacts" WHERE "Id" = {promptId} FOR SHARE
                """)
            : context.Set<PromptArtifact>().Where(item => item.Id == promptId);
        return await artifacts.AsNoTracking().Select(item => new PromptSearchProjectionState(
            !item.IsArchived && item.Status == PromptArtifactStatus.Final && item.CurrentVersionNumber > 0,
            item.UpdatedAtUtc)).SingleOrDefaultAsync(cancellationToken);
    }

    private static Task<PromptArtifactBindingFact?> ReadBindingAsync(
        PromptsDbContext context, Guid promptId, CancellationToken cancellationToken) {
        return context.Set<PromptArtifact>().AsNoTracking().Where(item => item.Id == promptId)
            .Select(item => new PromptArtifactBindingFact(item.Id, item.ProjectId, item.Kind))
            .SingleOrDefaultAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<PromptArtifactProjectionFact>> ReadFactsAsync(
        PromptsDbContext context, Guid projectId, CancellationToken cancellationToken) {
        ArgumentOutOfRangeException.ThrowIfEqual(projectId, Guid.Empty);
        return await context.Set<PromptArtifact>().AsNoTracking()
            .Where(item => item.ProjectId == projectId && !item.IsArchived)
            .OrderBy(item => item.CreatedAtUtc)
            .Select(item => new PromptArtifactProjectionFact(item.Id, item.ProjectId, item.Kind, item.Phase,
                item.Title, item.Summary, item.Status, item.CreatedAtUtc, item.UpdatedAtUtc))
            .ToListAsync(cancellationToken);
    }
}
