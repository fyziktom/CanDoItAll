using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Llm.Conversations;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Ports;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence;

public sealed class DatabaseLlmChatRuntimePersistenceBoundary : ILlmChatRuntimePersistenceBoundary
{
    private readonly IDatabaseRuntimeState runtimeState;
    private readonly ILlmChatOperationScopeAccessor operationScope;

    public DatabaseLlmChatRuntimePersistenceBoundary(
        AppDbContext dbContext,
        IDatabaseRuntimeState runtimeState,
        ILlmChatOperationScopeAccessor operationScope,
        IServiceScopeFactory serviceScopeFactory)
    {
        this.runtimeState = runtimeState;
        this.operationScope = operationScope;
        var store = new ProfileFencedLlmConversationStore(
            new EfLlmConversationStore(dbContext),
            new EfLlmConversationTurnStore(dbContext),
            runtimeState,
            operationScope);
        ConversationStore = store;
        ConversationTurnStore = store;
        StreamingEvidenceSink = new FreshScopeLlmChatOperationEvidenceSink(serviceScopeFactory);
    }

    public ILlmConversationStore ConversationStore { get; }

    public ILlmConversationTurnStore ConversationTurnStore { get; }

    public ILlmChatOperationEvidenceSink StreamingEvidenceSink { get; }

    public ILlmInvocationPort Fence(ILlmInvocationPort invocationPort)
    {
        return new ProfileFencedLlmChatInvocationPort(invocationPort, runtimeState, operationScope);
    }

    public ILlmStreamingInvocationPort Fence(ILlmStreamingInvocationPort invocationPort)
    {
        return new ProfileFencedLlmChatStreamingInvocationPort(invocationPort, runtimeState, operationScope);
    }
}
