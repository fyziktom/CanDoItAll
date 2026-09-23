using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Conversations.Shell;
using CanDoItAll.Modules.Workbench.ProjectStructure;

namespace CanDoItAll.Modules.Workbench.Pages;

public partial class ProjectStructurePage {
    protected override void OnInitialized() {
        ConversationShellCoordinator.Changed += HandleConversationShellChanged;
    }

    private Task ToggleAgentWindowAsync() {
        if (ConversationShellCoordinator.Snapshot().IsCatalogVisible) {
            ConversationShellCoordinator.HideCatalog();
        } else {
            ConversationShellCoordinator.ShowCatalog(ConversationCatalogKindFilter.Agents);
        }

        return Task.CompletedTask;
    }

    private async Task HandleAgentExecutionCompletedAsync(AgentChatExecutionCompleted notification) {
        if (!string.Equals(
                notification.Source.Kind.Value,
                ProjectStructureAgentChatContextBuilder.SourceKind,
                StringComparison.Ordinal) ||
            !Guid.TryParse(notification.Source.Id.Value, out var sourceProjectId) ||
            sourceProjectId != ProjectId) {
            return;
        }

        await ReloadSurfaceAsync();
        await InvokeAsync(StateHasChanged);
    }

    private void HandleConversationShellChanged(object? sender, EventArgs eventArgs)
        => _ = InvokeAsync(StateHasChanged);

    private void DisposeAgentWindowState()
        => ConversationShellCoordinator.Changed -= HandleConversationShellChanged;
}
