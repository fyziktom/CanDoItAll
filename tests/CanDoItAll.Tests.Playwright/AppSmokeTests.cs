using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

public sealed class AppSmokeTests(PlaywrightAppFixture fixture) : IClassFixture<PlaywrightAppFixture>
{
    [Fact]
    public async Task Dashboard_and_project_creation_flow_work()
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();

        var projectId = await CreateProjectAsync(page, "Playwright Project", "Discovery");

        await page.WaitForSelectorAsync("text=Structure canvas");
        Assert.Contains($"/projects/{projectId}/structure", page.Url, StringComparison.OrdinalIgnoreCase);
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
        await page.WaitForFunctionAsync("() => localStorage.getItem('candoitall.workbench.session')?.includes('route:test-lab') === true");

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

    [Fact]
    public async Task Direct_module_routes_and_workbench_surfaces_load_without_circuit_failure()
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();

        var projectsResponse = await page.GotoAsync($"{fixture.BaseUrl}/projects");
        Assert.NotNull(projectsResponse);
        Assert.True(projectsResponse!.Ok, $"Expected /projects to return 2xx, got {(int)projectsResponse.Status}.");
        await page.GetByTestId("projects-new-button").WaitForAsync();
        await page.WaitForSelectorAsync("text=Project workspace");
        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());

        await CreateProjectAsync(page, "Playwright Workbench", "Discovery");

        await page.WaitForURLAsync("**/projects/*/structure");
        await page.WaitForSelectorAsync("text=Structure canvas");
        await page.Locator(".cw-workbench-shell").WaitForAsync();
        await page.GetByLabel("Canvas zoom").WaitForAsync();
        await page.GetByLabel("Open quick create actions").ClickAsync();
        await page.Locator(".cw-create-rail").GetByRole(AriaRole.Button, new() { Name = "Note", Exact = true }).WaitForAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Help" }).ClickAsync();
        await page.GetByRole(AriaRole.Dialog, new() { Name = "Canvas shortcuts and gestures" }).WaitForAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Close help" }).ClickAsync();
        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());

        var match = Regex.Match(page.Url, @"/projects/(?<projectId>[0-9a-fA-F-]+)/structure$", RegexOptions.IgnoreCase);
        Assert.True(match.Success, $"Could not parse project id from {page.Url}.");

        var calendarResponse = await page.GotoAsync($"{fixture.BaseUrl}/projects/{match.Groups["projectId"].Value}/calendar");
        Assert.NotNull(calendarResponse);
        Assert.True(calendarResponse!.Ok, $"Expected calendar route to return 2xx, got {(int)calendarResponse.Status}.");
        await page.WaitForSelectorAsync("text=Project calendar");
        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
    }

    [Fact]
    public async Task Prompt_factory_canvas_surface_loads_and_exposes_shared_chrome()
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();

        var projectId = await CreateProjectAsync(page, "Playwright Prompt Factory", "Review");

        var response = await page.GotoAsync($"{fixture.BaseUrl}/prompt-factory");
        Assert.NotNull(response);
        Assert.True(response!.Ok, $"Expected /prompt-factory to return 2xx, got {(int)response.Status}.");

        await page.WaitForSelectorAsync("text=Prompt session workbench");
        await page.GetByTestId("prompt-factory-project").SelectOptionAsync(projectId.ToString());
        await page.Locator(".cw-workbench-shell").WaitForAsync();
        await page.GetByLabel("Canvas zoom").WaitForAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Help" }).ClickAsync();
        await page.GetByRole(AriaRole.Dialog, new() { Name = "Canvas shortcuts and gestures" }).WaitForAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Close help" }).ClickAsync();
        Assert.Equal(projectId.ToString(), await page.GetByTestId("prompt-factory-project").InputValueAsync());
        await page.GetByTestId("prompt-factory-build").WaitForAsync();
        await page.Locator(".cw-panel-card:has-text('Generated prompt') textarea").WaitForAsync();
        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
    }

    private async Task<Guid> CreateProjectAsync(IPage page, string projectName, string phase)
    {
        await page.GotoAsync($"{fixture.BaseUrl}/projects");
        await page.GetByTestId("projects-new-button").WaitForAsync();
        await page.GetByTestId("projects-new-button").ClickAsync();

        try
        {
            await page.GetByTestId("project-name-input").WaitForAsync(new() { Timeout = 2_000 });
        }
        catch (TimeoutException)
        {
            await page.GetByTestId("projects-new-button").ClickAsync();
            await page.GetByTestId("project-name-input").WaitForAsync();
        }

        await page.GetByTestId("project-name-input").FillAsync(projectName);
        await page.Locator("input[name=\"editor.CurrentPhase\"]").FillAsync(phase);
        await Task.WhenAll(
            page.WaitForURLAsync("**/projects/*/structure"),
            page.GetByRole(AriaRole.Button, new() { Name = "Save and open structure" }).ClickAsync());

        var match = Regex.Match(page.Url, @"/projects/(?<projectId>[0-9a-fA-F-]+)/structure$", RegexOptions.IgnoreCase);
        Assert.True(match.Success, $"Could not parse project id from {page.Url}.");
        return Guid.Parse(match.Groups["projectId"].Value);
    }
}
