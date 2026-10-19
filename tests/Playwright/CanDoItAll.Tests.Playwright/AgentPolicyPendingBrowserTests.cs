using System.Text.Json;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

[Trait("Category", "Playwright")]
[Trait("Category", "HostPlatform")]
public sealed class AgentPolicyPendingBrowserTests {
    [Fact]
    public async Task IndexedDb_transactions_bound_storage_and_preserve_exact_operations_across_tabs_and_reload() {
        const string origin = "https://agent-policy-pending.test";
        const string modulePath = "/js/agentDecisionPolicyPending.js";
        using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        await using var context = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 1920, Height = 1080 } });
        var module = await File.ReadAllTextAsync(Path.Combine(PlaywrightTestHostPaths.RepositoryRoot,
            "src", "App", "CanDoItAll.Web", "wwwroot", "js", "agentDecisionPolicyPending.js"));
        await context.RouteAsync(origin + "/**", route => route.FulfillAsync(new() {
            ContentType = route.Request.Url.EndsWith(modulePath, StringComparison.Ordinal) ? "text/javascript" : "text/html",
            Body = route.Request.Url.EndsWith(modulePath, StringComparison.Ordinal) ? module : "<!doctype html><title>Agent policy retention proof</title>"
        }));
        var first = await PageAsync();
        var second = await PageAsync();
        var input = new {
            key = new { caller = "ap1:" + new string('a', 64), profileId = Guid.NewGuid().ToString("D"), scopeKind = 0, scopeKey = "sandbox", agentId = Guid.NewGuid().ToString("D") },
            operationId = Guid.NewGuid().ToString("D"), payload = "{\"revision\":\"9007199254740993\"}"
        };
        var prepared = await Task.WhenAll(
            first.EvaluateAsync<JsonElement>("x => pending.prepare(x.key, x.operationId, x.payload)", input),
            second.EvaluateAsync<JsonElement>("x => pending.prepare(x.key, x.operationId, x.payload)", input));
        Assert.Single(prepared, Changed);
        var dispatched = await Task.WhenAll(
            first.EvaluateAsync<JsonElement>("x => pending.beginDispatch(x.key, x.operationId, x.payload)", input),
            second.EvaluateAsync<JsonElement>("x => pending.beginDispatch(x.key, x.operationId, x.payload)", input));
        Assert.Single(dispatched, Changed);
        Assert.False(Changed(await first.EvaluateAsync<JsonElement>("x => pending.cancelPrepared(x.key, x.operationId, x.payload)", input)));
        Assert.False(Changed(await second.EvaluateAsync<JsonElement>("x => pending.clearResolved(x.key, x.operationId, x.payload)", input)));
        await first.ReloadAsync();
        await InitializeAsync(first);
        var retained = await first.EvaluateAsync<JsonElement>("key => pending.read(key)", input.key);
        Assert.Equal(input.payload, retained.GetProperty("payload").GetString());
        Assert.Equal(1, retained.GetProperty("stage").GetInt32());
        var wrongCaller = input.key with { caller = "ap1:" + new string('b', 64) };
        Assert.Equal(JsonValueKind.Null, (await first.EvaluateAsync<JsonElement>("key => pending.read(key)", wrongCaller)).ValueKind);
        Assert.Equal(JsonValueKind.Null, (await first.EvaluateAsync<JsonElement>("key => pending.read(key)", input.key with { profileId = Guid.NewGuid().ToString("D") })).ValueKind);
        Assert.Equal(JsonValueKind.Null, (await first.EvaluateAsync<JsonElement>("key => pending.read(key)", input.key with { scopeKey = "other" })).ValueKind);
        var result = new { outcome = "Accepted", input.operationId, snapshot = new { revision = "9007199254740994" }, followupRequired = true };
        await first.EvaluateAsync<JsonElement>("x => pending.recordOutcome(x.key, x.operationId, x.payload, x.outcome)",
            new { input.key, input.operationId, input.payload, outcome = JsonSerializer.Serialize(result) });
        var historical = await second.EvaluateAsync<JsonElement>("x => pending.recordOutcome(x.key, x.operationId, x.payload, x.outcome)",
            new { input.key, input.operationId, input.payload, outcome = JsonSerializer.Serialize(result with { followupRequired = false }) });
        Assert.False(Changed(historical));
        Assert.True(JsonDocument.Parse(historical.GetProperty("record").GetProperty("outcome").GetString()!).RootElement.GetProperty("followupRequired").GetBoolean());
        var mismatch = input with { payload = "different original command" };
        Assert.False(Changed(await second.EvaluateAsync<JsonElement>("x => pending.clearResolved(x.key, x.operationId, x.payload)", mismatch)));
        Assert.True(Changed(await first.EvaluateAsync<JsonElement>("x => pending.clearResolved(x.key, x.operationId, x.payload)", input)));
        var newer = input with { operationId = Guid.NewGuid().ToString("D"), payload = "new submission" };
        await second.EvaluateAsync<JsonElement>("x => pending.prepare(x.key, x.operationId, x.payload)", newer);
        Assert.False(Changed(await first.EvaluateAsync<JsonElement>("x => pending.clearResolved(x.key, x.operationId, x.payload)", input)));
        Assert.Equal(newer.operationId, (await first.EvaluateAsync<JsonElement>("key => pending.read(key)", input.key)).GetProperty("operationId").GetString());
        Assert.True(Changed(await second.EvaluateAsync<JsonElement>("x => pending.cancelPrepared(x.key, x.operationId, x.payload)", newer)));
        var oversized = await Assert.ThrowsAsync<PlaywrightException>(() => first.EvaluateAsync<JsonElement>(
            "x => pending.prepare(x.key, x.operationId, 'é'.repeat(4097))", input));
        Assert.Contains("PayloadLimit", oversized.Message, StringComparison.Ordinal);
        var bound = await first.EvaluateAsync<JsonElement>("""
            async x => {
                for (let i = 0; i < 128; i++) {
                    await pending.prepare({ ...x.key, agentId: crypto.randomUUID() }, crypto.randomUUID(), x.payload);
                }
                try { await pending.prepare(x.key, x.operationId, x.payload); }
                catch (error) { return { refused: error.message.includes('CapacityExceeded') }; }
                return { refused: false };
            }
            """, input);
        Assert.True(bound.GetProperty("refused").GetBoolean());
        async Task<IPage> PageAsync() {
            var page = await context.NewPageAsync();
            await page.GotoAsync(origin);
            await InitializeAsync(page);
            return page;
        }
        async Task InitializeAsync(IPage page) => await page.EvaluateAsync("async path => { window.pending = await import(path); await pending.initialize(); }", modulePath);
    }
    private static bool Changed(JsonElement result) => result.GetProperty("changed").GetBoolean();
}
