using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.LlmChats.Common;
using CanDoItAll.Modules.LlmChats.Definitions;
using CanDoItAll.Modules.LlmChats.Persistence.Entities;
using CanDoItAll.Modules.LlmChats.Persistence.Repositories;
using CanDoItAll.Modules.LlmChats.Ports;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.LlmChats.Persistence.ReadModels;

public sealed class EfLlmChatDefinitionReadStore(AppDbContext dbContext) : ILlmChatDefinitionReadStore
{
    public async Task<LlmChatDefinitionReadModel?> TryGetAsync(
        LlmChatDefinitionId id,
        CancellationToken cancellationToken = default)
    {
        var row = await JoinRevision(
                dbContext.Set<LlmChatDefinitionRow>()
                    .AsNoTracking()
                    .Where(definition => definition.Id == id.Value))
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
        {
            return null;
        }

        var tags = await LoadTagsAsync([id.Value], cancellationToken).ConfigureAwait(false);
        return Map(row.Definition, row.Revision, tags);
    }

    public async Task<LlmChatPage<LlmChatDefinitionReadModel, LlmChatDefinitionCursor>> ListPageAsync(
        int take,
        LlmChatDefinitionCursor? cursor,
        LlmChatDefinitionStatus? status,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(take, 1);
        var query = dbContext.Set<LlmChatDefinitionRow>().AsNoTracking();
        if (status is { } value)
        {
            query = query.Where(item => item.Status == value);
        }

        if (cursor is { } position)
        {
            query = query.Where(item =>
                item.UpdatedAtUtc < position.UpdatedAtUtc ||
                item.UpdatedAtUtc == position.UpdatedAtUtc &&
                item.Id.CompareTo(position.DefinitionId.Value) > 0);
        }

        var pageQuery = query
            .OrderByDescending(item => item.UpdatedAtUtc)
            .ThenBy(item => item.Id)
            .Take(checked(take + 1));
        var rows = await JoinRevision(pageQuery)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        var pageRows = rows.Take(take).ToArray();
        var tags = await LoadTagsAsync(
            [.. pageRows.Select(item => item.Definition.Id)],
            cancellationToken).ConfigureAwait(false);
        var items = pageRows
            .Select(item => Map(item.Definition, item.Revision, tags))
            .ToArray();
        LlmChatDefinitionCursor? nextCursor = rows.Length > take && pageRows.Length > 0
            ? new LlmChatDefinitionCursor(
                pageRows[^1].Definition.UpdatedAtUtc,
                new LlmChatDefinitionId(pageRows[^1].Definition.Id))
            : null;
        return new LlmChatPage<LlmChatDefinitionReadModel, LlmChatDefinitionCursor>(items, nextCursor);
    }

    private IQueryable<DefinitionRow> JoinRevision(IQueryable<LlmChatDefinitionRow> definitions)
        => from definition in definitions
           join revision in dbContext.Set<LlmChatDefinitionRevisionRow>().AsNoTracking()
               on new { DefinitionId = definition.Id, Revision = definition.CurrentRevision }
               equals new { revision.DefinitionId, revision.Revision }
           select new DefinitionRow(definition, revision);

    private async Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> LoadTagsAsync(
        IReadOnlyList<Guid> definitionIds,
        CancellationToken cancellationToken)
    {
        if (definitionIds.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<string>>();
        }

        var rows = await dbContext.Set<LlmChatDefinitionTagRow>()
            .AsNoTracking()
            .Where(row => definitionIds.Contains(row.DefinitionId))
            .OrderBy(row => row.DefinitionId)
            .ThenBy(row => row.Tag)
            .Select(row => new { row.DefinitionId, row.Tag })
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows
            .GroupBy(row => row.DefinitionId)
            .ToDictionary(
                group => group.Key,
                IReadOnlyList<string> (group) => [.. group.Select(row => row.Tag)]);
    }

    private static LlmChatDefinitionReadModel Map(
        LlmChatDefinitionRow definition,
        LlmChatDefinitionRevisionRow revision,
        IReadOnlyDictionary<Guid, IReadOnlyList<string>> tags)
        => new(
            LlmChatPersistenceMapper.ToDomain(definition),
            LlmChatPersistenceMapper.ToDomain(revision),
            tags.GetValueOrDefault(definition.Id) ?? []);

    private sealed record DefinitionRow(
        LlmChatDefinitionRow Definition,
        LlmChatDefinitionRevisionRow Revision);
}
