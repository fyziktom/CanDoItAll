using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;
using CanDoItAll.Infrastructure.Diagnostics;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Automation;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class PostgreSqlRuntimeThroughputBenchmarkTests {
    private const string RunVariable = "CANDOITALL_RUN_Scenario06_BENCHMARK";
    private const string OutputVariable = "CANDOITALL_Scenario06_BENCHMARK_OUTPUT";
    private const int SeededRecordCount = 768;
    private const int ClaimBatchSize = 64;
    private const int SequentialParallelism = 1;
    private const int BoundedParallelism = 8;
    private static readonly TimeSpan SimulatedSideEffectDelay = TimeSpan.FromMilliseconds(2);
    private static readonly TimeSpan ClaimLeaseDuration = TimeSpan.FromMinutes(2);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) {
        WriteIndented = true
    };

    [Fact]
    public async Task Run_postgresql_throughput_benchmark_when_enabled() {
        if (!string.Equals(Environment.GetEnvironmentVariable(RunVariable), "1", StringComparison.Ordinal)) {
            return;
        }

        await using var testEnvironment = CanDoItAllTestEnvironment.Create("integration-postgres-runtime-throughput");
        var activeProfile = testEnvironment.CreatePostgreSqlProfile("runtime-throughput");
        await using var provider = await TestApplicationBootstrap.BuildServiceProviderAsync(
            activeProfile,
            "CanDoItAll.Tests.Integration",
            TestSchemaBootstrapModules.Full);

        await using var scope = provider.CreateAsyncScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        Assert.True(dbContext.Database.IsNpgsql(), "Scenario06 benchmark must run against PostgreSQL.");

        using var metricProbe = new RuntimeClaimMetricProbe();
        var results = new List<RuntimeThroughputBenchmarkResult>();
        foreach (var workload in BenchmarkWorkloads) {
            results.Add(await RunWorkloadModeAsync(
                dbContextFactory,
                workload,
                "sequential",
                SequentialParallelism,
                CancellationToken.None));
            results.Add(await RunWorkloadModeAsync(
                dbContextFactory,
                workload,
                "bounded-parallel",
                BoundedParallelism,
                CancellationToken.None));
        }

        var duplicateSuppressionCount = await ProbeAutomationDuplicateSuppressionAsync(
            provider,
            CancellationToken.None);

        foreach (var result in results) {
            RuntimeClaimMetrics.RecordBatch(
                result.Workload,
                result.ClaimedRecords,
                result.ProcessedRecords,
                result.ClaimBatchSize,
                result.EffectiveParallelism,
                TimeSpan.FromSeconds(result.ElapsedSeconds));

            for (var index = 0; index < result.StaleFinalizationCount; index++) {
                RuntimeClaimMetrics.RecordStaleFinalization(result.Workload);
            }
        }

        var output = new RuntimeThroughputBenchmarkOutput(
            DateTimeOffset.UtcNow,
            "PostgreSQL",
            SeededRecordCount,
            ClaimBatchSize,
            SimulatedSideEffectDelay.TotalMilliseconds,
            SequentialParallelism,
            BoundedParallelism,
            new RuntimeProtectionCounterSummary(
                duplicateSuppressionCount,
                results.Sum(item => item.StaleFinalizationCount)),
            metricProbe.Instruments,
            results);

        await WriteBenchmarkOutputAsync(output, CancellationToken.None);

        Assert.All(results, result => Assert.True(
            result.CompletedWithoutMissingRecords,
            $"{result.Workload}/{result.Mode} processed {result.ProcessedRecords} of {result.SeededRecords}."));
        Assert.Contains(RuntimeClaimMetrics.ClaimedRecordsInstrumentName, metricProbe.Instruments);
        Assert.Contains(RuntimeClaimMetrics.ProcessedRecordsInstrumentName, metricProbe.Instruments);
        Assert.Contains(RuntimeClaimMetrics.BatchDurationInstrumentName, metricProbe.Instruments);
        Assert.Contains(RuntimeClaimMetrics.StaleFinalizationsInstrumentName, metricProbe.Instruments);
        Assert.Contains(RuntimeClaimMetrics.DuplicateSuppressionsInstrumentName, metricProbe.Instruments);
        Assert.True(metricProbe.DuplicateSuppressions >= duplicateSuppressionCount);
        Assert.True(metricProbe.StaleFinalizations >= results.Sum(item => item.StaleFinalizationCount));
    }

    private static async Task<RuntimeThroughputBenchmarkResult> RunWorkloadModeAsync(
        IDbContextFactory<AppDbContext> dbContextFactory,
        RuntimeBenchmarkWorkload workload,
        string mode,
        int workerCount,
        CancellationToken cancellationToken) {
        await ResetAndSeedAsync(dbContextFactory, workload, cancellationToken);

        var counters = new RuntimeBenchmarkCounters();
        var claimBatchSizes = new ConcurrentBag<int>();
        var processingDurations = new ConcurrentBag<double>();
        var processedIds = new ConcurrentQueue<Guid>();
        var elapsed = Stopwatch.StartNew();
        var workers = Enumerable.Range(0, workerCount)
            .Select(workerIndex => RunWorkerAsync(
                dbContextFactory,
                workload,
                mode,
                workerIndex,
                counters,
                claimBatchSizes,
                processingDurations,
                processedIds,
                cancellationToken))
            .ToArray();

        await Task.WhenAll(workers);
        elapsed.Stop();

        if (!processedIds.TryPeek(out var staleProbeId)) {
            throw new InvalidOperationException($"The Scenario06 {workload.Name}/{mode} benchmark did not process any records.");
        }

        var staleFinalizationCount = await AttemptStaleFinalizationAsync(
            dbContextFactory,
            workload,
            staleProbeId,
            cancellationToken);
        var durations = processingDurations.ToArray();
        var batches = claimBatchSizes.ToArray();
        var processedRecords = counters.ProcessedRecords;
        var elapsedSeconds = Math.Max(elapsed.Elapsed.TotalSeconds, 0.001d);

        return new RuntimeThroughputBenchmarkResult(
            workload.Name,
            mode,
            SeededRecordCount,
            counters.ClaimedRecords,
            processedRecords,
            ClaimBatchSize,
            Round(batches.Length == 0 ? 0 : batches.Average()),
            counters.EffectiveParallelism,
            Round(processedRecords / elapsedSeconds),
            Round(durations.Length == 0 ? 0 : durations.Average()),
            Round(Percentile(durations, 0.95d)),
            Round(elapsedSeconds),
            staleFinalizationCount,
            0,
            processedRecords == SeededRecordCount);
    }

    private static async Task RunWorkerAsync(
        IDbContextFactory<AppDbContext> dbContextFactory,
        RuntimeBenchmarkWorkload workload,
        string mode,
        int workerIndex,
        RuntimeBenchmarkCounters counters,
        ConcurrentBag<int> claimBatchSizes,
        ConcurrentBag<double> processingDurations,
        ConcurrentQueue<Guid> processedIds,
        CancellationToken cancellationToken) {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open) {
            await connection.OpenAsync(cancellationToken);
        }

        var iteration = 0;
        while (true) {
            cancellationToken.ThrowIfCancellationRequested();
            var now = DateTimeOffset.UtcNow;
            var leaseToken = $"scenario06-{mode}-{workerIndex}-{iteration}";
            var claimedRecords = await ClaimBatchAsync(
                connection,
                workload,
                leaseToken,
                now,
                cancellationToken);
            if (claimedRecords.Count == 0) {
                return;
            }

            iteration++;
            counters.AddClaimedRecords(claimedRecords.Count);
            claimBatchSizes.Add(claimedRecords.Count);
            foreach (var claimedRecord in claimedRecords) {
                await ProcessClaimedRecordAsync(
                    connection,
                    workload,
                    claimedRecord,
                    leaseToken,
                    counters,
                    processingDurations,
                    processedIds,
                    cancellationToken);
            }
        }
    }

    private static async Task ProcessClaimedRecordAsync(
        DbConnection connection,
        RuntimeBenchmarkWorkload workload,
        Guid claimedRecord,
        string leaseToken,
        RuntimeBenchmarkCounters counters,
        ConcurrentBag<double> processingDurations,
        ConcurrentQueue<Guid> processedIds,
        CancellationToken cancellationToken) {
        var stopwatch = Stopwatch.StartNew();
        counters.StartProcessing();
        try {
            await Task.Delay(SimulatedSideEffectDelay, cancellationToken);
            var affectedRows = await ExecuteNonQueryAsync(
                connection,
                workload.FinalizeSql,
                [
                    new DbParameterValue("@id", claimedRecord),
                    new DbParameterValue("@leaseToken", leaseToken),
                    new DbParameterValue("@now", DateTimeOffset.UtcNow)
                ],
                cancellationToken);
            if (affectedRows == 1) {
                counters.AddProcessedRecord();
                processedIds.Enqueue(claimedRecord);
            }
        }
        finally {
            counters.FinishProcessing();
            stopwatch.Stop();
            processingDurations.Add(stopwatch.Elapsed.TotalMilliseconds);
        }
    }

    private static async Task<IReadOnlyList<Guid>> ClaimBatchAsync(
        DbConnection connection,
        RuntimeBenchmarkWorkload workload,
        string leaseToken,
        DateTimeOffset now,
        CancellationToken cancellationToken) {
        await using var command = connection.CreateCommand();
        command.CommandText = workload.ClaimSql;
        AddParameter(command, "@now", now);
        AddParameter(command, "@leaseToken", leaseToken);
        AddParameter(command, "@leaseExpiresAtUtc", now.Add(ClaimLeaseDuration));
        AddParameter(command, "@batchSize", ClaimBatchSize);

        var claimedRecords = new List<Guid>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) {
            claimedRecords.Add(reader.GetGuid(0));
        }

        return claimedRecords;
    }

    private static async Task<int> AttemptStaleFinalizationAsync(
        IDbContextFactory<AppDbContext> dbContextFactory,
        RuntimeBenchmarkWorkload workload,
        Guid processedRecordId,
        CancellationToken cancellationToken) {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open) {
            await connection.OpenAsync(cancellationToken);
        }

        var affectedRows = await ExecuteNonQueryAsync(
            connection,
            workload.FinalizeSql,
            [
                new DbParameterValue("@id", processedRecordId),
                new DbParameterValue("@leaseToken", "scenario06-stale-token"),
                new DbParameterValue("@now", DateTimeOffset.UtcNow)
            ],
            cancellationToken);
        if (affectedRows != 0) {
            throw new InvalidOperationException($"The Scenario06 stale finalization probe unexpectedly updated {workload.Name}.");
        }

        return 1;
    }

    private static async Task ResetAndSeedAsync(
        IDbContextFactory<AppDbContext> dbContextFactory,
        RuntimeBenchmarkWorkload workload,
        CancellationToken cancellationToken) {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open) {
            await connection.OpenAsync(cancellationToken);
        }

        await ExecuteNonQueryAsync(connection, workload.ResetSql, [], cancellationToken);
        await ExecuteNonQueryAsync(
            connection,
            workload.SeedSql,
            [new DbParameterValue("@recordCount", SeededRecordCount)],
            cancellationToken);
        await ExecuteNonQueryAsync(connection, workload.AnalyzeSql, [], cancellationToken);
    }

    private static async Task<int> ProbeAutomationDuplicateSuppressionAsync(
        ServiceProvider provider,
        CancellationToken cancellationToken) {
        await using var scope = provider.CreateAsyncScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IAutomationMessagePublisher>();
        var options = new AutomationPublishOptions(DedupeKey: $"scenario06-benchmark-{Guid.NewGuid():N}");
        var first = await publisher.PublishAsync(
            new Scenario06BenchmarkEnvelope("duplicate-probe"),
            options,
            cancellationToken);
        var second = await publisher.PublishAsync(
            new Scenario06BenchmarkEnvelope("duplicate-probe"),
            options,
            cancellationToken);

        Assert.Equal(first, second);
        return 1;
    }

    private static async Task WriteBenchmarkOutputAsync(
        RuntimeThroughputBenchmarkOutput output,
        CancellationToken cancellationToken) {
        var configuredPath = Environment.GetEnvironmentVariable(OutputVariable);
        var outputPath = string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(AppContext.BaseDirectory, "scenario06-benchmark-output.json")
            : configuredPath;
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directory)) {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(
            outputPath,
            JsonSerializer.Serialize(output, JsonOptions),
            cancellationToken);
    }

    private static async Task<int> ExecuteNonQueryAsync(
        DbConnection connection,
        string sql,
        IReadOnlyCollection<DbParameterValue> parameters,
        CancellationToken cancellationToken) {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var parameter in parameters) {
            AddParameter(command, parameter.Name, parameter.Value);
        }

        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddParameter(DbCommand command, string name, object? value) {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static double Percentile(double[] values, double percentile) {
        if (values.Length == 0) {
            return 0;
        }

        Array.Sort(values);
        var index = Math.Clamp(
            (int)Math.Ceiling(values.Length * percentile) - 1,
            0,
            values.Length - 1);
        return values[index];
    }

    private static double Round(double value) {
        return Math.Round(value, 3, MidpointRounding.AwayFromZero);
    }

    private static readonly IReadOnlyList<RuntimeBenchmarkWorkload> BenchmarkWorkloads =
    [
        new RuntimeBenchmarkWorkload(
            "process-outbox",
            ResetProcessOutboxSql,
            SeedProcessOutboxSql,
            AnalyzeProcessOutboxSql,
            ClaimProcessOutboxSql,
            FinalizeProcessOutboxSql),
        new RuntimeBenchmarkWorkload(
            "automation-delivery",
            ResetAutomationDeliverySql,
            SeedAutomationDeliverySql,
            AnalyzeAutomationDeliverySql,
            ClaimAutomationDeliverySql,
            FinalizeAutomationDeliverySql),
        new RuntimeBenchmarkWorkload(
            "connector-command",
            ResetConnectorCommandSql,
            SeedConnectorCommandSql,
            AnalyzeConnectorCommandSql,
            ClaimConnectorCommandSql,
            FinalizeConnectorCommandSql)
    ];

    private const string ResetProcessOutboxSql =
        """
        DELETE FROM "Processes_Outbox"
        WHERE "CommandKey" = 'scenario06-benchmark-process';
        """;

    private const string SeedProcessOutboxSql =
        """
        INSERT INTO "Processes_Outbox" (
            "Id",
            "ProjectId",
            "ProcessDefinitionId",
            "ProcessRunId",
            "CommandKey",
            "PayloadJson",
            "Status",
            "AttemptCount",
            "LastError",
            "LeaseToken",
            "CreatedAtUtc",
            "UpdatedAtUtc",
            "NextAttemptAtUtc",
            "LeaseExpiresAtUtc")
        SELECT
            ('00000000-0000-0000-6100-' || lpad(series.value::text, 12, '0'))::uuid,
            NULL,
            NULL,
            NULL,
            'scenario06-benchmark-process',
            '{}',
            0,
            0,
            '',
            '',
            now() - make_interval(secs => series.value),
            now() - make_interval(secs => series.value),
            NULL,
            NULL
        FROM generate_series(1, @recordCount) AS series(value);
        """;

    private const string AnalyzeProcessOutboxSql =
        """
        ANALYZE "Processes_Outbox";
        """;

    private const string ClaimProcessOutboxSql =
        """
        WITH due AS (
            SELECT o."Id"
            FROM "Processes_Outbox" AS o
            WHERE o."Status" = 0
              AND (o."NextAttemptAtUtc" IS NULL OR o."NextAttemptAtUtc" <= @now)
              AND (o."LeaseExpiresAtUtc" IS NULL OR o."LeaseExpiresAtUtc" <= @now)
            ORDER BY COALESCE(o."NextAttemptAtUtc", o."CreatedAtUtc"), o."CreatedAtUtc"
            FOR UPDATE SKIP LOCKED
            LIMIT @batchSize
        )
        UPDATE "Processes_Outbox" AS o
        SET "LeaseToken" = @leaseToken,
            "LeaseExpiresAtUtc" = @leaseExpiresAtUtc,
            "UpdatedAtUtc" = @now
        FROM due
        WHERE o."Id" = due."Id"
        RETURNING o."Id";
        """;

    private const string FinalizeProcessOutboxSql =
        """
        UPDATE "Processes_Outbox"
        SET "Status" = 1,
            "CompletedAtUtc" = @now,
            "UpdatedAtUtc" = @now,
            "LeaseToken" = '',
            "LeaseExpiresAtUtc" = NULL
        WHERE "Id" = @id
          AND "Status" = 0
          AND "LeaseToken" = @leaseToken
          AND "LeaseExpiresAtUtc" IS NOT NULL
          AND "LeaseExpiresAtUtc" > @now;
        """;

    private const string ResetAutomationDeliverySql =
        """
        DELETE FROM "Automation_EnvelopeDeliveries"
        WHERE "EnvelopeType" = 'scenario06.benchmark.delivery';

        DELETE FROM "Automation_Envelopes"
        WHERE "EnvelopeType" = 'scenario06.benchmark.delivery';
        """;

    private const string SeedAutomationDeliverySql =
        """
        INSERT INTO "Automation_Envelopes" (
            "Id",
            "EnvelopeType",
            "PayloadJson",
            "State",
            "AttemptCount",
            "AvailableAtUtc",
            "CreatedAtUtc",
            "UpdatedAtUtc")
        SELECT
            ('00000000-0000-0000-6200-' || lpad(series.value::text, 12, '0'))::uuid,
            'scenario06.benchmark.delivery',
            '{}',
            0,
            0,
            now(),
            now() - make_interval(secs => series.value),
            now()
        FROM generate_series(1, @recordCount) AS series(value);

        INSERT INTO "Automation_EnvelopeDeliveries" (
            "Id",
            "EnvelopeId",
            "EnvelopeType",
            "HandlerKey",
            "State",
            "AttemptCount",
            "MaxAttempts",
            "AvailableAtUtc",
            "CreatedAtUtc",
            "UpdatedAtUtc",
            "LastError",
            "LockToken",
            "LockedAtUtc")
        SELECT
            ('00000000-0000-0000-6300-' || lpad(series.value::text, 12, '0'))::uuid,
            ('00000000-0000-0000-6200-' || lpad(series.value::text, 12, '0'))::uuid,
            'scenario06.benchmark.delivery',
            'scenario06-handler',
            0,
            0,
            3,
            now() - interval '1 minute',
            now() - make_interval(secs => series.value),
            now(),
            '',
            '',
            NULL
        FROM generate_series(1, @recordCount) AS series(value);
        """;

    private const string AnalyzeAutomationDeliverySql =
        """
        ANALYZE "Automation_Envelopes";
        ANALYZE "Automation_EnvelopeDeliveries";
        """;

    private const string ClaimAutomationDeliverySql =
        """
        WITH due AS (
            SELECT d."Id"
            FROM "Automation_EnvelopeDeliveries" AS d
            WHERE d."AvailableAtUtc" <= @now
              AND (
                  d."State" = 0
                  OR d."State" = 2
                  OR (
                      d."State" = 1
                      AND d."LockedAtUtc" IS NOT NULL
                      AND d."LockedAtUtc" <= @now - interval '2 minutes'
                  )
              )
            ORDER BY d."AvailableAtUtc", d."CreatedAtUtc"
            FOR UPDATE SKIP LOCKED
            LIMIT @batchSize
        )
        UPDATE "Automation_EnvelopeDeliveries" AS d
        SET "State" = 1,
            "AttemptCount" = d."AttemptCount" + 1,
            "LastAttemptAtUtc" = @now,
            "UpdatedAtUtc" = @now,
            "CompletedAtUtc" = NULL,
            "LockedAtUtc" = @now,
            "LockToken" = @leaseToken
        FROM due
        WHERE d."Id" = due."Id"
        RETURNING d."Id";
        """;

    private const string FinalizeAutomationDeliverySql =
        """
        UPDATE "Automation_EnvelopeDeliveries"
        SET "State" = 3,
            "CompletedAtUtc" = @now,
            "UpdatedAtUtc" = @now,
            "LastError" = '',
            "LockToken" = '',
            "LockedAtUtc" = NULL
        WHERE "Id" = @id
          AND "State" = 1
          AND "LockToken" = @leaseToken;
        """;

    private const string ResetConnectorCommandSql =
        """
        DELETE FROM "Workspace_ConnectorCommands"
        WHERE "ConnectorPluginKey" = 'scenario06-benchmark-plugin';
        """;

    private const string SeedConnectorCommandSql =
        """
        INSERT INTO "Workspace_ConnectorCommands" (
            "Id",
            "ProjectId",
            "ConnectorPluginKey",
            "CommandKey",
            "IdempotencyKey",
            "PayloadJson",
            "Status",
            "ApprovalState",
            "AttemptCount",
            "LastError",
            "ResultJson",
            "LeaseToken",
            "RequestedBy",
            "CreatedAtUtc",
            "UpdatedAtUtc",
            "NextAttemptAtUtc",
            "LeaseExpiresAtUtc")
        SELECT
            ('00000000-0000-0000-6400-' || lpad(series.value::text, 12, '0'))::uuid,
            '20000000-0000-0000-0000-000000000001',
            'scenario06-benchmark-plugin',
            'deliver',
            'scenario06-idem-' || series.value,
            '{}',
            0,
            0,
            0,
            '',
            '',
            '',
            'integration-tests',
            now() - make_interval(secs => series.value),
            now() - make_interval(secs => series.value),
            NULL,
            NULL
        FROM generate_series(1, @recordCount) AS series(value);
        """;

    private const string AnalyzeConnectorCommandSql =
        """
        ANALYZE "Workspace_ConnectorCommands";
        """;

    private const string ClaimConnectorCommandSql =
        """
        WITH due AS (
            SELECT c."Id"
            FROM "Workspace_ConnectorCommands" AS c
            WHERE c."Status" = 0
              AND c."ApprovalState" <> 1
              AND (c."NextAttemptAtUtc" IS NULL OR c."NextAttemptAtUtc" <= @now)
              AND (c."LeaseExpiresAtUtc" IS NULL OR c."LeaseExpiresAtUtc" <= @now)
            ORDER BY COALESCE(c."NextAttemptAtUtc", c."CreatedAtUtc"), c."CreatedAtUtc"
            FOR UPDATE SKIP LOCKED
            LIMIT @batchSize
        )
        UPDATE "Workspace_ConnectorCommands" AS c
        SET "LeaseToken" = @leaseToken,
            "LeaseExpiresAtUtc" = @leaseExpiresAtUtc,
            "UpdatedAtUtc" = @now
        FROM due
        WHERE c."Id" = due."Id"
        RETURNING c."Id";
        """;

    private const string FinalizeConnectorCommandSql =
        """
        UPDATE "Workspace_ConnectorCommands"
        SET "Status" = 1,
            "CompletedAtUtc" = @now,
            "UpdatedAtUtc" = @now,
            "LastError" = '',
            "ResultJson" = '{}',
            "LeaseToken" = '',
            "LeaseExpiresAtUtc" = NULL
        WHERE "Id" = @id
          AND "Status" = 0
          AND "LeaseToken" = @leaseToken
          AND "LeaseExpiresAtUtc" IS NOT NULL
          AND "LeaseExpiresAtUtc" > @now;
        """;

    private sealed record RuntimeBenchmarkWorkload(
        string Name,
        string ResetSql,
        string SeedSql,
        string AnalyzeSql,
        string ClaimSql,
        string FinalizeSql);

    private sealed record DbParameterValue(string Name, object? Value);

    private sealed record Scenario06BenchmarkEnvelope(string Value);

    private sealed record RuntimeProtectionCounterSummary(
        int DuplicateSuppressionCount,
        int StaleFinalizationCount);

    private sealed record RuntimeThroughputBenchmarkOutput(
        DateTimeOffset CapturedAtUtc,
        string Provider,
        int SeededRecordCount,
        int ClaimBatchSize,
        double SimulatedSideEffectDelayMs,
        int SequentialParallelism,
        int BoundedParallelism,
        RuntimeProtectionCounterSummary ProtectionCounters,
        IReadOnlyList<string> MetricInstrumentsObserved,
        IReadOnlyList<RuntimeThroughputBenchmarkResult> Results);

    private sealed record RuntimeThroughputBenchmarkResult(
        string Workload,
        string Mode,
        int SeededRecords,
        int ClaimedRecords,
        int ProcessedRecords,
        int ClaimBatchSize,
        double AverageClaimBatchSize,
        int EffectiveParallelism,
        double RecordsPerSecond,
        double AverageProcessingTimeMs,
        double P95ProcessingTimeMs,
        double ElapsedSeconds,
        int StaleFinalizationCount,
        int DuplicateSuppressionCount,
        bool CompletedWithoutMissingRecords);

    private sealed class RuntimeBenchmarkCounters {
        private int claimedRecords;
        private int processedRecords;
        private int activeProcessors;
        private int effectiveParallelism;

        public int ClaimedRecords => Volatile.Read(ref claimedRecords);

        public int ProcessedRecords => Volatile.Read(ref processedRecords);

        public int EffectiveParallelism => Volatile.Read(ref effectiveParallelism);

        public void AddClaimedRecords(int count) {
            Interlocked.Add(ref claimedRecords, count);
        }

        public void AddProcessedRecord() {
            Interlocked.Increment(ref processedRecords);
        }

        public void StartProcessing() {
            var activeCount = Interlocked.Increment(ref activeProcessors);
            while (true) {
                var observedMax = Volatile.Read(ref effectiveParallelism);
                if (activeCount <= observedMax) {
                    return;
                }

                if (Interlocked.CompareExchange(ref effectiveParallelism, activeCount, observedMax) == observedMax) {
                    return;
                }
            }
        }

        public void FinishProcessing() {
            Interlocked.Decrement(ref activeProcessors);
        }
    }

    private sealed class RuntimeClaimMetricProbe : IDisposable {
        private readonly MeterListener listener = new();
        private readonly ConcurrentDictionary<string, byte> instruments = new(StringComparer.Ordinal);
        private long duplicateSuppressions;
        private long staleFinalizations;

        public RuntimeClaimMetricProbe() {
            listener.InstrumentPublished = (instrument, meterListener) => {
                if (!string.Equals(instrument.Meter.Name, RuntimeClaimMetrics.MeterName, StringComparison.Ordinal)) {
                    return;
                }

                instruments.TryAdd(instrument.Name, 0);
                meterListener.EnableMeasurementEvents(instrument);
            };
            listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) => {
                if (string.Equals(instrument.Name, RuntimeClaimMetrics.DuplicateSuppressionsInstrumentName, StringComparison.Ordinal)) {
                    Interlocked.Add(ref duplicateSuppressions, measurement);
                }

                if (string.Equals(instrument.Name, RuntimeClaimMetrics.StaleFinalizationsInstrumentName, StringComparison.Ordinal)) {
                    Interlocked.Add(ref staleFinalizations, measurement);
                }
            });
            listener.Start();
        }

        public IReadOnlyList<string> Instruments => instruments.Keys.Order(StringComparer.Ordinal).ToArray();

        public long DuplicateSuppressions => Interlocked.Read(ref duplicateSuppressions);

        public long StaleFinalizations => Interlocked.Read(ref staleFinalizations);

        public void Dispose() {
            listener.Dispose();
        }
    }
}
