using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Ports;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence;

public sealed class ProfileFencedLlmChatInvocationPort(
    ILlmInvocationPort inner,
    IDatabaseRuntimeState runtimeState,
    ILlmChatOperationScopeAccessor operationScope) : ILlmInvocationPort
{
    public async Task<LlmInvocationResult> InvokeAsync(
        LlmInvocationRequest request,
        CancellationToken cancellationToken = default)
    {
        var identity = LlmChatRuntimeFence.RequireCurrent(runtimeState, operationScope);
        try
        {
            var result = await inner.InvokeAsync(request, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            LlmChatRuntimeFence.EnsureCurrent(runtimeState, identity);
            return result;
        }
        catch (OperationCanceledException) when (!LlmChatRuntimeFence.IsCurrent(runtimeState.GetSnapshot(), identity))
        {
            throw new LlmChatRuntimeProfileChangedException();
        }
    }
}
