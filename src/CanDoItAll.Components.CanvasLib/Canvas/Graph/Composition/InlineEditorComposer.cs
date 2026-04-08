namespace CanDoItAll.Components.CanvasLib;

public sealed class InlineEditorComposerSnapshot
{
    public string TestHookId { get; init; } = "inline-editor-composer";

    public string Label { get; init; } = "Inline editor composer";

    public string Title { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string StatePill { get; init; } = string.Empty;

    public IReadOnlyList<string> Metrics { get; init; } = [];

    public string DraftLabel { get; init; } = string.Empty;

    public string Placeholder { get; init; } = string.Empty;

    public string SubmitLabel { get; init; } = string.Empty;

    public bool IsEnabled { get; init; }
}

public static class InlineEditorComposerFactory
{
    public static InlineEditorComposerSnapshot CreateForWorkbench(
        CanvasWorkbenchSurface surface,
        SelectionModel selection)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(selection);

        var inlineNode = surface.Nodes.FirstOrDefault(node => node.IsInlineTextNode)
            ?? surface.Nodes.FirstOrDefault(node => string.Equals(node.Id, selection.PrimaryNodeId, StringComparison.Ordinal))
            ?? surface.Nodes.FirstOrDefault();
        var isEnabled = inlineNode is not null;

        return new InlineEditorComposerSnapshot
        {
            Title = "Inline note and quick-edit flows now have a reusable editor host instead of only existing inside runtime branches",
            Summary = "Small text edits can share one inline editor contract with explicit placeholder, submit, and selection-aware ownership rules.",
            StatePill = isEnabled ? "Enabled" : "Idle",
            Metrics =
            [
                $"{surface.Nodes.Count(node => node.IsInlineTextNode)} inline text nodes",
                $"{selection.SelectedNodeIds.Count} selected nodes",
                $"{(surface.Chrome.ChildNoteActionId is null ? 0 : 1)} child note actions",
                $"{(surface.Chrome.SiblingNoteActionId is null ? 0 : 1)} sibling note actions"
            ],
            DraftLabel = inlineNode?.InlineText ?? inlineNode?.LeadText ?? "Draft note preview",
            Placeholder = inlineNode?.InlineTextPlaceholder ?? surface.Chrome.InlineNotePlaceholder ?? "Capture a quick note",
            SubmitLabel = "Save draft",
            IsEnabled = isEnabled
        };
    }
}


