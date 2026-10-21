using System.Data.Common;
using System.Diagnostics;
using System.Text.Json;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using Xunit.Abstractions;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionPersistenceFixture;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionRunScopeFixture;

namespace CanDoItAll.Tests.Integration.Processes;

[Trait("Category", "LongRunning")]
public sealed class ProcessDecisionRunScopeCapacityTests(ITestOutputHelper output) {
    [Theory]
    [InlineData(1)]
    [InlineData(32)]
    [InlineData(128)]
    [InlineData(255)]
    public async Task Complete_root_reconciliation_preserves_full_integrity_at_the_finite_capacity(int members) {
        await using var app = await TestApplication.CreateAsync(new ProcessDecisionPublicationTestClock().Harness());
        await using var context = Context(app.Services);
        var setupStarted = Stopwatch.GetTimestamp();
        var initial = Initial(app.Services);
        var owner = Owner(context, app.Services);
        var state = (await owner.CommitAsync(initial)).State;
        var scopeId = initial.InitialRunDecisionAdmission!.MaterialScope!.ScopeId;
        var scope = (await Scopes(context, app.Services).ReadAsync(scopeId))!;
        var captured = await CaptureAsync(context, app.Services, state, scope);
        var joined = await JoinZeroEffectAsync(captured.State);
        state = (await owner.CommitAsync(Claim(joined.State))).State;
        for (var index = 1; index < members; index++) {
            var descendant = await owner.CommitAsync(Descendant(state, initial.InitialRunDecisionAdmission));
            var member = await JoinZeroEffectAsync(descendant.State);
            var completed = await QuiesceAsync(context, app.Services, member.State, ProcessDecisionPhaseChange.Complete);
            Assert.Equal(ProcessDecisionPhase.Completed, completed.Decision!.Current.Phase);
        }
        state = (await QuiesceAsync(context, app.Services, state, ProcessDecisionPhaseChange.Complete)).State;
        scope = (await Scopes(context, app.Services).ReadAsync(scopeId))!;
        Assert.Equal(members, scope.CurrentMemberships);
        var budget = (await new EfProcessDecisionBudgetStore(context).ReadAsync(state.RunId, null))!;
        Assert.Equal(members + 1, budget.AllocationCount);
        Assert.Equal(3, budget.ConsumedWorkTicks);
        Assert.Equal(0, budget.ReservedWorkTicks);
        Assert.InRange(budget.AllocationCount, 1, DecisionBudgetBalance.MaximumAllocations);
        output.WriteLine(JsonSerializer.Serialize(new {
            Workload = "shared-root-capacity-setup", Members = members, Allocations = budget.AllocationCount,
            ElapsedMilliseconds = Stopwatch.GetElapsedTime(setupStarted).TotalMilliseconds
        }));

        var probe = new ReconciliationReads();
        await using var finishing = Context(app.Services, probe);
        var request = ScopeRequest(state, new(scope.Expectation, ProcessDecisionRunScopeCommandKind.Complete));
        var allocated = GC.GetTotalAllocatedBytes(true);
        var started = Stopwatch.GetTimestamp();
        var result = await Owner(finishing, app.Services).CommitAsync(request);
        var elapsed = Stopwatch.GetElapsedTime(started);
        var allocatedBytes = GC.GetTotalAllocatedBytes(true) - allocated;
        Assert.True(result.CommittedNow);
        Assert.Equal(ProcessDecisionRunScopePhase.Completed, result.DecisionRunScope!.State.Phase);
        Assert.Equal(2, probe.Selected.Count);
        output.WriteLine(JsonSerializer.Serialize(new {
            Workload = "shared-root-capacity-finish", Members = members, Allocations = budget.AllocationCount,
            ElapsedMilliseconds = elapsed.TotalMilliseconds, ProcessAllocatedBytes = allocatedBytes,
            ReaderCommands = probe.ReaderCommands, CompleteSetQueries = probe.Selected.Count
        }));
        await finishing.Database.OpenConnectionAsync();
        foreach (var selected in probe.Selected) {
            Assert.DoesNotContain("\"PayloadJson\"", selected.Sql, StringComparison.Ordinal);
            Assert.DoesNotContain("\"InputJson\"", selected.Sql, StringComparison.Ordinal);
            await using var command = finishing.Database.GetDbConnection().CreateCommand();
            command.CommandTimeout = 5;
            command.CommandText = "EXPLAIN (ANALYZE, BUFFERS, VERBOSE, FORMAT JSON) " + selected.Sql;
            command.Parameters.AddRange(selected.Parameters);
            var plan = Assert.IsType<string>(await command.ExecuteScalarAsync());
            using var parsed = JsonDocument.Parse(plan);
            var actualRows = parsed.RootElement[0].GetProperty("Plan").GetProperty("Actual Rows").GetDouble();
            Assert.Equal((double)(selected.Kind == ProjectionKind.Members ? members : budget.AllocationCount), actualRows);
            output.WriteLine(JsonSerializer.Serialize(new {
                Workload = "shared-root-capacity-query", Members = members, Kind = selected.Kind.ToString(), Plan = plan
            }));
        }
        Assert.InRange(elapsed.TotalMilliseconds, 0, 1000);

        async Task<ProcessRuntimeCommitResult> JoinZeroEffectAsync(ProcessRuntimeStateSnapshot current) {
            var shared = (await Scopes(context, app.Services).ReadAsync(scopeId))!;
            var join = await JoinRequestAsync(context, app.Services, current, shared);
            var settlement = Assert.IsType<ProcessDecisionBudgetSettlement>(join.DecisionBudget);
            return await owner.CommitAsync(join with { DecisionBudget = settlement with { Settlement = new(1, new(0, 0)) } });
        }
    }

    private enum ProjectionKind { Members, Allocations }

    private sealed record SelectedRead(ProjectionKind Kind, string Sql, NpgsqlParameter[] Parameters);

    private sealed class ReconciliationReads : DbCommandInterceptor {
        internal int ReaderCommands { get; private set; }
        internal List<SelectedRead> Selected { get; } = [];

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            ReaderCommands++;
            if (!command.CommandText.StartsWith("SELECT", StringComparison.Ordinal)) {
                return ValueTask.FromResult(result);
            }
            ProjectionKind? kind = null;
            if (command.CommandText.Contains("FROM process_decision_run_scope_members", StringComparison.Ordinal)) {
                kind = ProjectionKind.Members;
            } else if (command.CommandText.Contains("FROM process_decision_budget_allocations", StringComparison.Ordinal)) {
                kind = ProjectionKind.Allocations;
            }
            if (kind is { } selected) {
                Selected.Add(new(selected, command.CommandText,
                    command.Parameters.Cast<NpgsqlParameter>().Select(item => (NpgsqlParameter)((ICloneable)item).Clone()).ToArray()));
            }
            return ValueTask.FromResult(result);
        }
    }
}
