using CanDoItAll.Composition;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit.Abstractions;

namespace CanDoItAll.Tests.Integration;

public sealed class FiniteOperationMigrationTests(ITestOutputHelper output) {
    private const string PreviousMigration = "20261016163615_AddProjectDefinitionOperationLifecycle";
    private const string SubjectMigration = "20261016233100_ExtractFiniteOperationEpochMetadata";
    private static readonly DateTimeOffset SavedAt = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Upgrade_preserves_every_retained_tuple_and_original_receipt_observation() {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
        await using var fixture = new ProjectOperationEpochTests.Fixture(PostgresTestDatabaseLease.Create("finite-metadata-upgrade"));
        await using var context = fixture.Context();
        await context.GetService<IMigrator>().MigrateAsync(PreviousMigration);
        var historical = Guid.NewGuid();
        await SeedMetadataAsync(context, fixture.Binding, historical);
        var project = new Project { Name = "Retained migration project", Slug = Guid.NewGuid().ToString("N"), Objective = "Retained objective" };
        context.Add(project);
        await context.SaveChangesAsync();
        var caller = ProjectMutationCallerScope.LocalWorkspace;
        var operation = new ProjectDefinitionOperation(fixture.Binding.Namespace, new(2), Guid.NewGuid(), caller.Bind(fixture.Binding.Namespace));
        var acknowledgement = new ProjectEditorAcknowledgement(new(new(fixture.Profile.Profile.Id, project.Id, project.LifetimeId),
            project.ConcurrencyToken), project.Name, project.Description, project.Objective, project.Status, project.CurrentPhase, null, [], [], null);
        var accepted = new ProjectDefinitionAccepted<ProjectEditorAcknowledgement>(operation, ProjectDefinitionResultVersion.VersionOne,
            ProjectDefinitionCommandKind.Replace, SavedAt, SavedAt.AddDays(1).AddMilliseconds(17000).AddSeconds(900000), acknowledgement);
        var payload = ProjectDefinitionOperationCodec.EncodeAccepted(accepted, ProjectDefinitionLimits.VersionOneMaximum).ToArray();
        var receipt = new ProjectDefinitionReceiptRecord {
            NamespaceId = operation.Namespace.Value, Epoch = operation.Epoch.Value, CallerKind = caller.Kind, CallerKey = caller.Key.ToArray(),
            OperationId = operation.Id, SchemaVersion = 1, NormalizationVersion = 1, ResultVersion = 1,
            CommandKind = ProjectDefinitionCommandKind.Replace, SemanticDigest = new byte[32], ProjectId = project.Id,
            LifetimeId = project.LifetimeId, AcceptedRevision = project.ConcurrencyToken, AcceptedAtUtc = SavedAt, AcceptedPayload = payload
        };
        context.Add(receipt);
        await context.SaveChangesAsync();
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Projects_DefinitionReceipts"
            SELECT {historical}, 5, "CallerKind", "CallerKey", {Guid.NewGuid()}, "SchemaVersion", "NormalizationVersion", "ResultVersion",
                "CommandKind", "SemanticDigest", "ProjectId", "LifetimeId", "AcceptedRevision", "AcceptedAtUtc", "AcceptedPayload", "CreationFingerprint"
            FROM "Projects_DefinitionReceipts" WHERE "OperationId" = {operation.Id}
            """);
        var before = await ReadRetainedAsync(context, upgraded: false);
        Assert.Equal(10, before.Length);
        string[]? installedWorkbench = null;

        for (var restart = 0; restart < 2; restart++) {
            await using var migrated = fixture.Context();
            var started = System.Diagnostics.Stopwatch.GetTimestamp();
            await migrated.Database.MigrateAsync();
            output.WriteLine("Upgrade iteration {0}: retained states {1}, epochs {2}, receipts {3}; migration {4:F2} ms.",
                restart, before.Count(row => row.StartsWith("state:", StringComparison.Ordinal)),
                before.Count(row => row.StartsWith("epoch:", StringComparison.Ordinal)),
                before.Count(row => row.StartsWith("receipt:", StringComparison.Ordinal)),
                System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            Assert.False(migrated.Database.HasPendingModelChanges());
            Assert.Equal(before, await ReadRetainedAsync(migrated, upgraded: true));
            Assert.All(await migrated.Set<FiniteOperationStateRecord>().AsNoTracking()
                .Where(state => state.PoolId == ProjectFiniteOperationPool.Pool.Value).ToArrayAsync(), state => {
                Assert.Equal(ProjectFiniteOperationPool.Pool.Value, state.PoolId);
                Assert.Equal(1, state.RetentionProtocolVersion);
            });
            Assert.All(await migrated.Set<FiniteOperationEpochRecord>().AsNoTracking()
                .Where(epoch => epoch.PoolId == ProjectFiniteOperationPool.Pool.Value).ToArrayAsync(),
                epoch => Assert.Equal(ProjectFiniteOperationPool.Pool.Value, epoch.PoolId));
            var installed = await AssertWorkbenchInstallationAsync(migrated, fixture.Binding.Namespace.Value);
            if (installedWorkbench is not null) {
                Assert.Equal(installedWorkbench, installed);
            }
            installedWorkbench = installed;
            var observed = await fixture.CreateReceiptReader(null!).ObserveAsync(fixture.Binding, operation,
                new ProjectDefinitionReceiptInvocation(caller, new AllowedReceiptAccess()), CancellationToken.None);
            var found = Assert.IsType<ProjectDefinitionLookup.FoundFull>(observed.Lookup);
            Assert.Equal(payload, ProjectDefinitionOperationCodec.EncodeAccepted(found.Result, ProjectDefinitionLimits.VersionOneMaximum).ToArray());
            await AssertNeutralCatalogAsync(migrated);
        }
    }

    [Fact]
    public async Task Inconsistent_history_refuses_before_cutover_and_leaves_historical_tables_and_receipts_intact() {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
        foreach (var corruption in Enum.GetValues<HistoryCorruption>()) {
            await using var fixture = new ProjectOperationEpochTests.Fixture(PostgresTestDatabaseLease.Create("finite-metadata-preflight"));
            await using var context = fixture.Context();
            await context.GetService<IMigrator>().MigrateAsync(PreviousMigration);
            var historical = Guid.NewGuid();
            await SeedMetadataAsync(context, fixture.Binding, historical);
            var sql = corruption switch {
                HistoryCorruption.OrphanEpoch => "DELETE FROM \"Projects_OperationState\" WHERE \"CurrentEpoch\" = 8",
                HistoryCorruption.MissingCurrent => "DELETE FROM \"Projects_OperationEpochs\" WHERE \"State\" = 1",
                HistoryCorruption.MissingUnretired => "DELETE FROM \"Projects_OperationEpochs\" WHERE \"Epoch\" = 3",
                HistoryCorruption.HistoricalOpen => "UPDATE \"Projects_OperationEpochs\" SET \"State\" = 1, \"ClosedAtUtc\" = NULL WHERE \"Epoch\" = 5",
                HistoryCorruption.InactiveLifecycle => "UPDATE \"Infrastructure_DatasetLifecycle\" SET \"State\" = 2",
                HistoryCorruption.OrphanReceipt => """
                    INSERT INTO "Projects_DefinitionReceipts" VALUES
                    (gen_random_uuid(), 1, 3, decode(repeat('00', 32), 'hex'), gen_random_uuid(), 1, 1, 1, 2,
                        decode(repeat('00', 32), 'hex'), gen_random_uuid(), gen_random_uuid(), gen_random_uuid(),
                        clock_timestamp(), decode('ab', 'hex'), NULL)
                    """,
                _ => throw new ArgumentOutOfRangeException(nameof(corruption))
            };
            await context.Database.ExecuteSqlRawAsync(sql);
            var before = await ReadRetainedAsync(context, upgraded: false);

            var failure = await Assert.ThrowsAsync<PostgresException>(() => context.Database.MigrateAsync());

            Assert.Equal(PostgresErrorCodes.RaiseException, failure.SqlState);
            Assert.False(failure.IsTransient);
            Assert.Equal(before, await ReadRetainedAsync(context, upgraded: false));
            Assert.Equal(PreviousMigration, (await context.Database.GetAppliedMigrationsAsync()).Last());
            Assert.False(await context.Database.SqlQueryRaw<bool>("SELECT to_regclass('\"Infrastructure_FiniteOperationState\"') IS NOT NULL AS \"Value\"").SingleAsync());
        }
    }

    [Fact]
    public async Task Unexpected_foreign_keys_and_user_triggers_refuse_without_changing_schema() {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
        await using var lease = PostgresTestDatabaseLease.Create("finite-metadata-schema-audit");
        await using var context = new AppDbContext(lease.CreateAppDbContextOptions());
        await context.GetService<IMigrator>().MigrateAsync(PreviousMigration);
        await context.Database.ExecuteSqlRawAsync("""
            CREATE TABLE "FiniteMigrationExternal" ("NamespaceId" uuid PRIMARY KEY);
            CREATE FUNCTION "FiniteMigrationTrigger"() RETURNS trigger LANGUAGE plpgsql AS $body$ BEGIN RETURN NEW; END $body$;
            """);
        foreach (var extension in Enum.GetValues<SchemaExtension>()) {
            var (install, remove) = extension switch {
                SchemaExtension.IncomingForeignKey => (
                    "ALTER TABLE \"FiniteMigrationExternal\" ADD CONSTRAINT \"UnexpectedIncoming\" FOREIGN KEY (\"NamespaceId\") REFERENCES \"Projects_OperationState\" (\"NamespaceId\")",
                    "ALTER TABLE \"FiniteMigrationExternal\" DROP CONSTRAINT \"UnexpectedIncoming\""),
                SchemaExtension.OutgoingForeignKey => (
                    "ALTER TABLE \"Projects_OperationState\" ADD CONSTRAINT \"UnexpectedOutgoing\" FOREIGN KEY (\"NamespaceId\") REFERENCES \"FiniteMigrationExternal\" (\"NamespaceId\")",
                    "ALTER TABLE \"Projects_OperationState\" DROP CONSTRAINT \"UnexpectedOutgoing\""),
                SchemaExtension.UserTrigger => (
                    "CREATE TRIGGER \"UnexpectedTrigger\" BEFORE DELETE ON \"Projects_OperationEpochs\" FOR EACH ROW EXECUTE FUNCTION \"FiniteMigrationTrigger\"()",
                    "DROP TRIGGER \"UnexpectedTrigger\" ON \"Projects_OperationEpochs\""),
                SchemaExtension.WeakenedCheck => (
                    "ALTER TABLE \"Projects_OperationEpochs\" DROP CONSTRAINT \"CK_Projects_OperationEpoch_Number\", ADD CONSTRAINT \"CK_Projects_OperationEpoch_Number\" CHECK (\"Epoch\" >= 0)",
                    "ALTER TABLE \"Projects_OperationEpochs\" DROP CONSTRAINT \"CK_Projects_OperationEpoch_Number\", ADD CONSTRAINT \"CK_Projects_OperationEpoch_Number\" CHECK (\"Epoch\" > 0)"),
                SchemaExtension.MissingOpenIndex => (
                    "DROP INDEX \"IX_Projects_OperationEpochs_NamespaceId\"",
                    "CREATE UNIQUE INDEX \"IX_Projects_OperationEpochs_NamespaceId\" ON \"Projects_OperationEpochs\" (\"NamespaceId\") WHERE \"State\" = 1"),
                SchemaExtension.CounterDefault => (
                    "ALTER TABLE \"Projects_OperationState\" ALTER COLUMN \"CurrentEpoch\" SET DEFAULT 1",
                    "ALTER TABLE \"Projects_OperationState\" ALTER COLUMN \"CurrentEpoch\" DROP DEFAULT"),
                _ => throw new ArgumentOutOfRangeException(nameof(extension))
            };
            await context.Database.ExecuteSqlRawAsync(install);

            var failure = await Assert.ThrowsAsync<PostgresException>(() => context.Database.MigrateAsync());

            Assert.Equal(PostgresErrorCodes.RaiseException, failure.SqlState);
            Assert.False(failure.IsTransient);
            Assert.Equal(PreviousMigration, (await context.Database.GetAppliedMigrationsAsync()).Last());
            await context.Database.ExecuteSqlRawAsync(remove);
        }
        await context.Database.MigrateAsync();
        await AssertNeutralCatalogAsync(context);
    }

    [Fact]
    public async Task Fresh_upgrade_requires_explicit_bootstrap_and_down_refuses_without_data_loss() {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
        await using var fixture = new ProjectOperationEpochTests.Fixture(PostgresTestDatabaseLease.Create("finite-metadata-fresh"));
        await using var context = fixture.Context();
        await context.GetService<IMigrator>().MigrateAsync(SubjectMigration);
        Assert.Empty(await context.Set<FiniteOperationStateRecord>().ToArrayAsync());
        Assert.Empty(await context.Set<FiniteOperationEpochRecord>().ToArrayAsync());
        await fixture.InitializeAsync(fixture.Service);
        await fixture.InitializeAsync(fixture.Service);
        var before = await ReadRetainedAsync(context, upgraded: true);
        Assert.Equal(SubjectMigration, (await context.Database.GetAppliedMigrationsAsync()).Last());

        var failure = await Assert.ThrowsAsync<PostgresException>(() => context.GetService<IMigrator>().MigrateAsync(PreviousMigration));

        Assert.Equal(PostgresErrorCodes.RaiseException, failure.SqlState);
        Assert.False(failure.IsTransient);
        Assert.Equal("Finite-operation metadata downgrade is unsupported. Use an explicitly coordinated stopped-host restore with matching binaries.", failure.MessageText);
        Assert.Equal(SubjectMigration, (await context.Database.GetAppliedMigrationsAsync()).Last());
        Assert.Equal(before, await ReadRetainedAsync(context, upgraded: true));
        Assert.Single(await context.Set<FiniteOperationStateRecord>().ToArrayAsync());
        Assert.Single(await context.Set<FiniteOperationEpochRecord>().ToArrayAsync());
        await AssertNeutralCatalogAsync(context);
        await AssertInvalidMetadataRefusedAsync(context, fixture.Binding);
        await context.Database.MigrateAsync();
        Assert.False(context.Database.HasPendingModelChanges());
        Assert.Equal(before, await ReadRetainedAsync(context, upgraded: true));
        await AssertWorkbenchInstallationAsync(context, fixture.Binding.Namespace.Value);
    }

    private static Task SeedMetadataAsync(AppDbContext context, DatasetActivationBinding binding, Guid historical)
        => context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Infrastructure_DatasetLifecycle" VALUES
                (1, {binding.Namespace.Value}, {binding.MaintenanceId}, {binding.SchemaVersion}, {(int)binding.GuardProtocol}, 1);
            INSERT INTO "Projects_OperationState" VALUES ({binding.Namespace.Value}, 4, 1), ({historical}, 8, 3);
            INSERT INTO "Projects_OperationEpochs" VALUES
                ({binding.Namespace.Value}, 2, 2, {SavedAt}, {SavedAt.AddDays(1)}, {SavedAt.AddDays(1)}, 17000, 900000),
                ({binding.Namespace.Value}, 3, 2, {SavedAt.AddDays(1)}, {SavedAt.AddDays(2)}, {SavedAt.AddDays(2)}, 23000, 800000),
                ({binding.Namespace.Value}, 4, 1, {SavedAt.AddDays(2)}, {SavedAt.AddYears(20)}, NULL, 31000, 700000),
                ({historical}, 2, 2, {SavedAt}, {SavedAt.AddDays(1)}, {SavedAt.AddDays(2)}, 11000, 950000),
                ({historical}, 5, 2, {SavedAt}, {SavedAt.AddDays(1)}, {SavedAt.AddDays(3)}, 19000, 850000);
            """);

