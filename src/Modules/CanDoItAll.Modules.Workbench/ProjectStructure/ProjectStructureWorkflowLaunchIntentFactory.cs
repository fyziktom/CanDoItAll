using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectStructureWorkflowLaunchIntentFactory {
    public WorkflowLaunchIntent Create(ProjectWorkflowAdmission admission) {
        ArgumentNullException.ThrowIfNull(admission);
        if (admission.LaunchIntent.Origin is not WorkflowLaunchOrigin.ProjectStructureNode { StructureAdmission: { } binding } ||
            binding.RunId != admission.Binding.RunId || binding.IntentId != admission.Binding.IntentId) {
            throw new InvalidOperationException("A Structure launch requires its exact durable admission.");
        }

        return admission.LaunchIntent;
    }
}
