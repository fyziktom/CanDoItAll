using CanDoItAll.SchedulerPlanner.UI;

namespace CanDoItAll.Modules.SchedulerPlanner.Presentation;

public enum SchedulerCalendarView { Day, Week, Month, Year, List }

// The workspace state a link reproduces. The routed page maps it to its address; drafts, the target picker and
// confirmations stay outside it. A null calendar position means the calendar's own default view and date.
public sealed record SchedulerRoute(
    SchedulerTab Tab = SchedulerTab.Calendar,
    Guid? PlanId = null,
    SchedulerCalendarView? CalendarView = null,
    DateOnly? CalendarDate = null,
    string ScheduleSearch = "",
    SchedulerPlanTargetKind? ScheduleKind = null,
    ScheduleStateFilter ScheduleState = ScheduleStateFilter.All,
    string HistorySearch = "",
    SchedulerPlanRunDispatchStatus? HistoryStatus = null,
    SchedulerPlanTargetKind? HistoryKind = null) {
    public SchedulerScheduleFilters ScheduleFilters => new(ScheduleSearch, ScheduleKind, ScheduleState);
}

// What a user intent changed, so the page records it with the matching history rule.
public enum SchedulerNavigationKind {
    Tab,
    OpenEditor,
    CloseEditor,
    EditorRemoved,
    Calendar,
    ScheduleFilters,
    HistorySearch
}
