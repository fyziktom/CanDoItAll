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
using CanDoItAll.CrmHr.UI.Assignments;

namespace CanDoItAll.Modules.CrmHr.Pages;

// The page is the Assignments workspace's view: the surface binds to these members and to nothing else of the host.
public partial class CrmHrAssignmentsPage : ICrmHrAssignmentsWorkspaceView
{
    void ICrmHrWorkspaceView.RequestRender() => _ = InvokeAsync(StateHasChanged);

    int ICrmHrAssignmentsWorkspaceView.ProjectCount => projectCount;
    IReadOnlyList<ProjectPartyAssignmentDetail> ICrmHrAssignmentsWorkspaceView.ScheduleAssignments => scheduleAssignments;
    IReadOnlyList<ProjectPartyAssignmentDetail> ICrmHrAssignmentsWorkspaceView.RelationshipAssignments => relationshipAssignments;
    IReadOnlyList<ProjectPartyAssignmentDetail> ICrmHrAssignmentsWorkspaceView.AllocationAssignments => allocationAssignments;
    ProjectPartyAssignmentCounts ICrmHrAssignmentsWorkspaceView.AssignmentCounts => assignmentCounts;
    int ICrmHrAssignmentsWorkspaceView.ScheduleAssignmentTotalCount => scheduleAssignmentTotalCount;
    int ICrmHrAssignmentsWorkspaceView.RelationshipAssignmentPageIndex => relationshipAssignmentPageIndex;
    int ICrmHrAssignmentsWorkspaceView.RelationshipAssignmentTotalCount => relationshipAssignmentTotalCount;
    int ICrmHrAssignmentsWorkspaceView.AllocationAssignmentPageIndex => allocationAssignmentPageIndex;
    int ICrmHrAssignmentsWorkspaceView.AllocationAssignmentTotalCount => allocationAssignmentTotalCount;
    IReadOnlyList<SkillCatalogItemModel> ICrmHrAssignmentsWorkspaceView.SkillCatalog => skillCatalog;
    IReadOnlyList<StaffingRequestItemModel> ICrmHrAssignmentsWorkspaceView.StaffingRequests => staffingRequests;
    IReadOnlyList<StaffingCandidateItemModel> ICrmHrAssignmentsWorkspaceView.StaffingCandidates => staffingCandidates;
    int ICrmHrAssignmentsWorkspaceView.StaffingRequestPageIndex => staffingRequestPageIndex;
    int ICrmHrAssignmentsWorkspaceView.StaffingRequestTotalCount => staffingRequestTotalCount;
    int ICrmHrAssignmentsWorkspaceView.StaffingCandidatePageIndex => staffingCandidatePageIndex;
    int ICrmHrAssignmentsWorkspaceView.StaffingCandidateTotalCount => staffingCandidateTotalCount;
    StaffingDashboardModel ICrmHrAssignmentsWorkspaceView.StaffingDashboard => staffingDashboard;
    Guid? ICrmHrAssignmentsWorkspaceView.SelectedProjectId => selectedProjectId;
    AssignmentWorkspaceTab ICrmHrAssignmentsWorkspaceView.SelectedWorkspaceTab => selectedWorkspaceTab;
    bool ICrmHrAssignmentsWorkspaceView.IsProjectContextLoading => isProjectContextLoading;
    string ICrmHrAssignmentsWorkspaceView.CandidateSearchText => candidateSearchText;
    string ICrmHrAssignmentsWorkspaceView.CandidateSkillFilter => candidateSkillFilter;
    string ICrmHrAssignmentsWorkspaceView.CandidateAvailabilityFilter => candidateAvailabilityFilter;
    string ICrmHrAssignmentsWorkspaceView.StaffingRequestSearchText => staffingRequestSearchText;
    string ICrmHrAssignmentsWorkspaceView.RelationshipAssignmentSearchText => relationshipAssignmentSearchText;
    string ICrmHrAssignmentsWorkspaceView.AllocationAssignmentSearchText => allocationAssignmentSearchText;
    StaffingRequestStatus? ICrmHrAssignmentsWorkspaceView.StaffingRequestStatusFilter => staffingRequestStatusFilter;
    ProjectPartyAssignmentRole? ICrmHrAssignmentsWorkspaceView.RelationshipAssignmentRoleFilter => relationshipAssignmentRoleFilter;
    ProjectPartyAssignmentRole? ICrmHrAssignmentsWorkspaceView.AllocationAssignmentRoleFilter => allocationAssignmentRoleFilter;
    ProjectPartyAssignmentUpsertRequest ICrmHrAssignmentsWorkspaceView.DraftAssignment => draftAssignment;
    ProjectPartyAssignmentUpsertRequest ICrmHrAssignmentsWorkspaceView.AllocationDraft => allocationDraft;
    StaffingRequestEditorModel ICrmHrAssignmentsWorkspaceView.StaffingRequestDraft => staffingRequestDraft;
    WorkspaceTabsIdentity ICrmHrAssignmentsWorkspaceView.WorkspaceTabsKey => WorkspaceTabsKey;
    int? ICrmHrAssignmentsWorkspaceView.StaffingRequestCount => StaffingRequestCount;
    string ICrmHrAssignmentsWorkspaceView.StaffingRequestBadgeText => StaffingRequestBadgeText;
    ProjectRecordQueryItem? ICrmHrAssignmentsWorkspaceView.SelectedProject => SelectedProject;
    AssignmentComparisons ICrmHrAssignmentsWorkspaceView.Comparisons => editing.Drafts.Comparisons;
    Task ICrmHrAssignmentsWorkspaceView.CompareAsync(AssignmentEditorKind editor) => editing.CompareAsync(editor, selectionLifetimeCancellation?.Token ?? default);
    Task ICrmHrAssignmentsWorkspaceView.ApplyComparisonAsync() => editing.ApplyComparisonAsync(selectionLifetimeCancellation?.Token ?? default);
    Task ICrmHrAssignmentsWorkspaceView.UndoComparisonAsync() => editing.UndoComparisonAsync(selectionLifetimeCancellation?.Token ?? default);
    string? ICrmHrAssignmentsWorkspaceView.EditingMessage => editing.Message;
    bool ICrmHrAssignmentsWorkspaceView.EditingRequiresAttention => editing.RequiresAttention;
    bool ICrmHrAssignmentsWorkspaceView.RecordsChanged => recordsChanged;
    bool ICrmHrAssignmentsWorkspaceView.RelationshipDirty => editing.Drafts.Relationship.IsDirty;
    bool ICrmHrAssignmentsWorkspaceView.AllocationDirty => editing.Drafts.Allocation.IsDirty;
    bool ICrmHrAssignmentsWorkspaceView.StaffingDirty => editing.Drafts.Staffing.IsDirty;
    bool ICrmHrAssignmentsWorkspaceView.RelationshipSaveDisabled => !editing.HasAccess || editing.IsUnknown || editing.IsBusy || !editing.Drafts.Relationship.CanSave || !editing.Drafts.Comparisons.CanSave(AssignmentEditorKind.Relationship);
    bool ICrmHrAssignmentsWorkspaceView.AllocationSaveDisabled => !editing.HasAccess || editing.IsUnknown || editing.IsBusy || !editing.Drafts.Allocation.CanSave || !editing.Drafts.Comparisons.CanSave(AssignmentEditorKind.Allocation);
    bool ICrmHrAssignmentsWorkspaceView.StaffingSaveDisabled => !editing.HasAccess || editing.IsUnknown || editing.IsBusy || !editing.Drafts.Staffing.CanSave || !editing.Drafts.Comparisons.CanSave(AssignmentEditorKind.Staffing);
    bool ICrmHrAssignmentsWorkspaceView.WritesDisabled => !editing.HasAccess || editing.IsUnknown || editing.IsBusy;
    Task ICrmHrAssignmentsWorkspaceView.AssignmentInputChangedAsync(CanDoItAll.Editing.RecordInput<AssignmentField> input) {
        inputRevision++;
        editing.Drafts.Relationship.Input(input);
        return Task.CompletedTask;
    }
    Task ICrmHrAssignmentsWorkspaceView.AllocationInputChangedAsync(CanDoItAll.Editing.RecordInput<AssignmentField> input) {
        inputRevision++;
        editing.Drafts.Allocation.Input(input);
        return Task.CompletedTask;
    }
    Task ICrmHrAssignmentsWorkspaceView.StaffingInputChangedAsync(CanDoItAll.Editing.RecordInput<StaffingField> input) {
        inputRevision++;
        editing.Drafts.Staffing.Input(input);
        return Task.CompletedTask;
    }
    Task<bool> ICrmHrAssignmentsWorkspaceView.ReviewRelationshipPrimaryAsync() => editing.ReviewPrimaryAsync(draftAssignment, selectionLifetimeCancellation?.Token ?? default);
    Task<bool> ICrmHrAssignmentsWorkspaceView.ReviewAllocationPrimaryAsync() => editing.ReviewPrimaryAsync(allocationDraft, selectionLifetimeCancellation?.Token ?? default);
    Task ICrmHrAssignmentsWorkspaceView.RefreshRecordsAsync() => ReviewUnknownRecordsAsync();
    Task ICrmHrAssignmentsWorkspaceView.RefreshSkillCatalogAsync() => EnsureSelectionDataLoadedAsync(AssignmentSelectionData.SkillCatalog, force: true);
    bool ICrmHrAssignmentsWorkspaceView.HasUnknownOperation => editing.IsUnknown;
    bool ICrmHrAssignmentsWorkspaceView.CanDiscardReviewedUnknown => editing.CanDiscardReviewedUnknown;
    Task ICrmHrAssignmentsWorkspaceView.DiscardReviewedUnknownAsync() => DiscardReviewedUnknownAsync();
    Task<bool> ICrmHrAssignmentsWorkspaceView.RequestDiscardAsync(AssignmentEditorKind editor) => RequestDiscardAsync(editor);

