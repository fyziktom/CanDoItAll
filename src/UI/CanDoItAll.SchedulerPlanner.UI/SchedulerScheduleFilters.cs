using CanDoItAll.Modules.SchedulerPlanner;

namespace CanDoItAll.SchedulerPlanner.UI;

public sealed record SchedulerScheduleFilters(
    string Search = "",
    SchedulerPlanTargetKind? TargetKind = null,
    ScheduleStateFilter State = ScheduleStateFilter.All) {
    public bool IsEmpty => Search.Length == 0 && TargetKind is null && State == ScheduleStateFilter.All;
}
