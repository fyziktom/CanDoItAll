using System.Runtime.CompilerServices;
using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.LlmChats.Common;
using CanDoItAll.Modules.LlmChats.Ports;

namespace CanDoItAll.Modules.LlmChats.Persistence;

public sealed class ProfileFencedLlmChatStreamingInvocationPort(
    ILlmStreamingInvocationPort inner,
    IDatabaseRuntimeState runtimeState,
    ILlmChatOperationScopeAccessor operationScope) : ILlmStreamingInvocationPort
{
    public async IAsyncEnumerable<LlmStreamingUpdate> StreamAsync(
        LlmInvocationRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var identity = LlmChatRuntimeFence.RequireCurrent(runtimeState, operationScope);
        await using var enumerator = inner.StreamAsync(request, cancellationToken)
            .GetAsyncEnumerator(cancellationToken);
        while (true)
        {
            bool moved;
            try
            {
                moved = await enumerator.MoveNextAsync().ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!LlmChatRuntimeFence.IsCurrent(runtimeState.GetSnapshot(), identity))
            {
                throw new LlmChatRuntimeProfileChangedException();
            }

            if (!moved)
            {
                break;
            }

            cancellationToken.ThrowIfCancellationRequested();
            LlmChatRuntimeFence.EnsureCurrent(runtimeState, identity);
            yield return enumerator.Current;
        }

        LlmChatRuntimeFence.EnsureCurrent(runtimeState, identity);
    }
}
