namespace CanDoItAll.Components.BaseLib;

public sealed record TreeViewNode
{
    public required string Id { get; init; }

    public required string Text { get; init; }

    public string Icon { get; init; } = string.Empty;

    public string Tooltip { get; init; } = string.Empty;

    public IReadOnlyList<TreeViewNode> Children { get; init; } = [];

    public bool IsExpanded { get; init; }

    public bool IsSelected { get; init; }

    public bool IsDisabled { get; init; }

    public bool IsSelectable { get; init; } = true;

    public string BadgeText { get; init; } = string.Empty;

    public string DataTestId { get; init; } = string.Empty;

    public string ChildrenDataTestId { get; init; } = string.Empty;

    public bool HasChildren => Children.Count > 0;
}

public sealed record TreeViewNodeContextMenuRequest(
    string NodeId,
    double ClientX,
    double ClientY);

public enum TreeViewStyle
{
    Default,
    Workbench
}
