using Bunit;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.Shell;

public sealed class MainLayoutWorkbenchTabTests {
    [Fact]
    public async Task A_query_identity_updates_the_page_tab_and_opens_its_own_tab_only_on_an_explicit_command() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var cut = harness.Context.Render<MainLayoutRetirementTests.ObservedLayout>();
        await cut.Instance.FirstRender.WaitAsync(TimeSpan.FromSeconds(20));
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        var workbench = harness.Context.Services.GetRequiredService<WorkbenchStateService>();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        await NavigateAsync(cut, navigation, "/prompt-gallery");
        var page = workbench.GetActiveTab()!;
        var count = workbench.Tabs.Count;
        await NavigateAsync(cut, navigation, $"/prompt-gallery?promptId={first:D}");
        await NavigateAsync(cut, navigation, $"/prompt-gallery?promptId={second:D}");

        Assert.Equal(count, workbench.Tabs.Count);
        Assert.Equal(page.TabId, workbench.ActiveTabId);
        Assert.Equal($"/prompt-gallery?promptId={second:D}", workbench.GetActiveTab()!.Route);

        await NavigateAsync(cut, navigation, $"/prompt-gallery?promptId={first:D}", WorkbenchNavigation.OpenTabHistoryState);

        Assert.Equal(count + 1, workbench.Tabs.Count);
        Assert.Equal($"prompt:{first:N}", workbench.ActiveTabId);
        Assert.Equal(WorkbenchTabKinds.PromptDetail, workbench.GetActiveTab()!.TabKind);
    }

    private static async Task NavigateAsync(IRenderedComponent<MainLayoutRetirementTests.ObservedLayout> cut, NavigationManager navigation,
        string uri, string? historyEntryState = null) {
        await cut.InvokeAsync(() => navigation.NavigateTo(uri, new NavigationOptions { HistoryEntryState = historyEntryState }));
        await cut.Instance.NavigationCompletion.WaitAsync(TimeSpan.FromSeconds(10));
    }
}
