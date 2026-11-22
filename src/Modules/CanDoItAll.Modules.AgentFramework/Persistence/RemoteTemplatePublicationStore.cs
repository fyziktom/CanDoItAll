using CanDoItAll.AgentFramework.Llm.SimpleChats.Ports;
using CanDoItAll.RemoteInstances;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Modules.AgentFramework;

internal sealed class RemoteTemplatePublication {
    public RemoteTemplateKind Kind { get; init; }
    public Guid DefinitionId { get; init; }
    public long CreatedUtcTicks { get; init; }
    public bool ApiAvailable { get; set; } = true;
    public bool SharedTemplate { get; set; } = true;
}

internal sealed class RemoteTemplatePublicationConfiguration : IEntityTypeConfiguration<RemoteTemplatePublication> {
    public void Configure(EntityTypeBuilder<RemoteTemplatePublication> builder) {
        builder.ToTable("Workspace_RemoteTemplatePublications");
        builder.HasKey(publication => new { publication.Kind, publication.DefinitionId, publication.CreatedUtcTicks });
        builder.Property(publication => publication.ApiAvailable).HasDefaultValue(true).IsConcurrencyToken();
        builder.Property(publication => publication.SharedTemplate).HasDefaultValue(true).IsConcurrencyToken();
    }
}

public sealed record DefinitionSharingSettings(bool ApiAvailable, bool SharedTemplate) {
    public static DefinitionSharingSettings Default { get; } = new(true, false);
}

public sealed class RemoteTemplatePublicationStore(IDbContextFactory<WorkflowDbContext> factory) : ILlmChatSharingReader {
    async Task<LlmChatSharingSnapshot> ILlmChatSharingReader.ReadAsync(Guid? definitionId, CancellationToken token) {
        await using var db = await factory.CreateDbContextAsync(token);
        var query = db.Set<RemoteTemplatePublication>().AsNoTracking()
            .Where(row => row.Kind == RemoteTemplateKind.SimpleChat && row.CreatedUtcTicks == 0);
        if (definitionId is { } id) {
            query = query.Where(row => row.DefinitionId == id);
        }
        var rows = await query.Select(row => new { row.DefinitionId, row.ApiAvailable, row.SharedTemplate }).ToListAsync(token);
        return rows.Count == 0 ? LlmChatSharingSnapshot.Default : new(
            rows.Where(row => row.SharedTemplate).Select(row => row.DefinitionId).ToHashSet(),
            rows.Where(row => !row.ApiAvailable).Select(row => row.DefinitionId).ToHashSet());
    }

    public async Task<IReadOnlySet<Guid>> ListAsync(RemoteTemplateKind kind, CancellationToken token) {
        await using var db = await factory.CreateDbContextAsync(token);
        return await db.Set<RemoteTemplatePublication>().Where(item => item.Kind == kind && item.SharedTemplate)
            .Select(item => item.DefinitionId).ToHashSetAsync(token);
    }

    public async Task<IReadOnlySet<(Guid DefinitionId, long CreatedUtcTicks)>> ListLifetimesAsync(RemoteTemplateKind kind, CancellationToken token) {
        await using var db = await factory.CreateDbContextAsync(token);
        var publications = await db.Set<RemoteTemplatePublication>().AsNoTracking().Where(item => item.Kind == kind && item.SharedTemplate)
            .Select(item => new { item.DefinitionId, item.CreatedUtcTicks }).ToListAsync(token);
        return publications.Select(item => (item.DefinitionId, item.CreatedUtcTicks)).ToHashSet();
    }

    public async Task<bool> IsPublishedAsync(RemoteTemplateKind kind, Guid id, CancellationToken token, DateTimeOffset? definitionCreatedAtUtc = null) {
        return (await GetSharingAsync(kind, id, token, definitionCreatedAtUtc)).SharedTemplate;
    }

    public async Task<DefinitionSharingSettings> GetSharingAsync(RemoteTemplateKind kind, Guid id, CancellationToken token,
        DateTimeOffset? definitionCreatedAtUtc = null) {
        await using var db = await factory.CreateDbContextAsync(token);
        var generation = definitionCreatedAtUtc?.UtcTicks ?? 0;
        return await db.Set<RemoteTemplatePublication>().AsNoTracking()
            .Where(row => row.Kind == kind && row.DefinitionId == id && row.CreatedUtcTicks == generation)
            .Select(row => new DefinitionSharingSettings(row.ApiAvailable, row.SharedTemplate)).SingleOrDefaultAsync(token)
            ?? DefinitionSharingSettings.Default;
    }

