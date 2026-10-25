using System.Data.Common;
using System.Text.Json;
using CanDoItAll.Composition;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace CanDoItAll.Tests.Integration.Processes;

internal static class ProcessRecoverySelectionIndexFixture {
    internal const string Previous = "20261022080512_AddProcessCancellationRecovery";
    internal const string RuntimeIndex = "IX_process_runtime_states_RootRunId_RunId";
    internal const string DecisionIndex = "IX_process_decision_heads_RootRunId_DecisionId";
    internal static readonly Guid HotRoot = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    internal static readonly Guid SparseRoot = new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    internal static PostgresTestDatabaseLease CreateLease() {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
        return PostgresTestDatabaseLease.Create("recovery-selection-indexes");
    }

    internal static string Migration(AppDbContext context) => Assert.Single(context.Database.GetMigrations(),
        item => item.EndsWith("_IndexProcessCancellationSelections", StringComparison.Ordinal));

    internal static Task UpgradeAsync(AppDbContext context) => context.GetService<IMigrator>().MigrateAsync(Migration(context));

    internal static string Table(bool decision) => decision ? "process_decision_heads" : "process_runtime_states";
    internal static string Key(bool decision) => decision ? "DecisionId" : "RunId";
    internal static string Index(bool decision) => decision ? DecisionIndex : RuntimeIndex;
    internal static string OldIndex(bool decision) => $"IX_{Table(decision)}_RootRunId";

    internal static Task<bool> ExistsAsync(AppDbContext context, string index) => context.Database.SqlQuery<bool>(
        $"SELECT to_regclass({"\"" + index + "\""}) IS NOT NULL AS \"Value\"").SingleAsync();

    internal static Task<bool> ValidAsync(AppDbContext context, string index) => context.Database.SqlQuery<bool>(
        $"SELECT indisvalid AND indisready AND indislive AS \"Value\" FROM pg_index WHERE indexrelid = to_regclass({"\"" + index + "\""})").SingleAsync();

    internal static async Task<string[]> RowsAsync(AppDbContext context) => [
        .. await context.Database.SqlQueryRaw<string>("""SELECT to_jsonb(r)::text AS "Value" FROM process_runtime_states r ORDER BY "RunId" """).ToArrayAsync(),
        .. await context.Database.SqlQueryRaw<string>("""SELECT to_jsonb(h)::text AS "Value" FROM process_decision_heads h ORDER BY "DecisionId" """).ToArrayAsync()
    ];

    internal static async Task SeedAsync(AppDbContext context, int start, int count) {
        var assignment = JsonSerializer.Serialize(new ProcessRuntimeStepAssignmentEntity {
            StepKey = "retained-step", RoleKey = "retained-role", ExecutorKind = "agent",
            CreatedAtUtc = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        });
        var budget = JsonSerializer.Serialize(new ProcessDecisionBudgetEntity {
            Revision = 1, MaximumWorkTicks = 100, MaximumBasisAgeTicks = TimeSpan.TicksPerMinute
        });
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO process_runtime_states ("RunId", "RootRunId", "PlanId", "PlanHash", "Status", "UpdatedAtUtc", "ConcurrencyToken", "BlockedRecoveryActionsJson")
            SELECT md5(ordinal::text)::uuid,
                CASE WHEN ordinal <= 20000 THEN {HotRoot} WHEN ordinal <= 21000 THEN {SparseRoot} ELSE md5(('root-' || (ordinal % 300))::text)::uuid END,
                md5(ordinal::text)::uuid, 'index-fixture', 'Completed', clock_timestamp(), gen_random_uuid(), '[]'
            FROM generate_series({start}, {start + count - 1}) AS ordinal;
            INSERT INTO process_runtime_step_assignments
            SELECT (jsonb_populate_record(NULL::process_runtime_step_assignments, CAST({assignment} AS jsonb) ||
                jsonb_build_object('RunId', md5(ordinal::text)::uuid, 'StepInstanceId', md5(ordinal::text)::uuid, 'PlanId', md5(ordinal::text)::uuid))).*
            FROM generate_series({start}, {start + count - 1}) AS ordinal;
            INSERT INTO process_decision_budgets
            SELECT (jsonb_populate_record(NULL::process_decision_budgets, CAST({budget} AS jsonb) ||
                jsonb_build_object('BudgetId', r."RunId", 'RootRunId', r."RootRunId", 'RunId', r."RunId", 'StepInstanceId', r."RunId"))).*
            FROM generate_series({start}, {start + count - 1}) ordinal JOIN process_runtime_states r ON r."RunId" = md5(ordinal::text)::uuid;
            INSERT INTO process_decision_heads ("DecisionId", "RootRunId", "RunId", "StepInstanceId", "BudgetId", "CurrentGeneration", "Revision")
            SELECT r."RunId", r."RootRunId", r."RunId", r."RunId", r."RunId", 1, 1
            FROM generate_series({start}, {start + count - 1}) ordinal JOIN process_runtime_states r ON r."RunId" = md5(ordinal::text)::uuid;
            """);
    }

    internal sealed class SchemaFault(int after) : DbCommandInterceptor {
        private int completed;
        internal int Completed => completed;
        public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            int result, CancellationToken cancellationToken = default) {
            if ((command.CommandText.StartsWith("CREATE INDEX CONCURRENTLY", StringComparison.Ordinal) ||
                    command.CommandText.StartsWith("DROP INDEX CONCURRENTLY", StringComparison.Ordinal)) && ++completed == after) {
                throw new IOException("Injected lost acknowledgement after a concurrent index command.");
            }
            return ValueTask.FromResult(result);
        }
    }

    internal sealed class QueryProbe : DbCommandInterceptor {
        internal List<(string Sql, NpgsqlParameter[] Parameters, int Timeout)> Commands { get; } = [];
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            Commands.Add((command.CommandText, command.Parameters.Cast<NpgsqlParameter>().Select(parameter => parameter.Clone()).ToArray(), command.CommandTimeout));
            return ValueTask.FromResult(result);
        }
    }
}
