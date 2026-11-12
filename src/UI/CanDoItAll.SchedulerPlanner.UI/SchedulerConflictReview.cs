using CanDoItAll.Editing;
using CanDoItAll.Modules.SchedulerPlanner;

namespace CanDoItAll.SchedulerPlanner.UI;

public sealed class SchedulerConflictReview {
    public SchedulerConflictReview(SchedulerDraft draft, SchedulerDraftValues current) {
        var original = draft.Baseline;
        var local = draft.Values;
        Revision = draft.Revision;
        Current = current;
        Name = new(original.Name, local.Name, current.Name);
        Description = TextMerge.Compare(original.Description, local.Description, current.Description).Field;
        Recurrence = new(ScheduleRecurrence.From(original), ScheduleRecurrence.From(local), ScheduleRecurrence.From(current));
        Target = new(ScheduleTargetInput.From(original), ScheduleTargetInput.From(local), ScheduleTargetInput.From(current));
        Enabled = new(original.IsEnabled, local.IsEnabled, current.IsEnabled);
        HasUnfinishedInput = draft.HasUnappliedInput && draft.Issues.Count > 0;
        if (HasUnfinishedInput) {
            Target.RequireChoice();
        }
    }

    public long Revision { get; }
    public SchedulerDraftValues Current { get; }
    public MergeField<string> Name { get; }
    public MergeField<string> Description { get; }
    public MergeField<ScheduleRecurrence> Recurrence { get; }
    public MergeField<ScheduleTargetInput> Target { get; }
    public MergeField<bool> Enabled { get; }
    public bool HasUnfinishedInput { get; }
    public bool IsResolved => Name.IsResolved && Description.IsResolved && Recurrence.IsResolved && Target.IsResolved && Enabled.IsResolved;
    public bool RetainsInput => Target.Result == Target.Local && Target.Choice != MergeChoice.Current;

    public SchedulerDraftValues Apply() => Current with {
        Name = Name.Result,
        Description = Description.Result,
        CronExpression = Recurrence.Result.Cron,
        TimeZoneId = Recurrence.Result.TimeZone,
        StartAtUtc = Recurrence.Result.Start,
        EndAtUtc = Recurrence.Result.End,
        MisfirePolicy = Recurrence.Result.Misfire,
        TargetKind = Target.Result.Kind,
        TargetId = Target.Result.Id,
        TargetVersionId = Target.Result.Version,
        InputJson = Target.Result.Input,
        IsEnabled = Enabled.Result
    };

    public sealed record ScheduleRecurrence(string Cron, string TimeZone, DateTimeOffset? Start, DateTimeOffset? End, SchedulerPlanMisfirePolicy Misfire) {
        public static ScheduleRecurrence From(SchedulerDraftValues values)
            => new(values.CronExpression, values.TimeZoneId, values.StartAtUtc, values.EndAtUtc, values.MisfirePolicy);
    }

    public sealed record ScheduleTargetInput(SchedulerPlanTargetKind Kind, Guid Id, Guid? Version, string Input) {
        public static ScheduleTargetInput From(SchedulerDraftValues values)
            => new(values.TargetKind, values.TargetId, values.TargetVersionId, values.InputJson);
    }
}
