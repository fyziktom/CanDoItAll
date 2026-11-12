using System.Data.Common;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionPersistenceFixture;
using static CanDoItAll.Tests.Integration.Processes.ProcessCancellationRecoveryFixture;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed class ProcessCancellationRecoverySeedTests {
    [Fact]
    public async Task Finite_pages_skip_existing_live_markers_and_do_not_hydrate_runtime_payloads() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        for (var index = 0; index < 5; index++) {
            await RootAsync(context, legacy: true);
        }
        var live = await RootAsync(context);
        await new EfProcessRuntimeUnitOfWork(context).CommitAsync(ProcessProjectAdmissionFixture.Initial());
        var probe = new SeedProbe();
        var queue = Queue(app.Services, probe);
        var inserted = 0;
        for (var page = 0; page < 3; page++) {
            var result = await queue.SeedLegacyAsync(2);
            Assert.Equal(ProcessCancellationSeedStatus.Advanced, result.Status);
            Assert.Equal(2, result.Selected);
            inserted += result.Inserted;
        }
        Assert.Equal(5, inserted);
        Assert.Equal(new(ProcessCancellationSeedStatus.Completed, 0, 0), await queue.SeedLegacyAsync(2));
        var saved = await context.CancellationRecoverySeeds.AsNoTracking().SingleAsync();
        Assert.True(saved.Completed);
        Assert.Equal(4, saved.Revision);
        Assert.Equal(ProcessCancellationRecoveryOrigin.LiveBarrier,
            (await context.CancellationRecoveries.AsNoTracking().SingleAsync(item => item.RootRunId == live.RunId.Value)).Origin);
        Assert.Equal(6, await context.CancellationRecoveries.CountAsync());
        Assert.Equal(4, probe.Selections.Count);
        Assert.All(probe.Selections, sql => {
            Assert.Contains("LIMIT", sql, StringComparison.Ordinal);
            Assert.DoesNotContain("JOIN", sql, StringComparison.Ordinal);
            Assert.DoesNotContain("PayloadJson", sql, StringComparison.Ordinal);
        });
        Assert.Equal(3, probe.Inserts);
        Assert.Equal(new(ProcessCancellationSeedStatus.Completed, 0, 0), await queue.SeedLegacyAsync());
        Assert.Equal(saved.Revision, (await context.CancellationRecoverySeeds.AsNoTracking().SingleAsync()).Revision);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => queue.SeedLegacyAsync(65));
    }

    [Fact]
    public async Task Busy_seed_is_explicit_and_a_missing_seed_is_corruption() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var queue = Queue(app.Services);
        await using (var transaction = await context.Database.BeginTransactionAsync()) {
            await context.Database.ExecuteSqlRawAsync("SELECT 1 FROM process_cancellation_recovery_seed FOR UPDATE");
            Assert.Equal(new(ProcessCancellationSeedStatus.Busy, 0, 0), await queue.SeedLegacyAsync().WaitAsync(TimeSpan.FromSeconds(2)));
        }
        Assert.Equal(new(ProcessCancellationSeedStatus.Completed, 0, 0), await queue.SeedLegacyAsync());
        await context.CancellationRecoverySeeds.ExecuteDeleteAsync();
        await Assert.ThrowsAsync<InvalidDataException>(() => queue.SeedLegacyAsync());
    }

    [Fact]
    public async Task Failed_seed_rolls_back_its_page_and_restart_keeps_new_live_roots_below_the_cursor() {
        await using var environment = CanDoItAllTestEnvironment.Create("process-decision-generation");
        var harness = new TestHarnessOptions { TestEnvironment = environment, ActiveProfile = environment.CreatePostgreSqlProfile("cancellation-seed-restart") };
        Guid last;
        await using (var app = await TestApplication.CreateAsync(harness)) {
            await using var context = Context(app.Services);
            await RootAsync(context, legacy: true);
            await Assert.ThrowsAsync<IOException>(() => Queue(app.Services, new SeedProbe(failAfterInsert: true)).SeedLegacyAsync(1));
            Assert.Empty(await context.CancellationRecoveries.ToArrayAsync());
            Assert.Equal(0, (await context.CancellationRecoverySeeds.AsNoTracking().SingleAsync()).Revision);
            Assert.Equal(1, (await Queue(app.Services).SeedLegacyAsync(1)).Inserted);
            last = (await context.CancellationRecoverySeeds.AsNoTracking().SingleAsync()).LastRootRunId!.Value;
        }
        await using var restarted = await TestApplication.CreateAsync(harness);
        await using var reopened = Context(restarted.Services);
        var initial = ProcessProjectAdmissionFixture.Initial();
        var lower = new Guid("00000000-0000-0000-0000-000000000001");
        Assert.True(lower.CompareTo(last) < 0);
        var original = initial.OriginalState;
        var state = original with { RunId = new(lower), RootRunId = new(lower) };
        initial = initial with { OriginalState = state, Mutation = initial.Mutation with {
            State = state, Events = initial.Mutation.Events.Select(item => item with { RunId = state.RunId, RootRunId = state.RootRunId }).ToArray()
        }, InitialAssignments = initial.InitialAssignments!.Select(item => item with { RunId = state.RunId }).ToArray() };
        var owner = new EfProcessRuntimeUnitOfWork(reopened);
        state = (await owner.CommitAsync(initial)).State;
        await ProcessRootCancellationFixture.StopAsync(owner, state);
        Assert.Equal(new(ProcessCancellationSeedStatus.Completed, 0, 0), await Queue(restarted.Services).SeedLegacyAsync(1));
        Assert.Equal(ProcessCancellationRecoveryOrigin.LiveBarrier,
            (await reopened.CancellationRecoveries.AsNoTracking().SingleAsync(item => item.RootRunId == lower)).Origin);
    }

    private sealed class SeedProbe(bool failAfterInsert = false) : DbCommandInterceptor {
        internal List<string> Selections { get; } = [];
        internal int Inserts { get; private set; }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            if (command.CommandText.Contains("FROM process_runtime_states", StringComparison.Ordinal)) {
                Selections.Add(command.CommandText);
            }
            return ValueTask.FromResult(result);
        }
        public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData, int result,
            CancellationToken cancellationToken = default) {
            if (command.CommandText.Contains("INSERT INTO process_cancellation_recovery", StringComparison.Ordinal)) {
                Inserts++;
                if (failAfterInsert) {
                    throw new IOException("Injected failure after a bounded seed page insert.");
                }
            }
            return ValueTask.FromResult(result);
        }
    }
}
