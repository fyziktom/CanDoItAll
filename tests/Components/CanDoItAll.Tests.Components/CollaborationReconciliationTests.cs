using Bunit;
using CanDoItAll.Collaboration.UI;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.Collaboration;
using CanDoItAll.Modules.Collaboration.Pages;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Components.Collaboration;

public sealed class CollaborationReconciliationTests : BunitContext {
    public CollaborationReconciliationTests() {
        Services.AddCanDoItAllBaseLib();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Real_reply_input_survives_delayed_reconciliation(bool remainingUnread, bool withoutBlur) {
        var owner = new Owner();
        IRenderedComponent<CollaborationWorkspaceSurface>? cut = null;
        var routes = new List<Guid?>();
        using var session = new CollaborationWorkspaceSession(owner, NullLogger<CollaborationWorkspaceSession>.Instance,
            () => cut?.Render(), routes.Add, _ => { }, (_, _) => { });
        await session.ApplyRouteAsync(null);
        await session.SetUnreadOnlyAsync(true);
        cut = Render<CollaborationWorkspaceSurface>(parameters => parameters.Add(item => item.View, session));
        await cut.InvokeAsync(() => cut.Find("[data-testid=collaboration-reply-message]").Change("Accepted first reply"));
        var pending = new TaskCompletionSource<CollaborationWorkspaceModel>(TaskCreationOptions.RunContinuationsAsynchronously);
        owner.Read = id => id == Owner.A ? pending.Task : Task.FromResult(Owner.Snapshot(id, false, remainingUnread));
        var submit = cut.InvokeAsync(() => cut.Find("[data-testid=collaboration-reply-form]").SubmitAsync());
        cut.WaitForAssertion(() => Assert.Equal(CollaborationReadState.Refreshing, session.ReadState));
        var target = session.Target;
        var draft = session.Reply;
        var context = draft.Context;
        await cut.InvokeAsync(() => {
            var field = cut.Find("[data-testid=collaboration-reply-message]");
            if (withoutBlur) {
                field.Input("Unsent successor for A");
            } else {
                field.Change("Unsent successor for A");
            }
        });
        pending.SetResult(Owner.Snapshot(Owner.A, false, remainingUnread));
        await submit;
        Assert.Same(target, session.Target);
        Assert.Same(draft, session.Reply);
        Assert.Same(context, session.Reply.Context);
        Assert.True(context.IsModified());
        Assert.Equal("Unsent successor for A", session.Reply.Model.MessageBody);
        Assert.Equal("Unsent successor for A", cut.Find("[data-testid=collaboration-reply-message]").GetAttribute("value"));
        Assert.Single(cut.FindAll("[data-testid=collaboration-reply-retained]"));
        Assert.Equal(Owner.A, Assert.Single(owner.Replies).ThreadId);
        Assert.Empty(routes);
        await cut.InvokeAsync(() => session.SetSectionAsync(CollaborationSection.Threads));
        await cut.InvokeAsync(() => session.SelectAsync(Owner.B));
        Assert.Empty(session.Reply.Model.MessageBody);
        Assert.Equal([Owner.B], routes);
        Assert.Single(owner.Replies);
    }

    [Fact]
    public async Task Disabled_mark_read_is_not_completion_until_the_target_snapshot_is_accepted() {
        var owner = new Owner();
        var write = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
        var read = new TaskCompletionSource<CollaborationWorkspaceModel>(TaskCreationOptions.RunContinuationsAsynchronously);
        IRenderedComponent<CollaborationWorkspaceSurface>? cut = null;
        using var session = new CollaborationWorkspaceSession(owner, NullLogger<CollaborationWorkspaceSession>.Instance,
            () => cut?.Render(), _ => { }, _ => { }, (_, _) => { });
        await session.ApplyRouteAsync(null);
        cut = Render<CollaborationWorkspaceSurface>(parameters => parameters.Add(item => item.View, session));
        Assert.True(session.Workspace!.SelectedThread!.IsUnread);
        owner.Mark = () => write.Task;
        owner.Read = _ => read.Task;
        var mark = cut.InvokeAsync(() => cut.Find("[data-testid=collaboration-mark-read]").ClickAsync(new MouseEventArgs()));
        cut.WaitForAssertion(() => Assert.True(cut.Find("[data-testid=collaboration-mark-read]").HasAttribute("disabled")));
        Assert.Equal("ready", cut.Find("[data-testid=collaboration-workspace]").GetAttribute("data-phase"));
        Assert.False(AcceptedRead(cut, Owner.A));
        Assert.False(mark.IsCompleted);
        Assert.True(session.Workspace.SelectedThread.IsUnread);
        write.SetResult(Result.Success());
        cut.WaitForAssertion(() => Assert.Equal("refreshing", cut.Find("[data-testid=collaboration-workspace]").GetAttribute("data-phase")));
        Assert.False(AcceptedRead(cut, Owner.A));
        read.SetResult(Owner.Snapshot(Owner.A, false, true));
        await mark;
        Assert.True(AcceptedRead(cut, Owner.A));
        Assert.False(AcceptedRead(cut, Owner.B));
        Assert.Equal(1, owner.Marks);
    }

    private static bool AcceptedRead(IRenderedComponent<CollaborationWorkspaceSurface> cut, Guid id) {
        var surface = cut.Find("[data-testid=collaboration-workspace]");
        return surface.GetAttribute("data-selected-thread-id") == id.ToString("D")
            && surface.GetAttribute("data-selected-unread") == "false"
            && surface.GetAttribute("data-phase") == "ready";
    }

    private sealed class Owner : ICollaborationWorkspaceOwner {
        public static readonly Guid A = new(1, 0, 0, new byte[8]);
        public static readonly Guid B = new(2, 0, 0, new byte[8]);
        public Func<Guid?, Task<CollaborationWorkspaceModel>> Read { get; set; } = id => Task.FromResult(Snapshot(id));
        public Func<Task<Result>> Mark { get; set; } = () => Task.FromResult(Result.Success());
        public List<CollaborationMessageWriteRequest> Replies { get; } = [];
        public int Marks { get; private set; }

        public Task<CollaborationWorkspaceModel> GetWorkspaceAsync(Guid? selectedThreadId = null, CancellationToken cancellationToken = default) => Read(selectedThreadId);
        public Task<Result<Guid>> CreateThreadAsync(CollaborationThreadCreateRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result> AppendMessageAsync(CollaborationMessageWriteRequest request, CancellationToken cancellationToken = default) {
            Replies.Add(request);
            return Task.FromResult(Result.Success());
        }
        public Task<Result> MarkThreadAsReadAsync(Guid threadId, CancellationToken cancellationToken = default) {
            Assert.Equal(A, threadId);
            ++Marks;
            return Mark();
        }

        public static CollaborationWorkspaceModel Snapshot(Guid? id, bool aUnread = true, bool bUnread = true) {
            var now = DateTimeOffset.UnixEpoch;
            var inbox = new[] { A, B }.Select(value => new CollaborationInboxItemSummary(value, value, CollaborationInboxItemKind.Notification,
                value == A ? "A" : "B", "Preview", $"/collaboration?threadId={value:D}", value == A ? aUnread : bUnread, 1, now)).ToArray();
            var threads = inbox.Select(item => new CollaborationThreadSummary(item.ThreadId, item.Title, CollaborationContextKind.Manual,
                "Manual", item.ItemKind, CollaborationThreadState.Open, now, 1)).ToArray();
            var selected = inbox.Single(item => item.ThreadId == (id ?? A));
            var detail = new CollaborationThreadDetailModel(selected.ThreadId, selected.Title, CollaborationContextKind.Manual, null, null,
                "Manual", "/scheduler", selected.ItemKind, CollaborationThreadState.Open, selected.IsUnread, selected.IsUnread ? 1 : 0, now, now, [], []);
            return new(inbox, threads, [], detail, new(inbox.Count(item => item.IsUnread), 0));
        }
    }
}
