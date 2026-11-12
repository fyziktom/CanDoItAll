using CanDoItAll.AppComponents.Navigation;
using CanDoItAll.CrmHr.UI.Assignments;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.CrmHr.Pages;
using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Tests.Unit.CrmHr;

// The Assignments location: the project, the workflow tab and the committed filters of that tab's list. A filter
// belongs to one project and one tab.
public sealed class CrmHrAssignmentsUrlCodecTests {
    private const string ProjectText = "8e7d6c5b-4a39-4281-9f0e-d1c2b3a49586";
    private static readonly AssignmentsUrlCodec Codec = AssignmentsUrlCodec.Instance;
    private static readonly Guid Project = Guid.Parse(ProjectText);

    [Theory]
    [InlineData("", "")]
    [InlineData("?projectId=" + ProjectText, "?projectId=" + ProjectText)]
    [InlineData("?tab=resource-schedule", "")]
    [InlineData("?tab=relationships", "?tab=relationships")]
    [InlineData("?tab=relationships&q=ana&role=reviewer&page=2", "?tab=relationships")]
    [InlineData("?projectId=" + ProjectText + "&tab=relationships&q=ana&role=reviewer&page=2",
        "?projectId=" + ProjectText + "&tab=relationships&q=ana&role=reviewer&page=2")]
    [InlineData("?projectId=" + ProjectText + "&tab=relationships&status=open", "?projectId=" + ProjectText + "&tab=relationships")]
    [InlineData("?projectId=" + ProjectText + "&tab=allocations&role=customer", "?projectId=" + ProjectText + "&tab=allocations")]
    [InlineData("?projectId=" + ProjectText + "&tab=allocations&role=ai-agent&page=3", "?projectId=" + ProjectText + "&tab=allocations&role=ai-agent&page=3")]
    [InlineData("?projectId=" + ProjectText + "&tab=staffing-requests&status=proposed&role=manager&q=backend&page=3",
        "?projectId=" + ProjectText + "&tab=staffing-requests&q=backend&status=proposed&page=3")]
    [InlineData("?projectId=" + ProjectText + "&tab=STAFFING-REQUESTS&status=CANCELLED", "?projectId=" + ProjectText + "&tab=staffing-requests&status=cancelled")]
    [InlineData("?projectId=" + ProjectText + "&status=open&q=backend&page=2&role=manager", "?projectId=" + ProjectText)]
    [InlineData("?projectId=" + ProjectText + "&tab=timeline&q=backend", "?projectId=" + ProjectText)]
    [InlineData("?projectId=not-a-guid&tab=allocations&role=manager", "?tab=allocations")]
    [InlineData("?projectId=" + ProjectText + "&tab=allocations&role=chef&status=open&page=0", "?projectId=" + ProjectText + "&tab=allocations")]
    public void Every_location_has_one_stable_canonical_form(string query, string canonical) {
        var state = Read(query);

        Assert.Equal(canonical, Codec.Format(state));
        Assert.Equal(state, Read(canonical));
        Assert.Equal(canonical, Codec.Format(Read(canonical)));
    }

    [Theory]
    [InlineData(AssignmentWorkspaceTab.ResourceSchedule, "resource-schedule")]
    [InlineData(AssignmentWorkspaceTab.Relationships, "relationships")]
    [InlineData(AssignmentWorkspaceTab.StaffingRequests, "staffing-requests")]
    [InlineData(AssignmentWorkspaceTab.Allocations, "allocations")]
    public void Every_workflow_tab_has_a_token(AssignmentWorkspaceTab tab, string token) {
        Assert.Equal(token, AssignmentsUrlCodec.Tabs.Format(tab));
        Assert.Equal(tab, Read($"?projectId={Project}&tab={token}").Tab);
    }

    [Theory]
    [InlineData(ProjectPartyAssignmentRole.Customer, "customer")]
    [InlineData(ProjectPartyAssignmentRole.CustomerContact, "customer-contact")]
    [InlineData(ProjectPartyAssignmentRole.DeliveryUnit, "delivery-unit")]
    [InlineData(ProjectPartyAssignmentRole.TeamMember, "team-member")]
    [InlineData(ProjectPartyAssignmentRole.Manager, "manager")]
    [InlineData(ProjectPartyAssignmentRole.Partner, "partner")]
    [InlineData(ProjectPartyAssignmentRole.Vendor, "vendor")]
    [InlineData(ProjectPartyAssignmentRole.Stakeholder, "stakeholder")]
    [InlineData(ProjectPartyAssignmentRole.MeetingParticipant, "meeting-participant")]
    [InlineData(ProjectPartyAssignmentRole.WorkItemAssignee, "work-item-assignee")]
    [InlineData(ProjectPartyAssignmentRole.Reviewer, "reviewer")]
    [InlineData(ProjectPartyAssignmentRole.AiAgent, "ai-agent")]
    [InlineData(ProjectPartyAssignmentRole.BillingContact, "billing-contact")]
    [InlineData(ProjectPartyAssignmentRole.TechnicalContact, "technical-contact")]
    public void Every_role_filters_relationships_and_only_allocation_roles_filter_allocations(ProjectPartyAssignmentRole role, string token) {
        Assert.Equal(token, AssignmentsUrlCodec.Roles.Format(role));
        Assert.Equal(role, Read($"?projectId={Project}&tab=relationships&role={token}").Role);
        Assert.Equal(
            AssignmentsUrlCodec.AllocationRoles.Contains(role) ? role : (ProjectPartyAssignmentRole?)null,
            Read($"?projectId={Project}&tab=allocations&role={token}").Role);
        Assert.Null(Read($"?projectId={Project}&tab=staffing-requests&role={token}").Role);
    }

