using CanDoItAll.Collaboration.UI;
using CanDoItAll.Modules.Collaboration;
using CanDoItAll.Modules.Collaboration.Pages;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Unit.Collaboration;

public sealed class CollaborationWorkspaceSessionTests {
    private static readonly Guid A = new(1, 0, 0, new byte[8]);
    private static readonly Guid B = new(2, 0, 0, new byte[8]);

    [Fact]
    public async Task Same_route_refresh_and_section_preserve_both_drafts_and_contexts() {
        var owner = new Owner();
        using var session = Session(owner);
        await session.ApplyRouteAsync(null);
        var draft = session.Reply;
        var create = session.NewThread;
        draft.Model.MessageBody = "Unsent reply";
        create.Model.Subject = "Unsent create";
        await session.ApplyRouteAsync(null);
        Assert.Equal(1, owner.Reads);
        await session.SetSectionAsync(CollaborationSection.Threads);
        await session.RefreshAsync();
        Assert.Same(draft, session.Reply);
        Assert.Same(draft.Context, session.Reply.Context);
        Assert.Same(create, session.NewThread);
        Assert.Equal("Unsent reply", session.Reply.Model.MessageBody);
        Assert.Equal("Unsent create", session.NewThread.Model.Subject);
    }

    [Fact]
    public async Task Unread_filter_keeps_empty_selection_despite_owner_null_fallback_and_does_not_filter_threads() {
        var owner = new Owner { Read = (id, _) => Task.FromResult(Snapshot(id, unread: false)) };
        using var session = Session(owner);
        await session.ApplyRouteAsync(null);
        await session.SetUnreadOnlyAsync(true);
        Assert.Null(session.Target);
        Assert.Null(session.Workspace!.SelectedThread);
        Assert.False(session.IsMissing);
        await session.RefreshAsync();
        Assert.Null(session.Workspace!.SelectedThread);
        await session.SetSectionAsync(CollaborationSection.Threads);
        Assert.Equal(A, session.Workspace!.SelectedThread!.ThreadId);
    }

    [Fact]
    public async Task Explicit_missing_thread_is_distinct_from_successful_empty_workspace() {
        using var missing = Session(new());
        await missing.ApplyRouteAsync(Guid.NewGuid());
        Assert.True(missing.IsMissing);
        Assert.NotNull(missing.Target);
        using var empty = Session(new Owner { Read = (_, _) => Task.FromResult(new CollaborationWorkspaceModel([], [], [], null, new(0, 0))) });
        await empty.ApplyRouteAsync(null);
        Assert.False(empty.IsMissing);
        Assert.Equal(CollaborationReadState.Ready, empty.ReadState);
    }

    [Fact]
    public async Task A_B_A_ignores_old_success_error_and_finally_even_when_cancellation_is_ignored() {
        var first = Completion<CollaborationWorkspaceModel>();
        var second = Completion<CollaborationWorkspaceModel>();
        var third = Completion<CollaborationWorkspaceModel>();
        var reads = new Queue<Task<CollaborationWorkspaceModel>>([first.Task, second.Task, third.Task]);
        var tokens = new List<CancellationToken>();
        var owner = new Owner { Read = (_, token) => {
            tokens.Add(token);
            return reads.Dequeue();
        } };
        using var session = Session(owner);
        var loadA = session.ApplyRouteAsync(A);
        var loadB = session.ApplyRouteAsync(B);
        var returnA = session.ApplyRouteAsync(A);
        Assert.True(tokens[0].IsCancellationRequested);
        Assert.True(tokens[1].IsCancellationRequested);
        first.SetResult(Snapshot(A));
        second.SetException(new InvalidOperationException("Old B failure"));
        await Task.WhenAll(loadA, loadB);
        Assert.Equal(CollaborationReadState.Loading, session.ReadState);
        Assert.Null(session.Workspace);
        Assert.Null(session.ReadError);
        third.SetResult(Snapshot(A));
        await returnA;
        Assert.Equal(A, session.Workspace!.SelectedThread!.ThreadId);
        Assert.Equal(CollaborationReadState.Ready, session.ReadState);
    }

    [Fact]
    public async Task Disposal_retires_pending_reads_without_rendering_or_accepting_results() {
        var pending = Completion<CollaborationWorkspaceModel>();
        var renders = 0;
        var session = new CollaborationWorkspaceSession(new Owner { Read = (_, _) => pending.Task }, NullLogger<CollaborationWorkspaceSession>.Instance, () => renders++, _ => { }, _ => { }, (_, _) => { });
        var load = session.ApplyRouteAsync(A);
        session.Dispose();
        var before = renders;
        pending.SetResult(Snapshot(A));
        await load;
        Assert.Equal(before, renders);
        Assert.Null(session.Workspace);
    }

