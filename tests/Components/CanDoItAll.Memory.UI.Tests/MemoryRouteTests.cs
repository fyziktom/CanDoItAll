using Bunit;
using CanDoItAll.AppComponents.Navigation;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Memory.UI;
using CanDoItAll.Memory.UiSandbox;
using CanDoItAll.Modules.Memory.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.MemoryUi;

public sealed class MemoryRouteTests {
    [Fact]
    public async Task The_owner_default_is_replaced_and_tab_and_provider_intents_push_once() {
        using var workspace = new MemoryProvidersPageController(new MemoryScenarioStore());
        var routes = Observe(workspace);

        await workspace.OpenAsync(new());
        workspace.ActiveTabIndex = 4;
        workspace.ActiveTabIndex = 4;
        await workspace.SelectProviderAsync(MemoryScenarioStore.ProviderB);
        var draft = workspace.Draft;
        workspace.Editor.DisplayName = "Unsaved B draft";
        await workspace.SelectProviderAsync(MemoryScenarioStore.ProviderB);

        Assert.Same(draft, workspace.Draft);
        Assert.Equal("Unsaved B draft", workspace.Editor.DisplayName);
        Assert.Equal([
            (new MemoryUrlState(MemoryScenarioStore.ProviderA), UrlHistoryMode.Replace),
            (new MemoryUrlState(MemoryScenarioStore.ProviderA, MemoryWorkspaceTab.Query), UrlHistoryMode.Push),
            (new MemoryUrlState(MemoryScenarioStore.ProviderB, MemoryWorkspaceTab.Query), UrlHistoryMode.Push)
        ], routes);
    }

    [Fact]
    public async Task An_explicit_provider_and_tab_win_over_the_owner_default_without_publishing() {
        using var workspace = new MemoryProvidersPageController(new MemoryScenarioStore());
        var routes = Observe(workspace);

        await workspace.OpenAsync(new(MemoryScenarioStore.ProviderB, MemoryWorkspaceTab.Events));

        Assert.Equal(MemoryScenarioStore.ProviderB, workspace.SelectedProviderId);
        Assert.Equal("Engineering memory", workspace.Editor.DisplayName);
        Assert.Equal(2, workspace.ActiveTabIndex);
        Assert.Equal(MemoryWorkspaceTab.Events, workspace.Tab);
        Assert.Empty(routes);
    }

    [Fact]
    public async Task A_missing_explicit_provider_is_reported_and_never_replaced_by_the_default() {
        using var workspace = new MemoryProvidersPageController(new MemoryScenarioStore());
        var routes = Observe(workspace);

        await workspace.OpenAsync(new("provider.missing"));

        Assert.Equal("provider.missing", workspace.SelectedProviderId);
        Assert.Null(workspace.SelectedProvider);
        Assert.Contains("selected provider is missing", workspace.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(routes);
    }

    [Fact]
    public async Task Applying_a_location_changes_the_tab_without_publishing_and_a_default_location_restores_the_owner_default() {
        using var workspace = new MemoryProvidersPageController(new MemoryScenarioStore());
        await workspace.OpenAsync(new(MemoryScenarioStore.ProviderB));
        var routes = Observe(workspace);

        await workspace.ApplyRouteAsync(new(MemoryScenarioStore.ProviderB, MemoryWorkspaceTab.ProviderUi));
        Assert.Equal(6, workspace.ActiveTabIndex);
        Assert.Empty(routes);

        await workspace.ApplyRouteAsync(new(Tab: MemoryWorkspaceTab.ProviderUi));
        Assert.Equal(MemoryScenarioStore.ProviderA, workspace.SelectedProviderId);
        Assert.Equal([(new MemoryUrlState(MemoryScenarioStore.ProviderA, MemoryWorkspaceTab.ProviderUi), UrlHistoryMode.Replace)], routes);
    }

    [Fact]
    public async Task A_rendered_tab_click_is_published_as_a_typed_tab() {
        using var workspace = new MemoryProvidersPageController(new MemoryScenarioStore());
        await workspace.OpenAsync(new());
        var routes = Observe(workspace);
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        var cut = context.Render<MemoryWorkspaceSurface>(parameters => parameters.Add(component => component.Controller, workspace));

        await cut.InvokeAsync(() => cut.Find("[data-testid='memory-ui-tab-ingestion']").Click());

        Assert.Equal([(new MemoryUrlState(MemoryScenarioStore.ProviderA, MemoryWorkspaceTab.Ingestion), UrlHistoryMode.Push)], routes);
    }

    [Fact]
    public void An_unknown_tab_index_is_refused() {
        using var workspace = new MemoryProvidersPageController(new MemoryScenarioStore());

        Assert.Throws<ArgumentOutOfRangeException>(() => workspace.ActiveTabIndex = 7);
    }

    private static List<(MemoryUrlState Route, UrlHistoryMode Mode)> Observe(MemoryProvidersPageController workspace) {
        var routes = new List<(MemoryUrlState Route, UrlHistoryMode Mode)>();
        workspace.RouteChanged += (route, mode) => routes.Add((route, mode));
        return routes;
    }
}
