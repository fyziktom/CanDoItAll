namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Application;

public interface ILlmChatDefinitionCommitObserver {
    void Committed(Guid id, bool lifecycle);
}
