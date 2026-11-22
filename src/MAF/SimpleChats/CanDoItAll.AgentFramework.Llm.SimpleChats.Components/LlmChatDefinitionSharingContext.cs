using CanDoItAll.AgentFramework.Llm.SimpleChats.UI;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Components;

public sealed record LlmChatDefinitionSharingContext(DefinitionCatalogCard? Definition, LlmChatEditorOpening Access, bool Disabled,
    Func<bool, bool, Task> SharingChanged);
