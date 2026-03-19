using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

public sealed class AppSmokeTests(PlaywrightAppFixture fixture) : IClassFixture<PlaywrightAppFixture>
{
    [Fact]
    public async Task Dashboard_and_project_creation_flow_work()
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync($"{fixture.BaseUrl}/projects");
        await page.GetByTestId("projects-new-button").WaitForAsync();
        await page.GetByTestId("projects-new-button").ClickAsync();
        await page.GetByTestId("project-name-input").FillAsync("Playwright Project");
        await page.GetByTestId("project-save-button").ClickAsync();

        await page.WaitForSelectorAsync("text=Project saved.");
        await page.WaitForURLAsync("**/projects?projectId=*");
    }

    [Fact]
    public async Task Workbench_session_routes_are_persisted_after_reload()
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync($"{fixture.BaseUrl}/projects");
        await page.GotoAsync($"{fixture.BaseUrl}/validation");
        await page.GotoAsync($"{fixture.BaseUrl}/test-lab");
        await page.WaitForSelectorAsync("text=Tests, evidence, and execution results");

        var storageBeforeReload = await page.EvaluateAsync<string?>("() => localStorage.getItem('candoitall.workbench.session')");
        Assert.NotNull(storageBeforeReload);
        Assert.Contains("\"version\":3", storageBeforeReload, StringComparison.Ordinal);
        Assert.Contains("route:test-lab", storageBeforeReload, StringComparison.Ordinal);

        await page.ReloadAsync();
        await page.WaitForSelectorAsync("text=Tests, evidence, and execution results");

        var storageAfterReload = await page.EvaluateAsync<string?>("() => localStorage.getItem('candoitall.workbench.session')");
        Assert.NotNull(storageAfterReload);
        Assert.Contains("\"version\":3", storageAfterReload, StringComparison.Ordinal);
        Assert.Contains("route:test-lab", storageAfterReload, StringComparison.Ordinal);
    }
}
