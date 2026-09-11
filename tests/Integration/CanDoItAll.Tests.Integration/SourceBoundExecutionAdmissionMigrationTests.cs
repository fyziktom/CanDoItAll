using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.SchedulerPlanner;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace CanDoItAll.Tests.Integration;

public sealed class SourceBoundExecutionAdmissionMigrationTests {
    private const string PreviousMigration = "20260911000227_MoveWorkItemAssignments";
    private const string CurrentMigration = "20260911010404_AddSourceBoundExecutionAdmissions";
    private static readonly DateTimeOffset SavedAt = new(2026, 4, 5, 6, 7, 8, TimeSpan.Zero);

    [Fact]
    public async Task Populated_upgrade_and_restarts_preserve_legacy_data_without_fabricating_authority() {
        await using var environment = CanDoItAllTestEnvironment.Create("source-admission-upgrade");
        var profile = environment.CreatePostgreSqlProfile("original");
        string[] expected;
        await using (var services = CreateProvider(profile)) {
            await using var context = await services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
            await context.GetService<IMigrator>().MigrateAsync(PreviousMigration);
            await SeedLegacyAsync(context);
            expected = await ReadLegacyAsync(context);
            Assert.Equal(6, expected.Length);
            await context.Database.MigrateAsync();
            Assert.Equal(expected, await ReadLegacyAsync(context));
            await AssertNoAuthorityAsync(context);
        }

        for (var restart = 0; restart < 2; restart++) {
            await using var services = CreateProvider(profile);
            await using var context = await services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
            await context.Database.MigrateAsync();
            Assert.Equal(expected, await ReadLegacyAsync(context));
            await AssertNoAuthorityAsync(context);
            Assert.False(context.Database.HasPendingModelChanges());
            Assert.Equal(159, context.Model.GetEntityTypes().Count());
            Assert.Equal(CurrentMigration, (await context.Database.GetAppliedMigrationsAsync()).Last());
        }
    }

    [Fact]
    public async Task Down_and_reupgrade_without_new_evidence_preserve_later_human_edits() {
        await using var environment = CanDoItAllTestEnvironment.Create("source-admission-down");
        var profile = environment.CreatePostgreSqlProfile("roundtrip");
        string[] expected;
        await using (var services = CreateProvider(profile)) {
            await using var context = await services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
            await context.GetService<IMigrator>().MigrateAsync(PreviousMigration);
            await SeedLegacyAsync(context);
            await context.Database.MigrateAsync();
            await context.Database.ExecuteSqlRawAsync("""
                UPDATE "SchedulerPlanner_Plans" SET "Description" = 'Human schedule edit after upgrade';
                UPDATE "Workbench_WorkAssignments" SET "Notes" = 'Human assignment edit after upgrade';
                """);
            expected = await ReadLegacyAsync(context);
            await context.GetService<IMigrator>().MigrateAsync(PreviousMigration);
            Assert.Equal(expected, await ReadLegacyAsync(context));
            Assert.Equal(PreviousMigration, (await context.Database.GetAppliedMigrationsAsync()).Last());
        }

        await using var restarted = CreateProvider(profile);
        await using var restored = await restarted.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
        Assert.Equal(expected, await ReadLegacyAsync(restored));
        await restored.Database.MigrateAsync();
        Assert.Equal(expected, await ReadLegacyAsync(restored));
        await AssertNoAuthorityAsync(restored);
        Assert.False(restored.Database.HasPendingModelChanges());
    }

