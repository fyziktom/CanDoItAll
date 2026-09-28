using CanDoItAll.Collaboration.UI;
using CanDoItAll.Collaboration.UiSandbox;

namespace CanDoItAll.Tests.Components.Collaboration;

public sealed class CollaborationScenarioTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Delayed_reconciliation_retains_the_successor_and_clear_releases_it(bool remainingUnread) {
        using var view = new CollaborationScenarioWorkspace(CollaborationScenario.DelayedReconciliation, () => { });
        var a = view.Target!.ThreadId;
        var b = view.Workspace!.Threads[1].ThreadId;
        if (!remainingUnread) {
            await view.SelectAsync(b);
            var mark = view.MarkReadAsync(view.Target!);
            view.CompletePending();
            await mark;
            await view.SelectAsync(a);
        }
        await view.SetUnreadOnlyAsync(true);
        view.Reply.Model.MessageBody = "Accepted A";
        var save = view.ReplyAsync(view.Target!, view.Reply);
        Assert.Equal(CollaborationReadState.Refreshing, view.ReadState);
        var target = view.Target;
        var draft = view.Reply;
        var context = draft.Context;
        draft.Model.MessageBody = "Unsent successor A";
        context.NotifyFieldChanged(new(draft.Model, nameof(draft.Model.MessageBody)));
        view.CompletePending();
        await save;
        Assert.Same(target, view.Target);
        Assert.Same(draft, view.Reply);
        Assert.Same(context, view.Reply.Context);
        Assert.True(view.IsReplyRetained);
        Assert.Equal("Unsent successor A", draft.Model.MessageBody);
        Assert.Single(view.Workspace!.SelectedThread!.Messages, item => item.Body == "Accepted A");
        view.ClearReply(target!, draft);
        var refresh = view.RefreshAsync();
        view.CompletePending();
        await refresh;
        Assert.Equal(remainingUnread ? b : (Guid?)null, view.Target?.ThreadId);
        Assert.False(view.IsReplyRetained);
    }

    [Fact]
    public async Task A_section_change_during_a_delayed_read_keeps_new_intent_without_stranding_loading() {
        using var view = new CollaborationScenarioWorkspace(CollaborationScenario.DelayedTarget, () => { });
        var b = view.Workspace!.Threads[1].ThreadId;
        var read = view.SelectAsync(b);
        await view.SetSectionAsync(CollaborationSection.Threads);
        view.CompletePending();
        await read;
        Assert.Equal(CollaborationReadState.Ready, view.ReadState);
        Assert.Equal(CollaborationSection.Threads, view.Section);
        Assert.Equal(b, view.Workspace!.SelectedThread!.ThreadId);
    }

    [Fact]
    public async Task Retired_delayed_reads_unwind_on_disposal_without_publishing() {
        var changes = 0;
        var view = new CollaborationScenarioWorkspace(CollaborationScenario.DelayedTarget, () => ++changes);
        var a = view.Target!.ThreadId;
        var b = view.Workspace!.Threads[1].ThreadId;
        var first = view.SelectAsync(b);
        var second = view.SelectAsync(a);
        var before = changes;
        view.Dispose();
        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(before, changes);
        Assert.Null(view.Workspace);
    }

    [Fact]
    public async Task Disposal_during_section_realignment_does_not_publish_its_outer_completion() {
        var changes = 0;
        var view = new CollaborationScenarioWorkspace(CollaborationScenario.DelayedTarget, () => ++changes);
        var section = view.SetSectionAsync(CollaborationSection.Escalations);
        var before = changes;
        view.Dispose();
        await section.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(before, changes);
        Assert.Null(view.Workspace);
    }

    [Fact]
    public async Task Retired_reply_still_commits_once_without_changing_the_current_editor() {
        using var view = new CollaborationScenarioWorkspace(CollaborationScenario.AdmittedSave, () => { });
        var a = view.Target!.ThreadId;
        var b = view.Workspace!.Threads[1].ThreadId;
        view.Reply.Model.MessageBody = "Admitted on A";
        var save = view.ReplyAsync(view.Target, view.Reply);
        await view.SelectAsync(b);
        var draft = view.Reply;
        var target = view.Target;
        draft.Model.MessageBody = "B's unsent draft";
        var projection = view.Workspace;
        view.CompletePending();
        await save;
        Assert.Same(target, view.Target);
        Assert.Same(draft, view.Reply);
        Assert.Same(projection, view.Workspace);
        Assert.Equal("B's unsent draft", draft.Model.MessageBody);
        Assert.Null(draft.Message);
        await view.SelectAsync(a);
        Assert.Single(view.Workspace!.SelectedThread!.Messages, item => item.Body == "Admitted on A");
    }

    [Fact]
    public async Task Old_reply_commits_without_releasing_or_notifying_an_independent_successor() {
        var changes = 0;
        using var view = new CollaborationScenarioWorkspace(CollaborationScenario.AdmittedSave, () => ++changes);
        var a = view.Target!.ThreadId;
        var b = view.Workspace!.Threads[1].ThreadId;
        view.Reply.Model.MessageBody = "Older A";
        var first = view.ReplyAsync(view.Target, view.Reply);
        await view.SelectAsync(b);
        await view.SelectAsync(a);
        var target = view.Target!;
        var successor = view.Reply;
        var context = successor.Context;
        successor.Model.MessageBody = "Successor A";
        var second = view.ReplyAsync(target, successor);
        var before = changes;
        view.CompletePending();
        await first;
        Assert.False(second.IsCompleted);
        Assert.Same(target, view.Target);
        Assert.Same(successor, view.Reply);
        Assert.Same(context, view.Reply.Context);
        Assert.True(successor.IsSaving);
        Assert.Equal("Successor A", successor.Model.MessageBody);
        Assert.Null(successor.Message);
        Assert.Equal(before, changes);
        view.CompletePending();
        await second;
        Assert.Single(view.Workspace!.SelectedThread!.Messages, item => item.Body == "Older A");
        Assert.Single(view.Workspace.SelectedThread.Messages, item => item.Body == "Successor A");
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public async Task Create_respects_effective_section_and_filter_intent_but_ignores_no_op_setters(bool section, bool effective) {
        using var view = new CollaborationScenarioWorkspace(CollaborationScenario.AdmittedSave, () => { });
        var target = view.Target;
        var reply = view.Reply;
        var save = view.CreateAsync(view.NewThread);
        if (section) {
            var selected = effective ? CollaborationSection.Threads : CollaborationSection.Inbox;
            await view.SetSectionAsync(selected);
            await view.SetSectionAsync(selected);
        } else {
            await view.SetUnreadOnlyAsync(effective);
            await view.SetUnreadOnlyAsync(effective);
        }
        view.CompletePending();
        await save;
        var id = Assert.IsType<Guid>(view.NewThread.SavedThreadId);
        Assert.Equal(section && effective ? CollaborationSection.Threads : CollaborationSection.Inbox, view.Section);
        Assert.Equal(!section && effective, view.UnreadOnly);
        if (effective) {
            Assert.Same(target, view.Target);
            Assert.Same(reply, view.Reply);
        } else {
            Assert.Equal(id, view.Target!.ThreadId);
        }
        await view.RefreshAsync();
        Assert.Single(view.Workspace!.Threads, item => item.ThreadId == id);
    }

    [Fact]
    public async Task Disposal_unwinds_all_admitted_actions_without_callbacks_to_a_replacement() {
        var changes = 0;
        var view = new CollaborationScenarioWorkspace(CollaborationScenario.AdmittedSave, () => ++changes);
        var reply = view.Reply;
        var create = view.NewThread;
        var first = view.ReplyAsync(view.Target!, reply);
        var second = view.CreateAsync(create);
        var before = changes;
        view.Dispose();
        using var replacement = new CollaborationScenarioWorkspace(CollaborationScenario.Representative, () => ++changes);
        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(reply.IsSaving);
        Assert.False(create.IsSaving);
        Assert.Equal(before, changes);
        Assert.Equal(3, replacement.Workspace!.Threads.Count);
        Assert.Empty(replacement.Reply.Model.MessageBody);
    }
}