    public async Task<bool> IsApiAvailableAsync(RemoteTemplateKind kind, Guid id, CancellationToken token,
        DateTimeOffset? definitionCreatedAtUtc = null) {
        await using var db = await factory.CreateDbContextAsync(token);
        var disabled = db.Set<RemoteTemplatePublication>().Where(row => row.Kind == kind && row.DefinitionId == id && !row.ApiAvailable);
        if (kind == RemoteTemplateKind.Workflow && definitionCreatedAtUtc is null) {
            return (await ReadWorkflowOverridesAsync(db, disabled, token)).Count == 0;
        }
        var generation = definitionCreatedAtUtc?.UtcTicks ?? 0;
        return !await disabled.AnyAsync(row => row.CreatedUtcTicks == generation, token);
    }

    public async Task<IReadOnlySet<Guid>> ListApiUnavailableIdsAsync(RemoteTemplateKind kind, CancellationToken token) {
        await using var db = await factory.CreateDbContextAsync(token);
        var disabled = db.Set<RemoteTemplatePublication>().Where(row => row.Kind == kind && !row.ApiAvailable);
        return kind == RemoteTemplateKind.Workflow
            ? (await ReadWorkflowOverridesAsync(db, disabled, token)).Keys.ToHashSet()
            : await disabled.Select(row => row.DefinitionId).ToHashSetAsync(token);
    }

    internal static Task<IReadOnlyDictionary<Guid, DefinitionSharingSettings>> ReadCurrentWorkflowOverridesAsync(WorkflowDbContext db, CancellationToken token) =>
        ReadWorkflowOverridesAsync(db, db.Set<RemoteTemplatePublication>().Where(row => row.Kind == RemoteTemplateKind.Workflow), token);

    private static async Task<IReadOnlyDictionary<Guid, DefinitionSharingSettings>> ReadWorkflowOverridesAsync(
        WorkflowDbContext db, IQueryable<RemoteTemplatePublication> rows, CancellationToken token) {
        var lifetimes = await (from row in rows
            join head in db.Set<WorkflowDefinitionHeadRecord>() on row.DefinitionId equals head.WorkflowId
            join version in db.Set<WorkflowDefinitionRecord>() on new { head.WorkflowId, head.VersionId } equals new { version.WorkflowId, version.VersionId }
            select new { row.DefinitionId, row.CreatedUtcTicks, version.CreatedAtUtc, row.ApiAvailable, row.SharedTemplate }).ToListAsync(token);
        return lifetimes.Where(row => row.CreatedUtcTicks == row.CreatedAtUtc.UtcTicks)
            .ToDictionary(row => row.DefinitionId, row => new DefinitionSharingSettings(row.ApiAvailable, row.SharedTemplate));
    }

    public async Task SetAsync(RemoteTemplateKind kind, Guid id, bool publish, CancellationToken token,
        Func<CancellationToken, ValueTask>? authorize = null, DateTimeOffset? definitionCreatedAtUtc = null) {
        var expected = await GetSharingAsync(kind, id, token, definitionCreatedAtUtc);
        await SetSharingAsync(kind, id, expected, expected with { SharedTemplate = publish }, authorize, token, definitionCreatedAtUtc);
    }

    public async Task SetSharingAsync(RemoteTemplateKind kind, Guid id, DefinitionSharingSettings expected, DefinitionSharingSettings desired,
        Func<CancellationToken, ValueTask>? authorize, CancellationToken token, DateTimeOffset? definitionCreatedAtUtc = null) {
        if (kind is not (RemoteTemplateKind.SimpleChat or RemoteTemplateKind.Workflow) || id == Guid.Empty ||
            kind == RemoteTemplateKind.Workflow && definitionCreatedAtUtc is null) {
            throw new ArgumentException("Select a saved chat or workflow definition.");
        }
        await using var db = await factory.CreateDbContextAsync(token);
        var generation = definitionCreatedAtUtc?.UtcTicks ?? 0;
        var rows = db.Set<RemoteTemplatePublication>();
        var row = await rows.FindAsync([kind, id, generation], token);
        var current = row is null ? DefinitionSharingSettings.Default : new(row.ApiAvailable, row.SharedTemplate);
        if (authorize is not null) {
            await authorize(token);
        }
        if (current == desired) {
            return;
        }
        if (current != expected) {
            throw Changed();
        }
        if (desired == DefinitionSharingSettings.Default) {
            rows.Remove(row!);
        } else {
            row ??= rows.Add(new() { Kind = kind, DefinitionId = id, CreatedUtcTicks = generation }).Entity;
            row.ApiAvailable = desired.ApiAvailable;
            row.SharedTemplate = desired.SharedTemplate;
        }
        try {
            await db.SaveChangesAsync(token);
        } catch (DbUpdateException) {
            var persisted = await GetSharingAsync(kind, id, token, definitionCreatedAtUtc);
            if (persisted == desired) {
                return;
            }
            if (persisted != expected) {
                throw Changed();
            }
            throw;
        }
    }

    private static RemoteInstanceReadException Changed() => new("Sharing changed in another editor. Reload sharing and review your choices.");
}
