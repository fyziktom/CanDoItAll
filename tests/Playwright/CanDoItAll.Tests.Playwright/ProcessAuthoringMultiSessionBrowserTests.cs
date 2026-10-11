using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Projections;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

[Trait("Category", "Playwright")]
[Trait("Category", "HostPlatform")]
public sealed class ProcessAuthoringMultiSessionBrowserTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task Native_commit_response_and_readback_failures_have_one_effect_and_a_working_recovery(bool loseResponse)
        => RunAsync(loseResponse ? "unknown-commit" : "known-commit", async (page, browser, host, control) => {
            control.LoseFirstCommitResponse = loseResponse;
            control.FailReadAfterFirstCommit = !loseResponse;
            await page.GetByTestId("processes-definition-editor-name").FillAsync("Committed despite unavailable response");
            await page.GetByTestId("processes-definition-save").ClickAsync();
            if (loseResponse) {
                await Assertions.Expect(page.GetByTestId("processes-authoring-recover")).ToBeVisibleAsync();
                await Assertions.Expect(page.GetByTestId("processes-definition-save")).ToBeDisabledAsync();
                await page.GetByTestId("processes-refresh").ClickAsync();
                Assert.Equal(1, Volatile.Read(ref control.DefinitionCommandCount));
                await page.GetByTestId("processes-authoring-recover").ClickAsync();
            }
            await Assertions.Expect(page.GetByTestId("processes-definition-editor-receipt")).ToContainTextAsync("Accepted");
            await Assertions.Expect(page.GetByTestId("processes-definition-save")).ToBeEnabledAsync();
            await page.GetByTestId("processes-refresh").ClickAsync();
            Assert.Equal(loseResponse ? 2 : 1, Volatile.Read(ref control.DefinitionCommandCount));
            await Assertions.Expect(page.Locator("body")).Not.ToContainTextAsync("Private authoring");
            await using var independent = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 1920, Height = 1080 } });
            var reopened = await independent.NewPageAsync();
            await OpenAsync(reopened, host);
            await Assertions.Expect(reopened.GetByTestId("processes-definition-editor-name")).ToHaveValueAsync("Committed despite unavailable response");
            await host.ReadAsync(async services => {
                var saved = await services.GetRequiredService<ProcessAuthoringWorkspace>().ReadAsync(ProcessWorkspaceShellScope.Global, new(ProcessNativeBrowserHost.CompleteDefinition), default);
                Assert.Equal(1, saved.Revision);
                await using var database = await services.GetRequiredService<IDbContextFactory<ProcessPersistenceDbContext>>().CreateDbContextAsync();
                Assert.Single(await database.AuthoringReceipts.Where(item => item.OperationId == control.DefinitionCommand!.OperationId).ToArrayAsync());
                return true;
            });
        });

    [Fact]
    public Task Independent_editors_advance_clean_state_retain_dirty_baselines_and_recover_cross_family_conflicts()
        => RunAsync("two-editors", async (a, browser, host, control) => {
            await using var second = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 1920, Height = 1080 } });
            var b = await second.NewPageAsync();
            await OpenAsync(b, host);
            await SaveAsync(a, host, "First committed revision");
            await b.GetByTestId("processes-refresh").ClickAsync();
            await Assertions.Expect(b.GetByTestId("processes-definition-editor-name")).ToHaveValueAsync("First committed revision");
            await b.GetByTestId("processes-definition-editor-name").FillAsync("Unsaved local revision");
            await SaveAsync(a, host, "Second committed revision");
            await b.GetByTestId("processes-refresh").ClickAsync();
            await Assertions.Expect(b.GetByTestId("processes-definition-draft-conflict")).ToBeVisibleAsync();
            await Assertions.Expect(b.GetByTestId("processes-definition-editor-name")).ToHaveValueAsync("Unsaved local revision");
            await b.GetByTestId("processes-definition-discard").ClickAsync();
            await Assertions.Expect(b.GetByTestId("processes-definition-editor-name")).ToHaveValueAsync("Second committed revision");
            await b.GetByTestId("processes-detail-tab-steps").ClickAsync();
            await b.GetByTestId("processes-step-title").FillAsync("Stale cross-family title");
            await a.GetByTestId("processes-detail-tab-roles").ClickAsync();
            await a.GetByTestId("processes-role-second-owner").ClickAsync();
            await a.GetByTestId("processes-role-display-name").FillAsync("Newer role name");
            await a.GetByTestId("processes-role-save").ClickAsync();
            await Assertions.Expect(a.GetByTestId("processes-role-editor-receipt")).ToContainTextAsync("Accepted");
            await b.GetByTestId("processes-step-save").ClickAsync();
            await Assertions.Expect(b.GetByTestId("processes-step-command-receipt")).ToContainTextAsync("Rejected");
            await Assertions.Expect(b.GetByTestId("processes-step-title")).ToHaveValueAsync("Stale cross-family title");
            await b.GetByTestId("processes-step-discard").ClickAsync();
            await b.GetByTestId("processes-step-title").FillAsync("Reconciled step title");
            await b.GetByTestId("processes-step-save").ClickAsync();
            await Assertions.Expect(b.GetByTestId("processes-step-command-receipt")).ToContainTextAsync("Accepted");
            await host.ReadAsync(async services => {
                var saved = await services.GetRequiredService<ProcessAuthoringWorkspace>().ReadAsync(ProcessWorkspaceShellScope.Global, new(ProcessNativeBrowserHost.CompleteDefinition), default);
                Assert.Equal(4, saved.Revision);
                Assert.Equal("Second committed revision", saved.Content.Definition.DisplayName);
                Assert.Equal("Newer role name", saved.Content.Definition.RoleUsages.Single(role => role.Key == "second-owner").DisplayName);
                Assert.Equal("Reconciled step title", saved.Content.Definition.Steps[0].Title);
                return true;
            });
            await b.ScreenshotAsync(new() { Path = Path.Combine(host.Evidence, "reconciled-second-editor.png"), FullPage = true });
        });

    private static async Task SaveAsync(IPage page, ProcessNativeBrowserHost host, string name) {
        await page.GetByTestId("processes-definition-editor-name").FillAsync(name);
        await page.GetByTestId("processes-definition-save").ClickAsync();
        await Assertions.Expect(page.GetByTestId("processes-definition-editor-receipt")).ToContainTextAsync("Accepted");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        while (true) {
            var saved = await host.ReadAsync(provider => provider.GetRequiredService<ProcessAuthoringWorkspace>()
                .ReadAsync(ProcessWorkspaceShellScope.Global, new(ProcessNativeBrowserHost.CompleteDefinition), deadline.Token));
            if (saved.Content.Definition.DisplayName == name) {
                break;
            }
            await Task.Delay(100, deadline.Token);
        }
        await Assertions.Expect(page.GetByTestId("processes-definition-save")).ToBeEnabledAsync();
    }

    private static async Task OpenAsync(IPage page, ProcessNativeBrowserHost host) {
        var saved = await host.ReadAsync(provider => provider.GetRequiredService<ProcessAuthoringWorkspace>()
            .ReadAsync(ProcessWorkspaceShellScope.Global, new(ProcessNativeBrowserHost.CompleteDefinition), default));
        await page.GotoAsync(host.BaseUrl + "/processes");
        await PlaywrightAppFixture.CompleteDatabaseStartupAsync(page);
        await Assertions.Expect(page.GetByTestId("processes-page-scaffold")).ToHaveAttributeAsync("data-interactive", "true");
        var definition = page.GetByTestId("processes-definition-" + ProcessNativeBrowserHost.CompleteDefinition);
        if (await definition.GetAttributeAsync("aria-selected") != "true") {
            await definition.ClickAsync();
        }
        await Assertions.Expect(page.GetByTestId("processes-definition-editor-name")).ToHaveValueAsync(saved.Content.Definition.DisplayName);
    }

    private static async Task RunAsync(string name, Func<IPage, IBrowser, ProcessNativeBrowserHost, ProcessAuthoringBrowserControl, Task> action) {
        await using var wire = await AgentResponseFixture.StartAsync("No provider execution in authoring tests.");
        var control = new ProcessAuthoringBrowserControl();
        control.ReleaseDefinition.TrySetResult();
        await using var host = await ProcessNativeBrowserHost.StartAsync(wire.BaseUrl, authoring: control);
        using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        await using var context = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 1920, Height = 1080 }, DeviceScaleFactor = 1 });
        await context.Tracing.StartAsync(new() { Screenshots = true, Snapshots = true, Sources = true });
        var page = await context.NewPageAsync();
        var errors = new List<string>();
        page.PageError += (_, error) => errors.Add(error);
        try {
            await OpenAsync(page, host);
            await action(page, browser, host, control);
            Assert.Empty(errors);
            await page.ScreenshotAsync(new() { Path = Path.Combine(host.Evidence, name + ".png"), FullPage = true });
        } catch {
            await page.ScreenshotAsync(new() { Path = Path.Combine(host.Evidence, name + "-failure.png"), FullPage = true });
            throw;
        } finally {
            await context.Tracing.StopAsync(new() { Path = Path.Combine(host.Evidence, name + ".zip") });
        }
    }
}
