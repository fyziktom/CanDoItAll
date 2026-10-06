namespace CanDoItAll.Workbench.Operators.UI.Runtime;

public enum OperatorActionTone { Primary, Information, Success, Warning, Danger }

public sealed record OperatorQuickAction(int Index, string Label, string Description, string Icon,
    OperatorActionTone Tone, string TestId, bool IsDisabled);

public sealed record OperatorQuickActionsView(Guid OpeningId, string Title, string NodeLabel, string Summary,
    string Notes, IReadOnlyList<OperatorQuickAction> Actions);

public sealed record OperatorWebPreviewView(Guid OpeningId, string Title, string SourceLabel, Uri Url, string Notes,
    bool CanEmbed, string EmbedUnavailableReason, bool CanStop, string Error, bool IsBusy = false, string? ProcessLabel = null);
