using Microsoft.Playwright;
using Xunit.Abstractions;

namespace CanDoItAll.Tests.Playwright;

[Trait("Category", "Playwright")]
[Trait("Category", "HostPlatform")]
public sealed class NavigationAcknowledgementBrowserTests(ITestOutputHelper output) {
    [Fact]
    public async Task Permanent_circuit_retirement_settles_an_admitted_navigation_without_an_unhandled_failure() {
        await using var host = new PlaywrightAppFixture();
        await host.InitializeAsync();
        await using var context = await host.Browser.NewContextAsync();
        await NavigationAcknowledgementProbe.InstallAsync(context);
        var page = await context.NewPageAsync();
        try {
            await page.GotoAsync(host.BaseUrl + "/agents?usageScope=agents");
            await PlaywrightAppFixture.CompleteDatabaseStartupAsync(page);
            await NavigationAcknowledgementProbe.HoldAsync(page);
            await page.GetByTestId("agents-overview-usage-scope").GetByRole(AriaRole.Button, new() { Name = "Both", Exact = true }).ClickAsync();
            await page.WaitForURLAsync(url => !url.Contains("usageScope=", StringComparison.Ordinal));
            await page.WaitForFunctionAsync("() => navigationAcknowledgementProbe.records.some(r => r.returnedUtc && !r.sentUtc)");
            output.WriteLine(await page.EvaluateAsync<string>("() => JSON.stringify(navigationAcknowledgementProbe.records)"));
            await page.EvaluateAsync("() => navigationAcknowledgementProbe.retire()");
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(80));
            while (!host.GetLogSnapshot(int.MaxValue).Contains("Navigation stopped because the session ended when navigating to /agents", StringComparison.Ordinal)) {
                await Task.Delay(50, deadline.Token);
            }
            Assert.Contains("Closing circuit with id", host.GetLogSnapshot(int.MaxValue), StringComparison.Ordinal);
            Assert.DoesNotContain("Unhandled exception", host.GetLogSnapshot(int.MaxValue), StringComparison.Ordinal);
        } finally {
            output.WriteLine(host.GetLogSnapshot(int.MaxValue));
        }
    }

    [Fact]
    public async Task Visible_usage_route_does_not_prove_navigation_acknowledgement() {
        await using var host = new PlaywrightAppFixture();
        await host.InitializeAsync();
        await using var context = await host.Browser.NewContextAsync();
        await NavigationAcknowledgementProbe.InstallAsync(context);
        var page = await context.NewPageAsync();
        try {
            await page.GotoAsync(host.BaseUrl + "/agents");
            await PlaywrightAppFixture.CompleteDatabaseStartupAsync(page);
            await NavigationAcknowledgementProbe.HoldAsync(page);
            await page.GetByTestId("agents-overview-usage-scope").GetByRole(AriaRole.Button, new() { Name = "Chats", Exact = true }).ClickAsync();
            await page.WaitForURLAsync(url => url.Contains("usageScope=simple-chats", StringComparison.Ordinal));
            await Assertions.Expect(page.GetByTestId("agents-overview-top-consumers")).ToContainTextAsync("Top 5 Chats");
            await page.WaitForFunctionAsync("() => navigationAcknowledgementProbe.records.some(r => r.returnedUtc && !r.sentUtc)");
            output.WriteLine(await page.EvaluateAsync<string>("() => JSON.stringify(navigationAcknowledgementProbe.records)"));
            Assert.Contains("Requesting navigation to URI", host.GetLogSnapshot(int.MaxValue), StringComparison.Ordinal);
            var completion = NavigationAcknowledgementProbe.WaitForCompletionAsync(page, host, 0);
            Assert.False(completion.IsCompleted);
            await NavigationAcknowledgementProbe.ReleaseAsync(page);
            await completion;
            foreach (var name in new[] { "Agents", "Both", "Chats", "Agents", "Both" }) {
                var before = await page.EvaluateAsync<int>("() => navigationAcknowledgementProbe.records.length");
                await page.GetByTestId("agents-overview-usage-scope").GetByRole(AriaRole.Button, new() { Name = name, Exact = true }).ClickAsync();
                await page.WaitForFunctionAsync("count => navigationAcknowledgementProbe.records.length > count", before);
                await NavigationAcknowledgementProbe.WaitForCompletionAsync(page, host, 0);
            }
            var firstConnection = await page.EvaluateAsync<string>("() => navigationAcknowledgementProbe.records[0].connectionId");
            await using var survivor = await host.Browser.NewContextAsync();
            await NavigationAcknowledgementProbe.InstallAsync(survivor);
            var survivorPage = await survivor.NewPageAsync();
            await survivorPage.GotoAsync(host.BaseUrl + "/agents?usageScope=agents");
            await PlaywrightAppFixture.CompleteDatabaseStartupAsync(survivorPage);
            await context.CloseAsync();
            var survivorLogStart = host.GetLogLines().Length;
            await survivorPage.GetByTestId("agents-overview-usage-scope").GetByRole(AriaRole.Button, new() { Name = "Chats", Exact = true }).ClickAsync();
            await survivorPage.WaitForURLAsync(url => url.Contains("usageScope=simple-chats", StringComparison.Ordinal));
            await NavigationAcknowledgementProbe.WaitForCompletionAsync(survivorPage, host, survivorLogStart);
            Assert.NotEqual(firstConnection, await survivorPage.EvaluateAsync<string>("() => navigationAcknowledgementProbe.records[0].connectionId"));
            await Assertions.Expect(survivorPage.Locator("#blazor-error-ui")).Not.ToBeVisibleAsync();
            Assert.DoesNotContain("Unhandled exception", host.GetLogSnapshot(int.MaxValue), StringComparison.Ordinal);
        } finally {
            output.WriteLine(host.GetLogSnapshot(int.MaxValue));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Unacknowledged_navigation_failure_remains_observable_in_its_original_circuit(bool abruptDisconnect) {
        await using var host = new PlaywrightAppFixture();
        await host.InitializeAsync();
        await using var context = await host.Browser.NewContextAsync();
        await NavigationAcknowledgementProbe.InstallAsync(context);
        var page = await context.NewPageAsync();
        try {
            await page.GotoAsync(host.BaseUrl + "/agents?usageScope=agents");
            await PlaywrightAppFixture.CompleteDatabaseStartupAsync(page);
            await NavigationAcknowledgementProbe.HoldAsync(page);
            await page.GetByTestId("agents-overview-usage-scope").GetByRole(AriaRole.Button, new() { Name = "Both", Exact = true }).ClickAsync();
            await page.WaitForURLAsync(url => !url.Contains("usageScope=", StringComparison.Ordinal));
            await Assertions.Expect(page.GetByTestId("agents-overview-provider-bar")).ToBeVisibleAsync();
            await Assertions.Expect(page.GetByTestId("agents-overview-provider-distribution")).ToBeVisibleAsync();
            await page.WaitForFunctionAsync("() => navigationAcknowledgementProbe.records.some(r => r.returnedUtc && !r.sentUtc)");
            output.WriteLine(await page.EvaluateAsync<string>("() => JSON.stringify(navigationAcknowledgementProbe.records)"));
            if (abruptDisconnect) {
                await context.RouteAsync("**/_blazor/disconnect", route => route.AbortAsync());
                await context.CloseAsync();
            }
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(80));
            while (!host.GetLogSnapshot(int.MaxValue).Contains("Navigation failed when changing the location to /agents", StringComparison.Ordinal)) {
                await Task.Delay(100, deadline.Token);
            }
            Assert.Contains("TaskCanceledException", host.GetLogSnapshot(int.MaxValue), StringComparison.Ordinal);
            Assert.DoesNotContain("Navigation completed when changing the location to /agents", host.GetLogSnapshot(int.MaxValue), StringComparison.Ordinal);
        } finally {
            output.WriteLine(host.GetLogSnapshot(int.MaxValue));
        }
    }
}
