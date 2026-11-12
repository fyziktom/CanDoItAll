using System.Threading.Channels;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Tests.Unit.Processes;

public sealed class ProcessCancellationRecoveryWorkerTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Live_and_legacy_opportunities_repeat_with_fresh_scopes_even_after_failures(bool failClaims) {
        await using var fixture = new Fixture { FailClaims = failClaims };
        await fixture.Worker.StartAsync(default);
        var pacing = await fixture.Clock.NextAsync();
        for (var turn = 0; turn < 8; turn++) {
            pacing.Fire();
            pacing = await fixture.Clock.NextAsync();
            Assert.All(fixture.Ports, port => Assert.True(port.Disposed));
            Assert.Equal(failClaims ? fixture.Options.Limits.FailureDelay : fixture.Options.Limits.YieldDelay, pacing.Duration);
        }
        Assert.Equal(new[] { 0, 0, 0, 1, 0, 0, 0, 1 }, fixture.Origins.Select(origin => (int)origin));
        Assert.Equal(new[] { 4, 8 }, fixture.SeedAfterClaims);
        Assert.Equal(10, fixture.Ports.Count);
        Assert.Equal(failClaims ? 0 : 8, fixture.Checkpoints);
    }

    [Fact]
    public async Task Empty_origins_are_checked_once_and_then_paced() {
        await using var fixture = new Fixture { Empty = true };
        await fixture.Worker.StartAsync(default);
        (await fixture.Clock.NextAsync()).Fire();
        var pacing = await fixture.Clock.NextAsync();
        Assert.Equal(new[] { ProcessCancellationRecoveryOrigin.LiveBarrier, ProcessCancellationRecoveryOrigin.LegacySeed }, fixture.Origins);
        Assert.Equal(fixture.Options.IdleDelay, pacing.Duration);
        Assert.Single(fixture.Ports);
        Assert.True(fixture.Ports[0].Disposed);
        Assert.Equal(0, fixture.Checkpoints);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Shutdown_awaits_held_owner_unwind_before_scope_disposal(bool seed) {
        await using var fixture = new Fixture { HoldStage = !seed, HoldSeed = seed };
        await fixture.Worker.StartAsync(default);
        for (var turn = 0; turn < (seed ? 4 : 1); turn++) {
            (await fixture.Clock.NextAsync()).Fire();
        }
        await fixture.Held.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(fixture.Ports[^1].Disposed);
        await fixture.Worker.StopAsync(default).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(fixture.Unwound);
        Assert.All(fixture.Ports, port => Assert.True(port.Disposed));
        Assert.Equal(seed ? 4 : 0, fixture.Checkpoints);
    }

    [Fact]
    public async Task Legacy_seed_deadline_unwinds_then_next_live_opportunity_runs() {
        await using var fixture = new Fixture { HoldSeed = true };
        await fixture.Worker.StartAsync(default);
        for (var turn = 0; turn < 4; turn++) {
            (await fixture.Clock.NextAsync()).Fire();
        }
        await fixture.Held.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var pacing = await fixture.Clock.NextAsync();
        Assert.True(fixture.Unwound);
        Assert.True(fixture.Ports[^1].Disposed);
        pacing.Fire();
        await fixture.Clock.NextAsync();
        Assert.Equal(ProcessCancellationRecoveryOrigin.LiveBarrier, fixture.Origins[^1]);
        Assert.Equal(5, fixture.Checkpoints);
    }

    [Fact]
    public async Task Disabled_worker_creates_no_scope_or_timer() {
        await using var fixture = new Fixture();
        fixture.Options.Enabled = false;
        await fixture.Worker.StartAsync(default);
        await fixture.Worker.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Empty(fixture.Ports);
        Assert.False(fixture.Clock.Pending.TryRead(out _));
    }

    private sealed class Fixture : IAsyncDisposable {
        private readonly ServiceProvider provider;
        internal ProcessCancellationRecoveryOptions Options { get; } = new() {
            Enabled = true, IdleDelay = TimeSpan.FromMilliseconds(100),
            Limits = ProcessCancellationRecoverySliceLimits.Default with {
                MaximumDuration = TimeSpan.FromSeconds(1), FailureDelay = TimeSpan.FromMilliseconds(200)
            }
        };
        internal PacingClock Clock { get; } = new();
        internal ProcessCancellationRecoveryWorker Worker { get; }
        internal List<Ports> Ports { get; } = [];
        internal List<ProcessCancellationRecoveryOrigin> Origins { get; } = [];
        internal List<int> SeedAfterClaims { get; } = [];
        internal TaskCompletionSource Held { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal bool Empty { get; init; }
        internal bool FailClaims { get; init; }
        internal bool HoldStage { get; init; }
        internal bool HoldSeed { get; init; }
        internal bool Unwound { get; private set; }
        internal int Checkpoints { get; set; }

        internal Fixture() {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<TimeProvider>(Clock);
            services.AddScoped(_ => {
                var ports = new Ports(this);
                Ports.Add(ports);
                return ports;
            });
            services.AddScoped<IProcessCancellationRecoveryQueue>(scope => scope.GetRequiredService<Ports>());
            services.AddScoped<IProcessCancellationRecoveryStep>(scope => scope.GetRequiredService<Ports>());
            services.AddScoped<ProcessCancellationRecoveryCoordinator>();
            provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            Worker = new(provider.GetRequiredService<IServiceScopeFactory>(), Microsoft.Extensions.Options.Options.Create(Options),
                Clock, NullLogger<ProcessCancellationRecoveryWorker>.Instance);
        }

        internal async Task WaitAsync(CancellationToken token) {
            Held.TrySetResult();
            try {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            } finally {
                Unwound = true;
            }
        }

        public async ValueTask DisposeAsync() {
            await Worker.StopAsync(default).WaitAsync(TimeSpan.FromSeconds(5));
            Worker.Dispose();
            await provider.DisposeAsync();
        }
    }

    private sealed class Ports(Fixture fixture) : IProcessCancellationRecoveryQueue, IProcessCancellationRecoveryStep, IDisposable {
        internal bool Disposed { get; private set; }

        public Task<ProcessCancellationRecoveryClaim?> ClaimAsync(ProcessCancellationRecoveryOrigin origin, TimeSpan leaseDuration,
            CancellationToken cancellationToken = default) {
            fixture.Origins.Add(origin);
            if (fixture.FailClaims) {
                throw new IOException("Selected queue failure.");
            }
            ProcessCancellationRecoveryClaim? claim = fixture.Empty ? null : new(new(Guid.NewGuid()), origin,
                origin == ProcessCancellationRecoveryOrigin.LiveBarrier ? RuntimeCommandId.New() : null,
                Guid.NewGuid(), 1, 0, DateTimeOffset.UtcNow.Add(leaseDuration), 0,
                ProcessCancellationRecoveryPosition.Initial, null, ProcessCancellationRecoveryPending.None);
            return Task.FromResult(claim);
        }

        public async Task<ProcessCancellationRecoveryCheckpoint> AdvanceAsync(ProcessCancellationRecoveryClaim claim, CancellationToken cancellationToken) {
            if (fixture.HoldStage) {
                await fixture.WaitAsync(cancellationToken);
            }
            return new(claim.Position, RetryAfter: TimeSpan.Zero);
        }

        public Task<ProcessCancellationCheckpointResult> CheckpointAsync(ProcessCancellationRecoveryClaim claim,
            ProcessCancellationRecoveryCheckpoint checkpoint, CancellationToken cancellationToken = default) {
            fixture.Checkpoints++;
            return Task.FromResult(new ProcessCancellationCheckpointResult(ProcessCancellationCheckpointStatus.Released));
        }

        public async Task<ProcessCancellationSeedResult> SeedLegacyAsync(int take = 16, CancellationToken cancellationToken = default) {
            Assert.Equal(fixture.Options.LegacySeedPageSize, take);
            fixture.SeedAfterClaims.Add(fixture.Origins.Count);
            if (fixture.HoldSeed) {
                await fixture.WaitAsync(cancellationToken);
            }
            return new(ProcessCancellationSeedStatus.Completed, 0, 0);
        }

        public void Dispose() {
            if (fixture.Held.Task.IsCompleted) {
                Assert.True(fixture.Unwound);
            }
            Disposed = true;
        }
    }

    private sealed class PacingClock : TimeProvider {
        private readonly Channel<PacingTimer> pending = Channel.CreateUnbounded<PacingTimer>();
        internal ChannelReader<PacingTimer> Pending => pending.Reader;
        internal async Task<PacingTimer> NextAsync() => await pending.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) {
            if (dueTime >= TimeSpan.FromSeconds(1)) {
                return System.CreateTimer(callback, state, dueTime, period);
            }
            var timer = new PacingTimer(callback, state, dueTime);
            pending.Writer.TryWrite(timer);
            return timer;
        }
    }

    private sealed class PacingTimer(TimerCallback callback, object? state, TimeSpan duration) : ITimer {
        private int disposed;
        internal TimeSpan Duration => duration;
        internal void Fire() {
            if (Volatile.Read(ref disposed) == 0) {
                callback(state);
            }
        }
        public bool Change(TimeSpan dueTime, TimeSpan period) => throw new NotSupportedException();
        public void Dispose() => Interlocked.Exchange(ref disposed, 1);
        public ValueTask DisposeAsync() {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
