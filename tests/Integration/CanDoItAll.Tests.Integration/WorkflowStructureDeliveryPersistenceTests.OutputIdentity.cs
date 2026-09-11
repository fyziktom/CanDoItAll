using System.Data.Common;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Workbench;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.ProjectStructure;

public sealed partial class WorkflowStructureDeliveryPersistenceTests {
    [Fact]
    public async Task Concurrent_output_identity_with_different_content_preserves_the_first_owner_plan() {
        await using var app = await TestApplication.CreateAsync(new() { ConfigureServices = RemoveAutomaticDelivery });
        await using var fixture = await CreateFixtureAsync(app, prepareOutput: false);
        var different = fixture.Plan with {
            Fingerprint = ProjectWorkflowContributionFingerprint.Create(fixture.Plan,
                fixture.Request with { Title = "Conflicting concurrent output" })
        };
        Assert.NotEqual(fixture.Plan.Fingerprint, different.Fingerprint);
        var barrier = new OutputSnapshotBarrier();
        var owners = ConcurrentOutputOwners(fixture.Services, barrier);

        var results = await Task.WhenAll(PrepareAsync(owners[0], fixture.Plan), PrepareAsync(owners[1], different));

        Assert.Equal(2, barrier.CapturedSnapshots);
        Assert.True(barrier.StartedTransactions >= 3);
        var winner = Assert.Single(results, result => result.Output is not null);
        Assert.NotNull(winner.Output);
        Assert.Single(results, result => result.Conflict);
        var retained = Assert.Single(await owners[0].ListAsync(fixture.Plan.Identity.Occurrence.RunId));
        Assert.Equal(winner.Output.Plan.Fingerprint, retained.Plan.Fingerprint);
        Assert.Equal(fixture.Plan.Identity, retained.Plan.Identity);
        Assert.Equal(fixture.Plan.ProjectLifetime, retained.Plan.ProjectLifetime);
        Assert.Null(retained.Receipt);
        Assert.Null(await fixture.Owner.FindWorkflowContributionAsync(fixture.Plan.Identity));

        static async Task<(WorkflowStructureOutput? Output, bool Conflict)> PrepareAsync(
            PersistentWorkflowStructureOutputStore owner, WorkflowStructureOutputPlan plan) {
            try {
                return (await owner.PrepareAsync(plan), false);
            } catch (WorkflowStructureOutputConflictException) {
                return (null, true);
            }
        }
    }

    private static PersistentWorkflowStructureOutputStore[] ConcurrentOutputOwners(IServiceProvider services,
        OutputSnapshotBarrier barrier) {
        var options = WorkflowOptions(services, barrier);
        return [Create(), Create()];

        PersistentWorkflowStructureOutputStore Create() => new(new WorkflowFactory(options), TimeProvider.System,
            services.GetRequiredService<IWorkflowStructureSourceAuthorityPolicy>(),
            services.GetRequiredService<CoordinatedDatabaseTransaction>(), options);
    }

    private sealed class OutputSnapshotBarrier : DbTransactionInterceptor {
        private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int capturedSnapshots;
        private int startedTransactions;

        public int CapturedSnapshots => Volatile.Read(ref capturedSnapshots);
        public int StartedTransactions => Volatile.Read(ref startedTransactions);

        public override async ValueTask<DbTransaction> TransactionStartedAsync(DbConnection connection,
            TransactionEndEventData eventData, DbTransaction result, CancellationToken cancellationToken = default) {
            if (Interlocked.Increment(ref startedTransactions) > 2) {
                return result;
            }
            await using var command = connection.CreateCommand();
            command.Transaction = result;
            command.CommandText = "SELECT pg_current_snapshot()::text";
            Assert.IsType<string>(await command.ExecuteScalarAsync(cancellationToken));
            if (Interlocked.Increment(ref capturedSnapshots) == 2) {
                ready.TrySetResult();
            }
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            return result;
        }
    }
}
