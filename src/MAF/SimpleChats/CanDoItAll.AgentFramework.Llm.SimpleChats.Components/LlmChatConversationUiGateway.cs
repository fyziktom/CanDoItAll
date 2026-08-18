using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Application;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Conversations;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Components;

public sealed record LlmChatConversationListItem(
    Guid ConversationId,
    Guid DefinitionId,
    int DefinitionRevision,
    string Title,
    string DefinitionName,
    LlmChatConversationStatus Status,
    LlmChatConversationOrigin Origin,
    long ConcurrencyToken,
    long TranscriptRevision,
    Guid? ActiveOperationId,
    DateTimeOffset UpdatedAtUtc);

public sealed record LlmChatMessageListItem(
    Guid EntryId,
    Guid TurnId,
    LlmMessageRole Role,
    string Text,
    DateTimeOffset CreatedAtUtc,
    string Model);

public sealed record LlmChatConversationView(
    LlmChatConversationListItem Conversation,
    IReadOnlyList<LlmChatMessageListItem> Messages,
    LlmChatTranscriptCursor? NextMessageCursor);

public interface ILlmChatConversationUiGateway
{
    Task<LlmChatUiResult<LlmChatPage<LlmChatConversationListItem, LlmChatConversationCursor>>> ListPageAsync(
        LlmChatConversationQuery query,
        CancellationToken cancellationToken = default);

    Task<LlmChatUiResult<LlmChatConversationView>> GetAsync(
        Guid conversationId,
        LlmChatTranscriptQuery transcriptQuery,
        CancellationToken cancellationToken = default);

    Task<LlmChatUiResult<LlmChatConversationView>> CreateAsync(
        Guid definitionId,
        string title,
        CancellationToken cancellationToken = default);

    Task<LlmChatUiResult<LlmChatConversationView>> RenameAsync(
        Guid conversationId,
        string title,
        long expectedConcurrencyToken,
        long expectedTranscriptRevision,
        CancellationToken cancellationToken = default);

    Task<LlmChatUiResult<LlmChatConversationView>> ArchiveAsync(
        Guid conversationId,
        long expectedConcurrencyToken,
        CancellationToken cancellationToken = default);
}

