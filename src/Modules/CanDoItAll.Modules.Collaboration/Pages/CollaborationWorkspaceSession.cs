using CanDoItAll.Collaboration.UI;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Collaboration.Pages;

public sealed class CollaborationWorkspaceSession(
    ICollaborationWorkspaceOwner owner,
    ILogger<CollaborationWorkspaceSession> logger,
    Action changed,
    Action<Guid?> selectionChanged,
    Action<string> navigate,
    Action<string, bool> notify) : ICollaborationWorkspaceView, IDisposable {
    private CancellationTokenSource? readCancellation;
    private long readGeneration;
    private long selectionGeneration;
    private bool disposed;
    private bool routeDelivered;
    private Guid? lastRoute;
    private SelectionKind selectionKind;

    public CollaborationWorkspaceModel? Workspace { get; private set; }
    public CollaborationSection Section { get; private set; }
    public CollaborationReadState ReadState { get; private set; } = CollaborationReadState.Loading;
    public bool UnreadOnly { get; private set; }
    public bool IsMissing => selectionKind == SelectionKind.Explicit && Workspace is { SelectedThread: null };
    public bool IsReplyRetained => Workspace?.SelectedThread is { } selected && Target?.ThreadId == selected.ThreadId
        && !VisibleIds(Workspace).Contains(selected.ThreadId) && CollaborationReplyPolicy.MustRetain(Reply);
    public string? ReadError { get; private set; }
    public CollaborationDraft<CollaborationThreadEditorModel> NewThread { get; private set; } = new(new());
    public CollaborationDraft<CollaborationReplyEditorModel> Reply { get; private set; } = new(new());
    public CollaborationTarget? Target { get; private set; }

    public async Task ApplyRouteAsync(Guid? threadId) {
        if (disposed || routeDelivered && lastRoute == threadId) {
            return;
        }

        routeDelivered = true;
        lastRoute = threadId;
        if (threadId.HasValue) {
            UnreadOnly = false;
        }
        ChangeTarget(threadId, threadId.HasValue ? SelectionKind.Explicit : SelectionKind.Initial);
        await ReadAsync(resolveSection: threadId.HasValue);
    }

    public Task RefreshAsync() => ReadAsync();

    public async Task SelectAsync(Guid threadId) {
        if (disposed || Target?.ThreadId == threadId) {
            return;
        }

        ChangeTarget(threadId, SelectionKind.Explicit);
        PublishSelection();
        await ReadAsync();
    }

    public async Task SetSectionAsync(CollaborationSection section) {
        if (disposed || Section == section) {
            return;
        }

        Section = section;
        ++selectionGeneration;
        await RealignAsync();
    }

    public async Task SetUnreadOnlyAsync(bool unreadOnly) {
        if (disposed || UnreadOnly == unreadOnly) {
            return;
        }

        UnreadOnly = unreadOnly;
        ++selectionGeneration;
        await RealignAsync();
    }

    private async Task RealignAsync() {
        if (Workspace is null) {
            changed();
            return;
        }

        var visible = VisibleIds(Workspace).ToArray();
        if (IsReplyRetained || Target is not null && visible.Contains(Target.ThreadId)) {
            changed();
            return;
        }

        Guid? next = visible.Length > 0 ? visible[0] : null;
        if (next is null) {
            var accepted = Workspace;
            var readError = ReadError;
            ChangeTarget(null, SelectionKind.Empty);
            Workspace = accepted with { SelectedThread = null };
            ReadError = readError;
            ReadState = readError is null ? CollaborationReadState.Ready : CollaborationReadState.Stale;
            PublishSelection();
            changed();
            return;
        }

        await SelectAsync(next.Value);
    }

    private IEnumerable<Guid> VisibleIds(CollaborationWorkspaceModel workspace) => Section switch {
        CollaborationSection.Threads => workspace.Threads.Select(item => item.ThreadId),
        CollaborationSection.Escalations => workspace.Escalations.Where(item => !UnreadOnly || item.IsUnread).Select(item => item.ThreadId),
        _ => workspace.InboxItems.Where(item => !UnreadOnly || item.IsUnread).Select(item => item.ThreadId)
    };

    private void ChangeTarget(Guid? threadId, SelectionKind kind) {
        ++selectionGeneration;
        ++readGeneration;
        readCancellation?.Cancel();
        readCancellation = null;
        selectionKind = kind;
        Target = threadId.HasValue ? new(threadId.Value) : null;
        Reply = new(new());
        Workspace = null;
        ReadError = null;
        ReadState = CollaborationReadState.Loading;
    }

    private async Task ReadAsync(bool resolveSection = false) {
        if (disposed) {
            return;
        }

        readCancellation?.Cancel();
        using var cancellation = new CancellationTokenSource();
        readCancellation = cancellation;
        var generation = ++readGeneration;
        var kind = selectionKind;
        var target = Target;
        var selection = selectionGeneration;
        ReadState = Workspace is null ? CollaborationReadState.Loading : CollaborationReadState.Refreshing;
        ReadError = null;
        changed();
        try {
            var result = await owner.GetWorkspaceAsync(target?.ThreadId, cancellation.Token);
            if (!IsCurrentRead(generation)) {
                return;
            }

            Workspace = kind == SelectionKind.Empty ? result with { SelectedThread = null } : result;
            if (kind == SelectionKind.Initial) {
                Target = result.SelectedThread is { } selected ? new(selected.ThreadId) : null;
                selectionKind = Target is null ? SelectionKind.Empty : SelectionKind.Explicit;
            }

            if (resolveSection && selection == selectionGeneration && Target is { } selectedTarget) {
                Section = result.Escalations.Any(item => item.ThreadId == selectedTarget.ThreadId)
                    ? CollaborationSection.Escalations
                    : result.Threads.Any(item => item.ThreadId == selectedTarget.ThreadId) ? CollaborationSection.Threads : CollaborationSection.Inbox;
            }

            ReadState = CollaborationReadState.Ready;
            if (target is { OutcomeUnknown: true }) {
                target.OutcomeUnknown = false;
                target.Message = "Unread state reloaded. Review it before marking again.";
            }
            if (!IsMissing && (UnreadOnly && Section != CollaborationSection.Threads || selection != selectionGeneration)) {
                await RealignAsync();
            }
        } catch (OperationCanceledException) when (cancellation.IsCancellationRequested) {
        } catch (Exception exception) {
            logger.LogWarning(exception, "Collaboration workspace read failed for thread {ThreadId}, generation {Generation}.", target?.ThreadId, generation);
            if (IsCurrentRead(generation)) {
                ReadError = Workspace is null
                    ? "The collaboration workspace could not be loaded. Retry the read."
                    : "Refresh failed. Showing the last accepted snapshot. Retry the read.";
                ReadState = Workspace is null ? CollaborationReadState.Failed : CollaborationReadState.Stale;
            }
        } finally {
            if (IsCurrentRead(generation)) {
                readCancellation = null;
                changed();
            }
        }
    }

    public void PrepareThread(CollaborationDraft<CollaborationThreadEditorModel> origin, CollaborationInboxItemKind kind) {
        if (disposed || !ReferenceEquals(NewThread, origin) || origin.IsSaving) {
            return;
        }

        NewThread = new(new() { ItemKind = kind });
        changed();
    }

    public async Task CreateAsync(CollaborationDraft<CollaborationThreadEditorModel> origin) {
        if (disposed || !ReferenceEquals(NewThread, origin) || !origin.TryBegin()) {
            return;
        }

        var request = CollaborationService.CreateManualThreadRequest(origin.Model);
        var selection = selectionGeneration;
        changed();
        Result<Guid> result;
        try {
            result = await owner.CreateThreadAsync(request);
        } catch (Exception exception) {
            HandleUnknown(origin, exception, "create", null);
            return;
        } finally {
            origin.Complete(origin.OutcomeUnknown);
        }

        if (disposed || !ReferenceEquals(NewThread, origin)) {
            return;
        }

        if (result.IsFailure) {
            Reject(origin, result, "Unable to create the collaboration thread.");
            return;
        }

        NewThread = new(new() { ItemKind = request.ItemKind }) {
            SavedThreadId = result.Value,
            Message = "Collaboration thread created."
        };
        if (selection == selectionGeneration) {
            Section = request.ItemKind == CollaborationInboxItemKind.Escalation ? CollaborationSection.Escalations : CollaborationSection.Inbox;
            ChangeTarget(result.Value, SelectionKind.Explicit);
            var createdTarget = Target;
            var acceptedDraft = NewThread;
            PublishSelection();
            notify(NewThread.Message, false);
            await ReadAsync();
            if (!disposed && ReferenceEquals(Target, createdTarget) && ReferenceEquals(NewThread, acceptedDraft)
                && ReadState is CollaborationReadState.Failed or CollaborationReadState.Stale) {
                acceptedDraft.Message = "Saved successfully. Refresh failed; retry the read, not the create.";
            }
        }

        if (!disposed) {
            changed();
        }
    }

    public async Task ReplyAsync(CollaborationTarget target, CollaborationDraft<CollaborationReplyEditorModel> origin) {
        if (!IsCurrentTarget(target) || !ReferenceEquals(Reply, origin) || Workspace?.SelectedThread is null || !origin.TryBegin()) {
            return;
        }

        var request = CollaborationService.CreateLocalReplyRequest(target.ThreadId, origin.Model);
        changed();
        Result result;
        try {
            result = await owner.AppendMessageAsync(request);
        } catch (Exception exception) {
            if (IsCurrentTarget(target) && ReferenceEquals(Reply, origin)) {
                HandleUnknown(origin, exception, "reply", target.ThreadId);
            }
            return;
        } finally {
            origin.Complete(origin.OutcomeUnknown);
        }

        if (!IsCurrentTarget(target) || !ReferenceEquals(Reply, origin)) {
            return;
        }

        if (result.IsFailure) {
            Reject(origin, result, "Unable to append the collaboration message.");
            return;
        }

        Reply = new(new()) { Message = "Reply recorded on the selected thread." };
        var acceptedDraft = Reply;
        notify(Reply.Message, false);
        await ReadAsync();
        if (IsCurrentTarget(target) && ReferenceEquals(Reply, acceptedDraft)
            && ReadState is CollaborationReadState.Failed or CollaborationReadState.Stale) {
            acceptedDraft.Message = "Reply saved. Refresh failed; retry the read, not the reply.";
            changed();
        }
    }

    public async Task MarkReadAsync(CollaborationTarget target) {
        if (!IsCurrentTarget(target) || target.IsMarkingRead || target.OutcomeUnknown || Workspace?.SelectedThread is not { IsUnread: true }) {
            return;
        }

        target.IsMarkingRead = true;
        target.Message = null;
        changed();
        Result result;
        try {
            result = await owner.MarkThreadAsReadAsync(target.ThreadId);
        } catch (Exception exception) {
            logger.LogWarning(exception, "Collaboration mark-read outcome unknown for thread {ThreadId}.", target.ThreadId);
            target.IsMarkingRead = false;
            if (IsCurrentTarget(target)) {
                target.OutcomeUnknown = true;
                target.Message = "Unread update outcome unknown. Refresh to inspect persisted state.";
                changed();
            }
            return;
        }

        if (!IsCurrentTarget(target)) {
            target.IsMarkingRead = false;
            return;
        }

        try {
            target.Message = result.IsSuccess ? "Selected thread marked as read." : FailureMessage(result, "Unable to update unread state.");
            notify(target.Message, result.IsFailure);
            if (result.IsSuccess) {
                await ReadAsync();
                if (IsCurrentTarget(target) && ReadState is CollaborationReadState.Failed or CollaborationReadState.Stale) {
                    target.Message = "Unread state saved. Refresh failed; retry the read.";
                }
            }
        } finally {
            target.IsMarkingRead = false;
            if (IsCurrentTarget(target)) {
                changed();
            }
        }
    }

    public void ClearReply(CollaborationTarget target, CollaborationDraft<CollaborationReplyEditorModel> origin) {
        if (IsCurrentTarget(target) && ReferenceEquals(Reply, origin) && !origin.IsSaving) {
            if (origin.OutcomeUnknown) {
                Reply = new(new() { MessageKind = origin.Model.MessageKind });
            } else {
                origin.Model.MessageBody = string.Empty;
                origin.Context.MarkAsUnmodified();
            }
            changed();
        }
    }

    public void OpenContext(CollaborationTarget target) {
        if (!IsCurrentTarget(target) || Workspace?.SelectedThread?.ContextRoute is not { } route) {
            return;
        }

        if (IsSafeContextRoute(route)) {
            navigate(route.Trim());
        } else {
            target.Message = "The linked context must be a local application route.";
            notify(target.Message, true);
            changed();
        }
    }

    public static bool IsSafeContextRoute(string route) {
        var value = route.Trim();
        return value.StartsWith('/') && !value.StartsWith("//", StringComparison.Ordinal)
            && !value.Contains('\\') && !value.Any(char.IsControl);
    }

    public void OpenScheduler() {
        if (!disposed) {
            navigate("/scheduler");
        }
    }

    private void HandleUnknown<T>(CollaborationDraft<T> origin, Exception exception, string action, Guid? target) where T : class {
        logger.LogWarning(exception, "Collaboration {Action} outcome unknown for thread {ThreadId}.", action, target);
        if (disposed) {
            return;
        }

        origin.Complete(unknown: true);
        origin.Message = "Save outcome unknown. Refresh and inspect Threads before explicitly starting a new draft. This submission cannot be replayed.";
        changed();
    }

    private void Reject<T>(CollaborationDraft<T> origin, Result result, string fallback) where T : class {
        origin.Message = FailureMessage(result, fallback);
        notify(origin.Message, true);
        changed();
    }

    private static string FailureMessage(Result result, string fallback) => result.Errors.FirstOrDefault()?.Message ?? fallback;
    private bool IsCurrentRead(long generation) => !disposed && generation == readGeneration;
    private bool IsCurrentTarget(CollaborationTarget target) => !disposed && ReferenceEquals(Target, target);

    private void PublishSelection() {
        lastRoute = Target?.ThreadId;
        routeDelivered = true;
        selectionChanged(lastRoute);
    }

    public void Dispose() {
        disposed = true;
        ++readGeneration;
        readCancellation?.Cancel();
        readCancellation = null;
    }

    private enum SelectionKind {
        Initial,
        Explicit,
        Empty
    }
}
