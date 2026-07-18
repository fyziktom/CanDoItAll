using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Workbench.ProjectStructure;

namespace CanDoItAll.Modules.Workbench.Pages;

public partial class ProjectStructurePage
{
    protected override void OnInitialized()
    {
        FloatingAgentChatCoordinator.Changed += HandleFloatingAgentChatChanged;
    }

    private Task ToggleAgentWindowAsync()
    {
        if (FloatingAgentChatCoordinator.Snapshot().IsCatalogVisible)
        {
            FloatingAgentChatCoordinator.HideCatalog();
        }
        else
        {
            FloatingAgentChatCoordinator.ShowCatalog();
        }

        return Task.CompletedTask;
    }

    private async Task HandleAgentExecutionCompletedAsync(AgentChatExecutionCompleted notification)
    {
        if (!string.Equals(
                notification.Source.Kind.Value,
                ProjectStructureAgentChatContextBuilder.SourceKind,
                StringComparison.Ordinal) ||
            !Guid.TryParse(notification.Source.Id.Value, out var sourceProjectId) ||
            sourceProjectId != ProjectId)
        {
            return;
        }

        await ReloadSurfaceAsync();
        await InvokeAsync(StateHasChanged);
    }

    private void HandleFloatingAgentChatChanged(object? sender, EventArgs eventArgs)
        => _ = InvokeAsync(StateHasChanged);

    private void DisposeAgentWindowState()
        => FloatingAgentChatCoordinator.Changed -= HandleFloatingAgentChatChanged;
}
