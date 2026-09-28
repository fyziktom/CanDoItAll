using System.Reflection;
using System.Text.Json;
using CanDoItAll.Modules.SchedulerPlanner;
using Quartz;

namespace CanDoItAll.Tests.Unit.SchedulerPlanner;

public sealed class SchedulerUiContractTests {
    [Theory]
    [InlineData("Europe/Prague", "2026-03-28T00:00:00Z", "2026-03-28T08:00:00Z", "2026-03-29T07:00:00Z")]
    [InlineData("Europe/Prague", "2026-10-24T00:00:00Z", "2026-10-24T07:00:00Z", "2026-10-25T08:00:00Z")]
    [InlineData("America/New_York", "2026-03-07T00:00:00Z", "2026-03-07T14:00:00Z", "2026-03-08T13:00:00Z")]
    [InlineData("America/New_York", "2026-10-31T00:00:00Z", "2026-10-31T13:00:00Z", "2026-11-01T14:00:00Z")]
    public void Installed_owner_projection_preserves_wall_clock_across_DST(string zone, string from, string first, string second) {
        var start = DateTimeOffset.Parse(from, System.Globalization.CultureInfo.InvariantCulture);
        var plan = Plan(zone);
        var occurrences = Project(plan, start, start.AddDays(3), 2);
        Assert.Equal([DateTimeOffset.Parse(first, System.Globalization.CultureInfo.InvariantCulture), DateTimeOffset.Parse(second, System.Globalization.CultureInfo.InvariantCulture)], occurrences);
        Assert.All(occurrences, time => Assert.Equal(TimeSpan.Zero, time.Offset));
    }

    [Fact]
    public void Installed_projection_keeps_start_end_and_count_bounds() {
        var from = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var plan = Plan("UTC");
        Assert.Equal(18, Project(plan, from, from.AddDays(30), 18).Length);
        var bounded = plan with { StartAtUtc = from.AddDays(2).AddHours(9), EndAtUtc = from.AddDays(3).AddHours(9) };
        Assert.Equal([bounded.StartAtUtc.Value, bounded.EndAtUtc.Value], Project(bounded, from, from.AddDays(30), 18));
    }

    [Theory]
    [InlineData(SchedulerPlanMisfirePolicy.FireOnceNow, MisfireInstruction.CronTrigger.FireOnceNow)]
    [InlineData(SchedulerPlanMisfirePolicy.DoNothing, MisfireInstruction.CronTrigger.DoNothing)]
    [InlineData(SchedulerPlanMisfirePolicy.IgnoreMisfire, MisfireInstruction.IgnoreMisfirePolicy)]
    public void Installed_trigger_builder_preserves_misfire_zone_and_UTC_bounds(SchedulerPlanMisfirePolicy policy, int instruction) {
        var start = new DateTimeOffset(2026, 3, 28, 0, 0, 0, TimeSpan.Zero);
        var plan = new SchedulerPlan { Name = "Fixture", CronExpression = "0 0 9 ? * *", TimeZoneId = "Europe/Prague",
            MisfirePolicy = policy, StartAtUtc = start, EndAtUtc = start.AddDays(5) };
        var method = typeof(SchedulerPlannerTriggerScheduler).GetMethod("BuildQuartzTrigger", BindingFlags.NonPublic | BindingFlags.Static)!;
        var trigger = Assert.IsAssignableFrom<ICronTrigger>(method.Invoke(null, [plan, new JobKey("fixture"), new TriggerKey("fixture")]));
        Assert.Equal(instruction, trigger.MisfireInstruction);
        Assert.Equal(plan.TimeZoneId, trigger.TimeZone.Id);
        Assert.Equal(plan.StartAtUtc, trigger.StartTimeUtc);
        Assert.Equal(plan.EndAtUtc, trigger.EndTimeUtc);
    }

    [Fact]
    public void Moved_contracts_keep_forwarders_enum_values_and_history_defaults_without_authority() {
        var forwarded = typeof(SchedulerPlannerService).Assembly.GetForwardedTypes();
        Assert.Contains(typeof(SchedulerPlanSummary), forwarded);
        Assert.Contains(typeof(SchedulerWorkflowInputSchema), forwarded);
        Assert.Contains(typeof(SchedulerHistoryQuery), forwarded);
        Assert.Equal("CanDoItAll.Modules.SchedulerPlanner.Contracts", typeof(SchedulerPlanSummary).Assembly.GetName().Name);
        Assert.Equal("CanDoItAll.Modules.SchedulerPlanner", typeof(SchedulerPlanEditorModel).Assembly.GetName().Name);
        Assert.Equal(0, (int)SchedulerPlanTargetKind.Process);
        Assert.Equal(1, (int)SchedulerPlanTargetKind.Workflow);
        Assert.Equal(6, (int)SchedulerPlanRunDispatchStatus.ObservationPending);
        Assert.Equal(50, JsonSerializer.Deserialize<SchedulerHistoryQuery>("{}")!.Take);
        Assert.DoesNotContain("StructureAuthority", JsonSerializer.Serialize(new SchedulerPlanEditorModel()), StringComparison.Ordinal);
    }

    private static DateTimeOffset[] Project(SchedulerPlanSummary plan, DateTimeOffset from, DateTimeOffset until, int count) {
        var method = typeof(SchedulerPlannerService).GetMethod("ProjectOccurrences", BindingFlags.NonPublic | BindingFlags.Static)!;
        return Assert.IsAssignableFrom<IEnumerable<DateTimeOffset>>(method.Invoke(null, [plan, from, until, count])).ToArray();
    }
    private static SchedulerPlanSummary Plan(string zone) => new(Guid.NewGuid(), "Fixture", string.Empty, SchedulerPlanTargetKind.Workflow,
        Guid.NewGuid(), Guid.NewGuid(), "Fixture Workflow", "0 0 9 ? * *", "Daily nine", zone, SchedulerPlanMisfirePolicy.FireOnceNow,
        true, null, null, null, null, string.Empty, new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
}
