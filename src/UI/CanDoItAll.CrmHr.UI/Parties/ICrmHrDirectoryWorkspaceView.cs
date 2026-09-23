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

namespace CanDoItAll.CrmHr.UI.Parties;

// What the Parties workspace renders and what it can ask its host to do. The routed host implements it: it owns the
// state, the reads, the mutations, navigation and the agent context; the surface only binds to it.
public interface ICrmHrDirectoryWorkspaceView : ICrmHrWorkspaceView
{
    CrmHrActivityPresentation PartyActivityPresentation { get; }
    string? PartyActivityFailureMessage { get; }
    PartyEditorViewModel Editor { get; }
    IReadOnlyList<PartyOrganizationAffiliationEditorModel> AffiliationEditors { get; }
    IReadOnlyDictionary<Guid, string> AffiliationPartyDisplayNames { get; }
    List<PartyRelationshipEditorModel> Relationships { get; }
    IReadOnlyList<PartyDuplicateCandidateModel> DuplicateCandidates { get; }
    PartyProjectAssignmentPage ProjectAssignmentsPage { get; }
    PartyDuplicateCandidateModel? MergeCandidate { get; }
    bool IsEditorDialogOpen { get; }
    bool IsMergeDialogOpen { get; }
    bool IsImportExportDialogOpen { get; }
    bool IsRelationshipsLoading { get; }
    bool IsProjectAssignmentsLoading { get; }
    long DirectoryBrowserVersion { get; }
    int SelectedEditorTabIndex { get; }
    string RelationshipsLoadError { get; }
    string ProjectAssignmentsLoadError { get; }
    string EditorTitle { get; }
    PartyOrganizationAffiliationListItemModel? CurrentPrimaryAffiliation { get; }
    string OtherCurrentAffiliationsTooltip { get; }
    Task SelectPartyAsync(Guid partyId);
    Task CreateNewAsync();
    Task CloseEditorDialogAsync();
    Task SaveAsync();
    Task<string> ExportPartyCsvAsync(PartyCsvExportScope scope, IReadOnlyList<Guid> partyIds);
    Task<Result<PartyCsvImportPreviewModel>> PreviewPartyImportAsync(string csvContent);
    Task<Result<int>> ApplyPartyImportAsync(IReadOnlyList<PartyCsvImportPreviewRowModel> rows);
    Task HandleImportAppliedAsync();
    void OpenImportExportDialog();
    Task CloseImportExportDialogAsync();
    Task HandleEditorTabChangedAsync(int index);
    Task HandleProjectAssignmentsPageRequestedAsync(int pageIndex);
    Task RetryRelationshipsLoadAsync();
    Task RetryActivityLoadAsync();
    Task RetryProjectAssignmentsLoadAsync();
    void AddAdditionalRole();
    void AddConfidentialNote();
    // A row action names the row the operator saw, never a position that later edits can shift.
    void RemoveAdditionalRole(PartyRoleAssignmentEditorModel role);
    void RemoveConfidentialNote(PartyConfidentialNoteEditorModel note);
    void OpenMergeDialog(PartyDuplicateCandidateModel candidate);
    Task HandleMergeCloseAsync();
    Task HandleMergeConfirmAsync(string reason);
    Task HandleAffiliationsChangedAsync(IReadOnlyList<PartyOrganizationAffiliationEditorModel> affiliations);
    string ProjectAssignmentsPageText { get; }
    bool CanMoveProjectAssignmentsPrevious { get; }
    bool CanMoveProjectAssignmentsNext { get; }
}
