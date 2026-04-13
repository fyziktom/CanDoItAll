namespace CanDoItAll.Modules.Processes;

public static class ProcessCanvasBranching
{
    public const string DefinitionStepNodePrefix = ProcessCanvasCatalog.NodePrefixes.DefinitionStep;
    public const string DefinitionBranchNodePrefix = ProcessCanvasCatalog.NodePrefixes.DefinitionBranchRouter;
    public const string DefinitionRoleNodePrefix = ProcessCanvasCatalog.NodePrefixes.DefinitionRole;
    public const string RuntimeStepNodePrefix = ProcessCanvasCatalog.NodePrefixes.RuntimeStep;
    public const string RuntimeBranchNodePrefix = ProcessCanvasCatalog.NodePrefixes.RuntimeBranchRouter;

    public const string DefaultRouteKey = "__default__";
    public const string ErrorRouteKey = "__error__";
    public const string DefaultRouteTitle = "Default";
    public const string ErrorRouteTitle = "Error";

    public const string StepInputPortId = ProcessCanvasCatalog.DefinitionPorts.BranchStepInput;
    public const string DecisionRoleInputPortId = ProcessCanvasCatalog.DefinitionPorts.BranchDecisionRoleInput;
    public const string RoleDecisionOutputPortId = ProcessCanvasCatalog.DefinitionPorts.RoleDecisionAuthorityOutput;

    public static void NormalizeDefinitionEditor(ProcessDefinitionEditorModel editor)
    {
        ArgumentNullException.ThrowIfNull(editor);

        foreach (var step in editor.Steps)
        {
            NormalizeStepDependencies(step, editor.Steps);
            NormalizeStepBranchOutcomes(step, editor.Steps);
        }

        foreach (var step in editor.Steps)
        {
            NormalizeStepDependencies(step, editor.Steps);
        }
    }

    public static void NormalizeStepDraft(ProcessStepEditorModel step)
    {
        ArgumentNullException.ThrowIfNull(step);

        ProcessDependencyCompatibilityBridge.GetCanonicalEditorDependencies(step);
        NormalizeStepBranchOutcomes(step, [step]);
    }

    public static bool ShouldRenderBranchRouter(ProcessStepEditorModel step)
    {
        ArgumentNullException.ThrowIfNull(step);

        return step.DecisionRoleRequirementId.HasValue || step.BranchOutcomes.Count > 0;
    }

    public static bool ShouldRenderBranchRouter(ProcessStepRunViewModel stepRun)
    {
        ArgumentNullException.ThrowIfNull(stepRun);

        return stepRun.AvailableBranchOutcomes.Count > 0;
    }

    public static IReadOnlyList<ProcessStepBranchOutcomeEditorModel> GetCustomBranchOutcomes(ProcessStepEditorModel step)
    {
        ArgumentNullException.ThrowIfNull(step);

        return step.BranchOutcomes
            .Where(outcome => !IsSystemOutcome(outcome))
            .ToList();
    }

    public static IReadOnlyList<ProcessStepBranchOutcomeEditorModel> GetOrderedBranchOutcomes(ProcessStepEditorModel step)
    {
        ArgumentNullException.ThrowIfNull(step);

        return [.. step.BranchOutcomes];
    }

    public static IReadOnlyList<ProcessStepDependencyEditorModel> GetOrderedDependencies(ProcessStepEditorModel step)
    {
        ArgumentNullException.ThrowIfNull(step);

        return [.. ProcessDependencyCompatibilityBridge.GetCanonicalEditorDependencies(step)];
    }

    public static bool IsSystemOutcome(ProcessStepBranchOutcomeEditorModel outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);

