using CanDoItAll.AgentFramework.Llm.SimpleChats.Application;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Definitions;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence.Entities;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Ports;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence.Repositories;

public sealed class EfLlmChatDefinitionRepository(SimpleChatsDbContext dbContext) : ILlmChatDefinitionRepository, ILlmChatDefinitionCreateReceiptRepository
{
    public async Task<LlmChatDefinitionCreateClaim?> TryGetReceiptAsync(
        LlmChatDefinitionCreateKey key,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(key);
        var row = await dbContext.Set<LlmChatDefinitionCreateReceiptRow>().AsNoTracking()
            .SingleOrDefaultAsync(item => item.Producer == key.Scope.Producer &&
                item.Actor == key.Scope.Actor && item.HistoryNamespace == key.Scope.HistoryNamespace &&
                item.IntentId == key.IntentId.Value, cancellationToken).ConfigureAwait(false);
        if (row is null) {
            return null;
        }

        if (row.SemanticVersion != LlmChatDefinitionCreateClaim.SemanticVersion ||
            row.DefinitionRevision != 1 || row.OriginalConcurrencyToken != 0) {
            throw new InvalidOperationException("The definition create receipt has unsupported immutable metadata.");
        }

        return new(new(key, new(row.DefinitionId), new(row.DefinitionRevision), row.OriginalConcurrencyToken,
            row.CreatedAtUtc), new(row.SemanticFingerprint));
    }

    public async Task<bool> TryClaimAsync(
        LlmChatDefinitionCreateClaim claim,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(claim);
        if (!dbContext.Database.IsNpgsql() || dbContext.Database.CurrentTransaction is null) {
            throw new InvalidOperationException("A definition create receipt requires the owner's active PostgreSQL transaction.");
        }

        var receipt = claim.Receipt;
        var key = receipt.Key;
        var affected = await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "LlmChats_DefinitionCreateReceipts"
                ("Producer", "Actor", "HistoryNamespace", "IntentId", "SemanticVersion", "SemanticFingerprint",
                 "DefinitionId", "DefinitionRevision", "OriginalConcurrencyToken", "CreatedAtUtc")
            VALUES ({key.Scope.Producer}, {key.Scope.Actor}, {key.Scope.HistoryNamespace}, {key.IntentId.Value},
                {LlmChatDefinitionCreateClaim.SemanticVersion}, {claim.Fingerprint.Value}, {receipt.DefinitionId.Value},
                {receipt.DefinitionRevision.Value}, {receipt.OriginalConcurrencyToken}, {receipt.CreatedAtUtc})
            ON CONFLICT ("Producer", "Actor", "HistoryNamespace", "IntentId") DO NOTHING
            """, cancellationToken).ConfigureAwait(false);
        return affected == 1;
    }

    public void ForgetAttempt(LlmChatDefinitionId definitionId) {
        foreach (var entry in dbContext.ChangeTracker.Entries().Where(entry => entry.Entity switch {
            LlmChatDefinitionRow row => row.Id == definitionId.Value,
            LlmChatDefinitionRevisionRow row => row.DefinitionId == definitionId.Value,
            LlmChatDefinitionTagRow row => row.DefinitionId == definitionId.Value,
            _ => false
        }).ToArray()) {
            entry.State = EntityState.Detached;
        }
    }

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