    [Theory]
    [InlineData(RetainedEvidence.CreationReservation)]
    [InlineData(RetainedEvidence.SchedulerFire)]
    [InlineData(RetainedEvidence.ProcessPreparation)]
    [InlineData(RetainedEvidence.ScheduleAuthority)]
    [InlineData(RetainedEvidence.ProcessProjectAdmission)]
    [InlineData(RetainedEvidence.ProcessLaunchPointer)]
    public async Task Retained_evidence_blocks_down_and_target_replacement_even_without_a_live_project(RetainedEvidence evidence) {
        await using var environment = CanDoItAllTestEnvironment.Create("source-admission-retained");
        await using var services = CreateProvider(environment.CreatePostgreSqlProfile("retained"));
        await using var scope = services.CreateAsyncScope();
        await using var context = await services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
        await context.Database.MigrateAsync();
        object row = evidence switch {
            RetainedEvidence.CreationReservation => new ProjectCreationReservationRecord {
                Id = Guid.NewGuid(), DatabaseProfileId = Guid.NewGuid(), ProjectId = Guid.NewGuid(), LifetimeId = Guid.NewGuid(),
                RequesterId = Guid.NewGuid(), State = ProjectCreationReservationState.Cancelled, CreatedAtUtc = SavedAt, CancelledAtUtc = SavedAt
            },
            RetainedEvidence.SchedulerFire => new SchedulerFireAdmissionRecord {
                Id = Guid.NewGuid(), PlanId = Guid.NewGuid(), DedupeKey = "retained-fire", SnapshotJson = "{\"historical\":true}",
                SnapshotFingerprint = "retained", PreparedWorkflowRunId = Guid.NewGuid(), State = SchedulerFireAdmissionState.Observed,
                CreatedAtUtc = SavedAt, UpdatedAtUtc = SavedAt, OutcomeJson = "{\"committed\":true}"
            },
            RetainedEvidence.ProcessPreparation => new ProcessPreparedLaunchEntity {
                Id = Guid.NewGuid(), RunId = Guid.NewGuid(), PlanId = Guid.NewGuid(), CallerIntentId = Guid.NewGuid(),
                RequestFingerprint = "retained-request", PreparationFingerprint = "retained-preparation", PayloadJson = "{\"historical\":true}",
                PreparedAtUtc = SavedAt, AcceptedAtUtc = SavedAt, State = ProcessLaunchContinuationState.Started,
                LinkDeliveryState = ProcessLaunchLinkDeliveryState.Delivered, DeliveredLinkId = Guid.NewGuid()
            },
            RetainedEvidence.ScheduleAuthority => new SchedulerPlan {
                Name = "Retained source", IsEnabled = false, StructureAuthorityJson = "{\"historical\":true}", CreatedAtUtc = SavedAt, UpdatedAtUtc = SavedAt
            },
            RetainedEvidence.ProcessProjectAdmission => NewAdmittedRuntime(),
            RetainedEvidence.ProcessLaunchPointer => NewRuntime(),
            _ => throw new ArgumentOutOfRangeException(nameof(evidence))
        };
        if (evidence == RetainedEvidence.ProcessLaunchPointer) {
            ((ProcessRuntimeStateEntity)row).LaunchAdmissionId = Guid.NewGuid();
        }
        context.Add(row);
        await context.SaveChangesAsync();
        var expected = await ReadEvidenceAsync(context);
        Assert.Single(expected);
        var failure = await Assert.ThrowsAsync<PostgresException>(() => context.GetService<IMigrator>().MigrateAsync(PreviousMigration));
        Assert.Equal(PostgresErrorCodes.RaiseException, failure.SqlState);
        Assert.Contains("Cannot remove retained source-bound", failure.MessageText, StringComparison.Ordinal);
        Assert.Equal(expected, await ReadEvidenceAsync(context));
        Assert.Contains(CurrentMigration, await context.Database.GetAppliedMigrationsAsync());
        Assert.False(context.Database.HasPendingModelChanges());
        var area = evidence switch {
            RetainedEvidence.CreationReservation => ProjectTransferTargetStateArea.Projects,
            RetainedEvidence.SchedulerFire or RetainedEvidence.ScheduleAuthority => ProjectTransferTargetStateArea.SchedulerPlanner,
            _ => ProjectTransferTargetStateArea.Processes
        };
        var participant = scope.ServiceProvider.GetServices<IProjectTransferTargetStateParticipant>().Single(item => item.Area == area);
        Assert.Contains(row.GetType(), participant.EntityTypesToLock);
        Assert.NotEmpty(await participant.FindResiduesAsync(context, CancellationToken.None));

        static ProcessRuntimeStateEntity NewAdmittedRuntime() {
            var runtime = NewRuntime();
            runtime.ProjectAdmissionDatabaseProfileId = Guid.NewGuid();
            runtime.ProjectAdmissionProjectId = Guid.NewGuid();
            runtime.ProjectAdmissionLifetimeId = Guid.NewGuid();
            return runtime;
        }
    }

