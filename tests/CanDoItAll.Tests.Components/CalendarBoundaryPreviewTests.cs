using Bunit;
using CanDoItAll.ComponentKit.Canvas;
using CanDoItAll.ComponentKit.Components;

namespace CanDoItAll.Tests.Components;

public sealed class CalendarBoundaryPreviewTests
{
    [Fact]
    public void Selection_panel_factory_projects_selected_event_context()
    {
        var snapshot = CalendarSelectionPanelFactory.Create(CreateSurface());

        Assert.Equal("Weekly review", snapshot.EventTitle);
        Assert.Single(snapshot.LinkedPlaylists);
        Assert.Equal(2, snapshot.ChecklistRows.Count);
        Assert.Contains("1 linked playlist(s)", snapshot.Metrics);
    }

    [Fact]
    public void Event_editor_factory_flags_invalid_end_time_ranges()
    {
        var surface = CreateSurface();
        surface.Events[0].EndUtc = surface.Events[0].StartUtc!.Value.AddMinutes(-30);

        var snapshot = CalendarEventEditorModalFactory.Create(surface);

        Assert.Equal("Needs input", snapshot.StatePill);
        Assert.Contains(snapshot.ValidationMessages, message => message.Contains("later than the start time", StringComparison.Ordinal));
    }

    [Fact]
    public void Crud_bridge_factory_counts_enabled_operations()
    {
        var snapshot = CalendarCrudBridgeFactory.Create(CreateSurface());

        Assert.Equal(5, snapshot.Operations.Count);
        Assert.All(snapshot.Operations, operation => Assert.True(operation.IsEnabled));
        Assert.Contains("5/5 operations enabled", snapshot.Metrics);
    }

    [Fact]
    public void Mini_month_factory_marks_selected_and_eventful_days()
    {
        var snapshot = CalendarMiniMonthNavigatorFactory.Create(CreateSurface());

        Assert.Equal(42, snapshot.Days.Count);
        Assert.Contains(snapshot.Days, day => day.IsSelected);
        Assert.Contains(snapshot.Days, day => day.HasEvents);
        Assert.Contains("1 eventful day(s)", snapshot.Metrics);
    }

    [Fact]
    public void Export_menu_factory_surfaces_disabled_state()
    {
        var surface = CreateSurface();
        surface.EnableListExport = false;

        var snapshot = CalendarExportMenuFactory.Create(surface);

        Assert.Equal("Disabled", snapshot.StatePill);
        Assert.Single(snapshot.Formats);
        Assert.Contains("Export disabled", snapshot.Formats[0].Label);
    }

    [Fact]
    public void Time_grid_factory_projects_blocks_for_anchor_day()
    {
        var snapshot = CalendarTimeGridRendererFactory.Create(CreateSurface());

        Assert.Equal(2, snapshot.Blocks.Count);
        Assert.Contains("2 visible block(s)", snapshot.Metrics);
        Assert.Contains("2026", snapshot.RangeLabel);
    }

    [Fact]
    public void Selection_panel_component_renders_playlist_and_checklist_sections()
    {
        using var context = new TestContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = context.RenderComponent<CalendarSelectionPanel>(
            parameters => parameters.Add(component => component.Snapshot, CalendarSelectionPanelFactory.Create(CreateSurface())));

        Assert.Contains("Linked playlists", cut.Markup);
        Assert.Contains("Checklist", cut.Markup);
        Assert.Contains("Weekly review", cut.Markup);
    }

    [Fact]
    public void Time_grid_component_renders_block_markup()
    {
        using var context = new TestContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = context.RenderComponent<CalendarTimeGridRenderer>(
            parameters => parameters.Add(component => component.Snapshot, CalendarTimeGridRendererFactory.Create(CreateSurface())));

        Assert.Contains("08:00", cut.Markup);
        Assert.Contains("Weekly review", cut.Markup);
        Assert.Contains("Launch rehearsal", cut.Markup);
    }

    private static CanvasCalendarSurface CreateSurface()
    {
        var start = new DateTimeOffset(2026, 3, 24, 10, 0, 0, TimeSpan.Zero);
        var secondStart = start.AddHours(4);

        return new CanvasCalendarSurface
        {
            SurfaceId = "calendar-preview",
            InitialView = "week",
            SelectedDate = "2026-03-24",
            SelectedEventId = "event-1",
            Timezone = "UTC",
            SlotMinutes = 30,
            BusinessHoursStart = 8,
            BusinessHoursEnd = 18,
            MiniMonthCount = 1,
            AllowCreate = true,
            AllowEdit = true,
            AllowDelete = true,
            AllowDragDrop = true,
            AllowResize = true,
            EnableListExport = true,
            EventTypes = ["Review", "Launch"],
            EventStatuses = ["Scheduled", "Blocked"],
            Events =
            [
                new CanvasCalendarEvent
                {
                    Id = "event-1",
                    EventId = "event-1",
                    Title = "Weekly review",
                    StartUtc = start,
                    EndUtc = start.AddHours(1),
                    Status = "Scheduled",
                    EventType = "Review",
                    LocationLabel = "Room A",
                    Notes = "Verify prompts and project artifacts.",
                    LinkedPlaylists =
                    [
                        new CanvasCalendarPlaylist
                        {
                            PlaylistId = "playlist-1",
                            Title = "Review playlist",
                            Subtitle = "Validation handoff",
                            Purpose = "Support the review",
                            Status = "Ready"
                        }
                    ],
                    ChecklistRows =
                    [
                        new CanvasCalendarChecklistRow { Label = "Confirm agenda", Status = "Done", Note = "Agenda approved." },
                        new CanvasCalendarChecklistRow { Label = "Attach evidence", Status = "Pending", Note = "Need the latest screenshots." }
                    ]
                },
                new CanvasCalendarEvent
                {
                    Id = "event-2",
                    EventId = "event-2",
                    Title = "Launch rehearsal",
                    StartUtc = secondStart,
                    EndUtc = secondStart.AddHours(2),
                    Status = "Blocked",
                    EventType = "Launch",
                    LocationLabel = "Room B",
                    Notes = "Follow-up rehearsal."
                }
            ]
        };
    }
}
