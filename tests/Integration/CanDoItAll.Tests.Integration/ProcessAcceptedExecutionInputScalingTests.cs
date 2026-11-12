using System.Data.Common;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit.Abstractions;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionPersistenceFixture;

namespace CanDoItAll.Tests.Integration.Processes;

[Trait("Category", "LongRunning")]
public sealed class ProcessAcceptedExecutionInputScalingTests(ITestOutputHelper output) {
    [Theory]
    [InlineData(1, 256)]
    [InlineData(128, 256)]
    [InlineData(1024, 256)]
    [InlineData(128, 1_048_576)]
    public async Task Selected_original_input_cost_does_not_follow_neighboring_steps_or_prompts(int steps, int neighboringPromptCharacters) {
        await using var fixture = await ProjectTaskPlanOperationFixture.CreateAsync();
        var services = fixture.Services;
        var source = await services.GetRequiredService<ProjectProcessLaunchAuthorityService>()
            .CaptureLocalAsync(fixture.Project.ProjectId, ProcessLaunchOperatorSurface.UserInterface);
        var preparation = ProcessPreparedLaunchScalingFixture.Create(steps, 256, source);
        var assignments = preparation.InitialCommit.InitialAssignments!.ToArray();
        if (steps > 1) {
            assignments[0] = assignments[0] with { Prompt = new('x', neighboringPromptCharacters) };
        }
        preparation = preparation with { InitialCommit = preparation.InitialCommit with { InitialAssignments = assignments } };
        var saved = await services.GetRequiredService<IProcessPreparedLaunchStore>().PrepareAsync(preparation);
        await using (var accepting = Context(services)) {
            var start = Stopwatch.GetTimestamp();
            Assert.True((await ProcessRootMaterialFixture.Owner(accepting, services)
                .CommitAsync(ProcessPreparedLaunchFixture.Commit(saved))).Succeeded);
            output.WriteLine(JsonSerializer.Serialize(new {
                Workload = "original-input-acceptance", Steps = steps, NeighborPromptCharacters = neighboringPromptCharacters,
                ElapsedMilliseconds = Stopwatch.GetElapsedTime(start).TotalMilliseconds
            }));
        }
        var selected = assignments[^1];
        var probe = new ReadProbe();
        await using var context = Context(services, probe);
        var reader = new EfProcessAcceptedExecutionInputReader(context, services.GetRequiredService<ICanonicalRuntimeDatabase>());
        for (var warmup = 0; warmup < 3; warmup++) {
            AssertOriginal(await reader.ReadAsync(selected.RunId, selected.StepInstanceId));
        }
        probe.Reads = 0;
        probe.Capture = true;
        AssertOriginal(await reader.ReadAsync(selected.RunId, selected.StepInstanceId));
        Assert.Equal(1, probe.Reads);
        Assert.DoesNotContain("process_runtime_step_assignments", probe.Sql!, StringComparison.Ordinal);
        Assert.DoesNotContain("process_instance_plans", probe.Sql!, StringComparison.Ordinal);
        Assert.Contains("\"InputJson\"", probe.Sql!, StringComparison.Ordinal);
        Assert.Single(probe.Columns!, name => name == "PayloadJson");
        Assert.Single(probe.Columns!, name => name == "InputJson");
        Assert.Single(probe.Columns!, name => name == "AcceptedSourceHeaderJson");
        await context.Database.OpenConnectionAsync();
        await using (var command = context.Database.GetDbConnection().CreateCommand()) {
            command.CommandText = "EXPLAIN (ANALYZE, BUFFERS, VERBOSE, FORMAT JSON) " + probe.Sql;
            command.CommandTimeout = 5;
            command.Parameters.AddRange(probe.Parameters!);
            var plan = Assert.IsType<string>(await command.ExecuteScalarAsync());
            using var parsed = JsonDocument.Parse(plan);
            Assert.Equal(1d, parsed.RootElement[0].GetProperty("Plan").GetProperty("Actual Rows").GetDouble());
            if (steps >= 1024) {
                Assert.Contains("PK_process_accepted_assignment_sources", plan, StringComparison.Ordinal);
            }
            output.WriteLine(JsonSerializer.Serialize(new { Workload = "original-input-query-plan", Steps = steps, probe.Sql, Plan = plan }));
        }
        probe.Capture = false;
        var row = await context.AcceptedAssignmentSources.AsNoTracking().SingleAsync(item => item.StepInstanceId == selected.StepInstanceId.Value);
        var headerBytes = await context.PreparedLaunches.Where(item => item.Id == row.AdmissionId)
            .Select(item => item.AcceptedSourceHeaderJson!).SingleAsync();
        var bytes = Encoding.UTF8.GetByteCount(headerBytes) + Encoding.UTF8.GetByteCount(row.PayloadJson) + Encoding.UTF8.GetByteCount(row.InputJson!);
        Assert.InRange(bytes, 2, 12_288);
        for (var round = 0; round < 3; round++) {
            const int repetitions = 8;
            probe.Reads = 0;
            var allocated = GC.GetTotalAllocatedBytes(true);
            var start = Stopwatch.GetTimestamp();
            for (var index = 0; index < repetitions; index++) {
                AssertOriginal(await reader.ReadAsync(selected.RunId, selected.StepInstanceId));
            }
            var elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            var allocatedBytes = GC.GetTotalAllocatedBytes(true) - allocated;
            Assert.Equal(repetitions, probe.Reads);
            output.WriteLine(JsonSerializer.Serialize(new {
                Workload = "original-input-selected-read", Steps = steps, NeighborPromptCharacters = neighboringPromptCharacters,
                Round = round, Repetitions = repetitions, ElapsedMilliseconds = elapsed, ProcessAllocatedBytes = allocatedBytes,
                SelectedPayloadUtf8Bytes = bytes, Queries = probe.Reads
            }));
        }
        probe.Reads = 0;
        probe.Capture = true;
        var state = await context.RuntimeStates.AsNoTracking().SingleAsync(item => item.RunId == selected.RunId.Value);
        probe.Reads = 0;
        await EfProcessAcceptedSource.ReadProjectedAsync(context, state, selected.StepInstanceId, CancellationToken.None);
        Assert.Equal(1, probe.Reads);
        Assert.DoesNotContain("\"InputJson\"", probe.Sql!, StringComparison.Ordinal);
        Assert.DoesNotContain("\"InputFingerprint\"", probe.Sql!, StringComparison.Ordinal);

        void AssertOriginal(ProcessAcceptedExecutionInput input) {
            Assert.Equal(selected.Prompt, input.Assignment.Prompt);
            Assert.Equal(selected.StepInstanceId, input.Assignment.StepInstanceId);
            Assert.Equal(saved.PreparationFingerprint, input.PreparationFingerprint);
            Assert.Equal(preparation.InitialCommit.InitialPlan!.Steps[^1].StepDefinitionId, input.RequireRequirements().StepDefinitionId);
        }
    }

    private sealed class ReadProbe : DbCommandInterceptor {
        internal int Reads { get; set; }
        internal bool Capture { get; set; }
        internal string? Sql { get; private set; }
        internal NpgsqlParameter[]? Parameters { get; private set; }
        internal string[]? Columns { get; private set; }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            Reads++;
            if (Capture) {
                Sql = command.CommandText;
                Parameters = command.Parameters.Cast<NpgsqlParameter>().Select(item => (NpgsqlParameter)((ICloneable)item).Clone()).ToArray();
            }
            return ValueTask.FromResult(result);
        }

        public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            DbDataReader result, CancellationToken cancellationToken = default) {
            if (Capture) {
                Columns = Enumerable.Range(0, result.FieldCount).Select(result.GetName).ToArray();
            }
            return ValueTask.FromResult(result);
        }
    }
}
