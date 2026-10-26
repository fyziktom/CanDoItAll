using System.Data;
using System.Data.Common;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace CanDoItAll.Tests.Integration;

public sealed partial class WorkspaceSettingsMigrationTests {
    private const string PolicyPreviousMigration = "20261017082009_AddResourceMetadataRevisions";
    private const string PolicyMigration = "20261017130214_AddWorkspaceDecisionDataPolicy";
    private const string PolicyCheck = "CK_Workspace_Settings_DecisionDataPolicyJson";
    private static readonly DecisionDataPolicy CompletePolicy = DecisionDataPolicy.PlanningDefault(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(2));

    [Fact]
    public async Task Policy_upgrade_preserves_all_ten_predecessor_fields_and_legacy_absence_without_revision_churn() {
        foreach (var retained in new[] { false, true }) {
            await using var fixture = await Fixture.CreatePolicyPredecessorAsync();
            await using var context = fixture.Canonical();
            if (retained) {
                await SeedPolicyPredecessorAsync(context);
            }
            var before = await PolicyPredecessorPayloadsAsync(context);
            var columns = await PolicyColumnsAsync(context);
            Assert.Equal(10, columns.Length);
            var indexes = await PolicyIndexesAsync(context);
            Assert.Equal(2, indexes.Length);
            for (var restart = 0; restart < 2; restart++) {
                await UpgradePolicyAsync(context);
                Assert.Equal(before, await PolicyPredecessorPayloadsAsync(context));
                Assert.All(await context.Set<WorkspaceSettings>().AsNoTracking().ToArrayAsync(), row => Assert.Null(row.DecisionDataPolicyJson));
                Assert.Equal(columns, (await PolicyColumnsAsync(context)).Where(column => column.Name != nameof(WorkspaceSettings.DecisionDataPolicyJson)));
                Assert.Equal(indexes, await PolicyIndexesAsync(context));
                Assert.Equal(new PolicyColumn(nameof(WorkspaceSettings.DecisionDataPolicyJson), "text", false, null),
                    Assert.Single(await PolicyColumnsAsync(context), column => column.Name == nameof(WorkspaceSettings.DecisionDataPolicyJson)));
                Assert.False(context.Database.HasPendingModelChanges());
                Assert.Equal(186, context.Model.GetEntityTypes().Count());
                await using var owner = fixture.Owner();
                foreach (var model in new[] { context.GetService<IDesignTimeModel>().Model, owner.GetService<IDesignTimeModel>().Model }) {
                    var entity = model.FindEntityType(typeof(WorkspaceSettings))!;
                    var policy = entity.FindProperty(nameof(WorkspaceSettings.DecisionDataPolicyJson))!;
                    Assert.Equal(typeof(string), policy.ClrType);
                    Assert.True(policy.IsNullable);
                    Assert.Equal("TEXT", policy.GetColumnType());
                    Assert.Null(policy.GetDefaultValueSql());
                    Assert.Equal(ValueGenerated.Never, policy.ValueGenerated);
                    Assert.Equal("\"DecisionDataPolicyJson\" IS NULL OR octet_length(\"DecisionDataPolicyJson\") BETWEEN 1 AND 1024",
                        Assert.Single(entity.GetCheckConstraints(), check => check.Name == PolicyCheck).Sql);
                }
            }
            await context.Database.MigrateAsync();
            var binding = await fixture.PolicyCoordinator.EnsureReadyAsync(fixture.Profile);
            Assert.Equal(binding, await fixture.PolicyCoordinator.EnsureReadyAsync(fixture.Profile));
            Assert.Equal(before, await PolicyPredecessorPayloadsAsync(context));
        }
    }

