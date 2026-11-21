namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Components;

public sealed record LlmChatDefinitionImportActions(Func<Guid, Task> Open, Func<Task> Refresh);
