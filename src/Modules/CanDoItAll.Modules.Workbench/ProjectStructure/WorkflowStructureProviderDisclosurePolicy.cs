using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Modules.Workbench;

public sealed class WorkflowStructureProviderDisclosurePolicy(ProjectStructureWorkflowAuthorityService authority)
    : IWorkflowProviderDisclosurePolicy {
    public WorkflowDisclosureOwnerId Owner => WorkflowStructureReadContext.DisclosureOwner;

    public bool RequiresEvidence(WorkflowNode node) => node.Settings.ExecutorId == WorkflowExecutorIds.ProjectStructure &&
        Operation(node) is WorkflowProjectStructureOperation.ListProjects or WorkflowProjectStructureOperation.ReadTree or WorkflowProjectStructureOperation.ReadNode;

    public async ValueTask RequireCurrentAsync(WorkflowRunSnapshot run, WorkflowDefinition definition,
        IReadOnlyList<WorkflowCompletedNodeRead> reads, CancellationToken cancellationToken = default) {
        List<ProjectWriteAdmission> targets = [];
        foreach (var read in reads) {
            var node = definition.Graph.Nodes.Single(candidate => candidate.Id == read.Proof.NodeId);
            if (!RequiresEvidence(node) || read.Evidence.Count == 0) {
                throw Denied();
            }
            var operation = Operation(node);
            var readTargets = new HashSet<ProjectWriteAdmission>();
            for (var index = 0; index < read.Evidence.Count; index++) {
                var part = read.Evidence[index];
                if (part.Owner != Owner || part.SchemaVersion != WorkflowStructureReadEvidence.SchemaVersion) {
                    throw Denied();
                }
                var payload = JsonSerializer.Deserialize<WorkflowStructureReadEvidence>(part.PayloadJson,
                    WorkflowProviderDisclosureContent.JsonOptions) ?? throw Denied();
                if (payload.Operation != operation || payload.PartIndex != index || payload.PartCount != read.Evidence.Count ||
                        payload.Targets is null || payload.Targets.Any(target => target is null || !readTargets.Add(target))) {
                    throw Denied();
                }
            }
            if (operation != WorkflowProjectStructureOperation.ListProjects && readTargets.Count != 1) {
                throw Denied();
            }
            targets.AddRange(readTargets);
        }
        await using var source = await authority.AcquireReadAsync(run, cancellationToken);
        await source.RequireCurrentAsync(targets.Distinct().ToArray(), cancellationToken);
    }

    private static WorkflowProjectStructureOperation Operation(WorkflowNode node)
        => WorkflowExecutorJson.Deserialize<WorkflowProjectStructureExecutorSettings>(node.Settings.ExecutorSettingsJson).Operation;

    private static InvalidOperationException Denied()
        => new("The retained Structure read is missing its exact original operation and target-lifetime evidence.");
}
