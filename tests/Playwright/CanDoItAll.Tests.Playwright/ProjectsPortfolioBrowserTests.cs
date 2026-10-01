using System.Text.RegularExpressions;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Projects;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright.Smoke;

[Trait("Category", "Playwright")]
[Trait("Category", "HostPlatform")]
public sealed class ProjectsPortfolioBrowserTests {
    [Fact]
    public async Task Ui_created_project_uses_actual_Files_scope_and_completed_Workspace_shell() {
        await using var host = await LiveUiHost.StartAsync();
        var page = await host.NewPageAsync();
        await NavigationAcknowledgementProbe.InstallAsync(page.Context);
        var oracle = CrmHrBrowserOracle.Attach(page);
        try {
            Guid projectId = await ProjectsPortfolioUiJourney.CreateAndEditAsync(host, page, oracle);
            string name = await host.SeedAsync(async services => (await services.GetRequiredService<ProjectsService>().GetAsync(projectId)).Name);
            const string content = "# Portfolio scoped file\n\nActual authorized Markdown bytes.";
            await host.SeedAsync(async services => {
                string root = services.GetRequiredService<IWorkspacePathResolver>().ResolveWorkspaceRoot();
                string directory = Path.Combine(root, "managed-files", "project-media", "files", projectId.ToString("N"));
                Directory.CreateDirectory(directory);
                await File.WriteAllTextAsync(Path.Combine(directory, "portfolio-proof.md"), content);
                return true;
            });
            await oracle.NavigateAsync(host.BaseUrl + "/projects");
            await Assertions.Expect(page.GetByTestId("projects-workspace")).ToHaveAttributeAsync("data-interactive", "true");
            await page.GetByTestId("projects-search-input").FillAsync(name);
            var card = page.GetByTestId("project-card");
            await Assertions.Expect(card).ToHaveCountAsync(1);
            await card.GetByTestId("project-card-files-button").ClickAsync();
            var files = page.GetByTestId("project-files-dialog");
            await files.Locator(".ft-file-browser__item-main").Filter(new() { HasText = "portfolio-proof.md" }).PressAsync("Enter");
            await Assertions.Expect(files.GetByTestId("interaction-text-view")).ToContainTextAsync("Actual authorized Markdown bytes.");
            await AssertVisibleFileSurfaceAsync(files);
            await Assertions.Expect(files.GetByTestId("interaction-mode-edit")).ToBeDisabledAsync();
            await page.ScreenshotAsync(new() { Path = host.Artifact("projects-native-files-dialog.png") });
            await files.GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).ClickAsync();
            await page.GetByTestId("projects-portfolio-tabs").GetByRole(AriaRole.Tab, new() { NameRegex = new Regex("^Files") }).ClickAsync();
            var pane = page.GetByTestId("project-files-portfolio-pane");
            await pane.Locator(".ft-file-browser__item-main").Filter(new() { HasText = "portfolio-proof.md" }).PressAsync("Enter");
            await Assertions.Expect(pane.GetByTestId("interaction-text-view")).ToContainTextAsync("Actual authorized Markdown bytes.");
            await AssertVisibleFileSurfaceAsync(pane);
            await page.GetByTestId("projects-search-input").FillAsync("No matching owned project");
            await Assertions.Expect(pane.GetByText("No projects match the shared filters", new() { Exact = true })).ToBeVisibleAsync();
            await Assertions.Expect(pane.GetByTestId("interaction-text-view")).ToHaveCountAsync(0);
            await page.GetByTestId("projects-search-input").FillAsync(name);
            await Assertions.Expect(pane.Locator(".ft-file-browser__item-main").Filter(new() { HasText = "portfolio-proof.md" })).ToBeVisibleAsync();
            await page.ScreenshotAsync(new() { Path = host.Artifact("projects-native-files-portfolio.png") });

            await host.AcknowledgeNavigationAsync(page, () => page.GetByTestId("shell-settings-action").ClickAsync());
            await Assertions.Expect(page.GetByTestId("defaults-name")).ToBeVisibleAsync();
            foreach (string tab in new[] { "API Access", "Storage", "Data Sources", "Workspace" }) {
                var tabName = new Regex("^" + Regex.Escape(tab) + (tab == "Workspace" ? "$" : ""));
                await host.AcknowledgeNavigationAsync(page, () => page.GetByRole(AriaRole.Button, new() { NameRegex = tabName }).ClickAsync());
                await Assertions.Expect(page.Locator("#blazor-error-ui")).Not.ToBeVisibleAsync();
            }
            await host.AcknowledgeNavigationAsync(page, () => page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("^Providers") }).ClickAsync());
            foreach (string tab in new[] { "Agents", "Simple Chats", "Overview" }) {
                await host.AcknowledgeNavigationAsync(page, () => page.GetByTestId("agents-shell-tabs").GetByRole(AriaRole.Button, new() { NameRegex = new Regex("^" + Regex.Escape(tab)) }).ClickAsync());
            }
            await page.GetByTestId("agents-overview-usage-scope").WaitForAsync();
            await host.AcknowledgeNavigationAsync(page, () => page.GetByTestId("agents-overview-usage-scope").GetByRole(AriaRole.Button, new() { Name = "Chats", Exact = true }).ClickAsync());
            await Assertions.Expect(page.GetByTestId("agents-overview-top-consumers")).ToContainTextAsync("Top 5 Chats");
            await page.GoBackAsync();
            await Assertions.Expect(page.GetByTestId("agents-overview-provider-bar")).ToBeVisibleAsync();
            await page.GoForwardAsync();
            await Assertions.Expect(page.GetByTestId("agents-overview-top-consumers")).ToContainTextAsync("Top 5 Chats");
            await oracle.NavigateAsync($"{host.BaseUrl}/projects?projectId={projectId:D}");
            await Assertions.Expect(page.GetByTestId("projects-workspace")).ToHaveAttributeAsync("data-interactive", "true");
            var overview = page.GetByTestId("projects-detail-modal");
            await overview.GetByRole(AriaRole.Button, new() { Name = "Edit project", Exact = true }).ClickAsync();
            await host.AcknowledgeNavigationAsync(page, () => page.GetByTestId("project-save-open-button").ClickAsync());
            await ProjectFilesUiJourney.ReadyFileCanvasAsync(page);
            await oracle.AssertCleanAsync();
        } catch {
            await page.ScreenshotAsync(new() { Path = host.Artifact("projects-handoff-failure.png") });
            await File.WriteAllTextAsync(host.Artifact("projects-handoff-failure.txt"), await page.Locator("body").InnerTextAsync());
            throw;
        } finally {
            await host.CaptureHostLogAsync();
        }
    }

    private static async Task AssertVisibleFileSurfaceAsync(ILocator host) {
        await Assertions.Expect(host.GetByTestId("interaction-text-view")).ToBeVisibleAsync();
        var bounds = await host.GetByTestId("file-interaction").BoundingBoxAsync();
        Assert.NotNull(bounds);
        Assert.True(bounds.Width >= 640, $"The actual desktop file renderer collapsed to {bounds.Width}px.");
        await Assertions.Expect(host.GetByTestId("interaction-text-view")).ToBeInViewportAsync();
    }
}
