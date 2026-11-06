namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Application;

public sealed class LlmChatCommittedException<T>(T accepted, Exception innerException)
    : Exception("The Simple Chat change is saved, but a secondary update requires reconciliation.", innerException) {
    public T Accepted { get; } = accepted;
}