public sealed class LlmChatConversationUiGateway(
    ILlmChatConversationApplicationService conversations,
    ILlmChatUiAuthorizationFacade authorization) : ILlmChatConversationUiGateway
{
    public async Task<LlmChatUiResult<LlmChatPage<LlmChatConversationListItem, LlmChatConversationCursor>>> ListPageAsync(
        LlmChatConversationQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (!await authorization.IsAllowedAsync(LlmChatUiPermission.Read, cancellationToken))
        {
            return LlmChatUiResultMapper.Forbidden<LlmChatPage<LlmChatConversationListItem, LlmChatConversationCursor>>(
                LlmChatUiPermission.Read);
        }

        var result = await conversations.ListPageAsync(query, cancellationToken).ConfigureAwait(false);
        return LlmChatUiResultMapper.Map(
            result,
            page => new LlmChatPage<LlmChatConversationListItem, LlmChatConversationCursor>(
                [.. page.Items.Select(ToListItem)],
                page.NextCursor));
    }

    public async Task<LlmChatUiResult<LlmChatConversationView>> GetAsync(
        Guid conversationId,
        LlmChatTranscriptQuery transcriptQuery,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transcriptQuery);
        if (!await authorization.IsAllowedAsync(LlmChatUiPermission.Read, cancellationToken))
        {
            return LlmChatUiResultMapper.Forbidden<LlmChatConversationView>(LlmChatUiPermission.Read);
        }

        if (!TryCreateId(conversationId, out var id))
        {
            return LlmChatUiResultMapper.Invalid<LlmChatConversationView>("Select a valid Simple Chat conversation.");
        }

        var result = await conversations.GetAsync(id, transcriptQuery, cancellationToken).ConfigureAwait(false);
        return LlmChatUiResultMapper.Map(result, ToView);
    }

    public async Task<LlmChatUiResult<LlmChatConversationView>> CreateAsync(
        Guid definitionId,
        string title,
        CancellationToken cancellationToken = default)
    {
        if (!await authorization.IsAllowedAsync(LlmChatUiPermission.Manage, cancellationToken))
        {
            return LlmChatUiResultMapper.Forbidden<LlmChatConversationView>(LlmChatUiPermission.Manage);
        }

        if (definitionId == Guid.Empty)
        {
            return LlmChatUiResultMapper.Invalid<LlmChatConversationView>("Select a valid Simple Chat definition.");
        }

        var result = await conversations.CreateAsync(
            new(new(definitionId), title, LlmChatConversationOrigin.Application),
            cancellationToken).ConfigureAwait(false);
        return LlmChatUiResultMapper.Map(result, ToView);
    }

    public async Task<LlmChatUiResult<LlmChatConversationView>> RenameAsync(
        Guid conversationId,
        string title,
        long expectedConcurrencyToken,
        long expectedTranscriptRevision,
        CancellationToken cancellationToken = default)
    {
        if (!await authorization.IsAllowedAsync(LlmChatUiPermission.Manage, cancellationToken))
        {
            return LlmChatUiResultMapper.Forbidden<LlmChatConversationView>(LlmChatUiPermission.Manage);
        }

        if (!TryCreateId(conversationId, out var id))
        {
            return LlmChatUiResultMapper.Invalid<LlmChatConversationView>("Select a valid Simple Chat conversation.");
        }

        var result = await conversations.RenameAsync(
            new(id, title, expectedConcurrencyToken, expectedTranscriptRevision),
            cancellationToken).ConfigureAwait(false);
        return LlmChatUiResultMapper.Map(result, ToView);
    }

    public async Task<LlmChatUiResult<LlmChatConversationView>> ArchiveAsync(
        Guid conversationId,
        long expectedConcurrencyToken,
        CancellationToken cancellationToken = default)
    {
        if (!await authorization.IsAllowedAsync(LlmChatUiPermission.Manage, cancellationToken))
        {
            return LlmChatUiResultMapper.Forbidden<LlmChatConversationView>(LlmChatUiPermission.Manage);
        }

        if (!TryCreateId(conversationId, out var id))
        {
            return LlmChatUiResultMapper.Invalid<LlmChatConversationView>("Select a valid Simple Chat conversation.");
        }

        var result = await conversations.ArchiveAsync(
            new(id, expectedConcurrencyToken),
            cancellationToken).ConfigureAwait(false);
        return LlmChatUiResultMapper.Map(result, ToView);
    }

    private static bool TryCreateId(Guid value, out LlmChatConversationId id)
    {
        if (value == Guid.Empty)
        {
            id = default;
            return false;
        }

        id = new(value);
        return true;
    }

    private static LlmChatConversationListItem ToListItem(LlmChatConversationDetails details)
        => new(
            details.Conversation.Id.Value,
            details.Conversation.DefinitionId.Value,
            details.Conversation.DefinitionRevision.Value,
            details.Conversation.Title,
            details.DefinitionName,
            details.Conversation.Status,
            details.Conversation.Origin,
            details.Conversation.ConcurrencyToken,
            details.Transcript.TranscriptRevision,
            details.ActiveOperationId?.Value,
            details.Conversation.UpdatedAtUtc);

    private static LlmChatConversationView ToView(LlmChatConversationDetails details)
        => new(
            ToListItem(details),
            [.. details.TranscriptMessages
                .Where(message => message.Role is LlmMessageRole.User or LlmMessageRole.Assistant)
                .Select(message => new LlmChatMessageListItem(
                    message.EntryId,
                    message.TurnId,
                    message.Role,
                    message.Text,
                    message.CreatedAtUtc,
                    message.Model))],
            details.NextMessageCursor);
}
