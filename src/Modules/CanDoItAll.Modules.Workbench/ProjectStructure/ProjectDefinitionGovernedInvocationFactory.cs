using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Modules.Workbench;

internal sealed class ProjectDefinitionGovernedInvocationFactory(ProjectAgentSourceMutationAuthority agents,
    ProjectProcessExecutionMutationService processes, IAgentCatalogReadLeaseStore catalog, ProjectWriteAdmissionService projects) {
    internal ProjectDefinitionInvocation ForAgent(ProjectAgentMutationAdmission original,
        AgentToolSessionObservation verifiedSession, AgentToolAdmittedInvocation verifiedProposal,
        IReadOnlyCollection<ProjectWriteAdmission> originalTargets) {
        var binding = ProjectDefinitionGovernedBindings.ForAgent(original, verifiedSession, verifiedProposal);
        return new(binding.Caller, new ProjectDefinitionAgentReceiptAccess(original, catalog, projects),
            agents.Bind(original, originalTargets), binding.Effect);
    }

    internal ProjectDefinitionInvocation ForProcess(ProjectProcessMutationAdmission original, ProjectProcessProjectOperation operation,
        IReadOnlyCollection<ProjectWriteAdmission> originalTargets, AgentToolAdmittedInvocation? verifiedProposal = null) {
        var binding = ProjectDefinitionGovernedBindings.ForProcess(original, operation, verifiedProposal);
        return new(binding.Caller, new ProjectDefinitionProcessReceiptAccess(original, processes, catalog, projects),
            processes.BindProjectSource(original, originalTargets, operation), binding.Effect);
    }
}
