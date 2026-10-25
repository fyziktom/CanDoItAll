using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.Workbench;

internal sealed class ProjectTaskPlanAgentPublication(IAgentCatalogReadLeaseStore catalog,
    ProjectAgentSourceMutationAuthority source, ProjectTaskPlanAgentParticipant participant) {
    internal ProjectTaskPlanSavedPublication Resolve(AgentPlanningDecisionObservation planning, AgentToolSessionAdmission session,
        AgentToolPreparedPayload payload, AgentToolApprovalBinding binding) {
        var original = ProjectTaskPlanAgentCodec.Resolve(planning, session, payload, binding);
        var expected = new AgentPlanningPublicationExpectation(WorkspaceScopeDescriptor.Organization(session.Profile.ProfileId.ToString("N")),
            session, planning.Expected.RunRevision, planning.Expected.JournalRevision);
        return new(original.Command, original.Target.Caller,
            new ProjectTaskPlanSavedReceiptAccess(original.Command, token => RequireReadAsync(planning.Decision, token)),
            work => new ProjectTaskPlanAgentAuthority(original, catalog, expected, work, binding, source));
    }

    internal Task RequireReadAsync(AgentPlanningDecisionRecord decision, CancellationToken cancellationToken)
        => participant.RequireCurrentReadAsync(decision, cancellationToken);
}
