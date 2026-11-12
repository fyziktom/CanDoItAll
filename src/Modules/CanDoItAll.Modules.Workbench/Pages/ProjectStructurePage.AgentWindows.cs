using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Conversations.Shell;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Modules.Workbench.Pages;

public partial class ProjectStructurePage {
    [Inject] private IFloatingAgentChatCoordinator FloatingAgentChats { get; set; } = default!;

    private Task ToggleAgentWindowAsync() {
        if (ConversationShellCoordinator.Snapshot().IsCatalogVisible) {
            ConversationShellCoordinator.HideCatalog();
        } else {
            ConversationShellCoordinator.ShowCatalog(ConversationCatalogKindFilter.Agents);
        }

        return Task.CompletedTask;
    }

    private bool CanAutoRefreshAgentChange(ProjectStructureChange change) =>
        change.ChatOrigin is { } chat && chat.ProjectId == ProjectId &&
        surface is { } current && current.ProjectId == ProjectId && !deferredCompletionCts.IsCancellationRequested &&
        composerOpening is null && quickActionDialog is null && imageOpening is null && textAssetOpening is null &&
        projectCreateOpening is null && projectCreateParentOpening is null && hierarchyOpening is null && blockOpening is null && transferOpening is null &&
        processLinkDialog is null && processStartDialog is null && workflowAddOpening is null && workflowStartOpening is null &&
        authoringOperations.Count == 0 && !TaskEditingSessions.HasOpenEditor(current.ExpectedProjectAdmission) &&
        FloatingAgentChats.Snapshot().VisibleChat?.ChatSessionId == chat.ChatSessionId;

    private void HandleConversationShellChanged(object? sender, EventArgs eventArgs)
        => _ = InvokeAsync(StateHasChanged);

    private void DisposeAgentWindowState()
        => ConversationShellCoordinator.Changed -= HandleConversationShellChanged;
}
