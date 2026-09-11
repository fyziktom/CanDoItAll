using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Resources;
using CanDoItAll.Modules.TestLab;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Integration;

public sealed partial class OwnerLifetimeHistoryMigrationTests {
    [Theory]
    [InlineData(ScalarEvidence.Resource)]
    [InlineData(ScalarEvidence.TestPlan)]
    [InlineData(ScalarEvidence.WorkAssignment)]
    [InlineData(ScalarEvidence.Participation)]
    [InlineData(ScalarEvidence.Staffing)]
    [InlineData(ScalarEvidence.MoveReceiptTuple)]
    [InlineData(ScalarEvidence.ContributionProfile)]
    [InlineData(ScalarEvidence.ContributionLifetime)]
    [InlineData(ScalarEvidence.AdmissionProfile)]
    [InlineData(ScalarEvidence.AdmissionLifetime)]
    [InlineData(ScalarEvidence.OutputProfile)]
    [InlineData(ScalarEvidence.OutputProject)]
    [InlineData(ScalarEvidence.OutputLifetime)]
    public Task Each_new_scalar_or_constrained_tuple_independently_prevents_down(ScalarEvidence evidence)
        => AssertDownBlockedAsync(async database => {
            var id = Guid.NewGuid();
            var lifetime = Guid.NewGuid();
            object row;
            switch (evidence) {
                case ScalarEvidence.Resource:
                    row = new ProjectResource { ProjectId = id, ProjectLifetimeId = lifetime, Name = "Retained resource", ConfigJson = LegacyJson };
                    break;
                case ScalarEvidence.TestPlan:
                    row = new TestPlan { ProjectId = id, ProjectLifetimeId = lifetime, Title = "Retained test plan" };
                    break;
                case ScalarEvidence.WorkAssignment:
                    row = new ProjectWorkAssignmentRecord { ProjectId = id, ProjectLifetimeId = lifetime, PartyId = Guid.NewGuid(), NodeKey = "saved-task", Notes = LegacyJson };
                    break;
                case ScalarEvidence.Participation:
                    row = new ProjectPartyAssignment { ProjectId = id, ProjectLifetimeId = lifetime, PartyId = Guid.NewGuid(),
                        AssignmentKind = ProjectPartyAssignmentKind.Manager, NodeKey = "saved-task", Notes = LegacyJson };
                    break;
                case ScalarEvidence.Staffing:
                    row = new StaffingRequest { ProjectId = id, ProjectLifetimeId = lifetime, Title = "Retained staffing", Notes = LegacyJson };
                    break;
                case ScalarEvidence.MoveReceiptTuple:
                    row = new ProjectPartyAssignmentMoveReceipt { OperationId = Guid.NewGuid(), SourceProjectId = id, TargetProjectId = Guid.NewGuid(),
                        DatabaseProfileId = Guid.NewGuid(), SourceProjectLifetimeId = lifetime, TargetProjectLifetimeId = Guid.NewGuid(),
                        NodeSetFingerprint = Fingerprint, CompletedAtUtc = SavedAt };
                    break;
                case ScalarEvidence.ContributionProfile:
                case ScalarEvidence.ContributionLifetime:
                    var contribution = Contribution();
                    contribution.DatabaseProfileId = evidence == ScalarEvidence.ContributionProfile ? id : null;
                    contribution.ProjectLifetimeId = evidence == ScalarEvidence.ContributionLifetime ? lifetime : null;
                    row = contribution;
                    break;
                case ScalarEvidence.AdmissionProfile:
                case ScalarEvidence.AdmissionLifetime:
                    var admission = Admission();
                    admission.DatabaseProfileId = evidence == ScalarEvidence.AdmissionProfile ? id : null;
                    admission.ProjectLifetimeId = evidence == ScalarEvidence.AdmissionLifetime ? lifetime : null;
                    row = admission;
                    break;
                case ScalarEvidence.OutputProfile:
                case ScalarEvidence.OutputProject:
                case ScalarEvidence.OutputLifetime:
                    var output = Output();
                    output.DatabaseProfileId = evidence == ScalarEvidence.OutputProfile ? id : null;
                    output.ProjectId = evidence == ScalarEvidence.OutputProject ? id : null;
                    output.ProjectLifetimeId = evidence == ScalarEvidence.OutputLifetime ? lifetime : null;
                    row = output;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(evidence));
            }
            database.Add(row);
            await database.SaveChangesAsync();
        });

    [Theory]
    [InlineData(EvidenceTable.Retirements, false)]
    [InlineData(EvidenceTable.Reservations, false)]
    [InlineData(EvidenceTable.WorkflowContributions, false)]
    [InlineData(EvidenceTable.WorkflowAdmissions, false)]
    [InlineData(EvidenceTable.Retirements, true)]
    [InlineData(EvidenceTable.Reservations, true)]
    [InlineData(EvidenceTable.WorkflowContributions, true)]
    [InlineData(EvidenceTable.WorkflowAdmissions, true)]
    public Task Each_existing_table_import_marker_blocks_down_without_erasing_valid_or_malformed_history(EvidenceTable table, bool malformed)
        => AssertDownBlockedAsync(async database => {
            var imported = ImportChain();
            object row;
            switch (table) {
                case EvidenceTable.Retirements:
                    row = new ProjectRetirementRecord { ProjectId = Guid.NewGuid(), LifetimeId = Guid.NewGuid(), RetiredAtUtc = SavedAt, ImportedHistory = imported };
                    break;
                case EvidenceTable.Reservations:
                    var reservation = Reservation(Guid.NewGuid());
                    reservation.State = ProjectCreationReservationState.Cancelled;
                    reservation.CancelledAtUtc = SavedAt;
                    reservation.ImportedHistory = imported;
                    row = reservation;
                    break;
                case EvidenceTable.WorkflowContributions:
                    var contribution = Contribution();
                    contribution.ImportedHistory = imported;
                    row = contribution;
                    break;
                case EvidenceTable.WorkflowAdmissions:
                    var admission = Admission();
                    admission.ImportedHistory = imported;
                    row = admission;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(table));
            }
            database.Add(row);
            await database.SaveChangesAsync();
            if (malformed) {
                var update = $"UPDATE {TableName(table)} SET \"ImportedHistory\" = {{0}}";
                await database.Database.ExecuteSqlRawAsync(update,
                    new object[] { "{malformed retained provenance" });
            }
        }, malformed);

    [Theory]
    [InlineData(NewTableEvidence.PreparedAsset)]
    [InlineData(NewTableEvidence.MaterializedAsset)]
    [InlineData(NewTableEvidence.CommittedAsset)]
    [InlineData(NewTableEvidence.ImportedAsset)]
    [InlineData(NewTableEvidence.UnboundWorkHistory)]
    [InlineData(NewTableEvidence.BoundWorkHistory)]
    public Task Every_new_table_state_including_unfinished_preparation_prevents_down(NewTableEvidence evidence)
        => AssertDownBlockedAsync(async database => {
            object row = evidence switch {
                NewTableEvidence.PreparedAsset => Asset(),
                NewTableEvidence.MaterializedAsset => Asset(materialized: true),
                NewTableEvidence.CommittedAsset => Asset(materialized: true, committed: true),
                NewTableEvidence.ImportedAsset => Asset(imported: true),
                NewTableEvidence.UnboundWorkHistory => WorkHistory(bound: false),
                NewTableEvidence.BoundWorkHistory => WorkHistory(bound: true),
                _ => throw new ArgumentOutOfRangeException(nameof(evidence))
            };
            database.Add(row);
            await database.SaveChangesAsync();
        });

    [Theory]
    [InlineData(EvidenceTable.WorkflowRuns)]
    [InlineData(EvidenceTable.WorkflowUsage)]
    [InlineData(EvidenceTable.WorkflowLaunches)]
    public Task Process_tool_origin_kind_blocks_down_without_any_saved_project_scope(EvidenceTable table)
        => AssertDownBlockedAsync(async database => {
            object row;
            switch (table) {
                case EvidenceTable.WorkflowRuns:
                    var run = Run();
                    run.OriginKind = WorkflowLaunchOriginKind.ProcessToolInvocation;
                    row = run;
                    break;
                case EvidenceTable.WorkflowUsage:
                    var usage = Usage();
                    usage.OriginKind = WorkflowLaunchOriginKind.ProcessToolInvocation;
                    row = usage;
                    break;
                case EvidenceTable.WorkflowLaunches:
                    var launch = Launch();
                    launch.OriginKind = WorkflowLaunchOriginKind.ProcessToolInvocation;
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

    [Fact]
    public async Task New_tables_round_trip_original_payloads_and_chained_history_across_independent_restart() {
        await using var environment = CanDoItAllTestEnvironment.Create("owner-lifetime-history-restart");
        var profile = environment.CreatePostgreSqlProfile("target");
        var history = WorkHistory(bound: false);
        var asset = Asset(materialized: true, committed: true, imported: true);
        string[] expected;
        await using (var services = CreateProvider(profile)) {
            await using var database = await ContextAsync(services);
            await MigrateCurrentAsync(database);
            database.AddRange(history, asset);
            await database.SaveChangesAsync();
            expected = await SnapshotDataAsync(database);
        }
        for (var restart = 0; restart < 2; restart++) {
            await using var services = CreateProvider(profile);
            await using var database = await ContextAsync(services);
            await MigrateCurrentAsync(database);
            Assert.Equal(expected, await SnapshotDataAsync(database));
            var restoredHistory = await database.Set<ProjectWorkAssignmentHistoryRecord>().AsNoTracking().SingleAsync();
            Assert.Equal(history.ToTransfer(), restoredHistory.ToTransfer());
            Assert.Equal(history.PayloadJson, restoredHistory.PayloadJson);
            Assert.Equal(history.ImportedHistory.Previous, restoredHistory.ImportedHistory.Previous);
            Assert.Null(restoredHistory.ProjectLifetimeId);
            var restoredAsset = await database.Set<ProjectProcessAssetContributionRecord>().AsNoTracking().SingleAsync();
            Assert.Equal(asset.ImportedHistory, restoredAsset.ImportedHistory);
            Assert.Equal(asset.SourceExecutionRunId, restoredAsset.SourceExecutionRunId);
            Assert.Equal(asset.StorageIntentId, restoredAsset.StorageIntentId);
            Assert.Equal(asset.PlanJson, restoredAsset.PlanJson);
            Assert.Equal(asset.MaterializedRequestJson, restoredAsset.MaterializedRequestJson);
            Assert.Equal(asset.ReceiptJson, restoredAsset.ReceiptJson);
            Assert.Empty(await database.Set<ProjectWorkAssignmentRecord>().ToArrayAsync());
            Assert.Empty(await database.Set<ProjectObjectRecord>().ToArrayAsync());
            Assert.Empty(await database.Set<Project>().ToArrayAsync());
        }
    }

    public enum ScalarEvidence {
        Resource, TestPlan, WorkAssignment, Participation, Staffing, MoveReceiptTuple,
        ContributionProfile, ContributionLifetime, AdmissionProfile, AdmissionLifetime, OutputProfile, OutputProject, OutputLifetime
    }

    public enum NewTableEvidence {
        PreparedAsset, MaterializedAsset, CommittedAsset, ImportedAsset, UnboundWorkHistory, BoundWorkHistory
    }
}
