using System.Data.Common;
using System.Text.Json;
using System.Text.RegularExpressions;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Plugins;
using CanDoItAll.Modules.SchedulerPlanner;
using CanDoItAll.Plugins.Abstractions;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Integration.Persistence;

public sealed partial class MigrationAuthorityIntegrationTests {
    private const string StartingMigration = "20260830104752_AddProviderHistoryExternalReference";
    private const string ApplicationName = "CanDoItAll.Tests.Integration";
    private const string LegacyGrantIndex = "IX_Plugins_CapabilityGrants_PluginId_Capability_RecipeId_ScopeK";
    private static readonly DateTimeOffset SavedAt = new(2026, 9, 10, 12, 0, 0, TimeSpan.Zero);
    private static readonly string[] TableNames = [
        "Plugins_CapabilityGrants", "Plugins_Connections", "Plugins_Installations", "Plugins_Logs",
        "Plugins_OAuthConnections", "Plugins_OAuthSessions", "SchedulerPlanner_Plans", "SchedulerPlanner_Runs"
    ];

    [Fact]
    public async Task Fresh_bootstrap_creates_exact_plugin_and_scheduler_schema_from_the_complete_model() {
        await using var environment = CanDoItAllTestEnvironment.Create("migration-authority-fresh");
        var profile = environment.CreatePostgreSqlProfile("fresh");
        await using var provider = await TestApplicationBootstrap.BuildServiceProviderAsync(
            profile, ApplicationName, TestSchemaBootstrapModules.Full);
        await using var context = await provider.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();

        await AssertCurrentMigrationsAsync(context);
        var expected = ReadExpectedSchema(context);
        var actual = await ReadSchemaAsync(context);
        AssertSchemaEqual(expected, actual);
        Assert.Equal(TableNames, actual.Columns.Select(column => column.Table).Distinct().Order(StringComparer.Ordinal));
        Assert.Single(actual.Indexes, index => index.Table == "Plugins_CapabilityGrants" && !index.Primary && index.Unique);
        Assert.DoesNotContain(actual.Indexes, index => index.Name == LegacyGrantIndex);
    }

