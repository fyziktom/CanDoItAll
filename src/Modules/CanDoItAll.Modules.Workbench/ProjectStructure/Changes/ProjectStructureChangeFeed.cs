using System.ComponentModel;

namespace CanDoItAll.Modules.Workbench;

public enum ProjectStructureChangeKind {
    Created,
    Updated,
    Deleted,
    RelationshipChanged
}

[Description("Position in the bounded, process-local project structure change feed. A new stream ID requires refreshing the structure.")]
public sealed record ProjectStructureChangeCursor(Guid StreamId, long Sequence);

[Description("A committed node change. NodeId is the canvas node key; Title is its label at commit, including for deleted nodes.")]
public sealed record ProjectStructureChangedNode(string NodeId, string Title, ProjectStructureChangeKind Kind);

[Description("One committed change, shared by polling and SSE. SessionId identifies its editor session, not authorization. Nodes can include deleted nodes.")]
public sealed record ProjectStructureChange(
    long Sequence,
    Guid ProjectId,
    Guid SessionId,
    string Actor,
    DateTimeOffset OccurredAtUtc,
    IReadOnlyList<ProjectStructureChangedNode> Nodes);

[Description("A page of committed changes. Save Cursor for the next poll. RequiresRefresh means replay was lost or the server restarted. A request without a cursor starts at the current position without historical notifications.")]
public sealed record ProjectStructureChanges(
    ProjectStructureChangeCursor Cursor,
    IReadOnlyList<ProjectStructureChange> Changes,
    bool RequiresRefresh);

public sealed class ProjectStructureChangeSession {
    public Guid Id { get; } = Guid.NewGuid();
    public string Actor { get; set; } = "Another editor";
}

public sealed class ProjectStructureChangeFeed {
    private const int Capacity = 512;
    private readonly object sync = new();
    private readonly Guid streamId = Guid.NewGuid();
    private readonly Queue<(Guid ProfileId, ProjectStructureChange Change)> entries = new();
    private TaskCompletionSource changed = NewSignal();
    private long sequence;

    public ProjectStructureChanges Read(Guid profileId, IReadOnlyCollection<Guid> projectIds,
        ProjectStructureChangeCursor? after = null, int take = 100) {
        ArgumentOutOfRangeException.ThrowIfLessThan(take, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(take, 100);
        lock (sync) {
            if (after is null) {
                return new(new(streamId, sequence), [], false);
            }
            var reset = after.StreamId != streamId || after.Sequence < 0 || after.Sequence > sequence;
            var gap = !reset && entries.TryPeek(out var oldest) && after.Sequence < oldest.Change.Sequence - 1;
            var matches = entries.Where(item => item.ProfileId == profileId && projectIds.Contains(item.Change.ProjectId) &&
                    (reset || item.Change.Sequence > after.Sequence))
                .Select(item => item.Change).Take(take + 1).ToArray();
            var page = matches.Take(take).ToArray();
            var next = matches.Length > take ? page[^1].Sequence : sequence;
            return new(new(streamId, next), page, reset || gap);
        }
    }

    public Task WaitAsync(ProjectStructureChangeCursor after, CancellationToken cancellationToken) {
        lock (sync) {
            return after.StreamId != streamId || after.Sequence != sequence
                ? Task.CompletedTask
                : changed.Task.WaitAsync(cancellationToken);
        }
    }

    internal void Publish(Guid profileId, Guid projectId, ProjectStructureChangeSession session,
        IReadOnlyList<ProjectStructureChangedNode> nodes) {
        if (nodes.Count == 0) {
            return;
        }
        TaskCompletionSource signal;
        lock (sync) {
            var actor = string.IsNullOrWhiteSpace(session.Actor) ? "Another editor" : session.Actor.Trim();
            entries.Enqueue((profileId, new(++sequence, projectId, session.Id, actor[..Math.Min(actor.Length, 160)],
                DateTimeOffset.UtcNow, nodes.ToArray())));
            while (entries.Count > Capacity) {
                entries.Dequeue();
            }
            signal = changed;
            changed = NewSignal();
        }
        signal.TrySetResult();
    }

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
