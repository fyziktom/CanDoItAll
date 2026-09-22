using System.Collections.Concurrent;
using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.AgentFramework.ProviderHistory.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace CanDoItAll.Tests.Integration;

public sealed class ProviderHistoryRuntimeIntegrationTests(ITestOutputHelper output) {
    [Fact]
    public async Task Scale_capture_and_cleanup_remain_bounded_under_concurrent_search() {
        await using var fixture = await HistoryPersistenceTestDatabase.CreateAsync();
        var active = await ProviderHistoryScaleFixture.SeedAsync(
            fixture, 10_000, "runtime-active", fixture.Clock.Now.AddDays(30));
        var expired = await ProviderHistoryScaleFixture.SeedAsync(
            fixture, 5_000, "runtime-expired", fixture.Clock.Now.AddDays(-1));
        var store = new HistoryReadStore(fixture.HistoryFactory, new([]), fixture.Details, fixture.Clock);
        var retention = new HistoryRetentionStore(fixture.HistoryFactory, fixture.HistoryOptions, fixture.Transactions, fixture.Clock);
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var cancellationToken = timeout.Token;

        var concurrent = Enumerable.Range(0, 24).Select(index => fixture.Start() with {
            StartedAtUtc = fixture.Clock.Now.AddSeconds(1).AddMilliseconds(index)
        }).ToArray();
        var searchCounts = new ConcurrentBag<int>();
        var purgeTask = Task.Run(async () => {
            var purged = 0;
            while (purged < expired.Inserted) {
                var batch = await retention.PurgeExpiredMetadataAsync(fixture.Partition, 1_000, cancellationToken);
                Assert.InRange(batch, 1, 1_000);
                purged += batch;
            }
            return purged;
        });
        var searchTask = Task.Run(async () => {
            var query = new ProviderRequestHistoryQuery(new HistoryProviderScope.AllAuthorized(),
                fixture.Clock.Now.AddDays(-4), fixture.Clock.Now.AddDays(1));
            for (var index = 0; index < 20; index++) {
                searchCounts.Add((await store.SearchAsync(fixture.Access.Context, query, null, cancellationToken)).Entries.Count);
            }
        });
        var captureTask = Task.WhenAll(concurrent.Select(async start => {
            await fixture.Capture.BeginAsync(start, null, cancellationToken);
            await fixture.Capture.CompleteAsync(start,
                fixture.Completion() with { FinishedAtUtc = start.StartedAtUtc.AddMilliseconds(50) }, null, cancellationToken);
        }));
        await Task.WhenAll(purgeTask, searchTask, captureTask);
        var purgedCount = await purgeTask;

        Assert.Equal(expired.Inserted, purgedCount);
        Assert.Equal(20, searchCounts.Count);
        Assert.All(searchCounts, count => Assert.Equal(51, count));
        await using var db = fixture.Factory.CreateDbContext();
        var attempts = concurrent.Select(item => item.AttemptId.Value).ToArray();
        Assert.Equal(concurrent.Length, await db.Set<HistoryEntryRow>()
            .CountAsync(row => attempts.Contains(row.AttemptId!.Value) && row.Outcome == HistoryOutcome.Succeeded, cancellationToken));
        Assert.Equal(active.Inserted + concurrent.Length,
            await db.Set<HistoryEntryRow>().CountAsync(row => row.ExpiresAtUtc > fixture.Clock.Now, cancellationToken));
        output.WriteLine(System.Text.Json.JsonSerializer.Serialize(new {
            ActiveRows = active.Inserted,
            ExpiredRowsPurged = purgedCount,
            ConcurrentCaptures = concurrent.Length
        }));
    }
}
