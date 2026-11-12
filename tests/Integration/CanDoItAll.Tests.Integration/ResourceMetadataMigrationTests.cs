using CanDoItAll.Composition;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Search;
using CanDoItAll.Modules.Resources;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace CanDoItAll.Tests.Integration;

public sealed class ResourceMetadataMigrationTests {
    private const string PreviousMigration = "20261017053554_AddWorkbenchTaskPlanReceipts";
    private const string MigrationName = "AddResourceMetadataRevisions";
    private static readonly DateTimeOffset SavedAt = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
    private static readonly string[] LegacyNames = [
        "ConfigJson", "ConfigSchemaVersion", "ConnectorPluginKey", "CreatedAtUtc", "Description", "Id",
        "LinkedSecretIdsJson", "LocationOrIdentifier", "MaintainerPartyId", "Name", "OwnerPartyId", "ProjectId",
        "ProjectLifetimeId", "ResourceKind", "Sensitivity", "SupportsIndexing", "SupportsPreview", "UpdatedAtUtc", "ValidationStatus"
    ];

    [Fact]
    public async Task Predecessor_upgrade_preserves_every_retained_field_and_Search_with_one_stable_revision_per_row() {
        foreach (var retained in new[] { false, true }) {
            await using var fixture = await Fixture.CreateAsync();
            await using var previous = fixture.Canonical();
            if (retained) {
                foreach (var row in RetainedRows()) {
                    await InsertLegacyAsync(previous, row);
                }
                await SeedSearchAsync(previous);
            }
            var original = await RetainedAsync(previous);
            var search = await SearchAsync(previous);
            var columns = await ColumnsAsync(previous);
            Assert.Equal(LegacyNames, columns.Select(column => column.Name));
            RevisionObservation[]? revisions = null;
            for (var restart = 0; restart < 2; restart++) {
                await using var upgraded = fixture.Canonical();
                var migration = await UpgradeAsync(upgraded);
                Assert.Equal(migration, (await upgraded.Database.GetAppliedMigrationsAsync()).Last());
                Assert.Equal(original, await RetainedAsync(upgraded));
                Assert.Equal(search, await SearchAsync(upgraded));
                Assert.Equal(columns, (await ColumnsAsync(upgraded)).Where(column => column.Name != nameof(ProjectResource.Revision)));
                var current = await RevisionsAsync(upgraded);
                Assert.Equal(retained ? 6 : 0, current.Length);
                Assert.All(current, row => Assert.NotEqual(Guid.Empty, row.Revision));
                Assert.Equal(current.Length, current.Select(row => row.Revision).Distinct().Count());
                if (revisions is not null) {
                    Assert.Equal(revisions, current);
                }
                revisions = current;
                Assert.False(upgraded.Database.HasPendingModelChanges());
            }
        }
    }

    [Fact]
    public async Task Fresh_catalog_requires_explicit_nonempty_revisions_and_matches_the_single_owner_model() {
        await using var fixture = await Fixture.CreateAsync(previous: false);
        await using var canonical = fixture.Canonical();
        await UpgradeAsync(canonical);
        Assert.False(canonical.Database.HasPendingModelChanges());
        Assert.Equal(186, canonical.Model.GetEntityTypes().Count());
        await using var owner = fixture.Owner();
        var entity = Assert.Single(owner.GetService<IDesignTimeModel>().Model.GetEntityTypes());
        Assert.Equal(typeof(ProjectResource), entity.ClrType);
        var complete = canonical.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(ProjectResource))!;
        Assert.Equal(complete.ToDebugString(MetadataDebugStringOptions.LongDefault), entity.ToDebugString(MetadataDebugStringOptions.LongDefault));
        var revision = entity.FindProperty(nameof(ProjectResource.Revision))!;
        Assert.True(revision.IsConcurrencyToken);
        Assert.False(revision.IsNullable);
        Assert.Equal(ValueGenerated.Never, revision.ValueGenerated);
        Assert.Null(revision.GetDefaultValueSql());

