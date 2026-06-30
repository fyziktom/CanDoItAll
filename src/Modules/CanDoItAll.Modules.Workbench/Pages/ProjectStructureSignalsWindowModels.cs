namespace CanDoItAll.Modules.Workbench.Pages;

public sealed record ProjectStructureSignalSection(
    string Key,
    string Label,
    string Description,
    IReadOnlyList<ProjectStructureSignalActionTile> Actions);

public sealed record ProjectStructureSignalActionTile(
    string ActionId,
    string Label,
    string Glyph,
    string Tone,
    bool IsActive,
    string Description);
