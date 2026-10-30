using CanDoItAll.Components.CanvasLib;

namespace CanDoItAll.Workbench.Structure.UI;

public sealed record StructureChangeItem(long Sequence, string Actor, DateTimeOffset OccurredAtUtc,
    string Summary, IReadOnlyList<string> NodeIds);

public sealed record StructureChangesPresentation(Guid ContextId, CanvasWorkbenchWindowState Window,
    IReadOnlyList<StructureChangeItem> Items, bool NeedsRefresh, bool IsBusy, string? Message);

public abstract record StructureChangesIntent(Guid ContextId);
public sealed record StructureChangesRefreshIntent(Guid ContextId, long? Sequence = null) : StructureChangesIntent(ContextId);
public sealed record StructureChangesWindowIntent(Guid ContextId, CanvasWorkbenchWindowState State) : StructureChangesIntent(ContextId);
