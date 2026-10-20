using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Modules.Workbench;

internal sealed class ProjectTaskPlanProcessPublication(IAgentCatalogReadLeaseStore catalog,
    ProjectProcessLaunchAuthorityService source, IProcessDecisionPublicationFence fence,
    IAgentExecutionProfileGenerationSource generations, ProjectTaskPlanProcessReadAuthority read) {
    internal ProjectTaskPlanSavedPublication Resolve(AgentPlanningDecisionObservation planning, AgentToolSessionAdmission session,
        AgentToolPreparedPayload payload, AgentToolApprovalBinding binding) {
        var original = ProjectTaskPlanProcessCodec.Resolve(planning, session, payload, binding);
        var launcher = (original.Target.Source.SourceAuthority!.Principal as ProcessLaunchPrincipal.AgentExecution)?.Ceiling.AgentId;
        var expected = AgentPlanningPublicationExpectation.Inherited(WorkspaceScopeDescriptor.Organization(session.Profile.ProfileId.ToString("N")),
            session, planning.Expected.RunRevision, planning.Expected.JournalRevision, launcher == session.AgentId ? null : launcher);
        return new(original.Command, original.Target.Caller,
            new ProjectTaskPlanSavedReceiptAccess(original.Command, token => RequireReadAsync(planning.Decision, token)),
            work => new ProjectTaskPlanProcessAuthority(original, catalog, expected, work, binding, source, fence, generations));
    }

    internal Task RequireReadAsync(AgentPlanningDecisionRecord decision, CancellationToken cancellationToken)
        => read.RequireAsync(decision, cancellationToken);
}
