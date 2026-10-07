using CanDoItAll.Collaboration.UI;
using CanDoItAll.Modules.Collaboration;

namespace CanDoItAll.Collaboration.UiSandbox;

public enum CollaborationScenario {
    Representative,
    Loading,
    Empty,
    MissingThread,
    LongTranscript,
    InvalidDraft,
    DirtyDraft,
    AdmittedSave,
    OwnerRefusal,
    FailedLoad,
    StaleRefresh,
    SavedWithRefreshWarning,
    DelayedTarget,
    DelayedReconciliation
}

public sealed class CollaborationScenarioWorkspace : ICollaborationWorkspaceView, IDisposable {
    private static readonly DateTimeOffset Epoch = new(2026, 9, 28, 10, 0, 0, TimeSpan.Zero);
    private readonly CollaborationScenario scenario;
    private readonly Action changed;
    private readonly List<CollaborationThreadDetailModel> threads = [];
    private readonly Queue<TaskCompletionSource> pending = [];
    private bool disposed;
    private int nextId = 100;
    private int selectionGeneration;
    private int readGeneration;

    public CollaborationWorkspaceModel? Workspace { get; private set; }
    public CollaborationSection Section { get; private set; }
    public CollaborationReadState ReadState { get; private set; }
    public bool UnreadOnly { get; private set; }
    public bool IsMissing => Target is not null && Workspace is { SelectedThread: null };
    public bool IsReplyRetained => Workspace?.SelectedThread is { } selected && Target?.ThreadId == selected.ThreadId
        && !VisibleIds(Workspace).Contains(selected.ThreadId) && CollaborationReplyPolicy.MustRetain(Reply);
    public string? ReadError { get; private set; }
    public CollaborationDraft<CollaborationThreadEditorModel> NewThread { get; private set; } = new(new());
    public CollaborationDraft<CollaborationReplyEditorModel> Reply { get; private set; } = new(new());
    public CollaborationTarget? Target { get; private set; }
    public string? NavigationMessage { get; private set; }

    public CollaborationScenarioWorkspace(CollaborationScenario scenario, Action changed) {
        this.scenario = scenario;
        this.changed = changed;
        if (scenario != CollaborationScenario.Empty) {
            threads.Add(Thread(1, "Release readiness", CollaborationInboxItemKind.Notification, true));
            threads.Add(Thread(2, "Approval needed", CollaborationInboxItemKind.Escalation, true));
            threads.Add(Thread(3, "System checkpoint", CollaborationInboxItemKind.Notification, false));
        }

        if (scenario == CollaborationScenario.LongTranscript) {
            threads[0] = threads[0] with {
                Subject = string.Join(" ", Enumerable.Repeat("A long but readable collaboration subject", 5)),
                Messages = Enumerable.Range(1, 60).Select(index => Message(index, $"Message {index}: " + string.Join(" ", Enumerable.Repeat("Transcript text with escaped <script> markup.", 12)))).ToArray()
            };
        }

        Target = scenario == CollaborationScenario.MissingThread ? new(Id(999)) : threads.FirstOrDefault() is { } first ? new(first.ThreadId) : null;
        ReadState = CollaborationReadState.Ready;
        Rebuild();
        if (scenario is CollaborationScenario.Loading or CollaborationScenario.FailedLoad) {
            Workspace = null;
            ReadState = scenario == CollaborationScenario.Loading ? CollaborationReadState.Loading : CollaborationReadState.Failed;
            ReadError = scenario == CollaborationScenario.FailedLoad ? "Scenario load failed. Refresh to recover." : null;
        }
        if (scenario == CollaborationScenario.StaleRefresh) {
            ReadState = CollaborationReadState.Stale;
            ReadError = "Refresh failed. Showing the last accepted snapshot. Retry the read.";
        }
        if (scenario is CollaborationScenario.DirtyDraft or CollaborationScenario.AdmittedSave or CollaborationScenario.OwnerRefusal or CollaborationScenario.SavedWithRefreshWarning) {
            NewThread.Model.Subject = "Draft collaboration item";
            NewThread.Model.MessageBody = "Unsent scenario text.";
            Reply.Model.MessageBody = "Scenario reply.";
        }
    }

    public Task RefreshAsync() => ReadAsync(scenario == CollaborationScenario.DelayedReconciliation);

    private async Task ReadAsync(bool delayed) {
        if (disposed) {
            return;
        }
        var generation = ++readGeneration;
        ReadState = Workspace is null ? CollaborationReadState.Loading : CollaborationReadState.Refreshing;
        ReadError = null;
        changed();
        if (delayed) {
            await WaitAsync();
        }
        if (disposed || generation != readGeneration) {
            return;
        }
        ReadState = CollaborationReadState.Ready;
        Rebuild();
        if (!IsMissing) {
            await AlignAsync();
        }
        if (!disposed && generation == readGeneration) {
            changed();
        }
    }

