using System.Collections.Concurrent;
using System.Text.Json;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Xunit.Abstractions;
using static CanDoItAll.Tests.Integration.Processes.ProcessCancellationRecoveryFixture;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionPersistenceFixture;

namespace CanDoItAll.Tests.Integration;

[Trait("Category", "FileSystemPortability")]
public sealed class ProcessCancellationRecoveryBacklogTests(ITestOutputHelper output) {
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Native_workers_progress_every_origin_and_defer_a_corrupt_first_root_without_losing_its_intent(int hosts) {
        await using var environment = CanDoItAllTestEnvironment.Create("candoitall-tests");
        var profile = environment.CreatePostgreSqlProfile("primary");
        var providers = new List<TestApplication>();
        var workers = new List<ProcessCancellationRecoveryWorker>();
        var attempts = Enumerable.Range(0, hosts).Select(_ => new ConcurrentQueue<ProcessCancellationRecoveryOrigin>()).ToArray();
        var seedPages = new ConcurrentQueue<int>();
        try {
            for (var host = 0; host < hosts; host++) {
                var trace = attempts[host];
                providers.Add(await TestApplication.CreateAsync(new TestHarnessOptions {
                    TestEnvironment = environment, ActiveProfile = profile,
                    ConfigureServices = services => {
                        services.Configure<ProcessCancellationRecoveryOptions>(options => {
                            options.Enabled = true;
                            options.IdleDelay = TimeSpan.FromMilliseconds(25);
                            options.Limits = ProcessCancellationRecoverySliceLimits.Default with {
                                MaximumOperations = 1, MaximumDuration = TimeSpan.FromSeconds(1), LeaseDuration = TimeSpan.FromSeconds(3)
                            };
                        });
                        services.AddScoped<EfProcessCancellationRecoveryQueue>();
                        services.RemoveAll<IProcessCancellationRecoveryQueue>();
                        services.AddScoped<IProcessCancellationRecoveryQueue>(scope => new RecordedQueue(
                            scope.GetRequiredService<EfProcessCancellationRecoveryQueue>(), trace, seedPages));
                    }
                }));
            }
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(90));
            var token = deadline.Token;
            var services = providers[0].Services;
            await using var context = Context(services);
            var live = new List<ProcessRunId>();
            var legacy = new List<ProcessRunId>();
            for (var root = 0; root < 32; root++) {
                live.Add((await RootAsync(context)).RootRunId);
            }
            for (var root = 0; root < 8; root++) {
                legacy.Add((await RootAsync(context, legacy: true)).RootRunId);
            }
            var queue = Queue(services);
            var seeded = false;
            for (var page = 0; page < 4; page++) {
                var result = await queue.SeedLegacyAsync(16, token);
                seedPages.Enqueue(result.Selected);
                if (result.Status == ProcessCancellationSeedStatus.Completed) {
                    seeded = true;
                    break;
                }
            }
            Assert.True(seeded);
            Assert.Equal(40, await context.CancellationRecoveries.CountAsync(token));
            var corrupt = (await queue.ClaimAsync(ProcessCancellationRecoveryOrigin.LiveBarrier, Lease, token))!;
            var original = Intent(corrupt);
            Assert.Equal(ProcessCancellationCheckpointStatus.Released,
                (await queue.CheckpointAsync(corrupt, new(corrupt.Position, original, RetryAfter: TimeSpan.Zero), token)).Status);
            const string malformed = "{invalid-retained-position";
            var due = DateTimeOffset.UtcNow.AddMinutes(-1);
            await context.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE process_cancellation_recovery SET "PositionJson" = {malformed}, "DueAtUtc" = {due}
                WHERE "RootRunId" = {corrupt.RootRunId.Value}
                """, token);
            var wire = (await context.CancellationRecoveries.AsNoTracking().SingleAsync(item => item.RootRunId == corrupt.RootRunId.Value, token)).IntentJson;
            foreach (var provider in providers) {
                var worker = provider.Services.GetServices<IHostedService>().OfType<ProcessCancellationRecoveryWorker>().Single();
                workers.Add(worker);
                await worker.StartAsync(token);
            }
            ProcessCancellationRecoveryEntity[] rows;
            while (true) {
                rows = await context.CancellationRecoveries.AsNoTracking().ToArrayAsync(token);
                if (rows.Where(item => item.RootRunId != corrupt.RootRunId.Value).All(item => item.ProgressRevision > 0) &&
                        rows.Single(item => item.RootRunId == corrupt.RootRunId.Value).Pending == ProcessCancellationRecoveryPending.InvalidEvidence &&
                        attempts.All(trace => trace.Count >= 8)) {
                    break;
                }
                await Task.Delay(25, token);
            }
            await Task.WhenAll(workers.Select(worker => worker.StopAsync(default)));
            var retained = rows.Single(item => item.RootRunId == corrupt.RootRunId.Value);
            Assert.Equal(wire, retained.IntentJson);
            Assert.Equal(malformed, retained.PositionJson);
            Assert.Null(retained.LeaseToken);
            Assert.False(retained.Completed);
            Assert.True(retained.DueAtUtc > retained.UpdatedAtUtc);
            Assert.All(live.Where(root => root != corrupt.RootRunId), root => Assert.True(rows.Single(item => item.RootRunId == root.Value).ProgressRevision > 0));
            Assert.All(legacy, root => Assert.True(rows.Single(item => item.RootRunId == root.Value).ProgressRevision > 0));
            foreach (var trace in attempts) {
                Assert.Equal(new[] { 0, 0, 0, 1, 0, 0, 0, 1 }, trace.Take(8).Select(origin => (int)origin));
            }
            Assert.All(seedPages, selected => Assert.InRange(selected, 0, 16));
            Assert.True((await context.CancellationRecoverySeeds.AsNoTracking().SingleAsync(token)).Completed);
            output.WriteLine(JsonSerializer.Serialize(new {
                Hosts = hosts, LiveRoots = live.Count, LegacyRoots = legacy.Count,
                HealthyRootsProgressed = rows.Count(item => item.RootRunId != corrupt.RootRunId.Value && item.ProgressRevision > 0),
                FirstPreferredOrigins = attempts.Select(trace => trace.Take(8).ToArray()),
                ClaimAttempts = attempts.Select(trace => trace.Count), SeedPageSizes = seedPages.ToArray(),
                CorruptIntentRetained = true, CorruptRootDeferred = true
            }));
        } finally {
            try {
                await Task.WhenAll(workers.Select(worker => worker.StopAsync(default)));
            } finally {
                await TestFixtureCleanup.DisposeAsync([.. providers.AsEnumerable().Reverse()]);
            }
        }
    }

    private sealed class RecordedQueue(IProcessCancellationRecoveryQueue inner,
        ConcurrentQueue<ProcessCancellationRecoveryOrigin> attempts, ConcurrentQueue<int> seedPages) : IProcessCancellationRecoveryQueue {
        public Task<ProcessCancellationRecoveryClaim?> ClaimAsync(ProcessCancellationRecoveryOrigin origin, TimeSpan leaseDuration,
            CancellationToken cancellationToken = default) {
            attempts.Enqueue(origin);
            return inner.ClaimAsync(origin, leaseDuration, cancellationToken);
        }

        public Task<ProcessCancellationCheckpointResult> CheckpointAsync(ProcessCancellationRecoveryClaim claim,
            ProcessCancellationRecoveryCheckpoint checkpoint, CancellationToken cancellationToken = default)
            => inner.CheckpointAsync(claim, checkpoint, cancellationToken);

        public async Task<ProcessCancellationSeedResult> SeedLegacyAsync(int take = 16, CancellationToken cancellationToken = default) {
            var result = await inner.SeedLegacyAsync(take, cancellationToken);
            seedPages.Enqueue(result.Selected);
            return result;
        }
    }
}
