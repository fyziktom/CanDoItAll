using Bunit;
using CanDoItAll.Modules.Projects.Pages;

namespace CanDoItAll.Tests.Components.ProjectStructure;

internal static class ProjectsPageTestActions {
    internal static async Task CloseEditorAsync(IRenderedComponent<ProjectsPage> page) {
        var close = page.InvokeAsync(() => page.Find("[data-testid='project-modal-close-button']").ClickAsync(new()));
        page.WaitForState(() => close.IsCompleted || page.FindAll("[data-testid='project-dirty-discard']").Count > 0);
        if (!close.IsCompleted) {
            await page.InvokeAsync(() => page.Find("[data-testid='project-dirty-discard']").ClickAsync(new()));
        }
        await close;
    }
}
