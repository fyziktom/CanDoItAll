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
public sealed class AgentPolicyRecoveryBrowserTests {
    [Fact]
    public async Task Native_producer_retains_before_dispatch_and_reload_recovers_historical_receipt_without_resend() {
        await using var host = await LiveUiHost.StartAsync();
        var agent = await host.SeedAsync(async services =>
            (await services.GetRequiredService<IAgentFrameworkWorkspaceService>().ListAgentsAsync(false)).First());
        var page = await host.NewPageAsync();
        var other = await page.Context.NewPageAsync();
        var oracle = CrmHrBrowserOracle.Attach(page);
        var otherOracle = CrmHrBrowserOracle.Attach(other);
        const string moduleRoute = "**/js/agentDecisionPolicyPending*.js";
        var interceptedModules = 0;
        await page.RouteAsync(moduleRoute, async route => {
            Interlocked.Increment(ref interceptedModules);
            var response = await route.FetchAsync();
            var source = (await response.TextAsync())
                .Replace("export function beginDispatch(", "function actualBeginDispatch(", StringComparison.Ordinal)
                .Replace("export function recordOutcome(", "function actualRecordOutcome(", StringComparison.Ordinal);
            await route.FulfillAsync(new() { Response = response, Body = source + """

                export async function beginDispatch(...args) {
                    const result = await actualBeginDispatch(...args);
                    if (result.changed && window.holdPolicyDispatch) {
                        window.policyDispatchWaiting = true;
                        await new Promise(resolve => { window.releasePolicyDispatch = resolve; });
                    }
                    return result;
                }
                export function recordOutcome(...args) {
                    if (window.failPolicyOutcome) {
                        window.policyOutcomeFailures = (window.policyOutcomeFailures || 0) + 1;
                        throw new Error('Injected terminal browser persistence failure');
                    }
                    return actualRecordOutcome(...args);
                }
                """ });
        });
        var passed = false;
        try {
            await OpenAsync(page);
            Assert.Equal(1, interceptedModules);
            await OpenAsync(other);
            var dialog = page.GetByTestId("agent-policy-dialog");
            var second = other.GetByTestId("agent-policy-dialog");
            await Assertions.Expect(dialog.GetByTestId("agent-policy-save")).ToBeEnabledAsync();
            await Assertions.Expect(second.GetByTestId("agent-policy-save")).ToBeEnabledAsync();
            var before = await ReadAsync();
            await page.EvaluateAsync("() => { window.holdPolicyDispatch = true; window.failPolicyOutcome = true; }");
            await dialog.GetByTestId("agent-policy-read").SelectOptionAsync(nameof(ReadBasisMode.RunSnapshot));
            await dialog.GetByTestId("agent-policy-work").FillAsync("00:03:00");
            await dialog.GetByTestId("agent-policy-save").ClickAsync();
            await page.WaitForFunctionAsync("window.policyDispatchWaiting === true");
            var retained = await RetainedAsync(page);
            Assert.Equal(1, retained.GetProperty("stage").GetInt32());
            var operation = retained.GetProperty("operationId").GetString()!;
            Assert.Equal(before, await ReadAsync());
            await second.GetByTestId("agent-policy-work").FillAsync("00:05:00");
            await second.GetByTestId("agent-policy-save").ClickAsync();
            await Assertions.Expect(second.GetByTestId("agent-policy-unconfirmed")).ToContainTextAsync(operation);
            await second.GetByTestId("agent-policy-recover").ClickAsync();
            await Assertions.Expect(second.GetByTestId("agent-policy-status")).ToContainTextAsync("does not prove");
            await Assertions.Expect(second.GetByTestId("agent-policy-work")).ToHaveValueAsync("00:05:00");
            Assert.Equal(before, await ReadAsync());
            await page.EvaluateAsync("window.releasePolicyDispatch()");
            await Assertions.Expect(dialog.GetByTestId("agent-policy-status")).ToContainTextAsync("recovery could not finish");
            Assert.Equal(1, await page.EvaluateAsync<int>("window.policyOutcomeFailures"));
            var accepted = await ReadAsync();
            Assert.Equal(before.Expectation.CatalogRevision.Next(), accepted.Expectation.CatalogRevision);
            Assert.Equal(TimeSpan.FromMinutes(3), accepted.Policy!.MaximumWorkDuration);
            Assert.Equal(retained.GetProperty("payload").GetString(), (await RetainedAsync(page)).GetProperty("payload").GetString());
            await host.SeedAsync(async services => {
                var workspace = services.GetRequiredService<IAgentFrameworkWorkspaceService>();
                Assert.IsType<AgentDecisionDataPolicySaveOutcome.Accepted>(await workspace.ReplaceAgentDecisionDataPolicyAsync(
                    new(Guid.NewGuid(), accepted.Expectation, new(ReadBasisMode.StepSnapshot, MaximumWorkDuration: TimeSpan.FromMinutes(2)))));
                return true;
            });
            var current = await ReadAsync();
            await page.UnrouteAsync(moduleRoute);
            await OpenAsync(page);
            Assert.False(await page.EvaluateAsync<bool>("'policyDispatchWaiting' in window"));
            await Assertions.Expect(dialog.GetByTestId("agent-policy-unconfirmed")).ToContainTextAsync(operation);
            await Assertions.Expect(dialog.GetByTestId("agent-policy-work")).ToHaveValueAsync("00:03:00");
            await dialog.GetByTestId("agent-policy-work").FillAsync("00:06:00");
            await dialog.GetByTestId("agent-policy-recover").ClickAsync();
            await Assertions.Expect(dialog.GetByTestId("agent-policy-historical")).ToContainTextAsync("RunSnapshot");
            await Assertions.Expect(dialog.GetByTestId("agent-policy-historical")).ToContainTextAsync("00:03:00");
            await Assertions.Expect(dialog.GetByTestId("agent-policy-work")).ToHaveValueAsync("00:06:00");
            await Assertions.Expect(dialog.GetByTestId("agent-policy-save")).ToBeDisabledAsync();
            Assert.Equal(current, await ReadAsync());
            Assert.Equal(JsonValueKind.Null, (await RetainedAsync(page)).ValueKind);
            await dialog.GetByTestId("agent-policy-read-current").ClickAsync();
            await Assertions.Expect(dialog.GetByTestId("agent-policy-comparison")).ToContainTextAsync("StepSnapshot");
            await Assertions.Expect(dialog.GetByTestId("agent-policy-save")).ToBeInViewportAsync(new() { Ratio = 1 });
            Assert.False(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth > document.documentElement.clientWidth"));
            await page.ScreenshotAsync(new() { Path = host.Artifact("agent-policy-historical-recovery.png") });
            await dialog.GetByTestId("agent-policy-keep-draft").ClickAsync();
            await Assertions.Expect(dialog.GetByTestId("agent-policy-save")).ToBeEnabledAsync();
            await Assertions.Expect(dialog.GetByTestId("agent-policy-work")).ToHaveValueAsync("00:06:00");
            Assert.Equal(current, await ReadAsync());
            await oracle.AssertCleanAsync();
            await otherOracle.AssertCleanAsync();
            passed = true;
        } finally {
            await page.ScreenshotAsync(new() { Path = host.Artifact(passed ? "agent-policy-recovery-final.png" : "agent-policy-recovery-failure.png"), Timeout = 5_000 });
            await host.CaptureHostLogAsync();
            await File.WriteAllTextAsync(host.Artifact("agent-policy-recovery-evidence.json"), JsonSerializer.Serialize(new {
                Passed = passed, Viewport = "1920x1080@1", RealNativeProducer = true, TerminalBrowserFault = true,
                TwoTabs = true, BrowserReload = true, HostRestart = false, agent.Id,
                ExpectedNavigationTeardown = oracle.ExpectedTeardown, OtherTabExpectedTeardown = otherOracle.ExpectedTeardown
            }));
        }
        Task<AgentDecisionDataPolicySnapshot> ReadAsync() => host.SeedAsync(services =>
            services.GetRequiredService<IAgentFrameworkWorkspaceService>().ReadAgentDecisionDataPolicyAsync(agent.Id));
        async Task OpenAsync(IPage target) {
            await (target == page ? oracle : otherOracle).NavigateAsync(host.BaseUrl + "/agents?tab=agents");
            await PlaywrightAppFixture.CompleteDatabaseStartupAsync(target);
            await Assertions.Expect(target.GetByTestId("agents-policy-recovery")).ToBeVisibleAsync();
            await target.GetByTestId("agents-catalog-search").FillAsync(agent.Name);
            await target.GetByTestId("agents-catalog-card").Filter(new() { HasText = agent.Name }).First.ClickAsync();
            await target.GetByTestId("agents-catalog-policy").ClickAsync();
            await Assertions.Expect(target.GetByTestId("agent-policy-read")).ToBeVisibleAsync();
        }
    }

    [Fact]
    public async Task Deleted_agent_receipt_recovers_from_catalog_after_an_actual_owned_host_restart() {
        await using var host = await LiveUiHost.StartAsync();
        var (agentId, siblingId) = await host.SeedAsync(async services => {
            var workspace = services.GetRequiredService<IAgentFrameworkWorkspaceService>();
            var seed = (await workspace.ListAgentsAsync(false)).First();
            return (await workspace.CloneAgentAsync(seed.Id, "Retained policy recovery"), seed.Id);
        });
        var page = await host.NewPageAsync();
        var oracle = CrmHrBrowserOracle.Attach(page);
        const string moduleRoute = "**/js/agentDecisionPolicyPending*.js";
        await page.RouteAsync(moduleRoute, async route => {
            var response = await route.FetchAsync();
            var source = (await response.TextAsync()).Replace("export function recordOutcome(", "function actualRecordOutcome(", StringComparison.Ordinal);
            await route.FulfillAsync(new() { Response = response, Body = source + """

                export function recordOutcome(...args) {
                    window.policyOutcomeFailures = (window.policyOutcomeFailures || 0) + 1;
                    throw new Error('Injected terminal browser persistence failure');
                }
                """ });
        });
        var passed = false;
        PlaywrightAppFixture.OwnedHostRestart? restart = null;
        try {
            await oracle.NavigateAsync(host.BaseUrl + "/agents?tab=agents");
            await PlaywrightAppFixture.CompleteDatabaseStartupAsync(page);
            await page.GetByTestId("agents-catalog-search").FillAsync("Retained policy recovery");
            await page.GetByTestId("agents-catalog-card").Filter(new() { HasText = "Retained policy recovery" }).ClickAsync();
            await page.GetByTestId("agents-catalog-policy").ClickAsync();
            var dialog = page.GetByTestId("agent-policy-dialog");
            await Assertions.Expect(dialog.GetByTestId("agent-policy-save")).ToBeEnabledAsync();
            await dialog.GetByTestId("agent-policy-read").SelectOptionAsync(nameof(ReadBasisMode.RunSnapshot));
            await dialog.GetByTestId("agent-policy-work").FillAsync("00:03:00");
            await dialog.GetByTestId("agent-policy-save").ClickAsync();
            await Assertions.Expect(dialog.GetByTestId("agent-policy-status")).ToContainTextAsync("recovery could not finish");
            Assert.Equal(1, await page.EvaluateAsync<int>("window.policyOutcomeFailures"));
            var retained = await RetainedAsync(page);
            Assert.Equal(1, retained.GetProperty("stage").GetInt32());
            var operation = retained.GetProperty("operationId").GetString()!;
            await host.SeedAsync(async services => {
                var workspace = services.GetRequiredService<IAgentFrameworkWorkspaceService>();
                Assert.Equal(TimeSpan.FromMinutes(3), (await workspace.ReadAgentDecisionDataPolicyAsync(agentId)).Policy!.MaximumWorkDuration);
                await workspace.DeleteAgentAsync(agentId);
                Assert.DoesNotContain(await workspace.ListAgentsAsync(false), item => item.Id == agentId);
                return true;
            });
            var catalogBeforeRecovery = await ReadSiblingAsync();
            await page.UnrouteAsync(moduleRoute);
            await oracle.NavigateAsync("about:blank");
            restart = await host.RestartOwnedApplicationAsync();
            Assert.NotEqual(restart.PreviousStartedUtc, restart.CurrentStartedUtc);
            await oracle.NavigateAsync(host.BaseUrl + "/agents?tab=agents");
            await PlaywrightAppFixture.CompleteDatabaseStartupAsync(page);
            Assert.False(await page.EvaluateAsync<bool>("'policyOutcomeFailures' in window"));
            Assert.Equal(retained.GetProperty("payload").GetString(), (await RetainedAsync(page)).GetProperty("payload").GetString());
            await page.GetByTestId("agents-policy-recovery").ClickAsync();
            var list = page.GetByTestId("agent-policy-recovery-dialog");
            await Assertions.Expect(list).ToContainTextAsync(operation);
            await Assertions.Expect(list.GetByTestId("agent-policy-recovery-reload")).ToBeInViewportAsync(new() { Ratio = 1 });
            await page.ScreenshotAsync(new() { Path = host.Artifact("agent-policy-deleted-recovery-list.png") });
            await list.GetByTestId($"agent-policy-recovery-{agentId:N}").ClickAsync();
            await Assertions.Expect(dialog.GetByTestId("agent-policy-unconfirmed")).ToContainTextAsync(operation);
            Assert.Equal(0, await dialog.Locator("form").CountAsync());
            await Assertions.Expect(dialog.GetByTestId("agent-policy-save")).ToBeDisabledAsync();
            await dialog.GetByTestId("agent-policy-recover").ClickAsync();
            await Assertions.Expect(dialog.GetByTestId("agent-policy-historical")).ToContainTextAsync("RunSnapshot");
            await Assertions.Expect(dialog.GetByTestId("agent-policy-historical")).ToContainTextAsync("00:03:00");
            await Assertions.Expect(dialog.GetByTestId("agent-policy-save")).ToBeDisabledAsync();
            Assert.Equal(JsonValueKind.Null, (await RetainedAsync(page)).ValueKind);
            Assert.Equal(catalogBeforeRecovery, await ReadSiblingAsync());
            await host.SeedAsync(async services => {
                Assert.DoesNotContain(await services.GetRequiredService<IAgentFrameworkWorkspaceService>().ListAgentsAsync(false), item => item.Id == agentId);
                return true;
            });
            await dialog.GetByTestId("agent-policy-read-current").ClickAsync();
            await Assertions.Expect(dialog.GetByTestId("agent-policy-status")).ToContainTextAsync("could not be read");
            await Assertions.Expect(dialog.GetByTestId("agent-policy-historical")).ToContainTextAsync("00:03:00");
            await Assertions.Expect(dialog.GetByTestId("agent-policy-save")).ToBeInViewportAsync(new() { Ratio = 1 });
            Assert.False(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth > document.documentElement.clientWidth"));
            await page.ScreenshotAsync(new() { Path = host.Artifact("agent-policy-deleted-recovery-receipt.png") });
            await oracle.AssertCleanAsync();
            passed = true;
        } finally {
            await page.ScreenshotAsync(new() { Path = host.Artifact(passed ? "agent-policy-deleted-final.png" : "agent-policy-deleted-failure.png"), Timeout = 5_000 });
            await host.CaptureHostLogAsync();
            await File.WriteAllTextAsync(host.Artifact("agent-policy-deleted-recovery-evidence.json"), JsonSerializer.Serialize(new {
                Passed = passed, Viewport = "1920x1080@1", RealNativeProducer = true, TerminalBrowserFault = true,
                DeletedAgent = agentId, HostRestart = restart, SameOrigin = host.BaseUrl, oracle.ExpectedTeardown
            }));
        }
        Task<AgentDecisionDataPolicySnapshot> ReadSiblingAsync() => host.SeedAsync(services =>
            services.GetRequiredService<IAgentFrameworkWorkspaceService>().ReadAgentDecisionDataPolicyAsync(siblingId));
    }

    private static Task<JsonElement> RetainedAsync(IPage page) => page.EvaluateAsync<JsonElement>("""
        () => new Promise((resolve, reject) => {
            const open = indexedDB.open('candoitall.agents.pending-policy', 1);
            open.onerror = () => reject(open.error);
            open.onsuccess = () => {
                const database = open.result;
                const tx = database.transaction('submissions', 'readonly');
                const request = tx.objectStore('submissions').getAll();
                tx.oncomplete = () => {
                    database.close();
                    if (request.result.length > 1) reject(new Error('Unexpected retained operation count'));
                    else resolve(request.result[0] ?? null);
                };
                tx.onabort = () => { database.close(); reject(tx.error); };
            };
        })
        """);
}
