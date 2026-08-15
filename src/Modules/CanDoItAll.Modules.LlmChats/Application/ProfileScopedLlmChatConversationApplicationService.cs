using CanDoItAll.Modules.LlmChats.Common;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.LlmChats.Application;

internal sealed class ProfileScopedLlmChatConversationApplicationService(
    LlmChatConversationApplicationService inner,
    LlmChatProfileScopeRunner scopeRunner) : ILlmChatConversationApplicationService
{
    public Task<Result<LlmChatConversationDetails>> CreateAsync(
        CreateLlmChatConversationCommand command,
        CancellationToken cancellationToken = default)
        => ExecuteAsync(token => inner.CreateAsync(command, token), cancellationToken);

    public Task<Result<LlmChatConversationDetails>> RenameAsync(
        RenameLlmChatConversationCommand command,
        CancellationToken cancellationToken = default)
        => ExecuteAsync(token => inner.RenameAsync(command, token), cancellationToken);

    public Task<Result<LlmChatConversationDetails>> ArchiveAsync(
        ArchiveLlmChatConversationCommand command,
        CancellationToken cancellationToken = default)
        => ExecuteAsync(token => inner.ArchiveAsync(command, token), cancellationToken);

    public Task<Result<LlmChatConversationDetails>> GetAsync(
        LlmChatConversationId conversationId,
        CancellationToken cancellationToken = default)
        => ExecuteAsync(token => inner.GetAsync(conversationId, token), cancellationToken);

    public Task<Result<LlmChatConversationDetails>> GetAsync(
        LlmChatConversationId conversationId,
        LlmChatTranscriptQuery transcriptQuery,
        CancellationToken cancellationToken = default)
        => ExecuteAsync(token => inner.GetAsync(conversationId, transcriptQuery, token), cancellationToken);

    public Task<Result<IReadOnlyList<LlmChatConversationDetails>>> ListAsync(
        LlmChatConversationQuery query,
        CancellationToken cancellationToken = default)
        => ExecuteAsync(token => inner.ListAsync(query, token), cancellationToken);

    public Task<Result<LlmChatPage<LlmChatConversationDetails>>> ListPageAsync(
        LlmChatConversationQuery query,
        CancellationToken cancellationToken = default)
        => ExecuteAsync(token => inner.ListPageAsync(query, token), cancellationToken);

    private Task<Result<T>> ExecuteAsync<T>(
        Func<CancellationToken, Task<Result<T>>> operation,
        CancellationToken cancellationToken)
        => scopeRunner.ExecuteAsync(LlmChatOperationId.New(), operation, cancellationToken);
}