    private static Task<string[]> ReadRetainedAsync(AppDbContext context, bool upgraded) {
        var state = upgraded ? "\"Infrastructure_FiniteOperationState\"" : "\"Projects_OperationState\"";
        var epochs = upgraded ? "\"Infrastructure_FiniteOperationEpochs\"" : "\"Projects_OperationEpochs\"";
        var statePool = upgraded ? $"WHERE s.\"PoolId\" = '{ProjectFiniteOperationPool.Pool.Value:D}'::uuid" : "";
        var epochPool = upgraded ? $"WHERE e.\"PoolId\" = '{ProjectFiniteOperationPool.Pool.Value:D}'::uuid" : "";
        var sql = $"""
            SELECT "Value" FROM (
                SELECT 'state:' || (to_jsonb(s) - 'PoolId' - 'RetentionProtocolVersion')::text AS "Value" FROM {state} s {statePool}
                UNION ALL SELECT 'epoch:' || (to_jsonb(e) - 'PoolId')::text FROM {epochs} e {epochPool}
                UNION ALL SELECT 'receipt:' || to_jsonb(r)::text FROM "Projects_DefinitionReceipts" r
                UNION ALL SELECT 'lifecycle:' || to_jsonb(d)::text FROM "Infrastructure_DatasetLifecycle" d
            ) retained ORDER BY "Value"
            """;
        return context.Database.SqlQueryRaw<string>(sql).ToArrayAsync();
    }

