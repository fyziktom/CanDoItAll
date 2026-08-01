using System.Text.Json;

namespace CanDoItAll.Modules.Workbench.CanvasAdapters;

public enum ProjectStructureGanttRowPlacement
{
    Before,
    After
}

public sealed record ProjectStructureGanttRowMoveRequest(
    string TaskNodeId,
    string AnchorTaskNodeId,
    ProjectStructureGanttRowPlacement Placement);

public sealed class ProjectStructureGanttRowOrderConflictException(
    string taskNodeId,
    string anchorTaskNodeId,
    ProjectStructureGanttRowPlacement placement)
    : InvalidOperationException(
        $"Gantt row order changed before task '{taskNodeId}' could be placed {placement.ToString().ToLowerInvariant()} '{anchorTaskNodeId}'.")
{
    public string TaskNodeId { get; } = taskNodeId;

    public string AnchorTaskNodeId { get; } = anchorTaskNodeId;

    public ProjectStructureGanttRowPlacement Placement { get; } = placement;
}

public sealed class ProjectStructureGanttViewState
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public ProjectStructureGanttViewState(IReadOnlyList<string>? orderedTaskNodeIds = null)
    {
        OrderedTaskNodeIds = Normalize(orderedTaskNodeIds);
    }

    public IReadOnlyList<string> OrderedTaskNodeIds { get; }

    public string ToJson()
    {
        return JsonSerializer.Serialize(this, SerializerOptions);
    }

    public IReadOnlyList<string> ResolveOrderedTaskNodeIds(IReadOnlyList<string> availableTaskNodeIds)
    {
        ArgumentNullException.ThrowIfNull(availableTaskNodeIds);

        var available = Normalize(availableTaskNodeIds);
        if (available.Count == 0)
        {
            return [];
        }

        var remaining = available.ToHashSet(StringComparer.Ordinal);
        var resolved = new List<string>(available.Count);
        foreach (var nodeId in OrderedTaskNodeIds)
        {
            if (remaining.Remove(nodeId))
            {
                resolved.Add(nodeId);
            }
        }

        resolved.AddRange(available.Where(remaining.Contains));
        return Array.AsReadOnly(resolved.ToArray());
    }

    public static ProjectStructureGanttViewState Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new ProjectStructureGanttViewState();
        }

        try
        {
            return JsonSerializer.Deserialize<ProjectStructureGanttViewState>(json, SerializerOptions)
                ?? throw new InvalidOperationException("The persisted Gantt view state was empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException("The persisted Gantt view state is invalid.", exception);
        }
    }

    private static IReadOnlyList<string> Normalize(IReadOnlyList<string>? orderedTaskNodeIds)
    {
        if (orderedTaskNodeIds is null || orderedTaskNodeIds.Count == 0)
        {
            return [];
        }

        return Array.AsReadOnly(orderedTaskNodeIds
            .Where(static nodeId => !string.IsNullOrWhiteSpace(nodeId))
            .Select(static nodeId => nodeId.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray());
    }
}
