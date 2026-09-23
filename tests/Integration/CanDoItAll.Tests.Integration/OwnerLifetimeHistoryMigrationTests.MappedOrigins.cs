using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace CanDoItAll.Tests.Integration;

public sealed partial class OwnerLifetimeHistoryMigrationTests {
    [Fact]
    public async Task Legacy_mapped_pairs_are_factual_projections_and_duplicate_children_survive_upgrade_and_restart() {
        await using var environment = CanDoItAllTestEnvironment.Create("owner-lifetime-mapped-upgrade");
        var profile = environment.CreatePostgreSqlProfile("legacy");
        var processRun = Guid.NewGuid();
        var assignment = Guid.NewGuid();
        var origin = " \n" + LegacyMappedOrigin(processRun, assignment).ToJsonString() + "\n ";
        Guid first;
        Guid second;
        Guid unrelated;
        string[] original;
        await using (var services = CreateProvider(profile)) {
            await using var database = await ContextAsync(services);
            await database.GetService<IMigrator>().MigrateAsync(PreviousMigration);
            first = await InsertLegacyRunAsync(database, origin);
            second = await InsertLegacyRunAsync(database, origin, WorkflowLaunchOriginKind.ProcessAssignment, processRun);
            unrelated = await InsertLegacyRunAsync(database, LegacyJson, WorkflowLaunchOriginKind.Api);
            original = await SnapshotDataAsync(database, legacy: true, excludeMappedProjections: true);
            await MigrateCurrentAsync(database);
            await AssertMappedProjectionAsync(database);
        }
        for (var restart = 0; restart < 2; restart++) {
            await using var services = CreateProvider(profile);
            await using var database = await ContextAsync(services);
            await MigrateCurrentAsync(database);
            await AssertMappedProjectionAsync(database);
        }

        async Task AssertMappedProjectionAsync(AppDbContext database) {
            Assert.Equal(original, await SnapshotDataAsync(database, legacy: true, excludeMappedProjections: true));
            var runs = await database.Set<WorkflowRunRecordEntity>().AsNoTracking().OrderBy(row => row.RunId).ToArrayAsync();
            Assert.Equal(3, runs.Length);
            foreach (var id in new[] { first, second }) {
                var row = Assert.Single(runs, run => run.RunId == id);
                Assert.Equal(origin, row.OriginJson);
                Assert.Equal(processRun, row.OriginProcessRunId);
                Assert.Equal(assignment, row.OriginProcessAssignmentId);
                Assert.Equal(id == first ? (WorkflowLaunchOriginKind?)null : WorkflowLaunchOriginKind.ProcessAssignment, row.OriginKind);
            }
            var unchanged = Assert.Single(runs, run => run.RunId == unrelated);
            Assert.Null(unchanged.OriginProcessRunId);
            Assert.Null(unchanged.OriginProcessAssignmentId);
            var entity = database.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(WorkflowRunRecordEntity))!;
            Assert.True(entity.FindProperty(nameof(WorkflowRunRecordEntity.OriginProcessAssignmentId))!.IsNullable);
            var index = Assert.Single(entity.GetIndexes(), index => index.GetDatabaseName() == "IX_WorkflowRuns_ProcessAssignment");
            Assert.False(index.IsUnique);
            Assert.Null(index.GetFilter());
            Assert.Equal(new[] { "OriginProcessRunId", "OriginProcessAssignmentId" }, index.Properties.Select(property => property.Name));
            Assert.Equal("CREATE INDEX \"IX_WorkflowRuns_ProcessAssignment\" ON public.\"AgentFramework_WorkflowRuns\" USING btree (\"OriginProcessRunId\", \"OriginProcessAssignmentId\")",
                await database.Database.SqlQueryRaw<string>("""
                    SELECT indexdef AS "Value" FROM pg_indexes WHERE schemaname = 'public'
                        AND tablename = 'AgentFramework_WorkflowRuns' AND indexname = 'IX_WorkflowRuns_ProcessAssignment'
                    """).SingleAsync());
            Assert.False(database.Database.HasPendingModelChanges());
        }
    }

    [Theory]
    [InlineData(InvalidMappedEvidence.MissingProcessRun)]
    [InlineData(InvalidMappedEvidence.MissingAssignment)]
    [InlineData(InvalidMappedEvidence.InvalidProcessRun)]
    [InlineData(InvalidMappedEvidence.InvalidAssignment)]
    [InlineData(InvalidMappedEvidence.EmptyProcessRun)]
    [InlineData(InvalidMappedEvidence.EmptyAssignment)]
    [InlineData(InvalidMappedEvidence.ConflictingProcessProjection)]
    [InlineData(InvalidMappedEvidence.ConflictingKind)]
    [InlineData(InvalidMappedEvidence.MalformedJson)]
    public async Task Malformed_or_conflicting_legacy_mapped_evidence_blocks_upgrade_without_schema_or_data_loss(InvalidMappedEvidence evidence) {
        await using var environment = CanDoItAllTestEnvironment.Create("owner-lifetime-mapped-conflict");
        var profile = environment.CreatePostgreSqlProfile("legacy");
        string[] original;
        string[] schema;
        string[] migrations;
        await using (var services = CreateProvider(profile)) {
            await using var database = await ContextAsync(services);
            await database.GetService<IMigrator>().MigrateAsync(PreviousMigration);
            var processRun = Guid.NewGuid();
            var origin = LegacyMappedOrigin(processRun, Guid.NewGuid());
            switch (evidence) {
                case InvalidMappedEvidence.MissingProcessRun:
                    origin.Remove("processRunId");
                    break;
                case InvalidMappedEvidence.MissingAssignment:
                    origin.Remove("assignmentId");
                    break;
                case InvalidMappedEvidence.InvalidProcessRun:
                    origin["processRunId"] = "original-invalid-guid";
                    break;
                case InvalidMappedEvidence.InvalidAssignment:
                    origin["assignmentId"] = "original-invalid-guid";
                    break;
                case InvalidMappedEvidence.EmptyProcessRun:
                    origin["processRunId"] = Guid.Empty;
                    break;
                case InvalidMappedEvidence.EmptyAssignment:
                    origin["assignmentId"] = Guid.Empty;
                    break;
            }
            var json = evidence == InvalidMappedEvidence.MalformedJson ? "{\"$origin\":\"process-assignment\", malformed  " : origin.ToJsonString();
            await InsertLegacyRunAsync(database, json,
                evidence == InvalidMappedEvidence.ConflictingKind ? WorkflowLaunchOriginKind.Api : WorkflowLaunchOriginKind.ProcessAssignment,
                evidence == InvalidMappedEvidence.ConflictingProcessProjection ? Guid.NewGuid() : null);
            original = await SnapshotDataAsync(database, legacy: true);
            schema = await SnapshotSchemaAsync(database);
            migrations = (await database.Database.GetAppliedMigrationsAsync()).ToArray();
            var failure = await Assert.ThrowsAsync<PostgresException>(() => MigrateCurrentAsync(database));
            Assert.Contains(failure.SqlState, new[] { PostgresErrorCodes.RaiseException, PostgresErrorCodes.InvalidTextRepresentation });
            Assert.Equal(original, await SnapshotDataAsync(database, legacy: true));
            Assert.Equal(schema, await SnapshotSchemaAsync(database));
            Assert.Equal(migrations, (await database.Database.GetAppliedMigrationsAsync()).ToArray());
        }
        await using var restarted = CreateProvider(profile);
        await using var readback = await ContextAsync(restarted);
        Assert.Equal(original, await SnapshotDataAsync(readback, legacy: true));
        Assert.Equal(schema, await SnapshotSchemaAsync(readback));
        Assert.Equal(migrations, (await readback.Database.GetAppliedMigrationsAsync()).ToArray());
    }

    [Theory]
    [InlineData(EvidenceTable.WorkflowRuns)]
    [InlineData(EvidenceTable.WorkflowUsage)]
    [InlineData(EvidenceTable.WorkflowLaunches)]
    public Task Mapped_origin_kind_blocks_down_in_each_owner_carrier_including_pending_claim_without_a_run(EvidenceTable table)
        => AssertDownBlockedAsync(async database => {
            object row;
            switch (table) {
                case EvidenceTable.WorkflowRuns:
                    var run = Run();
                    run.OriginKind = WorkflowLaunchOriginKind.ProcessDispatchAssignment;
                    row = run;
                    break;
                case EvidenceTable.WorkflowUsage:
                    var usage = Usage();
                    usage.OriginKind = WorkflowLaunchOriginKind.ProcessDispatchAssignment;
                    row = usage;
                    break;
                case EvidenceTable.WorkflowLaunches:
                    var launch = Launch();
                    launch.OriginKind = WorkflowLaunchOriginKind.ProcessDispatchAssignment;
                    launch.State = WorkflowLaunchIdempotencyClaimState.Pending;
                    launch.CompletionJson = string.Empty;
                    launch.CompletedAtUtc = null;
                    Assert.Empty(await database.Set<WorkflowRunRecordEntity>().ToArrayAsync());
                    row = launch;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(table));
            }
            database.Add(row);
            await database.SaveChangesAsync();
        });

    [Theory]
    [InlineData(EvidenceTable.WorkflowRuns)]
    [InlineData(EvidenceTable.WorkflowUsage)]
    [InlineData(EvidenceTable.WorkflowLaunches)]
    public Task Mapped_origin_marker_blocks_down_when_scalar_kind_and_assignment_projections_are_missing(EvidenceTable table)
        => AssertDownBlockedAsync(async database => {
            database.Add(OriginRow(table, new JsonObject { ["$origin"] = "process-dispatch-assignment-v1" }.ToJsonString()));
            await database.SaveChangesAsync();
        });

    [Fact]
    public Task Mapped_origin_in_resolved_completion_blocks_down_without_run_origin_or_kind()
        => AssertDownBlockedAsync(async database => {
            database.Add(CompletionOriginRow("resolvedRequest", new JsonObject { ["$origin"] = "process-dispatch-assignment-v1" }.ToJsonString()));
            await database.SaveChangesAsync();
        });

    [Fact]
    public Task Unexplained_assignment_projection_is_retained_when_its_original_origin_is_missing()
        => AssertDownBlockedAsync(async database => {
            var run = Run();
            run.OriginProcessAssignmentId = Guid.NewGuid();
            database.Add(run);
            await database.SaveChangesAsync();
        });

    private static JsonObject LegacyMappedOrigin(Guid processRun, Guid assignment) => new() {
        ["$origin"] = "process-assignment", ["processRunId"] = processRun, ["assignmentId"] = assignment,
        ["legacyExtension"] = "Original mapped origin  "
    };

    private static async Task<Guid> InsertLegacyRunAsync(AppDbContext database, string origin, WorkflowLaunchOriginKind? kind = null, Guid? processRun = null) {
        var runId = Guid.NewGuid();
        await database.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "AgentFramework_WorkflowRuns" ("RunId", "WorkflowId", "VersionId", "State", "Backend", "BackendRunId", "Summary", "CreatedAtUtc",
                "UpdatedAtUtc", "TerminalAtUtc", "ReportingActivityAtUtc", "OriginJson", "OriginKind", "OriginProjectId", "OriginProcessRunId")
            VALUES ({runId}, {Guid.NewGuid()}, {Guid.NewGuid()}, {0}, {0}, 'saved-mapped-backend', 'Original mapped summary  ',
                {SavedAt}, {SavedAt}, {SavedAt}, {SavedAt}, {origin}, {(int?)kind}, NULL, {processRun})
            """);
        return runId;
    }

    public enum InvalidMappedEvidence {
        MissingProcessRun, MissingAssignment, InvalidProcessRun, InvalidAssignment, EmptyProcessRun, EmptyAssignment,
        ConflictingProcessProjection, ConflictingKind, MalformedJson
    }
}
