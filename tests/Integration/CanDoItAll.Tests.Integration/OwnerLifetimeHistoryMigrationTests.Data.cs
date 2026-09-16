using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Resources;
using CanDoItAll.Modules.SchedulerPlanner;
using CanDoItAll.Modules.TestLab;
using CanDoItAll.Modules.Workbench;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Integration;

public sealed partial class OwnerLifetimeHistoryMigrationTests {
    private const string LegacyJson = "{ \"legacy\": { \"unknown\": 7 }, \"notes\": \"Original text  \" }";
    private const string Fingerprint = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string LegacyOriginJson = """{"structureAuthority":{"projectScope":null},"legacy":"Original origin  "}""";
    private const string LegacyPlanJson = """{"projectLifetime":null,"sourceAuthorityFingerprint":null,"legacy":"Original output  "}""";
    private const string LegacyAdmissionJson = """
        {"binding":{"authority":{"projectScope":null}},"launchIntent":{"origin":{"structureAuthority":{"projectScope":null}}},"legacy":"Original admission  "}
        """;

    private static ProjectCreationReservationRecord Reservation(Guid projectId) => new() {
        Id = Guid.NewGuid(), DatabaseProfileId = Guid.NewGuid(), ProjectId = projectId, LifetimeId = Guid.NewGuid(),
        RequesterId = Guid.NewGuid(), State = ProjectCreationReservationState.Reserved, CreatedAtUtc = SavedAt
    };

    private static RetainedEvidenceImport ImportChain() => new(Guid.NewGuid(), Guid.NewGuid(), new(Guid.NewGuid(), Guid.NewGuid()));

    private static ProjectWorkflowContributionRecord Contribution() => new() {
        RunId = Guid.NewGuid(), OccurrencePath = "retained-output", Slot = 2, ProjectId = Guid.NewGuid(), NativeObjectId = Guid.NewGuid(),
        PlanJson = LegacyJson, RequestJson = LegacyJson, NodeJson = LegacyJson, ReceiptJson = LegacyJson, PreparedAtUtc = SavedAt
    };

    private static ProjectWorkflowAdmissionRecord Admission() => new() {
        IntentId = Guid.NewGuid(), ProjectId = Guid.NewGuid(), NodeId = "saved-workflow", NativeNodeId = Guid.NewGuid(),
        RunId = Guid.NewGuid(), Sequence = 13, AdmissionJson = LegacyJson, StatusJson = LegacyJson,
        Delivery = ProjectWorkflowDeliveryState.TargetDeleted, DeliveryFinished = true, NextAttemptAtUtc = SavedAt
    };

    private static WorkflowStructureOutputRecord Output() => new() {
        RunId = Guid.NewGuid(), OccurrencePath = "retained-output", Slot = 2, PlanJson = LegacyJson,
        ReceiptJson = LegacyJson, NextInspectionAtUtc = SavedAt, IsComplete = true
    };

    private static WorkflowRunRecordEntity Run() => new() {
        RunId = Guid.NewGuid(), WorkflowId = Guid.NewGuid(), VersionId = Guid.NewGuid(), OriginJson = LegacyJson,
        CreatedAtUtc = SavedAt, UpdatedAtUtc = SavedAt, TerminalAtUtc = SavedAt
    };

    private static WorkflowUsageObservationRecordEntity Usage() => new() {
        Id = Guid.NewGuid(), RunId = Guid.NewGuid(), WorkflowId = Guid.NewGuid(), VersionId = Guid.NewGuid(),
        InvocationId = Guid.NewGuid(), Attempt = 2, NodeId = "saved-usage", OriginJson = LegacyJson,
        RecordedAtUtc = SavedAt, ProviderName = "Preserved provider", ProviderNameKey = "PRESERVED PROVIDER",
        Model = "Preserved model", ModelKey = "PRESERVED MODEL", TotalTokens = 17, CostUsd = 0.123456789m
    };

    private static WorkflowLaunchIdempotencyRecordEntity Launch() => new() {
        Id = Guid.NewGuid(), CallerKey = Guid.NewGuid().ToString("N"), WorkflowId = Guid.NewGuid(), RequestedVersionId = Guid.NewGuid(),
        OriginScopeKey = Fingerprint, Fingerprint = Fingerprint, CanonicalInputHash = Fingerprint,
        ClaimToken = Guid.NewGuid(), ReservedRunId = Guid.NewGuid(), ClaimedAtUtc = SavedAt,
        LeaseExpiresAtUtc = SavedAt.AddHours(1), State = WorkflowLaunchIdempotencyClaimState.Completed,
        CompletionJson = LegacyJson, CompletedAtUtc = SavedAt, ReplayCount = 9
    };

