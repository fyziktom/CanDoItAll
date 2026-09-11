using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;

namespace CanDoItAll.Tests.Integration;

public sealed partial class OwnerLifetimeHistoryMigrationTests {
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData(JsonEvidence.RunAuthority)]
    [InlineData(JsonEvidence.UsageAuthority)]
    [InlineData(JsonEvidence.LaunchCompletionAuthority)]
    [InlineData(JsonEvidence.LaunchResolvedAuthority)]
    [InlineData(JsonEvidence.AdmissionBindingAuthority)]
    [InlineData(JsonEvidence.AdmissionLaunchAuthority)]
    [InlineData(JsonEvidence.SchedulerAuthority)]
    [InlineData(JsonEvidence.FireAuthority)]
    [InlineData(JsonEvidence.ContributionPlanLifetime)]
    [InlineData(JsonEvidence.ContributionReceiptLifetime)]
    [InlineData(JsonEvidence.ContributionFingerprint)]
    [InlineData(JsonEvidence.OutputPlanLifetime)]
    [InlineData(JsonEvidence.OutputReceiptLifetime)]
    [InlineData(JsonEvidence.OutputFingerprint)]
    public Task Each_saved_json_scope_blocks_down_even_without_new_scalar_or_import_columns(JsonEvidence evidence)
        => AssertDownBlockedAsync(async database => {
            var lifetime = new WorkflowProjectLifetime(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
            var scope = new WorkflowStructureProjectScope([lifetime], [lifetime.ProjectId]);
            var payload = evidence switch {
                JsonEvidence.ContributionPlanLifetime or JsonEvidence.ContributionReceiptLifetime or
                    JsonEvidence.OutputPlanLifetime or JsonEvidence.OutputReceiptLifetime =>
                    JsonSerializer.SerializeToNode(lifetime, WebJson),
                JsonEvidence.ContributionFingerprint or JsonEvidence.OutputFingerprint => JsonValue.Create(Fingerprint),
                _ => new JsonObject { ["projectScope"] = JsonSerializer.SerializeToNode(scope, WebJson) }
            };
            database.Add(JsonRow(evidence, payload));
            await database.SaveChangesAsync();
        });

    [Theory]
    [InlineData(JsonEvidence.RunAuthority, "agent-execution/project-scope-v1")]
    [InlineData(JsonEvidence.UsageAuthority, "authenticated-operator/project-scope-v1")]
    [InlineData(JsonEvidence.LaunchCompletionAuthority, "local-operator/project-scope-v1")]
    [InlineData(JsonEvidence.AdmissionBindingAuthority, "agent-execution/process-tool-scope-v1")]
    [InlineData(JsonEvidence.AdmissionLaunchAuthority, "authenticated-operator/process-tool-scope-v1")]
    [InlineData(JsonEvidence.SchedulerAuthority, "local-operator/process-tool-scope-v1")]
    [InlineData(JsonEvidence.FireAuthority, "agent-execution/project-scope-v1")]
    public Task New_authority_channel_alone_is_retained_when_its_scope_payload_is_missing(JsonEvidence evidence, string channel)
        => AssertDownBlockedAsync(async database => {
            database.Add(JsonRow(evidence, new JsonObject { ["channel"] = channel }));
            await database.SaveChangesAsync();
        });

    [Theory]
    [InlineData(EvidenceTable.WorkflowRuns)]
    [InlineData(EvidenceTable.WorkflowUsage)]
    [InlineData(EvidenceTable.WorkflowLaunches)]
    public Task Process_tool_origin_json_marker_blocks_down_without_the_kind_projection(EvidenceTable table)
        => AssertDownBlockedAsync(async database => {
            database.Add(OriginRow(table, new JsonObject { ["$origin"] = "process-tool-invocation-v1" }.ToJsonString()));
            await database.SaveChangesAsync();
        });

    [Fact]
    public Task Process_tool_origin_in_resolved_completion_is_retained_without_run_origin_or_kind()
        => AssertDownBlockedAsync(async database => {
            database.Add(CompletionOriginRow("resolvedRequest", new JsonObject { ["$origin"] = "process-tool-invocation-v1" }.ToJsonString()));
            await database.SaveChangesAsync();
        });

    [Fact]
    public Task Authority_blocked_workflow_delivery_is_not_downgraded_to_an_older_state()
        => AssertDownBlockedAsync(async database => {
            var admission = Admission();
            admission.Delivery = ProjectWorkflowDeliveryState.AuthorityBlocked;
            database.Add(admission);
            await database.SaveChangesAsync();
        });

    [Theory]
    [InlineData(CleanupEvidence.DeleteSubtreeSource, false)]
    [InlineData(CleanupEvidence.DeleteSubtreeSource, true)]
    [InlineData(CleanupEvidence.DeleteProjectSource, false)]
    [InlineData(CleanupEvidence.DeleteProjectSource, true)]
    [InlineData(CleanupEvidence.MoveSource, false)]
    [InlineData(CleanupEvidence.MoveSource, true)]
    [InlineData(CleanupEvidence.MoveTarget, false)]
    [InlineData(CleanupEvidence.MoveTarget, true)]
    [InlineData(CleanupEvidence.MoveSelectedSource, false)]
    [InlineData(CleanupEvidence.MoveSelectedSource, true)]
    [InlineData(CleanupEvidence.MoveSelectedTarget, false)]
    [InlineData(CleanupEvidence.MoveSelectedTarget, true)]
    public Task Pending_and_completed_cleanup_keep_the_original_scope_in_each_producer_encoding(CleanupEvidence evidence, bool completed)
        => AssertDownBlockedAsync(async database => {
            var row = Cleanup(completed);
            var source = new ProjectAssignmentReference(Guid.NewGuid(), row.ProjectId, Guid.NewGuid());
            var target = new ProjectWriteAdmission(source.DatabaseProfileId, Guid.NewGuid(), Guid.NewGuid());
            switch (evidence) {
                case CleanupEvidence.DeleteSubtreeSource:
                    row.MutationKind = ProjectCrossModuleMutationKind.DeleteSubtree;
                    row.PayloadJson = JsonSerializer.Serialize(new DeleteSubtreeMutationPayload("saved-root", ["saved-node"], 3,
                        SourceReference: source));
                    break;
                case CleanupEvidence.DeleteProjectSource:
                    row.MutationKind = ProjectCrossModuleMutationKind.DeleteProject;
                    row.PayloadJson = JsonSerializer.Serialize(new DeleteProjectMutationPayload(["saved-node"], [],
                        SourceReference: source), WebJson);
                    break;
                case CleanupEvidence.MoveSource:
                case CleanupEvidence.MoveTarget:
                case CleanupEvidence.MoveSelectedSource:
                case CleanupEvidence.MoveSelectedTarget:
                    row.MutationKind = evidence is CleanupEvidence.MoveSelectedSource or CleanupEvidence.MoveSelectedTarget
                        ? ProjectCrossModuleMutationKind.MoveSelectedNodes : ProjectCrossModuleMutationKind.MoveDescendants;
                    row.PayloadJson = JsonSerializer.Serialize(new MoveDescendantsMutationPayload(row.ProjectId, target.ProjectId,
                        "saved-root", ["saved-node"], ["saved-root"],
                        SourceReference: evidence is CleanupEvidence.MoveSource or CleanupEvidence.MoveSelectedSource ? source : null,
                        ExpectedTargetAdmission: evidence is CleanupEvidence.MoveTarget or CleanupEvidence.MoveSelectedTarget ? target : null));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(evidence));
            }
            database.Add(row);
            await database.SaveChangesAsync();
        });

    [Theory]
    [InlineData(EvidenceTable.WorkflowRuns)]
    [InlineData(EvidenceTable.WorkflowUsage)]
    [InlineData(EvidenceTable.WorkflowLaunches)]
    [InlineData(EvidenceTable.Cleanup)]
    public Task Malformed_saved_authority_or_cleanup_is_preserved_and_blocks_destructive_down(EvidenceTable table)
        => AssertDownBlockedAsync(async database => {
            const string malformed = "{malformed saved evidence with original trailing spaces  ";
            if (table == EvidenceTable.Cleanup) {
                var row = Cleanup();
                row.PayloadJson = malformed;
                database.Add(row);
            } else {
                var row = table == EvidenceTable.WorkflowLaunches ? Launch() : OriginRow(table, malformed);
                if (row is WorkflowLaunchIdempotencyRecordEntity launch) {
                    launch.CompletionJson = malformed;
                }
                database.Add(row);
            }
            await database.SaveChangesAsync();
        }, malformed: true);

    private static object JsonRow(JsonEvidence evidence, JsonNode? payload) {
        string Wrap(string name) => new JsonObject { [name] = payload }.ToJsonString();
        switch (evidence) {
            case JsonEvidence.RunAuthority:
                return OriginRow(EvidenceTable.WorkflowRuns, Wrap("structureAuthority"));
            case JsonEvidence.UsageAuthority:
                return OriginRow(EvidenceTable.WorkflowUsage, Wrap("structureAuthority"));
            case JsonEvidence.LaunchCompletionAuthority:
                return OriginRow(EvidenceTable.WorkflowLaunches, Wrap("structureAuthority"));
            case JsonEvidence.LaunchResolvedAuthority:
                return CompletionOriginRow("resolvedRequest", Wrap("structureAuthority"));
            case JsonEvidence.AdmissionBindingAuthority:
            case JsonEvidence.AdmissionLaunchAuthority:
                var admission = Admission();
                admission.AdmissionJson = evidence == JsonEvidence.AdmissionBindingAuthority
                    ? new JsonObject { ["binding"] = new JsonObject { ["authority"] = payload } }.ToJsonString()
                    : new JsonObject { ["launchIntent"] = new JsonObject {
                        ["origin"] = new JsonObject { ["structureAuthority"] = payload } } }.ToJsonString();
                return admission;
            case JsonEvidence.SchedulerAuthority:
                var plan = Schedule();
                plan.StructureAuthorityJson = payload!.ToJsonString();
                return plan;
            case JsonEvidence.FireAuthority:
                var fire = Fire();
                fire.SnapshotJson = Wrap("authority");
                return fire;
            case JsonEvidence.ContributionPlanLifetime:
            case JsonEvidence.ContributionReceiptLifetime:
            case JsonEvidence.ContributionFingerprint:
                var contribution = Contribution();
                if (evidence == JsonEvidence.ContributionReceiptLifetime) {
                    contribution.ReceiptJson = Wrap("projectLifetime");
                } else {
                    contribution.PlanJson = Wrap(evidence == JsonEvidence.ContributionFingerprint ? "sourceAuthorityFingerprint" : "projectLifetime");
                }
                return contribution;
            case JsonEvidence.OutputPlanLifetime:
            case JsonEvidence.OutputReceiptLifetime:
            case JsonEvidence.OutputFingerprint:
                var output = Output();
                if (evidence == JsonEvidence.OutputReceiptLifetime) {
                    output.ReceiptJson = Wrap("projectLifetime");
                } else {
                    output.PlanJson = Wrap(evidence == JsonEvidence.OutputFingerprint ? "sourceAuthorityFingerprint" : "projectLifetime");
                }
                return output;
            default:
                throw new ArgumentOutOfRangeException(nameof(evidence));
        }
    }

    private static object OriginRow(EvidenceTable table, string origin) {
        switch (table) {
            case EvidenceTable.WorkflowRuns:
                var run = Run();
                run.OriginJson = origin;
                return run;
            case EvidenceTable.WorkflowUsage:
                var usage = Usage();
                usage.OriginJson = origin;
                return usage;
            case EvidenceTable.WorkflowLaunches:
                return CompletionOriginRow("run", origin);
            default:
                throw new ArgumentOutOfRangeException(nameof(table));
        }
    }

    private static WorkflowLaunchIdempotencyRecordEntity CompletionOriginRow(string savedMember, string origin) {
        var launch = Launch();
        launch.CompletionJson = new JsonObject { [savedMember] = new JsonObject { ["origin"] = JsonNode.Parse(origin) } }.ToJsonString();
        return launch;
    }

    public enum JsonEvidence {
        RunAuthority, UsageAuthority, LaunchCompletionAuthority, LaunchResolvedAuthority, AdmissionBindingAuthority, AdmissionLaunchAuthority,
        SchedulerAuthority, FireAuthority, ContributionPlanLifetime, ContributionReceiptLifetime, ContributionFingerprint,
        OutputPlanLifetime, OutputReceiptLifetime, OutputFingerprint
    }

    public enum CleanupEvidence {
        DeleteSubtreeSource, DeleteProjectSource, MoveSource, MoveTarget, MoveSelectedSource, MoveSelectedTarget
    }
}