    [Fact]
    public async Task Failed_refresh_preserves_same_scope_but_failed_new_target_never_shows_old_detail() {
        var owner = new Owner();
        using var session = Session(owner);
        await session.ApplyRouteAsync(A);
        var accepted = session.Workspace;
        owner.Read = (_, _) => throw new InvalidOperationException("Unavailable");
        await session.RefreshAsync();
        Assert.Same(accepted, session.Workspace);
        Assert.Equal(CollaborationReadState.Stale, session.ReadState);
        await session.ApplyRouteAsync(B);
        Assert.Null(session.Workspace);
        Assert.Equal(CollaborationReadState.Failed, session.ReadState);
        Assert.Equal(B, session.Target!.ThreadId);
    }

    [Fact]
    public async Task Empty_filter_preserves_the_warning_on_a_stale_snapshot() {
        var owner = new Owner { Read = (id, _) => Task.FromResult(Snapshot(id, unread: false)) };
        using var session = Session(owner);
        await session.ApplyRouteAsync(null);
        owner.Read = (_, _) => throw new InvalidOperationException("Refresh unavailable");
        await session.RefreshAsync();
        var warning = session.ReadError;
        await session.SetUnreadOnlyAsync(true);
        Assert.Null(session.Workspace!.SelectedThread);
        Assert.Equal(CollaborationReadState.Stale, session.ReadState);
        Assert.Equal(warning, session.ReadError);
    }

    [Fact]
    public async Task Superseded_reply_reconciliation_cannot_relabel_a_newer_saved_reply() {
        var owner = new Owner();
        using var session = Session(owner);
        await session.ApplyRouteAsync(A);
        var firstRead = Completion<CollaborationWorkspaceModel>();
        var secondRead = Completion<CollaborationWorkspaceModel>();
        var reads = new Queue<Task<CollaborationWorkspaceModel>>([firstRead.Task, secondRead.Task]);
        owner.Read = (_, _) => reads.Dequeue();
        session.Reply.Model.MessageBody = "First reply";
        var firstSave = session.ReplyAsync(session.Target!, session.Reply);
        session.Reply.Model.MessageBody = "Second reply";
        var secondSave = session.ReplyAsync(session.Target!, session.Reply);
        secondRead.SetException(new InvalidOperationException("Current refresh unavailable"));
        await secondSave;
        var current = session.Reply;
        current.Message = "Current draft status";
        firstRead.SetException(new InvalidOperationException("Retired refresh unavailable"));
        await firstSave;
        Assert.Same(current, session.Reply);
        Assert.Equal("Current draft status", current.Message);
        Assert.Equal(2, owner.Replies.Count);
    }

