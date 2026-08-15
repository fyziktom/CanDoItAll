using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.LlmChats.Common;
using CanDoItAll.Modules.LlmChats.Conversations;
using CanDoItAll.Modules.LlmChats.Persistence.Entities;
using CanDoItAll.Modules.LlmChats.Ports;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.LlmChats.Persistence.Repositories;

public sealed class EfLlmChatConversationRepository(AppDbContext dbContext) : ILlmChatConversationRepository
{
    public async Task<LlmChatConversation?> TryGetAsync(
        LlmChatConversationId id,
        CancellationToken cancellationToken = default)
    {
        var row = await dbContext.Set<LlmChatConversationRow>()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id.Value, cancellationToken)
            .ConfigureAwait(false);
        return row is null ? null : LlmChatPersistenceMapper.ToDomain(row);
    }

    public Task CreateAsync(LlmChatConversation conversation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(conversation);
        dbContext.Add(LlmChatPersistenceMapper.ToRow(conversation));
        return Task.CompletedTask;
    }

    public async Task ReplaceAsync(
        LlmChatConversation conversation,
        long expectedConcurrencyToken,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(conversation);
        ArgumentOutOfRangeException.ThrowIfNegative(expectedConcurrencyToken);
        var affected = await dbContext.Set<LlmChatConversationRow>()
            .Where(row => row.Id == conversation.Id.Value && row.ConcurrencyToken == expectedConcurrencyToken)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(row => row.Title, conversation.Title)
                .SetProperty(row => row.Status, conversation.Status)
                .SetProperty(row => row.UpdatedAtUtc, conversation.UpdatedAtUtc)
                .SetProperty(row => row.ConcurrencyToken, conversation.ConcurrencyToken),
                cancellationToken)
            .ConfigureAwait(false);
        if (affected != 1)
        {
            throw new DbUpdateConcurrencyException("The LLM Chat conversation changed before it could be persisted.");
        }
    }
}
