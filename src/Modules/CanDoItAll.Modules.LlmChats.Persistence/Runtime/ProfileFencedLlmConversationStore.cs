using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.LlmChats.Common;
using CanDoItAll.Modules.LlmChats.Ports;

namespace CanDoItAll.Modules.LlmChats.Persistence;

public sealed class ProfileFencedLlmConversationStore(
    ILlmConversationStore inner,
    IDatabaseRuntimeState runtimeState,
    ILlmChatOperationScopeAccessor operationScope,
    ILlmChatOperationEvidenceSink? evidenceSink = null) : ILlmConversationStore
{
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
        => MutateAsync(async token =>
        {
            var stored = await inner.ReplaceAsync(document, expectedTranscriptRevision, token).ConfigureAwait(false);
            var operationId = operationScope.Current?.OperationId;
            if (evidenceSink is not null &&
                operationId is { } currentOperationId &&
                stored.ActiveTurn?.TurnId == currentOperationId.Value)
            {
                await evidenceSink.MarkTurnAdmittedAsync(
                    currentOperationId,
                    stored.ActiveTurn.AdmittedAtUtc,
                    token).ConfigureAwait(false);
            }

            return stored;
        }, cancellationToken);

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