    [Fact]
    public async Task Reply_admits_once_captures_values_and_local_unread_semantics() {
        var pending = Completion<Result>();
        var owner = new Owner { Append = _ => pending.Task };
        using var session = Session(owner);
        await session.ApplyRouteAsync(A);
        var target = session.Target!;
        var draft = session.Reply;
        draft.Model.MessageBody = "Submitted";
        draft.Model.MessageKind = CollaborationMessageKind.System;
        var save = session.ReplyAsync(target, draft);
        await session.ReplyAsync(target, draft);
        Assert.True(draft.IsLocked);
        Assert.Single(owner.Replies);
        Assert.Equal("Submitted", owner.Replies[0].MessageBody);
        Assert.False(owner.Replies[0].MarkAsUnread);
        pending.SetResult(Result.Success());
        await save;
        Assert.Equal(string.Empty, session.Reply.Model.MessageBody);
        Assert.Equal(CollaborationMessageKind.Standard, session.Reply.Model.MessageKind);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Retired_reply_callbacks_and_completion_cannot_change_successor_draft_or_busy_gate(bool oldFailure) {
        var first = Completion<Result>();
        var second = Completion<Result>();
        var writes = new Queue<Task<Result>>([first.Task, second.Task]);
        var owner = new Owner { Append = _ => writes.Dequeue() };
        using var session = Session(owner);
        await session.ApplyRouteAsync(A);
        var oldTarget = session.Target!;
        var oldDraft = session.Reply;
        oldDraft.Model.MessageBody = "Old A";
        var oldSave = session.ReplyAsync(oldTarget, oldDraft);
        await session.SelectAsync(B);
        await session.SelectAsync(A);
        Assert.NotSame(oldTarget, session.Target);
        await session.ReplyAsync(oldTarget, oldDraft);
        Assert.Single(owner.Replies);
        var current = session.Reply;
        current.Model.MessageBody = "New A";
        var currentSave = session.ReplyAsync(session.Target!, current);
        if (oldFailure) {
            first.SetException(new InvalidOperationException("Retired command failure"));
        } else {
            first.SetResult(Result.Success());
        }
        await oldSave;
        Assert.Same(current, session.Reply);
        Assert.True(current.IsSaving);
        Assert.Equal("New A", current.Model.MessageBody);
        second.SetResult(Result.Success());
        await currentSave;
    }

    [Fact]
    public async Task Create_completion_preserves_identity_but_does_not_navigate_a_newer_target() {
        var pending = Completion<Result<Guid>>();
        var owner = new Owner { Create = _ => pending.Task };
        var routes = new List<Guid?>();
        using var session = Session(owner, routes.Add);
        await session.ApplyRouteAsync(A);
        Fill(session.NewThread.Model);
        var origin = session.NewThread;
        var save = session.CreateAsync(origin);
        await session.CreateAsync(origin);
        Assert.Equal(1, owner.Creates);
        await session.SelectAsync(B);
        session.Reply.Model.MessageBody = "B remains mine";
        var newId = Guid.NewGuid();
        pending.SetResult(Result<Guid>.Success(newId));
        await save;
        Assert.Equal(newId, session.NewThread.SavedThreadId);
        Assert.Equal(B, session.Target!.ThreadId);
        Assert.Equal("B remains mine", session.Reply.Model.MessageBody);
        Assert.Equal([B], routes);
    }

    [Fact]
    public async Task Committed_create_with_failed_refresh_retains_identity_and_retry_only_reads() {
        var owner = new Owner();
        using var session = Session(owner);
        await session.ApplyRouteAsync(null);
        Fill(session.NewThread.Model);
        owner.Read = (_, _) => throw new InvalidOperationException("Refresh unavailable");
        await session.CreateAsync(session.NewThread);
        Assert.Equal(B, session.Target!.ThreadId);
        Assert.Equal(B, session.NewThread.SavedThreadId);
        Assert.Contains("Saved", session.NewThread.Message);
        owner.Read = (id, _) => Task.FromResult(Snapshot(id));
        await session.RefreshAsync();
        Assert.Equal(1, owner.Creates);
        Assert.Equal(B, session.Workspace!.SelectedThread!.ThreadId);
    }

    [Fact]
    public async Task Committed_reply_with_failed_refresh_keeps_saved_message_and_does_not_replay() {
        var owner = new Owner();
        using var session = Session(owner);
        await session.ApplyRouteAsync(A);
        var draft = session.Reply;
        draft.Model.MessageBody = "Saved reply";
        owner.Read = (_, _) => throw new InvalidOperationException("Refresh unavailable");
        await session.ReplyAsync(session.Target!, draft);
        Assert.Equal(CollaborationReadState.Stale, session.ReadState);
        Assert.Contains("saved", session.Reply.Message);
        await session.RefreshAsync();
        Assert.Single(owner.Replies);
    }

    [Fact]
    public async Task Unknown_submission_is_preserved_and_locked_until_explicit_new_draft() {
        var owner = new Owner { Create = _ => throw new InvalidOperationException("Lost acknowledgement") };
        using var session = Session(owner);
        Fill(session.NewThread.Model);
        var draft = session.NewThread;
        await session.CreateAsync(draft);
        await session.CreateAsync(draft);
        Assert.Equal(1, owner.Creates);
        Assert.Same(draft, session.NewThread);
        Assert.True(draft.OutcomeUnknown);
        Assert.False(draft.IsSaving);
        await session.RefreshAsync();
        Assert.True(draft.IsLocked);
        session.PrepareThread(draft, CollaborationInboxItemKind.Notification);
        Assert.NotSame(draft, session.NewThread);
        Assert.False(session.NewThread.IsLocked);
    }

    [Fact]
    public async Task Owner_rejection_preserves_draft_context_and_allows_correction() {
        var owner = new Owner { Append = _ => Task.FromResult(Result.Failure(Error.Validation("Reply refused"))) };
        using var session = Session(owner);
        await session.ApplyRouteAsync(A);
        var draft = session.Reply;
        draft.Model.MessageBody = "Keep this";
        await session.ReplyAsync(session.Target!, draft);
        Assert.Same(draft, session.Reply);
        Assert.Equal("Keep this", draft.Model.MessageBody);
        Assert.Equal("Reply refused", draft.Message);
        Assert.False(draft.IsLocked);
    }

    [Fact]
    public async Task Clear_preserves_kind_but_target_switch_resets_reply_and_leaves_quick_create_alone() {
        using var session = Session(new());
        await session.ApplyRouteAsync(A);
        var create = session.NewThread;
        create.Model.Subject = "Independent";
        session.Reply.Model.MessageKind = CollaborationMessageKind.Escalation;
        session.Reply.Model.MessageBody = "Clear me";
        session.ClearReply(session.Target!, session.Reply);
        Assert.Equal(CollaborationMessageKind.Escalation, session.Reply.Model.MessageKind);
        Assert.Empty(session.Reply.Model.MessageBody);
        await session.SelectAsync(B);
        Assert.Equal(CollaborationMessageKind.Standard, session.Reply.Model.MessageKind);
        Assert.Same(create, session.NewThread);
        Assert.Equal("Independent", create.Model.Subject);
    }

    [Fact]
    public async Task Mark_read_is_admitted_once_and_retired_callback_cannot_target_new_selection() {
        var pending = Completion<Result>();
        var owner = new Owner { Mark = _ => pending.Task };
        using var session = Session(owner);
        await session.ApplyRouteAsync(A);
        var target = session.Target!;
        var save = session.MarkReadAsync(target);
        await session.MarkReadAsync(target);
        await session.SelectAsync(B);
        await session.MarkReadAsync(target);
        pending.SetResult(Result.Success());
        await save;
        Assert.Equal([A], owner.Marked);
        Assert.Equal(B, session.Target!.ThreadId);
        Assert.Null(session.Target.Message);
    }

    [Theory]
    [InlineData("/scheduler", true)]
    [InlineData("/processes?runId=1#detail", true)]
    [InlineData("https://example.test", false)]
    [InlineData("//example.test", false)]
    [InlineData("/\\example.test", false)]
    [InlineData("javascript:alert(1)", false)]
    public void Context_routes_accept_local_links_and_refuse_external_or_script_navigation(string route, bool safe) {
        Assert.Equal(safe, CollaborationWorkspaceSession.IsSafeContextRoute(route));
    }

    [Fact]
    public async Task Explicit_deep_link_clears_a_previous_unread_filter_without_reloading_route_echoes() {
        var owner = new Owner { Read = (id, _) => Task.FromResult(Snapshot(id, unread: false)) };
        using var session = Session(owner);
        await session.ApplyRouteAsync(null);
        await session.SetUnreadOnlyAsync(true);
        await session.ApplyRouteAsync(B);
        Assert.False(session.UnreadOnly);
        Assert.Equal(B, session.Workspace!.SelectedThread!.ThreadId);
        var reads = owner.Reads;
        await session.ApplyRouteAsync(B);
        Assert.Equal(reads, owner.Reads);
    }

    [Fact]
    public async Task Section_changed_during_initial_read_is_preserved_and_empty_selection_is_explicit() {
        var pending = Completion<CollaborationWorkspaceModel>();
        using var session = Session(new Owner { Read = (_, _) => pending.Task });
        var read = session.ApplyRouteAsync(null);
        await session.SetSectionAsync(CollaborationSection.Escalations);
        pending.SetResult(Snapshot(A));
        await read;
        Assert.Equal(CollaborationSection.Escalations, session.Section);
        Assert.Null(session.Target);
        Assert.Null(session.Workspace!.SelectedThread);
    }

    [Fact]
    public async Task Disposal_during_create_suppresses_completion_effects_without_cancelling_the_owner_write() {
        var pending = Completion<Result<Guid>>();
        var routes = new List<Guid?>();
        var session = Session(new Owner { Create = _ => pending.Task }, routes.Add);
        Fill(session.NewThread.Model);
        var draft = session.NewThread;
        var save = session.CreateAsync(draft);
        session.Dispose();
        pending.SetResult(Result<Guid>.Success(B));
        await save;
        Assert.Empty(routes);
        Assert.Same(draft, session.NewThread);
        Assert.Equal("Subject", draft.Model.Subject);
    }

    [Fact]
    public async Task Mark_read_admission_remains_held_until_its_reconciliation_finishes() {
        var owner = new Owner();
        using var session = Session(owner);
        await session.ApplyRouteAsync(A);
        var pending = Completion<CollaborationWorkspaceModel>();
        owner.Read = (_, _) => pending.Task;
        var target = session.Target!;
        var first = session.MarkReadAsync(target);
        var second = session.MarkReadAsync(target);
        var calls = owner.Marked.Count;
        pending.SetResult(Snapshot(A, unread: false));
        await Task.WhenAll(first, second);
        Assert.Equal(1, calls);
        Assert.False(target.IsMarkingRead);
    }

    [Fact]
    public async Task Unknown_mark_read_recovery_only_reads_before_enabling_a_new_explicit_action() {
        var owner = new Owner { Mark = _ => throw new InvalidOperationException("Missing acknowledgement") };
        using var session = Session(owner);
        await session.ApplyRouteAsync(A);
        var target = session.Target!;
        await session.MarkReadAsync(target);
        Assert.True(target.OutcomeUnknown);
        await session.MarkReadAsync(target);
        Assert.Single(owner.Marked);
        await session.RefreshAsync();
        Assert.False(target.OutcomeUnknown);
        Assert.Single(owner.Marked);
    }

    private static CollaborationWorkspaceSession Session(Owner owner, Action<Guid?>? route = null) => new(owner, NullLogger<CollaborationWorkspaceSession>.Instance, () => { }, route ?? (_ => { }), _ => { }, (_, _) => { });
    private static TaskCompletionSource<T> Completion<T>() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static void Fill(CollaborationThreadEditorModel model) {
        model.Subject = "Subject";
        model.MessageBody = "Message";
    }

    private static CollaborationWorkspaceModel Snapshot(Guid? id, bool unread = true) {
        var now = DateTimeOffset.UnixEpoch;
        var inbox = new[] { A, B }.Select(value => new CollaborationInboxItemSummary(value, value, CollaborationInboxItemKind.Notification, value == A ? "A" : "B", "Preview", $"/collaboration?threadId={value:D}", unread, unread ? 1 : 0, now)).ToArray();
        var threads = inbox.Select(item => new CollaborationThreadSummary(item.ThreadId, item.Title, CollaborationContextKind.Manual, "Manual", item.ItemKind, CollaborationThreadState.Open, now, 1)).ToArray();
        var selected = threads.FirstOrDefault(item => item.ThreadId == (id ?? A));
        var detail = selected is null ? null : new CollaborationThreadDetailModel(selected.ThreadId, selected.Subject, selected.ContextKind, null, null, "Manual", "/scheduler", selected.ItemKind, selected.State, unread, unread ? 1 : 0, now, now, [], []);
        return new(inbox, threads, [], detail, new(unread ? 2 : 0, 0));
    }

    private sealed class Owner : ICollaborationWorkspaceOwner {
        public int Reads { get; private set; }
        public int Creates { get; private set; }
        public List<CollaborationMessageWriteRequest> Replies { get; } = [];
        public List<Guid> Marked { get; } = [];
        public Func<Guid?, CancellationToken, Task<CollaborationWorkspaceModel>> Read { get; set; } = (id, _) => Task.FromResult(Snapshot(id));
        public Func<CollaborationThreadCreateRequest, Task<Result<Guid>>> Create { get; set; } = _ => Task.FromResult(Result<Guid>.Success(B));
        public Func<CollaborationMessageWriteRequest, Task<Result>> Append { get; set; } = _ => Task.FromResult(Result.Success());
        public Func<Guid, Task<Result>> Mark { get; set; } = _ => Task.FromResult(Result.Success());

        public Task<CollaborationWorkspaceModel> GetWorkspaceAsync(Guid? selectedThreadId = null, CancellationToken cancellationToken = default) {
            Reads++;
            return Read(selectedThreadId, cancellationToken);
        }

        public Task<Result<Guid>> CreateThreadAsync(CollaborationThreadCreateRequest request, CancellationToken cancellationToken = default) {
            Creates++;
            return Create(request);
        }

        public Task<Result> AppendMessageAsync(CollaborationMessageWriteRequest request, CancellationToken cancellationToken = default) {
            Replies.Add(request);
            return Append(request);
        }

        public Task<Result> MarkThreadAsReadAsync(Guid threadId, CancellationToken cancellationToken = default) {
            Marked.Add(threadId);
            return Mark(threadId);
        }
    }
}
