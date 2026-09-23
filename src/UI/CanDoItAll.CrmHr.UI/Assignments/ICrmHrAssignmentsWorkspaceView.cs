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

namespace CanDoItAll.CrmHr.UI.Assignments;

// What the Assignments workspace renders and what it can ask its host to do. The routed host implements it: it owns the
// state, the reads, the mutations, navigation and the agent context; the surface only binds to it.
public interface ICrmHrAssignmentsWorkspaceView : ICrmHrWorkspaceView
{
    int ProjectCount { get; }
    IReadOnlyList<ProjectPartyAssignmentDetail> ScheduleAssignments { get; }
    IReadOnlyList<ProjectPartyAssignmentDetail> RelationshipAssignments { get; }
    IReadOnlyList<ProjectPartyAssignmentDetail> AllocationAssignments { get; }
    ProjectPartyAssignmentCounts AssignmentCounts { get; }
    int ScheduleAssignmentTotalCount { get; }
    int RelationshipAssignmentPageIndex { get; }
    int RelationshipAssignmentTotalCount { get; }
    int AllocationAssignmentPageIndex { get; }
    int AllocationAssignmentTotalCount { get; }
    IReadOnlyList<SkillCatalogItemModel> SkillCatalog { get; }
    IReadOnlyList<StaffingRequestItemModel> StaffingRequests { get; }
    IReadOnlyList<StaffingCandidateItemModel> StaffingCandidates { get; }
    int StaffingRequestPageIndex { get; }
    int StaffingRequestTotalCount { get; }
    int StaffingCandidatePageIndex { get; }
    int StaffingCandidateTotalCount { get; }
    StaffingDashboardModel StaffingDashboard { get; }
    Guid? SelectedProjectId { get; }
    AssignmentWorkspaceTab SelectedWorkspaceTab { get; }
    bool IsProjectContextLoading { get; }
    string CandidateSearchText { get; }
    string CandidateSkillFilter { get; }
    string CandidateAvailabilityFilter { get; }
    string StaffingRequestSearchText { get; }
    string RelationshipAssignmentSearchText { get; }
    string AllocationAssignmentSearchText { get; }
    StaffingRequestStatus? StaffingRequestStatusFilter { get; }
    ProjectPartyAssignmentRole? RelationshipAssignmentRoleFilter { get; }
    ProjectPartyAssignmentRole? AllocationAssignmentRoleFilter { get; }
    ProjectPartyAssignmentUpsertRequest DraftAssignment { get; }
    ProjectPartyAssignmentUpsertRequest AllocationDraft { get; }
    StaffingRequestEditorModel StaffingRequestDraft { get; }
    WorkspaceTabsIdentity WorkspaceTabsKey { get; }
    int? StaffingRequestCount { get; }
    string StaffingRequestBadgeText { get; }
    ProjectRecordQueryItem? SelectedProject { get; }
    Task HandleProjectChanged(Guid? projectId);
    Task HandleWorkspaceTabChangedAsync(int selectedIndex);
    Task<bool> PrepareRelationshipCreateAsync();
    Task<bool> PrepareStaffingCreateAsync();
    Task<bool> PrepareAllocationCreateAsync();
    Task<bool> SaveDraftAsync();
    Task<bool> SaveAllocationAsync();
    Task<bool> SaveStaffingRequestAsync();
    Task SearchCandidatesAsync();
    Task HandleCandidateSearchTextChanged(string value);
    Task HandleCandidateSkillFilterChanged(string value);
    Task HandleCandidateAvailabilityFilterChanged(string value);
    Task HandleStaffingRequestSearchTextChangedAsync(string value);
    Task HandleStaffingRequestStatusFilterChangedAsync(StaffingRequestStatus? status);
    Task HandleRelationshipSearchTextChangedAsync(string value);
    Task HandleAllocationSearchTextChangedAsync(string value);
    Task HandleRelationshipRoleFilterChangedAsync(ProjectPartyAssignmentRole? role);
    Task HandleAllocationRoleFilterChangedAsync(ProjectPartyAssignmentRole? role);
    Task HandleRelationshipPageChangedAsync(int pageIndex);
    Task HandleAllocationPageChangedAsync(int pageIndex);
    Task HandleStaffingRequestPageChangedAsync(int pageIndex);
    Task HandleCandidatePageChangedAsync(int pageIndex);
    Task ResetDraftAsync();
    Task ResetAllocationAsync();
    Task ResetStaffingRequestAsync();
    Task ResetCandidateFiltersAsync();
    Task DeleteAssignmentAsync(Guid assignmentId);
    Task DeleteAllocationAsync(Guid assignmentId);
    Task DeleteStaffingRequestAsync(Guid staffingRequestId);
    bool IsLoading(AssignmentSelectionData data);
    Task<ProjectNodeDetails?> LoadTaskDetailsAsync(ProjectPartyAssignmentDetail assignment);
    void HandleProjectPickerLoadFailed(Exception exception);
    Task OpenStructure(Guid projectId);
    Task OpenGantt(Guid projectId);
    Task OpenSelectedProjectStructure();
    Task OpenSelectedProjectGantt();
}
