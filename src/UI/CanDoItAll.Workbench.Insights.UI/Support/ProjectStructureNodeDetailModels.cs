namespace CanDoItAll.Modules.Workbench.Pages;

public sealed record ProjectStructureDetailItem(
    string Label,
    string Value);

public sealed record ProjectStructureDetailSection(
    string Title,
    IReadOnlyList<ProjectStructureDetailItem> Items);
