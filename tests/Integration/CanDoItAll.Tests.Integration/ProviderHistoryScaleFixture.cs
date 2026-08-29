using System.Security.Cryptography;
using System.Text;
using System.Diagnostics;
using CanDoItAll.AgentFramework.ProviderHistory;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace CanDoItAll.Tests.Integration;

internal static class ProviderHistoryScaleFixture {
    internal static async Task<ScaleSeedResult> SeedAsync(
        HistoryPersistenceTestDatabase fixture,
        int count,
        string prefix,
        DateTimeOffset expiresAtUtc,
        CancellationToken cancellationToken = default) {
        await using var db = fixture.Factory.CreateDbContext();
        await db.Database.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(Sql, (NpgsqlConnection)db.Database.GetDbConnection()) {
            CommandTimeout = 180
        };
        Add(command, "partition", NpgsqlDbType.Uuid, fixture.Partition.StorageLineageId);
        Add(command, "count", NpgsqlDbType.Integer, count);
        Add(command, "prefix", NpgsqlDbType.Text, prefix);
        Add(command, "now", NpgsqlDbType.TimestampTz, fixture.Clock.Now);
        Add(command, "expires", NpgsqlDbType.TimestampTz, expiresAtUtc);
        Add(command, "granularity", NpgsqlDbType.Integer, (int)HistoryGranularity.ProviderCallAttempt);
        Add(command, "timeBasis", NpgsqlDbType.Integer, (int)HistoryTimeBasis.AttemptStarted);
        Add(command, "operation", NpgsqlDbType.Integer, (int)HistoryOperation.CompleteChat);
        Add(command, "workload", NpgsqlDbType.Integer, (int)HistoryWorkload.Direct);
        Add(command, "outcome", NpgsqlDbType.Integer, (int)HistoryOutcome.Succeeded);
        Add(command, "authentication", NpgsqlDbType.Integer, (int)HistoryAuthenticationKind.ManagedCredential);
        Add(command, "usage", NpgsqlDbType.Integer, (int)HistoryUsageState.Complete);
        Add(command, "price", NpgsqlDbType.Integer, (int)HistoryPriceState.CalculatedAtExecution);
        Add(command, "metadata", NpgsqlDbType.Integer, (int)HistoryMetadataAuthority.Standalone);
        Add(command, "retention", NpgsqlDbType.Integer, (int)HistoryRetentionAuthority.HistoryPolicy);
        Add(command, "detail", NpgsqlDbType.Integer, (int)HistoryDetailState.NotCaptured);
        var started = Stopwatch.StartNew();
        var inserted = await command.ExecuteNonQueryAsync(cancellationToken);
        await using var analyze = new NpgsqlCommand("ANALYZE \"ProviderHistory_Entries\"", command.Connection) {
            CommandTimeout = 180
        };
        await analyze.ExecuteNonQueryAsync(cancellationToken);
        started.Stop();
        return new(inserted, DeterministicGuid(prefix + "-provider-0"),
            DeterministicGuid(prefix + "-credential-0"), started.Elapsed);
    }

    internal static Guid DeterministicGuid(string value) =>
        Guid.ParseExact(Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(value))), "N");

    private static void Add(NpgsqlCommand command, string name, NpgsqlDbType type, object value) =>
        command.Parameters.Add(new(name, type) { Value = value });

    private const string Sql = """
        INSERT INTO "ProviderHistory_Entries" (
            "Id", "PartitionId", "RequestId", "AttemptId", "Granularity", "SortAtUtc", "TimeBasis",
            "StartedAtUtc", "FinishedAtUtc", "ProviderId", "ProviderName", "ProviderKind", "RequestedModel",
            "ResolvedModel", "Operation", "Workload", "Outcome", "AuthenticationKind", "CredentialId",
            "Issuer", "Subject", "CallerName", "UsageState", "InputTokens", "OutputTokens", "PriceState",
            "Amount", "Currency", "MetadataAuthority", "RetentionAuthority", "DetailState", "ExpiresAtUtc",
            "IsVisible", "Version", "ConcurrencyToken")
        SELECT
            md5(@prefix || '-entry-' || i)::uuid,
            @partition,
            md5(@prefix || '-request-' || i)::uuid,
            md5(@prefix || '-attempt-' || i)::uuid,
            @granularity,
            @now - make_interval(secs => (i / 5)::integer),
            @timeBasis,
            @now - make_interval(secs => (i / 5)::integer),
            @now - make_interval(secs => (i / 5)::integer) + interval '50 milliseconds',
            md5(@prefix || '-provider-' || (i % 20))::uuid,
            'Scale provider ' || (i % 20),
            'OpenAI',
            'model-' || (i % 100),
            'model-' || (i % 100),
            @operation,
            @workload,
            @outcome,
            @authentication,
            md5(@prefix || '-credential-' || (i % 40))::uuid,
            'scale-issuer',
            'scale-subject-' || (i % 10),
            'Scale caller',
            @usage,
            10 + (i % 100),
            5 + (i % 50),
            @price,
            0.001,
            'USD',
            @metadata,
            @retention,
            @detail,
            @expires,
            TRUE,
            1,
            md5(@prefix || '-concurrency-' || i)::uuid
        FROM generate_series(1, @count) AS i;
        """;
}

internal sealed record ScaleSeedResult(int Inserted, Guid ProviderId, Guid CredentialId, TimeSpan Elapsed);

