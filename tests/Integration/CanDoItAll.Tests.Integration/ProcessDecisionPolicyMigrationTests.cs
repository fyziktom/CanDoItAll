using System.Text.Json;
using CanDoItAll.Composition;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed class ProcessDecisionPolicyMigrationTests {
    private const string PreviousMigration = "20261019014901_IndexLegacyProjectDeletionSubject";
    private const string MigrationName = "AddProcessDecisionPolicyAdmission";

    [Fact]
    public async Task Upgrade_and_restart_preserve_all_legacy_assignment_fields_without_inventing_a_policy() {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
        await using var lease = PostgresTestDatabaseLease.Create("process-decision-policy-migration");
        await using var previous = new AppDbContext(lease.CreateAppDbContextOptions());
        await previous.GetService<IMigrator>().MigrateAsync(PreviousMigration);
        var row = LegacyRow();
        var json = JsonSerializer.Serialize(row);
        await previous.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO process_runtime_step_assignments
            SELECT * FROM json_populate_record(NULL::process_runtime_step_assignments, CAST({json} AS json))
            """);
        var original = await RetainedAsync(previous);
        for (var restart = 0; restart < 2; restart++) {
            await using var context = new AppDbContext(lease.CreateAppDbContextOptions());
            await UpgradeAsync(context);
            Assert.Equal(original, await RetainedAsync(context));
            var saved = await context.Set<ProcessRuntimeStepAssignmentEntity>().AsNoTracking().SingleAsync();
            Assert.Null(saved.DecisionDataPolicyJson);
            Assert.False(context.Database.HasPendingModelChanges());
        }
        await using var owner = new ProcessPersistenceDbContext(new DbContextOptionsBuilder<ProcessPersistenceDbContext>()
            .UseNpgsql(lease.ConnectionString).Options);
        var entity = owner.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(ProcessRuntimeStepAssignmentEntity))!;
        var complete = previous.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(ProcessRuntimeStepAssignmentEntity))!;
        Assert.Equal(complete.ToDebugString(MetadataDebugStringOptions.LongDefault), entity.ToDebugString(MetadataDebugStringOptions.LongDefault));
        var policy = entity.FindProperty(nameof(ProcessRuntimeStepAssignmentEntity.DecisionDataPolicyJson))!;
        Assert.True(policy.IsNullable);
        Assert.Equal("text", policy.GetColumnType());
        Assert.Null(policy.GetDefaultValueSql());
    }

    [Fact]
    public async Task Fresh_database_enforces_utf8_storage_bound_and_refuses_to_downgrade_retained_policy() {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
        await using var lease = PostgresTestDatabaseLease.Create("process-decision-policy-migration");
        await using var context = new AppDbContext(lease.CreateAppDbContextOptions());
        await UpgradeAsync(context);
        var row = LegacyRow();
        context.Add(row);
        await context.SaveChangesAsync();
        foreach (var invalid in new[] { string.Empty, new string('x', ProcessDecisionDataPolicyAdmission.MaximumUtf8Bytes + 1),
            new string('漢', ProcessDecisionDataPolicyAdmission.MaximumUtf8Bytes / 3 + 1) }) {
            var failure = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE process_runtime_step_assignments SET "DecisionDataPolicyJson" = {invalid} WHERE "RunId" = {row.RunId}
                """));
            Assert.Equal(PostgresErrorCodes.CheckViolation, failure.SqlState);
            Assert.Equal("CK_process_runtime_step_assignments_decision_policy", failure.ConstraintName);
        }
        var policy = ProcessDecisionPolicyFixture.Create(new(Guid.NewGuid()), new(row.StepInstanceId));
        var encoded = ProcessDecisionDataPolicyAdmissionCodec.Serialize(policy);
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE process_runtime_step_assignments SET "DecisionDataPolicyJson" = {encoded} WHERE "RunId" = {row.RunId}
            """);
        var before = await RetainedAsync(context);
        var refused = await Assert.ThrowsAsync<PostgresException>(() => context.GetService<IMigrator>().MigrateAsync(PreviousMigration));
        Assert.Equal(PostgresErrorCodes.RaiseException, refused.SqlState);
        Assert.False(refused.IsTransient);
        Assert.Contains("retained Process decision policy", refused.MessageText, StringComparison.Ordinal);
        Assert.Equal(before, await RetainedAsync(context));
        Assert.Equal(encoded, (await context.Set<ProcessRuntimeStepAssignmentEntity>().AsNoTracking().SingleAsync()).DecisionDataPolicyJson);
    }

    private static ProcessRuntimeStepAssignmentEntity LegacyRow() => new() {
        RunId = Guid.NewGuid(), StepInstanceId = Guid.NewGuid(), PlanId = Guid.NewGuid(), StepKey = "retained-step",
        RoleKey = "retained-role", RoleResourceKey = "retained-resource", RoleDisplayName = "Original role",
        ExecutorKind = "agent", ExecutorId = Guid.NewGuid().ToString("D"), ExecutorDisplayName = "Original executor",
        Prompt = "First line\nSecond line — 漢字", ReadinessHash = "original-readiness", AssignmentReason = "Original assignment",
        ProducedArtifactSlotIds = Guid.NewGuid().ToString("D"), RequiredArtifactSlotIds = string.Empty,
        AllowedOperations = "read", OperationTargetScope = "project", LaunchVariablesJson = " { \"key\" : \"value\" } ",
        CapabilityScopeJson = "{}", BranchGateSourceStepKey = "parent", BranchGateRequiredOutcomeKey = "ready",
        CreatedAtUtc = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero)
    };

    private static Task<string[]> RetainedAsync(AppDbContext context) => context.Database.SqlQueryRaw<string>("""
        SELECT (to_jsonb(a) - 'DecisionDataPolicyJson')::text AS "Value" FROM process_runtime_step_assignments a ORDER BY a."RunId"
        """).ToArrayAsync();

    private static Task UpgradeAsync(AppDbContext context) => context.GetService<IMigrator>().MigrateAsync(
        Assert.Single(context.Database.GetMigrations(), id => id.EndsWith("_" + MigrationName, StringComparison.Ordinal)));
}
