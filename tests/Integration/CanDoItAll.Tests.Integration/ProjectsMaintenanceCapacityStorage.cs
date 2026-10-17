using CanDoItAll.Infrastructure.Persistence;
using System.Data;
using CanDoItAll.Modules.Projects;
using Npgsql;

namespace CanDoItAll.Tests.Integration;

internal sealed record CapacityReceiptCount(long Rows, long Bytes, long EligibleRows, long EligibleBytes);
internal sealed record CapacityTableStatistics(long LiveEstimate, long DeadEstimate, long Inserts, long Updates,
    long Deletes, long VacuumCount, long AutovacuumCount, long AnalyzeCount, long AutoanalyzeCount,
    long ChangesSinceAnalyze, DateTimeOffset? LastVacuum, DateTimeOffset? LastAutovacuum,
    DateTimeOffset? LastAnalyze, DateTimeOffset? LastAutoanalyze, string[] Options);
internal sealed record CapacityRelation(string Name, long MainBytes, long FreeSpaceMapBytes, long VisibilityMapBytes,
    long TableBytes, long IndexBytes, long TotalBytes, long ToastHeapBytes, long ToastIndexBytes, long ToastTotalBytes,
    CapacityTableStatistics MainStatistics, CapacityTableStatistics? ToastStatistics);
internal sealed record CapacityStorageSnapshot(DateTimeOffset ServerUtc, decimal ClusterWalPosition, decimal ClusterWalBytes,
    long DatabaseReadBlocks, long DatabaseHitBlocks, long DatabaseDeadlocks, long DatabaseTemporaryBytes,
    CapacityRelation[] Relations, IReadOnlyDictionary<string, string> Settings);
internal sealed record CapacitySeed(Guid Namespace, int InitialRows, int ArrivalsPerSecond, int Seconds,
    int SmallPayloadBytes, int LargePayloadBytes, bool Mixed, long TotalRows, long TotalBytes);

internal sealed class ProjectsMaintenanceCapacityStorage(string connectionString) {
    internal const int InitialRows = 65_537;
    internal const int ArrivalSeconds = 360;
    internal const int ReuseRows = 8_193;
    internal const long MaximumRawBytes = 4L * 1024 * 1024 * 1024;
    internal const int MaximumRows = 250_000;
    private static readonly string[] Tables = [ProjectDefinitionReceiptRecord.TableName, FiniteOperationEpochRecord.TableName,
        FiniteOperationStateRecord.TableName, "Projects_Projects", "Projects_ProjectPhases", "Projects_ProjectOptionSelections", "Infrastructure_SearchDocuments"];

    internal async Task<NpgsqlConnection> OpenAsync(CancellationToken token) {
        var connection = new NpgsqlConnection(connectionString);
        try {
            await connection.OpenAsync(token);
            return connection;
        } catch {
            await connection.DisposeAsync();
            throw;
        }
    }

    internal async Task<int> PayloadSizeAsync(Guid operation, CancellationToken token) {
        await using var connection = await OpenAsync(token);
        await using var command = new NpgsqlCommand("SELECT octet_length(\"AcceptedPayload\") FROM \"Projects_DefinitionReceipts\" WHERE \"OperationId\"=@operation", connection);
        command.Parameters.AddWithValue("operation", operation);
        return (int)(await command.ExecuteScalarAsync(token) ?? throw new InvalidOperationException("The accepted template receipt is missing."));
    }

    internal static CapacitySeed Plan(bool mixed, int smallBytes, int largeBytes, int initial = InitialRows, int seconds = ArrivalSeconds) {
        if (smallBytes <= 0 || largeBytes <= 0 || Math.Max(smallBytes, largeBytes) > ProjectDefinitionReceiptRecord.MaximumPayloadBytes ||
            initial is < 1 or > InitialRows || seconds is < 0 or > ArrivalSeconds) {
            throw new ArgumentException("The maintenance cohort is outside its approved bounds.");
        }
        var rate = mixed ? 50 : 150;
        var rows = checked(initial + rate * seconds);
        var largeRows = mixed ? initial / 20 + rate * seconds / 20 : 0;
        var bytes = checked((long)(rows - largeRows) * smallBytes + (long)largeRows * largeBytes);
        var foregroundRows = rate * (30 + 120 + ArrivalSeconds);
        var reuseRows = ReuseRows * 3;
        var remainingLarge = mixed ? foregroundRows / 20 + 3 * (ReuseRows / 20) : 0;
        var totalRows = rows + foregroundRows + reuseRows + 32;
        var worstBytes = checked(bytes + (long)(foregroundRows + reuseRows - remainingLarge) * (smallBytes + 32) +
            (long)remainingLarge * (largeBytes + 32) + 32L * ProjectDefinitionReceiptRecord.MaximumPayloadBytes);
        if (totalRows > MaximumRows || worstBytes > MaximumRawBytes) {
            throw new InvalidOperationException("The declared maintenance workload exceeds its row or raw-payload preflight bound.");
        }
        return new(Guid.NewGuid(), initial, rate, seconds, smallBytes, largeBytes, mixed, rows, bytes);
    }

