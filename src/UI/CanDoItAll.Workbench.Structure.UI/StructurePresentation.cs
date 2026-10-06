using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.Workbench;

namespace CanDoItAll.Workbench.Structure.UI;

public sealed record StructurePresentation(Guid ContextId, SkeletonStateOverlaySnapshot Loading) {
    public CanvasWorkbenchSurface? Canvas { get; init; }
    public string? UnavailableTitle { get; init; }
    public string? UnavailableDescription { get; init; }
    public string? Error { get; init; }
    public StructureToolbarPresentation Toolbar { get; init; } = new();
    public StructureToolboxPresentation Toolbox { get; init; } = new();
    public int SelectionCount { get; init; }
    public bool ShowFeedback { get; init; }
    public string? Feedback { get; init; }
    public string FeedbackTone { get; init; } = "neutral";
    public bool HasPendingCleanup { get; init; }
    public bool IsRetryingCleanup { get; init; }
}

public sealed record StructureToolbarPresentation {
    public bool SelectionVisible { get; init; }
    public bool HealthVisible { get; init; }
    public bool ObjectIndexVisible { get; init; }
    public bool FilesVisible { get; init; }
    public bool SignalsVisible { get; init; }
    public bool AgentsVisible { get; init; }
    public string Mode { get; init; } = CanvasWorkbenchModes.Authoring;
    public bool CanConnect { get; init; }
    public bool CanRecompose { get; init; }
    public bool IsRecomposing { get; init; }
}

public sealed record StructureToolboxPresentation {
    public string WindowId { get; init; } = "structure-toolbox";
    public CanvasWorkbenchWindowState State { get; init; } = new();
    public IReadOnlyList<ProjectStructureInspectorCreateGroup> Groups { get; init; } = [];
    public string Search { get; init; } = string.Empty;
    public string SourceLabel { get; init; } = string.Empty;
    public double DefaultWidth { get; init; } = 380;
}

public enum StructureToolbarCommand {
    Select, Dependency, Delete, Inspector, Health, Toolbox, ObjectIndex, Files, Signals, Agents,
    Gantt, Recompose, RetryCleanup, Retry, Projects
}

public abstract record StructureIntent(Guid ContextId);
public sealed record StructureToolbarIntent(Guid ContextId, StructureToolbarCommand Command) : StructureIntent(ContextId);
public sealed record StructureViewIntent(Guid ContextId, int Index) : StructureIntent(ContextId);
public sealed record StructureSelectionIntent(Guid ContextId, CanvasWorkbenchSelectionChangedEventArgs Selection) : StructureIntent(ContextId);
public sealed record StructureNodesMovedIntent(Guid ContextId, CanvasWorkbenchNodesMovedEventArgs Move) : StructureIntent(ContextId);
public sealed record StructureContextIntent(Guid ContextId, CanvasWorkbenchContextActionRequest Request) : StructureIntent(ContextId);
public sealed record StructureComposerOpenedIntent(Guid ContextId, CanvasWorkbenchComposerOpening Opening) : StructureIntent(ContextId);
public sealed record StructureComposerClosedIntent(Guid ContextId, Guid OpeningId) : StructureIntent(ContextId);
public sealed record StructureCreateIntent(Guid ContextId, CanvasWorkbenchCreateActionRequest Request) : StructureIntent(ContextId);
public sealed record StructureNoteEditIntent(Guid ContextId, CanvasWorkbenchNodeEditRequest Request) : StructureIntent(ContextId);
public sealed record StructureOpenIntent(Guid ContextId, string NodeId) : StructureIntent(ContextId);
public sealed record StructureStateIntent(Guid ContextId, string StateJson) : StructureIntent(ContextId);
public sealed record StructureClipboardIntent(Guid ContextId, CanvasWorkbenchClipboardRequest Request) : StructureIntent(ContextId);
public sealed record StructureToolboxWindowIntent(Guid ContextId, CanvasWorkbenchWindowState State) : StructureIntent(ContextId);
public sealed record StructureToolboxSearchIntent(Guid ContextId, string Search) : StructureIntent(ContextId);
public sealed record StructureToolboxGroupIntent(Guid ContextId, string GroupId) : StructureIntent(ContextId);
public sealed record StructureToolboxActionIntent(Guid ContextId, string ActionId) : StructureIntent(ContextId);