    private static async Task<string[]> AssertWorkbenchInstallationAsync(AppDbContext context, Guid activeNamespace) {
        var state = Assert.Single(await context.Set<FiniteOperationStateRecord>().AsNoTracking()
            .Where(row => row.PoolId != ProjectFiniteOperationPool.Pool.Value).ToArrayAsync());
        Assert.Equal(WorkbenchTaskPlanFiniteOperations.Pool.Value, state.PoolId);
        Assert.Equal(activeNamespace, state.NamespaceId);
        Assert.Equal(WorkbenchTaskPlanFiniteOperations.ProtocolVersion, state.RetentionProtocolVersion);
        Assert.Equal(1, state.CurrentEpoch);
        Assert.Equal(0, state.RetiredThroughEpoch);
        var epoch = Assert.Single(await context.Set<FiniteOperationEpochRecord>().AsNoTracking()
            .Where(row => row.PoolId != ProjectFiniteOperationPool.Pool.Value).ToArrayAsync());
        Assert.Equal(state.PoolId, epoch.PoolId);
        Assert.Equal(activeNamespace, epoch.NamespaceId);
        Assert.Equal(1, epoch.Epoch);
        Assert.Equal(FiniteOperationEpochState.Open, epoch.State);
        Assert.Null(epoch.ClosedAtUtc);
        Assert.Equal(TimeSpan.FromDays(1), epoch.AdmissionClosesAtUtc - epoch.OpenedAtUtc);
        Assert.Equal(30000, epoch.MutationDeadlineMilliseconds);
        Assert.Equal(604800, epoch.ReplayRetentionSeconds);
        return await context.Database.SqlQuery<string>($"""
            SELECT 'state:' || to_jsonb(s)::text AS "Value" FROM "Infrastructure_FiniteOperationState" s WHERE "PoolId" = {state.PoolId}
            UNION ALL SELECT 'epoch:' || to_jsonb(e)::text FROM "Infrastructure_FiniteOperationEpochs" e WHERE "PoolId" = {state.PoolId}
            ORDER BY "Value"
            """).ToArrayAsync();
    }

