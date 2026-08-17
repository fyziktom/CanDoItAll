using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Llm.Conversations;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Application;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Ports;
using CanDoItAll.AgentFramework.Providers;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Runtime;

public sealed class LlmChatConversationEngineFactory(
    ILlmInvocationPort providerInvocationPort,
    ILlmStreamingInvocationPort providerStreamingInvocationPort,
    ILlmChatOperationEvidenceSink evidenceSink,
    IProviderModelCapabilityResolver capabilityResolver,
    ILlmChatOperationScopeAccessor operationScope,
    TimeProvider timeProvider,
    LlmChatStreamingConsumerState streamingConsumerState,
    ILlmChatRuntimePersistenceBoundary persistenceBoundary,
    ILlmConversationContextWindowPolicy contextWindowPolicy,
    ILlmChatConversationReadStore readStore,
    CanonicalLlmChatProviderResolver providerResolver,
    ILlmChatRuntimeLeaseFactory runtimeLeaseFactory)
{
    public ILlmChatConversationEngine Create()
    {
        ILlmInvocationPort invocationPort = new AuditedLlmChatInvocationPort(
            providerInvocationPort,
            evidenceSink,
            capabilityResolver,
            operationScope,
            timeProvider);
        invocationPort = persistenceBoundary.Fence(invocationPort);

        ILlmStreamingInvocationPort streamingInvocationPort = new AuditedLlmChatStreamingInvocationPort(
            providerStreamingInvocationPort,
            persistenceBoundary.StreamingEvidenceSink,
            capabilityResolver,
            operationScope,
            timeProvider,
            streamingConsumerState);
        streamingInvocationPort = persistenceBoundary.Fence(streamingInvocationPort);

        var conversationService = new LlmConversationService(
            invocationPort,
            persistenceBoundary.ConversationStore,
            persistenceBoundary.ConversationTurnStore,
            contextWindowPolicy,
            timeProvider);
        return new LlmChatConversationEngine(
            conversationService,
            readStore,
            providerResolver,
            runtimeLeaseFactory,
            operationScope,
            streamingInvocationPort);
    }
}
