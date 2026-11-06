using CanDoItAll.Processes.Persistence;
using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Resources;
using CanDoItAll.Modules.SchedulerPlanner;
using CanDoItAll.Modules.TestLab;
using CanDoItAll.Modules.Workbench;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Integration;

internal static class HistoricalProjectMigrationRows {
    internal static Task InsertCleanupAsync(AppDbContext database, ProjectCrossModuleMutationRecord row)
        => database.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Workbench_ProjectCrossModuleMutations"
                ("Id", "ProjectId", "ScopeNodeKey", "MutationKind", "Status", "PayloadJson", "ErrorMessage",
                 "AttemptCount", "CreatedAtUtc", "UpdatedAtUtc", "CompletedAtUtc", "ApprovalState", "LastAttemptAtUtc")
            VALUES ({row.Id}, {row.ProjectId}, {row.ScopeNodeKey}, {(int)row.MutationKind}, {(int)row.Status},
                {row.PayloadJson}, {row.ErrorMessage}, {row.AttemptCount}, {row.CreatedAtUtc}, {row.UpdatedAtUtc}, {row.CompletedAtUtc},
                {(int)row.ApprovalState}, {row.LastAttemptAtUtc})
            """);

    internal static Task InsertReservationAsync(AppDbContext database, ProjectCreationReservationRecord row) {
        var imported = row.ImportedHistory is null ? null : JsonSerializer.Serialize(row.ImportedHistory, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return database.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Projects_ProjectCreationReservations"
                ("Id", "DatabaseProfileId", "ProjectId", "LifetimeId", "RequesterId", "ParentProjectId", "ParentLifetimeId",
                 "State", "CreatedAtUtc", "ConsumedAtUtc", "CancelledAtUtc", "ImportedHistory")
            VALUES ({row.Id}, {row.DatabaseProfileId}, {row.ProjectId}, {row.LifetimeId}, {row.RequesterId},
                {row.ParentProjectId}, {row.ParentLifetimeId}, {(int)row.State}, {row.CreatedAtUtc}, {row.ConsumedAtUtc},
                {row.CancelledAtUtc}, {imported})
            """);
    }

    internal static Task InsertResourceAsync(AppDbContext database, ProjectResource row)
        => database.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Resources_ProjectResources"
                ("Id", "ProjectId", "ProjectLifetimeId", "OwnerPartyId", "MaintainerPartyId", "ResourceKind", "Name", "Description",
                 "ConnectorPluginKey", "ConfigSchemaVersion", "LocationOrIdentifier", "ConfigJson", "LinkedSecretIdsJson",
                 "ValidationStatus", "Sensitivity", "SupportsPreview", "SupportsIndexing", "CreatedAtUtc", "UpdatedAtUtc")
            VALUES ({row.Id}, {row.ProjectId}, {row.ProjectLifetimeId}, {row.OwnerPartyId}, {row.MaintainerPartyId}, {(int?)row.ResourceKind},
                {row.Name}, {row.Description}, {row.ConnectorPluginKey}, {row.ConfigSchemaVersion}, {row.LocationOrIdentifier},
                {row.ConfigJson}, {row.LinkedSecretIdsJson}, {(int)row.ValidationStatus}, {(int)row.Sensitivity},
                {row.SupportsPreview}, {row.SupportsIndexing}, {row.CreatedAtUtc}, {row.UpdatedAtUtc})
            """);

    internal static async Task InsertOwnerEvidenceAsync(AppDbContext database, object row) {
        switch (row) {
            case ProjectCrossModuleMutationRecord cleanup:
                await InsertCleanupAsync(database, cleanup);
                break;
            case ProjectCreationReservationRecord reservation:
                await InsertReservationAsync(database, reservation);
                break;
            case ProjectResource resource:
                await InsertResourceAsync(database, resource);
                break;
            case SchedulerPlan schedule:
                await InsertSchedulerPlanAsync(database, schedule);
                break;
            case TestPlan plan:
                await InsertTestPlanAsync(database, plan);
                break;
            default:
                database.Add(row);
                await database.SaveChangesAsync();
                break;
        }
    }

    internal static Task InsertWorkflowDefinitionAsync(AppDbContext database, WorkflowDefinitionRecord row)
        => database.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "AgentFramework_WorkflowDefinitions"
                ("WorkflowId", "VersionId", "Revision", "Name", "Description", "Status", "PreferredBackend", "DefinitionJson",
                 "InstructionSnapshotSchemaVersion", "CreatedAtUtc", "UpdatedAtUtc")
            VALUES ({row.WorkflowId}, {row.VersionId}, {row.Revision}, {row.Name}, {row.Description}, {(int)row.Status},
                {(int)row.PreferredBackend}, {row.DefinitionJson}, {row.InstructionSnapshotSchemaVersion}, {row.CreatedAtUtc}, {row.UpdatedAtUtc})
            """);

    private static Task InsertSchedulerPlanAsync(AppDbContext database, SchedulerPlan row)
        => database.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "SchedulerPlanner_Plans" ("Id", "Name", "Description", "TargetKind", "TargetId", "TargetVersionId",
                "TargetNameSnapshot", "CronExpression", "CronDescription", "TimeZoneId", "MisfirePolicy", "IsEnabled",
                "StartAtUtc", "EndAtUtc", "InputJson", "StructureAuthorityJson", "AutomationTriggerId", "AutomationTriggerKey",
                "NextPlannedFireAtUtc", "LastFiredAtUtc", "LastError", "CreatedAtUtc", "UpdatedAtUtc")
            VALUES ({row.Id}, {row.Name}, {row.Description}, {(int)row.TargetKind}, {row.TargetId}, {row.TargetVersionId},
                {row.TargetNameSnapshot}, {row.CronExpression}, {row.CronDescription}, {row.TimeZoneId}, {(int)row.MisfirePolicy},
                {row.IsEnabled}, {row.StartAtUtc}, {row.EndAtUtc}, {row.InputJson}, {row.StructureAuthorityJson}, {row.SchedulerTriggerId},
                {row.SchedulerTriggerKey}, {row.NextPlannedFireAtUtc}, {row.LastFiredAtUtc}, {row.LastError}, {row.CreatedAtUtc}, {row.UpdatedAtUtc})
            """);

    private static Task InsertTestPlanAsync(AppDbContext database, TestPlan row)
        => database.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "TestLab_TestPlans" ("Id", "ProjectId", "ProjectLifetimeId", "ResponsiblePartyId", "Title", "Phase",
                "CoverageGoal", "PlaywrightSpecPath", "CreatedAtUtc", "UpdatedAtUtc")
            VALUES ({row.Id}, {row.ProjectId}, {row.ProjectLifetimeId}, {row.ResponsiblePartyId}, {row.Title},
                {row.Phase}, {row.CoverageGoal}, {row.PlaywrightSpecPath}, {row.CreatedAtUtc}, {row.UpdatedAtUtc})
            """);

    internal static Task InsertPreparedLaunchAsync(AppDbContext context, ProcessPreparedLaunchEntity row)
        => context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "process_prepared_launches"
                ("Id", "CallerIntentId", "RunId", "PlanId", "RequestFingerprint", "PreparationFingerprint", "PayloadJson",
                 "PreparedAtUtc", "AcceptedAtUtc", "Execute", "State", "ContinuationOwner", "ContinuationGeneration",
                 "ContinuationLeaseExpiresAtUtc", "LinkDeliveryState", "DeliveredLinkId", "LinkConflictReason", "PublicFailure")
            VALUES ({row.Id}, {row.CallerIntentId}, {row.RunId}, {row.PlanId}, {row.RequestFingerprint}, {row.PreparationFingerprint},
                {row.PayloadJson}, {row.PreparedAtUtc}, {row.AcceptedAtUtc}, {row.Execute}, {row.State.ToString()},
                {row.ContinuationOwner}, {row.ContinuationGeneration}, {row.ContinuationLeaseExpiresAtUtc},
                {row.LinkDeliveryState.ToString()}, {row.DeliveredLinkId}, {row.LinkConflictReason?.ToString()}, {row.PublicFailure})
            """);

    internal static Task InsertAssignmentAsync(AppDbContext context, ProcessRuntimeStepAssignmentEntity row)
        => context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "process_runtime_step_assignments"
                ("RunId", "StepInstanceId", "PlanId", "StepKey", "RoleKey", "RoleResourceKey", "RoleDisplayName", "ExecutorKind",
                 "ExecutorId", "ExecutorDisplayName", "WorkflowId", "WorkflowVersionId", "WorkflowOutputMapping", "Prompt",
                 "ReadinessHash", "AssignmentReason", "ProducedArtifactSlotIds", "RequiredArtifactSlotIds", "AllowedOperations",
                 "OperationTargetScope", "LaunchVariablesJson", "CapabilityScopeJson", "BranchGateSourceStepKey", "BranchGateRequiredOutcomeKey", "CreatedAtUtc")
            VALUES ({row.RunId}, {row.StepInstanceId}, {row.PlanId}, {row.StepKey}, {row.RoleKey}, {row.RoleResourceKey}, {row.RoleDisplayName},
                {row.ExecutorKind}, {row.ExecutorId}, {row.ExecutorDisplayName}, {row.WorkflowId}, {row.WorkflowVersionId}, {row.WorkflowOutputMapping},
                {row.Prompt}, {row.ReadinessHash}, {row.AssignmentReason}, {row.ProducedArtifactSlotIds}, {row.RequiredArtifactSlotIds},
                {row.AllowedOperations}, {row.OperationTargetScope}, {row.LaunchVariablesJson}, {row.CapabilityScopeJson},
                {row.BranchGateSourceStepKey}, {row.BranchGateRequiredOutcomeKey}, {row.CreatedAtUtc})
            """);
}
