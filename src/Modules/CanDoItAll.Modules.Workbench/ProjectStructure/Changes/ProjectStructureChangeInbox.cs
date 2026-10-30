using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Workbench.Structure.UI;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectStructureChangeInbox {
    private const int MaximumItems = 50;
    private readonly List<ProjectStructureChange> items = [];
    private long acknowledged;
    private bool replayLost;
    private long replayLossRevision;
    private long refreshReplayLossRevision;
    private CanvasWorkbenchWindowState window = new() { IsVisible = false };

    public Guid ContextId { get; private set; } = Guid.NewGuid();
    public ProjectWriteAdmission? Admission { get; private set; }
    public ProjectStructureChangeCursor? Cursor { get; private set; }
    public bool IsBusy { get; private set; }
    public string? Message { get; private set; }
    public bool NeedsRefresh => replayLost || items.Any(item => item.Sequence > acknowledged);
    public StructureChangesPresentation Presentation => new(ContextId, window.Clone(), items.Select(item => new StructureChangeItem(
        item.Sequence, item.Actor, item.OccurredAtUtc,
        string.Join(", ", item.Nodes.Select(node => $"{node.Title} ({Label(node.Kind)})")),
        item.Nodes.Select(node => node.NodeId).Distinct(StringComparer.Ordinal).ToArray())).ToArray(), NeedsRefresh, IsBusy, Message);

    public void Reset(ProjectWriteAdmission? admission, ProjectStructureChangeCursor? cursor) {
        ContextId = Guid.NewGuid();
        Admission = admission;
        Cursor = cursor;
        acknowledged = cursor?.Sequence ?? 0;
        items.Clear();
        replayLost = false;
        replayLossRevision = 0;
        refreshReplayLossRevision = 0;
        IsBusy = false;
        Message = null;
        window = new() { IsVisible = false };
    }

    public void Receive(ProjectStructureChanges batch, Guid ownSessionId) {
        if (Cursor is not null && Cursor.StreamId != batch.Cursor.StreamId) {
            items.Clear();
            acknowledged = 0;
        }
        var remote = batch.Changes.Where(item => item.SessionId != ownSessionId &&
            (Cursor?.StreamId != batch.Cursor.StreamId || item.Sequence > (Cursor?.Sequence ?? -1))).ToArray();
        Cursor = batch.Cursor;
        if (batch.RequiresRefresh) {
            replayLost = true;
            replayLossRevision++;
            Message = "Some updates are no longer available. Refresh the structure to see the current state.";
        }
        items.InsertRange(0, remote.Reverse());
        if (items.Count > MaximumItems) {
            items.RemoveRange(MaximumItems, items.Count - MaximumItems);
        }
        if (remote.Length > 0 || batch.RequiresRefresh) {
            window.IsVisible = true;
            window.IsMinimized = false;
        }
    }

    public void SetWindow(CanvasWorkbenchWindowState state) => window = CanvasWorkbenchWindowState.Normalize(state);
    public void ToggleWindow() {
        window.IsVisible = !window.IsVisible;
        window.IsMinimized = false;
    }

    public bool BeginRefresh(out ProjectStructureChangeCursor? through) {
        through = Cursor;
        if (IsBusy) {
            return false;
        }
        IsBusy = true;
        refreshReplayLossRevision = replayLossRevision;
        Message = null;
        return true;
    }

    public IReadOnlyList<string> GetNodeIds(long? sequence) => sequence is null ? []
        : items.SingleOrDefault(item => item.Sequence == sequence)?.Nodes.Select(node => node.NodeId)
            .Distinct(StringComparer.Ordinal).ToArray() ?? [];

    public void CompleteRefresh(ProjectStructureChangeCursor? through, string? message = null) {
        if (through?.StreamId == Cursor?.StreamId) {
            acknowledged = Math.Max(acknowledged, through?.Sequence ?? 0);
            items.RemoveAll(item => item.Sequence <= acknowledged);
            if (refreshReplayLossRevision == replayLossRevision) {
                replayLost = false;
            }
        }
        IsBusy = false;
        Message = message;
    }

    public void FailRefresh(string message) {
        IsBusy = false;
        Message = message;
        window.IsVisible = true;
    }

    private static string Label(ProjectStructureChangeKind kind) => kind switch {
        ProjectStructureChangeKind.Created => "created",
        ProjectStructureChangeKind.Updated => "changed",
        ProjectStructureChangeKind.Deleted => "deleted",
        ProjectStructureChangeKind.RelationshipChanged => "relationship changed",
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
}
