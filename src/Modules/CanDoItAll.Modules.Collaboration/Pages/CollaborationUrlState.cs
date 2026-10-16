using CanDoItAll.AppComponents.Navigation;
using CanDoItAll.Collaboration.UI;

namespace CanDoItAll.Modules.Collaboration.Pages;

public sealed record CollaborationUrlState(CollaborationSection? View = null, bool UnreadOnly = false, Guid? ThreadId = null) {
    public CollaborationSection Section => View ?? CollaborationSection.Inbox;
}

public sealed class CollaborationUrlCodec : IUrlStateCodec<CollaborationUrlState> {
    public const string ViewKey = "view";
    public const string UnreadKey = "unread";
    public const string ThreadKey = "threadId";

    public static CollaborationUrlCodec Instance { get; } = new();

    public static UrlTokenMap<CollaborationSection> Views { get; } = new(
        (CollaborationSection.Inbox, "inbox"),
        (CollaborationSection.Threads, "threads"),
        (CollaborationSection.Escalations, "escalations"));

    public IReadOnlyList<string> Keys { get; } = [ViewKey, UnreadKey, ThreadKey];

    public CollaborationUrlState Read(UrlQuery query) {
        var threadId = query.GetId(ThreadKey);
        var view = query.GetToken(ViewKey, Views);
        return new(threadId is null ? view ?? CollaborationSection.Inbox : view, query.GetFlag(UnreadKey), threadId);
    }

    public void Write(CollaborationUrlState state, UrlQueryWriter query) {
        if (state.View is { } view && (view != CollaborationSection.Inbox || state.ThreadId is not null)) {
            query.Token(ViewKey, view, Views);
        }
        query.Flag(UnreadKey, state.UnreadOnly);
        query.Id(ThreadKey, state.ThreadId);
    }
}
