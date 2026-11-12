using System.Data.Common;
using System.Text.Json;
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
public sealed class ProcessDecisionRefreshCaptureQueryTests {
    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot)]
    [InlineData(ReadBasisMode.StepSnapshot)]
    public async Task Registered_query_requires_accounted_original_child_and_retains_both_budget_owners(ReadBasisMode mode) {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync(mode);
        var admission = await AdmitAsync(fixture);
        var query = fixture.Services.GetRequiredService<IProcessDecisionRefreshCaptureQuery>();
        await Assert.ThrowsAsync<InvalidDataException>(() => query.ReadAsync(fixture.State, admission));
        var state = await AccountAsync(fixture, admission);
        var selected = await query.ReadAsync(state, admission);
        Assert.Equal(fixture.Parent, selected.Original.Child!.Original);
        Assert.Equal(ProcessDecisionChildOutcome.NoEffect, selected.Original.Child.Observation.Closure!.Outcome);
        Assert.Equal(ProcessDecisionPhase.Reconciling, selected.Original.Head!.Phase);
        Assert.Equal(admission, selected.Original.Head.RefreshCommandId);
        Assert.Equal(selected.Root.Funding, selected.Input.Assignment.DecisionDataPolicy!.Funding);
        Assert.Equal(selected.Root.Policy.Budget, selected.RootBudget.Ceiling);
        Assert.Equal(selected.Input.Assignment.DecisionDataPolicy.Policy.Budget, selected.Original.Budget.Ceiling);
        Assert.Equal(selected.Root.BudgetId, selected.RootBudget.BudgetId);
        Assert.NotEqual(selected.RootBudget.BudgetId, selected.Original.Budget.BudgetId);
        Assert.Equal(fixture.Parent.Scope.DatabaseProfileId, selected.Input.Source.DatabaseProfileId);
        Assert.Equal(state.Status, selected.RootStatus);
        Assert.False(selected.RootWorkPending);
        Assert.False(selected.Original.OtherWorkPending);
        Assert.Equal(mode == ReadBasisMode.RunSnapshot, selected.SharedScope is not null);
        if (selected.SharedScope is { } scope) {
            Assert.Equal(ProcessDecisionRunScopePhase.Reconciling, scope.Phase);
            Assert.Equal(selected.Original.SharedScope, scope.Expectation);
            Assert.Equal(fixture.Parent.Material, scope.Material!.Stamp);
        }
        Assert.Equal(JsonSerializer.Serialize(state), JsonSerializer.Serialize(await fixture.Services
            .GetRequiredService<IProcessRuntimeStateStore>().LoadAsync(state.RunId)));
        await fixture.Entry.Files.Sql.VerifyAsync(0, 0);
        Assert.Single(fixture.Entry.Wire.Requests);
    }

    public enum ChangedOriginal { Admission, Root, Plan, Launch }

    [Theory]
    [InlineData(ChangedOriginal.Admission)]
    [InlineData(ChangedOriginal.Root)]
    [InlineData(ChangedOriginal.Plan)]
    [InlineData(ChangedOriginal.Launch)]
    public async Task Selection_rejects_another_original_identity(ChangedOriginal change) {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync();
        var admission = await AdmitAsync(fixture);
        var state = await AccountAsync(fixture, admission);
        state = change switch {
            ChangedOriginal.Root => state with { RootRunId = new(Guid.NewGuid()) },
            ChangedOriginal.Plan => state with { PlanId = new(Guid.NewGuid()) },
            ChangedOriginal.Launch => state with { LaunchAdmissionId = new(Guid.NewGuid()) },
            _ => state
        };
        var query = fixture.Services.GetRequiredService<IProcessDecisionRefreshCaptureQuery>();
        if (change == ChangedOriginal.Admission) {
            await Assert.ThrowsAsync<InvalidOperationException>(() => query.ReadAsync(state, RuntimeCommandId.New()));
        } else {
            await Assert.ThrowsAsync<InvalidDataException>(() => query.ReadAsync(state, admission));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Changed_local_or_root_ceiling_cannot_authorize_capture(bool root) {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync();
        var admission = await AdmitAsync(fixture);
        var state = await AccountAsync(fixture, admission);
        await using (var context = Context(fixture.Services)) {
            var budget = await context.DecisionBudgets.SingleAsync(row => row.RunId == state.RunId.Value && (row.StepInstanceId == null) == root);
            budget.MaximumWorkTicks += TimeSpan.TicksPerSecond;
            await context.SaveChangesAsync();
        }
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Services.GetRequiredService<IProcessDecisionRefreshCaptureQuery>()
            .ReadAsync(state, admission));
    }

    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot)]
    [InlineData(ReadBasisMode.StepSnapshot)]
    public async Task Progress_retains_original_reservation_and_abort_without_loading_histories(ReadBasisMode mode) {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync(mode);
        var admission = await AdmitAsync(fixture);
        var state = await AccountAsync(fixture, admission);
        var registered = fixture.Services.GetRequiredService<IProcessDecisionRefreshCaptureQuery>();
        var selected = await registered.ReadAsync(state, admission);
        ProcessDecisionRefreshCaptureIdentity identity = new(admission);
        var root = selected.SharedScope is not null;
        var allocation = root ? identity.RootAllocation : identity.LocalAllocation;
        var balance = root ? selected.RootBudget : selected.Original.Budget;
        var reserve = TimestampOnly(state, root ? identity.RootReserve : identity.LocalReserve) with {
            DecisionBudget = new ProcessDecisionBudgetReservation(new(root ? null : fixture.Request.Child.StepInstanceId,
                balance.BudgetId, balance.Revision), allocation, new(TimeSpan.TicksPerSecond, 0)),
            DecisionRunScope = root ? new(selected.SharedScope!.Expectation, ProcessDecisionRunScopeCommandKind.BeginCapture,
                new(allocation, identity.Material, ProcessDecisionRunCaptureKind.ManagerRefresh)) : null
        };
        var owner = fixture.Services.GetRequiredService<IProcessRuntimeUnitOfWork>();
        var reserved = await owner.CommitAsync(reserve);
        Assert.True(reserved.CommittedNow);
        var pending = await registered.ReadAsync(reserved.State, admission);
        Assert.True(pending.RootWorkPending);
        Assert.Equal(!root, pending.Original.OtherWorkPending);
        if (root) {
            Assert.Equal(ProcessDecisionRunScopePhase.Capturing, pending.SharedScope!.Phase);
        }
        var budget = reserved.DecisionBudget!.Change.Balance;
        var aborted = await owner.CommitAsync(TimestampOnly(reserved.State, root ? identity.RootAbort : identity.LocalAbort) with {
            DecisionBudget = new ProcessDecisionBudgetSettlement(new(root ? null : fixture.Request.Child.StepInstanceId,
                budget.BudgetId, budget.Revision), allocation, new(1, new(2, 0))),
            DecisionRunScope = root ? new(reserved.DecisionRunScope!.State.Expectation, ProcessDecisionRunScopeCommandKind.AbortCapture) : null
        });
        Assert.True(aborted.CommittedNow);
        SqlReads reads = new();
        var query = new EfProcessDecisionRefreshCaptureQuery(new Factory(fixture.Services, reads),
            fixture.Services.GetRequiredService<ICanonicalRuntimeDatabase>());
        var progress = await query.ReadProgressAsync(state.RunId, admission);
        var reservation = root ? progress.RootReservation : progress.LocalReservation;
        var abort = root ? progress.RootAbort : progress.LocalAbort;
        Assert.Equal(reserved.Outcome, reservation!.Outcome);
        Assert.Equal(reserved.DecisionBudget, reservation.Budget);
        Assert.Equal(reserved.DecisionRunScope, reservation.Scope);
        Assert.Equal(aborted.DecisionBudget, abort!.Budget);
        Assert.Equal(aborted.DecisionRunScope, abort.Scope);
        Assert.Null(progress.Successor);
        Assert.Null(progress.RootCompletion);
        var sql = Assert.Single(reads.Commands);
        Assert.Contains("CommandId", sql, StringComparison.Ordinal);
        await using (var context = Context(fixture.Services)) {
            foreach (var entity in context.Model.GetEntityTypes()) {
                if (entity.ClrType == typeof(ProcessRuntimeIdempotencyEntity)) {
                    Assert.Contains(entity.GetTableName()!, sql, StringComparison.Ordinal);
                } else {
                    Assert.DoesNotContain(entity.GetTableName()!, sql, StringComparison.Ordinal);
                }
            }
        }
        var completed = await registered.ReadAsync(aborted.State, admission);
        Assert.False(completed.RootWorkPending);
        Assert.False(completed.Original.OtherWorkPending);
        Assert.Equal(ProcessDecisionPhase.Reconciling, completed.Original.Head!.Phase);
    }

    [Fact]
    public async Task Progress_is_scoped_to_the_original_run_and_does_not_require_source_payloads() {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync();
        var admission = await AdmitAsync(fixture);
        var query = fixture.Services.GetRequiredService<IProcessDecisionRefreshCaptureQuery>();
        ProcessDecisionRefreshCaptureProgress empty = new(null, null, null, null, null, null);
        Assert.Equal(empty, await query.ReadProgressAsync(new(Guid.NewGuid()), admission));
        Assert.Equal(empty, await query.ReadProgressAsync(fixture.State.RunId, admission));
        Assert.Equal(empty, await query.ReadProgressAsync(fixture.State.RunId, RuntimeCommandId.New()));
    }

    private static ProcessRuntimeCommitRequest TimestampOnly(ProcessRuntimeStateSnapshot state, RuntimeCommandId command)
        => new(command, state, ProcessRuntimeMutation.Applied(state with {
            UpdatedAtUtc = new(state.UpdatedAtUtc.UtcTicks / 10 * 10 + 10, TimeSpan.Zero)
        }, [], []));

    private sealed class Factory(IServiceProvider services, SqlReads reads) : IDbContextFactory<ProcessPersistenceDbContext> {
        public ProcessPersistenceDbContext CreateDbContext() => Context(services, reads);
    }

    private sealed class SqlReads : DbCommandInterceptor {
        internal List<string> Commands { get; } = [];
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            Commands.Add(command.CommandText);
            return ValueTask.FromResult(result);
        }
    }
}
