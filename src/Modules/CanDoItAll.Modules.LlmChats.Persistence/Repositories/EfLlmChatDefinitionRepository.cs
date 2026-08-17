using CanDoItAll.Modules.LlmChats.Common;
using CanDoItAll.Modules.LlmChats.Definitions;
using CanDoItAll.Modules.LlmChats.Persistence.Entities;
using CanDoItAll.Modules.LlmChats.Ports;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.LlmChats.Persistence.Repositories;

public sealed class EfLlmChatDefinitionRepository(AppDbContext dbContext) : ILlmChatDefinitionRepository
{
    public async Task<LlmChatDefinition?> TryGetAsync(
        LlmChatDefinitionId id,
        CancellationToken cancellationToken = default)
    {
        var row = await dbContext.Set<LlmChatDefinitionRow>()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id.Value, cancellationToken)
            .ConfigureAwait(false);
        return row is null ? null : LlmChatPersistenceMapper.ToDomain(row);
    }

    public async Task<LlmChatDefinition?> TryGetForUpdateAsync(
        LlmChatDefinitionId id,
        CancellationToken cancellationToken = default)
    {
        var row = await dbContext.Set<LlmChatDefinitionRow>()
            .FromSqlInterpolated($"""
                SELECT *
                FROM "LlmChats_Definitions"
                WHERE "Id" = {id.Value}
                FOR UPDATE
                """)
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return row is null ? null : LlmChatPersistenceMapper.ToDomain(row);
    }

    public async Task<LlmChatDefinitionRevision?> TryGetRevisionAsync(
        LlmChatDefinitionId id,
        LlmChatDefinitionRevisionNumber revision,
        CancellationToken cancellationToken = default)
    {
        var row = await dbContext.Set<LlmChatDefinitionRevisionRow>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.DefinitionId == id.Value && item.Revision == revision.Value,
                cancellationToken)
            .ConfigureAwait(false);
        return row is null ? null : LlmChatPersistenceMapper.ToDomain(row);
    }

    public async Task<IReadOnlyList<string>> ListTagsAsync(
        LlmChatDefinitionId id,
        CancellationToken cancellationToken = default)
        => await dbContext.Set<LlmChatDefinitionTagRow>()
            .AsNoTracking()
            .Where(row => row.DefinitionId == id.Value)
            .OrderBy(row => row.Tag)
            .Select(row => row.Tag)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task ReplaceTagsAsync(
        LlmChatDefinitionId id,
        IReadOnlyList<string> tags,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tags);
        var desiredTags = tags.ToHashSet(StringComparer.Ordinal);
        var existingTags = await dbContext.Set<LlmChatDefinitionTagRow>()
            .Where(row => row.DefinitionId == id.Value)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var existingTagNames = existingTags
            .Select(row => row.Tag)
            .ToHashSet(StringComparer.Ordinal);
        dbContext.RemoveRange(existingTags.Where(row => !desiredTags.Contains(row.Tag)));
        dbContext.AddRange(desiredTags
            .Where(tag => !existingTagNames.Contains(tag))
            .Select(tag => new LlmChatDefinitionTagRow
        {
            DefinitionId = id.Value,
            Tag = tag
        }));
    }

    public Task CreateAsync(
        LlmChatDefinition definition,
        LlmChatDefinitionRevision revision,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(revision);
        if (definition.Id != revision.DefinitionId || definition.CurrentRevision != revision.Revision)
        {
            throw new ArgumentException("A new definition must reference its supplied initial revision.", nameof(revision));
        }

        dbContext.Add(LlmChatPersistenceMapper.ToRow(definition));
        dbContext.Add(LlmChatPersistenceMapper.ToRow(revision));
        return Task.CompletedTask;
    }

    public async Task ReplaceAsync(
        LlmChatDefinition definition,
        long expectedConcurrencyToken,
        LlmChatDefinitionRevision? appendedRevision,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentOutOfRangeException.ThrowIfNegative(expectedConcurrencyToken);
        var expectedRevision = appendedRevision is null
            ? definition.CurrentRevision.Value
            : checked(definition.CurrentRevision.Value - 1);
        if (appendedRevision is not null &&
            (appendedRevision.DefinitionId != definition.Id || appendedRevision.Revision != definition.CurrentRevision))
        {
            throw new ArgumentException("An appended revision must be the definition's new current revision.", nameof(appendedRevision));
        }

        var affected = await dbContext.Set<LlmChatDefinitionRow>()
            .Where(row => row.Id == definition.Id.Value &&
                          row.ConcurrencyToken == expectedConcurrencyToken &&
                          row.CurrentRevision == expectedRevision)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(row => row.Name, definition.Name)
                .SetProperty(row => row.Summary, definition.Summary)
                .SetProperty(row => row.AvatarImageUrl, definition.AvatarImageUrl)
                .SetProperty(row => row.Status, definition.Status)
                .SetProperty(row => row.CurrentRevision, definition.CurrentRevision.Value)
                .SetProperty(row => row.UpdatedAtUtc, definition.UpdatedAtUtc)
                .SetProperty(row => row.ConcurrencyToken, definition.ConcurrencyToken),
                cancellationToken)
            .ConfigureAwait(false);
        if (affected != 1)
        {
            throw new LlmChatPersistenceConcurrencyException(LlmChatConcurrencyResource.Definition);
        }

        if (appendedRevision is not null)
        {
            dbContext.Add(LlmChatPersistenceMapper.ToRow(appendedRevision));
        }
    }
}
