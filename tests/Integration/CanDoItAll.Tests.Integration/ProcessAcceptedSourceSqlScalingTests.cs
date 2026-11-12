using System.Data.Common;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit.Abstractions;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionPersistenceFixture;

namespace CanDoItAll.Tests.Integration.Processes;

[Trait("Category", "LongRunning")]
public sealed class ProcessAcceptedSourceSqlScalingTests(ITestOutputHelper output) {
    [Theory]
    [InlineData(1, 256)]
    [InlineData(64, 256)]
    [InlineData(256, 256)]
    [InlineData(1024, 256)]
    [InlineData(1, 1_048_576)]
    public async Task Actual_source_query_keeps_selected_bytes_bounded_and_reports_acceptance_and_legacy_cost(int steps, int promptCharacters) {
        await using var app = await TestApplication.CreateAsync(new ProcessDecisionPublicationTestClock().Harness());
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var preparation = ProcessPreparedLaunchScalingFixture.Create(steps, promptCharacters);
        var saved = await services.GetRequiredService<IProcessPreparedLaunchStore>().PrepareAsync(preparation);
        var transaction = new TransactionProbe();
        await using (var accepting = Context(services, transaction)) {
            var start = Stopwatch.GetTimestamp();
            Assert.True((await ProcessDecisionRunScopeFixture.Owner(accepting, services).CommitAsync(ProcessPreparedLaunchFixture.Commit(saved))).Succeeded);
            output.WriteLine(JsonSerializer.Serialize(new {
                Workload = "accepted-source-actual-acceptance", Steps = steps, PromptCharacters = promptCharacters,
                CommitMilliseconds = Stopwatch.GetElapsedTime(start).TotalMilliseconds, transaction.TransactionMilliseconds
            }));
            Assert.True(transaction.TransactionMilliseconds > 0);
        }
        var probe = new SourceReadProbe();
        await using var context = Context(services, probe);
        var state = await context.RuntimeStates.AsNoTracking().SingleAsync(item => item.RunId == preparation.InitialCommit.OriginalState.RunId.Value);
        var selected = preparation.InitialCommit.InitialAssignments![^1];
        var launch = await context.PreparedLaunches.AsNoTracking().SingleAsync(item => item.Id == preparation.AdmissionId.Value);
        var assignment = await context.AcceptedAssignmentSources.AsNoTracking().SingleAsync(item => item.AdmissionId == launch.Id && item.StepInstanceId == selected.StepInstanceId.Value);
        var selectedBytes = Encoding.UTF8.GetByteCount(launch.AcceptedSourceHeaderJson!) + Encoding.UTF8.GetByteCount(assignment.PayloadJson);
        Assert.InRange(selectedBytes, 2, 4096);
        Assert.Equal(steps, await context.AcceptedAssignmentSources.CountAsync(item => item.AdmissionId == launch.Id));
        var storedBytes = await context.Database.SqlQuery<long>($"""
            SELECT octet_length(p."AcceptedSourceHeaderJson")::bigint + sum(octet_length(s."PayloadJson")) AS "Value"
            FROM process_prepared_launches p JOIN process_accepted_assignment_sources s ON s."AdmissionId" = p."Id"
            WHERE p."Id" = {launch.Id} GROUP BY p."AcceptedSourceHeaderJson"
            """).SingleAsync();
        foreach (var version in new[] { ProcessAcceptedSourceVersion.OriginalProjection, ProcessAcceptedSourceVersion.LegacyPreparedLaunch }) {
            if (version == ProcessAcceptedSourceVersion.LegacyPreparedLaunch) {
                await context.AcceptedAssignmentSources.Where(item => item.AdmissionId == launch.Id).ExecuteDeleteAsync();
                await context.PreparedLaunches.Where(item => item.Id == launch.Id).ExecuteUpdateAsync(update => update
                    .SetProperty(item => item.AcceptedSourceVersion, version)
                    .SetProperty(item => item.AcceptedSourceHeaderJson, (string?)null)
                    .SetProperty(item => item.AcceptedSourceHeaderFingerprint, (string?)null));
            }
            for (var warmup = 0; warmup < 3; warmup++) {
                AssertOriginal(await ReadAsync());
            }
            probe.Reset();
            probe.CaptureDetails = true;
            AssertOriginal(await ReadAsync());
            Assert.Equal(1, probe.Reads);
            var query = Assert.IsType<SourceCommand>(probe.Last);
            Assert.Contains("CASE", query.Sql, StringComparison.Ordinal);
            Assert.Contains("ELSE ''", query.Sql, StringComparison.Ordinal);
            Assert.Contains("LEFT JOIN", query.Sql, StringComparison.Ordinal);
            Assert.Contains("process_accepted_assignment_sources", query.Sql, StringComparison.Ordinal);
            Assert.Contains("LIMIT 2", query.Sql, StringComparison.Ordinal);
            await context.Database.OpenConnectionAsync();
            await using (var command = context.Database.GetDbConnection().CreateCommand()) {
                command.CommandText = "EXPLAIN (ANALYZE, BUFFERS, VERBOSE, FORMAT JSON) " + query.Sql;
                command.CommandTimeout = 5;
                command.Parameters.AddRange(query.Parameters);
                var plan = Assert.IsType<string>(await command.ExecuteScalarAsync());
                output.WriteLine(JsonSerializer.Serialize(new { Workload = "accepted-source-sql-plan", Steps = steps, Version = version.ToString(), query.Sql, Plan = plan }));
                using var parsed = JsonDocument.Parse(plan);
                Assert.Equal(1d, parsed.RootElement[0].GetProperty("Plan").GetProperty("Actual Rows").GetDouble());
                if (steps >= 256 && version == ProcessAcceptedSourceVersion.OriginalProjection) {
                    Assert.Contains("PK_process_accepted_assignment_sources", plan, StringComparison.Ordinal);
                }
            }
            for (var round = 0; round < 3; round++) {
                const int repetitions = 8;
                probe.Reset();
                probe.CaptureDetails = false;
                ProcessAcceptedSource? last = null;
                var start = Stopwatch.GetTimestamp();
                for (var iteration = 0; iteration < repetitions; iteration++) {
                    last = await ReadAsync();
                }
                var milliseconds = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                Assert.Equal(repetitions, probe.Reads);
                AssertOriginal(Assert.IsType<ProcessAcceptedSource>(last));
                output.WriteLine(JsonSerializer.Serialize(new {
                    Workload = "accepted-source-actual-read", Steps = steps, PromptCharacters = promptCharacters, Version = version.ToString(),
                    Round = round, Repetitions = repetitions, ElapsedMilliseconds = milliseconds,
                    SourcePayloadUtf8Bytes = version == ProcessAcceptedSourceVersion.OriginalProjection ? selectedBytes : Encoding.UTF8.GetByteCount(launch.PayloadJson),
                    ProjectedStorageUtf8Bytes = storedBytes
                }));
            }
        }
        Task<ProcessAcceptedSource> ReadAsync() => EfProcessAcceptedSource.ReadAsync(context, state, selected.StepInstanceId, CancellationToken.None);
        void AssertOriginal(ProcessAcceptedSource result) {
            Assert.Equal(saved.PreparationFingerprint, result.Header.PreparationFingerprint);
            Assert.Equal(JsonSerializer.Serialize(ProcessAssignmentSource.From(selected)), JsonSerializer.Serialize(result.Assignment));
        }
    }

    private sealed record SourceCommand(string Sql, NpgsqlParameter[] Parameters);

    private sealed class SourceReadProbe : DbCommandInterceptor {
        internal int Reads { get; private set; }
        internal SourceCommand? Last { get; private set; }
        internal bool CaptureDetails { get; set; }
        internal void Reset() {
            Reads = 0;
            Last = null;
        }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            Reads++;
            if (CaptureDetails) {
                Last = new(command.CommandText, command.Parameters.Cast<NpgsqlParameter>()
                    .Select(parameter => (NpgsqlParameter)((ICloneable)parameter).Clone()).ToArray());
            }
            return ValueTask.FromResult(result);
        }
    }

    private sealed class TransactionProbe : DbTransactionInterceptor {
        private long started;
        internal double TransactionMilliseconds { get; private set; }
        public override ValueTask<DbTransaction> TransactionStartedAsync(DbConnection connection, TransactionEndEventData eventData,
            DbTransaction result, CancellationToken cancellationToken = default) {
            started = Stopwatch.GetTimestamp();
            return ValueTask.FromResult(result);
        }
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default) {
            TransactionMilliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            return Task.CompletedTask;
        }
    }
}