    [Fact]
    public async Task Repeated_bootstrap_issues_no_owner_ddl_and_only_seeds_missing_crm_defaults() {
        await using var environment = CanDoItAllTestEnvironment.Create("migration-authority-repeat");
        var profile = environment.CreatePostgreSqlProfile("repeat");
        var observer = new BootstrapCommandObserver();
        var observedFactory = new ObservedProfileFactory(observer);
        await using var provider = await TestApplicationBootstrap.BuildServiceProviderAsync(
            profile, ApplicationName, TestSchemaBootstrapModules.Full, configureServices: services => {
                services.RemoveAll<IProfileAppDbContextFactory>();
                services.AddSingleton<IProfileAppDbContextFactory>(observedFactory);
            });
        Assert.NotEmpty(observer.OwnerDdl);
        var canonical = provider.GetRequiredService<ICanonicalRuntimeDatabase>().Profile;
        Assert.All(observedFactory.Profiles, observed => Assert.Same(canonical, observed));
        var contextFactory = provider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        CrmHrLookupOption edited;
        CrmHrLookupOption[] removed;
        CrmHrLookupOption[] retained;
        await using (var context = await contextFactory.CreateDbContextAsync()) {
            var defaults = await context.Set<CrmHrLookupOption>().ToListAsync();
            Assert.Equal(15, defaults.Count);
            edited = Assert.Single(defaults, option => option.CatalogKind == LookupCatalogKind.OpportunityStage && option.Key == "Identified");
            edited.Key = "IDENTIFIED";
            edited.DisplayName = "Operator-edited stage";
            edited.DisplayOrder = 777;
            edited.IsSystemDefault = false;
            edited.UpdatedAtUtc = SavedAt;
            removed = [
                Assert.Single(defaults, option => option.CatalogKind == LookupCatalogKind.OpportunityStage && option.Key == "Proposal"),
                Assert.Single(defaults, option => option.CatalogKind == LookupCatalogKind.RelationshipStage && option.Key == "Dormant"),
                Assert.Single(defaults, option => option.CatalogKind == LookupCatalogKind.AssignmentKind && option.Key == nameof(ProjectPartyAssignmentKind.DeliveryUnit))
            ];
            retained = defaults.Except(removed).ToArray();
            context.RemoveRange(removed);
            await context.SaveChangesAsync();
        }

        observer.Reset();
        observedFactory.Profiles.Clear();
        var bootstrapper = provider.GetRequiredService<IAppDatabaseBootstrapper>();
        await bootstrapper.EnsureCurrentProfileReadyAsync();
        string firstReadback;
        await using (var context = await contextFactory.CreateDbContextAsync()) {
            var restored = await context.Set<CrmHrLookupOption>().AsNoTracking().OrderBy(option => option.Id).ToListAsync();
            Assert.Equal(15, restored.Count);
            foreach (var existing in retained) {
                Assert.Equal(JsonSerializer.Serialize(existing), JsonSerializer.Serialize(Assert.Single(restored, option => option.Id == existing.Id)));
            }

            Assert.Single(restored, option => option.CatalogKind == edited.CatalogKind && string.Equals(option.Key, edited.Key, StringComparison.OrdinalIgnoreCase));
            foreach (var missing in removed) {
                var seeded = Assert.Single(restored, option => option.CatalogKind == missing.CatalogKind && option.Key == missing.Key);
                Assert.NotEqual(missing.Id, seeded.Id);
                Assert.Equal(missing.DisplayName, seeded.DisplayName);
                Assert.Equal(missing.DisplayOrder, seeded.DisplayOrder);
                Assert.True(seeded.IsSystemDefault);
            }

            firstReadback = JsonSerializer.Serialize(restored);
        }

        await bootstrapper.EnsureCurrentProfileReadyAsync();
        Assert.Equal(2, observedFactory.Profiles.Count);
        Assert.All(observedFactory.Profiles, observed => Assert.Same(canonical, observed));
        Assert.True(observer.CommandCount > 0);
        Assert.Equal(0, observer.CrmLookupReads);
        Assert.Empty(observer.OwnerDdl);
        await using var finalContext = await contextFactory.CreateDbContextAsync();
        Assert.Equal(firstReadback, JsonSerializer.Serialize(await finalContext.Set<CrmHrLookupOption>()
            .AsNoTracking().OrderBy(option => option.Id).ToListAsync()));
        AssertSchemaEqual(ReadExpectedSchema(finalContext), await ReadSchemaAsync(finalContext));
    }

