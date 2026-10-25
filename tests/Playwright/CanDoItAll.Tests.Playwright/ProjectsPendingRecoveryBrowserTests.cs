using CanDoItAll.Modules.Projects;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright.Smoke;

[Trait("Category", "Playwright")]
[Trait("Category", "HostPlatform")]
public sealed class ProjectsPendingRecoveryBrowserTests {
    [Fact]
    public async Task Native_editor_reopens_the_same_durable_operation_after_browser_acceptance_persistence_fails() {
        await using var host = await LiveUiHost.StartAsync();
        var page = await host.NewPageAsync();
        const string moduleRoute = "**/js/projectPendingSubmissions*.js";
        var moduleRequests = 0;
        await page.RouteAsync(moduleRoute, async route => {
            Interlocked.Increment(ref moduleRequests);
            await route.ContinueAsync();
        });
        try {
            var id = await host.SeedAsync(async services => (await services.GetRequiredService<ProjectsService>().SaveAsync(new() { Name = "Before durable save" })).Value);
            await page.GotoAsync($"{host.BaseUrl}/projects?projectId={id:D}&mode=editor");
            await PlaywrightAppFixture.CompleteDatabaseStartupAsync(page);
            await Assertions.Expect(page.GetByTestId("projects-workspace")).ToHaveAttributeAsync("data-interactive", "true");
            var editor = page.GetByTestId("projects-editor-modal");
            await Assertions.Expect(editor.GetByTestId("project-save-button")).ToBeEnabledAsync();
            Assert.True(Volatile.Read(ref moduleRequests) > 0, "The pending-submission module must load through the test route before saving.");
            await page.EvaluateAsync("""
                () => {
                    window.projectAcceptedPersistenceFailures = 0;
                    const put = IDBObjectStore.prototype.put;
                    window.restoreProjectAcceptedPersistenceFault = () => {
                        IDBObjectStore.prototype.put = put;
                        delete window.restoreProjectAcceptedPersistenceFault;
                    };
                    IDBObjectStore.prototype.put = function(value, ...keys) {
                        if (this.transaction.db.name === "candoitall.projects.pending-submissions" &&
                            this.name === "submissions" && value.stage === "accepted") {
                            window.projectAcceptedPersistenceFailures++;
                            throw new DOMException("Injected acceptance persistence failure after owner success", "QuotaExceededError");
                        }
                        return put.call(this, value, ...keys);
                    };
                }
                """);
            await editor.GetByTestId("project-name-input").FillAsync("Accepted frozen browser name");
            await editor.GetByTestId("project-save-button").ClickAsync();
            var recovery = editor.GetByTestId("project-pending-recovery");
            await Assertions.Expect(recovery).ToContainTextAsync("Browser persistence failed");
            Assert.Equal(1, await page.EvaluateAsync<int>("window.projectAcceptedPersistenceFailures"));
            await Assertions.Expect(editor.GetByTestId("project-save-button")).ToBeDisabledAsync();
            var reference = await recovery.GetByText("Save reference:", new() { Exact = false }).TextContentAsync();
            var historical = await host.SeedAsync(services => services.GetRequiredService<ProjectsService>().GetAsync(id));
            Assert.Equal("Accepted frozen browser name", historical.Name);
            await editor.GetByTestId("project-name-input").FillAsync("Unpersisted later browser typing");
            await host.SeedAsync(async services => {
                var projects = services.GetRequiredService<ProjectsService>();
                var later = await projects.GetAsync(id);
                later.Name = "Current definition after another writer";
                Assert.True((await projects.SaveAsync(later)).IsSuccess);
                return true;
            });
            await page.UnrouteAsync(moduleRoute);
            await page.GotoAsync($"{host.BaseUrl}/projects");
            await Assertions.Expect(page.GetByTestId("project-pending-open")).ToBeVisibleAsync();
            await page.GetByTestId("project-pending-open").ClickAsync();
            await Assertions.Expect(recovery).ToContainTextAsync(reference!);
            await Assertions.Expect(editor.GetByTestId("project-name-input")).ToHaveValueAsync("Accepted frozen browser name");
            await Assertions.Expect(editor).ToContainTextAsync("Later typing and raw starter-object plans were not persisted");
            await Assertions.Expect(editor.GetByTestId("project-pending-apply")).ToBeDisabledAsync();
            await editor.GetByTestId("project-pending-check").ClickAsync();
            await Assertions.Expect(editor.GetByTestId("project-pending-apply")).ToBeEnabledAsync();
            await editor.GetByTestId("project-pending-apply").ClickAsync();
            await editor.GetByTestId("project-pending-current").ClickAsync();
            await Assertions.Expect(editor.GetByTestId("project-definition-comparison")).ToContainTextAsync("Current definition after another writer");
            await Assertions.Expect(editor.GetByTestId("project-name-input")).ToHaveValueAsync("Accepted frozen browser name");
            await page.ScreenshotAsync(new() { Path = host.Artifact("projects-durable-historical-recovery.png") });
            await editor.GetByTestId("project-pending-finish").ClickAsync();
            await Assertions.Expect(page.GetByTestId("project-pending-list")).ToHaveCountAsync(0);
            var current = await host.SeedAsync(services => services.GetRequiredService<ProjectsService>().GetAsync(id));
            Assert.Equal("Current definition after another writer", current.Name);
            Assert.NotEqual(historical.ExpectedDefinition!.Revision, current.ExpectedDefinition!.Revision);
            await Assertions.Expect(page.Locator("#blazor-error-ui")).Not.ToBeVisibleAsync();
        } catch {
            await page.ScreenshotAsync(new() { Path = host.Artifact("projects-durable-recovery-failure.png") });
            throw;
        } finally {
            await page.EvaluateAsync("() => window.restoreProjectAcceptedPersistenceFault?.()");
            await page.UnrouteAsync(moduleRoute);
            await host.CaptureHostLogAsync();
        }
    }
}
