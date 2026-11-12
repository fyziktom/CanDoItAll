using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Playwright;

[Trait("Category", "HostPlatform")]
public sealed class LiveRequestBudgetTests {
    [Fact]
    public async Task Concurrent_admission_stops_at_each_execution_and_persisted_campaign_bound() {
        await using var environment = CanDoItAllTestEnvironment.Create("live-request-budget");
        var path = Path.Combine(environment.RootPath, "requests.json");
        await File.WriteAllTextAsync(path, "[]");
        for (var execution = 0; execution < 4; execution++) {
            await using var proxy = new LiveModelRequestProxy(path);
            var attempts = await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => proxy.ReserveAsync(CancellationToken.None)));
            Assert.Equal(10, attempts.Count(accepted => accepted));
            Assert.Equal(10, proxy.Admitted);
        }
        await using var successor = new LiveModelRequestProxy(path);
        Assert.False(await successor.ReserveAsync(CancellationToken.None));
        Assert.Equal(0, successor.Admitted);
    }

    [Fact]
    public async Task Unreadable_budget_never_admits_a_request() {
        await using var environment = CanDoItAllTestEnvironment.Create("live-request-budget-invalid");
        var path = Path.Combine(environment.RootPath, "requests.json");
        await File.WriteAllTextAsync(path, "invalid");
        await using var proxy = new LiveModelRequestProxy(path);
        await Assert.ThrowsAsync<System.Text.Json.JsonException>(() => proxy.ReserveAsync(CancellationToken.None));
        Assert.Equal(0, proxy.Admitted);
    }
}
