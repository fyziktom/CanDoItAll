using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Modules.AgentFramework;

internal sealed class RemoteTemplatePublication {
    public RemoteTemplateKind Kind { get; init; }
    public Guid DefinitionId { get; init; }
    public long CreatedUtcTicks { get; init; }
}

internal sealed class RemoteTemplatePublicationConfiguration : IEntityTypeConfiguration<RemoteTemplatePublication> {
    public void Configure(EntityTypeBuilder<RemoteTemplatePublication> builder) {
        builder.ToTable("Workspace_RemoteTemplatePublications");
        builder.HasKey(publication => new { publication.Kind, publication.DefinitionId, publication.CreatedUtcTicks });
    }
}

public sealed class RemoteTemplatePublicationStore(IDbContextFactory<WorkflowDbContext> factory) {
    public async Task<IReadOnlySet<Guid>> ListAsync(RemoteTemplateKind kind, CancellationToken token) {
        await using var db = await factory.CreateDbContextAsync(token);
        return await db.Set<RemoteTemplatePublication>().Where(item => item.Kind == kind)
            .Select(item => item.DefinitionId).ToHashSetAsync(token);
    }

    public async Task<IReadOnlySet<(Guid DefinitionId, long CreatedUtcTicks)>> ListLifetimesAsync(RemoteTemplateKind kind, CancellationToken token) {
        await using var db = await factory.CreateDbContextAsync(token);
        var publications = await db.Set<RemoteTemplatePublication>().AsNoTracking().Where(item => item.Kind == kind)
            .Select(item => new { item.DefinitionId, item.CreatedUtcTicks }).ToListAsync(token);
        return publications.Select(item => (item.DefinitionId, item.CreatedUtcTicks)).ToHashSet();
    }

    public async Task<bool> IsPublishedAsync(RemoteTemplateKind kind, Guid id, CancellationToken token, DateTimeOffset? definitionCreatedAtUtc = null) {
        await using var db = await factory.CreateDbContextAsync(token);
        var generation = definitionCreatedAtUtc?.UtcTicks ?? 0;
        return await db.Set<RemoteTemplatePublication>().AnyAsync(item => item.Kind == kind && item.DefinitionId == id && item.CreatedUtcTicks == generation, token);
    }

    public async Task SetAsync(RemoteTemplateKind kind, Guid id, bool publish, CancellationToken token,
        Func<CancellationToken, ValueTask>? authorize = null, DateTimeOffset? definitionCreatedAtUtc = null) {
        if (kind is not (RemoteTemplateKind.SimpleChat or RemoteTemplateKind.Workflow) || id == Guid.Empty ||
            kind == RemoteTemplateKind.Workflow && definitionCreatedAtUtc is null) {
            throw new ArgumentException("Select a saved chat or workflow definition.");
        }
        await using var db = await factory.CreateDbContextAsync(token);
        var generation = definitionCreatedAtUtc?.UtcTicks ?? 0;
        var existing = await db.Set<RemoteTemplatePublication>().FindAsync([kind, id, generation], token);
        if (authorize is not null) {
            await authorize(token);
        }
        if (publish == (existing is not null)) {
            return;
        }
        if (publish) {
            db.Add(new RemoteTemplatePublication { Kind = kind, DefinitionId = id, CreatedUtcTicks = generation });
        } else {
            db.Remove(existing!);
        }
        try {
            await db.SaveChangesAsync(token);
        } catch (DbUpdateException) {
            if (await IsPublishedAsync(kind, id, token, definitionCreatedAtUtc) != publish) {
                throw;
            }
        }
    }
}
