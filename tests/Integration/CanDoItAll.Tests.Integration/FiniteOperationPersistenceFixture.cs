using System.Collections.Concurrent;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace CanDoItAll.Tests.Integration;

internal sealed class FiniteOperationPersistenceFixture(ProjectOperationEpochTests.Fixture owner) : IAsyncDisposable {
    internal static readonly FiniteOperationPoolId TestPool = new(new Guid("b9b24b15-e3bb-4bbc-9dac-aa61b1d18110"));
    internal static readonly FiniteReceiptFamilyId FirstFamily = new(new Guid("4dfd304c-3d67-4ee2-8f7d-0b00ad64b7be"));
    internal static readonly FiniteReceiptFamilyId SecondFamily = new(new Guid("95b52c22-d937-44a9-a47b-2e93559c4c39"));
    internal ProjectOperationEpochTests.Fixture Owner => owner;
    internal DatasetActivationBinding Binding => owner.Binding;
    internal FiniteOperationPoolRegistration Pool { get; } = new(TestPool, 1, owner.Pool.Timing,
        [new(FirstFamily, 2 * 1024 * 1024), new(SecondFamily, 2 * 1024 * 1024)]);
    internal ReceiptParticipant First { get; } = new(owner, FirstFamily, ReceiptTable.First);
    internal ReceiptParticipant Second { get; } = new(owner, SecondFamily, ReceiptTable.Second);
    internal FiniteOperationMaintenanceState Schedule { get; private set; } = null!;

    internal static async Task<FiniteOperationPersistenceFixture> CreateAsync() {
        var owner = await ProjectOperationEpochTests.Fixture.CreateAsync();
        var fixture = new FiniteOperationPersistenceFixture(owner);
        try {
            await owner.InitializeAsync(owner.Service, fixture.Pool);
            fixture.Schedule = new([fixture.Pool]);
            await using var context = owner.Context();
            await context.Database.ExecuteSqlRawAsync("""
                CREATE TABLE "FiniteTest_First" ("NamespaceId" uuid NOT NULL, "Epoch" bigint NOT NULL,
                    "Id" uuid PRIMARY KEY, "Payload" bytea NOT NULL);
                CREATE TABLE "FiniteTest_Second" ("NamespaceId" uuid NOT NULL, "Epoch" bigint NOT NULL,
                    "Id" uuid PRIMARY KEY, "Payload" bytea NOT NULL);
                """);
            return fixture;
        } catch {
            await fixture.DisposeAsync();
            throw;
        }
    }

    internal FiniteOperationReceiptMaintenance Maintenance(IInterceptor? interceptor = null,
        IReadOnlyList<IFiniteOperationReceiptRetentionParticipant>? participants = null)
        => new(owner.CreateNeutralOptions(interceptor), owner.Transactions, [Pool], participants ?? [First, Second]);

    internal FiniteOperationMaintenanceCycle Cycle() => new(Pool, Schedule);

