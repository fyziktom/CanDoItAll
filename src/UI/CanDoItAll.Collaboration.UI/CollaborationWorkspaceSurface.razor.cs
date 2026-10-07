using CanDoItAll.Modules.Collaboration;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Collaboration.UI;

public partial class CollaborationWorkspaceSurface {
    [Parameter, EditorRequired]
    public ICollaborationWorkspaceView View { get; set; } = default!;

    [Parameter]
    public bool Interactive { get; set; }

    private static readonly CollaborationWorkspaceModel EmptyWorkspace = new([], [], [], null, new(0, 0));
    private IReadOnlyList<CollaborationInboxItemSummary> VisibleInboxItems => (View.Workspace ?? EmptyWorkspace).InboxItems.Where(item => !View.UnreadOnly || item.IsUnread).ToArray();
    private IReadOnlyList<CollaborationInboxItemSummary> VisibleEscalations => (View.Workspace ?? EmptyWorkspace).Escalations.Where(item => !View.UnreadOnly || item.IsUnread).ToArray();
    private int SelectedIndex => View.Section switch {
        CollaborationSection.Inbox => 0,
        CollaborationSection.Threads => 1,
        CollaborationSection.Escalations => 2,
        _ => throw new ArgumentOutOfRangeException(nameof(View.Section))
    };

    private Task HandleSelectedViewChangedAsync(int index) => View.SetSectionAsync(index switch {
        0 => CollaborationSection.Inbox,
        1 => CollaborationSection.Threads,
        2 => CollaborationSection.Escalations,
        _ => throw new ArgumentOutOfRangeException(nameof(index))
    });

    private void HandleReplyInput(CollaborationDraft<CollaborationReplyEditorModel> origin, ChangeEventArgs args) {
        if (ReferenceEquals(View.Reply, origin) && !origin.IsLocked) {
            origin.Model.MessageBody = args.Value?.ToString() ?? string.Empty;
            origin.Context.NotifyFieldChanged(new(origin.Model, nameof(CollaborationReplyEditorModel.MessageBody)));
        }
    }

    private int ResolveCurrentListCount() {
        return SelectedIndex switch {
            2 => VisibleEscalations.Count,
            1 => (View.Workspace ?? EmptyWorkspace).Threads.Count,
            _ => VisibleInboxItems.Count
        };
    }

    private string ResolveListTitle() {
        return SelectedIndex switch {
            2 => "Escalations",
            1 => "Threads",
            _ => "Inbox"
        };
    }

    private string ResolveListDescription() {
        return SelectedIndex switch {
            2 => "Items that require explicit human attention or approval.",
            1 => "All canonical conversation threads, regardless of unread state.",
            _ => "Unread and recently updated collaboration items."
        };
    }

    private static string? ResolveBadgeText(int count) {
        return count > 0 ? count.ToString() : null;
    }

    private static string ResolveItemEyebrow(CollaborationInboxItemSummary item) {
        return item.ItemKind == CollaborationInboxItemKind.Escalation ? "Escalation" : "Notification";
    }

    private static string ResolveItemTone(CollaborationInboxItemKind itemKind) {
        return itemKind == CollaborationInboxItemKind.Escalation ? "warning" : "info";
    }
}
