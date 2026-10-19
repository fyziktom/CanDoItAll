using CanDoItAll.Composition;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace CanDoItAll.Tests.Integration;

public sealed class ProjectLifecycleMigrationTests {
    private const string PreviousMigration = "20261017130214_AddWorkspaceDecisionDataPolicy";
    private const string SubjectMigration = "20261018172443_AddProjectLifecycleOperations";

    [Fact]
    public async Task Upgrade_preserves_promises_opaque_receipts_and_legacy_rows_without_minting_release_authority() {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
        await using var fixture = new ProjectOperationEpochTests.Fixture(PostgresTestDatabaseLease.Create("lifecycle-migration"));
        await using var context = fixture.Context();
        await context.GetService<IMigrator>().MigrateAsync(PreviousMigration);
        var previousPool = new FiniteOperationPoolRegistration(fixture.Pool.Pool, 1, fixture.Pool.Timing,
            [new(ProjectFiniteOperationPool.DefinitionFamily, ProjectDefinitionReceiptRecord.MaximumPayloadBytes)]);
        await fixture.InitializeAsync(fixture.Service, previousPool);
        var projectId = Guid.NewGuid();
        var lifetimeId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        var recoveryId = Guid.NewGuid();
        var now = await fixture.NowAsync();
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Infrastructure_FiniteOperationState"
                ("NamespaceId", "PoolId", "RetentionProtocolVersion", "CurrentEpoch", "RetiredThroughEpoch")
                SELECT "NamespaceId", {WorkbenchTaskPlanFiniteOperations.Pool.Value}, 1, "CurrentEpoch", "RetiredThroughEpoch"
                FROM "Infrastructure_FiniteOperationState" WHERE "PoolId" = {fixture.Pool.Pool.Value};
            INSERT INTO "Infrastructure_FiniteOperationEpochs"
                ("NamespaceId", "PoolId", "Epoch", "State", "OpenedAtUtc", "AdmissionClosesAtUtc", "ClosedAtUtc",
                    "MutationDeadlineMilliseconds", "ReplayRetentionSeconds")
                SELECT "NamespaceId", {WorkbenchTaskPlanFiniteOperations.Pool.Value}, "Epoch", "State", "OpenedAtUtc",
                    "AdmissionClosesAtUtc", "ClosedAtUtc", "MutationDeadlineMilliseconds", "ReplayRetentionSeconds"
                FROM "Infrastructure_FiniteOperationEpochs" WHERE "PoolId" = {fixture.Pool.Pool.Value};
            INSERT INTO "Projects_DefinitionReceipts" ("NamespaceId", "Epoch", "CallerKind", "CallerKey", "OperationId",
                "SchemaVersion", "NormalizationVersion", "ResultVersion", "CommandKind", "SemanticDigest", "ProjectId",
                "LifetimeId", "AcceptedRevision", "AcceptedAtUtc", "AcceptedPayload")
            VALUES ({fixture.Binding.Namespace.Value}, 1, 3, {new byte[32]}, {Guid.NewGuid()}, 99, 99, 99, 2,
                {new byte[32]}, {projectId}, {lifetimeId}, {Guid.NewGuid()}, {now}, {new byte[] { 1, 2, 3 }});
            INSERT INTO "Projects_ProjectCreationReservations" ("Id", "DatabaseProfileId", "ProjectId", "LifetimeId",
                "RequesterId", "State", "CreatedAtUtc")
            VALUES ({reservationId}, {fixture.Profile.Profile.Id}, {projectId}, {lifetimeId}, {Guid.NewGuid()}, 1, {now});
            INSERT INTO "AgentFramework_ProjectAccessRevocations" ("Id", "ProjectId", "DatabaseProfileId", "ProjectLifetimeId",
                "Status", "CreatedAtUtc", "UpdatedAtUtc", "AttemptCount")
            VALUES ({recoveryId}, {projectId}, {fixture.Profile.Profile.Id}, {lifetimeId}, 1, {now}, {now}, 0);
            """);
        var retained = await RetainedAsync(context);
        Assert.Equal(7, retained.Length);

        for (var restart = 0; restart < 2; restart++) {
            await using var migrated = fixture.Context();
            await migrated.Database.MigrateAsync();
            Assert.False(migrated.Database.HasPendingModelChanges());
            Assert.Equal(retained, await RetainedAsync(migrated));
            var states = await migrated.Set<FiniteOperationStateRecord>().AsNoTracking().ToArrayAsync();
            Assert.Equal(2, Assert.Single(states, state => state.PoolId == fixture.Pool.Pool.Value).RetentionProtocolVersion);
            Assert.Equal(1, Assert.Single(states, state => state.PoolId == WorkbenchTaskPlanFiniteOperations.Pool.Value).RetentionProtocolVersion);
            Assert.Null((await migrated.Set<ProjectCreationReservationRecord>().AsNoTracking().SingleAsync()).NamespaceId);
            var recovery = await migrated.Set<AgentProjectStructureAccessRevocationRecord>().AsNoTracking().SingleAsync();
            Assert.Null(recovery.CompletionNamespaceId);
            Assert.Null(recovery.CompletionId);
            Assert.Empty(await migrated.Set<ProjectDeletionCompletionRecord>().AsNoTracking().ToArrayAsync());
            Assert.Empty(await migrated.Set<ProjectLifecycleReceiptRecord>().AsNoTracking().ToArrayAsync());
            var foreignKeys = await migrated.Database.SqlQueryRaw<int>("""
                SELECT count(*)::integer AS "Value" FROM pg_constraint WHERE contype = 'f' AND
                    (conrelid IN ('"Projects_DeletionCompletions"'::regclass, '"Projects_LifecycleReceipts"'::regclass)
                    OR confrelid IN ('"Projects_DeletionCompletions"'::regclass, '"Projects_LifecycleReceipts"'::regclass))
                """).SingleAsync();
            Assert.Equal(0, foreignKeys);
        }

        var refusal = await Assert.ThrowsAsync<PostgresException>(() => context.GetService<IMigrator>().MigrateAsync(PreviousMigration));
        Assert.Equal(PostgresErrorCodes.RaiseException, refusal.SqlState);
        Assert.Equal(SubjectMigration, (await context.Database.GetAppliedMigrationsAsync()).Last());
        Assert.Equal(retained, await RetainedAsync(context));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Unsupported_protocol_or_user_trigger_refuses_the_whole_schema_change(bool trigger) {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
        await using var fixture = new ProjectOperationEpochTests.Fixture(PostgresTestDatabaseLease.Create("lifecycle-migration-refusal"));
        await using var context = fixture.Context();
        await context.GetService<IMigrator>().MigrateAsync(PreviousMigration);
        var pool = new FiniteOperationPoolRegistration(fixture.Pool.Pool, trigger ? 1 : 99, fixture.Pool.Timing,
            [new(ProjectFiniteOperationPool.DefinitionFamily, ProjectDefinitionReceiptRecord.MaximumPayloadBytes)]);
        await fixture.InitializeAsync(fixture.Service, pool);
        if (trigger) {
            await context.Database.ExecuteSqlRawAsync("""
                CREATE FUNCTION lifecycle_migration_probe() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NEW; END $$;
                CREATE TRIGGER lifecycle_migration_probe BEFORE UPDATE ON "Infrastructure_FiniteOperationState"
                    FOR EACH ROW EXECUTE FUNCTION lifecycle_migration_probe();
                """);
        }
        var before = await RetainedAsync(context);
        var refusal = await Assert.ThrowsAsync<PostgresException>(() => context.Database.MigrateAsync());
        Assert.Equal(PostgresErrorCodes.RaiseException, refusal.SqlState);
        Assert.Equal(PreviousMigration, (await context.Database.GetAppliedMigrationsAsync()).Last());
        Assert.Equal(before, await RetainedAsync(context));
        Assert.False(await context.Database.SqlQueryRaw<bool>("""
            SELECT to_regclass('"Projects_DeletionCompletions"') IS NOT NULL
                OR to_regclass('"Projects_LifecycleReceipts"') IS NOT NULL
                OR EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = current_schema()
                    AND table_name = 'Projects_ProjectCreationReservations' AND column_name = 'NamespaceId') AS "Value"
            """).SingleAsync());
    }

    private static Task<string[]> RetainedAsync(AppDbContext context)
        => context.Database.SqlQueryRaw<string>("""
            SELECT "Value" FROM (
                SELECT 'state:' || (to_jsonb(s) - 'RetentionProtocolVersion')::text AS "Value" FROM "Infrastructure_FiniteOperationState" s
                UNION ALL SELECT 'epoch:' || to_jsonb(e)::text FROM "Infrastructure_FiniteOperationEpochs" e
                UNION ALL SELECT 'receipt:' || to_jsonb(r)::text FROM "Projects_DefinitionReceipts" r
                UNION ALL SELECT 'reservation:' || (to_jsonb(r) - 'NamespaceId')::text FROM "Projects_ProjectCreationReservations" r
                UNION ALL SELECT 'recovery:' || (to_jsonb(r) - 'CompletionNamespaceId' - 'CompletionId')::text FROM "AgentFramework_ProjectAccessRevocations" r
            ) retained ORDER BY "Value"
            """).ToArrayAsync();
}
