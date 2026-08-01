using CanDoItAll.Components.Gantt;

namespace CanDoItAll.Modules.Workbench;

internal static class ProjectStructureGanttScheduleMutationFactory
{
    public static ProjectStructureGanttScheduleMutationRequest Create(
        GanttTaskScheduleChangeRequest scheduleChange,
        ProjectStructureSurface surface,
        IReadOnlyList<GanttTask> projectedTasks)
    {
        ArgumentNullException.ThrowIfNull(scheduleChange);
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(projectedTasks);

        var nodesById = surface.Nodes.ToDictionary(static node => node.Id, StringComparer.Ordinal);
        var snapshots = projectedTasks
            .Select(task =>
            {
                if (!nodesById.TryGetValue(task.Id.Value, out var node))
                {
                    throw new InvalidOperationException(
                        $"Task '{ProjectStructureGanttMutationConventions.Mask(task.Id.Value)}' is missing from the rendered project surface.");
                }

                return new ProjectStructureTaskScheduleSnapshot(
                    task.Id,
                    node.StartUtc,
                    node.EndUtc,
                    node.DurationSeconds,
                    task.Start,
                    task.End);
            })
            .ToArray();
        return new ProjectStructureGanttScheduleMutationRequest(scheduleChange, snapshots);
    }
}
