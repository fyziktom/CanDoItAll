using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright.Smoke;

[Trait("Category", "Playwright")]
[Trait("Category", "HostPlatform")]
public sealed class ProjectStructureProjectCreateRecoveryBrowserTests {
    [Fact]
    public async Task Workbench_root_creation_recovers_in_Projects_after_browser_failure_without_linking_or_seeding() {
        await using var host = await LiveUiHost.StartAsync();
        var page = await host.NewPageAsync();
        try {
            var parent = await host.SeedAsync(async services => (await services.GetRequiredService<ProjectsService>()
                .SaveAsync(new() { Name = "Workbench recovery parent" })).Value);
            await page.GotoAsync($"{host.BaseUrl}/projects/{parent:D}/structure");
            await PlaywrightAppFixture.CompleteDatabaseStartupAsync(page);
            await ProjectFilesUiJourney.ReadyFileCanvasAsync(page);
            await ProjectFilesUiJourney.SelectFileNodeAsync(page, $"project:{parent:D}", "Workbench recovery parent");
            await page.GetByTestId("project-structure-selection-window").GetByRole(AriaRole.Button,
                new() { Name = "Add subproject", Exact = true }).ClickAsync();
            await page.GetByTestId("project-structure-hierarchy-create-project").ClickAsync();
            var modal = page.GetByTestId("projects-editor-modal");
            await Assertions.Expect(modal.GetByTestId("project-save-button")).ToBeEnabledAsync();
            await modal.GetByTestId("project-name-input").FillAsync("Accepted Workbench root");
            await modal.GetByText("4. Linked objects", new() { Exact = true }).ClickAsync();
            await modal.GetByRole(AriaRole.Button, new() { Name = "Add planned object", Exact = true }).First.ClickAsync();
            await modal.GetByTestId("project-starter-title").FillAsync("Unsent browser starter");
            await page.EvaluateAsync("""
                () => {
                    window.workbenchAcceptedPersistenceFailures = 0;
                    const put = IDBObjectStore.prototype.put;
                    window.restoreWorkbenchPersistenceFault = () => {
                        IDBObjectStore.prototype.put = put;
                        delete window.restoreWorkbenchPersistenceFault;
                    };
                    IDBObjectStore.prototype.put = function(value, ...keys) {
                        if (this.transaction.db.name === "candoitall.projects.pending-submissions" &&
                            this.name === "submissions" && value.stage === "accepted") {
                            window.workbenchAcceptedPersistenceFailures++;
                            throw new DOMException("Injected Workbench accepted-receipt persistence failure", "QuotaExceededError");
                        }
                        return put.call(this, value, ...keys);
                    };
                }
                """);
            await modal.GetByTestId("project-save-button").ClickAsync();
            var recovery = modal.GetByTestId("project-pending-recovery");
            await Assertions.Expect(recovery).ToContainTextAsync("Browser persistence failed");
            Assert.Equal(1, await page.EvaluateAsync<int>("window.workbenchAcceptedPersistenceFailures"));
            await Assertions.Expect(modal.GetByTestId("project-save-button")).ToBeDisabledAsync();
            var reference = await recovery.GetByText("Save reference:", new() { Exact = false }).TextContentAsync();
            var created = await host.SeedAsync(async services => Assert.Single(
                (await services.GetRequiredService<ProjectsService>().ListAsync()).Where(project => project.Id != parent)));
            Assert.Equal("Accepted Workbench root", created.Name);
            await page.ScreenshotAsync(new() { Path = host.Artifact("workbench-root-accepted-browser-failure.png") });
            await modal.GetByTestId("project-create-recovery-open").ClickAsync();
            await Assertions.Expect(page).ToHaveURLAsync($"{host.BaseUrl}/projects");
            await page.ReloadAsync();
            await page.GetByTestId("project-pending-open").ClickAsync();
            await Assertions.Expect(recovery).ToContainTextAsync(reference!);
            await Assertions.Expect(modal).ToContainTextAsync("Later typing and raw starter-object plans were not persisted");
            await modal.GetByTestId("project-pending-check").ClickAsync();
            await Assertions.Expect(modal.GetByTestId("project-pending-apply")).ToBeEnabledAsync();
            await modal.GetByTestId("project-pending-apply").ClickAsync();
            await Assertions.Expect(modal.GetByTestId("project-name-input")).ToHaveValueAsync("Accepted Workbench root");
            await page.ScreenshotAsync(new() { Path = host.Artifact("workbench-root-projects-recovery.png") });
            await modal.GetByTestId("project-pending-finish").ClickAsync();
            await Assertions.Expect(page.GetByTestId("project-pending-list")).ToHaveCountAsync(0);
            await host.SeedAsync(async services => {
                var projects = services.GetRequiredService<ProjectsService>();
                Assert.Equal(2, (await projects.ListAsync()).Count);
                Assert.Empty(await projects.ListHierarchyLinksAsync());
                await using var database = await services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>().CreateDbContextAsync();
                Assert.False(await database.Set<ProjectObjectRecord>().AnyAsync(row => row.ProjectId == created.Id && !row.IsSystemManaged));
                return true;
            });
            await Assertions.Expect(page.Locator("#blazor-error-ui")).Not.ToBeVisibleAsync();
        } catch {
            await page.ScreenshotAsync(new() { Path = host.Artifact("workbench-root-recovery-failure.png") });
            throw;
        } finally {
            await page.EvaluateAsync("() => window.restoreWorkbenchPersistenceFault?.()");
            await host.CaptureHostLogAsync();
        }
    }
}
