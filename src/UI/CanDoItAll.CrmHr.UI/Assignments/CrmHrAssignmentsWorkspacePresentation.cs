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

// Presentation helpers of the Assignments workspace: labels, tones, option lists and projections its surface renders.
public static class CrmHrAssignmentsWorkspacePresentation
{
    public const int StaffingPageSize = StaffingQueryLimits.DefaultPageSize;

    public const int AssignmentPageSize = ProjectPartyAssignmentQueryLimits.DefaultPageSize;
}

public readonly record struct WorkspaceTabsIdentity(
    Guid? ProjectId,
    int AssignmentCount,
    int AllocationCount,
    int StaffingRequestCount,
    bool StaffingRequestsLoaded);

public enum AssignmentWorkspaceTab
{
    ResourceSchedule,
    Relationships,
    StaffingRequests,
    Allocations
}

[Flags]
public enum AssignmentSelectionData
{
    None = 0,
    AssignmentCounts = 1 << 0,
    ScheduleAssignments = 1 << 1,
    RelationshipAssignments = 1 << 2,
    Assignments = RelationshipAssignments,
    AllocationAssignments = 1 << 3,
    SkillCatalog = 1 << 4,
    StaffingRequests = 1 << 5,
    StaffingCandidates = 1 << 6
}