    [Fact]
    public async Task Canonical_constraints_reject_partial_or_empty_project_tuples_and_partial_parent_reservations() {
        await using var environment = CanDoItAllTestEnvironment.Create("source-admission-constraints");
        await using var services = CreateProvider(environment.CreatePostgreSqlProfile("constraints"));
        await using var context = await services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
        await context.Database.MigrateAsync();
        var runtime = NewRuntime();
        context.Add(runtime);
        await context.SaveChangesAsync();
        runtime.ProjectAdmissionDatabaseProfileId = Guid.NewGuid();
        await AssertCheckAsync(context, "CK_process_runtime_states_project_admission");
        runtime.ProjectAdmissionProjectId = Guid.NewGuid();
        runtime.ProjectAdmissionLifetimeId = Guid.Empty;
        await AssertCheckAsync(context, "CK_process_runtime_states_project_admission");
        runtime.ProjectAdmissionLifetimeId = Guid.NewGuid();
        await context.SaveChangesAsync();
        var reservation = new ProjectCreationReservationRecord {
            Id = Guid.NewGuid(), DatabaseProfileId = Guid.NewGuid(), ProjectId = Guid.NewGuid(), LifetimeId = Guid.NewGuid(),
            RequesterId = Guid.NewGuid(), ParentProjectId = Guid.NewGuid(), State = ProjectCreationReservationState.Reserved, CreatedAtUtc = SavedAt
        };
        context.Add(reservation);
        await AssertCheckAsync(context, "CK_Projects_CreationReservation_Parent");
        reservation.ParentLifetimeId = Guid.NewGuid();
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var saved = await context.Set<ProcessRuntimeStateEntity>().SingleAsync();
        Assert.Equal(runtime.ProjectAdmissionLifetimeId, saved.ProjectAdmissionLifetimeId);
        Assert.Equal(reservation.ParentLifetimeId, (await context.Set<ProjectCreationReservationRecord>().SingleAsync()).ParentLifetimeId);
    }

    private static ProcessRuntimeStateEntity NewRuntime() => new() {
        RunId = Guid.NewGuid(), RootRunId = Guid.NewGuid(), PlanId = Guid.NewGuid(), PlanHash = "retained-plan",
        Status = ProcessRuntimeStatus.Completed, UpdatedAtUtc = SavedAt, ConcurrencyToken = Guid.NewGuid()
    };

    private static async Task AssertCheckAsync(AppDbContext context, string constraint) {
        var failure = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        var postgres = Assert.IsType<PostgresException>(failure.InnerException);
        Assert.Equal(PostgresErrorCodes.CheckViolation, postgres.SqlState);
        Assert.Equal(constraint, postgres.ConstraintName);
    }