    private static async Task AssertNeutralCatalogAsync(AppDbContext context) {
        var unexpected = await context.Database.SqlQueryRaw<int>("""
            SELECT (
                (SELECT count(*) FROM pg_constraint WHERE contype = 'f' AND
                    (conrelid IN ('"Infrastructure_FiniteOperationState"'::regclass, '"Infrastructure_FiniteOperationEpochs"'::regclass)
                    OR confrelid IN ('"Infrastructure_FiniteOperationState"'::regclass, '"Infrastructure_FiniteOperationEpochs"'::regclass))) +
                (SELECT count(*) FROM pg_trigger WHERE NOT tgisinternal AND
                    tgrelid IN ('"Infrastructure_FiniteOperationState"'::regclass, '"Infrastructure_FiniteOperationEpochs"'::regclass)) +
                (SELECT count(*) FROM pg_attribute WHERE atthasdef AND attname IN ('PoolId', 'RetentionProtocolVersion') AND
                    attrelid IN ('"Infrastructure_FiniteOperationState"'::regclass, '"Infrastructure_FiniteOperationEpochs"'::regclass))
            )::integer AS "Value"
            """).SingleAsync();
        Assert.Equal(0, unexpected);
        Assert.False(await context.Database.SqlQueryRaw<bool>("SELECT to_regclass('\"Projects_OperationState\"') IS NOT NULL OR to_regclass('\"Projects_OperationEpochs\"') IS NOT NULL AS \"Value\"").SingleAsync());
        var keys = await context.Database.SqlQueryRaw<string>("""
            SELECT conname || ':' || pg_get_constraintdef(oid) AS "Value" FROM pg_constraint
            WHERE contype = 'p' AND conrelid IN
                ('"Infrastructure_FiniteOperationState"'::regclass, '"Infrastructure_FiniteOperationEpochs"'::regclass)
            ORDER BY conname
            """).ToArrayAsync();
        Assert.Equal([
            "PK_Infrastructure_FiniteOperationEpochs:PRIMARY KEY (\"NamespaceId\", \"PoolId\", \"Epoch\")",
            "PK_Infrastructure_FiniteOperationState:PRIMARY KEY (\"NamespaceId\", \"PoolId\")"
        ], keys);
        var checks = await context.Database.SqlQueryRaw<string>("""
            SELECT conname::text AS "Value" FROM pg_constraint WHERE contype = 'c' AND convalidated AND conenforced
                AND conrelid IN ('"Infrastructure_FiniteOperationState"'::regclass, '"Infrastructure_FiniteOperationEpochs"'::regclass)
            ORDER BY conname
            """).ToArrayAsync();
        Assert.Equal([
            "CK_Infrastructure_FiniteOperationEpoch_Deadline", "CK_Infrastructure_FiniteOperationEpoch_Namespace",
            "CK_Infrastructure_FiniteOperationEpoch_Number", "CK_Infrastructure_FiniteOperationEpoch_Pool",
            "CK_Infrastructure_FiniteOperationEpoch_State", "CK_Infrastructure_FiniteOperationState_Epoch",
            "CK_Infrastructure_FiniteOperationState_Namespace", "CK_Infrastructure_FiniteOperationState_Pool",
            "CK_Infrastructure_FiniteOperationState_Protocol"
        ], checks);
        var index = await context.Database.SqlQueryRaw<CatalogIndex>("""
            SELECT i.indisunique AS "Unique", i.indisvalid AS "Valid", i.indisready AS "Ready",
                pg_get_expr(i.indpred, i.indrelid) AS "Predicate",
                ARRAY(SELECT a.attname::text FROM unnest(i.indkey) WITH ORDINALITY AS k(number, position)
                    JOIN pg_attribute a ON a.attrelid = i.indrelid AND a.attnum = k.number ORDER BY k.position) AS "Columns"
            FROM pg_index i WHERE i.indexrelid = '"IX_Infrastructure_FiniteOperationEpochs_NamespaceId_PoolId"'::regclass
            """).SingleAsync();
        Assert.True(index.Unique && index.Valid && index.Ready);
        Assert.Equal(["NamespaceId", "PoolId"], index.Columns);
        Assert.Equal("(\"State\" = 1)", index.Predicate);
    }

