using CanDoItAll.Modules.SchedulerPlanner;

namespace CanDoItAll.SchedulerPlanner.UI;

public sealed class SchedulerScheduleFilters {
    public string Search { get; set; } = string.Empty;
    public SchedulerPlanTargetKind? TargetKind { get; set; }
    public ScheduleStateFilter State { get; set; }
}