    private static ServiceProvider CreateProvider(TestDatabaseProfile profile) {
        var services = new ServiceCollection();
        TestApplicationBootstrap.ConfigureDefaultServices(services, TestApplicationBootstrap.BuildConfiguration(profile),
            new TestHostEnvironment(profile.EnvironmentRootPath, "CanDoItAll.Tests.Integration"));
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    private static async Task SeedLegacyAsync(AppDbContext context) {
        var project = new Project { Name = "Preserved source project", Slug = Guid.NewGuid().ToString("N"), CreatedAtUtc = SavedAt, UpdatedAtUtc = SavedAt };
        context.Add(project);
        context.Add(new ProjectObjectRecord { ProjectId = project.Id, NodeKey = "saved-task", ObjectType = ProjectObjectType.WorkItem,
            Title = "Saved native task", MetadataJson = "{\"unknown\":{\"value\":7}}", CreatedAtUtc = SavedAt, UpdatedAtUtc = SavedAt });
        context.Add(new ProjectWorkAssignmentRecord { Id = Guid.NewGuid(), ProjectId = project.Id, NodeKey = "saved-task",
            PartyId = Guid.NewGuid(), AllocationPercent = 12.123456789m, Notes = "Original history\nwith trailing spaces  " });
        context.Add(new WorkflowStructureOutputRecord { RunId = Guid.NewGuid(), OccurrencePath = "old-contribution",
            PlanJson = "{\"original\":true}", ReceiptJson = "{\"committed\":true}", NextInspectionAtUtc = SavedAt });
        await context.SaveChangesAsync();
        var planId = Guid.NewGuid();
        var runtime = NewRuntime();
        var inputJson = """{"source":"legacy","unknown":7}""";
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "SchedulerPlanner_Plans" ("Id", "Name", "Description", "TargetKind", "TargetId", "TargetVersionId",
                "TargetNameSnapshot", "CronExpression", "CronDescription", "TimeZoneId", "MisfirePolicy", "IsEnabled", "StartAtUtc", "EndAtUtc",
                "InputJson", "AutomationTriggerId", "AutomationTriggerKey", "NextPlannedFireAtUtc", "LastFiredAtUtc", "LastError", "CreatedAtUtc", "UpdatedAtUtc")
            VALUES ({planId}, 'Saved schedule', 'Original description', {(int)SchedulerPlanTargetKind.Workflow}, {Guid.NewGuid()}, NULL,
                'Saved workflow', '0 0 0 1 1 ? 2099', 'Future synthetic schedule', 'UTC', {(int)SchedulerPlanMisfirePolicy.FireOnceNow}, FALSE, NULL, NULL,
                {inputJson}, {Guid.NewGuid()}, 'legacy-trigger', NULL, {SavedAt}, 'Preserved outcome', {SavedAt}, {SavedAt});
            INSERT INTO "process_runtime_states" ("RunId", "RootRunId", "PlanId", "PlanHash", "Status", "UpdatedAtUtc", "ConcurrencyToken", "BlockedRecoveryActionsJson")
            VALUES ({runtime.RunId}, {runtime.RootRunId}, {runtime.PlanId}, {runtime.PlanHash}, {nameof(ProcessRuntimeStatus.Completed)},
                {SavedAt}, {runtime.ConcurrencyToken}, '[]');
            """);
    }

    private static Task<string[]> ReadLegacyAsync(AppDbContext context) => context.Database.SqlQueryRaw<string>("""
        SELECT 'project:' || to_jsonb(row)::text AS "Value" FROM "Projects_Projects" row
        UNION ALL SELECT 'native:' || to_jsonb(row)::text AS "Value" FROM "Workbench_ProjectObjects" row
        UNION ALL SELECT 'work:' || to_jsonb(row)::text AS "Value" FROM "Workbench_WorkAssignments" row
        UNION ALL SELECT 'workflow:' || to_jsonb(row)::text AS "Value" FROM "AgentFramework_WorkflowStructureOutputs" row
        UNION ALL SELECT 'schedule:' || (to_jsonb(row) - 'StructureAuthorityJson')::text AS "Value" FROM "SchedulerPlanner_Plans" row
        UNION ALL SELECT 'process:' || (to_jsonb(row) - ARRAY['LaunchAdmissionId', 'ProjectAdmissionDatabaseProfileId', 'ProjectAdmissionProjectId', 'ProjectAdmissionLifetimeId'])::text AS "Value"
            FROM "process_runtime_states" row
        ORDER BY "Value"
        """).ToArrayAsync();

    private static Task<string[]> ReadEvidenceAsync(AppDbContext context) => context.Database.SqlQueryRaw<string>("""
        SELECT 'creation:' || to_jsonb(row)::text AS "Value" FROM "Projects_ProjectCreationReservations" row
        UNION ALL SELECT 'fire:' || to_jsonb(row)::text AS "Value" FROM "SchedulerPlanner_FireAdmissions" row
        UNION ALL SELECT 'prepared:' || to_jsonb(row)::text AS "Value" FROM "process_prepared_launches" row
        UNION ALL SELECT 'schedule:' || to_jsonb(row)::text AS "Value" FROM "SchedulerPlanner_Plans" row WHERE "StructureAuthorityJson" IS NOT NULL
        UNION ALL SELECT 'runtime:' || to_jsonb(row)::text AS "Value" FROM "process_runtime_states" row
            WHERE "LaunchAdmissionId" IS NOT NULL OR "ProjectAdmissionDatabaseProfileId" IS NOT NULL
                OR "ProjectAdmissionProjectId" IS NOT NULL OR "ProjectAdmissionLifetimeId" IS NOT NULL
        ORDER BY "Value"
        """).ToArrayAsync();

    private static async Task AssertNoAuthorityAsync(AppDbContext context) => Assert.Empty(await ReadEvidenceAsync(context));

    public enum RetainedEvidence {
        CreationReservation,
        SchedulerFire,
        ProcessPreparation,
        ScheduleAuthority,
        ProcessProjectAdmission,
        ProcessLaunchPointer
    }
}
