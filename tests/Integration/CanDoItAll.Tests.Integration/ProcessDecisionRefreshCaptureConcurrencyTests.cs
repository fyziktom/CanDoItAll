using System.Data;
using System.Data.Common;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionPersistenceFixture;
using static CanDoItAll.Tests.Integration.ProcessDecisionRefreshCaptureFixture;

namespace CanDoItAll.Tests.Integration;

[Trait("Category", "FileSystemPortability")]
public sealed class ProcessDecisionRefreshCaptureConcurrencyTests {
    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot)]
    [InlineData(ReadBasisMode.StepSnapshot)]
    public async Task Preparation_keeps_a_read_only_snapshot_while_a_writer_commits_and_fresh_fence_rejects_stale_work(ReadBasisMode mode) {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync(mode);
        var admission = await AdmitAsync(fixture);
        var state = await AccountAsync(fixture, admission);
        var registered = fixture.Services.GetRequiredService<IProcessDecisionRefreshCaptureQuery>();
        var before = await registered.ReadAsync(state, admission);
        ProcessDecisionRefreshCaptureIdentity identity = new(admission);
        var root = mode == ReadBasisMode.RunSnapshot;
        var budget = root ? before.RootBudget : before.Original.Budget;
        var allocation = root ? identity.RootAllocation : identity.LocalAllocation;
        ProcessRuntimeCommitRequest reservation = new(root ? identity.RootReserve : identity.LocalReserve, state,
            ProcessRuntimeMutation.Applied(state with { UpdatedAtUtc = new(state.UpdatedAtUtc.UtcTicks + 10, TimeSpan.Zero) }, [], [])) {
            DecisionBudget = new ProcessDecisionBudgetReservation(new(root ? null : before.Original.StepInstanceId,
                budget.BudgetId, budget.Revision), allocation, new(TimeSpan.TicksPerSecond, 0)),
            DecisionRunScope = root ? new(before.SharedScope!.Expectation, ProcessDecisionRunScopeCommandKind.BeginCapture,
                new(allocation, identity.Material, ProcessDecisionRunCaptureKind.ManagerRefresh)) : null
        };
        ProcessRuntimeCommitResult? written = null;
        Interleave interleave = new(async () => {
            written = await fixture.Services.GetRequiredService<IProcessRuntimeUnitOfWork>().CommitAsync(reservation);
            Assert.True(written.CommittedNow);
        });
        var query = new EfProcessDecisionRefreshCaptureQuery(new Factory(fixture.Services, interleave),
            fixture.Services.GetRequiredService<ICanonicalRuntimeDatabase>());
        var snapshot = await query.ReadAsync(state, admission);
        Assert.NotNull(written);
        Assert.Equal(IsolationLevel.RepeatableRead, interleave.Isolation);
        Assert.Equal("repeatable read|on|5s|2s|1s", interleave.Settings);
        Assert.Equal(before.RootBudget, snapshot.RootBudget);
        Assert.Equal(before.Original.Budget, snapshot.Original.Budget);
        Assert.Equal(before.SharedScope, snapshot.SharedScope);
        Assert.False(snapshot.RootWorkPending);
        Assert.False(snapshot.Original.OtherWorkPending);
        var current = await registered.ReadAsync(written.State, admission);
        Assert.True(current.RootWorkPending);
        Assert.Equal(!root, current.Original.OtherWorkPending);
        Assert.True(current.RootBudget.Revision > snapshot.RootBudget.Revision);
        var stale = reservation with { CommandId = RuntimeCommandId.New() };
        await Assert.ThrowsAsync<ProcessRuntimeOptimisticConcurrencyException>(() => fixture.Services
            .GetRequiredService<IProcessRuntimeUnitOfWork>().CommitAsync(stale));
    }

    private sealed class Factory(IServiceProvider services, Interleave interleave) : IDbContextFactory<ProcessPersistenceDbContext> {
        public ProcessPersistenceDbContext CreateDbContext() => Context(services, interleave);
    }

    private sealed class Interleave(Func<Task> write) : DbCommandInterceptor {
        private int reads;
        internal IsolationLevel? Isolation { get; private set; }
        internal string? Settings { get; private set; }

        public override async ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            int result, CancellationToken cancellationToken = default) {
            Isolation = command.Transaction?.IsolationLevel;
            await using var inspect = command.Connection!.CreateCommand();
            inspect.Transaction = command.Transaction;
            inspect.CommandText = "SELECT current_setting('transaction_isolation') || '|' || current_setting('transaction_read_only') || '|' || current_setting('transaction_timeout') || '|' || current_setting('statement_timeout') || '|' || current_setting('lock_timeout')";
            Settings = (string)(await inspect.ExecuteScalarAsync(cancellationToken))!;
            return result;
        }

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            if (++reads == 3) {
                await write();
            }
            return result;
        }
    }
}
