using Quartz.Spi;

namespace CanDoItAll.Modules.SchedulerPlanner;

internal enum SchedulerPlanProjectionKind { Disabled, Ready, Exhausted, RecoveryPending }

internal sealed record SchedulerPlanProjection(SchedulerPlanProjectionKind Kind, IOperableTrigger? Trigger) {
    internal static SchedulerPlanProjection Prepare(SchedulerPlan plan, IOperableTrigger trigger, SchedulerPlanRun? latest,
        SchedulerFireAdmissionRecord? admission, DateTimeOffset now) {
        var first = trigger.ComputeFirstFireTimeUtc(null);
        if (plan.EndAtUtc < first) {
            first = null;
        }
        var lastFire = latest?.FiredAtUtc ?? plan.LastFiredAtUtc;
        var unresolved = admission is not null ? admission.State != SchedulerFireAdmissionState.Observed
            : latest is { Status: not (SchedulerPlanRunDispatchStatus.Dispatched or SchedulerPlanRunDispatchStatus.NoMessages) };

        if (first.HasValue && lastFire.HasValue && first <= lastFire) {
            var remaining = ReadRemainingOccurrence(plan, latest, admission);
            first = remaining ?? trigger.GetFireTimeAfter(lastFire);
            if (unresolved && first <= now) {
                return new(SchedulerPlanProjectionKind.RecoveryPending, null);
            }
            if (first.HasValue) {
                trigger.StartTimeUtc = first.Value;
                first = trigger.ComputeFirstFireTimeUtc(null);
            }
        }
        if (!first.HasValue) {
            return new(unresolved ? SchedulerPlanProjectionKind.RecoveryPending : SchedulerPlanProjectionKind.Exhausted, null);
        }
        return new(SchedulerPlanProjectionKind.Ready, trigger);
    }

    private static DateTimeOffset? ReadRemainingOccurrence(SchedulerPlan plan, SchedulerPlanRun? latest,
        SchedulerFireAdmissionRecord? admission) {
        if (admission is not { State: SchedulerFireAdmissionState.Observed } || latest is null) {
            return null;
        }
        if (SchedulerFireSnapshot.Hash(admission.SnapshotJson) != admission.SnapshotFingerprint) {
            throw new InvalidOperationException("The saved Scheduler fire snapshot fingerprint does not match.");
        }
        var snapshot = SchedulerFireSnapshot.Parse(admission.SnapshotJson);
        if (snapshot.PlanId != plan.Id || snapshot.PlanRunId != latest.Id || snapshot.FiredAtUtc != latest.FiredAtUtc) {
            throw new InvalidOperationException("The saved Scheduler fire snapshot does not match its history.");
        }
        if (snapshot.NextPlannedFireAtUtc is not { } remaining || remaining >= latest.FiredAtUtc) {
            return null;
        }
        // A delayed IgnoreMisfire fire can leave an earlier, still-unconsumed occurrence.
        // Without the original schedule in the receipt, an edited schedule cannot safely adopt that cursor.
        if (plan.TargetId != snapshot.TargetId || plan.TargetVersionId != snapshot.TargetVersionId ||
            plan.MisfirePolicy != SchedulerPlanMisfirePolicy.IgnoreMisfire ||
            !new Quartz.CronExpression(plan.CronExpression) { TimeZone = TimeZoneInfo.FindSystemTimeZoneById(plan.TimeZoneId) }.IsSatisfiedBy(remaining) ||
            plan.StartAtUtc > remaining || plan.EndAtUtc < remaining) {
            throw new InvalidOperationException("The saved missed Scheduler occurrence requires reconciliation with the current plan.");
        }
        return remaining;
    }
}
