using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectStructureChangeTests {
    private readonly Guid profile = Guid.NewGuid();
    private readonly Guid project = Guid.NewGuid();
    private readonly ProjectStructureChangeFeed feed = new();
    private readonly ProjectStructureChangeSession actor = new() { Actor = "Editor B" };

    [Fact]
    public async Task Capture_publishes_only_after_commit_and_coalesces_saved_node_changes() {
        await using var context = Context();
        var cursor = Read().Cursor;
        await using var mutation = new ProjectStructureSerializableMutationScope(
            await SerializableMutationScope.BeginAsync(context, "change-test", default)).Observe(context, feed, actor, profile);
        var node = Node();
        context.Add(node);
        await context.SaveChangesAsync();
        node.Title = "Accepted title";
        await context.SaveChangesAsync();
        Assert.Empty(Read(cursor).Changes);
        await mutation.CommitAsync(default);
        var change = Assert.Single(Read(cursor).Changes);
        Assert.Equal(actor.Id, change.SessionId);
        Assert.Equal("Editor B", change.Actor);
        var changedNode = Assert.Single(change.Nodes);
        Assert.Equal(node.NodeKey, changedNode.NodeId);
        Assert.Equal("Accepted title", changedNode.Title);
    }

    [Fact]
    public async Task Uncommitted_scope_disposal_does_not_publish_saved_changes() {
        await using var context = Context();
        var cursor = Read().Cursor;
        await using (var mutation = new ProjectStructureSerializableMutationScope(
            await SerializableMutationScope.BeginAsync(context, "change-test", default)).Observe(context, feed, actor, profile)) {
            context.Add(Node());
            await context.SaveChangesAsync();
        }
        Assert.Empty(Read(cursor).Changes);
    }

    [Fact]
    public async Task Saving_without_mutation_capture_does_not_publish_projection_changes() {
        await using var context = Context();
        var cursor = Read().Cursor;
        var node = Node();
        node.IsSystemManaged = true;
        context.Add(node);
        await context.SaveChangesAsync();
        Assert.Empty(Read(cursor).Changes);
    }

    [Fact]
    public void Replay_is_isolated_by_database_profile_and_project_and_pages_without_skipping() {
        var cursor = Read().Cursor;
        Publish("one");
        feed.Publish(Guid.NewGuid(), project, actor, [new("secret", "Other database", ProjectStructureChangeKind.Updated)]);
        feed.Publish(profile, Guid.NewGuid(), actor, [new("unrelated", "Other project", ProjectStructureChangeKind.Updated)]);
        Publish("two");
        var page = feed.Read(profile, [project], cursor, 1);
        Assert.Equal("one", Assert.Single(Assert.Single(page.Changes).Nodes).NodeId);
        var next = Read(page.Cursor);
        Assert.Equal("two", Assert.Single(Assert.Single(next.Changes).Nodes).NodeId);
        Assert.Empty(Read(next.Cursor).Changes);
    }

    [Fact]
    public void New_viewer_starts_at_current_position_without_historical_popups() {
        Publish("old");
        var bootstrap = Read();
        Assert.Empty(bootstrap.Changes);
        Publish("new");
        Assert.Equal("new", Assert.Single(Assert.Single(Read(bootstrap.Cursor).Changes).Nodes).NodeId);
    }

    [Fact]
    public void Retention_gap_and_process_reset_require_explicit_refresh() {
        var cursor = Read().Cursor;
        for (var index = 0; index < 513; index++) {
            Publish($"node-{index}");
        }
        Assert.True(Read(cursor).RequiresRefresh);
        Assert.True(Read(new(Guid.NewGuid(), 0)).RequiresRefresh);
        Assert.True(Read(cursor with { Sequence = long.MaxValue }).RequiresRefresh);
    }

    [Fact]
    public async Task Wait_cannot_lose_a_write_between_read_and_subscription_and_cancels_when_idle() {
        var cursor = Read().Cursor;
        Publish("racing");
        await feed.WaitAsync(cursor, default).WaitAsync(TimeSpan.FromSeconds(1));
        using var cancelled = new CancellationTokenSource();
        var wait = feed.WaitAsync(Read().Cursor, cancelled.Token);
        Assert.False(wait.IsCompleted);
        await cancelled.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => wait);
    }

    [Fact]
    public void Own_edits_advance_cursor_without_opening_the_inbox() {
        var inbox = Inbox();
        Publish("own");
        inbox.Receive(Read(inbox.Cursor), actor.Id);
        Assert.False(inbox.Presentation.Window.IsVisible);
        Assert.False(inbox.NeedsRefresh);
        Assert.Empty(inbox.Presentation.Items);
        Assert.Equal(Read().Cursor, inbox.Cursor);
    }

    [Fact]
    public void Remote_edits_reopen_dismissed_window_without_duplicating_replayed_items() {
        var inbox = Inbox();
        Publish("remote");
        var batch = Read(inbox.Cursor);
        inbox.Receive(batch, Guid.NewGuid());
        inbox.Receive(batch, Guid.NewGuid());
        Assert.Single(inbox.Presentation.Items);
        Assert.True(inbox.Presentation.Window.IsVisible);
        inbox.SetWindow(new CanvasWorkbenchWindowState { IsVisible = false });
        Publish("next");
        inbox.Receive(Read(inbox.Cursor), Guid.NewGuid());
        Assert.True(inbox.Presentation.Window.IsVisible);
        Assert.Equal(2, inbox.Presentation.Items.Count);
    }

    [Fact]
    public void Successful_refresh_removes_all_acknowledged_rows() {
        var inbox = Inbox();
        Publish("first");
        Publish("second");
        inbox.Receive(Read(inbox.Cursor), Guid.NewGuid());
        Assert.Equal(2, inbox.Presentation.Items.Count);
        Assert.True(inbox.BeginRefresh(out var through));
        inbox.CompleteRefresh(through);
        Assert.Empty(inbox.Presentation.Items);
        Assert.False(inbox.NeedsRefresh);
    }

    [Fact]
    public void Changes_arriving_during_refresh_remain_pending_and_refresh_is_not_reentrant() {
        var inbox = Inbox();
        Publish("first");
        inbox.Receive(Read(inbox.Cursor), Guid.NewGuid());
        Assert.True(inbox.BeginRefresh(out var through));
        Assert.False(inbox.BeginRefresh(out _));
        Publish("during-refresh");
        inbox.Receive(Read(inbox.Cursor), Guid.NewGuid());
        inbox.CompleteRefresh(through);
        Assert.True(inbox.NeedsRefresh);
        Assert.Equal(new[] { "during-refresh" }, Assert.Single(inbox.Presentation.Items).NodeIds);
        Assert.True(inbox.BeginRefresh(out through));
        inbox.CompleteRefresh(through);
        Assert.False(inbox.NeedsRefresh);
        Assert.Empty(inbox.Presentation.Items);
    }

    [Fact]
    public void Project_lifetime_reset_retires_old_items_and_failed_refresh_keeps_pending_state() {
        var inbox = Inbox();
        var original = inbox.ContextId;
        Publish("pending");
        inbox.Receive(Read(inbox.Cursor), Guid.NewGuid());
        Assert.True(inbox.BeginRefresh(out _));
        inbox.FailRefresh("Read failed");
        Assert.True(inbox.NeedsRefresh);
        Assert.Single(inbox.Presentation.Items);
        Assert.False(inbox.IsBusy);
        inbox.Reset(new(profile, project, Guid.NewGuid()), Read().Cursor);
        Assert.NotEqual(original, inbox.ContextId);
        Assert.Empty(inbox.Presentation.Items);
        Assert.False(inbox.Presentation.Window.IsVisible);
    }

    [Fact]
    public void Replay_loss_during_refresh_remains_pending_until_another_successful_read() {
        var inbox = Inbox();
        Assert.True(inbox.BeginRefresh(out var through));
        inbox.Receive(new(Read().Cursor, [], true), actor.Id);
        inbox.CompleteRefresh(through);
        Assert.True(inbox.NeedsRefresh);
        Assert.True(inbox.BeginRefresh(out through));
        inbox.CompleteRefresh(through);
        Assert.False(inbox.NeedsRefresh);
    }

    [Fact]
    public void Process_reset_rebases_acknowledgement_and_discards_old_history() {
        var inbox = Inbox();
        Publish("old");
        inbox.Receive(Read(inbox.Cursor), Guid.NewGuid());
        var newStream = Guid.NewGuid();
        inbox.Receive(new(new(newStream, 0), [], true), Guid.NewGuid());
        Assert.Empty(inbox.Presentation.Items);
        Assert.True(inbox.BeginRefresh(out var through));
        inbox.CompleteRefresh(through);
        inbox.Receive(new(new(newStream, 1), [new(1, project, Guid.NewGuid(), "Editor C", DateTimeOffset.UtcNow,
            [new("new", "New process change", ProjectStructureChangeKind.Updated)])], false), actor.Id);
        Assert.True(inbox.NeedsRefresh);
    }

    [Fact]
    public void Refresh_from_a_previous_stream_preserves_new_stream_rows() {
        var inbox = Inbox();
        Publish("old");
        inbox.Receive(Read(inbox.Cursor), Guid.NewGuid());
        Assert.True(inbox.BeginRefresh(out var through));
        inbox.Receive(new(new(Guid.NewGuid(), 1), [new(1, project, Guid.NewGuid(), "Editor C", DateTimeOffset.UtcNow,
            [new("new", "New stream change", ProjectStructureChangeKind.Updated)])], true), actor.Id);
        inbox.CompleteRefresh(through);
        Assert.Equal(new[] { "new" }, Assert.Single(inbox.Presentation.Items).NodeIds);
        Assert.True(inbox.NeedsRefresh);
    }

    private ProjectStructureChangeInbox Inbox() {
        var inbox = new ProjectStructureChangeInbox();
        inbox.Reset(new(profile, project, Guid.NewGuid()), Read().Cursor);
        return inbox;
    }

    private ProjectStructureChanges Read(ProjectStructureChangeCursor? cursor = null) => feed.Read(profile, [project], cursor);
    private void Publish(string nodeId) => feed.Publish(profile, project, actor, [new(nodeId, nodeId, ProjectStructureChangeKind.Updated)]);
    private ProjectObjectRecord Node() => new() { ProjectId = project, NodeKey = "note:change", Title = "Original", ObjectType = ProjectObjectType.ProjectBlock };
    private static WorkbenchDbContext Context() => new(new DbContextOptionsBuilder<WorkbenchDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}
