using System.Collections.Concurrent;
using System.Diagnostics;
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
        var store = new HistoryReadStore(fixture.Factory, new([]), fixture.Details, fixture.Clock);
        var retention = new HistoryRetentionStore(fixture.Factory, fixture.Clock);
        var beginSamples = new List<double>();
        var completeSamples = new List<double>();
        for (var index = 0; index < 20; index++) {
            var start = fixture.Start() with { StartedAtUtc = fixture.Clock.Now.AddMilliseconds(index) };
            var begin = Stopwatch.StartNew();
            await fixture.Capture.BeginAsync(start, null, default);
            begin.Stop();
            var complete = Stopwatch.StartNew();
            await fixture.Capture.CompleteAsync(start,
                fixture.Completion() with { FinishedAtUtc = start.StartedAtUtc.AddMilliseconds(50) }, null, default);
            complete.Stop();
            beginSamples.Add(begin.Elapsed.TotalMilliseconds);
            completeSamples.Add(complete.Elapsed.TotalMilliseconds);
        }
        Assert.InRange(Percentile95(beginSamples), 0, 25);
        Assert.InRange(Percentile95(completeSamples), 0, 25);

        var concurrent = Enumerable.Range(0, 24).Select(index => fixture.Start() with {
            StartedAtUtc = fixture.Clock.Now.AddSeconds(1).AddMilliseconds(index)
        }).ToArray();
        var searchCounts = new ConcurrentBag<int>();
        var purgeTask = Task.Run(async () => {
            var purged = 0;
            while (purged < expired.Inserted) {
                purged += await retention.PurgeExpiredMetadataAsync(fixture.Partition, 1_000, default);
            }
            return purged;
        });
        var searchTask = Task.Run(async () => {
            var query = new ProviderRequestHistoryQuery(new HistoryProviderScope.AllAuthorized(),
                fixture.Clock.Now.AddDays(-4), fixture.Clock.Now.AddDays(1));
            for (var index = 0; index < 20; index++) {
                searchCounts.Add((await store.SearchAsync(fixture.Access.Context, query, null, default)).Entries.Count);
            }
        });
        var captureTask = Task.WhenAll(concurrent.Select(async start => {
            await fixture.Capture.BeginAsync(start, null, default);
            await fixture.Capture.CompleteAsync(start,
                fixture.Completion() with { FinishedAtUtc = start.StartedAtUtc.AddMilliseconds(50) }, null, default);
        }));
        await Task.WhenAll(purgeTask, searchTask, captureTask);
        var purgedCount = await purgeTask;

        Assert.Equal(expired.Inserted, purgedCount);
        Assert.Equal(20, searchCounts.Count);
        Assert.All(searchCounts, count => Assert.Equal(51, count));
        await using var db = fixture.Factory.CreateDbContext();
        var attempts = concurrent.Select(item => item.AttemptId.Value).ToArray();
        Assert.Equal(concurrent.Length, await db.Set<HistoryEntryRow>()
            .CountAsync(row => attempts.Contains(row.AttemptId!.Value) && row.Outcome == HistoryOutcome.Succeeded));
        Assert.Equal(active.Inserted + 20 + concurrent.Length,
            await db.Set<HistoryEntryRow>().CountAsync(row => row.ExpiresAtUtc > fixture.Clock.Now));
        output.WriteLine(System.Text.Json.JsonSerializer.Serialize(new {
            ActiveRows = active.Inserted,
            ExpiredRowsPurged = purgedCount,
            ConcurrentCaptures = concurrent.Length,
            BeginP95Milliseconds = Percentile95(beginSamples),
            CompleteP95Milliseconds = Percentile95(completeSamples)
        }));
    }

    private static double Percentile95(IReadOnlyCollection<double> values) {
        var ordered = values.Order().ToArray();
        return ordered[(int)Math.Ceiling(ordered.Length * 0.95) - 1];
    }
}


