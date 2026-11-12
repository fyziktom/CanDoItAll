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
        List<Exception>? failures = null;
        foreach (var handler in Invalidated?.GetInvocationList() ?? []) {
            try {
                ((EventHandler<LlmChatDefinitionCatalogInvalidatedEventArgs>)handler)(this, new(definition));
            } catch (Exception exception) {
                (failures ??= []).Add(exception);
            }
        }
        if (failures is not null) {
            throw new AggregateException("The saved definition requires catalog reconciliation.", failures);
        }
    }
}

public sealed class LlmChatDefinitionCatalogInvalidatedEventArgs(
    LlmChatDefinitionListItem definition) : EventArgs
{
    public LlmChatDefinitionListItem Definition { get; } =
        definition ?? throw new ArgumentNullException(nameof(definition));
}
