using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Modules.Collaboration.Pages;

public partial class CollaborationHomePage
{
    [SupplyParameterFromQuery(Name = "threadId")]
    public Guid? ThreadIdQuery { get; set; }

    private readonly CollaborationThreadEditorModel newThreadEditor = new();
    private readonly CollaborationReplyEditorModel replyEditor = new();
    private CollaborationWorkspaceModel workspace = new([], [], [], null, new CollaborationShellState(0, 0));
    private Guid? selectedThreadId;
    private int selectedViewIndex;
    private bool showUnreadOnly;
    private bool hasLoaded;
    private bool isError;
    private string? message;

    private IReadOnlyList<CollaborationInboxItemSummary> VisibleInboxItems => showUnreadOnly
        ? workspace.InboxItems.Where(item => item.IsUnread).ToArray()
        : workspace.InboxItems;

    private IReadOnlyList<CollaborationInboxItemSummary> VisibleEscalations => showUnreadOnly
        ? workspace.Escalations.Where(item => item.IsUnread).ToArray()
        : workspace.Escalations;

    protected override Task OnInitializedAsync()
    {
        PrepareNewThread(CollaborationInboxItemKind.Notification);
        ResetReply();
        return Task.CompletedTask;
    }

    protected override async Task OnParametersSetAsync()
    {
        if (!hasLoaded || ThreadIdQuery != selectedThreadId)
        {
            selectedThreadId = ThreadIdQuery;
            await LoadWorkspaceAsync();
            if (ThreadIdQuery.HasValue)
            {
                selectedViewIndex = ResolveViewIndexForThread(ThreadIdQuery.Value);
            }

            hasLoaded = true;
        }
    }

    private async Task HandleSelectedViewChangedAsync(int index)
    {
        selectedViewIndex = index;
        selectedThreadId = ResolveDefaultThreadIdForCurrentView();
        await LoadWorkspaceAsync();
        ReplaceCurrentRoute();
    }

    private void PrepareNewThread(CollaborationInboxItemKind itemKind)
    {
        newThreadEditor.Subject = string.Empty;
        newThreadEditor.ContextKind = CollaborationContextKind.Manual;
        newThreadEditor.ContextLabel = string.Empty;
        newThreadEditor.ContextRoute = null;
        newThreadEditor.ItemKind = itemKind;
        newThreadEditor.MessageBody = string.Empty;
    }

    private async Task CreateThreadAsync()
    {
        var itemKind = newThreadEditor.ItemKind;
        var result = await CollaborationService.CreateThreadAsync(CollaborationService.CreateManualThreadRequest(newThreadEditor));
        if (result.IsFailure)
        {
            SetMessage(result.Errors.FirstOrDefault()?.Message ?? "Unable to create the collaboration thread.", true);
            return;
        }

        PrepareNewThread(itemKind);
        selectedThreadId = result.Value;
        selectedViewIndex = itemKind == CollaborationInboxItemKind.Escalation ? 2 : 0;
        SetMessage("Collaboration thread created.", false);
        await LoadWorkspaceAsync();
        ReplaceCurrentRoute();
    }

    private async Task SelectThreadAsync(Guid threadId)
    {
        selectedThreadId = threadId;
        await LoadWorkspaceAsync();
        ReplaceCurrentRoute();
    }

    private async Task AddReplyAsync()
    {
        if (workspace.SelectedThread is null)
        {
            return;
        }

        var result = await CollaborationService.AppendMessageAsync(CollaborationService.CreateLocalReplyRequest(workspace.SelectedThread.ThreadId, replyEditor));
        if (result.IsFailure)
        {
            SetMessage(result.Errors.FirstOrDefault()?.Message ?? "Unable to append the collaboration message.", true);
            return;
        }

        ResetReply();
        selectedThreadId = workspace.SelectedThread.ThreadId;
        SetMessage("Reply recorded on the selected thread.", false);
        await LoadWorkspaceAsync();
    }

    private async Task MarkSelectedThreadAsReadAsync()
    {
        if (workspace.SelectedThread is null)
        {
            return;
        }

        var result = await CollaborationService.MarkThreadAsReadAsync(workspace.SelectedThread.ThreadId);
        if (result.IsFailure)
        {
            SetMessage(result.Errors.FirstOrDefault()?.Message ?? "Unable to update unread state.", true);
            return;
        }

        selectedThreadId = workspace.SelectedThread.ThreadId;
        SetMessage("Selected thread marked as read.", false);
        await LoadWorkspaceAsync();
    }

