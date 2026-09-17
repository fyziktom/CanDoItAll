using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AppComponents;
using CanDoItAll.CrmHr.UI.Activity;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.CrmHr.UI.Pickers;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using CanDoItAll.CrmHr.UI;
using CanDoItAll.CrmHr.UI.Parties;

namespace CanDoItAll.Modules.CrmHr.Pages;

// The page is the Parties workspace's view: the surface binds to these members and to nothing else of the host.
public partial class CrmHrDirectoryPage : ICrmHrDirectoryWorkspaceView
{
    void ICrmHrWorkspaceView.RequestRender() => _ = InvokeAsync(StateHasChanged);

    CrmHrActivityPresentation ICrmHrDirectoryWorkspaceView.PartyActivityPresentation => PartyActivityPresentation;
    string? ICrmHrDirectoryWorkspaceView.PartyActivityFailureMessage => PartyActivityFailureMessage;
    PartyEditorViewModel ICrmHrDirectoryWorkspaceView.Editor => editor;
    IReadOnlyList<PartyOrganizationAffiliationEditorModel> ICrmHrDirectoryWorkspaceView.AffiliationEditors => affiliationEditors;
    IReadOnlyDictionary<Guid, string> ICrmHrDirectoryWorkspaceView.AffiliationPartyDisplayNames => affiliationPartyDisplayNames;
    List<PartyRelationshipEditorModel> ICrmHrDirectoryWorkspaceView.Relationships => relationships;
    IReadOnlyList<PartyDuplicateCandidateModel> ICrmHrDirectoryWorkspaceView.DuplicateCandidates => duplicateCandidates;
    PartyProjectAssignmentPage ICrmHrDirectoryWorkspaceView.ProjectAssignmentsPage => projectAssignmentsPage;
    PartyDuplicateCandidateModel? ICrmHrDirectoryWorkspaceView.MergeCandidate => mergeCandidate;
    bool ICrmHrDirectoryWorkspaceView.IsEditorDialogOpen => isEditorDialogOpen;
    bool ICrmHrDirectoryWorkspaceView.IsMergeDialogOpen => isMergeDialogOpen;
    bool ICrmHrDirectoryWorkspaceView.IsImportExportDialogOpen => isImportExportDialogOpen;
    bool ICrmHrDirectoryWorkspaceView.IsRelationshipsLoading => isRelationshipsLoading;
    bool ICrmHrDirectoryWorkspaceView.IsProjectAssignmentsLoading => isProjectAssignmentsLoading;
    long ICrmHrDirectoryWorkspaceView.DirectoryBrowserVersion => directoryBrowserVersion;
    int ICrmHrDirectoryWorkspaceView.SelectedEditorTabIndex => selectedEditorTabIndex;
    string ICrmHrDirectoryWorkspaceView.RelationshipsLoadError => relationshipsLoadError;
    string ICrmHrDirectoryWorkspaceView.ProjectAssignmentsLoadError => projectAssignmentsLoadError;
    string ICrmHrDirectoryWorkspaceView.EditorTitle => EditorTitle;
    PartyOrganizationAffiliationListItemModel? ICrmHrDirectoryWorkspaceView.CurrentPrimaryAffiliation => CurrentPrimaryAffiliation;
    string ICrmHrDirectoryWorkspaceView.OtherCurrentAffiliationsTooltip => OtherCurrentAffiliationsTooltip;
    Task ICrmHrDirectoryWorkspaceView.SelectPartyAsync(Guid partyId) => SelectPartyAsync(partyId);
    Task ICrmHrDirectoryWorkspaceView.CreateNewAsync() => CreateNewAsync();
    Task ICrmHrDirectoryWorkspaceView.CloseEditorDialogAsync() => CloseEditorDialogAsync();
    Task ICrmHrDirectoryWorkspaceView.SaveAsync() => SaveAsync();
    Task<string> ICrmHrDirectoryWorkspaceView.ExportPartyCsvAsync(PartyCsvExportScope scope, IReadOnlyList<Guid> partyIds) => ExportPartyCsvAsync(scope, partyIds);
    Task<Result<PartyCsvImportPreviewModel>> ICrmHrDirectoryWorkspaceView.PreviewPartyImportAsync(string csvContent) => PreviewPartyImportAsync(csvContent);
    Task<Result<int>> ICrmHrDirectoryWorkspaceView.ApplyPartyImportAsync(IReadOnlyList<PartyCsvImportPreviewRowModel> rows) => ApplyPartyImportAsync(rows);
    Task ICrmHrDirectoryWorkspaceView.HandleImportAppliedAsync() => HandleImportAppliedAsync();
    void ICrmHrDirectoryWorkspaceView.OpenImportExportDialog() => OpenImportExportDialog();
    Task ICrmHrDirectoryWorkspaceView.CloseImportExportDialogAsync() => CloseImportExportDialogAsync();
    Task ICrmHrDirectoryWorkspaceView.HandleEditorTabChangedAsync(int index) => HandleEditorTabChangedAsync(index);
    Task ICrmHrDirectoryWorkspaceView.HandleProjectAssignmentsPageRequestedAsync(int pageIndex) => HandleProjectAssignmentsPageRequestedAsync(pageIndex);
    Task ICrmHrDirectoryWorkspaceView.RetryRelationshipsLoadAsync() => RetryRelationshipsLoadAsync();
    Task ICrmHrDirectoryWorkspaceView.RetryActivityLoadAsync() => RetryActivityLoadAsync();
    Task ICrmHrDirectoryWorkspaceView.RetryProjectAssignmentsLoadAsync() => RetryProjectAssignmentsLoadAsync();
    void ICrmHrDirectoryWorkspaceView.AddAdditionalRole() => AddAdditionalRole();
    void ICrmHrDirectoryWorkspaceView.AddConfidentialNote() => AddConfidentialNote();
    void ICrmHrDirectoryWorkspaceView.RemoveAdditionalRole(int index) => RemoveAdditionalRole(index);
    void ICrmHrDirectoryWorkspaceView.RemoveConfidentialNote(int index) => RemoveConfidentialNote(index);
    void ICrmHrDirectoryWorkspaceView.OpenMergeDialog(PartyDuplicateCandidateModel candidate) => OpenMergeDialog(candidate);
    Task ICrmHrDirectoryWorkspaceView.HandleMergeCloseAsync() => HandleMergeCloseAsync();
    Task ICrmHrDirectoryWorkspaceView.HandleMergeConfirmAsync(string reason) => HandleMergeConfirmAsync(reason);
    Task ICrmHrDirectoryWorkspaceView.HandleAffiliationsChangedAsync(IReadOnlyList<PartyOrganizationAffiliationEditorModel> affiliations) => HandleAffiliationsChangedAsync(affiliations);
    string ICrmHrDirectoryWorkspaceView.ProjectAssignmentsPageText => ProjectAssignmentsPageText;
    bool ICrmHrDirectoryWorkspaceView.CanMoveProjectAssignmentsPrevious => CanMoveProjectAssignmentsPrevious;
    bool ICrmHrDirectoryWorkspaceView.CanMoveProjectAssignmentsNext => CanMoveProjectAssignmentsNext;
}