    internal async Task SeedAsync(CapacitySeed seed, Guid smallOperation, Guid largeOperation, CancellationToken token) {
        await using var connection = await OpenAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(token);
        await using var command = new NpgsqlCommand("""
            INSERT INTO "Infrastructure_FiniteOperationState" ("NamespaceId","PoolId","RetentionProtocolVersion","CurrentEpoch","RetiredThroughEpoch") VALUES (@namespace,@pool,@version,@epochs,0);
            INSERT INTO "Infrastructure_FiniteOperationEpochs" ("NamespaceId","PoolId","Epoch","State","OpenedAtUtc","AdmissionClosesAtUtc",
                "ClosedAtUtc","MutationDeadlineMilliseconds","ReplayRetentionSeconds")
            SELECT @namespace, @pool, bucket+1, 2, clock_timestamp()-interval '3 days', clock_timestamp()-interval '2 days'-interval '1 second',
                clock_timestamp()-interval '2 days', 1000, CASE WHEN bucket=0 THEN 1 ELSE 176400+bucket END
            FROM generate_series(0,@seconds) AS bucket;
            INSERT INTO "Projects_DefinitionReceipts" ("NamespaceId","Epoch","CallerKind","CallerKey","OperationId",
                "SchemaVersion","NormalizationVersion","ResultVersion","CommandKind","SemanticDigest","ProjectId",
                "LifetimeId","AcceptedRevision","AcceptedAtUtc","AcceptedPayload","CreationFingerprint")
            SELECT @namespace, bucket+1, r."CallerKind", r."CallerKey", gen_random_uuid(), r."SchemaVersion", r."NormalizationVersion",
                r."ResultVersion", r."CommandKind", r."SemanticDigest", r."ProjectId", r."LifetimeId", r."AcceptedRevision",
                r."AcceptedAtUtc", r."AcceptedPayload", r."CreationFingerprint"
            FROM generate_series(0,@seconds) AS bucket
            CROSS JOIN LATERAL generate_series(1,CASE WHEN bucket=0 THEN @initial ELSE @rate END) AS item
            JOIN "Projects_DefinitionReceipts" AS r ON r."OperationId"=CASE
                WHEN @mixed AND (item+CASE WHEN bucket=0 THEN 0 ELSE (bucket-1)*@rate END)%20=0 THEN @large ELSE @small END;
            """, connection, transaction) { CommandTimeout = 180 };
        command.Parameters.AddWithValue("namespace", seed.Namespace);
        command.Parameters.AddWithValue("pool", ProjectFiniteOperationPool.Pool.Value);
        command.Parameters.AddWithValue("version", ProjectFiniteOperationPool.ProtocolVersion);
        command.Parameters.AddWithValue("epochs", seed.Seconds + 1L);
        command.Parameters.AddWithValue("seconds", seed.Seconds);
        command.Parameters.AddWithValue("initial", seed.InitialRows);
        command.Parameters.AddWithValue("rate", seed.ArrivalsPerSecond);
        command.Parameters.AddWithValue("mixed", seed.Mixed);
        command.Parameters.AddWithValue("small", smallOperation);
        command.Parameters.AddWithValue("large", largeOperation);
        await command.ExecuteNonQueryAsync(token);
        await transaction.CommitAsync(token);
        var actual = await CountAsync(seed.Namespace, token);
        if (actual.Rows != seed.TotalRows || actual.Bytes != seed.TotalBytes) {
            throw new InvalidOperationException("The seeded receipt population differs from its exact declared size.");
        }
    }