    Task ICrmHrAssignmentsWorkspaceView.HandleProjectChanged(Guid? projectId) => HandleProjectChanged(projectId);
    Task ICrmHrAssignmentsWorkspaceView.HandleWorkspaceTabChangedAsync(int selectedIndex) => HandleWorkspaceTabChangedAsync(selectedIndex);
    Task<bool> ICrmHrAssignmentsWorkspaceView.PrepareRelationshipCreateAsync() => PrepareRelationshipCreateAsync();
    Task<bool> ICrmHrAssignmentsWorkspaceView.PrepareStaffingCreateAsync() => PrepareStaffingCreateAsync();
    Task<bool> ICrmHrAssignmentsWorkspaceView.PrepareAllocationCreateAsync() => PrepareAllocationCreateAsync();
    Task<bool> ICrmHrAssignmentsWorkspaceView.SaveDraftAsync() => SaveDraftAsync();
    Task<bool> ICrmHrAssignmentsWorkspaceView.SaveAllocationAsync() => SaveAllocationAsync();
    Task<bool> ICrmHrAssignmentsWorkspaceView.SaveStaffingRequestAsync() => SaveStaffingRequestAsync();
    Task ICrmHrAssignmentsWorkspaceView.SearchCandidatesAsync() => SearchCandidatesAsync();
    Task ICrmHrAssignmentsWorkspaceView.HandleCandidateSearchTextChanged(string value) => HandleCandidateSearchTextChanged(value);
    Task ICrmHrAssignmentsWorkspaceView.HandleCandidateSkillFilterChanged(string value) => HandleCandidateSkillFilterChanged(value);
    Task ICrmHrAssignmentsWorkspaceView.HandleCandidateAvailabilityFilterChanged(string value) => HandleCandidateAvailabilityFilterChanged(value);
    Task ICrmHrAssignmentsWorkspaceView.HandleStaffingRequestSearchTextChangedAsync(string value) => HandleStaffingRequestSearchTextChangedAsync(value);
    Task ICrmHrAssignmentsWorkspaceView.HandleStaffingRequestStatusFilterChangedAsync(StaffingRequestStatus? status) => HandleStaffingRequestStatusFilterChangedAsync(status);
    Task ICrmHrAssignmentsWorkspaceView.HandleRelationshipSearchTextChangedAsync(string value) => HandleRelationshipSearchTextChangedAsync(value);
    Task ICrmHrAssignmentsWorkspaceView.HandleAllocationSearchTextChangedAsync(string value) => HandleAllocationSearchTextChangedAsync(value);
    Task ICrmHrAssignmentsWorkspaceView.HandleRelationshipRoleFilterChangedAsync(ProjectPartyAssignmentRole? role) => HandleRelationshipRoleFilterChangedAsync(role);
    Task ICrmHrAssignmentsWorkspaceView.HandleAllocationRoleFilterChangedAsync(ProjectPartyAssignmentRole? role) => HandleAllocationRoleFilterChangedAsync(role);
    Task ICrmHrAssignmentsWorkspaceView.HandleRelationshipPageChangedAsync(int pageIndex) => HandleRelationshipPageChangedAsync(pageIndex);
    Task ICrmHrAssignmentsWorkspaceView.HandleAllocationPageChangedAsync(int pageIndex) => HandleAllocationPageChangedAsync(pageIndex);
    Task ICrmHrAssignmentsWorkspaceView.HandleStaffingRequestPageChangedAsync(int pageIndex) => HandleStaffingRequestPageChangedAsync(pageIndex);
    Task ICrmHrAssignmentsWorkspaceView.HandleCandidatePageChangedAsync(int pageIndex) => HandleCandidatePageChangedAsync(pageIndex);
    Task ICrmHrAssignmentsWorkspaceView.ResetDraftAsync() => ResetDraftAsync();
    Task ICrmHrAssignmentsWorkspaceView.ResetAllocationAsync() => ResetAllocationAsync();
    Task ICrmHrAssignmentsWorkspaceView.ResetStaffingRequestAsync() => ResetStaffingRequestAsync();
    Task ICrmHrAssignmentsWorkspaceView.ResetCandidateFiltersAsync() => ResetCandidateFiltersAsync();
    Task ICrmHrAssignmentsWorkspaceView.DeleteAssignmentAsync(ProjectPartyAssignmentDetail assignment) => DeleteAssignmentAsync(assignment);
    Task ICrmHrAssignmentsWorkspaceView.DeleteAllocationAsync(ProjectPartyAssignmentDetail assignment) => DeleteAllocationAsync(assignment);
    Task ICrmHrAssignmentsWorkspaceView.DeleteStaffingRequestAsync(StaffingRequestItemModel request) => DeleteStaffingRequestAsync(request);
    bool ICrmHrAssignmentsWorkspaceView.IsLoading(AssignmentSelectionData data) => IsLoading(data);
    bool ICrmHrAssignmentsWorkspaceView.HasDisplayed(AssignmentSelectionData data) => renderedSelectionData.HasFlag(data);
    Task<ProjectNodeDetails?> ICrmHrAssignmentsWorkspaceView.LoadTaskDetailsAsync(ProjectPartyAssignmentDetail assignment) => LoadTaskDetailsAsync(assignment);
    void ICrmHrAssignmentsWorkspaceView.HandleProjectPickerLoadFailed(Exception exception) => HandleProjectPickerLoadFailed(exception);
    Task ICrmHrAssignmentsWorkspaceView.OpenStructure(Guid projectId) => OpenStructure(projectId);
    Task ICrmHrAssignmentsWorkspaceView.OpenGantt(Guid projectId) => OpenGantt(projectId);
    Task ICrmHrAssignmentsWorkspaceView.OpenSelectedProjectStructure() => OpenSelectedProjectStructure();
    Task ICrmHrAssignmentsWorkspaceView.OpenSelectedProjectGantt() => OpenSelectedProjectGantt();
}
