using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using CanDoItAll.Workbench.Operators.UI.Parties;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Workbench.Pages;

public partial class ProjectStructurePage {
    [Inject] private IProjectNodeAssignmentPolicyBridge NodeAssignmentPolicyBridge { get; set; } = default!;
    [Inject] private ProjectPartyPresentationWriter PartyPresentation { get; set; } = default!;
    private ProjectPartyEditorSession? partySession;
    private readonly Queue<(ProjectWriteAdmission Project, string NodeId, PartyCreated Party)> createdParticipantParties = new();

    private async Task LoadPartyEditorAsync() {
        if (selectedNode is not { ObjectType: ProjectObjectType.Participant or ProjectObjectType.Meeting } node ||
            NodeAssignmentPolicyBridge.Resolve(node.ObjectType, node.ObjectSubtype).ReplacementRoles.Count == 0) {
            partySession?.Retire();
            partySession = null;
            return;
        }
        if (partySession is { IsCurrent: true } existing && existing.NodeId == node.Id) {
            return;
        }
        partySession?.Retire();
        var context = CaptureActionContext();
        var selection = insightsSelectionRevision;
        ProjectPartyEditorSession? opening = null;
        opening = new(context.Admission, node, ProjectPartyIntegrationBridge, NodeAssignmentPolicyBridge,
            ProjectWorkbenchService, PartyPresentation,
            () => ReferenceEquals(partySession, opening) && IsCurrentAction(context) && HasOriginalContentAuthority(context) && insightsSelectionRevision == selection,
            () => HasOriginalContentAuthority(context),
            async updated => {
                if (opening!.IsCurrent) {
                    await ApplySurfaceNodeUpdatesAsync([updated]);
                }
            }, RecordCreatedParty, Logger);
        partySession = opening;
        await InvokeAsync(StateHasChanged);
        await opening.LoadAsync();
    }

    private void RecordCreatedParty(ProjectWriteAdmission project, string nodeId, PartyCreated party) {
        createdParticipantParties.Enqueue((project, nodeId, party));
        while (createdParticipantParties.Count > 16) {
            createdParticipantParties.Dequeue();
        }
        Logger.LogInformation("Directory party {PartyId} created for original participant {NodeId}, project {ProjectId}, lifetime {LifetimeId}. Assignment has not been saved.",
            party.Id, nodeId, project.ProjectId, project.LifetimeId);
    }
}
