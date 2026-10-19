using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectTaskPlanHistoricalReceiptRecovery(ProjectDefinitionHistoricalReceiptRecovery definitions,
    ProjectTaskPlanReceiptReader receipts) : IAgentToolHistoricalReceiptReadProvider {
    public bool Supports(string toolName) => toolName == ProjectStructureToolPolicy.ProjectTaskPlanPublish;

    public async ValueTask<AgentToolHistoricalReceiptObservation> ReadAsync(AgentToolHistoricalReceiptReadClaim claim,
        CancellationToken cancellationToken = default) {
        var admitted = await claim.RequireAsync(cancellationToken);
        var planning = admitted.Planning ?? throw new NotSupportedException("This historical receipt has no original planning decision.");
        var original = ProjectTaskPlanAgentCodec.Resolve(planning, admitted.OriginalSession, admitted.Payload, admitted.Binding);
        var access = new HistoricalAccess(original, definitions.ForAgent(original.Target.Admission, claim));
        var lookup = await receipts.LookupAsync(original.Command.Operation, new(original.Target.Caller, access), cancellationToken);
        await claim.RequireAsync(cancellationToken);
        return lookup.Status switch {
            ProjectTaskPlanLookupStatus.Found when lookup.Accepted is { } accepted
                => new(AgentHistoricalReceiptKnowledge.Accepted, ProjectTaskPlanAgentResult.Receipt(original, accepted)),
            ProjectTaskPlanLookupStatus.Open or ProjectTaskPlanLookupStatus.Draining => new(AgentHistoricalReceiptKnowledge.NotObserved),
            ProjectTaskPlanLookupStatus.ClosedAbsent => new(AgentHistoricalReceiptKnowledge.ClosedAbsent),
            ProjectTaskPlanLookupStatus.Expired => new(AgentHistoricalReceiptKnowledge.Expired),
            ProjectTaskPlanLookupStatus.DatasetChanged => new(AgentHistoricalReceiptKnowledge.DatasetChanged),
            ProjectTaskPlanLookupStatus.CallerContextChanged => throw new AgentHistoricalReceiptContextChangedException(),
            ProjectTaskPlanLookupStatus.InvalidContext => new(AgentHistoricalReceiptKnowledge.Unsupported),
            ProjectTaskPlanLookupStatus.DetailUnavailable when lookup.DetailFailure == ProjectTaskPlanDetailFailure.AccessDenied
                => new(AgentHistoricalReceiptKnowledge.ReadDenied),
            ProjectTaskPlanLookupStatus.DetailUnavailable or ProjectTaskPlanLookupStatus.Unavailable
                => new(AgentHistoricalReceiptKnowledge.ReadUnavailable),
            _ => throw new InvalidDataException("The task-plan owner returned an unsupported historical receipt observation.")
        };
    }

    private sealed class HistoricalAccess(ProjectTaskPlanAgentCommand original, IProjectDefinitionReceiptAccess access)
        : IProjectTaskPlanReceiptAccess {
        public async Task<bool> CanReadAsync(ProjectTaskPlanSource expected, ProjectWriteAdmission target, CancellationToken cancellationToken) {
            if (expected != original.Command.Source || target != original.Target.Project) {
                return false;
            }
            foreach (var project in ProjectTaskPlanAgentParticipant.Projects(original.Target)) {
                if (!await access.CanReadAsync(project, cancellationToken)) {
                    return false;
                }
            }
            return true;
        }
    }
}
