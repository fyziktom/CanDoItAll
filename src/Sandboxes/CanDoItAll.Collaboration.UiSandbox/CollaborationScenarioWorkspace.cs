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
    DelayedTarget
}

public sealed class CollaborationScenarioWorkspace : ICollaborationWorkspaceView, IDisposable {
    private static readonly DateTimeOffset Epoch = new(2026, 9, 28, 10, 0, 0, TimeSpan.Zero);
    private readonly CollaborationScenario scenario;
    private readonly Action changed;
    private readonly List<CollaborationThreadDetailModel> threads = [];
    private readonly List<TaskCompletionSource> pending = [];
    private bool disposed;
    private int nextId = 100;
    private int selectionGeneration;

    public CollaborationWorkspaceModel? Workspace { get; private set; }
    public CollaborationSection Section { get; private set; }
    public CollaborationReadState ReadState { get; private set; }
    public bool UnreadOnly { get; private set; }
    public bool IsMissing => Target is not null && Workspace is { SelectedThread: null };
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

    public Task RefreshAsync() {
        ReadState = CollaborationReadState.Ready;
        ReadError = null;
        Rebuild();
        changed();
        return Task.CompletedTask;
    }

    public async Task SelectAsync(Guid threadId) {
        if (disposed || Target?.ThreadId == threadId) {
            return;
        }
        var generation = ++selectionGeneration;
        Target = new(threadId);
        Reply = new(new());
        if (scenario == CollaborationScenario.DelayedTarget) {
            Workspace = null;
            ReadState = CollaborationReadState.Loading;
            changed();
            await WaitAsync();
            if (disposed || generation != selectionGeneration) {
                return;
            }
        }
        ReadState = CollaborationReadState.Ready;
        Rebuild();
        changed();
    }

    public async Task SetSectionAsync(CollaborationSection section) {
        Section = section;
        await AlignAsync();
    }

    public async Task SetUnreadOnlyAsync(bool unreadOnly) {
        UnreadOnly = unreadOnly;
        await AlignAsync();
    }

    private async Task AlignAsync() {
        var visible = threads.Where(item => Section == CollaborationSection.Threads ||
            (!UnreadOnly || item.IsUnread) && (Section != CollaborationSection.Escalations || item.ItemKind == CollaborationInboxItemKind.Escalation)).ToArray();
        if (!visible.Any(item => item.ThreadId == Target?.ThreadId)) {
            if (visible.FirstOrDefault() is { } next) {
                await SelectAsync(next.ThreadId);
            } else {
                Target = null;
                Reply = new(new());
                Rebuild();
            }
        }
        changed();
    }

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
            Target = new(thread.ThreadId);
            Reply = new(new());
            Section = submission.ItemKind == CollaborationInboxItemKind.Escalation ? CollaborationSection.Escalations : CollaborationSection.Inbox;
        }
        CompleteWrite();
    }

    public async Task ReplyAsync(CollaborationTarget target, CollaborationDraft<CollaborationReplyEditorModel> origin) {
        if (!Current(target) || !ReferenceEquals(origin, Reply) || !origin.TryBegin()) {
            return;
        }
        var body = origin.Model.MessageBody;
        var kind = origin.Model.MessageKind;
        changed();
        if (scenario == CollaborationScenario.AdmittedSave) {
            await WaitAsync();
        }
        origin.Complete();
        if (!Current(target)) {
            return;
        }
        if (scenario == CollaborationScenario.OwnerRefusal) {
            origin.Message = "Scenario owner refused the reply. Your draft is preserved.";
            changed();
            return;
        }
        var index = threads.FindIndex(item => item.ThreadId == target.ThreadId);
        threads[index] = threads[index] with {
            Messages = [..threads[index].Messages, Message(nextId++, body) with { MessageKind = kind, RaisesEscalation = kind == CollaborationMessageKind.Escalation }],
            IsUnread = false,
            UnreadCount = 0,
            ItemKind = kind == CollaborationMessageKind.Escalation ? CollaborationInboxItemKind.Escalation : threads[index].ItemKind
        };
        Reply = new(new()) { Message = "Reply recorded on the selected thread." };
        CompleteWrite();
        if (ReadState != CollaborationReadState.Stale) {
            await AlignAsync();
        }
    }

    public async Task MarkReadAsync(CollaborationTarget target) {
        if (!Current(target) || target.IsMarkingRead) {
            return;
        }
        var index = threads.FindIndex(item => item.ThreadId == target.ThreadId);
        if (index < 0) {
            return;
        }
        threads[index] = threads[index] with { IsUnread = false, UnreadCount = 0 };
        CompleteWrite();
        if (ReadState != CollaborationReadState.Stale) {
            await AlignAsync();
        }
    }

    private void CompleteWrite() {
        if (scenario == CollaborationScenario.SavedWithRefreshWarning) {
            if (Workspace?.SelectedThread?.ThreadId != Target?.ThreadId) {
                Workspace = null;
            }
            ReadState = Workspace is null ? CollaborationReadState.Failed : CollaborationReadState.Stale;
            ReadError = "Saved successfully. Refresh failed; retry the read, not the write.";
        } else {
            Rebuild();
        }
        changed();
    }

    public void ClearReply(CollaborationTarget target, CollaborationDraft<CollaborationReplyEditorModel> origin) {
        if (Current(target) && ReferenceEquals(origin, Reply) && !origin.IsLocked) {
            origin.Model.MessageBody = string.Empty;
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
        NavigationMessage = "Scheduler navigation intent received.";
        changed();
    }

    public void CompletePending() {
        foreach (var completion in pending) {
            completion.TrySetResult();
        }
        pending.Clear();
    }

    private Task WaitAsync() {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        pending.Add(completion);
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
        CompletePending();
    }
}
