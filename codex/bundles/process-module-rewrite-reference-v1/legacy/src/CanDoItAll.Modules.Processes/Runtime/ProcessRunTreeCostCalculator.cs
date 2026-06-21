namespace CanDoItAll.Modules.Processes;

internal static class ProcessRunTreeCostCalculator
{
    public static IReadOnlyDictionary<Guid, ProcessRunTreeCostRollup> BuildRollups(
        IReadOnlyCollection<ProcessRunTreeCostInput> runs)
    {
        ArgumentNullException.ThrowIfNull(runs);

        var runsById = runs
            .Where(run => run.RunId != Guid.Empty)
            .GroupBy(run => run.RunId)
            .ToDictionary(group => group.Key, group => group.First());
        var childRunIdsByParentRunId = runsById.Values
            .Where(run => run.ParentRunId.HasValue && runsById.ContainsKey(run.ParentRunId.Value))
            .GroupBy(run => run.ParentRunId!.Value)
            .ToDictionary(
                group => group.Key,
                group => group.Select(run => run.RunId).ToArray());
        var rollups = new Dictionary<Guid, ProcessRunTreeCostRollup>(runsById.Count);

        foreach (var runId in runsById.Keys)
        {
            ResolveRollup(runId, runsById, childRunIdsByParentRunId, rollups, []);
        }

        return rollups;
    }

    public static IReadOnlySet<Guid> ResolveCoveredRunIds(
        IReadOnlyCollection<Guid> selectedRunIds,
        IReadOnlyCollection<ProcessRunTreeCostInput> runs)
    {
        ArgumentNullException.ThrowIfNull(selectedRunIds);
        ArgumentNullException.ThrowIfNull(runs);

        var runsById = runs
            .Where(run => run.RunId != Guid.Empty)
            .GroupBy(run => run.RunId)
            .ToDictionary(group => group.Key, group => group.First());
        var childRunIdsByParentRunId = runsById.Values
            .Where(run => run.ParentRunId.HasValue && runsById.ContainsKey(run.ParentRunId.Value))
            .GroupBy(run => run.ParentRunId!.Value)
            .ToDictionary(
                group => group.Key,
                group => group.Select(run => run.RunId).ToArray());
        var coveredRunIds = new HashSet<Guid>();
        foreach (var runId in selectedRunIds.Where(runId => runsById.ContainsKey(runId)))
        {
            AddSubtreeRunIds(runId, childRunIdsByParentRunId, coveredRunIds);
        }

        return coveredRunIds;
    }

    public static ProcessRunTreeCostRollup BuildCoveredRollup(
        IReadOnlyCollection<Guid> selectedRunIds,
        IReadOnlyCollection<ProcessRunTreeCostInput> runs)
    {
        ArgumentNullException.ThrowIfNull(selectedRunIds);
        ArgumentNullException.ThrowIfNull(runs);

        var runsById = runs
            .Where(run => run.RunId != Guid.Empty)
            .GroupBy(run => run.RunId)
            .ToDictionary(group => group.Key, group => group.First());
        var coveredRunIds = ResolveCoveredRunIds(selectedRunIds, runsById.Values.ToArray());
        var selectedCount = selectedRunIds
            .Where(runId => coveredRunIds.Contains(runId))
            .Distinct()
            .Count();

        return new ProcessRunTreeCostRollup(
            coveredRunIds
                .Select(runId => runsById[runId].EstimatedCost)
                .Sum(),
            coveredRunIds
                .Select(runId => runsById[runId].ActualCost)
                .Sum(),
            Math.Max(0, coveredRunIds.Count - selectedCount));
    }

    private static ProcessRunTreeCostRollup ResolveRollup(
        Guid runId,
        IReadOnlyDictionary<Guid, ProcessRunTreeCostInput> runsById,
        IReadOnlyDictionary<Guid, Guid[]> childRunIdsByParentRunId,
        Dictionary<Guid, ProcessRunTreeCostRollup> rollups,
        HashSet<Guid> visitingRunIds)
    {
        if (rollups.TryGetValue(runId, out var existing))
        {
            return existing;
        }

        if (!runsById.TryGetValue(runId, out var run) || !visitingRunIds.Add(runId))
        {
            return ProcessRunTreeCostRollup.Empty;
        }

        var estimatedCost = run.EstimatedCost;
        var actualCost = run.ActualCost;
        var descendantRunCount = 0;
        if (childRunIdsByParentRunId.TryGetValue(runId, out var childRunIds))
        {
            foreach (var childRunId in childRunIds)
            {
                if (visitingRunIds.Contains(childRunId))
                {
                    continue;
                }

                var childRollup = ResolveRollup(
                    childRunId,
                    runsById,
                    childRunIdsByParentRunId,
                    rollups,
                    visitingRunIds);
                estimatedCost += childRollup.EstimatedCost;
                actualCost += childRollup.ActualCost;
                descendantRunCount += 1 + childRollup.DescendantRunCount;
            }
        }

        visitingRunIds.Remove(runId);
        var rollup = new ProcessRunTreeCostRollup(estimatedCost, actualCost, descendantRunCount);
        rollups[runId] = rollup;
        return rollup;
    }

    private static void AddSubtreeRunIds(
        Guid runId,
        IReadOnlyDictionary<Guid, Guid[]> childRunIdsByParentRunId,
        HashSet<Guid> coveredRunIds)
    {
        if (!coveredRunIds.Add(runId))
        {
            return;
        }

        if (!childRunIdsByParentRunId.TryGetValue(runId, out var childRunIds))
        {
            return;
        }

        foreach (var childRunId in childRunIds)
        {
            AddSubtreeRunIds(childRunId, childRunIdsByParentRunId, coveredRunIds);
        }
    }
}

internal sealed record ProcessRunTreeCostInput(
    Guid RunId,
    Guid? ParentRunId,
    decimal EstimatedCost,
    decimal ActualCost);

internal sealed record ProcessRunTreeCostRollup(
    decimal EstimatedCost,
    decimal ActualCost,
    int DescendantRunCount)
{
    public static ProcessRunTreeCostRollup Empty { get; } = new(0m, 0m, 0);
}
