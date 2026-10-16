using CanDoItAll.AppComponents.Navigation;
using CanDoItAll.Modules.SchedulerPlanner;
using CanDoItAll.Modules.SchedulerPlanner.Pages;
using CanDoItAll.Modules.SchedulerPlanner.Presentation;
using CanDoItAll.SchedulerPlanner.UI;

namespace CanDoItAll.Tests.Unit.SchedulerPlanner;

public sealed class SchedulerUrlCodecTests {
    private const string Plan = "16f257ae-ed84-4282-a2e0-c3710ac03f46";
    private static readonly SchedulerUrlCodec Codec = SchedulerUrlCodec.Instance;

    [Theory]
    [InlineData("", "")]
    [InlineData("?tab=calendar", "")]
    [InlineData("?TAB=Schedules", "?tab=schedules")]
    [InlineData("?tab=new", "?tab=new")]
    [InlineData("?tab=NewSchedule", "")]
    [InlineData("?tab=unknown", "")]
    [InlineData("?planId=16F257AE-ED84-4282-A2E0-C3710AC03F46", "?planId=" + Plan)]
    [InlineData("?planId=not-a-plan", "")]
    [InlineData("?planId=00000000-0000-0000-0000-000000000000", "")]
    [InlineData("?tab=history&planId=" + Plan, "?tab=history&planId=" + Plan)]
    [InlineData("?view=month&date=2026-10-15", "?view=month&date=2026-10-15")]
    [InlineData("?view=week", "")]
    [InlineData("?view=agenda&date=2026-13-01", "")]
    [InlineData("?date=15.10.2026", "")]
    [InlineData("?tab=schedules&view=month&date=2026-10-15", "?tab=schedules")]
    [InlineData("?tab=schedules&q=%20%20Paused%20%20&kind=Workflow&state=paused", "?tab=schedules&q=Paused&kind=workflow&state=paused")]
    [InlineData("?tab=schedules&state=all&kind=job", "?tab=schedules")]
    [InlineData("?q=Paused&kind=process&state=enabled", "")]
    [InlineData("?historyQ=onboarding&historyStatus=no-messages&historyKind=process", "?historyQ=onboarding&historyStatus=no-messages&historyKind=process")]
    [InlineData("?tab=history&historyStatus=NoMessages", "?tab=history")]
    [InlineData("?historyStatus=waiting-for-approval&tab=new", "?tab=new&historyStatus=waiting-for-approval")]
    public void Locations_have_one_stable_canonical_form(string query, string canonical) {
        var state = Codec.Read(UrlQuery.Parse(query));

        Assert.Equal(canonical, Codec.Format(state));
        Assert.Equal(state, Codec.Normalize(state));
        Assert.Equal(canonical, Codec.Format(Codec.Read(UrlQuery.Parse(canonical))));
    }

    [Fact]
    public void Every_state_value_round_trips_in_a_fixed_key_order() {
        var schedules = new SchedulerRoute(SchedulerTab.Schedules, Guid.Parse(Plan), ScheduleSearch: "report", ScheduleKind: SchedulerPlanTargetKind.Workflow,
            ScheduleState: ScheduleStateFilter.Enabled, HistorySearch: "night", HistoryStatus: SchedulerPlanRunDispatchStatus.ObservationPending,
            HistoryKind: SchedulerPlanTargetKind.Process);
        var calendar = new SchedulerRoute(PlanId: Guid.Parse(Plan), CalendarView: SchedulerCalendarView.Year, CalendarDate: new DateOnly(2027, 1, 31));

        Assert.Equal($"?tab=schedules&planId={Plan}&q=report&kind=workflow&state=enabled&historyQ=night&historyStatus=observation-pending&historyKind=process",
            Codec.Format(schedules));
        Assert.Equal(schedules, Codec.Read(UrlQuery.Parse(Codec.Format(schedules))));
        Assert.Equal($"?planId={Plan}&view=year&date=2027-01-31", Codec.Format(calendar));
        Assert.Equal(calendar, Codec.Read(UrlQuery.Parse(Codec.Format(calendar))));
    }

    [Fact]
    public void Tab_dependent_state_is_written_only_where_its_tab_shows_it() {
        var remembered = new SchedulerRoute(SchedulerTab.History, CalendarView: SchedulerCalendarView.Month, CalendarDate: new DateOnly(2026, 10, 15),
            ScheduleSearch: "report", ScheduleState: ScheduleStateFilter.Paused, HistorySearch: "night");

        Assert.Equal("?tab=history&historyQ=night", Codec.Format(remembered));
        Assert.Equal("?view=month&date=2026-10-15&historyQ=night", Codec.Format(remembered with { Tab = SchedulerTab.Calendar }));
        Assert.Equal("?tab=schedules&q=report&state=paused&historyQ=night", Codec.Format(remembered with { Tab = SchedulerTab.Schedules }));
    }

    [Fact]
    public void Text_beyond_the_bound_is_neither_read_nor_written() {
        var tooLong = new string('a', SchedulerUrlCodec.MaximumTextLength + 1);

        Assert.Equal("?tab=schedules", Codec.Format(new SchedulerRoute(SchedulerTab.Schedules, ScheduleSearch: tooLong)));
        Assert.Equal(string.Empty, Codec.Read(UrlQuery.Parse("?historyQ=" + tooLong)).HistorySearch);
    }

    [Fact]
    public void Composing_keeps_foreign_parameters_and_the_fragment() {
        var composed = UrlLocation.Compose("http://localhost/scheduler?utm=mail&PLANID=" + Plan + "&q=old#calendar", Codec,
            new SchedulerRoute(SchedulerTab.Schedules, ScheduleSearch: "new"));

        Assert.Equal("http://localhost/scheduler?tab=schedules&q=new&utm=mail#calendar", composed);
    }

    [Fact]
    public void Token_maps_cover_every_typed_value() {
        Assert.All(Enum.GetValues<SchedulerTab>(), tab => Assert.True(SchedulerUrlCodec.Tabs.Contains(tab)));
        Assert.All(Enum.GetValues<SchedulerCalendarView>(), view => Assert.True(SchedulerUrlCodec.Views.Contains(view)));
        Assert.All(Enum.GetValues<SchedulerPlanTargetKind>(), kind => Assert.True(SchedulerUrlCodec.Kinds.Contains(kind)));
        Assert.All(Enum.GetValues<ScheduleStateFilter>(), state => Assert.True(SchedulerUrlCodec.States.Contains(state)));
        Assert.All(Enum.GetValues<SchedulerPlanRunDispatchStatus>(), status => Assert.True(SchedulerUrlCodec.Statuses.Contains(status)));
    }

    [Fact]
    public void Every_owned_key_has_a_query_trigger_on_the_routed_page() {
        Assert.Empty(UrlStateTriggers.Missing(typeof(SchedulerPlannerPage), Codec.Keys));
    }
}
