using Bunit;
using CanDoItAll.Components.BaseLib;

namespace CanDoItAll.Tests.Components.ProjectStructure;

internal static class TaskDialogTestDriver {
    public static Task ClickAsync(IRenderedComponent<DialogHost> host, string id) =>
        host.InvokeAsync(() => host.Find($"[data-testid='{id}']").ClickAsync(new()));

    public static async Task CloseAsync(IRenderedComponent<DialogHost> host, DialogService dialogs, Task opening) {
        var closing = host.InvokeAsync(() => dialogs.CloseAllAsync());
        host.WaitForAssertion(() => Assert.True(closing.IsCompleted || host.FindAll("[data-testid='planning-task-dirty-discard']").Count == 1));
        if (!closing.IsCompleted) {
            await ClickAsync(host, "planning-task-dirty-discard");
        }
        await closing.WaitAsync(TimeSpan.FromSeconds(5));
        await opening.WaitAsync(TimeSpan.FromSeconds(5));
    }
}
