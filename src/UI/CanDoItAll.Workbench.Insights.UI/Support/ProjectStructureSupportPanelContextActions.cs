namespace CanDoItAll.Modules.Workbench.Pages;

public sealed record ProjectStructureSupportPanelContextAction(
    string ActionId,
    string Label,
    string Icon,
    string Tone);

public sealed record ProjectStructureSupportPanelContextActionRequest(
    string NodeId,
    string ActionId,
    IReadOnlyList<string>? TargetNodeIds = null);
