using CanDoItAll.AppComponents.Navigation;
using CanDoItAll.Modules.SchedulerPlanner.Presentation;
using CanDoItAll.SchedulerPlanner.UI;

namespace CanDoItAll.Modules.SchedulerPlanner.Pages;

// /scheduler: the tab, the plan whose editor is open, the calendar position on the calendar tab, the schedule
// list filters on the schedules tab and the committed history query, which shapes every workspace read.
public sealed class SchedulerUrlCodec : IUrlStateCodec<SchedulerRoute> {
    public const string TabKey = "tab";
    public const string PlanKey = "planId";
    public const string ViewKey = "view";
    public const string DateKey = "date";
    public const string SearchKey = "q";
    public const string KindKey = "kind";
    public const string StateKey = "state";
    public const string HistorySearchKey = "historyQ";
    public const string HistoryStatusKey = "historyStatus";
    public const string HistoryKindKey = "historyKind";
    public const int MaximumTextLength = 200;

    public static SchedulerUrlCodec Instance { get; } = new();

    public static UrlTokenMap<SchedulerTab> Tabs { get; } = new(
        (SchedulerTab.Calendar, "calendar"),
        (SchedulerTab.Schedules, "schedules"),
        (SchedulerTab.NewSchedule, "new"),
        (SchedulerTab.History, "history"));

    public static UrlTokenMap<SchedulerCalendarView> Views { get; } = new(
        (SchedulerCalendarView.Day, "day"),
        (SchedulerCalendarView.Week, "week"),
        (SchedulerCalendarView.Month, "month"),
        (SchedulerCalendarView.Year, "year"),
        (SchedulerCalendarView.List, "list"));

    public static UrlTokenMap<SchedulerPlanTargetKind> Kinds { get; } = new(
        (SchedulerPlanTargetKind.Process, "process"),
        (SchedulerPlanTargetKind.Workflow, "workflow"));

    public static UrlTokenMap<ScheduleStateFilter> States { get; } = new(
        (ScheduleStateFilter.All, "all"),
        (ScheduleStateFilter.Enabled, "enabled"),
        (ScheduleStateFilter.Paused, "paused"));

    public static UrlTokenMap<SchedulerPlanRunDispatchStatus> Statuses { get; } = new(
        (SchedulerPlanRunDispatchStatus.Received, "received"),
        (SchedulerPlanRunDispatchStatus.Dispatching, "dispatching"),
        (SchedulerPlanRunDispatchStatus.Dispatched, "dispatched"),
        (SchedulerPlanRunDispatchStatus.Failed, "failed"),
        (SchedulerPlanRunDispatchStatus.NoMessages, "no-messages"),
        (SchedulerPlanRunDispatchStatus.WaitingForApproval, "waiting-for-approval"),
        (SchedulerPlanRunDispatchStatus.ObservationPending, "observation-pending"));

    public IReadOnlyList<string> Keys { get; } = [
        TabKey, PlanKey, ViewKey, DateKey, SearchKey, KindKey, StateKey, HistorySearchKey, HistoryStatusKey, HistoryKindKey
    ];

    public SchedulerRoute Read(UrlQuery query) {
        var tab = query.GetToken(TabKey, Tabs, SchedulerTab.Calendar);
        var onCalendar = tab == SchedulerTab.Calendar;
        var onSchedules = tab == SchedulerTab.Schedules;
        var view = onCalendar ? query.GetToken(ViewKey, Views) : null;
        return new(
            tab,
            query.GetId(PlanKey),
            view == SchedulerCalendarView.Week ? null : view,
            onCalendar ? query.GetDate(DateKey) : null,
            onSchedules ? query.GetText(SearchKey, MaximumTextLength) ?? string.Empty : string.Empty,
            onSchedules ? query.GetToken(KindKey, Kinds) : null,
            onSchedules ? query.GetToken(StateKey, States, ScheduleStateFilter.All) : ScheduleStateFilter.All,
            query.GetText(HistorySearchKey, MaximumTextLength) ?? string.Empty,
            query.GetToken(HistoryStatusKey, Statuses),
            query.GetToken(HistoryKindKey, Kinds));
    }

    public void Write(SchedulerRoute state, UrlQueryWriter query) {
        query.Token(TabKey, state.Tab, Tabs, SchedulerTab.Calendar);
        query.Id(PlanKey, state.PlanId);
        if (state.Tab == SchedulerTab.Calendar) {
            query.Token(ViewKey, state.CalendarView ?? SchedulerCalendarView.Week, Views, SchedulerCalendarView.Week);
            query.Date(DateKey, state.CalendarDate);
        }
        if (state.Tab == SchedulerTab.Schedules) {
            query.Text(SearchKey, Bounded(state.ScheduleSearch));
            query.Token(KindKey, state.ScheduleKind, Kinds);
            query.Token(StateKey, state.ScheduleState, States, ScheduleStateFilter.All);
        }
        query.Text(HistorySearchKey, Bounded(state.HistorySearch));
        query.Token(HistoryStatusKey, state.HistoryStatus, Statuses);
        query.Token(HistoryKindKey, state.HistoryKind, Kinds);
    }

    // Text that Read would drop is never written, so the written form always reads back.
    private static string? Bounded(string? text)
        => text?.Trim() is { Length: > 0 and <= MaximumTextLength } bounded ? bounded : null;
}
