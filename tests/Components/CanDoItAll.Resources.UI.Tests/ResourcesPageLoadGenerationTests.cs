using CanDoItAll.Modules.Resources;
using CanDoItAll.Resources.UiSandbox;

namespace CanDoItAll.Tests.Components.ResourcesUi;

public sealed class ResourcesPageLoadGenerationTests {
    [Fact]
    public async Task Older_save_refresh_cannot_replace_a_newer_editor_selection() {
        var store = new ResourceScenarioStore();
        using var controller = await ResourceRegistryTests.OpenAsync(store);
        var gate = store.HoldNext(ResourceScenarioLane.EditorRead);
        var save = controller.SaveAsync();
        await gate.Entered.Task;
        await controller.SelectAsync(store.Records.Keys.Last());
        var selected = controller.Draft;
        selected.Editor.Name = "newer selection";
        gate.Release();
        await save;
        Assert.Same(selected, controller.Draft);
        Assert.Equal("newer selection", selected.Editor.Name);
    }

    [Fact]
    public async Task A_new_route_invalidates_every_older_editor_load() {
        var store = new ResourceScenarioStore();
        using var controller = new ResourceRegistryController(store);
        var gate = store.HoldNext(ResourceScenarioLane.EditorRead);
        var first = controller.LoadRouteAsync(store.Records.Keys.First(), null);
        await gate.Entered.Task;
        await controller.LoadRouteAsync(store.Records.Keys.Last(), null);
        var selected = controller.Draft;
        gate.Release();
        await first;
        Assert.Same(selected, controller.Draft);
        Assert.Equal(store.Records.Keys.Last(), selected.Editor.Id);
    }
}
