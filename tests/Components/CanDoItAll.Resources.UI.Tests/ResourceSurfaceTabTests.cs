using Bunit;
using CanDoItAll.Resources.UI;
using CanDoItAll.Resources.UiSandbox;

namespace CanDoItAll.Tests.Components.ResourcesUi;

public sealed class ResourceSurfaceTabTests {
    [Fact]
    public async Task The_host_owned_tab_selects_the_panel_and_a_tab_click_raises_the_index() {
        var store = new ResourceScenarioStore();
        using var registry = await ResourceRegistryTests.OpenAsync(store);
        using var context = ResourceSurfaceTests.Context();
        await using var browse = await ResourceBrowseTests.OpenAsync(store, context);
        var raised = new List<int>();
        var cut = context.Render<ResourcesWorkspaceSurface>(parameters => parameters
            .Add(surface => surface.Registry, registry)
            .Add(surface => surface.Browse, browse)
            .Add(surface => surface.SelectedTab, 1)
            .Add(surface => surface.TabChanged, index => raised.Add(index)));

        Assert.Equal("true", cut.Find("[data-testid='resources-tab-browse']").GetAttribute("aria-selected"));
        Assert.Empty(cut.FindAll("[data-testid='resource-name-input']"));

        cut.Find("[data-testid='resources-tab-registry']").Click();

        Assert.Equal([0], raised);
        Assert.Equal("true", cut.Find("[data-testid='resources-tab-registry']").GetAttribute("aria-selected"));
        cut.Render(parameters => parameters.Add(surface => surface.SelectedTab, 0));
        Assert.NotNull(cut.Find("[data-testid='resource-name-input']"));

        cut.Render(parameters => parameters.Add(surface => surface.SelectedTab, 1));

        Assert.Equal("true", cut.Find("[data-testid='resources-tab-browse']").GetAttribute("aria-selected"));
        Assert.Equal([0], raised);
    }
}