    internal async Task<DateTimeOffset> ScheduleAsync(CapacitySeed seed, CancellationToken token) {
        await using var connection = await OpenAsync(token);
        await using var command = new NpgsqlCommand("""
            WITH anchor AS (SELECT clock_timestamp()+interval '3 seconds' AS starts)
            UPDATE "Infrastructure_FiniteOperationEpochs" AS e SET "OpenedAtUtc"=a.starts-interval '3 days',
                "AdmissionClosesAtUtc"=a.starts-interval '2 days'-interval '1 second', "ClosedAtUtc"=a.starts-interval '2 days',
                "ReplayRetentionSeconds"=CASE WHEN e."Epoch"=1 THEN 1 ELSE 172800+e."Epoch"-1 END
            FROM anchor AS a WHERE e."NamespaceId"=@namespace AND e."PoolId"=@pool RETURNING a.starts;
            """, connection);
        command.Parameters.AddWithValue("namespace", seed.Namespace);
        command.Parameters.AddWithValue("pool", ProjectFiniteOperationPool.Pool.Value);
        return new DateTimeOffset((DateTime)(await command.ExecuteScalarAsync(token)
            ?? throw new InvalidOperationException("The synthetic schedule is missing.")));
    }

    internal async Task<CapacityReceiptCount> CountAsync(Guid @namespace, CancellationToken token) {
        await using var connection = await OpenAsync(token);
        await using var command = new NpgsqlCommand("""
            SELECT count(*)::bigint, COALESCE(sum(octet_length(r."AcceptedPayload")),0)::bigint,
                count(*) FILTER (WHERE e."State"=2 AND e."ClosedAtUtc"+e."ReplayRetentionSeconds"*interval '1 second'<=clock_timestamp()
                    AND e."AdmissionClosesAtUtc"+e."MutationDeadlineMilliseconds"*interval '1 millisecond'
                        +e."ReplayRetentionSeconds"*interval '1 second'<=clock_timestamp())::bigint,
                COALESCE(sum(octet_length(r."AcceptedPayload")) FILTER (WHERE e."State"=2
                    AND e."ClosedAtUtc"+e."ReplayRetentionSeconds"*interval '1 second'<=clock_timestamp()
                    AND e."AdmissionClosesAtUtc"+e."MutationDeadlineMilliseconds"*interval '1 millisecond'
                        +e."ReplayRetentionSeconds"*interval '1 second'<=clock_timestamp()),0)::bigint
            FROM "Projects_DefinitionReceipts" AS r JOIN "Infrastructure_FiniteOperationEpochs" AS e USING ("NamespaceId","Epoch")
            WHERE r."NamespaceId"=@namespace AND e."PoolId"=@pool;
            """, connection) { CommandTimeout = 60 };
        command.Parameters.AddWithValue("namespace", @namespace);
        command.Parameters.AddWithValue("pool", ProjectFiniteOperationPool.Pool.Value);
        await using var reader = await command.ExecuteReaderAsync(token);
        await reader.ReadAsync(token);
        return new(reader.GetInt64(0), reader.GetInt64(1), reader.GetInt64(2), reader.GetInt64(3));
    }

    internal async Task ClearSyntheticAsync(Guid @namespace, Guid active, CancellationToken token) {
        if (@namespace == Guid.Empty || @namespace == active) {
            throw new ArgumentException("Synthetic cleanup cannot target the active namespace.");
        }
        await using var connection = await OpenAsync(token);
        await using var command = new NpgsqlCommand("""
            DELETE FROM "Projects_DefinitionReceipts" WHERE "NamespaceId"=@namespace;
            DELETE FROM "Infrastructure_FiniteOperationEpochs" WHERE "NamespaceId"=@namespace;
            DELETE FROM "Infrastructure_FiniteOperationState" WHERE "NamespaceId"=@namespace;
            """, connection) { CommandTimeout = 60 };
        command.Parameters.AddWithValue("namespace", @namespace);
        await command.ExecuteNonQueryAsync(token);
        await using var verify = new NpgsqlCommand("""
            SELECT EXISTS (SELECT 1 FROM "Projects_DefinitionReceipts" WHERE "NamespaceId"=@namespace)
                OR EXISTS (SELECT 1 FROM "Infrastructure_FiniteOperationEpochs" WHERE "NamespaceId"=@namespace)
                OR EXISTS (SELECT 1 FROM "Infrastructure_FiniteOperationState" WHERE "NamespaceId"=@namespace);
            """, connection);
        verify.Parameters.AddWithValue("namespace", @namespace);
        if ((bool)(await verify.ExecuteScalarAsync(token))!) {
            throw new InvalidOperationException("The exact synthetic namespace still contains owned cleanup residue.");
        }
    }

