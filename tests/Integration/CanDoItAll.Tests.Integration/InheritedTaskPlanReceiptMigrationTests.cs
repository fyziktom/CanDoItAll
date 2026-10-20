using CanDoItAll.Composition;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace CanDoItAll.Tests.Integration;

public sealed class InheritedTaskPlanReceiptMigrationTests {
    private const string PreviousMigration = "20261019230204_AddProcessSharedRunMaterialScopes";
    private const string Migration = "20261020071655_AddInheritedTaskPlanReceiptIdentity";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Empty_and_legacy_upgrade_restart_and_downgrade_preserve_every_receipt_byte(bool retained) {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
        await using var lease = PostgresTestDatabaseLease.Create("inherited-task-receipt");
        await using var context = new AppDbContext(lease.CreateAppDbContextOptions());
        await context.GetService<IMigrator>().MigrateAsync(PreviousMigration);
        if (retained) {
            context.AddRange(Receipt(1, 1), Receipt(1, 2));
            await context.SaveChangesAsync();
        }
        var before = await RowsAsync(context);
        var schema = await SchemaAsync(context, checks: false);
        var oldChecks = await SchemaAsync(context, checks: true);
        for (var restart = 0; restart < 2; restart++) {
            await using var reopened = new AppDbContext(lease.CreateAppDbContextOptions());
            await reopened.GetService<IMigrator>().MigrateAsync(Migration);
            Assert.Equal(before, await RowsAsync(reopened));
            Assert.Equal(schema, await SchemaAsync(reopened, checks: false));
            Assert.False(reopened.Database.HasPendingModelChanges());
        }
        await using var owner = new WorkbenchDbContext(new DbContextOptionsBuilder<WorkbenchDbContext>().UseNpgsql(lease.ConnectionString).Options);
        var canonical = context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(ProjectTaskPlanReceiptRecord))!;
        var scoped = owner.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(ProjectTaskPlanReceiptRecord))!;
        Assert.Equal(canonical.ToDebugString(MetadataDebugStringOptions.LongDefault), scoped.ToDebugString(MetadataDebugStringOptions.LongDefault));
        await context.GetService<IMigrator>().MigrateAsync(PreviousMigration);
        Assert.Equal(before, await RowsAsync(context));
        Assert.Equal(oldChecks, await SchemaAsync(context, checks: true));
        Assert.Equal(schema, await SchemaAsync(context, checks: false));
    }

    [Fact]
    public async Task Fresh_schema_rejects_cross_origin_or_non_active_work_receipts_and_down_retains_inherited_evidence() {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
        await using var lease = PostgresTestDatabaseLease.Create("inherited-task-receipt");
        await using var context = new AppDbContext(lease.CreateAppDbContextOptions());
        await context.GetService<IMigrator>().MigrateAsync(Migration);
        var inherited = Receipt(2, 2);
        context.AddRange(Receipt(1, 1), Receipt(1, 2), inherited);
        await context.SaveChangesAsync();
        var before = await RowsAsync(context);
        var checks = await SchemaAsync(context, checks: true);
        foreach (var invalid in new[] {
            "\"CallerKind\" = 3", "\"SourceKind\" = 1", "\"SchemaVersion\" = 1",
            "\"ResultVersion\" = 1", "\"SourceVersion\" = 1", "\"NormalizationVersion\" = 2"
        }) {
            var failure = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlRawAsync(
                $"UPDATE \"Workbench_TaskPlanReceipts\" SET {invalid} WHERE \"CallerKind\" = 2"));
            Assert.Equal(PostgresErrorCodes.CheckViolation, failure.SqlState);
            Assert.Equal(before, await RowsAsync(context));
        }
        var refused = await Assert.ThrowsAsync<PostgresException>(() => context.GetService<IMigrator>().MigrateAsync(PreviousMigration));
        Assert.Equal(PostgresErrorCodes.RaiseException, refused.SqlState);
        Assert.False(refused.IsTransient);
        Assert.Contains("retained inherited task-plan receipts", refused.MessageText, StringComparison.Ordinal);
        Assert.Equal(before, await RowsAsync(context));
        Assert.Equal(checks, await SchemaAsync(context, checks: true));
        Assert.Equal(Migration, (await context.Database.GetAppliedMigrationsAsync()).Last());
        await using var reopened = new AppDbContext(lease.CreateAppDbContextOptions());
        await reopened.GetService<IMigrator>().MigrateAsync(Migration);
        Assert.Equal(before, await RowsAsync(reopened));
        var saved = await reopened.Set<ProjectTaskPlanReceiptRecord>().SingleAsync(row => row.CallerKind == 2);
        Assert.Equal(inherited.AcceptedPayload, saved.AcceptedPayload);
        Assert.Equal(inherited.CallerKey, saved.CallerKey);
        Assert.Equal(inherited.AcceptedAtUtc, saved.AcceptedAtUtc);
    }

    private static ProjectTaskPlanReceiptRecord Receipt(int kind, int version) => new() {
        NamespaceId = Guid.NewGuid(), Epoch = 1, CallerKind = kind, CallerKey = Enumerable.Repeat((byte)17, 32).ToArray(),
        OperationId = Guid.NewGuid(), SchemaVersion = version, NormalizationVersion = 1, ResultVersion = version,
        SemanticDigest = Enumerable.Repeat((byte)18, 32).ToArray(), ProjectId = Guid.NewGuid(), LifetimeId = Guid.NewGuid(),
        OriginalDefinitionRevision = Guid.NewGuid(), DecisionId = Guid.NewGuid(), DecisionGeneration = 1,
        SourceKind = kind, SourceVersion = version, SourceFingerprint = Enumerable.Repeat((byte)19, 32).ToArray(),
        AcceptedAtUtc = new(2026, 10, 20, 0, 0, 0, TimeSpan.Zero), AcceptedPayload = "retained original owner bytes — 漢字"u8.ToArray()
    };

    private static Task<string[]> RowsAsync(AppDbContext context) => context.Database.SqlQueryRaw<string>("""
        SELECT to_jsonb(r)::text AS "Value" FROM "Workbench_TaskPlanReceipts" r ORDER BY r."NamespaceId", r."OperationId"
        """).ToArrayAsync();

    private static Task<string[]> SchemaAsync(AppDbContext context, bool checks) => checks
        ? context.Database.SqlQueryRaw<string>("""
            SELECT conname || ':' || pg_get_constraintdef(oid) || ':' || convalidated::text || ':' || conenforced::text AS "Value"
            FROM pg_constraint WHERE conrelid = '"Workbench_TaskPlanReceipts"'::regclass AND contype = 'c' ORDER BY conname
            """).ToArrayAsync()
        : context.Database.SqlQueryRaw<string>("""
            SELECT "Value" FROM (
                SELECT 'column:' || column_name || ':' || data_type || ':' || is_nullable AS "Value"
                FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'Workbench_TaskPlanReceipts'
                UNION ALL SELECT 'index:' || indexname || ':' || indexdef FROM pg_indexes
                WHERE schemaname = 'public' AND tablename = 'Workbench_TaskPlanReceipts'
            ) original_schema ORDER BY "Value"
            """).ToArrayAsync();
}
