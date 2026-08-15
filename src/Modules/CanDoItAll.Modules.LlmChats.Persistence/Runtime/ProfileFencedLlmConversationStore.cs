using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Llm.Conversations;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.LlmChats.Common;
using CanDoItAll.Modules.LlmChats.Ports;

namespace CanDoItAll.Modules.LlmChats.Persistence;

public sealed class ProfileFencedLlmConversationStore(
    ILlmConversationStore inner,
    ILlmConversationTurnStore turnInner,
    IDatabaseRuntimeState runtimeState,
    ILlmChatOperationScopeAccessor operationScope) : ILlmConversationStore, ILlmConversationTurnStore
{
    public ProfileFencedLlmConversationStore(
        ILlmConversationStore inner,
        IDatabaseRuntimeState runtimeState,
        ILlmChatOperationScopeAccessor operationScope)
        : this(inner, new DocumentLlmConversationTurnStore(inner), runtimeState, operationScope)
    {
    }

    public Task<LlmConversationDocument> CreateAsync(
        LlmConversationDocument document,
        CancellationToken cancellationToken = default)
        => MutateAsync(token => inner.CreateAsync(document, token), cancellationToken);

    public Task<LlmConversationDocument?> TryGetAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default)
        => inner.TryGetAsync(conversationId, cancellationToken);

    public Task<LlmConversationDocument> ReplaceAsync(
        LlmConversationDocument document,
        long expectedTranscriptRevision,
        CancellationToken cancellationToken = default)
        => MutateAsync(
            token => inner.ReplaceAsync(document, expectedTranscriptRevision, token),
            cancellationToken);

    public Task<IReadOnlyList<LlmConversationSummary>> ListAsync(
        CancellationToken cancellationToken = default)
        => inner.ListAsync(cancellationToken);

    public Task DeleteAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default)
        => MutateAsync(
            async token =>
            {
                await inner.DeleteAsync(conversationId, token).ConfigureAwait(false);
                return true;
            },
            cancellationToken);

    public Task<LlmConversationTurnSnapshot?> TryGetAsync(
        Guid conversationId,
        int maximumContextMessages,
        CancellationToken cancellationToken = default)
        => turnInner.TryGetAsync(conversationId, maximumContextMessages, cancellationToken);

    public Task<LlmConversationTurnSnapshot> AdmitAsync(
        LlmConversationTurnAdmissionWrite write,
        CancellationToken cancellationToken = default)
        => MutateAsync(token => turnInner.AdmitAsync(write, token), cancellationToken);

    public Task<LlmConversationTurnSnapshot> CompleteAsync(
        LlmConversationTurnCompletionWrite write,
        CancellationToken cancellationToken = default)
        => MutateAsync(token => turnInner.CompleteAsync(write, token), cancellationToken);

    public Task<LlmConversationTurnSnapshot> CompensateAsync(
        Guid conversationId,
        Guid turnId,
        DateTimeOffset updatedAtUtc,
        int maximumContextMessages,
        CancellationToken cancellationToken = default)
        => MutateAsync(
            token => turnInner.CompensateAsync(
                conversationId,
                turnId,
                updatedAtUtc,
                maximumContextMessages,
                token),
            cancellationToken);

    private async Task<T> MutateAsync<T>(
        Func<CancellationToken, Task<T>> mutate,
        CancellationToken cancellationToken)
    {
        var identity = LlmChatRuntimeFence.RequireCurrent(runtimeState, operationScope);
        try
        {
            var result = await mutate(cancellationToken).ConfigureAwait(false);
            LlmChatRuntimeFence.EnsureCurrent(runtimeState, identity);
            return result;
        }
        catch (OperationCanceledException) when (!LlmChatRuntimeFence.IsCurrent(runtimeState.GetSnapshot(), identity))
        {
            throw new LlmChatRuntimeProfileChangedException();
        }
    }
}
