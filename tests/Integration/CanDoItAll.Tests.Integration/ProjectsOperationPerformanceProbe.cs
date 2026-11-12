using System.Collections.Concurrent;
using System.Data.Common;
using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CanDoItAll.Tests.Integration.ProjectStructure;

internal sealed class ProjectsOperationPerformanceProbe : DbCommandInterceptor {
    private readonly AsyncLocal<int?> current = new();
    private readonly ConcurrentQueue<Command> commands = new();

    internal IDisposable Enter(int operation) {
        var previous = current.Value;
        current.Value = operation;
        return new Exit(() => current.Value = previous);
    }

    internal void Reset() => commands.Clear();
    internal Command[] Snapshot() => commands.ToArray();

    internal static object? Distribution(IEnumerable<double> values) {
        var sorted = values.Order().ToArray();
        if (sorted.Length == 0) {
            return null;
        }
        double At(double percentile) => sorted[Math.Clamp((int)Math.Ceiling(sorted.Length * percentile) - 1, 0, sorted.Length - 1)];
        return new { Count = sorted.Length, P50 = At(.50), P95 = At(.95), P99 = At(.99), Maximum = sorted[^1] };
    }

    internal static object Summarize(IEnumerable<Command> values) => values.GroupBy(item => (item.Owner, item.Kind))
        .OrderBy(group => group.Key.Owner).ThenBy(group => group.Key.Kind)
        .Select(group => new {
            group.Key.Owner, Kind = group.Key.Kind.ToString(), Executions = group.Count(), Failed = group.Count(item => item.Failed),
            SummedMilliseconds = group.Sum(item => item.Milliseconds), Duration = Distribution(group.Select(item => item.Milliseconds))
        }).ToArray();

    private void Record(DbCommand command, CommandEndEventData data, bool failed = false) {
        if (current.Value is not { } operation) {
            return;
        }
        var sql = command.CommandText;
        var kind = sql.Contains("pg_advisory", StringComparison.Ordinal) ? CommandKind.Advisory
            : sql.Contains("Infrastructure_SearchDocuments", StringComparison.Ordinal) ? CommandKind.Search
            : sql.Contains("Projects_DefinitionReceipts", StringComparison.Ordinal) ? CommandKind.Receipt
            : sql.Contains("Infrastructure_FiniteOperation", StringComparison.Ordinal) ? CommandKind.Epoch
            : sql.Contains("Projects_ProjectPhases", StringComparison.Ordinal) || sql.Contains("Projects_ProjectOptionSelections", StringComparison.Ordinal) ? CommandKind.Children
            : sql.Contains("Projects_Projects", StringComparison.Ordinal) ? CommandKind.Root
            : CommandKind.Other;
        commands.Enqueue(new(operation, data.Context?.GetType().Name ?? "unknown", kind, data.Duration.TotalMilliseconds, failed));
    }

    public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
        DbDataReader result, CancellationToken cancellationToken = default) {
        Record(command, eventData);
        return ValueTask.FromResult(result);
    }

    public override ValueTask<object?> ScalarExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
        object? result, CancellationToken cancellationToken = default) {
        Record(command, eventData);
        return ValueTask.FromResult(result);
    }

    public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
        int result, CancellationToken cancellationToken = default) {
        Record(command, eventData);
        return ValueTask.FromResult(result);
    }

    public override Task CommandFailedAsync(DbCommand command, CommandErrorEventData eventData, CancellationToken cancellationToken = default) {
        Record(command, eventData, failed: true);
        return Task.CompletedTask;
    }

    public override Task CommandCanceledAsync(DbCommand command, CommandEndEventData eventData, CancellationToken cancellationToken = default) {
        Record(command, eventData, failed: true);
        return Task.CompletedTask;
    }

    internal enum CommandKind { Advisory, Search, Receipt, Epoch, Root, Children, Other }
    internal sealed record Command(int Operation, string Owner, CommandKind Kind, double Milliseconds, bool Failed);
    private sealed class Exit(Action exit) : IDisposable {
        public void Dispose() => exit();
    }
}