    private static SchedulerPlan Schedule() => new() {
        Name = "Retained schedule", Description = "Original schedule notes  ", TargetId = Guid.NewGuid(),
        TargetKind = SchedulerPlanTargetKind.Workflow, TargetNameSnapshot = "Original workflow", InputJson = LegacyJson,
        IsEnabled = false, SchedulerTriggerId = Guid.NewGuid(), SchedulerTriggerKey = "retained-schedule", CreatedAtUtc = SavedAt, UpdatedAtUtc = SavedAt
    };

    private static SchedulerFireAdmissionRecord Fire() => new() {
        Id = Guid.NewGuid(), PlanId = Guid.NewGuid(), DedupeKey = Guid.NewGuid().ToString("N"), SnapshotJson = LegacyJson,
        SnapshotFingerprint = Fingerprint, PreparedWorkflowRunId = Guid.NewGuid(), State = SchedulerFireAdmissionState.Observed,
        Generation = 7, OutcomeJson = LegacyJson, CreatedAtUtc = SavedAt, UpdatedAtUtc = SavedAt
    };

    private static ProjectCrossModuleMutationRecord Cleanup(bool completed = true) {
        var projectId = Guid.NewGuid();
        return new() {
            ProjectId = projectId, ScopeNodeKey = "retained-root", MutationKind = ProjectCrossModuleMutationKind.MoveDescendants,
            Status = completed ? ProjectCrossModuleMutationStatus.Completed : ProjectCrossModuleMutationStatus.WorkbenchCommitted,
            PayloadJson = JsonSerializer.Serialize(new MoveDescendantsMutationPayload(projectId, Guid.NewGuid(), "saved-root", ["saved-node"], ["saved-root"])),
            ErrorMessage = "Preserved cleanup outcome", AttemptCount = 3,
            CompletedAtUtc = completed ? SavedAt : null, CreatedAtUtc = SavedAt, UpdatedAtUtc = SavedAt
        };
    }

    private static ProjectProcessAssetContributionRecord Asset(bool materialized = false, bool committed = false, bool imported = false) => new() {
        IntentId = Guid.NewGuid(), DatabaseProfileId = Guid.NewGuid(), ProjectId = Guid.NewGuid(), ProjectLifetimeId = Guid.NewGuid(),
        SourceExecutionRunId = Guid.NewGuid(), NativeObjectId = Guid.NewGuid(), StorageIntentId = Guid.NewGuid(),
        PlanJson = LegacyJson, PlanFingerprint = Fingerprint, PreparedAtUtc = SavedAt,
        MaterializedRequestJson = materialized ? LegacyJson : string.Empty,
        MaterializedFingerprint = materialized ? Fingerprint : string.Empty,
        NodeJson = committed ? LegacyJson : string.Empty, ReceiptJson = committed ? LegacyJson : string.Empty,
        ImportedHistory = imported ? ImportChain() : null
    };

    private static ProjectWorkAssignmentHistoryRecord WorkHistory(bool bound) {
        var original = new ProjectWorkAssignmentRecord {
            Id = Guid.NewGuid(), ProjectId = Guid.NewGuid(), ProjectLifetimeId = bound ? Guid.NewGuid() : null,
            PartyId = Guid.NewGuid(), PartyOrganizationAffiliationId = Guid.NewGuid(), NodeKey = "saved-native-task",
            PhaseName = "Preserved phase", OpportunityId = Guid.NewGuid(), AllocationPercent = 12.123456789m,
            StartsAtUtc = SavedAt, EndsAtUtc = SavedAt.AddDays(7), IsPrimary = true,
            Source = "Original selected-project source", Notes = "Original assignment\nwith trailing spaces  "
        };
        var captured = ProjectWorkAssignmentHistoryRecord.Capture(original, Guid.NewGuid());
        return new(captured with { PayloadJson = " \n" + captured.PayloadJson + "\n ", ImportedHistory = ImportChain() });
    }