    internal async Task VacuumAsync(CancellationToken token) {
        await using var connection = await OpenAsync(token);
        await using var command = new NpgsqlCommand("VACUUM (ANALYZE) \"Projects_DefinitionReceipts\", \"Infrastructure_FiniteOperationEpochs\", \"Infrastructure_FiniteOperationState\"", connection) { CommandTimeout = 120 };
        await command.ExecuteNonQueryAsync(token);
    }

    internal async Task AnalyzeAsync(CancellationToken token) {
        await using var connection = await OpenAsync(token);
        await using var command = new NpgsqlCommand("ANALYZE \"Projects_DefinitionReceipts\", \"Infrastructure_FiniteOperationEpochs\", \"Infrastructure_FiniteOperationState\"", connection) { CommandTimeout = 120 };
        await command.ExecuteNonQueryAsync(token);
    }

    internal async Task VerifyBackendExitAsync(int[] processes, CancellationToken token) {
        if (processes.Length is < 1 or > CanDoItAll.Tests.Maintenance.MaintenanceHostProtocol.MaximumBackendProcesses) {
            throw new ArgumentException("The owned backend evidence is missing or exceeds its bound.");
        }
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        await using var connection = await OpenAsync(deadline.Token);
        await using var command = new NpgsqlCommand("SELECT count(*) FROM pg_stat_activity WHERE datname=current_database() AND pid=ANY(@processes)", connection);
        command.Parameters.AddWithValue("processes", processes);
        while ((long)(await command.ExecuteScalarAsync(deadline.Token))! != 0) {
            await Task.Delay(TimeSpan.FromMilliseconds(100), deadline.Token);
        }
    }

