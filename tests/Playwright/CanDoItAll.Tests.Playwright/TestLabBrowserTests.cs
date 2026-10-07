using System.Text.RegularExpressions;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.TestLab;
using CanDoItAll.Tests.Playwright.TestLab;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright.Smoke;

[Collection(PlaywrightCollection.Name)]
[Trait("Category", "Playwright")]
[Trait("Category", "HostPlatform")]
public sealed class TestLabBrowserTests(PlaywrightAppFixture fixture) {
    private static readonly string Artifacts = Path.Combine(PlaywrightTestHostPaths.RepositoryRoot, "output", "playwright", "testlab-ui");

    [Fact]
    public async Task Production_all_sections_save_reopen_filters_global_and_historical_references_use_real_owners() {
        Directory.CreateDirectory(Artifacts);
        await using var provider = await ProviderAsync(fixture.OwnedDatabaseProfile);
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var admission = await OwnerPostcommitTestProbe.CreateProjectAsync(services, "Browser TestLab project");
        var party = await services.GetRequiredService<PartyDirectoryService>().SavePartyAsync(new() {
            DisplayName = "TestLab browser reviewer", PartyType = PartyType.Person, LifecycleStatus = PartyLifecycleStatus.Active,
            LastChangedBy = "testlab-browser-proof"
        });
        Assert.True(party.IsSuccess);
        var owner = services.GetRequiredService<TestLabService>();
        var unrelated = await owner.SaveAsync(new() { Title = $"Unrelated TestLab reset {Guid.NewGuid():N}" });
        Assert.True(unrelated.IsSuccess);
        await using var context = await fixture.Browser.NewContextAsync(new() { ViewportSize = new() { Width = 1600, Height = 1000 } });
        var page = await context.NewPageAsync();
        var errors = Observe(page);
        await OpenAsync(page, $"{fixture.BaseUrl}/test-lab?projectId={admission.ProjectId:D}");
        await Assertions.Expect(page.GetByTestId("testlab-project-select")).ToHaveValueAsync(admission.ProjectId.ToString());
        await page.GetByTestId("testlab-responsible-party-select").SelectOptionAsync(party.Value.ToString());
        var title = $"Browser TestLab {Guid.NewGuid():N}";
        await FillAggregateAsync(page, title);
        await page.GetByTestId("testlab-save-button").ClickAsync();
        var id = await SavedIdAsync(page, "Saved");
        var saved = await owner.GetAsync(id);
        Assert.Equal(admission, saved.ExpectedProjectAdmission);
        Assert.Equal(party.Value, saved.ResponsiblePartyId);
        Assert.Equal(title, saved.Title);
        Assert.Equal("Browser phase", saved.Phase);
        Assert.Equal("Complete browser coverage", saved.CoverageGoal);
        Assert.Equal("tests/browser/testlab.spec.ts", saved.PlaywrightSpecPath);
        Assert.Equal("Create and reopen", Assert.Single(saved.Cases).Name);
        Assert.Equal("Stable aggregate", saved.Cases[0].StoryOrFeature);
        Assert.Equal(TestCaseStatus.Passed, saved.Cases[0].Status);
        Assert.Equal("Case notes", saved.Cases[0].Notes);
        Assert.Equal("Browser screenshot", Assert.Single(saved.Evidence).EvidenceLabel);
        Assert.Equal("artifacts/testlab/accepted.png", saved.Evidence[0].ArtifactPath);
        Assert.Equal("Screenshot", saved.Evidence[0].EvidenceKind);
        Assert.Equal("Evidence notes", saved.Evidence[0].Notes);
        Assert.Equal("2026-09-28T12:34:56.1234560-04:00", Assert.Single(saved.Runs).ExecutedAtUtc.ToOffset(TimeSpan.FromHours(-4)).ToString("O"));
        Assert.Equal("Browser Playwright", saved.Runs[0].Runner);
        Assert.Equal(TestCaseStatus.Passed, saved.Runs[0].Result);
        Assert.Equal("Run notes", saved.Runs[0].Summary);
        var childIds = Children(saved);
        Assert.All(childIds, child => Assert.NotEqual(Guid.Empty, child));
        await ScreenshotAsync(page, "production-runs");
        await OpenAsync(page, $"{fixture.BaseUrl}/test-lab?planId={id:D}");
        await Assertions.Expect(page.GetByTestId("testlab-title-input")).ToHaveValueAsync(title);
        await page.GetByTestId("testlab-title-input").FillAsync(title + " edited");
        await TabAsync(page, "Cases");
        await TabAsync(page, "Overview");
        await Assertions.Expect(page.GetByTestId("testlab-title-input")).ToHaveValueAsync(title + " edited");
        await page.GetByTestId("testlab-save-button").ClickAsync();
        Assert.Equal(id, await SavedIdAsync(page, "Saved"));
        Assert.Equal(childIds, Children(await owner.GetAsync(id)));
        Assert.Single(await owner.ListAsync(), item => item.Title.StartsWith(title, StringComparison.Ordinal));
        Assert.Equal(title + " edited", (await owner.GetAsync(id)).Title);
        await page.GetByTestId("testlab-project-filter").SelectOptionAsync(admission.ProjectId.ToString());
        await page.GetByTestId("testlab-phase-filter").SelectOptionAsync("Browser phase");
        await page.GetByTestId("testlab-result-filter").SelectOptionAsync(nameof(TestCaseStatus.Passed));
        await Assertions.Expect(page.GetByTestId("testlab-list-state")).ToHaveTextAsync("1 matching plans");
        await page.GetByTestId("testlab-search").FillAsync("no matching result");
        await Assertions.Expect(page.GetByTestId("testlab-list-state")).ToHaveTextAsync("0 matching plans");
        await page.GetByRole(AriaRole.Button, new() { Name = "Reset", Exact = true }).First.ClickAsync();
        var allPlans = await owner.ListAsync();
        Assert.Contains(allPlans, plan => plan.Id == id);
        Assert.Contains(allPlans, plan => plan.Id == unrelated.Value);
        await Assertions.Expect(page.GetByTestId("testlab-project-filter")).ToHaveValueAsync(string.Empty);
        await Assertions.Expect(page.GetByTestId("testlab-phase-filter")).ToHaveValueAsync(string.Empty);
        await Assertions.Expect(page.GetByTestId("testlab-result-filter")).ToHaveValueAsync(string.Empty);
        await Assertions.Expect(page.GetByTestId("testlab-search")).ToHaveValueAsync(string.Empty);
        await Assertions.Expect(page.GetByTestId("testlab-list-state")).ToHaveTextAsync($"{allPlans.Count} matching plans");
        foreach (var plan in allPlans) {
            await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex(Regex.Escape(plan.Title)) })).ToBeVisibleAsync();
        }
        await ScreenshotAsync(page, "production-overview");

        await OpenAsync(page, $"{fixture.BaseUrl}/test-lab");
        await page.GetByTestId("testlab-title-input").FillAsync("Browser global plan");
        await page.GetByTestId("testlab-save-button").ClickAsync();
        var global = await owner.GetAsync(await SavedIdAsync(page, "Saved"));
        Assert.Null(global.ProjectId);
        Assert.Null(global.ExpectedProjectAdmission);
        await using (var database = await services.GetRequiredService<IDbContextFactory<TestLabDbContext>>().CreateDbContextAsync()) {
            var historical = await database.Set<TestPlan>().SingleAsync(item => item.Id == id);
            historical.ProjectLifetimeId = Guid.NewGuid();
            historical.ResponsiblePartyId = Guid.NewGuid();
            await database.SaveChangesAsync();
        }
        await OpenAsync(page, $"{fixture.BaseUrl}/test-lab?planId={id:D}");
        await Assertions.Expect(page.GetByTestId("testlab-project-select").Locator("option:checked")).ToContainTextAsync("Unavailable historical project");
        await Assertions.Expect(page.GetByTestId("testlab-responsible-party-select").Locator("option:checked")).ToContainTextAsync("Unavailable responsible party");
        await ScreenshotAsync(page, "production-historical");
        await page.GotoAsync($"{fixture.BaseUrl}/collaboration");
        await CollaborationBrowserTests.ReadyAsync(page);
        await page.GotoAsync($"{fixture.BaseUrl}/test-lab?planId={Guid.NewGuid():D}");
        await Assertions.Expect(page.GetByTestId("testlab-editor-state")).ToContainTextAsync("Requested plan was not found");
        await Assertions.Expect(page.GetByTestId("testlab-title-input")).ToHaveCountAsync(0);
        Assert.Empty(errors);
        var logs = fixture.GetLogSnapshot(2000);
        await File.WriteAllTextAsync(Path.Combine(Artifacts, "production-server.log"), logs);
        Assert.DoesNotContain("Unhandled exception", logs, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ObjectDisposedException", logs, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Production_controlled_readback_and_activity_failure_preserve_real_commits_and_unblurred_input() {
        Directory.CreateDirectory(Artifacts);
        await using var host = new TestLabBrowserHost(sandbox: false);
        try {
            await host.ReadyAsync(sandbox: false);
            await using var provider = await ProviderAsync(host.Profile!);
            await using var scope = provider.CreateAsyncScope();
            var owner = scope.ServiceProvider.GetRequiredService<TestLabService>();
            await using var context = await fixture.Browser.NewContextAsync(new() { ViewportSize = new() { Width = 1600, Height = 1000 } });
            var page = await context.NewPageAsync();
            var errors = Observe(page);
            await OpenAsync(page, host.BaseUrl + "/test-lab");
            await FillAggregateAsync(page, "  Controlled browser submission  ");
            await TabAsync(page, "Overview");
            await host.CommandAsync(TestLabProbeCommand.HoldReadback);
            await page.GetByTestId("testlab-save-button").ClickAsync();
            await host.ObserveAsync(TestLabProbeProtocol.ReadHeld);
            await page.GetByTestId("testlab-title-input").FillAsync("Newer text before blur");
            await Assertions.Expect(page.GetByTestId("testlab-title-input")).ToBeFocusedAsync();
            await host.CommandAsync(TestLabProbeCommand.Release);
            var id = await SavedIdAsync(page, "Saved");
            await Assertions.Expect(page.GetByTestId("testlab-title-input")).ToHaveValueAsync("Newer text before blur");
            await Assertions.Expect(page.GetByTestId("testlab-title-input")).ToBeFocusedAsync();
            Assert.Equal("Controlled browser submission", (await owner.GetAsync(id)).Title);
            await ScreenshotAsync(page, "production-retained-input");
            await page.GetByRole(AriaRole.Button, new() { Name = "Reset", Exact = true }).Last.ClickAsync();
            await Assertions.Expect(page.GetByTestId("testlab-title-input")).ToHaveValueAsync(string.Empty);
            await FillAggregateAsync(page, "Committed activity warning");
            await host.CommandAsync(TestLabProbeCommand.FailActivity);
            await page.GetByTestId("testlab-save-button").ClickAsync();
            var warningId = await SavedIdAsync(page, "SavedWithWarning");
            var accepted = await owner.GetAsync(warningId);
            Assert.Equal("Committed activity warning", accepted.Title);
            Assert.All(Children(accepted), child => Assert.NotEqual(Guid.Empty, child));
            await ScreenshotAsync(page, "production-committed-warning");
            await page.GetByTestId("testlab-retry").ClickAsync();
            Assert.Equal(warningId, await SavedIdAsync(page, "Saved"));
            Assert.Equal(Children(accepted), Children(await owner.GetAsync(warningId)));
            Assert.Equal(2, (await owner.ListAsync()).Count);
            Assert.Empty(errors);
            Assert.DoesNotContain("Unhandled exception", host.Logs, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("ObjectDisposedException", host.Logs, StringComparison.Ordinal);
        } finally {
            await host.DisposeAsync();
            await File.WriteAllTextAsync(Path.Combine(Artifacts, "controlled-server.log"), host.Logs);
            Assert.DoesNotContain("ObjectDisposedException", host.Logs, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Standalone_sandbox_real_sections_scenarios_delays_and_assets_work_without_database() {
        Directory.CreateDirectory(Artifacts);
        await using var host = new TestLabBrowserHost(sandbox: true);
        try {
            await host.ReadyAsync(sandbox: true);
            await using var context = await fixture.Browser.NewContextAsync(new() { ViewportSize = new() { Width = 1600, Height = 1000 } });
            var page = await context.NewPageAsync();
            var errors = Observe(page);
            await page.GotoAsync(host.BaseUrl);
            await Assertions.Expect(page.GetByTestId("testlab-save-button")).ToBeEnabledAsync();
            Assert.Equal("Parity", await page.Locator("html").GetAttributeAsync("data-asset-mode"));
            await ScreenshotAsync(page, "sandbox-overview");
            foreach (var section in new[] { "Cases", "Evidence", "Runs", "Overview" }) {
                await TabAsync(page, section);
                await ScreenshotAsync(page, "sandbox-" + section.ToLowerInvariant());
            }
            var savedParty = await page.GetByTestId("testlab-responsible-party-select").InputValueAsync();
            await page.GetByTestId("testlab-project-select").SelectOptionAsync(string.Empty);
            await Assertions.Expect(page.GetByTestId("testlab-responsible-party-select")).ToHaveValueAsync(savedParty);
            await Assertions.Expect(page.GetByTestId("testlab-responsible-party-select").Locator("option:checked")).ToHaveTextAsync("Delivery reviewer");
            await Assertions.Expect(page.GetByTestId("testlab-sandbox-state")).ToHaveTextAsync("0 pending · 0 committed");
            await ScreenshotAsync(page, "sandbox-global-party");
            foreach (var (scenario, observation) in new[] {
                ("Empty", "No saved plans."), ("FilteredEmpty", "0 matching plans"),
                ("ReadFailure", "Plans unavailable"), ("StaleRefresh", "Previously loaded plans"), ("Large", "250 matching plans")
            }) {
                await page.GetByTestId("testlab-scenario").SelectOptionAsync(scenario);
                await Assertions.Expect(page.GetByTestId("testlab-list-state")).ToContainTextAsync(observation);
            }
            await page.GetByTestId("testlab-scenario").SelectOptionAsync("MissingPlan");
            await Assertions.Expect(page.GetByTestId("testlab-editor-state")).ToContainTextAsync("not found");
            await page.GetByTestId("testlab-scenario").SelectOptionAsync("MissingReferences");
            await Assertions.Expect(page.GetByTestId("testlab-responsible-party-select").Locator("option:checked")).ToContainTextAsync("Unavailable");
            await ScreenshotAsync(page, "sandbox-missing-party");
            await page.GetByTestId("testlab-scenario").SelectOptionAsync("ReferenceFailure");
            await Assertions.Expect(page.GetByTestId("testlab-references-state")).ToContainTextAsync("Responsible parties are unavailable");
            await page.GetByTestId("testlab-scenario").SelectOptionAsync("InvalidEditor");
            await page.GetByTestId("testlab-save-button").ClickAsync();
            await Assertions.Expect(page.GetByText("Title is required.", new() { Exact = true })).ToBeVisibleAsync();
            await page.GetByTestId("testlab-scenario").SelectOptionAsync("DelayedReadback");
            await Assertions.Expect(page.GetByTestId("testlab-save-state")).ToHaveAttributeAsync("data-state", "Ready");
            await page.GetByTestId("testlab-save-button").ClickAsync();
            await Assertions.Expect(page.GetByTestId("testlab-sandbox-state")).ToHaveTextAsync("1 pending · 1 committed");
            await Assertions.Expect(page.GetByTestId("testlab-save-state")).ToHaveAttributeAsync("data-state", "Pending");
            var pendingPlanId = await page.GetByTestId("testlab-save-state").GetAttributeAsync("data-plan-id");
            await page.GetByTestId("testlab-responsible-party-select").SelectOptionAsync(string.Empty);
            await Assertions.Expect(page.GetByTestId("testlab-responsible-party-select")).ToHaveValueAsync(string.Empty);
            await Assertions.Expect(page.GetByTestId("testlab-sandbox-state")).ToHaveTextAsync("1 pending · 1 committed");
            await page.GetByTestId("testlab-title-input").FillAsync("Sandbox newer title");
            await Assertions.Expect(page.GetByTestId("testlab-title-input")).ToBeFocusedAsync();
            await page.GetByTestId("testlab-complete-pending").DispatchEventAsync("click");
            Assert.Equal(Guid.Parse(pendingPlanId!), await SavedIdAsync(page, "Saved"));
            await Assertions.Expect(page.GetByTestId("testlab-title-input")).ToHaveValueAsync("Sandbox newer title");
            await Assertions.Expect(page.GetByTestId("testlab-title-input")).ToBeFocusedAsync();
            await Assertions.Expect(page.GetByTestId("testlab-responsible-party-select")).ToHaveValueAsync(string.Empty);
            await Assertions.Expect(page.GetByTestId("testlab-save-button")).ToBeEnabledAsync();
            await Assertions.Expect(page.GetByTestId("testlab-sandbox-state")).ToHaveTextAsync("0 pending · 1 committed");
            await ScreenshotAsync(page, "sandbox-party-readback");
            await page.GetByTestId("testlab-scenario").SelectOptionAsync("CommittedWarning");
            await page.GetByTestId("testlab-save-button").ClickAsync();
            await SavedIdAsync(page, "SavedWithWarning");
            await page.GetByTestId("testlab-retry").ClickAsync();
            await SavedIdAsync(page, "Saved");
            await Assertions.Expect(page.GetByTestId("testlab-sandbox-state")).ToHaveTextAsync("0 pending · 1 committed");
            await ScreenshotAsync(page, "sandbox-refreshed");
            Assert.False(await page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth > innerWidth"));
            Assert.True(await page.EvaluateAsync<bool>("() => [...document.styleSheets].some(s => s.href?.includes('CanDoItAll.Components.BaseLib'))"));
            Assert.Empty(errors);
        } finally {
            await host.DisposeAsync();
            await File.WriteAllTextAsync(Path.Combine(Artifacts, "sandbox-server.log"), host.Logs);
            Assert.DoesNotContain("Unhandled exception", host.Logs, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static async Task FillAggregateAsync(IPage page, string title) {
        await page.GetByTestId("testlab-title-input").FillAsync(title);
        await page.GetByTestId("testlab-plan-phase").FillAsync("  Browser phase  ");
        await page.GetByTestId("testlab-plan-coveragegoal").FillAsync("Complete browser coverage");
        await page.GetByTestId("testlab-plan-playwrightspecpath").FillAsync("tests/browser/testlab.spec.ts");
        await TabAsync(page, "Cases");
        await page.GetByRole(AriaRole.Button, new() { Name = "Add case", Exact = true }).ClickAsync();
        await page.GetByTestId("testlab-case-name").FillAsync("Create and reopen");
        await page.GetByTestId("testlab-case-storyorfeature").FillAsync("Stable aggregate");
        await page.GetByTestId("testlab-case-status").SelectOptionAsync(nameof(TestCaseStatus.Passed));
        await page.GetByTestId("testlab-case-notes").FillAsync("Case notes");
        await TabAsync(page, "Evidence");
        await page.GetByRole(AriaRole.Button, new() { Name = "Add evidence", Exact = true }).ClickAsync();
        await page.GetByTestId("testlab-evidence-evidencelabel").FillAsync("Browser screenshot");
        await page.GetByTestId("testlab-evidence-artifactpath").FillAsync("artifacts/testlab/accepted.png");
        await page.GetByTestId("testlab-evidence-notes").FillAsync("Evidence notes");
        await TabAsync(page, "Runs");
        await page.GetByRole(AriaRole.Button, new() { Name = "Add run", Exact = true }).ClickAsync();
        await page.GetByTestId("testlab-run-timestamp").FillAsync("2026-09-28T12:34:56.1234560-04:00");
        await page.GetByTestId("testlab-run-runner").FillAsync("Browser Playwright");
        await page.GetByTestId("testlab-run-result").SelectOptionAsync(nameof(TestCaseStatus.Passed));
        await page.GetByTestId("testlab-run-summary").FillAsync("Run notes");
    }

    private static Task<ServiceProvider> ProviderAsync(TestDatabaseProfile profile) => TestApplicationBootstrap.BuildServiceProviderAsync(
        profile, "TestLab.Browser.Readback", TestSchemaBootstrapModules.Full, new Dictionary<string, string?> { ["DevelopmentManager:TuningModeEnabled"] = "false" });
    private static Guid[] Children(TestPlanEditorModel plan) => plan.Cases.Select(item => item.Id!.Value)
        .Concat(plan.Evidence.Select(item => item.Id!.Value)).Concat(plan.Runs.Select(item => item.Id!.Value)).ToArray();
    private static async Task TabAsync(IPage page, string label) {
        var tab = page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("^" + label) });
        await tab.ClickAsync();
        await Assertions.Expect(page.GetByTestId("testlab-workspace")).ToHaveAttributeAsync("data-section", label);
        await Assertions.Expect(tab).ToHaveClassAsync(new Regex("\\bbg-slate-900\\b"));
    }
    private static async Task OpenAsync(IPage page, string url) {
        await page.GotoAsync(url);
        await PlaywrightAppFixture.CompleteDatabaseStartupAsync(page);
        await Assertions.Expect(page.GetByTestId("testlab-workspace")).ToHaveAttributeAsync("data-interactive", "true", new() { Timeout = 30_000 });
        await Assertions.Expect(page.GetByTestId("testlab-title-input")).ToBeVisibleAsync(new() { Timeout = 30_000 });
    }
    private static async Task<Guid> SavedIdAsync(IPage page, string state) {
        var status = page.GetByTestId("testlab-save-state");
        await Assertions.Expect(status).ToHaveAttributeAsync("data-state", state, new() { Timeout = 30_000 });
        return Guid.Parse((await status.GetAttributeAsync("data-plan-id"))!);
    }
    private static Task ScreenshotAsync(IPage page, string name) => page.ScreenshotAsync(new() { Path = Path.Combine(Artifacts, name + ".png") });
    private static List<string> Observe(IPage page) {
        var errors = new List<string>();
        page.PageError += (_, message) => errors.Add(message);
        page.Console += (_, message) => {
            if (message.Type == "error") {
                errors.Add(message.Text);
            }
        };
        page.Response += (_, response) => {
            if (response.Status >= 400 && response.Request.ResourceType is "stylesheet" or "script" or "font") {
                errors.Add($"Asset {response.Status}: {response.Url}");
            }
        };
        page.RequestFailed += (_, request) => {
            if (request.ResourceType is "stylesheet" or "script" or "font") {
                errors.Add("Failed asset: " + request.Url);
            }
        };
        return errors;
    }
}
