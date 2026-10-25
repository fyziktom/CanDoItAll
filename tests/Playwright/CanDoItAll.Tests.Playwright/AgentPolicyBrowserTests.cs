using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Playwright.Smoke;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

[Trait("Category", "Playwright")]
[Trait("Category", "HostPlatform")]
public sealed class AgentPolicyBrowserTests {
    [Fact]
    public async Task Native_catalog_policy_edit_retains_conflicting_draft_until_explicit_rebase() {
        await using var host = await LiveUiHost.StartAsync();
        var agent = await host.SeedAsync(async services =>
            (await services.GetRequiredService<IAgentFrameworkWorkspaceService>().ListAgentsAsync(false)).First());
        var page = await host.NewPageAsync();
        var oracle = CrmHrBrowserOracle.Attach(page);
        var passed = false;
        try {
            await oracle.NavigateAsync(host.BaseUrl + "/agents?tab=agents");
            await PlaywrightAppFixture.CompleteDatabaseStartupAsync(page);
            await page.GetByTestId("agents-catalog-search").FillAsync(agent.Name);
            var card = page.GetByTestId("agents-catalog-card").Filter(new() { HasText = agent.Name }).First;
            await card.ClickAsync();
            await page.GetByTestId("agents-catalog-policy").ClickAsync();
            var dialog = page.GetByTestId("agent-policy-dialog");
            await Assertions.Expect(dialog).ToContainTextAsync(agent.Name);
            await Assertions.Expect(dialog.GetByTestId("agent-policy-save")).ToBeEnabledAsync();
            await dialog.GetByTestId("agent-policy-read").SelectOptionAsync(nameof(ReadBasisMode.RunSnapshot));
            await dialog.GetByTestId("agent-policy-work").FillAsync("00:03:00");
            await dialog.GetByTestId("agent-policy-save").ClickAsync();
            await Assertions.Expect(dialog.GetByTestId("agent-policy-status")).ToContainTextAsync("Policy saved for future work", new() { Timeout = 45_000 });
            var accepted = await ReadAsync();
            Assert.Equal(ReadBasisMode.RunSnapshot, accepted.Policy!.ReadBasisMode);
            Assert.Equal(TimeSpan.FromMinutes(3), accepted.Policy.MaximumWorkDuration);
            await dialog.GetByTestId("agent-policy-work").FillAsync("00:04:00");
            await dialog.GetByTestId("agent-policy-close").ClickAsync();
            await page.GetByTestId("agents-catalog-policy").ClickAsync();
            await Assertions.Expect(dialog.GetByTestId("agent-policy-work")).ToHaveValueAsync("00:04:00");
            await host.SeedAsync(async services => {
                var workspace = services.GetRequiredService<IAgentFrameworkWorkspaceService>();
                var original = await workspace.ReadAgentDecisionDataPolicyAsync(agent.Id);
                Assert.IsType<AgentDecisionDataPolicySaveOutcome.Accepted>(await workspace.ReplaceAgentDecisionDataPolicyAsync(
                    new(Guid.NewGuid(), original.Expectation, new(ReadBasisMode.StepSnapshot, MaximumWorkDuration: TimeSpan.FromMinutes(2)))));
                return true;
            });
            await dialog.GetByTestId("agent-policy-save").ClickAsync();
            await Assertions.Expect(dialog.GetByTestId("agent-policy-status")).ToContainTextAsync("Policy was not saved", new() { Timeout = 45_000 });
            await Assertions.Expect(dialog.GetByTestId("agent-policy-save")).ToBeDisabledAsync();
            await Assertions.Expect(dialog.GetByTestId("agent-policy-work")).ToHaveValueAsync("00:04:00");
            Assert.Equal(TimeSpan.FromMinutes(2), (await ReadAsync()).Policy!.MaximumWorkDuration);
            await dialog.GetByTestId("agent-policy-read-current").ClickAsync();
            await Assertions.Expect(dialog.GetByTestId("agent-policy-comparison")).ToContainTextAsync("StepSnapshot");
            await ShotAsync("policy-conflict-comparison.png");
            await dialog.GetByTestId("agent-policy-keep-draft").ClickAsync();
            await Assertions.Expect(dialog.GetByTestId("agent-policy-save")).ToBeEnabledAsync();
            await dialog.GetByTestId("agent-policy-save").ClickAsync();
            await Assertions.Expect(dialog.GetByTestId("agent-policy-status")).ToContainTextAsync("Policy saved for future work", new() { Timeout = 45_000 });
            var rebased = await ReadAsync();
            Assert.Equal(ReadBasisMode.RunSnapshot, rebased.Policy!.ReadBasisMode);
            Assert.Equal(TimeSpan.FromMinutes(4), rebased.Policy.MaximumWorkDuration);
            Assert.NotEqual(accepted.Expectation, rebased.Expectation);
            await ShotAsync("policy-saved.png");
            await oracle.AssertCleanAsync();
            passed = true;

            Task<AgentDecisionDataPolicySnapshot> ReadAsync() => host.SeedAsync(services =>
                services.GetRequiredService<IAgentFrameworkWorkspaceService>().ReadAgentDecisionDataPolicyAsync(agent.Id));
            async Task ShotAsync(string name) {
                Assert.False(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth > document.documentElement.clientWidth"));
                await Assertions.Expect(dialog.GetByTestId("agent-policy-save")).ToBeInViewportAsync(new() { Ratio = 1 });
                await page.ScreenshotAsync(new() { Path = host.Artifact(name) });
            }
        } finally {
            await page.ScreenshotAsync(new() { Path = host.Artifact(passed ? "policy-final.png" : "policy-failure.png"), Timeout = 5_000 });
            await host.CaptureHostLogAsync();
            await File.WriteAllTextAsync(host.Artifact("policy-evidence.json"), JsonSerializer.Serialize(new {
                Passed = passed, Viewport = "1920x1080@1", RealCatalogAndPolicyOwner = true, agent.Id
            }));
        }
    }

    [Fact]
    public async Task Sandbox_inheritance_validation_comparison_and_unknown_outcome_keep_actions_readable() {
        await using var host = new RazorSandboxHost(RazorSandboxTarget.AgentChat);
        await host.StartAsync(published: false);
        using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        await using var context = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 1920, Height = 1080 }, DeviceScaleFactor = 1 });
        var page = await context.NewPageAsync();
        page.SetDefaultTimeout(30_000);
        var errors = new List<string>();
        page.PageError += (_, error) => errors.Add(error);
        var directory = Path.Combine(PlaywrightTestHostPaths.RepositoryRoot, "output", "playwright", "agent-policy-sandbox", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var passed = false;
        try {
            await page.GotoAsync(host.BaseUrl + "/agent-policy");
            var open = page.GetByTestId("policy-sample-open");
            await open.ClickAsync();
            var dialog = page.GetByTestId("agent-policy-dialog");
            await Assertions.Expect(dialog.GetByTestId("agent-policy-read")).ToHaveValueAsync("");
            await dialog.GetByTestId("agent-policy-work").FillAsync("invalid");
            await dialog.GetByTestId("agent-policy-save").ClickAsync();
            await Assertions.Expect(dialog.GetByTestId("agent-policy-status")).ToContainTextAsync("positive durations");
            await dialog.GetByTestId("agent-policy-work").FillAsync("00:05:00");
            await dialog.GetByTestId("agent-policy-save").ClickAsync();
            await dialog.GetByTestId("agent-policy-read-current").ClickAsync();
            await Assertions.Expect(dialog.GetByTestId("agent-policy-comparison")).ToBeVisibleAsync();
            await Assertions.Expect(dialog.GetByTestId("agent-policy-save")).ToBeInViewportAsync(new() { Ratio = 1 });
            Assert.False(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth > document.documentElement.clientWidth"));
            await page.ScreenshotAsync(new() { Path = Path.Combine(directory, "policy-comparison.png") });
            await dialog.GetByTestId("agent-policy-use-current").ClickAsync();
            await Assertions.Expect(dialog.GetByTestId("agent-policy-read")).ToHaveValueAsync(nameof(ReadBasisMode.StepSnapshot));
            await Assertions.Expect(dialog.GetByTestId("agent-policy-work")).ToHaveValueAsync("");
            await dialog.GetByTestId("agent-policy-close").FocusAsync();
            await page.Keyboard.PressAsync("Escape");
            await Assertions.Expect(dialog).ToHaveCountAsync(0);
            await Assertions.Expect(open).ToBeFocusedAsync();
            await page.GetByTestId("policy-sample-unconfirmed").ClickAsync();
            await dialog.GetByTestId("agent-policy-read-current").ClickAsync();
            await Assertions.Expect(dialog.GetByTestId("agent-policy-unconfirmed")).ToBeVisibleAsync();
            await Assertions.Expect(dialog.GetByTestId("agent-policy-save")).ToBeDisabledAsync();
            await Assertions.Expect(dialog.GetByTestId("agent-policy-keep-draft")).ToBeDisabledAsync();
            await page.ScreenshotAsync(new() { Path = Path.Combine(directory, "policy-unconfirmed.png") });
            Assert.Empty(errors);
            passed = true;
        } finally {
            await page.ScreenshotAsync(new() { Path = Path.Combine(directory, passed ? "final.png" : "failure.png"), Timeout = 5_000 });
            await File.WriteAllTextAsync(Path.Combine(directory, "evidence.json"), JsonSerializer.Serialize(new {
                Passed = passed, Viewport = "1920x1080@1", BackendFree = true, Errors = errors
            }));
        }
    }
}
