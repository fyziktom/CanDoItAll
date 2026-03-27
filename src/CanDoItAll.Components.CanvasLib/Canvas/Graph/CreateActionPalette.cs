namespace CanDoItAll.Components.CanvasLib;

public sealed class CreateActionPaletteSnapshot
{
    public string TestHookId { get; init; } = "create-action-palette";

    public string Label { get; init; } = "Create action palette";

    public string Title { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string StatePill { get; init; } = string.Empty;

    public IReadOnlyList<string> Metrics { get; init; } = [];

    public IReadOnlyList<CanvasWorkbenchAction> QuickActions { get; init; } = [];

    public IReadOnlyList<CanvasWorkbenchAction> GroupActions { get; init; } = [];
}

public static class CreateActionPaletteFactory
{
    public static CreateActionPaletteSnapshot CreateForWorkbench(CanvasWorkbenchSurface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);

        return new CreateActionPaletteSnapshot
        {
            Title = "Shared create flows now project through one palette boundary instead of piggybacking on menu internals",
            Summary = "Toolbar insert actions, contextual create commands, and future command-palette entry points can all consume the same shared action metadata.",
            StatePill = surface.Chrome.QuickCreateActions.Count > 0 ? "Armed" : "Idle",
            Metrics =
            [
                $"{surface.Chrome.QuickCreateActions.Count} quick create actions",
                $"{surface.Chrome.GroupContextActions.Count} group actions",
                $"{surface.Chrome.QuickCreateActions.Count(action => action.RequiresInput)} input-driven actions",
                $"{surface.Chrome.QuickCreateActions.Count(action => action.RequiresFile)} file-driven actions"
            ],
            QuickActions = surface.Chrome.QuickCreateActions.Take(4).ToList(),
            GroupActions = surface.Chrome.GroupContextActions.Take(4).ToList()
        };
    }
}


