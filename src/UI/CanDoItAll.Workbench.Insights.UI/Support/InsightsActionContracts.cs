using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.Workbench.Pages;

namespace CanDoItAll.Workbench.Insights.UI;

public readonly record struct InsightsOrigin(Guid ProjectId, Guid ProfileId, Guid LifetimeId, Guid ViewId, long SelectionRevision);
public enum InsightsWindow { Selection, ObjectIndex, Signals, Health }
public enum InsightsStatus { InProgress, Done, NotApplicable }
public enum InsightsNativeView { Toolbox, AttachmentLocal, AttachmentPreview, Mermaid }

public abstract record InsightsSelectionCommand {
    public sealed record Clear : InsightsSelectionCommand;
    public sealed record Focus(string NodeId) : InsightsSelectionCommand;
    public sealed record Status(InsightsStatus Value) : InsightsSelectionCommand;
    public sealed record Progress(int Percent) : InsightsSelectionCommand;
    public sealed record Marker(ProjectStructureSelectionMarkerRequest Value) : InsightsSelectionCommand;
    public sealed record Priority(int Value) : InsightsSelectionCommand;
    public sealed record BorderName(string Value) : InsightsSelectionCommand;
    public sealed record CreateBorder(string Name) : InsightsSelectionCommand;
    public sealed record ClearBorders : InsightsSelectionCommand;
    public sealed record Inspector(string ActionId) : InsightsSelectionCommand;
    public sealed record Open(InsightsNativeView View) : InsightsSelectionCommand;
}

public sealed record InsightsSelectionIntent(InsightsOrigin Origin, IReadOnlyList<string> NodeIds, InsightsSelectionCommand Command);
public sealed record InsightsSignalIntent(InsightsOrigin Origin, IReadOnlyList<string> NodeIds, string ActionId);
public sealed record InsightsWindowIntent(InsightsOrigin Origin, InsightsWindow Window, CanvasWorkbenchWindowState State);
public sealed record InsightsSearchIntent(InsightsOrigin Origin, string Text);
public sealed record InsightsFocusIntent(InsightsOrigin Origin, string NodeId);
public sealed record InsightsValidateIntent(InsightsOrigin Origin, IReadOnlyList<string> NodeIds);

public sealed record InsightsOutlineNode(string Id, string Title, string Status, string KindLabel, string Icon,
    string Subtitle, string Subtype, IReadOnlyList<ProjectStructureSupportPanelContextAction> Actions);
public sealed record InsightsMenuRequest(InsightsOrigin Origin, Guid OpeningId, string NodeId, double ClientX, double ClientY);
public sealed record InsightsMenu(InsightsOrigin Origin, Guid OpeningId, string NodeId, string Title, IReadOnlyList<string> NodeIds,
    double ClientX, double ClientY, IReadOnlyList<ProjectStructureSupportPanelContextAction> Actions);
public sealed record InsightsMenuIntent(InsightsMenu Menu, string ActionId);
