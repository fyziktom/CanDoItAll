using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.LlmChats.Common;
using CanDoItAll.Modules.LlmChats.Conversations;
using CanDoItAll.Modules.LlmChats.Definitions;
using CanDoItAll.Modules.LlmChats.Operations;
using CanDoItAll.Modules.LlmChats.Persistence.Entities;
using CanDoItAll.Modules.LlmChats.Persistence.Repositories;
using CanDoItAll.Modules.LlmChats.Ports;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.LlmChats.Persistence.ReadModels;

public sealed class EfLlmChatConversationReadStore(AppDbContext dbContext) : ILlmChatConversationReadStore
{
    public async Task<LlmChatConversationReadModel?> TryGetAsync(
        LlmChatConversationId id,
        CancellationToken cancellationToken = default)
    {
        var row = await JoinReadModel(
                dbContext.Set<LlmChatConversationRow>()
                    .AsNoTracking()
                    .Where(conversation => conversation.Id == id.Value))
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return row is null ? null : Map(row);
    }

    public async Task<LlmChatPage<LlmChatConversationReadModel, LlmChatConversationCursor>> ListPageAsync(
        int take,
        LlmChatConversationCursor? cursor,
        LlmChatDefinitionId? definitionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(take, 1);
        var query = dbContext.Set<LlmChatConversationRow>().AsNoTracking();
        if (definitionId is { } id)
        {
            query = query.Where(item => item.DefinitionId == id.Value);
        }

        if (cursor is { } position)
        {
            query = query.Where(item =>
                item.UpdatedAtUtc < position.UpdatedAtUtc ||
                item.UpdatedAtUtc == position.UpdatedAtUtc &&
                item.Id.CompareTo(position.ConversationId.Value) > 0);
        }

        var pageQuery = query
            .OrderByDescending(item => item.UpdatedAtUtc)
            .ThenBy(item => item.Id)
            .Take(checked(take + 1));
        var rows = await JoinReadModel(pageQuery)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        var pageRows = rows.Take(take).ToArray();
        var items = pageRows.Select(Map).ToArray();
        LlmChatConversationCursor? nextCursor = rows.Length > take && pageRows.Length > 0
            ? new LlmChatConversationCursor(
                pageRows[^1].Conversation.UpdatedAtUtc,
                new LlmChatConversationId(pageRows[^1].Conversation.Id))
            : null;
        return new LlmChatPage<LlmChatConversationReadModel, LlmChatConversationCursor>(items, nextCursor);
    }

    public async Task<LlmChatTranscriptReadModel?> TryGetTranscriptPageAsync(
        LlmChatConversationId id,
        int take,
        LlmChatTranscriptCursor? cursor,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(take, 1);
        var conversation = await TryGetAsync(id, cancellationToken).ConfigureAwait(false);
        if (conversation is null)
        {
            return null;
        }

        var afterSequence = cursor?.Sequence ?? 0;
        var rows = await dbContext.Set<LlmChatMessageRow>()
            .AsNoTracking()
            .Where(row =>
                row.ConversationId == id.Value &&
                row.Sequence > afterSequence &&
                row.Role != LlmMessageRole.System)
            .OrderBy(row => row.Sequence)
            .Take(checked(take + 1))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        var pageRows = rows.Take(take).ToArray();
        LlmChatTranscriptCursor? nextCursor = rows.Length > take && pageRows.Length > 0
            ? new LlmChatTranscriptCursor(pageRows[^1].Sequence)
            : null;
        return new LlmChatTranscriptReadModel(
            conversation,
            [.. pageRows.Select(MapMessage)],
            nextCursor);
    }

    public async Task<LlmChatConversationTurnEvidence?> TryInspectTurnAsync(
        LlmChatConversationId conversationId,
        LlmChatOperationId operationId,
        CancellationToken cancellationToken = default)
    {
        var conversation = await TryGetAsync(conversationId, cancellationToken).ConfigureAwait(false);
        if (conversation is null)
        {
            return null;
        }

        var assistant = await dbContext.Set<LlmChatMessageRow>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                row => row.ConversationId == conversationId.Value &&
                       row.TurnId == operationId.Value &&
                       row.Role == LlmMessageRole.Assistant,
                cancellationToken)
            .ConfigureAwait(false);
        return new LlmChatConversationTurnEvidence(
            conversation.Transcript,
            operationId,
            conversation.Transcript.HasActiveTurn &&
            await IsExactActiveTurnAsync(conversationId, operationId, cancellationToken).ConfigureAwait(false),
            assistant is null
                ? null
                : new LlmChatAssistantTurnEvidence(
                    assistant.EntryId,
                    assistant.Text,
                    assistant.Model,
                    MapUsage(assistant) ?? LlmUsage.Zero,
                    assistant.CreatedAtUtc));
    }

    private async Task<bool> IsExactActiveTurnAsync(
        LlmChatConversationId conversationId,
        LlmChatOperationId operationId,
        CancellationToken cancellationToken)
        => await dbContext.Set<LlmChatTranscriptRow>()
            .AsNoTracking()
            .AnyAsync(
                row => row.ConversationId == conversationId.Value && row.ActiveTurnId == operationId.Value,
                cancellationToken)
            .ConfigureAwait(false);

    private IQueryable<ConversationRow> JoinReadModel(IQueryable<LlmChatConversationRow> conversations)
        => from conversation in conversations
           join definition in dbContext.Set<LlmChatDefinitionRow>().AsNoTracking()
               on conversation.DefinitionId equals definition.Id
           join transcript in dbContext.Set<LlmChatTranscriptRow>().AsNoTracking()
               on conversation.Id equals transcript.ConversationId
           select new ConversationRow(conversation, definition.Name, transcript);

    private static LlmChatConversationReadModel Map(ConversationRow row)
        => new(
            LlmChatPersistenceMapper.ToDomain(row.Conversation),
            row.DefinitionName,
            new LlmChatConversationEngineState(
                new LlmChatConversationId(row.Conversation.Id),
                row.Transcript.TranscriptRevision,
                row.Transcript.ActiveTurnId is { } activeTurnId
                    ? new LlmChatOperationId(activeTurnId)
                    : null,
                row.Conversation.CreatedAtUtc,
                row.Conversation.UpdatedAtUtc));

    private static LlmChatTranscriptEntry MapMessage(LlmChatMessageRow row)
        => new(
            row.EntryId,
            row.TurnId,
            row.Role,
            row.Text,
            row.CreatedAtUtc,
            row.Model,
            MapUsage(row));

    private static LlmUsage? MapUsage(LlmChatMessageRow row)
    {
        var hasAny = row.InputTokens.HasValue || row.OutputTokens.HasValue || row.CachedInputTokens.HasValue;
        var hasAll = row.InputTokens.HasValue && row.OutputTokens.HasValue && row.CachedInputTokens.HasValue;
        if (hasAny != hasAll)
        {
            throw new LlmConversationException(
                LlmConversationFailureKind.StorageCorrupted,
                row.ConversationId,
                "Stored transcript message usage is incomplete.");
        }

        return hasAll
            ? new LlmUsage(row.InputTokens!.Value, row.OutputTokens!.Value, row.CachedInputTokens!.Value)
            : null;
    }

    private sealed record ConversationRow(
        LlmChatConversationRow Conversation,
        string DefinitionName,
        LlmChatTranscriptRow Transcript);
}
