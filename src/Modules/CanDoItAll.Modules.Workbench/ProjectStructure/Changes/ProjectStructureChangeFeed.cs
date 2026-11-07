using System.ComponentModel;
using CanDoItAll.AgentFramework.Core;

namespace CanDoItAll.Modules.Workbench;

public enum ProjectStructureChangeKind {
    Created,
    Updated,
    Deleted,
    RelationshipChanged
}

[Description("Position in the bounded, process-local project structure change feed. A new stream ID requires refreshing the structure.")]
public sealed record ProjectStructureChangeCursor(
    [property: Description("GUID identifying this process-local feed incarnation; a changed value invalidates earlier cursor positions.")] Guid StreamId,
    [property: Description("Non-negative feed sequence returned by the server. Retain the complete cursor for the next poll or SSE resume.")] long Sequence);

[Description("A committed node change. NodeId is the canvas node key; Title is its label at commit, including for deleted nodes.")]
public sealed record ProjectStructureChangedNode(
    [property: Description("Canonical canvas node key within the addressed project; a deleted key can no longer be selected after refresh.")] string NodeId,
    [property: Description("Node display label captured at commit, including for a deleted node; this hint is not current node content.")] string Title,
    [property: Description("Committed change kind as a JSON integer: 0 Created, 1 Updated, 2 Deleted, 3 RelationshipChanged.")] ProjectStructureChangeKind Kind);

[Description("One committed structure or projection change, shared by polling and SSE. Nodes can include deleted nodes.")]
public sealed record ProjectStructureChange(
    [property: Description("Server-issued sequence of this committed change within the current process feed.")] long Sequence,
    [property: Description("GUID of the project whose committed structure contribution produced this hint.")] Guid ProjectId,
    [property: Description("Originating editor session or execution GUID for identifying the source of a change.")] Guid SessionId,
    [property: Description("Originating actor display label, intended only for the update notice and not as proof of authority.")] string Actor,
    [property: Description("Server commit-notification instant with UTC offset; this is not an audit-log ordering guarantee.")] DateTimeOffset OccurredAtUtc,
    [property: Description("Changed node keys, labels and kinds captured for this commit; refresh canonical Structure before acting on current content.")] IReadOnlyList<ProjectStructureChangedNode> Nodes) {
    internal ProjectStructureChatChangeOrigin? ChatOrigin { get; init; }
}

internal readonly record struct ProjectStructureChatChangeOrigin(Guid ProjectId, Guid ChatSessionId);

[Description("A page of committed changes. Save Cursor for the next poll. RequiresRefresh means replay was lost or the server restarted. A request without a cursor starts at the current position without historical notifications.")]
public sealed record ProjectStructureChanges(
    [property: Description("Server-issued next feed position; retain both streamId and sequence for polling or SSE resume.")] ProjectStructureChangeCursor Cursor,
    [property: Description("Bounded committed hints after the supplied cursor. An omitted cursor establishes a current position and returns no history.")] IReadOnlyList<ProjectStructureChange> Changes,
    [property: Description("True when replay continuity was lost or the stream changed; explicitly refresh canonical Structure before relying on displayed node state.")] bool RequiresRefresh);

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
        if (WorkspaceExecutionAuditContext.Current is not { } execution) {
            Publish(profileId, projectId, session.Id, session.Actor, nodes);
            return;
        }

        var chat = execution.ChatSessionId is { } chatSessionId &&
            execution.SourceKind == ProjectStructureAccessState.ProjectStructureSourceKind &&
            Guid.TryParse(execution.SourceId, out var sourceProjectId)
                ? new ProjectStructureChatChangeOrigin(sourceProjectId, chatSessionId)
                : (ProjectStructureChatChangeOrigin?)null;
        var actor = string.IsNullOrEmpty(execution.ProcessRunId) ? "Agent" : "Process agent";
        Publish(profileId, projectId, execution.ExecutionRunId, actor, nodes, chat);
    }

    internal void Publish(Guid profileId, Guid projectId, Guid sourceId, string actor,
        IReadOnlyList<ProjectStructureChangedNode> nodes, ProjectStructureChatChangeOrigin? chat = null) {
        if (nodes.Count == 0) {
            return;
        }
        TaskCompletionSource signal;
        lock (sync) {
            var label = string.IsNullOrWhiteSpace(actor) ? "Another editor" : actor.Trim();
            entries.Enqueue((profileId, new(++sequence, projectId, sourceId, label[..Math.Min(label.Length, 160)],
                DateTimeOffset.UtcNow, nodes.ToArray()) { ChatOrigin = chat }));
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
