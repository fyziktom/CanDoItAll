using CanDoItAll.Components.Gantt;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectAssignmentGanttProjectionAdapterTests
{
    private static readonly DateOnly Today = new(2026, 7, 18);

    [Fact]
    public void Build_projects_stable_resource_task_with_inclusive_end()
    {
        var assignmentId = Guid.NewGuid();
        var assignment = CreateAssignment(
            assignmentId,
            "Ada Lovelace",
            ProjectPartyType.Person,
            ProjectPartyAssignmentRole.TeamMember,
            60m,
            new DateOnly(2026, 7, 20),
            new DateOnly(2026, 7, 22));

        var projection = ProjectAssignmentGanttProjectionAdapter.Build([assignment], Today);

        var task = Assert.Single(projection.Tasks);
        Assert.Empty(projection.Issues);
        Assert.Equal($"assignment:{assignmentId:D}", task.Id.Value);
        Assert.Equal("Ada Lovelace · Team member · 60% allocated", task.Title);
        Assert.Equal(UtcDate(2026, 7, 20), task.Start);
        Assert.Equal(UtcEndOfDay(2026, 7, 22), task.End);
        Assert.Null(task.ProgressPercent);
        var resource = Assert.Single(task.Assignments);
        Assert.Equal(GanttAssignmentKind.Person, resource.Kind);
        Assert.Equal("Ada Lovelace", resource.Name);
    }

    [Fact]
    public void Build_clips_open_boundaries_to_a_deterministic_horizon()
    {
        var assignment = CreateAssignment(
            Guid.NewGuid(),
            "Review agent",
            ProjectPartyType.AiAgent,
            ProjectPartyAssignmentRole.AiAgent,
            allocationPercent: null,
            startsOn: null,
            endsOn: null);

        var projection = ProjectAssignmentGanttProjectionAdapter.Build([assignment], Today);

        Assert.True(projection.HasOpenBoundary);
        Assert.Equal(new DateOnly(2026, 6, 18), projection.HorizonStart);
        Assert.Equal(new DateOnly(2026, 10, 16), projection.HorizonEndInclusive);
        var task = Assert.Single(projection.Tasks);
        Assert.Equal(UtcDate(2026, 6, 18), task.Start);
        Assert.Equal(UtcEndOfDay(2026, 10, 16), task.End);
        Assert.Contains("allocation not set", task.Title, StringComparison.Ordinal);
        Assert.Equal(GanttAssignmentKind.Agent, Assert.Single(task.Assignments).Kind);
    }

    [Fact]
    public void Build_omits_invalid_or_unrepresentable_assignments_with_typed_issues()
    {
        var reversed = CreateAssignment(
            Guid.NewGuid(),
            "Reversed",
            ProjectPartyType.Person,
            ProjectPartyAssignmentRole.TeamMember,
            100m,
            new DateOnly(2026, 8, 2),
            new DateOnly(2026, 8, 1));
        var unrepresentable = CreateAssignment(
            Guid.NewGuid(),
            "Unbounded",
            ProjectPartyType.Person,
            ProjectPartyAssignmentRole.TeamMember,
            100m,
            new DateOnly(2026, 8, 1),
            DateOnly.MaxValue);
        var unnamed = CreateAssignment(
            Guid.NewGuid(),
            "   ",
            ProjectPartyType.Person,
            ProjectPartyAssignmentRole.TeamMember,
            100m,
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 2));

        var projection = ProjectAssignmentGanttProjectionAdapter.Build(
            [reversed, unrepresentable, unnamed],
            Today);

        Assert.Empty(projection.Tasks);
        Assert.Collection(
            projection.Issues,
            issue => Assert.Equal(ProjectAssignmentGanttProjectionIssueCode.InvalidDateRange, issue.Code),
            issue => Assert.Equal(ProjectAssignmentGanttProjectionIssueCode.UnrepresentableDate, issue.Code),
            issue => Assert.Equal(ProjectAssignmentGanttProjectionIssueCode.MissingPartyName, issue.Code));
    }

    [Fact]
    public void Build_keeps_organization_units_as_named_rows_without_false_resource_kind()
    {
        var assignment = CreateAssignment(
            Guid.NewGuid(),
            "Platform delivery unit",
            ProjectPartyType.OrganizationUnit,
            ProjectPartyAssignmentRole.DeliveryUnit,
            40m,
            new DateOnly(2026, 7, 20),
            new DateOnly(2026, 7, 25));

        var projection = ProjectAssignmentGanttProjectionAdapter.Build([assignment], Today);

        var task = Assert.Single(projection.Tasks);
        Assert.Empty(task.Assignments);
        Assert.StartsWith("Platform delivery unit · Delivery unit", task.Title, StringComparison.Ordinal);
    }

    private static ProjectPartyAssignmentDetail CreateAssignment(
        Guid assignmentId,
        string partyName,
        ProjectPartyType partyType,
        ProjectPartyAssignmentRole role,
        decimal? allocationPercent,
        DateOnly? startsOn,
        DateOnly? endsOn)
    {
        return new ProjectPartyAssignmentDetail(
            assignmentId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            role,
            partyName,
            partyType.ToString(),
            partyType,
            string.Empty,
            IsPrimary: false,
            allocationPercent,
            startsOn.HasValue ? UtcDate(startsOn.Value.Year, startsOn.Value.Month, startsOn.Value.Day) : null,
            endsOn.HasValue ? UtcDate(endsOn.Value.Year, endsOn.Value.Month, endsOn.Value.Day) : null,
            "unit-test",
            string.Empty);
    }

    private static DateTimeOffset UtcDate(int year, int month, int day)
        => new(year, month, day, 0, 0, 0, TimeSpan.Zero);

    private static DateTimeOffset UtcEndOfDay(int year, int month, int day)
        => new(new DateOnly(year, month, day).ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc));
}