    private async Task ShowUnreadOnlyAsync()
    {
        showUnreadOnly = true;
        await RealignSelectionAsync();
    }

    private async Task ShowAllItemsAsync()
    {
        showUnreadOnly = false;
        await RealignSelectionAsync();
    }

    private async Task RealignSelectionAsync()
    {
        var visibleThreadIds = ResolveVisibleThreadIds().ToHashSet();
        if (selectedThreadId.HasValue && visibleThreadIds.Contains(selectedThreadId.Value))
        {
            await LoadWorkspaceAsync();
            return;
        }

        selectedThreadId = ResolveDefaultThreadIdForCurrentView();
        await LoadWorkspaceAsync();
        ReplaceCurrentRoute();
    }

    private async Task LoadWorkspaceAsync()
    {
        workspace = await CollaborationService.GetWorkspaceAsync(selectedThreadId);
        selectedThreadId = workspace.SelectedThread?.ThreadId;
    }

    private void OpenActivity()
    {
        Navigation.NavigateTo("/activity");
    }

    private void OpenContext()
    {
        if (workspace.SelectedThread is null || string.IsNullOrWhiteSpace(workspace.SelectedThread.ContextRoute))
        {
            return;
        }

        Navigation.NavigateTo(workspace.SelectedThread.ContextRoute);
    }

    private void ClearReply()
    {
        replyEditor.MessageBody = string.Empty;
    }

    private void ResetReply()
    {
        replyEditor.MessageBody = string.Empty;
        replyEditor.MessageKind = CollaborationMessageKind.Standard;
    }

    private void SetMessage(string value, bool error)
    {
        message = value;
        isError = error;
    }

    private void ReplaceCurrentRoute()
    {
        var route = selectedThreadId.HasValue
            ? $"/collaboration?threadId={selectedThreadId.Value:D}"
            : "/collaboration";
        Navigation.NavigateTo(route, replace: true);
    }

    private int ResolveViewIndexForThread(Guid threadId)
    {
        if (workspace.Escalations.Any(item => item.ThreadId == threadId))
        {
            return 2;
        }

        if (workspace.Threads.Any(item => item.ThreadId == threadId))
        {
            return 1;
        }

        return 0;
    }

    private Guid? ResolveDefaultThreadIdForCurrentView()
    {
        return selectedViewIndex switch
        {
            2 => VisibleEscalations.FirstOrDefault()?.ThreadId,
            1 => workspace.Threads.FirstOrDefault()?.ThreadId,
            _ => VisibleInboxItems.FirstOrDefault()?.ThreadId
        };
    }

    private IEnumerable<Guid> ResolveVisibleThreadIds()
    {
        return selectedViewIndex switch
        {
            2 => VisibleEscalations.Select(item => item.ThreadId),
            1 => workspace.Threads.Select(item => item.ThreadId),
            _ => VisibleInboxItems.Select(item => item.ThreadId)
        };
    }

    private int ResolveCurrentListCount()
    {
        return selectedViewIndex switch
        {
            2 => VisibleEscalations.Count,
            1 => workspace.Threads.Count,
            _ => VisibleInboxItems.Count
        };
    }

    private string ResolveListTitle()
    {
        return selectedViewIndex switch
        {
            2 => "Escalations",
            1 => "Threads",
            _ => "Inbox"
        };
    }

    private string ResolveListDescription()
    {
        return selectedViewIndex switch
        {
            2 => "Items that require explicit human attention or approval.",
            1 => "All canonical conversation threads, regardless of unread state.",
            _ => "Unread and recently updated collaboration items."
        };
    }

    private string ResolveMessageClasses()
    {
        return isError
            ? "rounded-[1.25rem] border border-rose-200 bg-rose-50 px-4 py-4 text-sm font-medium text-rose-700"
            : "rounded-[1.25rem] border border-emerald-200 bg-emerald-50 px-4 py-4 text-sm font-medium text-emerald-700";
    }

    private static string? ResolveBadgeText(int count)
    {
        return count > 0 ? count.ToString() : null;
    }

    private static string ResolveItemEyebrow(CollaborationInboxItemSummary item)
    {
        return item.ItemKind == CollaborationInboxItemKind.Escalation ? "Escalation" : "Notification";
    }

    private static string ResolveItemTone(CollaborationInboxItemKind itemKind)
    {
        return itemKind == CollaborationInboxItemKind.Escalation ? "warning" : "info";
    }
}
