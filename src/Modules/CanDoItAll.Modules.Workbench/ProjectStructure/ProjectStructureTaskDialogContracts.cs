using CanDoItAll.Components.CanvasLib;

namespace CanDoItAll.Modules.Workbench;

public sealed record ProjectStructureTaskDialogResult(
    CanvasWorkbenchCreateActionRequest CreateRequest,
    ProjectStructureTaskResourceSelection? Assignee);