        var columns = await ColumnsAsync(canonical);
        Assert.Equal(LegacyNames.Append(nameof(ProjectResource.Revision)).Order(StringComparer.Ordinal), columns.Select(column => column.Name));
        Assert.Equal(new ColumnObservation(nameof(ProjectResource.Revision), "uuid", true, null),
            Assert.Single(columns, column => column.Name == nameof(ProjectResource.Revision)));
        Assert.Equal(["PRIMARY KEY (\"Id\")"], await canonical.Database.SqlQueryRaw<string>("""
            SELECT pg_get_constraintdef(oid) AS "Value" FROM pg_constraint
            WHERE conrelid = '"Resources_ProjectResources"'::regclass AND contype = 'p'
            """).ToArrayAsync());
        Assert.Equal(["CK_Resources_ProjectResources_Revision"], await canonical.Database.SqlQueryRaw<string>("""
            SELECT conname::text AS "Value" FROM pg_constraint
            WHERE conrelid = '"Resources_ProjectResources"'::regclass AND contype = 'c' AND convalidated AND conenforced
            """).ToArrayAsync());
        var index = Assert.Single(await canonical.Database.SqlQueryRaw<IndexObservation>("""
            SELECT ARRAY(SELECT a.attname::text FROM unnest(i.indkey) WITH ORDINALITY k(attnum, position)
                    JOIN pg_attribute a ON a.attrelid = i.indrelid AND a.attnum = k.attnum ORDER BY k.position) AS "Columns",
                i.indisunique AS "Unique", i.indisvalid AS "Valid", i.indisready AS "Ready",
                i.indpred IS NULL AND i.indexprs IS NULL AND i.indnatts = i.indnkeyatts AS "Plain",
                am.amname::text AS "Method"
            FROM pg_index i JOIN pg_class c ON c.oid = i.indexrelid JOIN pg_am am ON am.oid = c.relam
            WHERE i.indrelid = '"Resources_ProjectResources"'::regclass AND NOT i.indisprimary
            """).ToArrayAsync());
        Assert.Equal([nameof(ProjectResource.ProjectId), nameof(ProjectResource.ProjectLifetimeId), nameof(ProjectResource.ConnectorPluginKey)], index.Columns);
        Assert.False(index.Unique);
        Assert.True(index.Valid && index.Ready && index.Plain);
        Assert.Equal("btree", index.Method);