    [Fact]
    public async Task Policy_check_counts_encoded_bytes_and_irreversible_down_preserves_configured_and_null_history() {
        await using var fixture = await Fixture.CreatePolicyPredecessorAsync();
        await using var context = fixture.Canonical();
        var ids = await SeedPolicyPredecessorAsync(context);
        await UpgradePolicyAsync(context);
        await fixture.SeedHistoricalPolicyActivationAsync();
        var owner = fixture.PolicyWriter();
        Assert.Null((await owner.ReadV2Async()).DecisionDataPolicy);
        foreach (var text in new[] { "x", new string('x', 1024), new string('é', 512) }) {
            await SetPolicyTextAsync(context, ids.Current, text);
            Assert.Equal(text, (await context.Set<WorkspaceSettings>().AsNoTracking().SingleAsync(row => row.Id == ids.Current)).DecisionDataPolicyJson);
            await Assert.ThrowsAsync<WorkspaceSettingsPolicyUnavailableException>(() => owner.ReadV2Async());
        }
        foreach (var invalid in new[] { "", new string('x', 1025), new string('é', 513) }) {
            var before = await PolicyFullPayloadsAsync(context);
            var failure = await Assert.ThrowsAsync<PostgresException>(() => SetPolicyTextAsync(context, ids.Current, invalid));
            Assert.Equal(PostgresErrorCodes.CheckViolation, failure.SqlState);
            Assert.Equal(PolicyCheck, failure.ConstraintName);
            Assert.Equal(before, await PolicyFullPayloadsAsync(context));
        }
        await SetPolicyTextAsync(context, ids.Current, null);
        var original = await owner.ReadV2Async();
        var accepted = Assert.IsType<WorkspaceSettingsSaveOutcomeV2.Accepted>(await owner.ReplaceV2Async(
            new(original.Expectation, original.Values, CompletePolicy)));
        await SetPolicyTextAsync(context, ids.Inactive, " \n" + DecisionDataPolicyCodec.Serialize(CompletePolicy) + "\n ");
        Assert.Equal(accepted.Snapshot, await owner.ReadV2Async());
        foreach (var allNull in new[] { false, true }) {
            if (allNull) {
                await context.Database.ExecuteSqlRawAsync("UPDATE \"Workspace_Settings\" SET \"DecisionDataPolicyJson\" = NULL");
            }
            var payload = await PolicyFullPayloadsAsync(context);
            var schema = await PolicySchemaAsync(context);
            var history = await PolicyHistoryAsync(context);
            await UpgradePolicyAsync(context);
            Assert.Equal(payload, await PolicyFullPayloadsAsync(context));
            var failure = await Assert.ThrowsAsync<PostgresException>(() => context.GetService<IMigrator>().MigrateAsync(PolicyPreviousMigration));
            RequirePolicyRefusal(failure);
            Assert.Contains("downgrade is unsupported", failure.MessageText, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(payload, await PolicyFullPayloadsAsync(context));
            Assert.Equal(schema, await PolicySchemaAsync(context));
            Assert.Equal(history, await PolicyHistoryAsync(context));
        }
    }

    [Fact]
    public async Task Policy_guard_refuses_schema_drift_wrong_isolation_and_lock_timeout_and_rolls_back_post_DDL_failure() {
        await using var fixture = await Fixture.CreatePolicyPredecessorAsync();
        await using var context = fixture.Canonical();
        await SeedPolicyPredecessorAsync(context);
        foreach (var drift in Enum.GetValues<PolicyDrift>()) {
            await SetPolicyDriftAsync(context, drift, enabled: true);
            Exception? primary = null;
            try {
                var before = await PolicyPredecessorPayloadsAsync(context);
                var schema = await PolicySchemaAsync(context);
                var history = await PolicyHistoryAsync(context);
                RequirePolicyRefusal(await Assert.ThrowsAsync<PostgresException>(() => UpgradePolicyAsync(context)));
                Assert.Equal(before, await PolicyPredecessorPayloadsAsync(context));
                Assert.Equal(schema, await PolicySchemaAsync(context));
                Assert.Equal(history, await PolicyHistoryAsync(context));
            } catch (Exception failure) {
                primary = failure;
                throw;
            } finally {
                try {
                    await SetPolicyDriftAsync(context, drift, enabled: false);
                } catch (Exception cleanup) when (primary is not null) {
                    throw new AggregateException("Workspace migration assertion and schema restoration failed.", primary, cleanup);
                }
            }
        }
        var original = await PolicyPredecessorPayloadsAsync(context);
        var originalSchema = await PolicySchemaAsync(context);
        var originalHistory = await PolicyHistoryAsync(context);
        var up = PolicyUpSql(context);
        await using (var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead)) {
            RequirePolicyRefusal(await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlRawAsync(up)));
            await transaction.RollbackAsync();
        }
        Assert.Equal(originalSchema, await PolicySchemaAsync(context));
        Assert.Equal(originalHistory, await PolicyHistoryAsync(context));
        await RequirePolicyLockAsync(fixture, lifecycle: false);
        Assert.Equal(original, await PolicyPredecessorPayloadsAsync(context));
        Assert.Equal(originalSchema, await PolicySchemaAsync(context));
        Assert.Equal(originalHistory, await PolicyHistoryAsync(context));
        var fault = new PolicyPostDdlFault(up);
        await using (var failing = fixture.Canonical(fault)) {
            var failure = await Assert.ThrowsAsync<IOException>(() => UpgradePolicyAsync(failing));
            Assert.Equal(PolicyPostDdlFault.FailureMessage, failure.Message);
            Assert.True(fault.ObservedInstalledColumnAndCheck);
        }
        Assert.Equal(original, await PolicyPredecessorPayloadsAsync(context));
        Assert.Equal(originalSchema, await PolicySchemaAsync(context));
        Assert.Equal(originalHistory, await PolicyHistoryAsync(context));
        await RequirePolicyLockAsync(fixture, lifecycle: true);
        Assert.Equal(original, await PolicyPredecessorPayloadsAsync(context));
        Assert.Equal(PolicyMigration, (await context.Database.GetAppliedMigrationsAsync()).Last());
        Assert.False(context.Database.HasPendingModelChanges());
    }

    private static void RequirePolicyMigration(AppDbContext context) {
        var migrations = context.Database.GetMigrations().ToArray();
        var position = Array.IndexOf(migrations, PolicyMigration);
        Assert.True(position > 0, "Root must bind the generated canonical policy migration before integrating this overlay.");
        Assert.Equal(PolicyPreviousMigration, migrations[position - 1]);
    }

    private static async Task UpgradePolicyAsync(AppDbContext context, CancellationToken cancellationToken = default) {
        RequirePolicyMigration(context);
        await context.GetService<IMigrator>().MigrateAsync(PolicyMigration, cancellationToken);
    }

    private static string PolicyUpSql(AppDbContext context) {
        RequirePolicyMigration(context);
        var assembly = context.GetService<IMigrationsAssembly>();
        var migration = assembly.CreateMigration(assembly.Migrations[PolicyMigration], context.Database.ProviderName!);
        var operation = Assert.IsType<SqlOperation>(Assert.Single(migration.UpOperations));
        Assert.False(operation.SuppressTransaction);
        return operation.Sql;
    }

    private static void RequirePolicyRefusal(PostgresException failure) {
        Assert.Equal(PostgresErrorCodes.RaiseException, failure.SqlState);
        Assert.False(failure.IsTransient);
        Assert.Contains("workspace", failure.MessageText, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<PolicyRowIds> SeedPolicyPredecessorAsync(AppDbContext context) {
        var ids = new PolicyRowIds(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        foreach (var id in new[] { ids.Current, ids.Inactive, ids.Legacy }) {
            await context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "Workspace_Settings" ("Id", "WorkspaceName", "DefaultProviderProfileId", "DefaultPromptOutputFormat",
                    "CurrencyCode", "CurrencyCultureName", "Notes", "UpdatedAtUtc", "ConcurrencyToken", "IsCurrent")
                VALUES ({id}, {"Retained policy workspace"}, {Guid.NewGuid()}, {"JSON"}, {"CZK"}, {"cs-CZ"},
                    {"Retained UTF-8 漢字 <text>\nSecond line"}, {SavedAt.AddTicks(1234560)}, {Guid.NewGuid()}, {id == ids.Current})
                """);
        }
        return ids;
    }

    private static Task SetPolicyTextAsync(AppDbContext context, Guid id, string? text)
        => context.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Workspace_Settings\" SET \"DecisionDataPolicyJson\" = {text} WHERE \"Id\" = {id}");

    private static Task<string[]> PolicyPredecessorPayloadsAsync(AppDbContext context) => context.Database.SqlQueryRaw<string>("""
        SELECT to_jsonb(retained)::text AS "Value" FROM (
            SELECT "Id", "WorkspaceName", "DefaultProviderProfileId", "DefaultPromptOutputFormat", "CurrencyCode",
                "CurrencyCultureName", "Notes", "UpdatedAtUtc", "ConcurrencyToken", "IsCurrent" FROM "Workspace_Settings"
        ) retained ORDER BY retained."Id"
        """).ToArrayAsync();
    private static Task<string[]> PolicyFullPayloadsAsync(AppDbContext context) => context.Database.SqlQueryRaw<string>("""
        SELECT to_jsonb(retained)::text AS "Value" FROM "Workspace_Settings" retained ORDER BY retained."Id"
        """).ToArrayAsync();
    private static Task<string[]> PolicyHistoryAsync(AppDbContext context) => context.Database.SqlQueryRaw<string>("""
        SELECT to_jsonb(retained)::text AS "Value" FROM "__EFMigrationsHistory" retained ORDER BY retained."MigrationId"
        """).ToArrayAsync();
    private static Task<PolicyColumn[]> PolicyColumnsAsync(AppDbContext context) => context.Database.SqlQueryRaw<PolicyColumn>("""
        SELECT a.attname::text AS "Name", format_type(a.atttypid, a.atttypmod) AS "Type", a.attnotnull AS "NotNull",
            pg_get_expr(d.adbin, d.adrelid) AS "DefaultSql"
        FROM pg_attribute a LEFT JOIN pg_attrdef d ON d.adrelid = a.attrelid AND d.adnum = a.attnum
        WHERE a.attrelid = '"Workspace_Settings"'::regclass AND a.attnum > 0 AND NOT a.attisdropped
        ORDER BY a.attname COLLATE "C"
        """).ToArrayAsync();
    private static Task<string[]> PolicyIndexesAsync(AppDbContext context) => context.Database.SqlQueryRaw<string>("""
        SELECT pg_get_indexdef(i.indexrelid) || ':' || i.indisunique || ':' || i.indisvalid || ':' || i.indisready || ':' ||
            i.indislive || ':' || am.amname || ':' || coalesce(pg_get_expr(i.indpred, i.indrelid), '') AS "Value"
        FROM pg_index i JOIN pg_class c ON c.oid = i.indexrelid JOIN pg_am am ON am.oid = c.relam
        WHERE i.indrelid = '"Workspace_Settings"'::regclass ORDER BY c.relname COLLATE "C"
        """).ToArrayAsync();
    private static Task<string[]> PolicySchemaAsync(AppDbContext context) => context.Database.SqlQueryRaw<string>("""
        SELECT value AS "Value" FROM (
            SELECT 'column:' || a.attnum || ':' || a.attname || ':' || format_type(a.atttypid, a.atttypmod) || ':' ||
                a.attnotnull || ':' || coalesce(pg_get_expr(d.adbin, d.adrelid), '') AS value
            FROM pg_attribute a LEFT JOIN pg_attrdef d ON d.adrelid = a.attrelid AND d.adnum = a.attnum
            WHERE a.attrelid = '"Workspace_Settings"'::regclass AND a.attnum > 0 AND NOT a.attisdropped
            UNION ALL SELECT 'constraint:' || conname || ':' || pg_get_constraintdef(oid) || ':' || convalidated || ':' || conenforced
            FROM pg_constraint WHERE conrelid = '"Workspace_Settings"'::regclass
            UNION ALL SELECT 'index:' || pg_get_indexdef(indexrelid) || ':' || indisvalid || ':' || indisready || ':' || indislive
            FROM pg_index WHERE indrelid = '"Workspace_Settings"'::regclass
            UNION ALL SELECT 'trigger:' || pg_get_triggerdef(oid) FROM pg_trigger
            WHERE tgrelid = '"Workspace_Settings"'::regclass AND NOT tgisinternal
            UNION ALL SELECT 'relation:' || relkind::text || ':' || relpersistence::text || ':' || relrowsecurity || ':' || relforcerowsecurity
            FROM pg_class WHERE oid = '"Workspace_Settings"'::regclass
        ) observed ORDER BY value COLLATE "C"
        """).ToArrayAsync();

    private static Task SetPolicyDriftAsync(AppDbContext context, PolicyDrift drift, bool enabled) {
        var sql = drift switch {
            PolicyDrift.PartialColumn => enabled ? "ALTER TABLE \"Workspace_Settings\" ADD COLUMN \"DecisionDataPolicyJson\" text" :
                "ALTER TABLE \"Workspace_Settings\" DROP COLUMN \"DecisionDataPolicyJson\"",
            PolicyDrift.Default => enabled ? "ALTER TABLE \"Workspace_Settings\" ALTER COLUMN \"WorkspaceName\" SET DEFAULT 'unexpected'" :
                "ALTER TABLE \"Workspace_Settings\" ALTER COLUMN \"WorkspaceName\" DROP DEFAULT",
            PolicyDrift.Type => enabled ? "ALTER TABLE \"Workspace_Settings\" ALTER COLUMN \"WorkspaceName\" TYPE varchar(201)" :
                "ALTER TABLE \"Workspace_Settings\" ALTER COLUMN \"WorkspaceName\" TYPE varchar(200)",
            PolicyDrift.Nullability => enabled ? "ALTER TABLE \"Workspace_Settings\" ALTER COLUMN \"Notes\" DROP NOT NULL" :
                "ALTER TABLE \"Workspace_Settings\" ALTER COLUMN \"Notes\" SET NOT NULL",
            PolicyDrift.Predicate => enabled ? """
                DROP INDEX "IX_Workspace_Settings_IsCurrent";
                CREATE UNIQUE INDEX "IX_Workspace_Settings_IsCurrent" ON "Workspace_Settings" ("IsCurrent") WHERE "IsCurrent" AND "WorkspaceName" <> '';
                """ : """
                DROP INDEX "IX_Workspace_Settings_IsCurrent";
                CREATE UNIQUE INDEX "IX_Workspace_Settings_IsCurrent" ON "Workspace_Settings" ("IsCurrent") WHERE "IsCurrent";
                """,
            PolicyDrift.ExtraCheck => enabled ? "ALTER TABLE \"Workspace_Settings\" ADD CONSTRAINT workspace_policy_probe CHECK (length(\"Notes\") >= 0)" :
                "ALTER TABLE \"Workspace_Settings\" DROP CONSTRAINT workspace_policy_probe",
            PolicyDrift.UserTrigger => enabled ? """
                CREATE FUNCTION workspace_policy_probe() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NEW; END $$;
                CREATE TRIGGER workspace_policy_probe BEFORE UPDATE ON "Workspace_Settings"
                FOR EACH ROW EXECUTE FUNCTION workspace_policy_probe();
                """ : """
                DROP TRIGGER workspace_policy_probe ON "Workspace_Settings";
                DROP FUNCTION workspace_policy_probe();
                """,
            PolicyDrift.RowSecurity => enabled ? "ALTER TABLE \"Workspace_Settings\" ENABLE ROW LEVEL SECURITY" :
                "ALTER TABLE \"Workspace_Settings\" DISABLE ROW LEVEL SECURITY",
            PolicyDrift.LifecycleRowSecurity => enabled ? "ALTER TABLE \"Infrastructure_DatasetLifecycle\" ENABLE ROW LEVEL SECURITY" :
                "ALTER TABLE \"Infrastructure_DatasetLifecycle\" DISABLE ROW LEVEL SECURITY",
            PolicyDrift.HistoryRowSecurity => enabled ? "ALTER TABLE \"__EFMigrationsHistory\" ENABLE ROW LEVEL SECURITY" :
                "ALTER TABLE \"__EFMigrationsHistory\" DISABLE ROW LEVEL SECURITY",
            _ => throw new ArgumentOutOfRangeException(nameof(drift))
        };
        return context.Database.ExecuteSqlRawAsync(sql);
    }

    private static async Task RequirePolicyLockAsync(Fixture fixture, bool lifecycle) {
        await using var blocker = fixture.Canonical();
        await using var migration = fixture.Canonical();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        IDbContextTransaction? held = await blocker.Database.BeginTransactionAsync(cancellation.Token);
        Task? pending = null;
        Exception? primary = null;
        Exception? observed = null;
        try {
            await blocker.Database.ExecuteSqlRawAsync(lifecycle ?
                "LOCK TABLE \"Infrastructure_DatasetLifecycle\" IN ROW SHARE MODE" :
                "LOCK TABLE \"Workspace_Settings\" IN ACCESS EXCLUSIVE MODE", cancellation.Token);
            var holder = ((NpgsqlConnection)blocker.Database.GetDbConnection()).ProcessID;
            await migration.Database.OpenConnectionAsync(cancellation.Token);
            var waiter = ((NpgsqlConnection)migration.Database.GetDbConnection()).ProcessID;
            pending = UpgradePolicyAsync(migration, cancellation.Token);
            await RequirePolicyWaitAsync(fixture, waiter, holder, lifecycle, cancellation.Token);
            if (lifecycle) {
                var release = held;
                held = null;
                await release.DisposeAsync();
                await pending;
            } else {
                observed = await Record.ExceptionAsync(() => pending);
                var wrapper = Assert.IsType<InvalidOperationException>(observed);
                var databaseFailure = Assert.IsType<PostgresException>(wrapper.InnerException);
                Assert.Equal(PostgresErrorCodes.LockNotAvailable, databaseFailure.SqlState);
                Assert.True(databaseFailure.IsTransient);
            }
        } catch (Exception failure) {
            primary = failure;
            throw;
        } finally {
            await FinishPolicyOperationAsync(held, pending, cancellation, primary, observed);
        }
    }

    private static async Task RequirePolicyWaitAsync(Fixture fixture, int waiter, int holder, bool requireNoWorkspaceLock,
        CancellationToken cancellationToken) {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        while (true) {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT @holder = ANY(pg_blocking_pids(@waiter)),
                    EXISTS(SELECT 1 FROM pg_locks WHERE pid = @waiter AND granted AND relation = '"Workspace_Settings"'::regclass)
                """;
            command.Parameters.AddWithValue("holder", holder);
            command.Parameters.AddWithValue("waiter", waiter);
            await using (var reader = await command.ExecuteReaderAsync(deadline.Token)) {
                Assert.True(await reader.ReadAsync(deadline.Token));
                if (reader.GetBoolean(0)) {
                    if (requireNoWorkspaceLock) {
                        Assert.False(reader.GetBoolean(1));
                    }
                    return;
                }
            }
            await Task.Delay(TimeSpan.FromMilliseconds(20), deadline.Token);
        }
    }

    private static async Task FinishPolicyOperationAsync(IDbContextTransaction? blocker, Task? pending,
        CancellationTokenSource cancellation, Exception? primary, Exception? observed) {
        var failures = new List<Exception>();
        if (pending is { IsCompleted: false }) {
            try {
                await cancellation.CancelAsync();
            } catch (Exception failure) {
                failures.Add(failure);
            }
        }
        if (blocker is not null) {
            try {
                await blocker.DisposeAsync();
            } catch (Exception failure) {
                failures.Add(failure);
            }
        }
        if (pending is not null) {
            try {
                await pending;
            } catch (OperationCanceledException) when (cancellation.IsCancellationRequested) {
            } catch (Exception failure) {
                if (!ReferenceEquals(failure, primary) && !ReferenceEquals(failure, observed)) {
                    failures.Add(failure);
                }
            }
        }
        if (failures.Count != 0) {
            if (primary is not null) {
                failures.Insert(0, primary);
            }
            throw new AggregateException("Workspace migration operation or terminal cleanup failed.", failures);
        }
    }

    private sealed class PolicyPostDdlFault(string up) : DbCommandInterceptor {
        internal const string FailureMessage = "Owned Workspace policy post-DDL failure.";
        internal bool ObservedInstalledColumnAndCheck { get; private set; }

        public override async ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData, int result,
            CancellationToken cancellationToken = default) {
            if (!command.CommandText.Contains(up, StringComparison.Ordinal)) {
                return result;
            }
            Assert.NotNull(command.Transaction);
            await using var observe = command.Connection!.CreateCommand();
            observe.Transaction = command.Transaction;
            observe.CommandText = """
                SELECT EXISTS(SELECT 1 FROM pg_attribute WHERE attrelid = '"Workspace_Settings"'::regclass
                    AND attname = 'DecisionDataPolicyJson' AND NOT attisdropped)
                    AND EXISTS(SELECT 1 FROM pg_constraint WHERE conrelid = '"Workspace_Settings"'::regclass
                        AND conname = 'CK_Workspace_Settings_DecisionDataPolicyJson' AND convalidated AND conenforced)
                """;
            ObservedInstalledColumnAndCheck = (bool)(await observe.ExecuteScalarAsync(cancellationToken))!;
            throw new IOException(FailureMessage);
        }
    }

    private sealed record PolicyRowIds(Guid Current, Guid Inactive, Guid Legacy);
    private sealed record PolicyColumn(string Name, string Type, bool NotNull, string? DefaultSql);
    private enum PolicyDrift { PartialColumn, Default, Type, Nullability, Predicate, ExtraCheck, UserTrigger, RowSecurity, LifecycleRowSecurity, HistoryRowSecurity }
}
