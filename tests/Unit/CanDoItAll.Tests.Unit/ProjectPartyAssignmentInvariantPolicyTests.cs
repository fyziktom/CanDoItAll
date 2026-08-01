using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectPartyAssignmentInvariantPolicyTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(100.01)]
    public void ValidateValues_rejects_allocation_outside_the_supported_range(double value)
    {
        var request = new ProjectPartyAssignmentUpsertRequest
        {
            AllocationPercent = (decimal)value
        };

        var error = ProjectPartyAssignmentInvariantPolicy.ValidateValues(request);

        Assert.NotNull(error);
        Assert.Equal("crmhr.project-assignment.allocation-range", error.Code);
    }

    [Fact]
    public void ValidateValues_accepts_open_allocation_and_inclusive_same_day_range()
    {
        var request = new ProjectPartyAssignmentUpsertRequest
        {
            AllocationPercent = null,
            StartsOn = new DateOnly(2026, 7, 18),
            EndsOn = new DateOnly(2026, 7, 18)
        };

        var error = ProjectPartyAssignmentInvariantPolicy.ValidateValues(request);

        Assert.Null(error);
    }

    [Fact]
    public void ValidateValues_rejects_reversed_date_range()
    {
        var request = new ProjectPartyAssignmentUpsertRequest
        {
            AllocationPercent = 100m,
            StartsOn = new DateOnly(2026, 7, 19),
            EndsOn = new DateOnly(2026, 7, 18)
        };

        var error = ProjectPartyAssignmentInvariantPolicy.ValidateValues(request);

        Assert.NotNull(error);
        Assert.Equal("crmhr.project-assignment.date-range-invalid", error.Code);
    }

    [Fact]
    public void ValidatePartyType_restricts_work_item_assignees_to_people_and_agents()
    {
        var organizationError = ProjectPartyAssignmentInvariantPolicy.ValidatePartyType(
            ProjectPartyAssignmentRole.WorkItemAssignee,
            PartyType.Organization);
        var personError = ProjectPartyAssignmentInvariantPolicy.ValidatePartyType(
            ProjectPartyAssignmentRole.WorkItemAssignee,
            PartyType.Person);
        var unrelatedRoleError = ProjectPartyAssignmentInvariantPolicy.ValidatePartyType(
            ProjectPartyAssignmentRole.Customer,
            PartyType.Organization);

        Assert.NotNull(organizationError);
        Assert.Equal("crmhr.project-assignment.work-item-assignee-party-type-invalid", organizationError.Code);
        Assert.Null(personError);
        Assert.Null(unrelatedRoleError);
    }
}
