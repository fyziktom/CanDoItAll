using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.AgentFramework.Maf;

internal static class MafWorkflowDisclosureRecovery {
    public static WorkflowPreviewSimulationPlan Restore(WorkflowBackendResumeRequest request, WorkflowDefinition definition) {
        var version = request.ExternalRequest.Continuation?.CompilerContractVersion;
        if (version == WorkflowProviderDisclosureProtocol.Legacy) {
            if (request.DisclosureDeclaration is not null) {
                throw Denied("The legacy Workflow request cannot acquire a newer disclosure declaration.");
            }
            return WorkflowPreviewSimulationPlan.Empty;
        }
        if (version != WorkflowProviderDisclosureProtocol.Current || request.DisclosureDeclaration is not { } declaration) {
            throw Denied("The admitted Workflow recovery requires its original private declaration.");
        }
        declaration.Validate();
        if (declaration.CompilerVersion != version || declaration.RunId != request.Run.RunId ||
                declaration.WorkflowId != request.Run.WorkflowId || declaration.VersionId != request.Run.VersionId ||
                declaration.WorkflowId != definition.Id || declaration.VersionId != definition.VersionId ||
                declaration.DefinitionHash != WorkflowProviderDisclosureContent.Definition(definition) ||
                declaration.SourceHash != WorkflowProviderDisclosureContent.Source(request.Run.Origin)) {
            throw Denied("The private Workflow declaration differs from its exact checkpointed run, source or definition.");
        }
        List<WorkflowPreviewSimulationStep> steps = [];
        foreach (var admitted in declaration.Simulations) {
            if (admitted.Step is not { } step || !definition.Graph.Nodes.Any(node =>
                    node.Id == step.NodeId && node.Settings.ExecutorId == step.SourceExecutorId)) {
                throw Denied("The original simulated Workflow step is unavailable; recovery cannot replace it with real execution.");
            }
            steps.Add(step);
        }
        return new(steps);
    }

    private static WorkflowBackendResumeException Denied(string message)
        => new(WorkflowBackendResumeFailureKind.CompilerContractMismatch, message);
}
