using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Workbench.Planning.UI;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.WorkbenchPlanning;

public sealed class CalendarSurfaceTests {
    [Fact]
    public void Visible_calendar_export_is_connected_to_the_renderer() {
        using var context = CreateContext();
        var cut = context.Render<PlanningCalendarSurface>(parameters => parameters.Add(component => component.Presentation, CreatePresentation()));
        Assert.NotNull(cut.FindComponent<CanvasCalendar>().Instance.ExportAsync);
    }

    [Theory]
    [InlineData(PlanningReadState.Loading, "Preparing the calendar")]
    [InlineData(PlanningReadState.Ready, "Choose an event")]
    [InlineData(PlanningReadState.Failed, "Retry calendar")]
    [InlineData(PlanningReadState.Unavailable, "Open projects")]
    [InlineData(PlanningReadState.Stale, "last accepted schedule")]
    public void Availability_renders_with_only_neutral_component_services(PlanningReadState state, string expected) {
        using var context = CreateContext();
        var presentation = CreatePresentation() with { State = state, Error = state == PlanningReadState.Failed ? "Read failed" : null };
        var cut = context.Render<PlanningCalendarSurface>(parameters => parameters.Add(component => component.Presentation, presentation));
        Assert.Contains(expected, cut.Markup);
        Assert.DoesNotContain(typeof(PlanningCalendarSurface).Assembly.GetReferencedAssemblies(), name =>
            name.Name?.StartsWith("CanDoItAll.Modules.", StringComparison.Ordinal) == true ||
            name.Name?.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task Queued_shared_control_callbacks_keep_the_displayed_origin_and_original_receiver() {
        using var context = CreateContext();
        var original = CreatePresentation();
        var first = new List<CalendarStateIntent>();
        var second = new List<CalendarStateIntent>();
        var cut = context.Render<PlanningCalendarSurface>(parameters => parameters
            .Add(component => component.Presentation, original)
            .Add(component => component.StateChanged, (CalendarStateIntent intent) => first.Add(intent)));
        var callback = cut.FindComponent<CanvasCalendar>().Instance.StateChanged;
        var successor = CreatePresentation();
        cut.Render(parameters => parameters
            .Add(component => component.Presentation, successor)
            .Add(component => component.StateChanged, (CalendarStateIntent intent) => second.Add(intent)));
        var state = new CanvasCalendarStateChangedEventArgs("{}", null, "2026-10-05", "month", "month", "UTC");
        await cut.InvokeAsync(() => callback.InvokeAsync(state));

        Assert.Equal(new CalendarStateIntent(original.Origin, state), Assert.Single(first));
        Assert.Empty(second);
        Assert.Equal(successor.Calendar, cut.FindComponent<CanvasCalendar>().Instance.Surface);
    }

    [Fact]
    public async Task Two_calendars_emit_independent_selection_intents() {
        using var context = CreateContext();
        var firstIntents = new List<CalendarSelectionIntent>();
        var secondIntents = new List<CalendarSelectionIntent>();
        var first = CreatePresentation();
        var second = CreatePresentation();
        var a = context.Render<PlanningCalendarSurface>(parameters => parameters
            .Add(component => component.Presentation, first)
            .Add(component => component.SelectionChanged, (CalendarSelectionIntent intent) => firstIntents.Add(intent)));
        var b = context.Render<PlanningCalendarSurface>(parameters => parameters
            .Add(component => component.Presentation, second)
            .Add(component => component.SelectionChanged, (CalendarSelectionIntent intent) => secondIntents.Add(intent)));
        var id = Guid.NewGuid();
        await a.InvokeAsync(() => a.FindComponent<CanvasCalendar>().Instance.SelectionChanged.InvokeAsync(
            new(new CanvasCalendarEvent { EventId = id.ToString("D") }, new())));
        Assert.Equal(new CalendarSelectionIntent(first.Origin, id), Assert.Single(firstIntents));
        Assert.Empty(secondIntents);
        Assert.Equal(second.Calendar, b.FindComponent<CanvasCalendar>().Instance.Surface);
    }

    [Theory]
    [InlineData("day")]
    [InlineData("week")]
    [InlineData("month")]
    [InlineData("list")]
    [InlineData("year")]
    public void Normalized_state_round_trips_without_losing_additional_calendar_state(string view) {
        var state = new ProjectCalendarViewState(view, "project", "2026-11-01", "America/New_York", Guid.NewGuid());
        var json = ProjectCalendarStateParser.Serialize(state, "{\"customFilter\":{\"status\":\"Planned\"},\"preferredView\":\"day\"}");
        Assert.Equal(state, ProjectCalendarStateParser.Parse(json));
        Assert.Contains("\"customFilter\":{\"status\":\"Planned\"}", json);
        Assert.Contains($"\"preferredView\":\"{view}\"", json);
    }

    private static CalendarPresentation CreatePresentation() {
        var origin = Guid.NewGuid();
        return new(origin) {
            State = PlanningReadState.Ready,
            ProjectName = "Independent planning",
            Calendar = new() { SurfaceId = origin.ToString("N"), AllowCreate = false, AllowEdit = false, AllowDelete = false }
        };
    }

    [Theory]
    [InlineData("2026-03-08T06:30:00Z", "America/New_York", "2026-03-08 01:30 -05:00")]
    [InlineData("2026-03-08T07:30:00Z", "America/New_York", "2026-03-08 03:30 -04:00")]
    [InlineData("2026-11-01T05:30:00Z", "America/New_York", "2026-11-01 01:30 -04:00")]
    [InlineData("2026-11-01T06:30:00Z", "America/New_York", "2026-11-01 01:30 -05:00")]
    [InlineData("2026-11-01T06:30:00Z", "Asia/Kathmandu", "2026-11-01 12:15 +05:45")]
    public void Explicit_calendar_timezone_handles_dst_and_fractional_offsets(string utc, string zone, string expected) {
        Assert.Equal(expected, CalendarTimeDisplay.Format(DateTimeOffset.Parse(utc, System.Globalization.CultureInfo.InvariantCulture), zone));
    }

    private static BunitContext CreateContext() {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        return context;
    }
}
