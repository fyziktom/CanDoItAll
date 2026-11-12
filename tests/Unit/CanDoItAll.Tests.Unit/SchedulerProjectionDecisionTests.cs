using System.Globalization;
using System.Reflection;
using CanDoItAll.Modules.SchedulerPlanner;
using Quartz;
using Quartz.Spi;

namespace CanDoItAll.Tests.Unit.SchedulerPlanner;

public sealed class SchedulerProjectionDecisionTests {
    public enum ScheduleCase { Future, Missed, ExpiredEnd, Completed, Unresolved, ImplicitExpiredEnd }

    public static IEnumerable<object[]> Cases() {
        foreach (var policy in Enum.GetValues<SchedulerPlanMisfirePolicy>()) {
            foreach (var scenario in Enum.GetValues<ScheduleCase>()) {
                yield return [policy, scenario];
            }
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Actual_Quartz_projection_distinguishes_remaining_missed_consumed_and_unresolved_work(
        SchedulerPlanMisfirePolicy policy, ScheduleCase scenario) {
        var now = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        var occurrence = scenario == ScheduleCase.Future ? now.AddDays(2) : now.AddYears(-1);
        var plan = new SchedulerPlan {
            CronExpression = occurrence.ToString("s m H d M '?' yyyy", CultureInfo.InvariantCulture),
            TimeZoneId = "UTC", MisfirePolicy = policy, StartAtUtc = occurrence.AddMinutes(-5), CreatedAtUtc = occurrence.AddDays(-1)
        };
        if (scenario is ScheduleCase.ExpiredEnd or ScheduleCase.ImplicitExpiredEnd) {
            plan.EndAtUtc = occurrence.AddMinutes(5);
        }
        if (scenario == ScheduleCase.ImplicitExpiredEnd) {
            plan.StartAtUtc = null;
        }
        SchedulerPlanRun? history = scenario is ScheduleCase.Completed or ScheduleCase.Unresolved ? new() {
            PlanId = plan.Id, FiredAtUtc = occurrence,
            Status = scenario == ScheduleCase.Completed ? SchedulerPlanRunDispatchStatus.Dispatched : SchedulerPlanRunDispatchStatus.Dispatching
        } : null;
        var trigger = Build(plan);
        var result = SchedulerPlanProjection.Prepare(plan, trigger, history, null, now);
        if (scenario is ScheduleCase.Completed or ScheduleCase.ImplicitExpiredEnd or ScheduleCase.Unresolved) {
            Assert.Equal(scenario == ScheduleCase.Unresolved ? SchedulerPlanProjectionKind.RecoveryPending : SchedulerPlanProjectionKind.Exhausted, result.Kind);
            Assert.Null(result.Trigger);
            Assert.True(plan.IsEnabled);
            return;
        }
        Assert.Equal(SchedulerPlanProjectionKind.Ready, result.Kind);
        Assert.Same(trigger, result.Trigger);
        Assert.Equal(occurrence, trigger.GetNextFireTimeUtc());
        if (scenario == ScheduleCase.Future) {
            return;
        }
        var before = DateTimeOffset.UtcNow;
        trigger.UpdateAfterMisfire(null);
        var after = DateTimeOffset.UtcNow;
        if (policy == SchedulerPlanMisfirePolicy.DoNothing) {
            Assert.Null(trigger.GetNextFireTimeUtc());
        } else if (policy == SchedulerPlanMisfirePolicy.FireOnceNow) {
            Assert.InRange(trigger.GetNextFireTimeUtc()!.Value, before, after);
        } else {
            Assert.Equal(occurrence, trigger.GetNextFireTimeUtc());
        }
    }

    [Fact]
    public void Delayed_ignore_misfire_resumes_the_native_receipts_unconsumed_occurrence() {
        var now = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        var plan = new SchedulerPlan { CronExpression = "0 * * ? * *", StartAtUtc = now.AddHours(-1), MisfirePolicy = SchedulerPlanMisfirePolicy.IgnoreMisfire };
        var history = new SchedulerPlanRun { PlanId = plan.Id, FiredAtUtc = now, Status = SchedulerPlanRunDispatchStatus.Dispatched };
        var remaining = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, TimeSpan.Zero).AddMinutes(-20);
        var snapshot = new SchedulerFireSnapshot(plan.Id, history.Id, Guid.NewGuid(), Guid.NewGuid(), now, remaining, "Owned",
            plan.TargetKind, plan.TargetId, plan.TargetVersionId, "Owned", "{}", null);
        var admission = new SchedulerFireAdmissionRecord { Id = history.Id, PlanId = plan.Id, State = SchedulerFireAdmissionState.Observed,
            SnapshotJson = snapshot.ToJson(), SnapshotFingerprint = snapshot.Fingerprint() };
        var result = SchedulerPlanProjection.Prepare(plan, Build(plan), history, admission, now);
        Assert.Equal(SchedulerPlanProjectionKind.Ready, result.Kind);
        Assert.Equal(remaining, result.Trigger!.GetNextFireTimeUtc());
        admission.State = SchedulerFireAdmissionState.Prepared;
        var unresolved = SchedulerPlanProjection.Prepare(plan, Build(plan), history, admission, now.AddHours(1));
        Assert.Equal(SchedulerPlanProjectionKind.RecoveryPending, unresolved.Kind);
        Assert.Null(unresolved.Trigger);
        admission.State = SchedulerFireAdmissionState.Observed;
        plan.TargetId = Guid.NewGuid();
        Assert.Throws<InvalidOperationException>(() => SchedulerPlanProjection.Prepare(plan, Build(plan), history, admission, now));
    }

    [Theory]
    [InlineData("Europe/Prague", "2026-03-28T08:00:00Z", "2026-03-29T07:00:00Z")]
    [InlineData("Europe/Prague", "2026-10-24T07:00:00Z", "2026-10-25T08:00:00Z")]
    [InlineData("America/New_York", "2026-03-07T14:00:00Z", "2026-03-08T13:00:00Z")]
    public void Consumed_occurrence_advances_using_installed_timezone_and_DST_rules(string zone, string fired, string expected) {
        var last = DateTimeOffset.Parse(fired, CultureInfo.InvariantCulture);
        var plan = new SchedulerPlan { CronExpression = "0 0 9 ? * *", TimeZoneId = zone, StartAtUtc = last.AddDays(-1), LastFiredAtUtc = last };
        var result = SchedulerPlanProjection.Prepare(plan, Build(plan), null, null, last);
        Assert.Equal(DateTimeOffset.Parse(expected, CultureInfo.InvariantCulture), result.Trigger!.GetNextFireTimeUtc());
        Assert.Null(result.Trigger.CalendarName);
    }

    [Fact]
    public void Invalid_configuration_is_not_reclassified_as_exhaustion() {
        var plan = new SchedulerPlan { CronExpression = "invalid" };
        Assert.IsType<FormatException>(Assert.Throws<TargetInvocationException>(() => Build(plan)).InnerException);
        plan.CronExpression = "0 * * ? * *";
        plan.TimeZoneId = "Owned/Missing-Time-Zone";
        Assert.IsType<TimeZoneNotFoundException>(Assert.Throws<TargetInvocationException>(() => Build(plan)).InnerException);
        plan.TimeZoneId = "UTC";
        plan.StartAtUtc = DateTimeOffset.UtcNow;
        plan.EndAtUtc = plan.StartAtUtc.Value.AddDays(-1);
        Assert.IsAssignableFrom<ArgumentException>(Assert.Throws<TargetInvocationException>(() => Build(plan)).InnerException);
    }

    private static IOperableTrigger Build(SchedulerPlan plan) {
        var method = typeof(SchedulerPlannerTriggerScheduler).GetMethod("BuildQuartzTrigger", BindingFlags.NonPublic | BindingFlags.Static)!;
        var trigger = Assert.IsAssignableFrom<IOperableTrigger>(method.Invoke(null, [plan, new JobKey("owned"), new TriggerKey("owned")]));
        trigger.Validate();
        return trigger;
    }
}
