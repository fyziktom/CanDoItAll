using CanDoItAll.Modules.Collaboration;

namespace CanDoItAll.Collaboration.UI;

public enum CollaborationSection {
    Inbox,
    Threads,
    Escalations
}

public enum CollaborationReadState {
    Loading,
    Ready,
    Refreshing,
    Failed,
    Stale
}

public interface ICollaborationWorkspaceView {
    CollaborationWorkspaceModel? Workspace { get; }
    CollaborationSection Section { get; }
    CollaborationReadState ReadState { get; }
    bool UnreadOnly { get; }
    bool IsMissing { get; }
    string? ReadError { get; }
    CollaborationDraft<CollaborationThreadEditorModel> NewThread { get; }
    CollaborationDraft<CollaborationReplyEditorModel> Reply { get; }
    CollaborationTarget? Target { get; }
    Task RefreshAsync();
    Task SelectAsync(Guid threadId);
    Task SetSectionAsync(CollaborationSection section);
    Task SetUnreadOnlyAsync(bool unreadOnly);
    void PrepareThread(CollaborationDraft<CollaborationThreadEditorModel> origin, CollaborationInboxItemKind kind);
    Task CreateAsync(CollaborationDraft<CollaborationThreadEditorModel> origin);
    Task ReplyAsync(CollaborationTarget target, CollaborationDraft<CollaborationReplyEditorModel> origin);
    Task MarkReadAsync(CollaborationTarget target);
    void ClearReply(CollaborationTarget target, CollaborationDraft<CollaborationReplyEditorModel> origin);
    void OpenContext(CollaborationTarget target);
    void OpenScheduler();
}