    private static async Task<Project> SeedLegacyAsync(AppDbContext database, bool includeUnambiguous) {
        var project = new Project { Name = "Unambiguous saved project", Slug = Guid.NewGuid().ToString("N"), CreatedAtUtc = SavedAt, UpdatedAtUtc = SavedAt };
        var recreated = new Project { Name = "Recreated saved ID", Slug = Guid.NewGuid().ToString("N"), CreatedAtUtc = SavedAt, UpdatedAtUtc = SavedAt };
        database.AddRange(project, recreated);
        await database.SaveChangesAsync();
        await database.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Projects_ProjectRetirements" ("LifetimeId", "ProjectId", "RetiredAtUtc")
            VALUES ({Guid.NewGuid()}, {recreated.Id}, {SavedAt})
            """);
        if (includeUnambiguous) {
            await InsertLegacyReferencesAsync(database, project.Id);
        }
        await InsertLegacyReferencesAsync(database, recreated.Id);
        await InsertLegacyReferencesAsync(database, Guid.NewGuid());
        await InsertLegacyReferencesAsync(database, Guid.Empty);
        await InsertLegacyReferencesAsync(database, null);
        await InsertLegacyOwnerEvidenceAsync(database);
        var usage = Usage();
        usage.OriginJson = LegacyOriginJson;
        var launch = CompletionOriginRow("run", LegacyOriginJson);
        var schedule = Schedule();
        schedule.StructureAuthorityJson = """{"projectScope":null}""";
        var fire = Fire();
        fire.SnapshotJson = """{"authority":{"projectScope":null}}""";
        var subtree = Cleanup();
        subtree.MutationKind = ProjectCrossModuleMutationKind.DeleteSubtree;
        subtree.PayloadJson = JsonSerializer.Serialize(new DeleteSubtreeMutationPayload("saved-root", ["saved-node"], 3));
        var deletion = Cleanup();
        deletion.MutationKind = ProjectCrossModuleMutationKind.DeleteProject;
        deletion.PayloadJson = JsonSerializer.Serialize(new DeleteProjectMutationPayload(["saved-node"], []), WebJson);
        database.AddRange(usage, launch, schedule, fire, Cleanup(), subtree, deletion);
        await database.SaveChangesAsync();
        return project;
    }

    private static async Task InsertLegacyReferencesAsync(AppDbContext database, Guid? projectId) {
        var notes = "Original reference\nwith trailing spaces  ";
        if (projectId is not null) {
            await database.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "Resources_ProjectResources" ("Id", "ProjectId", "OwnerPartyId", "MaintainerPartyId", "ResourceKind", "Name", "Description",
                    "ConnectorPluginKey", "ConfigSchemaVersion", "LocationOrIdentifier", "ConfigJson", "LinkedSecretIdsJson", "ValidationStatus", "Sensitivity",
                    "SupportsPreview", "SupportsIndexing", "CreatedAtUtc", "UpdatedAtUtc")
                VALUES ({Guid.NewGuid()}, {projectId}, NULL, NULL, NULL, 'Saved resource', {notes}, 'saved-connector', 'v1', 'saved-reference',
                    {LegacyJson}, '[]', {0}, {0}, TRUE, FALSE, {SavedAt}, {SavedAt});
                INSERT INTO "Workbench_WorkAssignments" ("Id", "ProjectId", "PartyId", "PartyOrganizationAffiliationId", "NodeKey", "PhaseName", "OpportunityId",
                    "AllocationPercent", "StartsAtUtc", "EndsAtUtc", "IsPrimary", "Source", "Notes")
                VALUES ({Guid.NewGuid()}, {projectId}, {Guid.NewGuid()}, NULL, 'saved-task', 'Original phase', NULL, {12.123456789m},
                    {SavedAt}, {SavedAt.AddDays(3)}, TRUE, 'Original Work source', {notes});
                INSERT INTO "CrmHr_ProjectPartyAssignments" ("Id", "ProjectId", "PartyId", "PartyOrganizationAffiliationId", "AssignmentKind", "NodeKey", "PhaseName", "OpportunityId",
                    "AllocationPercent", "StartsAtUtc", "EndsAtUtc", "IsPrimary", "Source", "Notes")
                VALUES ({Guid.NewGuid()}, {projectId}, {Guid.NewGuid()}, NULL, {nameof(ProjectPartyAssignmentKind.Manager)}, 'saved-task', 'Original phase', NULL,
                    {23.123456789m}, {SavedAt}, {SavedAt.AddDays(4)}, TRUE, 'Original CRM source', {notes});
                """);
        }
        await database.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "TestLab_TestPlans" ("Id", "ProjectId", "ResponsiblePartyId", "Title", "Phase", "CoverageGoal", "PlaywrightSpecPath", "CreatedAtUtc", "UpdatedAtUtc")
            VALUES ({Guid.NewGuid()}, {projectId}, NULL, 'Saved plan', 'Original phase', {notes}, 'saved.spec.cs', {SavedAt}, {SavedAt});
            INSERT INTO "CrmHr_StaffingRequests" ("Id", "ProjectId", "RequestedByPartyId", "DeliveryUnitPartyId", "Title", "NeededRole", "NeededSkillsJson",
                "StartDateUtc", "EndDateUtc", "AllocationPercent", "Status", "Notes")
            VALUES ({Guid.NewGuid()}, {projectId}, NULL, NULL, 'Saved staffing', 'Original role', '[]', {SavedAt}, {SavedAt.AddDays(5)}, {34.125m},
                {nameof(StaffingRequestStatus.Draft)}, {notes});
            """);
    }

    private static Task InsertLegacyOwnerEvidenceAsync(AppDbContext database) => database.Database.ExecuteSqlInterpolatedAsync($"""
        INSERT INTO "CrmHr_ProjectPartyAssignmentMoveReceipts" ("OperationId", "SourceProjectId", "TargetProjectId", "NodeSetFingerprint", "CompletedAtUtc")
        VALUES ({Guid.NewGuid()}, {Guid.NewGuid()}, {Guid.NewGuid()}, {Fingerprint}, {SavedAt});
        INSERT INTO "Projects_ProjectCreationReservations" ("Id", "DatabaseProfileId", "ProjectId", "LifetimeId", "RequesterId", "ParentProjectId", "ParentLifetimeId",
            "State", "CreatedAtUtc", "ConsumedAtUtc", "CancelledAtUtc")
        VALUES ({Guid.NewGuid()}, {Guid.NewGuid()}, {Guid.NewGuid()}, {Guid.NewGuid()}, {Guid.NewGuid()}, NULL, NULL,
            {(int)ProjectCreationReservationState.Cancelled}, {SavedAt}, {SavedAt}, {SavedAt});
        INSERT INTO "Workbench_WorkflowContributionReceipts" ("RunId", "OccurrencePath", "Slot", "ProjectId", "NativeObjectId", "PlanJson", "RequestJson",
            "StoragePlacementIntentId", "PreparedAtUtc", "NodeJson", "ReceiptJson")
        VALUES ({Guid.NewGuid()}, 'legacy-output', {2}, {Guid.NewGuid()}, {Guid.NewGuid()}, {LegacyPlanJson}, {LegacyJson}, NULL, {SavedAt}, {LegacyJson}, {LegacyPlanJson});
        INSERT INTO "Workbench_WorkflowAdmissions" ("IntentId", "ProjectId", "NodeId", "NativeNodeId", "RunId", "Sequence", "AdmissionJson", "Delivery",
            "StatusJson", "DeliveryFinished", "NextAttemptAtUtc", "ObservedRunUpdatedAtUtc")
        VALUES ({Guid.NewGuid()}, {Guid.NewGuid()}, 'saved-workflow', {Guid.NewGuid()}, {Guid.NewGuid()}, {13L}, {LegacyAdmissionJson},
            {(int)ProjectWorkflowDeliveryState.TargetDeleted}, {LegacyJson}, TRUE, {SavedAt}, {SavedAt});
        INSERT INTO "AgentFramework_WorkflowStructureOutputs" ("RunId", "OccurrencePath", "Slot", "PlanJson", "ReceiptJson", "NextInspectionAtUtc",
            "AssetDispatchStarted", "StoragePlacementIntentId", "IsComplete")
        VALUES ({Guid.NewGuid()}, 'legacy-output', {2}, {LegacyPlanJson}, {LegacyPlanJson}, {SavedAt}, FALSE, NULL, TRUE);
        INSERT INTO "AgentFramework_WorkflowRuns" ("RunId", "WorkflowId", "VersionId", "State", "Backend", "BackendRunId", "Summary", "CreatedAtUtc",
            "UpdatedAtUtc", "TerminalAtUtc", "ReportingActivityAtUtc", "OriginJson", "OriginKind", "OriginProjectId", "OriginProcessRunId")
        VALUES ({Guid.NewGuid()}, {Guid.NewGuid()}, {Guid.NewGuid()}, {0}, {0}, 'saved-backend', 'Original summary', {SavedAt}, {SavedAt}, {SavedAt},
            {SavedAt}, {LegacyOriginJson}, NULL, NULL, NULL);
        """);
}
