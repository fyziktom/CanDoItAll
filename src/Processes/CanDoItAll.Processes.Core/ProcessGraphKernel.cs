using CanDoItAll.Processes.Abstractions;

namespace CanDoItAll.Processes.Core;

public sealed record ProcessDefinitionKernel(
    ProcessDefinitionId DefinitionId,
    ProcessDefinitionVersionId VersionId,
    IReadOnlyList<ProcessGraphNode> Steps,
    IReadOnlyList<ProcessGraphEdge> Edges,
    IReadOnlyList<ProcessArtifactDefinition> Artifacts,
    IReadOnlyList<ProcessArtifactSlotDefinition> ArtifactSlots,
    IReadOnlyList<ProcessBranchDefinition> Branches);

public sealed record ProcessGraphNode(
    ProcessStepDefinitionId Id,
    string Key,
    ProcessStepKind Kind,
    StrategyId? StrategyId = null);

public sealed record ProcessGraphEdge(
    ProcessStepDefinitionId SourceId,
    ProcessStepDefinitionId TargetId,
    bool IsBackwardRoute = false,
    LoopBudgetDefinition? LoopBudget = null);

public enum ProcessStepKind
{
    Start,
    Activity,
    Branch,
    Join,
    End
}

public static class ProcessGraphKernel
{
    public static bool HasDuplicateKeys(IEnumerable<ProcessGraphNode> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in nodes)
        {
            if (string.IsNullOrWhiteSpace(node.Key))
            {
                continue;
            }

            if (!seen.Add(node.Key))
            {
                return true;
            }
        }

        return false;
    }

    public static ProcessValidationResult Validate(ProcessDefinitionKernel definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var failures = new List<ProcessValidationFailure>();
        ValidateSteps(definition.Steps, failures);
        ValidateEdges(definition.Steps, definition.Edges, failures);
        failures.AddRange(ProcessArtifactRules.Validate(definition.Artifacts, definition.ArtifactSlots).Failures);
        failures.AddRange(ProcessBranchRules.Validate(definition.Branches).Failures);

        return ProcessValidationResult.From(failures);
    }

    private static void ValidateSteps(
        IReadOnlyList<ProcessGraphNode> steps,
        ICollection<ProcessValidationFailure> failures)
    {
        if (steps.Count == 0)
        {
            failures.Add(new ProcessValidationFailure(
                "Definition.HasNoSteps",
                "A process definition must contain at least one step."));
            return;
        }

        var ids = new HashSet<ProcessStepDefinitionId>();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var step in steps)
        {
            if (!ids.Add(step.Id))
            {
                failures.Add(new ProcessValidationFailure(
                    "Definition.DuplicateStepId",
                    $"Step id '{step.Id}' appears more than once."));
            }

            if (string.IsNullOrWhiteSpace(step.Key))
            {
                failures.Add(new ProcessValidationFailure(
                    "Definition.EmptyStepKey",
                    $"Step '{step.Id}' must have a key."));
            }
            else if (!keys.Add(step.Key))
            {
                failures.Add(new ProcessValidationFailure(
                    "Definition.DuplicateStepKey",
                    $"Step key '{step.Key}' appears more than once."));
            }
        }
    }

    private static void ValidateEdges(
        IReadOnlyList<ProcessGraphNode> steps,
        IReadOnlyList<ProcessGraphEdge> edges,
        ICollection<ProcessValidationFailure> failures)
    {
        var stepIds = steps.Select(step => step.Id).ToHashSet();
        foreach (var edge in edges)
        {
            if (!stepIds.Contains(edge.SourceId))
            {
                failures.Add(new ProcessValidationFailure(
                    "Definition.EdgeSourceMissing",
                    $"Edge source '{edge.SourceId}' does not exist."));
            }

            if (!stepIds.Contains(edge.TargetId))
            {
                failures.Add(new ProcessValidationFailure(
                    "Definition.EdgeTargetMissing",
                    $"Edge target '{edge.TargetId}' does not exist."));
            }

            if (edge.IsBackwardRoute && edge.LoopBudget is null)
            {
                failures.Add(new ProcessValidationFailure(
                    "Definition.BackwardEdgeMissingBudget",
                    $"Backward edge from '{edge.SourceId}' to '{edge.TargetId}' must define a loop budget."));
            }
        }

        if (HasForwardCycle(steps, edges))
        {
            failures.Add(new ProcessValidationFailure(
                "Definition.ForwardCycle",
                "Forward graph edges must be acyclic; repeating paths must be marked as backward routes."));
        }
    }

    private static bool HasForwardCycle(
        IReadOnlyList<ProcessGraphNode> steps,
        IReadOnlyList<ProcessGraphEdge> edges)
    {
        var adjacency = steps.ToDictionary(
            step => step.Id,
            _ => new List<ProcessStepDefinitionId>());

        foreach (var edge in edges)
        {
            if (!edge.IsBackwardRoute &&
                adjacency.TryGetValue(edge.SourceId, out var targets) &&
                adjacency.ContainsKey(edge.TargetId))
            {
                targets.Add(edge.TargetId);
            }
        }

        var visiting = new HashSet<ProcessStepDefinitionId>();
        var visited = new HashSet<ProcessStepDefinitionId>();
        foreach (var step in steps)
        {
            if (Visit(step.Id, adjacency, visiting, visited))
            {
                return true;
            }
        }

        return false;
    }

    private static bool Visit(
        ProcessStepDefinitionId stepId,
        IReadOnlyDictionary<ProcessStepDefinitionId, List<ProcessStepDefinitionId>> adjacency,
        ISet<ProcessStepDefinitionId> visiting,
        ISet<ProcessStepDefinitionId> visited)
    {
        if (visited.Contains(stepId))
        {
            return false;
        }

        if (!visiting.Add(stepId))
        {
            return true;
        }

        foreach (var target in adjacency[stepId])
        {
            if (Visit(target, adjacency, visiting, visited))
            {
                return true;
            }
        }

        visiting.Remove(stepId);
        visited.Add(stepId);
        return false;
    }
}
