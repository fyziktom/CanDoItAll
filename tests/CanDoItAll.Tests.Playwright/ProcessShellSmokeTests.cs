using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

[Collection(PlaywrightCollection.Name)]
public sealed class ProcessShellSmokeTests
{
    private readonly PlaywrightAppFixture fixture;

    public ProcessShellSmokeTests(PlaywrightAppFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task Process_shell_routes_render_global_and_project_scoped_workspaces()
    {
        var artifactDirectory = Path.Combine(
            PlaywrightTestHostPaths.RepositoryRoot,
            "output",
            "playwright",
            "process-shell-sb17");
        Directory.CreateDirectory(artifactDirectory);

        await using var context = await fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize
            {
                Width = 1440,
                Height = 900
            }
        });
        var page = await context.NewPageAsync();

        var globalResponse = await page.GotoAsync($"{fixture.BaseUrl}/processes");
        Assert.NotNull(globalResponse);
        Assert.True(globalResponse!.Ok, $"Expected /processes to return 2xx, got {(int)globalResponse.Status}.");
        await DismissStartupModalIfPresentAsync(page);
        await page.GetByTestId("processes-shell").WaitForAsync();
        await page.GetByTestId("processes-command-strip").WaitForAsync();
        await page.GetByTestId("processes-tab-definitions").WaitForAsync();
        await page.GetByTestId("processes-definition-search").FillAsync("architecture");
        await page.GetByTestId("processes-definition-search-submit").ClickAsync();
        await page.GetByTestId("processes-definition-architecture-decision-governance").WaitForAsync();
        await page.GetByTestId("processes-definition-architecture-decision-governance").ClickAsync();
        await page.GetByTestId("processes-definition-editor").WaitForAsync();
        await page.GetByTestId("processes-definition-editor-name").FillAsync("Architecture decision governance SB16");
        await page.GetByTestId("processes-definition-editor-owner").FillAsync("Architecture board");
        await page.GetByTestId("processes-definition-editor-manager-override").FillAsync("Use the architecture board manager.");
        await page.GetByTestId("processes-definition-save").ClickAsync();
        await ExpectTextContainsAsync(page.GetByTestId("processes-definition-editor-receipt"), "Draft saved");
        await page.GetByTestId("processes-definition-publish").ClickAsync();
        await ExpectTextContainsAsync(page.GetByTestId("processes-definition-editor-receipt"), "published");
        await page.GetByTestId("processes-definition-canvas").WaitForAsync();
        await page.GetByTestId("processes-canvas-node-step-decision-intake").ClickAsync();
        await ExpectTextContainsAsync(page.GetByTestId("processes-canvas-selection"), "Capture architecture decision demand");
        await page.GetByTestId("processes-canvas-toolbox-process-step-implementation").ClickAsync();
        await ExpectTextContainsAsync(page.GetByTestId("processes-canvas-command-receipt"), "accepted");
        await page.GetByTestId("processes-canvas-recompose").ClickAsync();
        await ExpectTextContainsAsync(page.GetByTestId("processes-canvas-command-receipt"), "recomposed");
        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(artifactDirectory, "processes-definition-canvas.png"),
            FullPage = true
        });
        await page.GetByTestId("processes-definition-role-editor").WaitForAsync();
        await page.GetByTestId("processes-role-solution-architect").ClickAsync();
        await page.GetByTestId("processes-role-display-name").FillAsync("Principal architecture steward SB17");
        await page.GetByTestId("processes-role-project-assignment").SelectOptionAsync(new[] { "Manager" });
        await page.GetByTestId("processes-role-allocation").FillAsync("45");
        await page.GetByTestId("processes-role-save").ClickAsync();
        await ExpectTextContainsAsync(page.GetByTestId("processes-role-editor-receipt"), "saved");
        await page.GetByTestId("processes-role-template-action").SelectOptionAsync(new[] { "process-role.solution-architect" });
        await page.GetByTestId("processes-role-apply-template").ClickAsync();
        await ExpectTextContainsAsync(page.GetByTestId("processes-role-editor-receipt"), "customized");
        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(artifactDirectory, "processes-definition-role-editor.png"),
            FullPage = true
        });
        await page.GetByTestId("processes-feed-defaults").ClickAsync();
        await page.GetByTestId("processes-feed-defaults-receipt").WaitForAsync();
        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(artifactDirectory, "processes-global-definition-catalog.png"),
            FullPage = true
        });
        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());

        var projectId = await CreateProjectAsync(page, "Playwright Process Shell", "Discovery");
        var runId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        var projectResponse = await page.GotoAsync($"{fixture.BaseUrl}/projects/{projectId:D}/processes?runId={runId:D}");
        Assert.NotNull(projectResponse);
        Assert.True(projectResponse!.Ok, $"Expected project-scoped processes route to return 2xx, got {(int)projectResponse.Status}.");
        await page.GetByTestId("processes-shell").WaitForAsync();
        await page.GetByTestId("processes-tab-panel-liveruns").WaitForAsync();
        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(artifactDirectory, "processes-project-shell.png"),
            FullPage = true
        });
        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
    }

    private async Task<Guid> CreateProjectAsync(IPage page, string projectName, string phase)
    {
        await page.GotoAsync($"{fixture.BaseUrl}/projects");
        await DismissStartupModalIfPresentAsync(page, timeoutMs: 15_000);
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
        await page.GetByTestId("project-phase-input").FillAsync(phase);
        await Task.WhenAll(
            page.WaitForURLAsync("**/projects/*/structure"),
            page.GetByRole(AriaRole.Button, new() { Name = "Save and open structure" }).ClickAsync());

        var match = Regex.Match(page.Url, @"/projects/(?<projectId>[0-9a-fA-F-]+)/structure$", RegexOptions.IgnoreCase);
        Assert.True(match.Success, $"Could not parse project id from {page.Url}.");
        return Guid.Parse(match.Groups["projectId"].Value);
    }

    private static async Task DismissStartupModalIfPresentAsync(IPage page, float timeoutMs = 1_500)
    {
        var startupDialog = page.GetByTestId("database-startup-modal");
        if (!await WaitForLocatorAsync(startupDialog, timeoutMs))
        {
            return;
        }

        await page.GetByTestId("database-startup-continue").ClickAsync();
        await startupDialog.WaitForAsync(new() { State = WaitForSelectorState.Detached });
    }

    private static async Task<bool> WaitForLocatorAsync(ILocator locator, float timeoutMs)
    {
        try
        {
            await locator.WaitForAsync(new() { Timeout = timeoutMs });
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    private static async Task ExpectTextContainsAsync(ILocator locator, string expectedText)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        string? renderedText = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            renderedText = await locator.TextContentAsync();
            if (renderedText?.Contains(expectedText, StringComparison.OrdinalIgnoreCase) == true)
            {
                return;
            }

            await Task.Delay(100);
        }

        Assert.Fail($"Expected locator text to contain '{expectedText}', but saw '{renderedText}'.");
    }
}