    internal async Task RetireFirstAsync() {
        await using var context = owner.Context();
        await using var transaction = await context.Database.BeginTransactionAsync();
        var now = await FiniteOperationEpochStore.ReadTimeAsync(context, CancellationToken.None);
        var epoch = await context.Set<FiniteOperationEpochRecord>().SingleAsync(row => row.NamespaceId == Binding.Namespace.Value && row.PoolId == Pool.Pool.Value);
        epoch.State = FiniteOperationEpochState.Closed;
        epoch.OpenedAtUtc = now.AddDays(-4);
        epoch.AdmissionClosesAtUtc = now.AddDays(-3);
        epoch.ClosedAtUtc = now.AddDays(-2);
        epoch.ReplayRetentionSeconds = 60;
        epoch.MutationDeadlineMilliseconds = 1000;
        await context.SaveChangesAsync();
        var state = await context.Set<FiniteOperationStateRecord>().SingleAsync(row => row.NamespaceId == Binding.Namespace.Value && row.PoolId == Pool.Pool.Value);
        state.CurrentEpoch = 2;
        state.RetiredThroughEpoch = 1;
        context.Add(new FiniteOperationEpochRecord {
            NamespaceId = Binding.Namespace.Value, PoolId = Pool.Pool.Value, Epoch = 2, State = FiniteOperationEpochState.Open,
            OpenedAtUtc = now, AdmissionClosesAtUtc = now.AddDays(1), MutationDeadlineMilliseconds = 30000, ReplayRetentionSeconds = 604800
        });
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    internal async Task<bool> HasFirstEpochAsync() {
        await using var context = owner.Context();
        return await context.Set<FiniteOperationEpochRecord>().AnyAsync(row => row.NamespaceId == Binding.Namespace.Value &&
            row.PoolId == Pool.Pool.Value && row.Epoch == 1);
    }

    public ValueTask DisposeAsync() => owner.DisposeAsync();

    internal enum ReceiptTable { First, Second }

    internal sealed class ReceiptParticipant(ProjectOperationEpochTests.Fixture owner, FiniteReceiptFamilyId family,
        ReceiptTable table) : IFiniteOperationReceiptRetentionParticipant {
        internal string Table => table switch {
            ReceiptTable.First => "\"FiniteTest_First\"",
            ReceiptTable.Second => "\"FiniteTest_Second\"",
            _ => throw new ArgumentOutOfRangeException(nameof(table))
        };
        public FiniteOperationPoolId Pool => TestPool;
        public FiniteReceiptFamilyId Family => family;
        internal Func<CancellationToken, Task>? BeforePurge { get; set; }
        internal Func<CancellationToken, Task>? AfterInspection { get; set; }
        internal ConcurrentBag<string> TransactionIds { get; } = [];

        internal async Task SeedAsync(int count, int bytes = 3) {
            await using var context = owner.Context();
            var sql = $"INSERT INTO {Table} (\"NamespaceId\",\"Epoch\",\"Id\",\"Payload\") " +
                "SELECT @namespace,1,gen_random_uuid(),decode(repeat('ab',@bytes),'hex') FROM generate_series(1,@count)";
            await context.Database.ExecuteSqlRawAsync(sql, new NpgsqlParameter("namespace", owner.Binding.Namespace.Value),
                new NpgsqlParameter("bytes", bytes), new NpgsqlParameter("count", count));
        }

        internal async Task<int> CountAsync() {
            await using var context = owner.Context();
            var sql = $"SELECT count(*)::integer AS \"Value\" FROM {Table}";
            return await context.Database.SqlQueryRaw<int>(sql).SingleAsync();
        }

        internal async Task ClearAsync() {
            await using var context = owner.Context();
            var sql = $"DELETE FROM {Table}";
            await context.Database.ExecuteSqlRawAsync(sql);
        }

        public async Task<FiniteReceiptPurgeObservation> PurgeRetiredBatchEnlistedAsync(FiniteReceiptPurgeRequest request,
            CancellationToken cancellationToken) {
            await using var context = await owner.Transactions.CreateEnlistedAsync(owner.CreateNeutralOptions(),
                static options => new FiniteOperationDbContext(options), cancellationToken);
            TransactionIds.Add(await context.Database.SqlQueryRaw<string>(
                "SELECT pg_current_xact_id()::text AS \"Value\"").SingleAsync(cancellationToken));
            if (BeforePurge is { } before) {
                await before(cancellationToken);
            }
            var sql = $"""
                WITH candidates AS (
                    SELECT r."Id",octet_length(r."Payload") AS bytes FROM {Table} AS r
                    JOIN "Infrastructure_FiniteOperationEpochs" AS e ON e."NamespaceId"=r."NamespaceId" AND e."Epoch"=r."Epoch" AND e."PoolId"=@pool
                    JOIN "Infrastructure_FiniteOperationState" AS s ON s."NamespaceId"=e."NamespaceId" AND s."PoolId"=e."PoolId"
                    WHERE e."NamespaceId"=@namespace AND e."State"=2 AND e."Epoch"<=s."RetiredThroughEpoch" AND s."RetentionProtocolVersion"=@version
                    ORDER BY r."Id" LIMIT @rows FOR UPDATE OF r SKIP LOCKED
                ), bounded AS (SELECT "Id",sum(bytes) OVER(ORDER BY "Id") AS total FROM candidates),
                deleted AS (DELETE FROM {Table} AS r USING bounded AS b WHERE r."Id"=b."Id" AND b.total<=@bytes
                    AND EXISTS(SELECT 1 FROM "Infrastructure_DatasetLifecycle" WHERE "Id"=1 AND "NamespaceId"=@namespace
                        AND "MaintenanceId"=@maintenance AND "State"=1 AND "SchemaVersion"=@schema AND "GuardProtocol"=@guard)
                    RETURNING octet_length(r."Payload") AS bytes)
                SELECT count(*)::integer AS "Rows",coalesce(sum(bytes),0)::bigint AS "Bytes" FROM deleted
                """;
            var deleted = (await context.Database.SqlQueryRaw<CountRow>(sql,
                new NpgsqlParameter("pool", Pool.Value), new NpgsqlParameter("namespace", request.Binding.Namespace.Value),
                new NpgsqlParameter("version", request.Pool.ProtocolVersion), new NpgsqlParameter("rows", request.MaximumRows),
                new NpgsqlParameter("bytes", request.MaximumBytes), new NpgsqlParameter("maintenance", request.Binding.MaintenanceId),
                new NpgsqlParameter("schema", request.Binding.SchemaVersion), new NpgsqlParameter("guard", (int)request.Binding.GuardProtocol))
                .ToArrayAsync(cancellationToken)).Single();
            var observed = await FiniteOperationEpochStore.ReadTimeAsync(context, cancellationToken);
            var remainingSql = $"SELECT EXISTS(SELECT 1 FROM {Table}) AS \"Value\"";
            var remains = await context.Database.SqlQueryRaw<bool>(remainingSql).SingleAsync(cancellationToken);
            return new(deleted.Rows, deleted.Bytes, remains ? FiniteOperationEligibleWork.Present : FiniteOperationEligibleWork.NoneObserved,
                observed, remains ? observed : null);
        }

        public async Task<IReadOnlyList<FiniteOperationEpochAddress>> FindNonemptyEpochsEnlistedAsync(FiniteEpochInspectionRequest request,
            CancellationToken cancellationToken) {
            await using var context = await owner.Transactions.CreateEnlistedAsync(owner.CreateNeutralOptions(),
                static options => new FiniteOperationDbContext(options), cancellationToken);
            TransactionIds.Add(await context.Database.SqlQueryRaw<string>(
                "SELECT pg_current_xact_id()::text AS \"Value\"").SingleAsync(cancellationToken));
            var sql = $"""
                SELECT candidate."NamespaceId", candidate."Epoch" FROM unnest(@namespaces,@epochs) AS candidate("NamespaceId","Epoch")
                WHERE EXISTS(SELECT 1 FROM {Table} AS r WHERE r."NamespaceId"=candidate."NamespaceId" AND r."Epoch"=candidate."Epoch")
                """;
            var rows = await context.Database.SqlQueryRaw<EpochRow>(sql,
                new NpgsqlParameter("namespaces", request.Candidates.Select(candidate => candidate.Namespace.Value).ToArray()),
                new NpgsqlParameter("epochs", request.Candidates.Select(candidate => candidate.Epoch).ToArray())).ToArrayAsync(cancellationToken);
            if (AfterInspection is { } after) {
                await after(cancellationToken);
            }
            return rows.Select(row => new FiniteOperationEpochAddress(new(row.NamespaceId), Pool, row.Epoch)).ToArray();
        }
    }

    private sealed class CountRow {
        public int Rows { get; set; }
        public long Bytes { get; set; }
    }

    private sealed class EpochRow {
        public Guid NamespaceId { get; set; }
        public long Epoch { get; set; }
    }
}
