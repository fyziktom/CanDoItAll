using System.Collections.Immutable;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.UI;

public enum ConversationDialogKind { None, Start, Rename, Archive }
public enum ConversationDialogAction { Close, SelectDefinition, LoadMoreDefinitions, TitleChanged, Confirm }
public readonly record struct ConversationDialogOrigin(long Generation, ConversationDialogKind Kind, Guid? ConversationId = null);
public sealed record ConversationDialogPresentation(
    ConversationDialogOrigin Origin,
    string Title = "",
    Guid? SelectedDefinitionId = null,
    ImmutableArray<DefinitionCatalogCard> Definitions = default,
    bool HasMoreDefinitions = false,
    bool IsLoading = false,
    bool IsMutating = false);
public sealed record ConversationDialogIntent(
    ConversationDialogOrigin Origin,
    ConversationDialogAction Action,
    string? Text = null,
    Guid? DefinitionId = null);