    public async Task SelectAsync(Guid threadId) {
        if (disposed || Target?.ThreadId == threadId) {
            return;
        }
        ++selectionGeneration;
        Target = new(threadId);
        Reply = new(new());
        Workspace = null;
        await ReadAsync(scenario == CollaborationScenario.DelayedTarget);
    }

    public async Task SetSectionAsync(CollaborationSection section) {
        if (disposed || Section == section) {
            return;
        }
        Section = section;
        ++selectionGeneration;
        await AlignAsync();
    }

    public async Task SetUnreadOnlyAsync(bool unreadOnly) {
        if (disposed || UnreadOnly == unreadOnly) {
            return;
        }
        UnreadOnly = unreadOnly;
        ++selectionGeneration;
        await AlignAsync();
    }

    private async Task AlignAsync() {
        if (Workspace is null || IsReplyRetained) {
            changed();
            return;
        }
        var visible = VisibleIds(Workspace).ToArray();
        if (Target is null || !visible.Contains(Target.ThreadId)) {
            if (visible.Length > 0) {
                await SelectAsync(visible[0]);
            } else {
                ++selectionGeneration;
                ++readGeneration;
                Target = null;
                Reply = new(new());
                Workspace = Workspace with { SelectedThread = null };
            }
        }
        if (!disposed) {
            changed();
        }
    }

    private IEnumerable<Guid> VisibleIds(CollaborationWorkspaceModel workspace) => Section switch {
        CollaborationSection.Threads => workspace.Threads.Select(item => item.ThreadId),
        CollaborationSection.Escalations => workspace.Escalations.Where(item => !UnreadOnly || item.IsUnread).Select(item => item.ThreadId),
        _ => workspace.InboxItems.Where(item => !UnreadOnly || item.IsUnread).Select(item => item.ThreadId)
    };

    public void PrepareThread(CollaborationDraft<CollaborationThreadEditorModel> origin, CollaborationInboxItemKind kind) {
        if (!disposed && ReferenceEquals(origin, NewThread) && !origin.IsSaving) {
            NewThread = new(new() { ItemKind = kind });
            changed();
        }
    }

    public async Task CreateAsync(CollaborationDraft<CollaborationThreadEditorModel> origin) {
        if (disposed || !ReferenceEquals(origin, NewThread) || !origin.TryBegin()) {
            return;
        }
        var submission = new { origin.Model.Subject, origin.Model.MessageBody, origin.Model.ContextLabel, origin.Model.ContextRoute, origin.Model.ContextKind, origin.Model.ItemKind };
        var generation = selectionGeneration;
        changed();
        if (scenario == CollaborationScenario.AdmittedSave) {
            await WaitAsync();
        }
        origin.Complete();
        if (disposed) {
            return;
        }
        if (scenario == CollaborationScenario.OwnerRefusal) {
            origin.Message = "Scenario owner refused the submission. Your draft is preserved.";
            changed();
            return;
        }
        var thread = Thread(nextId++, submission.Subject, submission.ItemKind, true) with {
            ContextKind = submission.ContextKind,
            ContextLabel = submission.ContextLabel,
            ContextRoute = submission.ContextRoute,
            Messages = [Message(nextId++, submission.MessageBody)]
        };
        threads.Insert(0, thread);
        NewThread = new(new() { ItemKind = submission.ItemKind }) { SavedThreadId = thread.ThreadId, Message = "Collaboration thread created." };
        if (generation == selectionGeneration) {
            ++selectionGeneration;
            Target = new(thread.ThreadId);
            Reply = new(new());
            Workspace = null;
            Section = submission.ItemKind == CollaborationInboxItemKind.Escalation ? CollaborationSection.Escalations : CollaborationSection.Inbox;
            await CompleteWriteAsync();
        } else {
            changed();
        }
    }

    public async Task ReplyAsync(CollaborationTarget target, CollaborationDraft<CollaborationReplyEditorModel> origin) {
        if (!Current(target) || !ReferenceEquals(origin, Reply) || Workspace?.SelectedThread is null || !origin.TryBegin()) {
            return;
        }
        var body = origin.Model.MessageBody;
        var kind = origin.Model.MessageKind;
        changed();
        if (scenario == CollaborationScenario.AdmittedSave) {
            await WaitAsync();
        }
        origin.Complete();
        if (disposed) {
            return;
        }
        if (scenario == CollaborationScenario.OwnerRefusal) {
            if (Current(target) && ReferenceEquals(origin, Reply)) {
                origin.Message = "Scenario owner refused the reply. Your draft is preserved.";
                changed();
            }
            return;
        }
        var index = threads.FindIndex(item => item.ThreadId == target.ThreadId);
        threads[index] = threads[index] with {
            Messages = [..threads[index].Messages, Message(nextId++, body) with { MessageKind = kind, RaisesEscalation = kind == CollaborationMessageKind.Escalation }],
            IsUnread = false,
            UnreadCount = 0,
            ItemKind = kind == CollaborationMessageKind.Escalation ? CollaborationInboxItemKind.Escalation : threads[index].ItemKind
        };
        if (!Current(target) || !ReferenceEquals(origin, Reply)) {
            return;
        }
        Reply = new(new()) { Message = "Reply recorded on the selected thread." };
        await CompleteWriteAsync();
    }