    internal async Task<CapacityStorageSnapshot> ObserveAsync(CancellationToken token) {
        await using var connection = await OpenAsync(token);
        var relations = new List<CapacityRelation>();
        await using (var command = new NpgsqlCommand("""
            SELECT c.relname, pg_relation_size(c.oid), pg_relation_size(c.oid,'fsm'), pg_relation_size(c.oid,'vm'),
                pg_table_size(c.oid), pg_indexes_size(c.oid), pg_total_relation_size(c.oid),
                CASE WHEN c.reltoastrelid=0 THEN 0 ELSE pg_relation_size(c.reltoastrelid) END,
                CASE WHEN c.reltoastrelid=0 THEN 0 ELSE pg_indexes_size(c.reltoastrelid) END,
                CASE WHEN c.reltoastrelid=0 THEN 0 ELSE pg_total_relation_size(c.reltoastrelid) END,
                c.reltoastrelid<>0,
                s.n_live_tup,s.n_dead_tup,s.n_tup_ins,s.n_tup_upd,s.n_tup_del,s.vacuum_count,s.autovacuum_count,
                s.analyze_count,s.autoanalyze_count,s.n_mod_since_analyze,s.last_vacuum,s.last_autovacuum,
                s.last_analyze,s.last_autoanalyze,COALESCE(c.reloptions,ARRAY[]::text[]),
                t.n_live_tup,t.n_dead_tup,t.n_tup_ins,t.n_tup_upd,t.n_tup_del,t.vacuum_count,t.autovacuum_count,
                t.analyze_count,t.autoanalyze_count,t.n_mod_since_analyze,t.last_vacuum,t.last_autovacuum,
                t.last_analyze,t.last_autoanalyze,COALESCE(tc.reloptions,ARRAY[]::text[])
            FROM pg_class AS c JOIN pg_stat_user_tables AS s ON s.relid=c.oid
            LEFT JOIN pg_stat_all_tables AS t ON t.relid=c.reltoastrelid
            LEFT JOIN pg_class AS tc ON tc.oid=c.reltoastrelid
            WHERE c.relname=ANY(@tables) ORDER BY c.relname;
            """, connection)) {
            command.Parameters.AddWithValue("tables", Tables);
            await using var reader = await command.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token)) {
                relations.Add(new(reader.GetString(0), reader.GetInt64(1), reader.GetInt64(2), reader.GetInt64(3),
                    reader.GetInt64(4), reader.GetInt64(5), reader.GetInt64(6), reader.GetInt64(7), reader.GetInt64(8),
                    reader.GetInt64(9), ReadStatistics(reader, 11), reader.GetBoolean(10) ? ReadStatistics(reader, 26) : null));
            }
        }
        if (relations.Count != Tables.Length) {
            throw new InvalidOperationException("The maintenance storage observation is missing an owned relation.");
        }
        var settings = new Dictionary<string, string>(StringComparer.Ordinal);
        await using (var command = new NpgsqlCommand("""
            SELECT name,setting FROM pg_settings WHERE name IN ('server_version','block_size','autovacuum',
                'autovacuum_vacuum_threshold','autovacuum_vacuum_scale_factor','autovacuum_naptime',
                'default_toast_compression','wal_compression','checkpoint_timeout','max_connections','shared_buffers') ORDER BY name;
            """, connection)) {
            await using var reader = await command.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token)) {
                settings.Add(reader.GetString(0), reader.GetString(1));
            }
        }
        if (!settings.TryGetValue("autovacuum", out var autovacuum) || autovacuum != "on") {
            throw new InvalidOperationException("The declared capacity workload requires autovacuum enabled.");
        }
        await using var summary = new NpgsqlCommand("""
            SELECT clock_timestamp(), pg_wal_lsn_diff(pg_current_wal_insert_lsn(),'0/0'::pg_lsn),
                (SELECT wal_bytes FROM pg_stat_wal), blks_read,blks_hit,deadlocks,temp_bytes
            FROM pg_stat_database WHERE datname=current_database();
            """, connection);
        await using var summaryReader = await summary.ExecuteReaderAsync(token);
        await summaryReader.ReadAsync(token);
        return new(new DateTimeOffset(summaryReader.GetDateTime(0)), summaryReader.GetDecimal(1), summaryReader.GetDecimal(2),
            summaryReader.GetInt64(3), summaryReader.GetInt64(4), summaryReader.GetInt64(5), summaryReader.GetInt64(6), relations.ToArray(), settings);
    }

    private static CapacityTableStatistics ReadStatistics(NpgsqlDataReader reader, int offset) {
        DateTimeOffset? Time(int position) => reader.IsDBNull(position) ? null : new DateTimeOffset(reader.GetDateTime(position));
        return new(reader.GetInt64(offset), reader.GetInt64(offset + 1), reader.GetInt64(offset + 2), reader.GetInt64(offset + 3),
            reader.GetInt64(offset + 4), reader.GetInt64(offset + 5), reader.GetInt64(offset + 6), reader.GetInt64(offset + 7),
            reader.GetInt64(offset + 8), reader.GetInt64(offset + 9), Time(offset + 10), Time(offset + 11),
            Time(offset + 12), Time(offset + 13), reader.GetFieldValue<string[]>(offset + 14));
    }

    internal async Task<CapacityLock> LockAsync(Guid @namespace, bool state, CancellationToken token) {
        var connection = await OpenAsync(token);
        NpgsqlTransaction? transaction = null;
        try {
            transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, token);
            await using var command = new NpgsqlCommand(state
                ? "SELECT 1 FROM \"Infrastructure_FiniteOperationState\" WHERE \"NamespaceId\"=@namespace FOR UPDATE"
                : "SELECT 1 FROM \"Projects_DefinitionReceipts\" WHERE \"NamespaceId\"=@namespace FOR UPDATE", connection, transaction);
            command.Parameters.AddWithValue("namespace", @namespace);
            if (await command.ExecuteScalarAsync(token) is null) {
                throw new InvalidOperationException("The intended maintenance pressure target is missing.");
            }
            return new(connection, transaction);
        } catch {
            if (transaction is not null) {
                await transaction.DisposeAsync();
            }
            await connection.DisposeAsync();
            throw;
        }
    }

    internal sealed class CapacityLock(NpgsqlConnection connection, NpgsqlTransaction transaction) : IAsyncDisposable {
        private bool disposed;
        public async ValueTask DisposeAsync() {
            if (disposed) {
                return;
            }
            disposed = true;
            try {
                await transaction.DisposeAsync();
            } finally {
                await connection.DisposeAsync();
            }
        }
    }
}