    private static async Task AssertInvalidMetadataRefusedAsync(AppDbContext context, DatasetActivationBinding binding) {
        foreach (var invalid in Enum.GetValues<InvalidMetadata>()) {
            var namespaceId = invalid is InvalidMetadata.StateNamespace or InvalidMetadata.EpochNamespace ? Guid.Empty : Guid.NewGuid();
            var pool = invalid is InvalidMetadata.StatePool or InvalidMetadata.EpochPool ? Guid.Empty : ProjectFiniteOperationPool.Pool.Value;
            Task inserting = invalid switch {
                InvalidMetadata.StateNamespace or InvalidMetadata.StatePool or InvalidMetadata.Protocol or InvalidMetadata.StateEpoch
                    => context.Database.ExecuteSqlInterpolatedAsync($"""
                        INSERT INTO "Infrastructure_FiniteOperationState"
                            ("NamespaceId", "PoolId", "RetentionProtocolVersion", "CurrentEpoch", "RetiredThroughEpoch")
                        VALUES ({namespaceId}, {pool}, {(invalid == InvalidMetadata.Protocol ? 0 : 1)}, 1,
                            {(invalid == InvalidMetadata.StateEpoch ? 1 : 0)})
                        """),
                _ => context.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO "Infrastructure_FiniteOperationEpochs"
                        ("NamespaceId", "PoolId", "Epoch", "State", "OpenedAtUtc", "AdmissionClosesAtUtc", "ClosedAtUtc",
                         "MutationDeadlineMilliseconds", "ReplayRetentionSeconds")
                    VALUES ({namespaceId}, {pool}, {(invalid == InvalidMetadata.EpochNumber ? 0L : 1L)},
                        {(invalid == InvalidMetadata.EpochState ? 2 : 1)}, {SavedAt}, {SavedAt.AddDays(1)}, NULL,
                        {(invalid == InvalidMetadata.EpochDeadline ? 0 : 30000)}, 604800)
                    """)
            };
            var failure = await Assert.ThrowsAsync<PostgresException>(() => inserting);
            Assert.Equal(PostgresErrorCodes.CheckViolation, failure.SqlState);
        }
        var duplicate = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Infrastructure_FiniteOperationEpochs"
                ("NamespaceId", "PoolId", "Epoch", "State", "OpenedAtUtc", "AdmissionClosesAtUtc", "ClosedAtUtc",
                 "MutationDeadlineMilliseconds", "ReplayRetentionSeconds")
            VALUES ({binding.Namespace.Value}, {ProjectFiniteOperationPool.Pool.Value}, 2, 1, {SavedAt}, {SavedAt.AddDays(1)}, NULL, 30000, 604800)
            """));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, duplicate.SqlState);
        Assert.Equal("IX_Infrastructure_FiniteOperationEpochs_NamespaceId_PoolId", duplicate.ConstraintName);
    }

    private enum HistoryCorruption { OrphanEpoch, MissingCurrent, MissingUnretired, HistoricalOpen, InactiveLifecycle, OrphanReceipt }
    private enum SchemaExtension { IncomingForeignKey, OutgoingForeignKey, UserTrigger, WeakenedCheck, MissingOpenIndex, CounterDefault }
    private enum InvalidMetadata { StateNamespace, StatePool, Protocol, StateEpoch, EpochNamespace, EpochPool, EpochNumber, EpochDeadline, EpochState }
    private sealed class CatalogIndex {
        public bool Unique { get; set; }
        public bool Valid { get; set; }
        public bool Ready { get; set; }
        public string Predicate { get; set; } = "";
        public string[] Columns { get; set; } = [];
    }
    private sealed class AllowedReceiptAccess : IProjectDefinitionReceiptAccess {
        public Task<bool> CanReadAsync(ProjectWriteAdmission target, CancellationToken cancellationToken) => Task.FromResult(true);
    }
}
