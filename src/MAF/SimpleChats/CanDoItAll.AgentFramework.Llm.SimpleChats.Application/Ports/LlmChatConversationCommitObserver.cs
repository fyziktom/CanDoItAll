namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Application;

public interface ILlmChatConversationCommitObserver {
    void Committed(Guid conversationId, bool lifecycle);
}
