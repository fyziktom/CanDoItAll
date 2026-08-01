using System.Globalization;
using CanDoItAll.Components.Gantt;
using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Modules.CrmHr;

internal static class ProjectAssignmentGanttProjectionAdapter
{
    private const int PastHorizonDays = 30;
    private const int FutureHorizonDays = 90;
    private const int FutureOpenAssignmentDays = 30;
    private static readonly DateOnly MaximumRenderableEnd = DateOnly.MaxValue.AddDays(-1);

    public static ProjectAssignmentGanttProjection Build(
        IReadOnlyList<ProjectPartyAssignmentDetail> assignments,
        DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(assignments);

        var issues = new List<ProjectAssignmentGanttProjectionIssue>();
        var candidates = new List<ProjectionCandidate>(assignments.Count);
        foreach (var assignment in assignments)
        {
            var startsOn = ToUtcDate(assignment.StartsAtUtc);
            var endsOn = ToUtcDate(assignment.EndsAtUtc);
            var issue = Validate(assignment, startsOn, endsOn);
            if (issue is not null)
            {
                issues.Add(issue);
                continue;
            }

            candidates.Add(new ProjectionCandidate(assignment, startsOn, endsOn));
        }

        var horizonStart = AddDaysClamped(today, -PastHorizonDays, DateOnly.MaxValue);
        var horizonEndInclusive = AddDaysClamped(today, FutureHorizonDays, MaximumRenderableEnd);
        foreach (var candidate in candidates)
        {
            if (candidate.StartsOn is { } startsOn)
            {
                if (startsOn < horizonStart)
                {
                    horizonStart = startsOn;
                }

                if (startsOn > horizonEndInclusive)
                {
                    horizonEndInclusive = AddDaysClamped(
                        startsOn,
                        FutureOpenAssignmentDays,
                        MaximumRenderableEnd);
                }
            }

            if (candidate.EndsOn is { } endsOn)
            {
                if (endsOn < horizonStart)
                {
                    horizonStart = endsOn;
                }

                if (endsOn > horizonEndInclusive)
                {
                    horizonEndInclusive = endsOn;
                }
            }
        }

        var tasks = candidates
            .Select(candidate => BuildTask(candidate, horizonStart, horizonEndInclusive))
            .OrderBy(task => task.Start)
            .ThenBy(task => task.Title, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new ProjectAssignmentGanttProjection(
            tasks,
            issues,
            horizonStart,
            horizonEndInclusive,
            candidates.Any(candidate => !candidate.StartsOn.HasValue || !candidate.EndsOn.HasValue));
    }

    private static ProjectAssignmentGanttProjectionIssue? Validate(
        ProjectPartyAssignmentDetail assignment,
        DateOnly? startsOn,
        DateOnly? endsOn)
    {
        if (string.IsNullOrWhiteSpace(assignment.PartyDisplayName))
        {
            return new ProjectAssignmentGanttProjectionIssue(
                assignment,
                ProjectAssignmentGanttProjectionIssueCode.MissingPartyName,
                "The assigned resource has no display name.");
        }

        if (startsOn == DateOnly.MaxValue || endsOn == DateOnly.MaxValue)
        {
            return new ProjectAssignmentGanttProjectionIssue(
                assignment,
                ProjectAssignmentGanttProjectionIssueCode.UnrepresentableDate,
                "The assignment ends outside the chart's representable date range.");
        }

        if (startsOn.HasValue && endsOn.HasValue && startsOn.Value > endsOn.Value)
        {
            return new ProjectAssignmentGanttProjectionIssue(
                assignment,
                ProjectAssignmentGanttProjectionIssueCode.InvalidDateRange,
                "The assignment start date is after its end date.");
        }

        return null;
    }

    private static GanttTask BuildTask(
        ProjectionCandidate candidate,
        DateOnly horizonStart,
        DateOnly horizonEndInclusive)
    {
        var assignment = candidate.Assignment;
        var allocation = assignment.AllocationPercent is { } allocationPercent
            ? string.Create(CultureInfo.InvariantCulture, $"{allocationPercent:0.##}% allocated")
            : "allocation not set";
        var title = $"{assignment.PartyDisplayName.Trim()} · {ProjectPartyAssignmentPresentation.ResolveRoleLabel(assignment.Role)} · {allocation}";
        var decorations = TryResolveAssignmentKind(assignment.PartyType, out var kind)
            ? new[] { new GanttAssignment(kind, assignment.PartyDisplayName) }
            : [];

        return new GanttTask(
            new GanttTaskId($"assignment:{assignment.Id:D}"),
            title,
            ToUtcDateTimeOffset(candidate.StartsOn ?? horizonStart),
            ToUtcEndOfDay(candidate.EndsOn ?? horizonEndInclusive),
            decorations);
    }

    private static bool TryResolveAssignmentKind(
        ProjectPartyType partyType,
        out GanttAssignmentKind assignmentKind)
    {
        switch (partyType)
        {
            case ProjectPartyType.Person:
                assignmentKind = GanttAssignmentKind.Person;
                return true;
            case ProjectPartyType.AiAgent:
                assignmentKind = GanttAssignmentKind.Agent;
                return true;
            case ProjectPartyType.Organization:
            case ProjectPartyType.OrganizationUnit:
                assignmentKind = default;
                return false;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(partyType),
                    partyType,
                    "The project party type is not supported.");
        }
    }

    private static DateOnly? ToUtcDate(DateTimeOffset? value)
        => value.HasValue ? DateOnly.FromDateTime(value.Value.UtcDateTime) : null;

    private static DateTimeOffset ToUtcDateTimeOffset(DateOnly value)
        => new(value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));

    private static DateTimeOffset ToUtcEndOfDay(DateOnly value)
        => new(value.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc));

    private static DateOnly AddDaysClamped(DateOnly value, int days, DateOnly maximum)
    {
        var dayNumber = Math.Clamp(
            (long)value.DayNumber + days,
            DateOnly.MinValue.DayNumber,
            maximum.DayNumber);
        return DateOnly.FromDayNumber((int)dayNumber);
    }

    private sealed record ProjectionCandidate(
        ProjectPartyAssignmentDetail Assignment,
        DateOnly? StartsOn,
        DateOnly? EndsOn);
}

internal sealed record ProjectAssignmentGanttProjection(
    IReadOnlyList<GanttTask> Tasks,
    IReadOnlyList<ProjectAssignmentGanttProjectionIssue> Issues,
    DateOnly HorizonStart,
    DateOnly HorizonEndInclusive,
    bool HasOpenBoundary);

internal sealed record ProjectAssignmentGanttProjectionIssue(
    ProjectPartyAssignmentDetail Assignment,
    ProjectAssignmentGanttProjectionIssueCode Code,
    string Message);

internal enum ProjectAssignmentGanttProjectionIssueCode
{
    InvalidDateRange,
    UnrepresentableDate,
    MissingPartyName
}
