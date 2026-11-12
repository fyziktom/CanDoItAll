using System.Data.Common;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionPersistenceFixture;
using static CanDoItAll.Tests.Integration.Processes.ProcessRootCancellationFixture;
using static CanDoItAll.Tests.Integration.Processes.ProcessCancellationRecoveryFixture;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed class ProcessCancellationDecisionRecoveryTests {
    [Fact]
    public async Task Native_decision_and_child_keysets_are_root_bound_metadata_queries() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var started = await StartAsync(context);
        var first = await EnrollAsync(context, started.State);
        var second = await EnrollAsync(context, first.State);
        var other = await StartAsync(context);
        Assert.True(second.Succeeded);
        Assert.True(other.Succeeded);
        var queries = new QueryProbe();
        var reader = new EfProcessCancellationDecisionReader(new Factory(Options(app.Services, queries)));
        var reference = Assert.IsType<ProcessDecisionReference>(await reader.NextAsync(started.State.RootRunId, null, default));
        Assert.Equal(second.Decision!.Current.Reference, reference);
        Assert.Null(await reader.NextAsync(started.State.RootRunId, reference.DecisionId, default));
        var selected = (await reader.ReadAsync(started.State.RootRunId, reference, default))!;
        Assert.Equal(started.State.RunId, selected.RunId);
        Assert.Equal(Step(started.State), selected.StepInstanceId);
        Assert.False(selected.HistoricalWorkPending);
        Assert.Null(await reader.ReadAsync(other.State.RootRunId, reference, default));
        Assert.Null(await reader.ReadAsync(started.State.RootRunId, reference with { Generation = 2 }, default));
        var children = new List<ProcessDecisionChildId>();
        ProcessDecisionChildId? after = null;
        for (var index = 0; index < 3; index++) {
            after = await reader.NextChildAsync(started.State.RootRunId, reference, after, default);
            if (after is { } child) {
                children.Add(child);
            }
        }
        Assert.Null(after);
        Assert.Equal(new[] { first.Decision!.Child!.ChildId, second.Decision.Child!.ChildId }.OrderBy(item => item.Value), children);
        Assert.Null(await reader.NextChildAsync(other.State.RootRunId, reference, null, default));
        Assert.All(queries.Commands, query => {
            Assert.Equal(5, query.Timeout);
            Assert.DoesNotContain("MaterialJson", query.Sql, StringComparison.Ordinal);
            Assert.DoesNotContain("SnapshotJson", query.Sql, StringComparison.Ordinal);
            Assert.DoesNotContain("process_runtime_steps", query.Sql, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task Historical_unclosed_child_is_preserved_as_pending_under_the_current_generation() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var started = await StartAsync(context);
        var enrolled = await EnrollAsync(context, started.State);
        var closed = await CloseAsync(context, enrolled.State, enrolled.Decision!.Child!, NoEffect(), 5);
        var reconciling = await PhaseAsync(context, closed.State, ProcessDecisionPhaseChange.BeginReconciliation);
        var refreshed = await RefreshAsync(context, reconciling.State, ProcessDecisionRefreshKind.ManagerRefresh);
        Assert.True(refreshed.Succeeded);
        var historical = await context.DecisionChildren.SingleAsync();
        historical.OwnerRevision = null;
        historical.Outcome = null;
        historical.AcceptedReceiptId = null;
        await context.SaveChangesAsync();
        var reader = new EfProcessCancellationDecisionReader(new Factory(Options(app.Services)));
        var current = refreshed.Decision!.Current.Reference;
        var selection = (await reader.ReadAsync(started.State.RootRunId, current, default))!;
        Assert.True(selection.HistoricalWorkPending);
        Assert.Equal(current, selection.Reference);
        Assert.Null(await reader.NextChildAsync(started.State.RootRunId, current, null, default));
        Assert.Null(await reader.ReadAsync(started.State.RootRunId, enrolled.Decision.Current.Reference, default));
    }

    [Fact]
    public async Task Empty_terminal_decision_finishes_through_registered_stage_without_business_effects() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var initial = await StartAsync(context);
        var stopped = await StopAsync(new EfProcessRuntimeUnitOfWork(context), initial.State);
        var events = await context.RuntimeEvents.CountAsync();
        var budget = await BudgetAsync(context, stopped);
        var queue = Queue(app.Services);
        var claim = (await queue.ClaimAsync(ProcessCancellationRecoveryOrigin.LiveBarrier, Lease))!;
        claim = (await queue.CheckpointAsync(claim, new(new(ProcessCancellationRecoveryStage.Decisions)))).Claim!;
        for (var operation = 0; operation < 6 && claim.Position.Stage == ProcessCancellationRecoveryStage.Decisions; operation++) {
            await using var scope = app.Services.CreateAsyncScope();
            var checkpoint = await scope.ServiceProvider.GetRequiredService<ProcessCancellationDecisionRecoveryStep>().AdvanceAsync(claim, default);
            Assert.Equal(ProcessCancellationRecoveryPending.None, checkpoint.Pending);
            claim = (await queue.CheckpointAsync(claim, checkpoint)).Claim!;
        }
        Assert.Equal(ProcessCancellationRecoveryStage.SharedScope, claim.Position.Stage);
        Assert.Equal(ProcessDecisionPhase.Cancelled, (await DecisionAsync(context, stopped)).Phase);
        Assert.Equal(budget, await BudgetAsync(context, stopped));
        Assert.Equal(events, await context.RuntimeEvents.CountAsync());
    }

    private sealed class QueryProbe : DbCommandInterceptor {
        internal List<(string Sql, int Timeout)> Commands { get; } = [];
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            Commands.Add((command.CommandText, command.CommandTimeout));
            return ValueTask.FromResult(result);
        }
    }
}
