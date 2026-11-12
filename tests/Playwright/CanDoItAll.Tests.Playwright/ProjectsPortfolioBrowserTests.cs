using System.Text.RegularExpressions;
using System.Text;
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
            await Assertions.Expect(page).ToHaveURLAsync(new Regex($@"/projects\?q=[^&]+&filesId={projectId:D}$"));
            var files = page.GetByTestId("project-files-dialog");
            await ProjectsFilesBrowserProof.DownloadAsync(host, page, files, "portfolio-proof.md", Encoding.UTF8.GetBytes(content), "projects-dialog");
            await files.Locator(".ft-file-browser__item-main").Filter(new() { HasText = "portfolio-proof.md" }).PressAsync("Enter");
            await Assertions.Expect(files.GetByTestId("interaction-markdown-view")).ToContainTextAsync("Actual authorized Markdown bytes.");
            await AssertVisibleFileSurfaceAsync(files);
            await Assertions.Expect(files.GetByTestId("interaction-mode-edit")).ToBeDisabledAsync();
            await page.ScreenshotAsync(new() { Path = host.Artifact("projects-native-files-dialog.png") });
            await files.GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).ClickAsync();
            await Assertions.Expect(files).ToHaveCountAsync(0);
            await Assertions.Expect(page).ToHaveURLAsync(new Regex(@"/projects\?q=[^&]+$"));
            await page.GetByTestId("projects-portfolio-tabs").GetByRole(AriaRole.Tab, new() { NameRegex = new Regex("^Files") }).ClickAsync();
            await Assertions.Expect(page).ToHaveURLAsync(new Regex(@"/projects\?q=[^&]+&view=files$"));
            var pane = page.GetByTestId("project-files-portfolio-pane");
            await ProjectsFilesBrowserProof.DownloadAsync(host, page, pane, "portfolio-proof.md", Encoding.UTF8.GetBytes(content), "projects-portfolio");
            await pane.Locator(".ft-file-browser__item-main").Filter(new() { HasText = "portfolio-proof.md" }).PressAsync("Enter");
            await Assertions.Expect(pane.GetByTestId("interaction-markdown-view")).ToContainTextAsync("Actual authorized Markdown bytes.");
            await AssertVisibleFileSurfaceAsync(pane);
            await page.GetByTestId("projects-search-input").FillAsync("No matching owned project");
            await Assertions.Expect(pane.GetByText("No projects match the shared filters", new() { Exact = true })).ToBeVisibleAsync();
            await Assertions.Expect(pane.GetByTestId("interaction-markdown-view")).ToHaveCountAsync(0);
            await page.GetByTestId("projects-search-input").FillAsync(name);
            await Assertions.Expect(pane.Locator(".ft-file-browser__item-main").Filter(new() { HasText = "portfolio-proof.md" })).ToBeVisibleAsync();
            await page.ScreenshotAsync(new() { Path = host.Artifact("projects-native-files-portfolio.png") });
            // Back returns to the cards with the same search; Forward restores the Files view.
            await page.GoBackAsync();
            await Assertions.Expect(page).ToHaveURLAsync(new Regex(@"/projects\?q=[^&]+$"));
            await Assertions.Expect(pane).ToBeHiddenAsync();
            await page.GoForwardAsync();
            await Assertions.Expect(page).ToHaveURLAsync(new Regex(@"/projects\?q=[^&]+&view=files$"));
            await Assertions.Expect(pane.Locator(".ft-file-browser__item-main").Filter(new() { HasText = "portfolio-proof.md" })).ToBeVisibleAsync();

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
            await Assertions.Expect(page).ToHaveURLAsync($"{host.BaseUrl}/projects?projectId={projectId:D}&mode=editor");
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

    [Fact]
    public async Task Two_browser_contexts_preserve_conflicting_drafts_and_require_explicit_current_based_recovery() {
        await using var host = await LiveUiHost.StartAsync();
        var first = await host.NewPageAsync();
        await using var secondContext = await first.Context.Browser!.NewContextAsync(new() {
            ViewportSize = new() { Width = 1920, Height = 1080 }, DeviceScaleFactor = 1
        });
        var second = await secondContext.NewPageAsync();
        second.SetDefaultTimeout(45_000);
        var firstOracle = CrmHrBrowserOracle.Attach(first);
        var secondOracle = CrmHrBrowserOracle.Attach(second);
        try {
            Guid id = await host.SeedAsync(async services => (await services.GetRequiredService<ProjectsService>().SaveAsync(new() {
                Name = "Concurrent browser project", Phases = [new() { Name = "Original phase" }],
                Options = [new() { Category = ProjectOptionCategory.Language, OptionName = "Original language" }]
            })).Value);
            foreach (var (page, oracle) in new[] { (first, firstOracle), (second, secondOracle) }) {
                await oracle.NavigateAsync($"{host.BaseUrl}/projects?projectId={id:D}&mode=editor");
                await PlaywrightAppFixture.CompleteDatabaseStartupAsync(page);
                await Assertions.Expect(page.GetByTestId("projects-workspace")).ToHaveAttributeAsync("data-interactive", "true");
                await Assertions.Expect(page.GetByTestId("project-name-input")).ToHaveValueAsync("Concurrent browser project");
            }
            var firstEditor = first.GetByTestId("projects-editor-modal");
            var secondEditor = second.GetByTestId("projects-editor-modal");
            await firstEditor.GetByTestId("project-name-input").FillAsync("My preserved browser draft");
            await secondEditor.GetByTestId("project-name-input").FillAsync("Competing saved name");
            await secondEditor.GetByRole(AriaRole.Button, new() { Name = "2. Dates and phases", Exact = true }).ClickAsync();
            await secondEditor.GetByTestId("project-phase-row").Locator("input:not([type='date'])").FillAsync("Competing phase");
            await secondEditor.GetByTestId("project-phase-row").Locator("textarea").FillAsync("Competing phase goal");
            await secondEditor.GetByRole(AriaRole.Button, new() { Name = "3. Stack profile", Exact = true }).ClickAsync();
            await secondEditor.GetByTestId("project-option-Language").Locator("input").Nth(0).FillAsync("Competing language");
            await secondEditor.GetByTestId("project-option-Language").Locator("input").Nth(1).FillAsync("Competing option notes");
            await secondEditor.GetByTestId("project-save-button").ClickAsync();
            await Assertions.Expect(secondEditor).ToContainTextAsync("Project saved.");
            var competing = await host.SeedAsync(services => services.GetRequiredService<ProjectsService>().GetAsync(id));

            await firstEditor.GetByTestId("project-save-button").ClickAsync();
            await Assertions.Expect(firstEditor.GetByTestId("project-definition-conflict")).ToBeVisibleAsync();
            await Assertions.Expect(firstEditor.GetByTestId("project-name-input")).ToHaveValueAsync("My preserved browser draft");
            await Assertions.Expect(firstEditor.GetByTestId("project-save-button")).ToBeDisabledAsync();
            var date = firstEditor.GetByTestId("project-target-date");
            await date.PressAsync("ArrowRight");
            await date.PressAsync("1");
            await date.PressAsync("Tab");
            Assert.True(await date.EvaluateAsync<bool>("element => element.validity.badInput"));
            await firstEditor.GetByTestId("project-conflict-read").ClickAsync();
            var comparison = firstEditor.GetByTestId("project-definition-comparison");
            await Assertions.Expect(comparison).ToContainTextAsync("Competing saved name");
            await Assertions.Expect(comparison).ToContainTextAsync("Competing phase goal");
            await Assertions.Expect(comparison).ToContainTextAsync("Competing option notes");
            Assert.True(await date.EvaluateAsync<bool>("element => element.validity.badInput"));
            await Assertions.Expect(firstEditor.GetByTestId("project-name-input")).ToHaveValueAsync("My preserved browser draft");
            var bounds = await firstEditor.BoundingBoxAsync();
            Assert.NotNull(bounds);
            Assert.True(bounds.Width >= 1000, $"The desktop conflict editor collapsed to {bounds.Width}px.");
            await Assertions.Expect(firstEditor.GetByTestId("project-save-button")).ToBeInViewportAsync();
            await first.ScreenshotAsync(new() { Path = host.Artifact("projects-concurrent-conflict.png") });

            await firstEditor.GetByTestId("project-conflict-reconcile").ClickAsync();
            await Assertions.Expect(firstEditor).ToContainTextAsync("Complete or clear invalid dates");
            await Assertions.Expect(firstEditor.GetByTestId("project-name-input")).ToHaveValueAsync("My preserved browser draft");
            Assert.True(await date.EvaluateAsync<bool>("element => element.validity.badInput"));
            await date.FillAsync(string.Empty);
            await firstEditor.GetByTestId("project-conflict-reconcile").ClickAsync();
            await Assertions.Expect(firstEditor.GetByTestId("project-definition-reconciliation")).ToBeVisibleAsync();
            await Assertions.Expect(firstEditor.GetByTestId("project-name-input")).ToHaveValueAsync("Competing saved name");
            await Assertions.Expect(comparison).ToContainTextAsync("My preserved browser draft");
            await firstEditor.GetByTestId("project-name-input").FillAsync("Manually reconciled browser name");
            await first.ScreenshotAsync(new() { Path = host.Artifact("projects-concurrent-reconciliation.png") });
            await firstEditor.GetByTestId("project-save-button").ClickAsync();
            await Assertions.Expect(firstEditor).ToContainTextAsync("Project saved.");
            await Assertions.Expect(comparison).ToHaveCountAsync(0);
            var reconciled = await host.SeedAsync(services => services.GetRequiredService<ProjectsService>().GetAsync(id));
            Assert.Equal("Manually reconciled browser name", reconciled.Name);
            Assert.Equal("Competing phase", Assert.Single(reconciled.Phases).Name);
            Assert.Equal(competing.Phases[0].Id, reconciled.Phases[0].Id);
            Assert.Equal("Competing language", reconciled.Options.Single(option => option.Category == ProjectOptionCategory.Language).OptionName);
            Assert.NotEqual(competing.ExpectedDefinition!.Revision, reconciled.ExpectedDefinition!.Revision);

            await secondEditor.GetByRole(AriaRole.Button, new() { Name = "1. Identity", Exact = true }).ClickAsync();
            await secondEditor.GetByTestId("project-name-input").FillAsync("Second stale browser draft");
            await secondEditor.GetByTestId("project-save-button").ClickAsync();
            await Assertions.Expect(secondEditor.GetByTestId("project-definition-conflict")).ToBeVisibleAsync();
            await secondEditor.GetByTestId("project-conflict-read").ClickAsync();
            await Assertions.Expect(secondEditor.GetByTestId("project-definition-comparison")).ToContainTextAsync(reconciled.Name);
            await secondEditor.GetByTestId("project-conflict-reload").ClickAsync();
            await Assertions.Expect(secondEditor.GetByTestId("project-name-input")).ToHaveValueAsync(reconciled.Name);
            await Assertions.Expect(secondEditor.GetByTestId("project-definition-conflict")).ToHaveCountAsync(0);
            await Assertions.Expect(secondEditor.GetByTestId("project-definition-comparison")).ToHaveCountAsync(0);
            await firstOracle.AssertCleanAsync();
            await secondOracle.AssertCleanAsync();
        } catch {
            await first.ScreenshotAsync(new() { Path = host.Artifact("projects-concurrent-first-failure.png") });
            await second.ScreenshotAsync(new() { Path = host.Artifact("projects-concurrent-second-failure.png") });
            throw;
        } finally {
            await host.CaptureHostLogAsync();
        }
    }

    private static async Task AssertVisibleFileSurfaceAsync(ILocator host) {
        await Assertions.Expect(host.GetByTestId("interaction-markdown-view")).ToBeVisibleAsync();
        var bounds = await host.GetByTestId("file-interaction").BoundingBoxAsync();
        Assert.NotNull(bounds);
        Assert.True(bounds.Width >= 640, $"The actual desktop file renderer collapsed to {bounds.Width}px.");
        await Assertions.Expect(host.GetByTestId("interaction-markdown-view")).ToBeInViewportAsync();
    }
}