    [Fact]
    public async Task Populated_task_start_schema_upgrades_and_restarts_without_rewriting_records_or_legacy_schema_extras() {
        await using var environment = CanDoItAllTestEnvironment.Create("migration-authority-upgrade");
        var profile = environment.CreatePostgreSqlProfile("upgrade");
        var services = new ServiceCollection();
        TestApplicationBootstrap.ConfigureDefaultServices(services, TestApplicationBootstrap.BuildConfiguration(profile),
            new TestHostEnvironment(profile.EnvironmentRootPath, ApplicationName));
        FixtureIds ids;
        RetainedGraph graph;
        string[] savedGraphRows;
        string[] savedRecords;
        SchemaSnapshot legacySchema;
        await using (var original = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true })) {
            await using var context = await original.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
            await context.Database.GetService<IMigrator>().MigrateAsync(StartingMigration);
            var applied = (await context.Database.GetAppliedMigrationsAsync()).ToArray();
            Assert.Equal(StartingMigration, applied[^1]);
            Assert.Equal(context.Database.GetMigrations().Take(applied.Length), applied);
            await using var seedScope = original.CreateAsyncScope();
            graph = await SeedRetainedGraphAsync(seedScope.ServiceProvider, context, profile);
            ids = await SeedRecordsAsync(context, graph);
            savedGraphRows = await ReadRetainedGraphRowsAsync(context, graph);
            Assert.Equal(13, savedGraphRows.Length);
            await context.Database.ExecuteSqlRawAsync("""
                CREATE UNIQUE INDEX "IX_Plugins_CapabilityGrants_PluginId_Capability_RecipeId_ScopeK"
                    ON "Plugins_CapabilityGrants" ("PluginId", "Capability", "RecipeId", "ScopeKind", "ScopeKey");
                ALTER TABLE "SchedulerPlanner_Runs" ALTER COLUMN "Route" SET DEFAULT '';
                ALTER TABLE "SchedulerPlanner_Runs" ALTER COLUMN "RetryCategory" SET DEFAULT 0;
                """);
            savedRecords = await ReadRecordsAsync(context, ids);
            legacySchema = await ReadSchemaAsync(context);
            Assert.Contains(legacySchema.Indexes, index => index.Name == LegacyGrantIndex);
            Assert.NotNull(Assert.Single(legacySchema.Columns, column => column.Table == "SchedulerPlanner_Runs" && column.Name == "Route").DefaultSql);
            Assert.Equal("0", Assert.Single(legacySchema.Columns, column => column.Table == "SchedulerPlanner_Runs" && column.Name == "RetryCategory").DefaultSql);
        }

        Guid? projectLifetime = null;
        for (var restart = 0; restart < 2; restart++) {
            await using var provider = await TestApplicationBootstrap.BuildServiceProviderAsync(
                profile, ApplicationName, TestSchemaBootstrapModules.Full);
            await using var context = await provider.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
            await AssertCurrentMigrationsAsync(context);
            Assert.Equal(savedRecords, await ReadRecordsAsync(context, ids));
            AssertRetainedSchema(legacySchema, await ReadSchemaAsync(context));
            Assert.Equal(savedGraphRows, await ReadRetainedGraphRowsAsync(context, graph));
            projectLifetime = await AssertRetainedGraphAsync(provider, profile, graph, projectLifetime);
        }
    }

    private static SchemaSnapshot ReadExpectedSchema(AppDbContext context) {
        var tables = context.GetService<IDesignTimeModel>().Model.GetRelationalModel().Tables
            .Where(table => TableNames.Contains(table.Name, StringComparer.Ordinal)).OrderBy(table => table.Name, StringComparer.Ordinal).ToArray();
        Assert.Equal(TableNames, tables.Select(table => table.Name));
        var columns = tables.SelectMany(table => table.Columns.Select(column => {
            Assert.False(column.TryGetDefaultValue(out _));
            Assert.Null(column.DefaultValueSql);
            Assert.Null(column.ComputedColumnSql);
            return new ColumnDefinition(table.Name, column.Name, column.StoreType.ToLowerInvariant(), column.IsNullable, null, string.Empty, string.Empty);
        })).OrderBy(column => column.Table, StringComparer.Ordinal).ThenBy(column => column.Name, StringComparer.Ordinal).ToArray();
        var indexes = tables.SelectMany(table => table.UniqueConstraints.Select(key =>
                new IndexDefinition(table.Name, key.Name, key == table.PrimaryKey, true, "btree", JoinColumns(key.Columns),
                    SortOptions(key.Columns.Count), false, null, null, true, true))
            .Concat(table.Indexes.Select(index => {
                Assert.False(index.IsDescending?.Any(descending => descending) ?? false);
                return new IndexDefinition(table.Name, index.Name, false, index.IsUnique, "btree", JoinColumns(index.Columns),
                    SortOptions(index.Columns.Count), false, index.Filter, null, true, true);
            }))).OrderBy(index => index.Table, StringComparer.Ordinal).ThenBy(index => index.Name, StringComparer.Ordinal).ToArray();
        var foreignKeys = tables.SelectMany(table => table.ForeignKeyConstraints.Select(key =>
                new ForeignKeyDefinition(table.Name, key.Name, JoinColumns(key.Columns), key.PrincipalTable.Schema, key.PrincipalTable.Name,
                    JoinColumns(key.PrincipalColumns), DeleteAction(key.OnDeleteAction), "a", "s", false, false, true)))
            .OrderBy(key => key.Table, StringComparer.Ordinal).ThenBy(key => key.Name, StringComparer.Ordinal).ToArray();
        return new(columns, indexes, foreignKeys);

        static string JoinColumns(IEnumerable<IColumn> columns) => string.Join(",", columns.Select(column => column.Name));
        static string SortOptions(int count) => string.Join(" ", Enumerable.Repeat("0", count));
        static string DeleteAction(ReferentialAction action) => action switch {
            ReferentialAction.NoAction => "a",
            ReferentialAction.Restrict => "r",
            ReferentialAction.Cascade => "c",
            ReferentialAction.SetNull => "n",
            ReferentialAction.SetDefault => "d",
            _ => throw new ArgumentOutOfRangeException(nameof(action))
        };
    }

    private static async Task<SchemaSnapshot> ReadSchemaAsync(AppDbContext context) {
        var columns = await ReadCatalogAsync(context, """
            SELECT t.relname, a.attname, format_type(a.atttypid, a.atttypmod), NOT a.attnotnull,
                   pg_get_expr(d.adbin, d.adrelid), a.attidentity::text, a.attgenerated::text
            FROM pg_class t
            JOIN pg_namespace n ON n.oid = t.relnamespace
            JOIN pg_attribute a ON a.attrelid = t.oid AND a.attnum > 0 AND NOT a.attisdropped
            LEFT JOIN pg_attrdef d ON d.adrelid = t.oid AND d.adnum = a.attnum
            WHERE n.nspname = current_schema() AND t.relname = ANY (@tables) AND t.relkind = 'r'
            ORDER BY t.relname COLLATE "C", a.attname COLLATE "C";
            """, reader => new ColumnDefinition(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetBoolean(3),
                NullableString(reader, 4), reader.GetString(5), reader.GetString(6)));
        var indexes = await ReadCatalogAsync(context, """
            SELECT t.relname, i.relname, x.indisprimary, x.indisunique, m.amname,
                   array_to_string(ARRAY(SELECT a.attname::text FROM unnest(x.indkey) WITH ORDINALITY k(attnum, position)
                       LEFT JOIN pg_attribute a ON a.attrelid = t.oid AND a.attnum = k.attnum
                       WHERE k.position <= x.indnkeyatts ORDER BY k.position), ','),
                   x.indoption::text, x.indnatts <> x.indnkeyatts, pg_get_expr(x.indpred, x.indrelid),
                   pg_get_expr(x.indexprs, x.indrelid), x.indisvalid, x.indisready
            FROM pg_index x
            JOIN pg_class t ON t.oid = x.indrelid
            JOIN pg_namespace n ON n.oid = t.relnamespace
            JOIN pg_class i ON i.oid = x.indexrelid
            JOIN pg_am m ON m.oid = i.relam
            WHERE n.nspname = current_schema() AND t.relname = ANY (@tables)
            ORDER BY t.relname COLLATE "C", i.relname COLLATE "C";
            """, reader => new IndexDefinition(reader.GetString(0), reader.GetString(1), reader.GetBoolean(2), reader.GetBoolean(3),
                reader.GetString(4), reader.GetString(5), reader.GetString(6), reader.GetBoolean(7), NullableString(reader, 8),
                NullableString(reader, 9), reader.GetBoolean(10), reader.GetBoolean(11)));
        var foreignKeys = await ReadCatalogAsync(context, """
            SELECT t.relname, c.conname,
                   array_to_string(ARRAY(SELECT a.attname::text FROM unnest(c.conkey) WITH ORDINALITY k(attnum, position)
                       JOIN pg_attribute a ON a.attrelid = t.oid AND a.attnum = k.attnum ORDER BY k.position), ','),
                   CASE WHEN pn.nspname = current_schema() THEN NULL ELSE pn.nspname::text END, p.relname,
                   array_to_string(ARRAY(SELECT a.attname::text FROM unnest(c.confkey) WITH ORDINALITY k(attnum, position)
                       JOIN pg_attribute a ON a.attrelid = p.oid AND a.attnum = k.attnum ORDER BY k.position), ','),
                   c.confdeltype::text, c.confupdtype::text, c.confmatchtype::text, c.condeferrable, c.condeferred, c.convalidated
            FROM pg_constraint c
            JOIN pg_class t ON t.oid = c.conrelid
            JOIN pg_namespace n ON n.oid = t.relnamespace
            JOIN pg_class p ON p.oid = c.confrelid
            JOIN pg_namespace pn ON pn.oid = p.relnamespace
            WHERE n.nspname = current_schema() AND t.relname = ANY (@tables) AND c.contype = 'f'
            ORDER BY t.relname COLLATE "C", c.conname COLLATE "C";
            """, reader => new ForeignKeyDefinition(reader.GetString(0), reader.GetString(1), reader.GetString(2), NullableString(reader, 3),
                reader.GetString(4), reader.GetString(5), reader.GetString(6), reader.GetString(7), reader.GetString(8), reader.GetBoolean(9),
                reader.GetBoolean(10), reader.GetBoolean(11)));
        return new(columns, indexes, foreignKeys);
    }

    private static async Task<T[]> ReadCatalogAsync<T>(AppDbContext context, string sql, Func<DbDataReader, T> read) {
        if (context.Database.GetDbConnection().State != System.Data.ConnectionState.Open) {
            await context.Database.OpenConnectionAsync();
        }
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        var parameter = command.CreateParameter();
        parameter.ParameterName = "tables";
        parameter.Value = TableNames;
        command.Parameters.Add(parameter);
        var rows = new List<T>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) {
            rows.Add(read(reader));
        }

        return rows.ToArray();
    }

    private static string? NullableString(DbDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    private static void AssertSchemaEqual(SchemaSnapshot expected, SchemaSnapshot actual) {
        Assert.Equal(expected.Columns, actual.Columns);
        Assert.Equal(expected.Indexes, actual.Indexes);
        Assert.Equal(expected.ForeignKeys, actual.ForeignKeys);
    }

    private static void AssertRetainedSchema(SchemaSnapshot legacy, SchemaSnapshot current) {
        var addition = Assert.Single(current.Columns.Where(column => !legacy.Columns.Contains(column)));
        Assert.Equal(new ColumnDefinition("SchedulerPlanner_Plans", "StructureAuthorityJson", "text", true, null, string.Empty, string.Empty), addition);
        Assert.Equal(legacy.Columns, current.Columns.Where(column => column != addition));
        Assert.Equal(legacy.Indexes, current.Indexes);
        Assert.Equal(legacy.ForeignKeys, current.ForeignKeys);
    }

    private static async Task AssertCurrentMigrationsAsync(AppDbContext context) {
        var known = context.Database.GetMigrations().ToArray();
        Assert.NotEmpty(known);
        Assert.Equal(PostgreSqlMigrationBaseline.CurrentMigrationId, known[0]);
        Assert.Equal(known, await context.Database.GetAppliedMigrationsAsync());
        Assert.False(context.Database.HasPendingModelChanges());
        Assert.Equal(161, context.Model.GetEntityTypes().Count());
    }

    private static async Task<FixtureIds> SeedRecordsAsync(AppDbContext context, RetainedGraph graph) {
        const string pluginId = "fixture.migration-authority";
        var installation = new PluginInstallationRecord {
            PluginId = pluginId, PackageId = "fixture.package", DisplayNameSnapshot = "Stored installation", Version = "1.2.3",
            Vendor = "Fixture vendor", ManifestSnapshotJson = "{\"fixture\":true}", IsEnabled = false,
            InstalledBy = "fixture-operator", InstalledAtUtc = SavedAt, UpdatedAtUtc = SavedAt, ConcurrencyToken = Guid.NewGuid()
        };
        var grant = new PluginCapabilityGrantRecord {
            PluginId = pluginId, Capability = (int)PluginCapabilityKind.WorkflowExecutor, RecipeId = "fixture.recipe",
            ScopeKind = nameof(PluginGrantScopeKind.Workflow), ScopeKey = "fixture-workflow",
            State = nameof(PluginGrantState.Granted), RiskKind = nameof(PluginGrantRiskKind.Low), Reason = "Stored approval", UpdatedBy = "fixture-operator",
            CreatedAtUtc = SavedAt, UpdatedAtUtc = SavedAt, ConcurrencyToken = Guid.NewGuid()
        };
        var connection = new PluginConnectionRecord {
            PluginId = pluginId, ConnectionKey = "fixture-connection", DisplayName = "Stored connection", SettingsJson = "{\"region\":\"fixture\"}",
            IsEnabled = false, HealthStatus = "Stored status", UpdatedBy = "fixture-operator",
            CreatedAtUtc = SavedAt, UpdatedAtUtc = SavedAt, ConcurrencyToken = Guid.NewGuid()
        };
        var oauthConnection = new PluginOAuthConnectionRecord {
            ConnectionId = connection.Id, PluginId = pluginId, ConnectionKey = connection.ConnectionKey, ProviderKey = "fixture-provider",
            TokenVaultKey = "fixture-reference-without-token", AccountDisplay = "Fixture account", GrantedScopesJson = "[\"fixture.read\"]",
            AccessTokenExpiresAtUtc = SavedAt.AddHours(1), RefreshTokenExpiresAtUtc = SavedAt.AddDays(1),
            CreatedAtUtc = SavedAt, UpdatedAtUtc = SavedAt, ConcurrencyToken = Guid.NewGuid()
        };
        var oauthSession = new PluginOAuthSessionRecord {
            StateHash = "fixture-state-hash", PluginId = pluginId, ConnectionId = connection.Id, ConnectionKey = connection.ConnectionKey,
            ProviderKey = "fixture-provider", CodeVerifierVaultKey = "fixture-reference-without-verifier", RedirectUri = "https://example.test/oauth/callback",
            ReturnPath = "/plugins", RequestedScopesJson = "[\"fixture.read\"]", CreatedAtUtc = SavedAt, ExpiresAtUtc = SavedAt.AddMinutes(5),
            CompletedAtUtc = SavedAt.AddMinutes(1), Status = "Completed", ConcurrencyToken = Guid.NewGuid()
        };
        var log = new PluginLogRecord {
            PluginId = pluginId, PackageId = installation.PackageId, WorkflowExecutorId = "fixture.executor", StreamKind = nameof(PluginLogStreamKind.Runtime),
            OperationKind = nameof(PluginLogOperationKind.PluginEvent), Severity = nameof(PluginLogSeverity.Information), Status = "Completed", Message = "Stored fixture log",
            DetailsJson = "{\"count\":2}", CorrelationId = "fixture-correlation", CreatedAtUtc = SavedAt, ConcurrencyToken = Guid.NewGuid()
        };
        var plan = new SchedulerPlan {
            Name = "Stored plan", Description = "Preserve migration fixture", TargetKind = SchedulerPlanTargetKind.Workflow,
            TargetId = graph.WorkflowId, TargetVersionId = graph.WorkflowVersionId, TargetNameSnapshot = "Fixture workflow",
            CronExpression = "0 0 12 * * ?", CronDescription = "Daily fixture", TimeZoneId = "UTC", MisfirePolicy = SchedulerPlanMisfirePolicy.DoNothing,
            IsEnabled = false, StartAtUtc = SavedAt, EndAtUtc = SavedAt.AddDays(2), InputJson = "{\"fixture\":true}",
            SchedulerTriggerId = Guid.NewGuid(), SchedulerTriggerKey = "fixture-trigger", NextPlannedFireAtUtc = SavedAt.AddDays(1),
            LastFiredAtUtc = SavedAt, LastError = "Stored fixture error", CreatedAtUtc = SavedAt, UpdatedAtUtc = SavedAt
        };
        var run = new SchedulerPlanRun {
            PlanId = plan.Id, DedupeKey = "fixture-run-dedupe", SchedulerFireId = Guid.NewGuid(), CorrelationId = Guid.NewGuid(), FiredAtUtc = SavedAt,
            Status = SchedulerPlanRunDispatchStatus.WaitingForApproval, AttemptCount = 2, TargetRunId = graph.WorkflowRunId, TargetRunKind = "Workflow",
            Summary = "Stored summary", ErrorMessage = "Stored dispatch message", Route = SchedulerPlanRunRoutes.WaitingForApproval,
            RetryCategory = SchedulerPlanRunRetryCategory.WorkflowWaitingForApproval, DispatchedAtUtc = SavedAt.AddSeconds(1),
            CreatedAtUtc = SavedAt, UpdatedAtUtc = SavedAt.AddSeconds(1)
        };
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "SchedulerPlanner_Plans" ("Id", "Name", "Description", "TargetKind", "TargetId", "TargetVersionId",
                "TargetNameSnapshot", "CronExpression", "CronDescription", "TimeZoneId", "MisfirePolicy", "IsEnabled", "StartAtUtc", "EndAtUtc",
                "InputJson", "AutomationTriggerId", "AutomationTriggerKey", "NextPlannedFireAtUtc", "LastFiredAtUtc", "LastError", "CreatedAtUtc", "UpdatedAtUtc")
            VALUES ({plan.Id}, {plan.Name}, {plan.Description}, {(int)plan.TargetKind}, {plan.TargetId}, {plan.TargetVersionId},
                {plan.TargetNameSnapshot}, {plan.CronExpression}, {plan.CronDescription}, {plan.TimeZoneId}, {(int)plan.MisfirePolicy},
                {plan.IsEnabled}, {plan.StartAtUtc}, {plan.EndAtUtc}, {plan.InputJson}, {plan.SchedulerTriggerId}, {plan.SchedulerTriggerKey},
                {plan.NextPlannedFireAtUtc}, {plan.LastFiredAtUtc}, {plan.LastError}, {plan.CreatedAtUtc}, {plan.UpdatedAtUtc});
            """);
        context.AddRange(installation, grant, connection, oauthConnection, oauthSession, log, run);
        await context.SaveChangesAsync();
        return new(installation.Id, grant.Id, connection.Id, oauthConnection.Id, oauthSession.Id, log.Id, plan.Id, run.Id);
    }

    private static async Task<string[]> ReadRecordsAsync(AppDbContext context, FixtureIds ids) => [
        JsonSerializer.Serialize(await context.Set<PluginInstallationRecord>().AsNoTracking().SingleAsync(row => row.Id == ids.Installation)),
        JsonSerializer.Serialize(await context.Set<PluginCapabilityGrantRecord>().AsNoTracking().SingleAsync(row => row.Id == ids.Grant)),
        JsonSerializer.Serialize(await context.Set<PluginConnectionRecord>().AsNoTracking().SingleAsync(row => row.Id == ids.Connection)),
        JsonSerializer.Serialize(await context.Set<PluginOAuthConnectionRecord>().AsNoTracking().SingleAsync(row => row.Id == ids.OAuthConnection)),
        JsonSerializer.Serialize(await context.Set<PluginOAuthSessionRecord>().AsNoTracking().SingleAsync(row => row.Id == ids.OAuthSession)),
        JsonSerializer.Serialize(await context.Set<PluginLogRecord>().AsNoTracking().SingleAsync(row => row.Id == ids.Log)),
        await context.Database.SqlQuery<string>($"""
            SELECT (to_jsonb(row) - 'StructureAuthorityJson')::text AS "Value"
            FROM "SchedulerPlanner_Plans" row WHERE "Id" = {ids.Plan}
            """).SingleAsync(),
        JsonSerializer.Serialize(await context.Set<SchedulerPlanRun>().AsNoTracking().SingleAsync(row => row.Id == ids.Run))
    ];

    private sealed record FixtureIds(Guid Installation, Guid Grant, Guid Connection, Guid OAuthConnection, Guid OAuthSession, Guid Log, Guid Plan, Guid Run);
    private sealed record SchemaSnapshot(ColumnDefinition[] Columns, IndexDefinition[] Indexes, ForeignKeyDefinition[] ForeignKeys);
    private sealed record ColumnDefinition(string Table, string Name, string StoreType, bool Nullable, string? DefaultSql, string Identity, string Generated);
    private sealed record IndexDefinition(string Table, string Name, bool Primary, bool Unique, string Method, string Columns, string SortOptions,
        bool HasIncludedColumns, string? Filter, string? Expressions, bool Valid, bool Ready);
    private sealed record ForeignKeyDefinition(string Table, string Name, string Columns, string? PrincipalSchema, string PrincipalTable, string PrincipalColumns,
        string DeleteAction, string UpdateAction, string Match, bool Deferrable, bool InitiallyDeferred, bool Validated);

    private sealed class ObservedProfileFactory(BootstrapCommandObserver observer) : IProfileAppDbContextFactory {
        public List<ResolvedDatabaseProfile> Profiles { get; } = [];

        public Task<AppDbContext> CreateDbContextForProfileAsync(ResolvedDatabaseProfile profile, CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            Profiles.Add(profile);
            var options = new DbContextOptionsBuilder<AppDbContext>(AppDbContextOptionsConfigurator.CreateOptions(profile))
                .AddInterceptors(observer).Options;
            return Task.FromResult(new AppDbContext(options));
        }
    }

    private sealed class BootstrapCommandObserver : DbCommandInterceptor {
        public int CommandCount { get; private set; }
        public int CrmLookupReads { get; private set; }
        public List<string> OwnerDdl { get; } = [];

        public void Reset() {
            CommandCount = 0;
            CrmLookupReads = 0;
            OwnerDdl.Clear();
        }

        private void Observe(DbCommand command) {
            CommandCount++;
            if (command.CommandText.Contains("FROM \"CrmHr_LookupOptions\"", StringComparison.Ordinal)) {
                CrmLookupReads++;
            }

            if (!Regex.IsMatch(command.CommandText, @"\b(?:CREATE|ALTER)\s+(?:UNIQUE\s+)?(?:TABLE|INDEX)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)) {
                return;
            }

            foreach (var table in TableNames.Where(table => command.CommandText.Contains(table, StringComparison.Ordinal))) {
                OwnerDdl.Add(table);
            }
        }

        public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<int> result) {
            Observe(command);
            return result;
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default) {
            Observe(command);
            return ValueTask.FromResult(result);
        }

        public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result) {
            Observe(command);
            return result;
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            Observe(command);
            return ValueTask.FromResult(result);
        }

        public override InterceptionResult<object> ScalarExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<object> result) {
            Observe(command);
            return result;
        }

        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<object> result, CancellationToken cancellationToken = default) {
            Observe(command);
            return ValueTask.FromResult(result);
        }
    }
}
