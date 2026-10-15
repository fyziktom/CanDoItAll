using CanDoItAll.AppComponents.Navigation;
using CanDoItAll.CrmHr.UI.Assignments;
using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Modules.CrmHr.Pages;

// The Assignments location: the project, the workflow tab and the committed filters of that tab's list. A filter
// belongs to one project and one tab: another project or a tab without that filter has none.
public sealed record AssignmentsUrlState(
    Guid? ProjectId,
    AssignmentWorkspaceTab Tab,
    string? Search,
    ProjectPartyAssignmentRole? Role,
    StaffingRequestStatus? Status,
    int Page);

public sealed class AssignmentsUrlCodec : IUrlStateCodec<AssignmentsUrlState> {
    public const string ProjectKey = "projectId";
    public const string TabKey = "tab";
    public const string SearchKey = CrmHrCatalogUrlKeys.Search;
    public const string RoleKey = "role";
    public const string StatusKey = "status";
    public const string PageKey = CrmHrCatalogUrlKeys.Page;

    public static AssignmentsUrlCodec Instance { get; } = new();

    public static UrlTokenMap<AssignmentWorkspaceTab> Tabs { get; } = new(
        (AssignmentWorkspaceTab.ResourceSchedule, "resource-schedule"),
        (AssignmentWorkspaceTab.Relationships, "relationships"),
        (AssignmentWorkspaceTab.StaffingRequests, "staffing-requests"),
        (AssignmentWorkspaceTab.Allocations, "allocations"));

    public static UrlTokenMap<ProjectPartyAssignmentRole> Roles { get; } = new(
        (ProjectPartyAssignmentRole.Customer, "customer"),
        (ProjectPartyAssignmentRole.CustomerContact, "customer-contact"),
        (ProjectPartyAssignmentRole.DeliveryUnit, "delivery-unit"),
        (ProjectPartyAssignmentRole.TeamMember, "team-member"),
        (ProjectPartyAssignmentRole.Manager, "manager"),
        (ProjectPartyAssignmentRole.Partner, "partner"),
        (ProjectPartyAssignmentRole.Vendor, "vendor"),
        (ProjectPartyAssignmentRole.Stakeholder, "stakeholder"),
        (ProjectPartyAssignmentRole.MeetingParticipant, "meeting-participant"),
        (ProjectPartyAssignmentRole.WorkItemAssignee, "work-item-assignee"),
        (ProjectPartyAssignmentRole.Reviewer, "reviewer"),
        (ProjectPartyAssignmentRole.AiAgent, "ai-agent"),
        (ProjectPartyAssignmentRole.BillingContact, "billing-contact"),
        (ProjectPartyAssignmentRole.TechnicalContact, "technical-contact"));

    public static UrlTokenMap<StaffingRequestStatus> Statuses { get; } = new(
        (StaffingRequestStatus.Draft, "draft"),
        (StaffingRequestStatus.Open, "open"),
        (StaffingRequestStatus.Proposed, "proposed"),
        (StaffingRequestStatus.Confirmed, "confirmed"),
        (StaffingRequestStatus.Closed, "closed"),
        (StaffingRequestStatus.Cancelled, "cancelled"));

    // The roles an allocation can carry; the allocation role filter offers only these.
    public static IReadOnlyList<ProjectPartyAssignmentRole> AllocationRoles { get; } =
    [
        ProjectPartyAssignmentRole.TeamMember,
        ProjectPartyAssignmentRole.DeliveryUnit,
        ProjectPartyAssignmentRole.Manager,
        ProjectPartyAssignmentRole.AiAgent,
        ProjectPartyAssignmentRole.Reviewer,
        ProjectPartyAssignmentRole.WorkItemAssignee
    ];

    public static AssignmentsUrlState Default { get; } = new(null, AssignmentWorkspaceTab.ResourceSchedule, null, null, null, 1);

    public IReadOnlyList<string> Keys { get; } = [ProjectKey, TabKey, SearchKey, RoleKey, StatusKey, PageKey];

    public static bool HasList(AssignmentWorkspaceTab tab) => tab is not AssignmentWorkspaceTab.ResourceSchedule;

    public static int MaximumSearchLength(AssignmentWorkspaceTab tab)
        => tab == AssignmentWorkspaceTab.StaffingRequests
            ? StaffingQueryLimits.MaximumSearchLength
            : ProjectPartyAssignmentQueryLimits.MaximumSearchLength;

    // Filters without their project, and filters a tab does not have, are dropped.
    public AssignmentsUrlState Read(UrlQuery query) {
        ArgumentNullException.ThrowIfNull(query);
        var projectId = query.GetId(ProjectKey);
        var tab = query.GetToken(TabKey, Tabs, AssignmentWorkspaceTab.ResourceSchedule);
        if (projectId is null || !HasList(tab)) {
            return Default with { ProjectId = projectId, Tab = tab };
        }

        var role = query.GetToken(RoleKey, Roles);
        return new(
            projectId,
            tab,
            query.GetText(SearchKey, MaximumSearchLength(tab)),
            IsRoleOf(tab, role) ? role : null,
            tab == AssignmentWorkspaceTab.StaffingRequests ? query.GetToken(StatusKey, Statuses) : null,
            query.GetPage(PageKey));
    }

    public void Write(AssignmentsUrlState state, UrlQueryWriter query) {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(query);
        query.Id(ProjectKey, state.ProjectId);
        query.Token(TabKey, state.Tab, Tabs, AssignmentWorkspaceTab.ResourceSchedule);
        if (state.ProjectId is null || !HasList(state.Tab)) {
            return;
        }

        var search = state.Search?.Trim();
        query.Text(SearchKey, search?.Length <= MaximumSearchLength(state.Tab) ? search : null);
        query.Token(RoleKey, IsRoleOf(state.Tab, state.Role) ? state.Role : null, Roles);
        query.Token(StatusKey, state.Tab == AssignmentWorkspaceTab.StaffingRequests ? state.Status : null, Statuses);
        query.Page(PageKey, state.Page);
    }

    private static bool IsRoleOf(AssignmentWorkspaceTab tab, ProjectPartyAssignmentRole? role)
        => role is { } value && tab switch {
            AssignmentWorkspaceTab.Relationships => true,
            AssignmentWorkspaceTab.Allocations => AllocationRoles.Contains(value),
            _ => false
        };
}
