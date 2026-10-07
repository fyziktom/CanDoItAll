using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

[Trait("Category", "Playwright")]
[Trait("Category", "HostPlatform")]
public sealed class StorageRecoveryBrowserTests {
    [Fact]
    public async Task Production_recovery_continues_exact_prepared_workflow_outputs_without_driver_or_model_dispatch() {
        await using var host = new StorageRecoveryBrowserHost();
        var seeds = await host.ReadyAsync();
        Assert.Equal(2, seeds.Length);
        using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        await using (var readOnly = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 1600, Height = 1000 } })) {
            await host.AuthorizeAsync(readOnly, readOnly: true);
            await UseAuthenticatedLongPollingAsync(readOnly);
            var readPage = await readOnly.NewPageAsync();
            await readPage.GotoAsync(host.BaseUrl + "/settings?tab=storage");
            await PlaywrightAppFixture.CompleteDatabaseStartupAsync(readPage);
            await readPage.GetByTestId("storage-settings-recovery").ClickAsync();
            foreach (var seed in seeds) {
                await readPage.GetByTestId($"storage-recovery-inspect-{seed.IntentId:N}").ClickAsync();
                await Assertions.Expect(readPage.GetByTestId("storage-recovery-dialog")).ToContainTextAsync("ReadOnlyAuthority");
                await Assertions.Expect(readPage.GetByTestId("storage-recovery-complete-workflow-asset")).ToHaveCountAsync(0);
                await Assertions.Expect(readPage.GetByTestId("storage-recovery-record-workflow-receipt")).ToHaveCountAsync(0);
            }
            await readPage.GetByTestId("storage-recovery-close").ClickAsync();
        }
        await using var context = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 1600, Height = 1000 } });
        await host.AuthorizeAsync(context);
        await UseAuthenticatedLongPollingAsync(context);
        var page = await context.NewPageAsync();
        var errors = new List<string>();
        page.PageError += (_, error) => errors.Add(error);
        await page.GotoAsync(host.BaseUrl + "/settings?tab=storage");
        await PlaywrightAppFixture.CompleteDatabaseStartupAsync(page);
        await page.GetByTestId("storage-settings-recovery").ClickAsync();
        var evidence = Path.Combine(PlaywrightTestHostPaths.RepositoryRoot, "output", "playwright", "workspace-recovery-ui");
        Directory.CreateDirectory(evidence);
        try {
            foreach (var seed in seeds) {
                await page.GetByTestId($"storage-recovery-inspect-{seed.IntentId:N}").ClickAsync();
                await Assertions.Expect(page.GetByTestId("storage-recovery-selected-intent")).ToHaveTextAsync(seed.IntentId.ToString());
                await Assertions.Expect(page.GetByTestId("storage-recovery-dialog")).ToContainTextAsync(seed.RunId.ToString());
                var action = page.GetByTestId(seed.NativeCommitted ? "storage-recovery-record-workflow-receipt" : "storage-recovery-complete-workflow-asset");
                await action.ClickAsync();
                await Assertions.Expect(page.GetByTestId("storage-recovery-acknowledged")).ToContainTextAsync("ReceiptRecorded");
                await Assertions.Expect(page.GetByTestId("storage-recovery-acknowledged")).ToContainTextAsync(seed.IntentId.ToString());
                await Assertions.Expect(action).ToHaveCountAsync(0);
                await Assertions.Expect(page.GetByTestId("storage-recovery-error")).ToHaveCountAsync(0);
                await page.ScreenshotAsync(new() { Path = Path.Combine(evidence, seed.NativeCommitted ? "production-workflow-receipt.png" : "production-workflow-prepared.png") });
            }
            var proof = await host.VerifyAsync();
            Assert.Equal(seeds.Length, proof.Length);
            foreach (var item in proof) {
                Assert.Equal(seeds.Single(seed => seed.IntentId == item.IntentId).OriginalContentHash, item.ContentHash);
                Assert.True(item.StorageReceiptUnchanged && item.WorkflowRunUnchanged);
                Assert.Equal(0, item.DriverResolutions);
                Assert.Equal(0, item.AgentExecutions);
                Assert.Equal(0, item.ProviderRequests);
            }
            await File.WriteAllTextAsync(Path.Combine(evidence, "production-workflow-proof.json"), JsonSerializer.Serialize(new { seeds, proof }, new JsonSerializerOptions { WriteIndented = true }));
            await page.GetByTestId("storage-recovery-refresh").ClickAsync();
            await page.GetByTestId("storage-recovery-close").ClickAsync();
            await Assertions.Expect(page.GetByTestId("storage-recovery-dialog")).ToHaveCountAsync(0);
            Assert.Empty(errors);
            Assert.Contains("\"IsAuthenticated\":true", host.Logs, StringComparison.Ordinal);
        } finally {
            await page.ScreenshotAsync(new() { Path = Path.Combine(evidence, "production-final.png") });
        }
    }

    private static Task UseAuthenticatedLongPollingAsync(IBrowserContext context) => context.RouteAsync("**/_blazor/negotiate?*", async route => {
        var response = await route.FetchAsync();
        var negotiation = JsonNode.Parse(await response.TextAsync())!;
        var transport = Assert.Single(negotiation["availableTransports"]!.AsArray(), item => item!["transport"]!.GetValue<string>() == "LongPolling");
        negotiation["availableTransports"] = new JsonArray(transport!.DeepClone());
        await route.FulfillAsync(new() { Response = response, Body = negotiation.ToJsonString(), ContentType = "application/json" });
    });
}
