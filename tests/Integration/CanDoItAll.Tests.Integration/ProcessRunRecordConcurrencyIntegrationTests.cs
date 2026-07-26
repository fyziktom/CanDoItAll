using System.Buffers.Binary;
using System.Collections.Concurrent;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace CanDoItAll.Tests.Integration;

public sealed class ProcessRunRecordConcurrencyIntegrationTests
{
    [Fact]
    public async Task Validated_seed_observes_record_committed_by_previous_advisory_lock_waiter()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("process-run-record-concurrency");
        var profile = testEnvironment.CreatePostgreSqlProfile("process-run-record-concurrency");
        var errorLogs = new ConcurrentQueue<string>();
        await using var setupContext = CreateDbContext(profile.ConnectionString, errorLogs);
        await setupContext.Database.EnsureCreatedAsync();

        var now = DateTimeOffset.UtcNow;
        var runId = ProcessRunId.New();
        setupContext.RuntimeStates.Add(new ProcessRuntimeStateEntity
        {
            RunId = runId.Value,
            RootRunId = runId.Value,
            PlanId = Guid.NewGuid(),
            PlanHash = "hash:plan",
            Status = ProcessRuntimeStatus.Completed,
            UpdatedAtUtc = now,
            ConcurrencyToken = Guid.NewGuid()
        });
        setupContext.RuntimeEvents.Add(new ProcessRuntimeEventEntity
        {
            GlobalSequence = 1,
            RootSequence = 1,
            EventId = Guid.NewGuid(),
            RootRunId = runId.Value,
            RunId = runId.Value,
            CorrelationId = $"test:{runId}",
            ActorKind = "System",
            ActorId = "system",
            SchemaVersion = "1.0",
            Sensitivity = "Normal",
            OccurredAtUtc = now,
            EventType = ProcessRuntimeEventTypes.ProcessRunCompleted.Value,
            PayloadHash = "hash:event"
        });
        await setupContext.SaveChangesAsync();

        await using var lockConnection = new NpgsqlConnection(profile.ConnectionString);
        await lockConnection.OpenAsync();
        await using var lockTransaction = await lockConnection.BeginTransactionAsync();
        await using (var lockCommand = lockConnection.CreateCommand())
        {
            lockCommand.Transaction = lockTransaction;
            lockCommand.CommandText = "select pg_advisory_xact_lock(@key);";
            lockCommand.Parameters.AddWithValue("key", CreateAdvisoryLockKey(runId.Value));
            await lockCommand.ExecuteNonQueryAsync();
        }

        await using var monitoringConnection = new NpgsqlConnection(profile.ConnectionString);
        await monitoringConnection.OpenAsync();
        await using var firstContext = CreateDbContext(profile.ConnectionString, errorLogs);
        await using var secondContext = CreateDbContext(profile.ConnectionString, errorLogs);
        var seed = NewSeed(runId, now);
        var firstUpsert = new EfProcessRunRecordStore(firstContext).UpsertSeedAsync(seed);
        await WaitForAdvisoryWaitersAsync(monitoringConnection, expectedCount: 1);
        var secondUpsert = new EfProcessRunRecordStore(secondContext).UpsertSeedAsync(seed with
        {
            Validation = ProcessRunRecordSeedValidation.CurrentReportableSource
        });
        await WaitForAdvisoryWaitersAsync(monitoringConnection, expectedCount: 2);

        await lockTransaction.CommitAsync();
        var results = await Task.WhenAll(firstUpsert, secondUpsert);

        Assert.Equal([true, false], results);
        Assert.DoesNotContain(
            errorLogs,
            message =>
                message.Contains("23505", StringComparison.Ordinal) ||
                message.Contains("PK_process_run_records", StringComparison.Ordinal));
    }

    private static ProcessPersistenceDbContext CreateDbContext(
        string connectionString,
        ConcurrentQueue<string> errorLogs)
    {
        var options = new DbContextOptionsBuilder<ProcessPersistenceDbContext>()
            .UseNpgsql(connectionString)
            .LogTo(errorLogs.Enqueue, LogLevel.Error)
            .Options;
        return new ProcessPersistenceDbContext(options);
    }

    private static ProcessRunRecordSeed NewSeed(ProcessRunId runId, DateTimeOffset now)
    {
        return new ProcessRunRecordSeed(
            new ProcessRunRecordIdentity(
                runId,
                runId,
                ParentRunId: null,
                PlanId: null,
                DefinitionId: null,
                DefinitionVersionId: null,
                ProjectId: null),
            ProcessRunDisposition.Succeeded,
            now,
            SourceGlobalSequence: 1,
            SourceRootSequence: 1,
            now);
    }

    private static long CreateAdvisoryLockKey(Guid runId)
    {
        Span<byte> bytes = stackalloc byte[16];
        runId.TryWriteBytes(bytes);
        return BinaryPrimitives.ReadInt64LittleEndian(bytes[..8]) ^
               BinaryPrimitives.ReadInt64LittleEndian(bytes[8..]);
    }

    private static async Task WaitForAdvisoryWaitersAsync(
        NpgsqlConnection connection,
        int expectedCount)
    {
        var timeoutAt = DateTimeOffset.UtcNow.AddSeconds(10);
        while (DateTimeOffset.UtcNow < timeoutAt)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                select count(*)
                from pg_locks
                where locktype = 'advisory'
                  and not granted
                  and database = (
                      select oid
                      from pg_database
                      where datname = current_database());
                """;
            var count = Convert.ToInt32(await command.ExecuteScalarAsync());
            if (count >= expectedCount)
            {
                return;
            }

            await Task.Delay(25);
        }

        throw new TimeoutException(
            $"Expected {expectedCount} advisory-lock waiter(s) before the test timeout.");
    }
}
