using CanDoItAll.Workbench.Planning.UI;

namespace CanDoItAll.Modules.Workbench;

public static class ProjectCalendarPresentationAdapter {
    public static CalendarPresentation Build(ProjectCalendarSurface surface, ProjectCalendarViewState state, Guid origin) {
        TimeZoneInfo.FindSystemTimeZoneById(state.Timezone);
        var canvas = ProjectCalendarAdapter.BuildSurface(surface, state);
        canvas.SurfaceId = $"project-calendar:{surface.ProjectId:N}:{origin:N}";
        var selected = surface.Events.FirstOrDefault(item => item.Id == state.SelectedEventId);
        return new(origin) {
            State = PlanningReadState.Ready,
            ProjectName = surface.ProjectName,
            Calendar = canvas,
            LinkedArtifactCount = surface.Events.Count(item => !string.IsNullOrWhiteSpace(item.Route)),
            StatusCount = surface.Events.Select(item => item.Status).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            SelectedEvent = selected is null ? null : new(
                selected.Id, selected.Title, selected.ObjectType.ToString(), selected.Status,
                CalendarTimeDisplay.Format(selected.StartUtc, state.Timezone), CalendarTimeDisplay.Format(selected.EndUtc, state.Timezone), state.Timezone, selected.Route)
        };
    }

}
