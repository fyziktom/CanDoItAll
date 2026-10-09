using CanDoItAll.Processes.Projections;

namespace CanDoItAll.Processes.Application;

internal static class ProcessAuthoringCanvasLayout {
    public static (IReadOnlyList<ProcessDefinitionCanvasEditorNodeProjection> Nodes, IReadOnlyList<ProcessDefinitionCanvasEdgeProjection> Edges) Restore(
        IReadOnlyList<ProcessDefinitionCanvasEditorNodeProjection> nodes, IReadOnlyList<ProcessDefinitionCanvasEdgeProjection> edges,
        IReadOnlyList<ProcessAuthoringReferencePlacement> placements) {
        var keys = new Dictionary<ProcessDefinitionCanvasNodeKey, ProcessDefinitionCanvasNodeKey>();
        var restored = nodes.Select(node => {
            var saved = placements.FirstOrDefault(item => !item.IsClone && item.Key == node.NodeKey.Value) ??
                placements.FirstOrDefault(item => !item.IsClone && item.Kind == node.Kind && item.SemanticKey == SemanticKey(node) && item.StepKey == node.StepKey?.Value);
            if (saved is null) {
                return node;
            }
            ProcessDefinitionCanvasNodeKey key = new(saved.Key);
            keys[node.NodeKey] = key;
            return node with { NodeKey = key, X = saved.X, Y = saved.Y };
        }).ToList();
        foreach (var saved in placements.Where(item => item.IsClone)) {
            var source = nodes.FirstOrDefault(node => node.Kind == saved.Kind && SemanticKey(node) == saved.SemanticKey &&
                (saved.StepKey is null || node.StepKey?.Value == saved.StepKey));
            if (source is not null) {
                restored.Add(source with { NodeKey = new(saved.Key), StepKey = null, X = saved.X, Y = saved.Y, Subtitle = "Reference", Badges = ["Reference"] });
            }
        }
        return (restored, edges.Select(edge => edge with {
            FromNodeKey = keys.GetValueOrDefault(edge.FromNodeKey, edge.FromNodeKey),
            ToNodeKey = keys.GetValueOrDefault(edge.ToNodeKey, edge.ToNodeKey)
        }).ToArray());
    }

    public static string SemanticKey(ProcessDefinitionCanvasEditorNodeProjection node)
        => node.RoleKey?.Value ?? node.ArtifactKey ?? node.StepKey?.Value ?? node.NodeKey.Value;
}
