namespace CanDoItAll.Modules.Workbench.CanvasAdapters;

public sealed record ProjectStructureValidationOverlaySummary(
    bool IsVisible,
    int BlockedCount,
    int ReviewCount,
    int PriorityCount,
    int SelectedIssueCount,
    IReadOnlyList<string> SpotlightNodes);