    public async Task MarkReadAsync(CollaborationTarget target) {
        if (!Current(target) || target.IsMarkingRead || Workspace?.SelectedThread is not { IsUnread: true }) {
            return;
        }
        var index = threads.FindIndex(item => item.ThreadId == target.ThreadId);
        if (index < 0) {
            return;
        }
        target.IsMarkingRead = true;
        threads[index] = threads[index] with { IsUnread = false, UnreadCount = 0 };
        try {
            await CompleteWriteAsync();
        } finally {
            target.IsMarkingRead = false;
            if (Current(target)) {
                changed();
            }
        }
    }

    private async Task CompleteWriteAsync() {
        if (scenario == CollaborationScenario.SavedWithRefreshWarning) {
            if (Workspace?.SelectedThread?.ThreadId != Target?.ThreadId) {
                Workspace = null;
            }
            ReadState = Workspace is null ? CollaborationReadState.Failed : CollaborationReadState.Stale;
            ReadError = "Saved successfully. Refresh failed; retry the read, not the write.";
            changed();
        } else {
            await RefreshAsync();
        }
    }

    public void ClearReply(CollaborationTarget target, CollaborationDraft<CollaborationReplyEditorModel> origin) {
        if (Current(target) && ReferenceEquals(origin, Reply) && !origin.IsLocked) {
            origin.Model.MessageBody = string.Empty;
            origin.Context.MarkAsUnmodified();
            changed();
        }
    }

    public void OpenContext(CollaborationTarget target) {
        if (Current(target)) {
            NavigationMessage = $"Linked context: {Workspace?.SelectedThread?.ContextRoute}";
            changed();
        }
    }

    public void OpenScheduler() {
        if (!disposed) {
            NavigationMessage = "Scheduler navigation intent received.";
            changed();
        }
    }

    public void CompletePending() {
        if (pending.TryDequeue(out var completion)) {
            completion.TrySetResult();
        }
    }

    private Task WaitAsync() {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        pending.Enqueue(completion);
        return completion.Task;
    }

    private bool Current(CollaborationTarget target) => !disposed && ReferenceEquals(Target, target);

    private void Rebuild() {
        var inbox = threads.Select(item => new CollaborationInboxItemSummary(item.ThreadId, item.ThreadId, item.ItemKind, item.Subject, item.Messages.LastOrDefault()?.Body ?? string.Empty, $"/collaboration?threadId={item.ThreadId:D}", item.IsUnread, item.UnreadCount, item.LastActivityAtUtc)).ToArray();
        Workspace = new(inbox, threads.Select(item => new CollaborationThreadSummary(item.ThreadId, item.Subject, item.ContextKind, item.ContextLabel, item.ItemKind, item.State, item.LastActivityAtUtc, item.Messages.Count)).ToArray(),
            inbox.Where(item => item.ItemKind == CollaborationInboxItemKind.Escalation).ToArray(), threads.Find(item => item.ThreadId == Target?.ThreadId),
            new(inbox.Sum(item => item.UnreadCount), inbox.Count(item => item.IsUnread && item.ItemKind == CollaborationInboxItemKind.Escalation)));
    }

    private static Guid Id(int value) => new(value, 0, 0, new byte[8]);
    private static CollaborationMessageSummary Message(int id, string body) => new(Id(id), CollaborationMessageKind.Standard, CollaborationMessageAuthorKind.User, "Local operator", body, false, Epoch.AddMinutes(id));
    private static CollaborationThreadDetailModel Thread(int id, string subject, CollaborationInboxItemKind kind, bool unread) => new(Id(id), subject, CollaborationContextKind.Manual, null, null, "Release planning", "/scheduler", kind, CollaborationThreadState.Open, unread, unread ? 1 : 0, Epoch, Epoch.AddMinutes(id),
        [new(Id(id), "local-user", "Local operator", CollaborationParticipantKind.User, "")],
        [Message(id, $"Deterministic message for {subject}.") with { MessageKind = id == 3 ? CollaborationMessageKind.System : kind == CollaborationInboxItemKind.Escalation ? CollaborationMessageKind.Escalation : CollaborationMessageKind.Standard, RaisesEscalation = kind == CollaborationInboxItemKind.Escalation }]);

    public void Dispose() {
        disposed = true;
        ++readGeneration;
        while (pending.TryDequeue(out var completion)) {
            completion.TrySetResult();
        }
    }
}
