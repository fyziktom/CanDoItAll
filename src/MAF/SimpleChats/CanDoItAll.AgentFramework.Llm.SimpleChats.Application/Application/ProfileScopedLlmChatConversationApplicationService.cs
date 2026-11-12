using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Application;

internal sealed class ProfileScopedLlmChatConversationApplicationService(
    LlmChatProfileScopeRunner scopeRunner) : ILlmChatConversationApplicationService
{
    public Task<Result<LlmChatConversationDetails>> CreateAsync(
        CreateLlmChatConversationCommand command,
        CancellationToken cancellationToken = default)
        => ExecuteAsync((inner, token) => inner.CreateAsync(command, token), cancellationToken);

    public Task<Result<LlmChatConversationDetails>> RenameAsync(
        RenameLlmChatConversationCommand command,
        CancellationToken cancellationToken = default)
        => ExecuteAsync((inner, token) => inner.RenameAsync(command, token), cancellationToken);

    public Task<Result<LlmChatConversationDetails>> ArchiveAsync(
        ArchiveLlmChatConversationCommand command,
        CancellationToken cancellationToken = default)
        => ExecuteAsync((inner, token) => inner.ArchiveAsync(command, token), cancellationToken);

    public Task<Result<LlmChatConversationDetails>> GetAsync(
        LlmChatConversationId conversationId,
        CancellationToken cancellationToken = default)
        => ExecuteAsync((inner, token) => inner.GetAsync(conversationId, token), cancellationToken);

    public Task<Result<LlmChatConversationDetails>> GetAsync(
        LlmChatConversationId conversationId,
        LlmChatTranscriptQuery transcriptQuery,
        CancellationToken cancellationToken = default)
        => ExecuteAsync((inner, token) => inner.GetAsync(conversationId, transcriptQuery, token), cancellationToken);

    public Task<Result<IReadOnlyList<LlmChatConversationDetails>>> ListAsync(
        LlmChatConversationQuery query,
        CancellationToken cancellationToken = default)
        => ExecuteAsync((inner, token) => inner.ListAsync(query, token), cancellationToken);

    public Task<Result<LlmChatPage<LlmChatConversationDetails, LlmChatConversationCursor>>> ListPageAsync(
        LlmChatConversationQuery query,
        CancellationToken cancellationToken = default)
        => ExecuteAsync((inner, token) => inner.ListPageAsync(query, token), cancellationToken);

    private Task<Result<T>> ExecuteAsync<T>(
        Func<LlmChatConversationApplicationService, CancellationToken, Task<Result<T>>> operation,
        CancellationToken cancellationToken)
        => scopeRunner.ExecuteOwnedAsync<LlmChatConversationApplicationService, T>(LlmChatOperationId.New(), operation, cancellationToken);
}
