using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Modules.Workbench;

internal interface IProjectTaskPlanSavedEffectAuthority : IProjectTaskPlanPublicationAuthority, IAsyncDisposable { }

internal sealed record ProjectTaskPlanSavedPublication(ProjectTaskPlanCommand Command, ProjectTaskPlanCaller Caller,
    IProjectTaskPlanReceiptAccess ReceiptAccess, Func<AgentPlanningLiveWork, IProjectTaskPlanSavedEffectAuthority> CreateAuthority);

internal sealed class ProjectTaskPlanSavedReceiptAccess(ProjectTaskPlanCommand original,
    Func<CancellationToken, Task> requireRead) : IProjectTaskPlanReceiptAccess {
    public async Task<bool> CanReadAsync(ProjectTaskPlanSource source, ProjectWriteAdmission target, CancellationToken cancellationToken) {
        if (source != original.Source || target != original.Basis.Project.Expectation.Project) {
            return false;
        }
        try {
            await requireRead(cancellationToken);
            return true;
        } catch (Exception exception) when (exception is AgentToolReceiptAccessDeniedException or UnauthorizedAccessException or ProjectWriteAdmissionRejectedException) {
            return false;
        }
    }
}

internal sealed class ProjectTaskPlanSavedPublicationResolver(ProjectTaskPlanAgentPublication agent,
    ProjectTaskPlanProcessPublication process) {
    internal ProjectTaskPlanSavedPublication Resolve(AgentPlanningDecisionObservation planning, AgentToolSessionAdmission session,
        AgentToolPreparedPayload payload, AgentToolApprovalBinding binding) => planning.Decision.InheritedOrigin is null
            ? agent.Resolve(planning, session, payload, binding) : process.Resolve(planning, session, payload, binding);

    internal Task RequireReadAsync(AgentPlanningDecisionRecord decision, CancellationToken cancellationToken)
        => decision.InheritedOrigin is null ? agent.RequireReadAsync(decision, cancellationToken) : process.RequireReadAsync(decision, cancellationToken);

    internal async Task<bool> CanReadAsync(AgentToolSessionObservation observation, CancellationToken cancellationToken) {
        if (observation.Planning is not { } planning || planning.Decision.Session != observation.Session.Reference) {
            return false;
        }
        try {
            await RequireReadAsync(planning.Decision, cancellationToken);
            return true;
        } catch (Exception exception) when (exception is AgentToolReceiptAccessDeniedException or UnauthorizedAccessException or ProjectWriteAdmissionRejectedException) {
            return false;
        }
    }
}
