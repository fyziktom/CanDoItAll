using CanDoItAll.Modules.LlmChats.Common;
using CanDoItAll.Modules.LlmChats.Conversations;
using CanDoItAll.Modules.LlmChats.Operations;
using CanDoItAll.Modules.LlmChats.Ports;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.LlmChats.Application;

public sealed record CreateLlmChatConversationCommand(
    LlmChatDefinitionId DefinitionId,
    string Title,
    LlmChatConversationOrigin Origin);

public sealed record RenameLlmChatConversationCommand(
    LlmChatConversationId ConversationId,
    string Title,
    long ExpectedConcurrencyToken,
    long ExpectedTranscriptRevision);

public sealed record ArchiveLlmChatConversationCommand(
    LlmChatConversationId ConversationId,
    long ExpectedConcurrencyToken);

public sealed record LlmChatConversationQuery
{
    public const int MaximumTake = 100;

    public LlmChatConversationQuery(
        int take = 50,
        LlmChatDefinitionId? definitionId = null,
        LlmChatConversationCursor? cursor = null)
    {
        if (take is < 1 or > MaximumTake)
        {
            throw new ArgumentOutOfRangeException(nameof(take), take, $"Take must be between 1 and {MaximumTake}.");
        }

        Take = take;
        DefinitionId = definitionId;
        Cursor = cursor;
    }

    public int Take { get; }

    public LlmChatDefinitionId? DefinitionId { get; }

    public LlmChatConversationCursor? Cursor { get; }
}

public sealed record LlmChatTranscriptQuery
{
    public const int MaximumTake = 100;

    public LlmChatTranscriptQuery(int take = 50, LlmChatTranscriptCursor? cursor = null)
    {
        if (take is < 1 or > MaximumTake)
        {
            throw new ArgumentOutOfRangeException(nameof(take), take, $"Take must be between 1 and {MaximumTake}.");
        }

        Take = take;
        Cursor = cursor;
    }

    public int Take { get; }

    public LlmChatTranscriptCursor? Cursor { get; }
}

public sealed record LlmChatConversationDetails(
    LlmChatConversation Conversation,
    string DefinitionName,
    LlmChatConversationEngineState Transcript,
    IReadOnlyList<LlmChatTranscriptEntry>? Messages = null,
    LlmChatTranscriptCursor? NextMessageCursor = null)
{
    public LlmChatOperationId? ActiveOperationId => Transcript.ActiveOperationId;

    public IReadOnlyList<LlmChatTranscriptEntry> TranscriptMessages => Messages ?? [];
}

public interface ILlmChatConversationApplicationService
{
    Task<Result<LlmChatConversationDetails>> CreateAsync(
        CreateLlmChatConversationCommand command,
        CancellationToken cancellationToken = default);

    Task<Result<LlmChatConversationDetails>> RenameAsync(
        RenameLlmChatConversationCommand command,
        CancellationToken cancellationToken = default);

    Task<Result<LlmChatConversationDetails>> ArchiveAsync(
        ArchiveLlmChatConversationCommand command,
        CancellationToken cancellationToken = default);

    Task<Result<LlmChatConversationDetails>> GetAsync(
        LlmChatConversationId conversationId,
        CancellationToken cancellationToken = default);

    Task<Result<LlmChatConversationDetails>> GetAsync(
        LlmChatConversationId conversationId,
        LlmChatTranscriptQuery transcriptQuery,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<LlmChatConversationDetails>>> ListAsync(
        LlmChatConversationQuery query,
        CancellationToken cancellationToken = default);

    Task<Result<LlmChatPage<LlmChatConversationDetails, LlmChatConversationCursor>>> ListPageAsync(
        LlmChatConversationQuery query,
        CancellationToken cancellationToken = default);
}
