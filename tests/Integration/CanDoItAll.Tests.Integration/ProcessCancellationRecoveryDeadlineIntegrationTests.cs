using System.Diagnostics;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Processes.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using static CanDoItAll.Tests.Integration.Processes.ProcessCancellationRecoveryFixture;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionPersistenceFixture;

namespace CanDoItAll.Tests.Integration.Processes;

[Trait("Category", "FileSystemPortability")]
public sealed class ProcessCancellationRecoveryDeadlineIntegrationTests(Xunit.Abstractions.ITestOutputHelper output) {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Native_owner_wait_honors_deadline_and_retains_original_intent_until_database_lease_takeover(bool databaseWait) {
        await using var app = await TestApplication.CreateAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using var context = Context(services);
        var root = await RootAsync(context);
        var queue = Queue(services);
        var claim = (await queue.ClaimAsync(ProcessCancellationRecoveryOrigin.LiveBarrier, Lease, deadline.Token))!;
        var intent = Intent(claim);
        Assert.Equal(ProcessCancellationCheckpointStatus.Released,
            (await queue.CheckpointAsync(claim, new(claim.Position, intent, RetryAfter: TimeSpan.Zero), deadline.Token)).Status);
        var before = await context.CancellationRecoveries.AsNoTracking().SingleAsync(deadline.Token);
        var store = services.GetRequiredService<ISandboxWorkspaceStore>();
        await store.LoadCatalogAsync(deadline.Token);
        var layout = new FileSandboxWorkspaceStorageLayout(services.GetRequiredService<IWorkspacePathResolver>().ResolveWorkspaceRoot(),
            WorkspaceScopeDescriptor.Organization(services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile.Profile.Id.ToString("N")));
        await using var blockerContext = Context(services);
        IAsyncDisposable blocker;
        if (databaseWait) {
            blocker = await blockerContext.Database.BeginTransactionAsync(deadline.Token);
            await blockerContext.Database.ExecuteSqlInterpolatedAsync($"""
                SELECT 1 FROM process_runtime_states WHERE "RunId" = {root.RunId.Value} FOR UPDATE
                """, deadline.Token);
        } else {
            blocker = await new DurableFileWriter(new PhysicalFileSystemPathPolicyFactory()).AcquireCoordinationAsync(layout.RootPath,
                layout.WorkspaceLockPath, TimeSpan.FromSeconds(1), false, deadline.Token);
        }
        await using (blocker) {
            var owner = new NativeWait(services, Assert.IsAssignableFrom<ISandboxWorkspaceExecutionRunStore>(store), root.RunId.Value, databaseWait);
            var coordinator = new ProcessCancellationRecoveryCoordinator(queue, owner, TimeProvider.System,
                NullLogger<ProcessCancellationRecoveryCoordinator>.Instance);
            var limits = ProcessCancellationRecoverySliceLimits.Default with {
                MaximumDuration = TimeSpan.FromSeconds(1), LeaseDuration = TimeSpan.FromSeconds(3)
            };
            var started = Stopwatch.GetTimestamp();
            var execution = coordinator.RunSliceAsync(ProcessCancellationRecoveryOrigin.LiveBarrier, limits, deadline.Token);
            await owner.Entered.Task.WaitAsync(deadline.Token);
            var result = await execution;
            var elapsed = Stopwatch.GetElapsedTime(started);
            output.WriteLine($"Native {(databaseWait ? "PostgreSQL row" : "File workspace")} wait: {elapsed.TotalMilliseconds:F1} ms.");
            Assert.Equal(ProcessCancellationRecoverySliceStatus.TimedOut, result.Status);
            Assert.Equal(root.RootRunId, result.RootRunId);
            Assert.Equal(1, result.Operations);
            Assert.True(owner.Unwound);
            Assert.True(elapsed < TimeSpan.FromSeconds(5), "The cancellable native owner did not unwind within the test bound.");
            var retained = await context.CancellationRecoveries.AsNoTracking().SingleAsync(deadline.Token);
            Assert.Equal(before.IntentJson, retained.IntentJson);
            Assert.Equal(before.PositionJson, retained.PositionJson);
            Assert.Equal(owner.Original!.LeaseToken, retained.LeaseToken);
            Assert.Equal(owner.Original.ProgressRevision, retained.ProgressRevision);
            Assert.False(retained.Completed);
            Assert.Null(await Queue(services).ClaimAsync(ProcessCancellationRecoveryOrigin.LiveBarrier, limits.LeaseDuration, deadline.Token));
            await WaitExpiredAsync(services, retained.LeaseExpiresAtUtc!.Value);
            var successor = (await Queue(services).ClaimAsync(ProcessCancellationRecoveryOrigin.LiveBarrier, limits.LeaseDuration, deadline.Token))!;
            Assert.Equal(intent, successor.Intent);
            Assert.Equal(owner.Original.Position, successor.Position);
            Assert.Equal(owner.Original.ProgressRevision, successor.ProgressRevision);
            Assert.Equal(owner.Original.LeaseGeneration + 1, successor.LeaseGeneration);
            Assert.NotEqual(owner.Original.LeaseToken, successor.LeaseToken);
            Assert.Equal(ProcessCancellationCheckpointStatus.NotOwned, (await queue.CheckpointAsync(owner.Original,
                new(owner.Original.Position, intent), deadline.Token)).Status);
        }
    }

    private sealed class NativeWait(IServiceProvider services, ISandboxWorkspaceExecutionRunStore store, Guid runId, bool databaseWait)
        : IProcessCancellationRecoveryStep {
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal ProcessCancellationRecoveryClaim? Original { get; private set; }
        internal bool Unwound { get; private set; }

        public async Task<ProcessCancellationRecoveryCheckpoint> AdvanceAsync(ProcessCancellationRecoveryClaim claim, CancellationToken cancellationToken) {
            Original = claim;
            Entered.SetResult();
            try {
                if (databaseWait) {
                    await using var context = Context(services);
                    await context.Database.SqlQuery<int>($"""
                        SELECT 1 AS "Value" FROM process_runtime_states WHERE "RunId" = {runId} FOR UPDATE
                        """).ToArrayAsync(cancellationToken);
                } else {
                    await store.GetExecutionRunAsync(runId, cancellationToken);
                }
                throw new InvalidOperationException("The deliberately held native owner must be cancelled before it returns a checkpoint.");
            } finally {
                Unwound = true;
            }
        }
    }
}
