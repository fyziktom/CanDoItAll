using System.Text.Json;
using CanDoItAll.Components.Gantt;
using CanDoItAll.Modules.Workbench;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class ProjectStructureInitialTaskScheduleTests {
    private static readonly DateTimeOffset Start = new(2027, 2, 5, 9, 0, 0, TimeSpan.Zero);
    private static readonly GanttTaskId TaskId = new("custom:0123456789abcdef0123456789abcdef");

    [Fact]
    public void Initial_schedule_accepts_the_exact_unscheduled_snapshot() {
        var schedule = new ProjectStructureInitialTaskSchedule(null, null, 3600, Start, Start.AddHours(2));
        schedule.Validate();
        schedule.RequireCurrentState(TaskId, null, null, 3600);
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void Initial_schedule_rejects_changed_dates_or_duration(bool changedStart, bool changedEnd, bool changedDuration) {
        var schedule = new ProjectStructureInitialTaskSchedule(null, null, null, Start, Start.AddHours(1));
        var exception = Assert.Throws<ProjectStructureGanttMutationException>(() => schedule.RequireCurrentState(TaskId,
            changedStart ? Start : null, changedEnd ? Start.AddHours(1) : null, changedDuration ? 3600 : null));
        Assert.Equal(ProjectStructureGanttMutationErrorCode.StaleTask, exception.Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Initial_schedule_rejects_empty_or_inverted_intervals(int hours) {
        Assert.Throws<ArgumentException>(() => new ProjectStructureInitialTaskSchedule(null, null, null, Start, Start.AddHours(hours)).Validate());
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void Initial_schedule_does_not_repurpose_an_existing_interval(bool startExists, bool endExists) {
        Assert.Throws<ArgumentException>(() => new ProjectStructureInitialTaskSchedule(startExists ? Start : null,
            endExists ? Start.AddHours(1) : null, null, Start, Start.AddHours(2)).Validate());
    }

    [Theory]
    [InlineData("currentStartUtc")]
    [InlineData("currentEndUtc")]
    [InlineData("currentDurationSeconds")]
    [InlineData("proposedStartUtc")]
    [InlineData("proposedEndUtc")]
    public void Initial_schedule_requires_explicit_current_and_proposed_values(string omitted) {
        var json = JsonSerializer.SerializeToNode(new ProjectStructureInitialTaskSchedule(null, null, null, Start, Start.AddHours(1)),
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!.AsObject();
        json.Remove(omitted);
        Assert.Throws<JsonException>(() => json.Deserialize<ProjectStructureInitialTaskSchedule>(new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }
}