internal sealed class ProjectsOperationStorageMeasurement {
    public long Receipts { get; set; }
    public long PayloadBytes { get; set; }
    public long HeapBytes { get; set; }
    public long IndexBytes { get; set; }
    public long TotalBytes { get; set; }
    public long Projects { get; set; }
    public long SearchDocuments { get; set; }
    public double MeanPayloadBytes { get; set; }
    public double P95PayloadBytes { get; set; }
    public long MaximumPayloadBytes { get; set; }

    internal static Task<ProjectsOperationStorageMeasurement> ReadAsync(AppDbContext context, CancellationToken token)
        => context.Database.SqlQuery<ProjectsOperationStorageMeasurement>($"""
            SELECT count(*) AS "Receipts", COALESCE(sum(octet_length("AcceptedPayload")),0)::bigint AS "PayloadBytes",
                pg_relation_size('"Projects_DefinitionReceipts"') AS "HeapBytes",
                pg_indexes_size('"Projects_DefinitionReceipts"') AS "IndexBytes",
                pg_total_relation_size('"Projects_DefinitionReceipts"') AS "TotalBytes",
                (SELECT count(*) FROM "Projects_Projects") AS "Projects",
                (SELECT count(*) FROM "Infrastructure_SearchDocuments") AS "SearchDocuments",
                COALESCE(avg(octet_length("AcceptedPayload")),0)::float8 AS "MeanPayloadBytes",
                COALESCE(percentile_disc(0.95) WITHIN GROUP (ORDER BY octet_length("AcceptedPayload")),0)::float8 AS "P95PayloadBytes",
                COALESCE(max(octet_length("AcceptedPayload")),0)::bigint AS "MaximumPayloadBytes"
            FROM "Projects_DefinitionReceipts"
            """).SingleAsync(token);

    internal static Task<string> ReadWalAsync(AppDbContext context, CancellationToken token)
        => context.Database.SqlQuery<string>($"SELECT pg_current_wal_insert_lsn()::text AS \"Value\"").SingleAsync(token);

    internal static Task<long> WalDeltaAsync(AppDbContext context, string start, CancellationToken token)
        => context.Database.SqlQuery<long>($"SELECT pg_wal_lsn_diff(pg_current_wal_insert_lsn(), {start}::pg_lsn)::bigint AS \"Value\"").SingleAsync(token);

    internal static async Task<object> ChildReadPlanAsync(AppDbContext context, Guid project, CancellationToken token) {
        await context.Database.OpenConnectionAsync(token);
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = "EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON) SELECT * FROM \"Projects_ProjectPhases\" WHERE \"ProjectId\" = @project ORDER BY \"OrderIndex\", \"Id\"";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "project";
        parameter.Value = project;
        command.Parameters.Add(parameter);
        using var document = JsonDocument.Parse((string)(await command.ExecuteScalarAsync(token))!);
        return SummarizeChildReadPlan(document.RootElement[0]);
    }

    internal static object SummarizeChildReadPlan(JsonElement root) {
        var nodes = new List<object>();
        void Visit(JsonElement node) {
            nodes.Add(new {
                Node = node.GetProperty("Node Type").GetString(),
                Index = node.TryGetProperty("Index Name", out var index) ? index.GetString() : null,
                EstimatedRows = node.GetProperty("Plan Rows").GetInt64(),
                ActualRows = node.GetProperty("Actual Rows").GetDouble(),
                ActualLoops = node.GetProperty("Actual Loops").GetInt64(),
                ActualTotalMilliseconds = node.GetProperty("Actual Total Time").GetDouble(),
                SharedHitBlocks = node.GetProperty("Shared Hit Blocks").GetInt64(),
                SharedReadBlocks = node.GetProperty("Shared Read Blocks").GetInt64()
            });
            if (node.TryGetProperty("Plans", out var children)) {
                foreach (var child in children.EnumerateArray()) {
                    Visit(child);
                }
            }
        }
        Visit(root.GetProperty("Plan"));
        return new { PlanningMilliseconds = root.GetProperty("Planning Time").GetDouble(),
            ExecutionMilliseconds = root.GetProperty("Execution Time").GetDouble(), Nodes = nodes };
    }
}
