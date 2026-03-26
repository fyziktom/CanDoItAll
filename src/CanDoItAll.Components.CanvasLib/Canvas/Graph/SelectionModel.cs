namespace CanDoItAll.Components.CanvasLib;

public sealed class SelectionModel
{
    private readonly IReadOnlyList<string> selectedNodeIds;

    private SelectionModel(IReadOnlyList<string> selectedNodeIds)
    {
        this.selectedNodeIds = selectedNodeIds;
    }

    public static SelectionModel Empty { get; } = new([]);

    public IReadOnlyList<string> SelectedNodeIds => selectedNodeIds;

    public string? PrimaryNodeId => selectedNodeIds.FirstOrDefault();

    public bool IsEmpty => selectedNodeIds.Count == 0;

    public static SelectionModel From(IEnumerable<string>? selectedNodeIds, string? primaryNodeId = null)
    {
        var normalized = Normalize(selectedNodeIds, primaryNodeId);
        return normalized.Count == 0
            ? Empty
            : new SelectionModel(normalized);
    }

    public SelectionModel Replace(IEnumerable<string>? selectedNodeIds, string? primaryNodeId = null)
        => From(selectedNodeIds, primaryNodeId);

    public SelectionModel Add(string? nodeId, bool makePrimary = false)
    {
        var normalizedNodeId = NormalizeId(nodeId);
        if (normalizedNodeId is null)
        {
            return this;
        }

        var next = selectedNodeIds.ToList();
        next.RemoveAll(existing => string.Equals(existing, normalizedNodeId, StringComparison.Ordinal));
        next.Add(normalizedNodeId);
        return From(next, makePrimary ? normalizedNodeId : PrimaryNodeId);
    }

    public SelectionModel Toggle(string? nodeId)
    {
        var normalizedNodeId = NormalizeId(nodeId);
        if (normalizedNodeId is null)
        {
            return this;
        }

        if (!selectedNodeIds.Contains(normalizedNodeId, StringComparer.Ordinal))
        {
            return Add(normalizedNodeId, makePrimary: IsEmpty);
        }

        var next = selectedNodeIds
            .Where(existing => !string.Equals(existing, normalizedNodeId, StringComparison.Ordinal))
            .ToList();

        var nextPrimary = string.Equals(PrimaryNodeId, normalizedNodeId, StringComparison.Ordinal)
            ? next.FirstOrDefault()
            : PrimaryNodeId;

        return From(next, nextPrimary);
    }

    public SelectionModel Remove(string? nodeId)
    {
        var normalizedNodeId = NormalizeId(nodeId);
        if (normalizedNodeId is null)
        {
            return this;
        }

        return From(
            selectedNodeIds.Where(existing => !string.Equals(existing, normalizedNodeId, StringComparison.Ordinal)),
            string.Equals(PrimaryNodeId, normalizedNodeId, StringComparison.Ordinal) ? null : PrimaryNodeId);
    }

    public SelectionModel RemoveMissing(IEnumerable<string>? validNodeIds)
    {
        var validIds = new HashSet<string>(
            (validNodeIds ?? [])
                .Select(NormalizeId)
                .OfType<string>(),
            StringComparer.Ordinal);

        if (validIds.Count == 0)
        {
            return Empty;
        }

        var next = selectedNodeIds.Where(validIds.Contains).ToList();
        var nextPrimary = PrimaryNodeId is not null && validIds.Contains(PrimaryNodeId)
            ? PrimaryNodeId
            : null;

        return From(next, nextPrimary);
    }

    public SelectionModel Clear() => Empty;

    public List<string> ToList() => [.. selectedNodeIds];

    private static IReadOnlyList<string> Normalize(IEnumerable<string>? selectedNodeIds, string? primaryNodeId)
    {
        var ordered = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var candidate in selectedNodeIds ?? [])
        {
            var normalized = NormalizeId(candidate);
            if (normalized is null || !seen.Add(normalized))
            {
                continue;
            }

            ordered.Add(normalized);
        }

        var normalizedPrimary = NormalizeId(primaryNodeId);
        if (normalizedPrimary is not null)
        {
            ordered.RemoveAll(existing => string.Equals(existing, normalizedPrimary, StringComparison.Ordinal));
            ordered.Insert(0, normalizedPrimary);
        }

        return ordered;
    }

    private static string? NormalizeId(string? nodeId)
    {
        var normalized = nodeId?.Trim();
        return string.IsNullOrWhiteSpace(normalized)
            ? null
            : normalized;
    }
}


