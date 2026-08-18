namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Components;

public interface ILlmChatDefinitionCatalogInvalidator
{
    event EventHandler<LlmChatDefinitionCatalogInvalidatedEventArgs>? Invalidated;

    void Invalidate(LlmChatDefinitionListItem definition);
}

public sealed class LlmChatDefinitionCatalogInvalidationHub : ILlmChatDefinitionCatalogInvalidator
{
    public event EventHandler<LlmChatDefinitionCatalogInvalidatedEventArgs>? Invalidated;

    public void Invalidate(LlmChatDefinitionListItem definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        Invalidated?.Invoke(this, new LlmChatDefinitionCatalogInvalidatedEventArgs(definition));
    }
}

public sealed class LlmChatDefinitionCatalogInvalidatedEventArgs(
    LlmChatDefinitionListItem definition) : EventArgs
{
    public LlmChatDefinitionListItem Definition { get; } =
        definition ?? throw new ArgumentNullException(nameof(definition));
}