        var omitted = await Assert.ThrowsAsync<PostgresException>(() => InsertLegacyAsync(canonical, RetainedRows()[0]));
        Assert.Equal(PostgresErrorCodes.NotNullViolation, omitted.SqlState);
        var row = new ProjectResource { ProjectId = Guid.NewGuid(), ProjectLifetimeId = Guid.NewGuid(),
            ConnectorPluginKey = ResourceConnectorPluginKeys.WebLink, Name = "Explicit application revision", CreatedAtUtc = SavedAt, UpdatedAtUtc = SavedAt };
        var originalRevision = row.Revision;
        Assert.NotEqual(Guid.Empty, originalRevision);
        owner.Add(row);
        await owner.SaveChangesAsync();
        var nullRevision = await Assert.ThrowsAsync<PostgresException>(() => canonical.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "Resources_ProjectResources" SET "Revision" = NULL WHERE "Id" = {row.Id}
            """));
        Assert.Equal(PostgresErrorCodes.NotNullViolation, nullRevision.SqlState);
        var emptyRevision = await Assert.ThrowsAsync<PostgresException>(() => canonical.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "Resources_ProjectResources" SET "Revision" = {Guid.Empty} WHERE "Id" = {row.Id}
            """));
        Assert.Equal(PostgresErrorCodes.CheckViolation, emptyRevision.SqlState);
        Assert.Equal("CK_Resources_ProjectResources_Revision", emptyRevision.ConstraintName);
        Assert.Equal(originalRevision, (await RevisionsAsync(canonical)).Single().Revision);
        owner.Add(new ProjectResource { ProjectId = row.ProjectId, ProjectLifetimeId = row.ProjectLifetimeId,
            ConnectorPluginKey = row.ConnectorPluginKey, Name = "Same nonunique candidate", CreatedAtUtc = SavedAt, UpdatedAtUtc = SavedAt });
        await owner.SaveChangesAsync();
        Assert.Equal(2, await owner.Set<ProjectResource>().CountAsync());
    }

    [Fact]
    public async Task Drift_partial_installation_lock_refusal_and_down_leave_history_and_retained_bytes_unchanged() {
        await using var fixture = await Fixture.CreateAsync();
        await using var context = fixture.Canonical();
        await InsertLegacyAsync(context, RetainedRows()[0]);
        await SeedSearchAsync(context);
        foreach (var drift in Enum.GetValues<Drift>()) {
            await SetDriftAsync(context, drift, enabled: true);
            Exception? primaryFailure = null;
            try {
                var before = await RetainedAsync(context);
                var search = await SearchAsync(context);
                var schema = await SchemaAsync(context);
                var failure = await Assert.ThrowsAsync<PostgresException>(() => UpgradeAsync(context));
                RequirePermanentRefusal(failure);
                Assert.Equal(PreviousMigration, (await context.Database.GetAppliedMigrationsAsync()).Last());
                Assert.Equal(before, await RetainedAsync(context));
                Assert.Equal(search, await SearchAsync(context));
                Assert.Equal(schema, await SchemaAsync(context));
            } catch (Exception failure) {
                primaryFailure = failure;
                throw;
            } finally {
                try {
                    await SetDriftAsync(context, drift, enabled: false);
                } catch (Exception cleanupFailure) when (primaryFailure is not null) {
                    throw new AggregateException("Resource migration assertion and schema restoration both failed.", primaryFailure, cleanupFailure);
                }
            }
        }

        var original = await RetainedAsync(context);
        var originalSearch = await SearchAsync(context);
        var originalSchema = await SchemaAsync(context);
        await using (var blocker = fixture.Canonical()) {
            var held = await blocker.Database.BeginTransactionAsync();
            await blocker.Database.ExecuteSqlRawAsync("LOCK TABLE \"Resources_ProjectResources\" IN ACCESS EXCLUSIVE MODE");
            var holderPid = ((NpgsqlConnection)blocker.Database.GetDbConnection()).ProcessID;
            await context.Database.OpenConnectionAsync();
            var migrationPid = ((NpgsqlConnection)context.Database.GetDbConnection()).ProcessID;
            var pending = UpgradeAsync(context);
            Exception? primaryFailure = null;
            try {
                await fixture.RequireWaitAsync(migrationPid, holderPid);
                var failure = await Record.ExceptionAsync(() => pending);
                var databaseFailure = failure as PostgresException ?? Assert.IsType<PostgresException>(failure?.InnerException);
                Assert.Equal(PostgresErrorCodes.LockNotAvailable, databaseFailure.SqlState);
            } catch (Exception failure) {
                primaryFailure = failure;
                throw;
            } finally {
                try {
                    await held.DisposeAsync();
                } catch (Exception cleanupFailure) when (primaryFailure is not null) {
                    throw new AggregateException("Resource migration lock assertion and blocker release both failed.", primaryFailure, cleanupFailure);
                } finally {
                    try {
                        await pending;
                    } catch {
                    }
                }
            }
            await context.Database.CloseConnectionAsync();
        }
        Assert.Equal(PreviousMigration, (await context.Database.GetAppliedMigrationsAsync()).Last());
        Assert.Equal(original, await RetainedAsync(context));
        Assert.Equal(originalSearch, await SearchAsync(context));
        Assert.Equal(originalSchema, await SchemaAsync(context));

        var migration = await UpgradeAsync(context);
        var installed = await SchemaAsync(context);
        var revisions = await RevisionsAsync(context);
        var down = await Assert.ThrowsAsync<PostgresException>(() => context.GetService<IMigrator>().MigrateAsync(PreviousMigration));
        RequirePermanentRefusal(down);
        Assert.Contains("downgrade is unsupported", down.MessageText, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(migration, (await context.Database.GetAppliedMigrationsAsync()).Last());
        Assert.Equal(original, await RetainedAsync(context));
        Assert.Equal(originalSearch, await SearchAsync(context));
        Assert.Equal(revisions, await RevisionsAsync(context));
        Assert.Equal(installed, await SchemaAsync(context));
    }

    private static void RequirePermanentRefusal(PostgresException failure) {
        Assert.Equal(PostgresErrorCodes.RaiseException, failure.SqlState);
        Assert.False(failure.IsTransient);
        Assert.Contains("resource", failure.MessageText, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<string> UpgradeAsync(AppDbContext context) {
        var migration = Assert.Single(context.Database.GetMigrations(), id => id.EndsWith("_" + MigrationName, StringComparison.Ordinal));
        await context.GetService<IMigrator>().MigrateAsync(migration);
        return migration;
    }

    private static RetainedRow[] RetainedRows() => [
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), ResourceConnectorPluginKeys.WebLink, " { \"url\" : \"https://example.invalid/source\" } ", "[]"),
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), ResourceConnectorPluginKeys.StorageObject, "{\"governed\":true}", "[]"),
        new(Guid.NewGuid(), Guid.Empty, null, "unknown.legacy", "not valid json", "[\"retained-secret-shape\"]"),
        new(Guid.NewGuid(), Guid.NewGuid(), null, "retained.orphan", "{}", "[]"),
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "unknown.large", new('x', 300 * 1024), new('s', 5000)),
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "unknown.unicode", "{\"text\":\"\\u003c漢字\\u003e\"}", "[]")
    ];

    private static Task InsertLegacyAsync(AppDbContext context, RetainedRow row)
        => context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Resources_ProjectResources" ("Id", "ProjectId", "ProjectLifetimeId", "OwnerPartyId", "MaintainerPartyId",
                "ResourceKind", "Name", "Description", "ConnectorPluginKey", "ConfigSchemaVersion", "LocationOrIdentifier",
                "ConfigJson", "LinkedSecretIdsJson", "ValidationStatus", "Sensitivity", "SupportsPreview", "SupportsIndexing", "CreatedAtUtc", "UpdatedAtUtc")
            VALUES ({row.Id}, {row.ProjectId}, {row.LifetimeId}, {Guid.NewGuid()}, {Guid.NewGuid()}, 1,
                {"  Retained resource  "}, {"First line\nSecond line — untouched"}, {row.Connector}, {"legacy/v1"},
                {"https://example.invalid/retained?literal=%3Cvalue%3E"}, {row.Configuration}, {row.SecretReferences}, 0, 1, true, false,
                {SavedAt}, {SavedAt.AddTicks(10)})
            """);

    private static async Task SeedSearchAsync(AppDbContext context) {
        context.Add(new SearchDocument { SourceType = SearchDocument.ResourceSourceType, SourceKey = Guid.NewGuid().ToString("D"),
            Category = "resource", Title = "Retained Search projection", Summary = "Unchanged summary", Body = "Retained body <raw> 漢字",
            Route = "/resources", UpdatedAtUtc = SavedAt });
        await context.SaveChangesAsync();
    }

    private static Task<string[]> RetainedAsync(AppDbContext context) => context.Database.SqlQueryRaw<string>("""
        SELECT (to_jsonb(r) - 'Revision')::text AS "Value" FROM "Resources_ProjectResources" r ORDER BY r."Id"
        """).ToArrayAsync();
    private static Task<string[]> SearchAsync(AppDbContext context) => context.Database.SqlQueryRaw<string>("""
        SELECT to_jsonb(s)::text AS "Value" FROM "Infrastructure_SearchDocuments" s ORDER BY s."Id"
        """).ToArrayAsync();
    private static Task<RevisionObservation[]> RevisionsAsync(AppDbContext context) => context.Database.SqlQueryRaw<RevisionObservation>("""
        SELECT "Id", "Revision" FROM "Resources_ProjectResources" ORDER BY "Id"
        """).ToArrayAsync();
    private static Task<ColumnObservation[]> ColumnsAsync(AppDbContext context) => context.Database.SqlQueryRaw<ColumnObservation>("""
        SELECT a.attname::text AS "Name", format_type(a.atttypid, a.atttypmod) AS "Type", a.attnotnull AS "NotNull",
            pg_get_expr(d.adbin, d.adrelid) AS "DefaultSql"
        FROM pg_attribute a LEFT JOIN pg_attrdef d ON d.adrelid = a.attrelid AND d.adnum = a.attnum
        WHERE a.attrelid = '"Resources_ProjectResources"'::regclass AND a.attnum > 0 AND NOT a.attisdropped
        ORDER BY a.attname COLLATE "C"
        """).ToArrayAsync();
    private static Task<string[]> SchemaAsync(AppDbContext context) => context.Database.SqlQueryRaw<string>("""
        SELECT value AS "Value" FROM (
            SELECT 'column:' || a.attnum || ':' || a.attname || ':' || format_type(a.atttypid, a.atttypmod) || ':' ||
                a.attnotnull || ':' || coalesce(pg_get_expr(d.adbin, d.adrelid), '') AS value
            FROM pg_attribute a LEFT JOIN pg_attrdef d ON d.adrelid = a.attrelid AND d.adnum = a.attnum
            WHERE a.attrelid = '"Resources_ProjectResources"'::regclass AND a.attnum > 0 AND NOT a.attisdropped
            UNION ALL SELECT 'constraint:' || conname || ':' || pg_get_constraintdef(oid) || ':' || convalidated || ':' || conenforced
            FROM pg_constraint WHERE conrelid = '"Resources_ProjectResources"'::regclass
            UNION ALL SELECT 'index:' || pg_get_indexdef(indexrelid) || ':' || indisvalid || ':' || indisready
            FROM pg_index WHERE indrelid = '"Resources_ProjectResources"'::regclass
            UNION ALL SELECT 'trigger:' || pg_get_triggerdef(oid) FROM pg_trigger
            WHERE tgrelid = '"Resources_ProjectResources"'::regclass AND NOT tgisinternal
            UNION ALL SELECT 'relation:' || relkind::text || ':' || relrowsecurity || ':' || relforcerowsecurity FROM pg_class
            WHERE oid = '"Resources_ProjectResources"'::regclass
        ) observed ORDER BY value COLLATE "C"
        """).ToArrayAsync();

    private static Task SetDriftAsync(AppDbContext context, Drift drift, bool enabled) {
        var sql = drift switch {
            Drift.PartialRevision => enabled ? "ALTER TABLE \"Resources_ProjectResources\" ADD COLUMN \"Revision\" uuid" :
                "ALTER TABLE \"Resources_ProjectResources\" DROP COLUMN \"Revision\"",
            Drift.ColumnShape => enabled ? "ALTER TABLE \"Resources_ProjectResources\" ALTER COLUMN \"Name\" TYPE varchar(201)" :
                "ALTER TABLE \"Resources_ProjectResources\" ALTER COLUMN \"Name\" TYPE varchar(200)",
            Drift.MissingPrimaryKey => enabled ? "ALTER TABLE \"Resources_ProjectResources\" DROP CONSTRAINT \"PK_Resources_ProjectResources\"" :
                "ALTER TABLE \"Resources_ProjectResources\" ADD CONSTRAINT \"PK_Resources_ProjectResources\" PRIMARY KEY (\"Id\")",
            Drift.RowSecurity => enabled ? "ALTER TABLE \"Resources_ProjectResources\" ENABLE ROW LEVEL SECURITY" :
                "ALTER TABLE \"Resources_ProjectResources\" DISABLE ROW LEVEL SECURITY",
            Drift.UserTrigger => enabled ? """
                CREATE FUNCTION resource_migration_probe() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NEW; END $$;
                CREATE TRIGGER resource_migration_probe BEFORE UPDATE ON "Resources_ProjectResources"
                FOR EACH ROW EXECUTE FUNCTION resource_migration_probe();
                """ : """
                DROP TRIGGER resource_migration_probe ON "Resources_ProjectResources";
                DROP FUNCTION resource_migration_probe();
                """,
            _ => throw new ArgumentOutOfRangeException(nameof(drift))
        };
        return context.Database.ExecuteSqlRawAsync(sql);
    }

    private enum Drift { PartialRevision, ColumnShape, MissingPrimaryKey, RowSecurity, UserTrigger }
    private sealed record RetainedRow(Guid Id, Guid ProjectId, Guid? LifetimeId, string Connector, string Configuration, string SecretReferences);
    private sealed record RevisionObservation(Guid Id, Guid Revision);
    private sealed record ColumnObservation(string Name, string Type, bool NotNull, string? DefaultSql);
    private sealed class IndexObservation {
        public string[] Columns { get; set; } = [];
        public bool Unique { get; set; }
        public bool Valid { get; set; }
        public bool Ready { get; set; }
        public bool Plain { get; set; }
        public string Method { get; set; } = string.Empty;
    }

    private sealed class Fixture(PostgresTestDatabaseLease lease) : IAsyncDisposable {
        internal AppDbContext Canonical() => new(lease.CreateAppDbContextOptions());
        internal ResourcesDbContext Owner() => new(new DbContextOptionsBuilder<ResourcesDbContext>().UseNpgsql(lease.ConnectionString).Options);

        internal async Task RequireWaitAsync(int waitingPid, int holderPid) {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await using var observer = new NpgsqlConnection(lease.ConnectionString);
            await observer.OpenAsync(deadline.Token);
            while (true) {
                await using var command = new NpgsqlCommand("SELECT @holder = ANY(pg_blocking_pids(@waiting))", observer);
                command.Parameters.AddWithValue("holder", holderPid);
                command.Parameters.AddWithValue("waiting", waitingPid);
                if (await command.ExecuteScalarAsync(deadline.Token) is true) {
                    return;
                }
                await Task.Delay(TimeSpan.FromMilliseconds(10), deadline.Token);
            }
        }

        internal static async Task<Fixture> CreateAsync(bool previous = true) {
            AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
            var lease = PostgresTestDatabaseLease.Create("resource-metadata-migration");
            try {
                var fixture = new Fixture(lease);
                if (previous) {
                    await using var context = fixture.Canonical();
                    await context.GetService<IMigrator>().MigrateAsync(PreviousMigration);
                }
                return fixture;
            } catch (Exception failure) {
                await TestFixtureCleanup.DisposeAfterFailureAsync(failure, lease);
                throw;
            }
        }

        public ValueTask DisposeAsync() => lease.DisposeAsync();
    }
}
