using System.Collections.Immutable;
using CanDoItAll.Editing;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.UI;

public enum ConversationDialogKind { None, Start, Rename, Archive }
public enum ConversationDialogAction { Close, SelectDefinition, LoadMoreDefinitions, TitleChanged, Confirm, Compare, ApplyComparison, ComparisonChanged, Undo, Discard, KeepEditing }
public readonly record struct ConversationDialogOrigin(long Generation, ConversationDialogKind Kind, Guid? ConversationId = null);
public sealed record ConversationDialogPresentation(
    ConversationDialogOrigin Origin,
    string Title = "",
    Guid? SelectedDefinitionId = null,
    ImmutableArray<DefinitionCatalogCard> Definitions = default,
    bool HasMoreDefinitions = false,
    bool IsLoading = false,
    bool IsMutating = false,
    MergeField<string>? TitleComparison = null,
    bool NeedsReview = false,
    string Notice = "",
    bool RequiresAcknowledgement = false,
    bool ConfirmDiscard = false,
    bool CanUndo = false,
    bool IsUnconfirmedCreate = false,
    string StateInformation = "");
public sealed record ConversationDialogIntent(
    ConversationDialogOrigin Origin,
    ConversationDialogAction Action,
    string? Text = null,
    Guid? DefinitionId = null,
    bool Acknowledged = false);
