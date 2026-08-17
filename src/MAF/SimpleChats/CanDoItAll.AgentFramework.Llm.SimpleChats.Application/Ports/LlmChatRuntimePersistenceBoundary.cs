using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Llm.Conversations;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Ports;

public interface ILlmChatRuntimePersistenceBoundary
{
    ILlmConversationStore ConversationStore { get; }

    ILlmConversationTurnStore ConversationTurnStore { get; }

    ILlmChatOperationEvidenceSink StreamingEvidenceSink { get; }

    ILlmInvocationPort Fence(ILlmInvocationPort invocationPort);

    ILlmStreamingInvocationPort Fence(ILlmStreamingInvocationPort invocationPort);
}
