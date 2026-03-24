using CanDoItAll.ComponentKit.Canvas;
using CanDoItAll.Modules.Workbench;

namespace CanDoItAll.Tests.Components;

public sealed class ProjectCalendarStateParserTests
{
    [Fact]
    public void Parse_restores_preferred_view_selected_date_and_event()
    {
        var eventId = Guid.NewGuid();

        var state = ProjectCalendarStateParser.Parse($$"""
            {
              "preferredView": "month",
              "scope": "project",
              "selectedDateKey": "2026-03-24",
              "timezone": "America/New_York",
              "selectedEventId": "{{eventId:D}}"
            }
            """);

        Assert.Equal("month", state.View);
        Assert.Equal("project", state.Scope);
        Assert.Equal("2026-03-24", state.SelectedDate);
        Assert.Equal("America/New_York", state.Timezone);
        Assert.Equal(eventId, state.SelectedEventId);
    }

    [Fact]
    public void From_state_changed_normalizes_unknown_views_to_week()
    {
        var args = new CanvasCalendarStateChangedEventArgs(
            "{}",
            null,
            "2026-03-25",
            "timeline",
            string.Empty,
            string.Empty);

        var state = ProjectCalendarStateParser.FromStateChanged(args);

        Assert.Equal("week", state.View);
        Assert.Equal("week", state.Scope);
        Assert.Equal("2026-03-25", state.SelectedDate);
        Assert.Equal("UTC", state.Timezone);
        Assert.Null(state.SelectedEventId);
    }
}