        return string.Equals(outcome.Key, DefaultRouteKey, StringComparison.Ordinal) ||
               string.Equals(outcome.Key, ErrorRouteKey, StringComparison.Ordinal);
    }

    public static bool IsSystemOutcome(ProcessStepBranchOutcomeDefinition outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);

        return string.Equals(outcome.Key, DefaultRouteKey, StringComparison.Ordinal) ||
               string.Equals(outcome.Key, ErrorRouteKey, StringComparison.Ordinal);
    }

    public static bool IsDefaultOutcome(ProcessStepBranchOutcomeEditorModel outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);

        return string.Equals(outcome.Key, DefaultRouteKey, StringComparison.Ordinal);
    }

    public static bool IsErrorOutcome(ProcessStepBranchOutcomeEditorModel outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);

        return string.Equals(outcome.Key, ErrorRouteKey, StringComparison.Ordinal);
    }

    public static Guid? GetDefaultOutcomeId(ProcessStepEditorModel step)
    {
        ArgumentNullException.ThrowIfNull(step);

        return step.BranchOutcomes.FirstOrDefault(IsDefaultOutcome)?.Id;
    }

    public static Guid? GetErrorOutcomeId(ProcessStepEditorModel step)
    {
        ArgumentNullException.ThrowIfNull(step);

        return step.BranchOutcomes.FirstOrDefault(IsErrorOutcome)?.Id;
    }

    public static string BuildDefinitionStepNodeId(ProcessStepEditorModel step)
    {
        ArgumentNullException.ThrowIfNull(step);

        return $"{DefinitionStepNodePrefix}{BuildNodeToken(step.Id, step.Key, step.Title, "step")}";
    }

    public static string BuildDefinitionBranchNodeId(ProcessStepEditorModel step)
    {
        ArgumentNullException.ThrowIfNull(step);

        return $"{DefinitionBranchNodePrefix}{BuildNodeToken(step.Id, step.Key, step.Title, "step")}";
    }

    public static string BuildDefinitionRoleNodeId(ProcessRoleEditorModel role)
    {
        ArgumentNullException.ThrowIfNull(role);

        return $"{DefinitionRoleNodePrefix}{BuildNodeToken(role.Id, role.Key, role.DisplayName, "role")}";
    }

    public static string BuildRuntimeStepNodeId(Guid stepRunId)
    {
        return $"{RuntimeStepNodePrefix}{stepRunId:D}";
    }

    public static string BuildRuntimeBranchNodeId(Guid stepRunId)
    {
        return $"{RuntimeBranchNodePrefix}{stepRunId:D}";
    }

    public static string BuildOutcomePortId(ProcessStepBranchOutcomeEditorModel outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);

        return ProcessCanvasCatalog.DefinitionPorts.BuildBranchOutcomeOutputPortId(outcome);
    }

    public static string BuildOutcomePortId(ProcessStepBranchOutcomeOptionViewModel outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);

        return ProcessCanvasCatalog.RuntimePorts.BuildBranchOutcomeOutputPortId(outcome);
    }

    public static string ResolveOutcomePortId(ProcessStepEditorModel sourceStep, Guid? branchOutcomeId)
    {
        ArgumentNullException.ThrowIfNull(sourceStep);

        var routedOutcome = sourceStep.BranchOutcomes.FirstOrDefault(outcome => outcome.Id == branchOutcomeId);
        if (routedOutcome is not null)
        {
            return BuildOutcomePortId(routedOutcome);
        }

        var defaultOutcome = sourceStep.BranchOutcomes.FirstOrDefault(IsDefaultOutcome);
        return defaultOutcome is null
            ? string.Empty
            : BuildOutcomePortId(defaultOutcome);
    }

    public static string ResolveOutcomePortId(ProcessStepRunViewModel sourceStepRun, Guid? branchOutcomeId)
    {
        ArgumentNullException.ThrowIfNull(sourceStepRun);

        var routedOutcome = sourceStepRun.AvailableBranchOutcomes.FirstOrDefault(outcome => outcome.Id == branchOutcomeId);
        if (routedOutcome is not null)
        {
            return BuildOutcomePortId(routedOutcome);
        }

        var defaultOutcome = sourceStepRun.AvailableBranchOutcomes.FirstOrDefault(outcome =>
            string.Equals(outcome.Title, DefaultRouteTitle, StringComparison.OrdinalIgnoreCase));
        return defaultOutcome is null
            ? string.Empty
            : BuildOutcomePortId(defaultOutcome);
    }

    public static bool TryResolveDefinitionStepToken(string? nodeId, out string token)
    {
        token = string.Empty;
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            return false;
        }

        if (nodeId.StartsWith(DefinitionStepNodePrefix, StringComparison.Ordinal))
        {
            token = nodeId[DefinitionStepNodePrefix.Length..];
            return true;
        }

        if (nodeId.StartsWith(DefinitionBranchNodePrefix, StringComparison.Ordinal))
        {
            token = nodeId[DefinitionBranchNodePrefix.Length..];
            return true;
        }

        return false;
    }

    public static bool TryResolveDefinitionRoleToken(string? nodeId, out string token)
    {
        token = string.Empty;
        if (string.IsNullOrWhiteSpace(nodeId) ||
            !nodeId.StartsWith(DefinitionRoleNodePrefix, StringComparison.Ordinal))
        {
            return false;
        }

        token = nodeId[DefinitionRoleNodePrefix.Length..];
        return true;
    }

    public static bool TryResolveRuntimeStepId(string? nodeId, out Guid stepRunId)
    {
        stepRunId = Guid.Empty;
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            return false;
        }

        var token = nodeId.StartsWith(RuntimeStepNodePrefix, StringComparison.Ordinal)
            ? nodeId[RuntimeStepNodePrefix.Length..]
            : nodeId.StartsWith(RuntimeBranchNodePrefix, StringComparison.Ordinal)
                ? nodeId[RuntimeBranchNodePrefix.Length..]
                : string.Empty;
        return Guid.TryParse(token, out stepRunId);
    }

    private static void NormalizeStepBranchOutcomes(
        ProcessStepEditorModel step,
        IReadOnlyList<ProcessStepEditorModel> allSteps)
    {
        ProcessDependencyCompatibilityBridge.GetCanonicalEditorDependencies(step);

        var systemOutcomes = step.BranchOutcomes
            .Where(IsSystemOutcome)
            .ToList();
        var customOutcomes = step.BranchOutcomes
            .Where(outcome => !IsSystemOutcome(outcome))
            .ToList();
        var systemOutcomeIds = systemOutcomes
            .Where(outcome => outcome.Id.HasValue)
            .Select(outcome => outcome.Id!.Value)
            .ToHashSet();
        var hasSystemDependents = step.Id.HasValue && allSteps.Any(candidate =>
            GetOrderedDependencies(candidate).Any(dependency =>
                dependency.DependsOnStepId == step.Id.Value &&
                dependency.DependsOnBranchOutcomeId.HasValue &&
                systemOutcomeIds.Contains(dependency.DependsOnBranchOutcomeId.Value)));
        var shouldKeepSystemOutcomes = step.StepKind == ProcessStepKind.Decision ||
            step.DecisionRoleRequirementId.HasValue ||
            customOutcomes.Count > 0 ||
            hasSystemDependents;

        if (!shouldKeepSystemOutcomes)
        {
            step.BranchOutcomes = customOutcomes;
            return;
        }

        var normalized = new List<ProcessStepBranchOutcomeEditorModel>(customOutcomes.Count + 2);
        normalized.AddRange(customOutcomes);
        normalized.Add(ResolveSystemOutcome(systemOutcomes, DefaultRouteKey, DefaultRouteTitle, "Continue when no explicit branch outcome is selected."));
        normalized.Add(ResolveSystemOutcome(systemOutcomes, ErrorRouteKey, ErrorRouteTitle, "Handle exceptions, failed validations, or explicit error escalation."));
        step.BranchOutcomes = normalized;
    }

    private static void NormalizeStepDependencies(
        ProcessStepEditorModel step,
        IReadOnlyList<ProcessStepEditorModel> allSteps)
    {
        var dependencies = ProcessDependencyCompatibilityBridge.GetCanonicalEditorDependencies(step);
        var normalized = new List<ProcessStepDependencyEditorModel>(dependencies.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var dependency in dependencies)
        {
            if (!dependency.DependsOnStepId.HasValue ||
                dependency.DependsOnStepId.Value == Guid.Empty ||
                (step.Id.HasValue && dependency.DependsOnStepId.Value == step.Id.Value))
            {
                continue;
            }

            var sourceStep = allSteps.FirstOrDefault(candidate => candidate.Id == dependency.DependsOnStepId.Value);
            if (sourceStep is null)
            {
                continue;
            }

            if (ShouldRenderBranchRouter(sourceStep))
            {
                dependency.DependsOnBranchOutcomeId ??= GetDefaultOutcomeId(sourceStep);
            }
            else if (dependency.DependsOnBranchOutcomeId.HasValue &&
                sourceStep.BranchOutcomes.All(outcome => outcome.Id != dependency.DependsOnBranchOutcomeId.Value))
            {
                dependency.DependsOnBranchOutcomeId = null;
            }

            var dedupeKey = $"{dependency.DependsOnStepId.Value:D}:{dependency.DependsOnBranchOutcomeId?.ToString("D") ?? string.Empty}";
            if (!seen.Add(dedupeKey))
            {
                continue;
            }

            dependency.Id ??= Guid.NewGuid();
            normalized.Add(new ProcessStepDependencyEditorModel
            {
                Id = dependency.Id,
                DependsOnStepId = dependency.DependsOnStepId,
                DependsOnBranchOutcomeId = dependency.DependsOnBranchOutcomeId
            });
        }

        ProcessDependencyCompatibilityBridge.SetCanonicalEditorDependencies(step, normalized);
    }

    private static ProcessStepBranchOutcomeEditorModel ResolveSystemOutcome(
        IReadOnlyList<ProcessStepBranchOutcomeEditorModel> existingOutcomes,
        string key,
        string title,
        string description)
    {
        var existing = existingOutcomes.FirstOrDefault(outcome => string.Equals(outcome.Key, key, StringComparison.Ordinal));
        if (existing is not null)
        {
            existing.Title = title;
            existing.Description = description;
            existing.Id ??= Guid.NewGuid();
            return existing;
        }

        return new ProcessStepBranchOutcomeEditorModel
        {
            Id = Guid.NewGuid(),
            Key = key,
            Title = title,
            Description = description
        };
    }
    private static string BuildNodeToken(Guid? id, string key, string title, string fallbackPrefix)
    {
        if (id.HasValue && id.Value != Guid.Empty)
        {
            return id.Value.ToString("D");
        }

        var source = string.IsNullOrWhiteSpace(key) ? title : key;
        if (string.IsNullOrWhiteSpace(source))
        {
            return $"{fallbackPrefix}-{Guid.NewGuid():N}";
        }

        return source
            .Trim()
            .ToLowerInvariant()
            .Replace(' ', '-');
    }
}
