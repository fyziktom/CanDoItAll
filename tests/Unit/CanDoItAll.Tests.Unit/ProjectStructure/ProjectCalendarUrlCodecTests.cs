using CanDoItAll.AppComponents.Navigation;
using CanDoItAll.Modules.Workbench.Pages;

namespace CanDoItAll.Tests.Unit.ProjectStructure;

public sealed class ProjectCalendarUrlCodecTests {
    private const string Event = "4b1f0a51-6a3d-4a8f-9f0e-0c2d6f7a4b11";
    private static readonly ProjectCalendarUrlCodec Codec = ProjectCalendarUrlCodec.Instance;

    [Theory]
    [InlineData("", "")]
    [InlineData("?view=week", "?view=week")]
    [InlineData("?eventId=4B1F0A51-6A3D-4A8F-9F0E-0C2D6F7A4B11&date=2026-10-05&view=MONTH", "?view=month&date=2026-10-05&eventId=" + Event)]
    [InlineData("?view=agenda&date=2026-02-30&eventId=not-a-guid", "")]
    [InlineData("?date=05.10.2026", "")]
    [InlineData("?view=list&date=2026-10-05", "?view=list&date=2026-10-05")]
    public void Locations_have_one_stable_canonical_form(string query, string canonical) {
        var state = Codec.Read(UrlQuery.Parse(query));

        Assert.Equal(canonical, Codec.Format(state));
        Assert.Equal(state, Codec.Normalize(state));
        Assert.Equal(canonical, Codec.Format(Codec.Read(UrlQuery.Parse(canonical))));
    }

    [Fact]
    public void An_absent_view_or_date_stays_absent_so_the_persisted_preference_applies() {
        var state = Codec.Read(UrlQuery.Parse("?eventId=" + Event));

        Assert.Null(state.View);
        Assert.Null(state.Date);
        Assert.Equal(Guid.Parse(Event), state.EventId);
    }

    [Theory]
    [InlineData("day", ProjectCalendarView.Day)]
    [InlineData("week", ProjectCalendarView.Week)]
    [InlineData("month", ProjectCalendarView.Month)]
    [InlineData("list", ProjectCalendarView.List)]
    [InlineData("year", ProjectCalendarView.Year)]
    public void Native_calendar_views_map_to_url_tokens(string nativeView, ProjectCalendarView view) {
        Assert.Equal(view, ProjectCalendarUrlCodec.ParseView(nativeView));
        Assert.Equal(nativeView, ProjectCalendarUrlCodec.Views.Format(view));
    }

    [Fact]
    public void Native_date_keys_round_trip_and_invalid_keys_are_absent() {
        var date = new DateOnly(2026, 10, 5);

        Assert.Equal(date, ProjectCalendarUrlCodec.ParseDate(" 2026-10-05 "));
        Assert.Equal("2026-10-05", ProjectCalendarUrlCodec.FormatDate(date));
        Assert.Null(ProjectCalendarUrlCodec.ParseDate("2026-13-01"));
        Assert.Null(ProjectCalendarUrlCodec.ParseDate(string.Empty));
        Assert.Null(ProjectCalendarUrlCodec.ParseView("agenda"));
    }

    [Fact]
    public void Every_codec_key_has_a_query_trigger_on_the_page() {
        Assert.Empty(UrlStateTriggers.Missing(typeof(ProjectCalendarPage), Codec.Keys));
    }
}
