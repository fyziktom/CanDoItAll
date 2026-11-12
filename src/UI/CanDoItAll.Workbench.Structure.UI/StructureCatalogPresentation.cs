using CanDoItAll.Components.CanvasLib;

namespace CanDoItAll.Modules.Workbench;

public sealed record ProjectStructureMutationTypeOption(
    string ActionId,
    string ObjectSubtype,
    string Label,
    string Description,
    string Icon,
    string Tone);

public sealed record ProjectStructureInspectorCreateGroup(
    string Key,
    string Label,
    string Description,
    bool IsOpen,
    IReadOnlyList<CanvasWorkbenchAction> Actions);