    [Theory]
    [InlineData(StaffingRequestStatus.Draft, "draft")]
    [InlineData(StaffingRequestStatus.Open, "open")]
    [InlineData(StaffingRequestStatus.Proposed, "proposed")]
    [InlineData(StaffingRequestStatus.Confirmed, "confirmed")]
    [InlineData(StaffingRequestStatus.Closed, "closed")]
    [InlineData(StaffingRequestStatus.Cancelled, "cancelled")]
    public void Every_staffing_status_filters_only_staffing_requests(StaffingRequestStatus status, string token) {
        Assert.Equal(token, AssignmentsUrlCodec.Statuses.Format(status));
        Assert.Equal(status, Read($"?projectId={Project}&tab=staffing-requests&status={token}").Status);
        Assert.Null(Read($"?projectId={Project}&tab=allocations&status={token}").Status);
    }

    [Fact]
    public void Search_text_is_bounded_by_the_list_it_filters() {
        var longest = new string('s', AssignmentsUrlCodec.MaximumSearchLength(AssignmentWorkspaceTab.Relationships));

        Assert.Equal(StaffingQueryLimits.MaximumSearchLength, AssignmentsUrlCodec.MaximumSearchLength(AssignmentWorkspaceTab.StaffingRequests));
        Assert.Equal(ProjectPartyAssignmentQueryLimits.MaximumSearchLength, AssignmentsUrlCodec.MaximumSearchLength(AssignmentWorkspaceTab.Allocations));
        Assert.Equal(longest, Read($"?projectId={Project}&tab=relationships&q={longest}").Search);
        Assert.Null(Read($"?projectId={Project}&tab=relationships&q={longest}s").Search);
        Assert.Equal(
            $"?projectId={Project}&tab=allocations",
            Codec.Format(AssignmentsUrlCodec.Default with { ProjectId = Project, Tab = AssignmentWorkspaceTab.Allocations, Search = longest + "s" }));
    }

    [Fact]
    public void Normalizing_drops_filters_without_their_project_or_list() {
        var schedule = new AssignmentsUrlState(Project, AssignmentWorkspaceTab.ResourceSchedule, "backend", ProjectPartyAssignmentRole.Manager, StaffingRequestStatus.Open, 4);
        var detached = new AssignmentsUrlState(null, AssignmentWorkspaceTab.StaffingRequests, "backend", null, StaffingRequestStatus.Open, 4);

        Assert.False(AssignmentsUrlCodec.HasList(AssignmentWorkspaceTab.ResourceSchedule));
        Assert.True(AssignmentsUrlCodec.HasList(AssignmentWorkspaceTab.StaffingRequests));
        Assert.Equal(AssignmentsUrlCodec.Default with { ProjectId = Project }, Codec.Normalize(schedule));
        Assert.Equal(AssignmentsUrlCodec.Default with { Tab = AssignmentWorkspaceTab.StaffingRequests }, Codec.Normalize(detached));
    }

    [Fact]
    public void Composing_the_location_replaces_owned_keys_and_keeps_foreign_ones() {
        var state = AssignmentsUrlCodec.Default with { ProjectId = Project, Tab = AssignmentWorkspaceTab.Relationships, Role = ProjectPartyAssignmentRole.Vendor };

        Assert.Equal(
            $"/crm-hr/assignments?projectId={Project}&tab=relationships&role=vendor&partyId=x",
            UrlLocation.Compose("/crm-hr/assignments?tab=allocations&partyId=x&page=4", Codec, state));
    }

    [Fact]
    public void Every_owned_key_has_a_query_trigger_on_the_page() {
        Assert.Equal(new[] { "projectId", "tab", "q", "role", "status", "page" }, Codec.Keys);
        Assert.Empty(UrlStateTriggers.Missing(typeof(CrmHrAssignmentsPage), Codec.Keys));
    }

    private static AssignmentsUrlState Read(string query) => Codec.Read(UrlQuery.Parse(query));
}
