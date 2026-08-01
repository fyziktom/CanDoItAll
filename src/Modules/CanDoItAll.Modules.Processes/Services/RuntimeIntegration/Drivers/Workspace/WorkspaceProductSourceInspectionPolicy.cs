using System.Text.Json;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Modules.Processes;

internal sealed record WorkspaceProductSourceInspectionPolicyIssue(
    string VariableName,
    string Reason);

internal sealed record WorkspaceProductSourceInspectionPolicyEvaluation(
    bool IsConfiguredForStep,
    bool IsInspectionRequired,
    IReadOnlyList<string> ExcludedPathFragments,
    WorkspaceProductSourceInspectionPolicyIssue? Issue)
{
    public static WorkspaceProductSourceInspectionPolicyEvaluation NotConfigured { get; } =
        new(false, false, [], null);
}

internal static class WorkspaceProductSourceInspectionPolicy
{
    internal static bool IsConfiguredForStep(
        IReadOnlyDictionary<string, string> launchVariables,
        string stepKey)
    {
        ArgumentNullException.ThrowIfNull(launchVariables);

        var stepKeys = ReadStepKeys(launchVariables);
        return stepKeys.Issue is null &&
               stepKeys.StepKeys.Contains(stepKey, StringComparer.OrdinalIgnoreCase);
    }

    internal static IReadOnlyList<string> ResolveExcludedPathFragments(
        IReadOnlyDictionary<string, string> launchVariables,
        string stepKey)
    {
        ArgumentNullException.ThrowIfNull(launchVariables);

        var exclusions = ReadStepStringListMap(
            launchVariables,
            ProcessRuntimeLaunchVariables.ProductSourceInspectionExcludedPathFragmentsByStep);
        if (exclusions.Issue is not null ||
            !exclusions.Values.TryGetValue(stepKey, out var fragments))
        {
            return [];
        }

        return NormalizePathFragments(fragments);
    }

    internal static WorkspaceProductSourceInspectionPolicyEvaluation Evaluate(
        IReadOnlyDictionary<string, string> launchVariables,
        string stepKey,
        string branchOutcomeKey)
    {
        ArgumentNullException.ThrowIfNull(launchVariables);

        var stepKeys = ReadStepKeys(launchVariables);
        if (stepKeys.Issue is not null)
        {
            return Invalid(stepKeys.Issue);
        }

        if (!stepKeys.StepKeys.Contains(stepKey, StringComparer.OrdinalIgnoreCase))
        {
            return WorkspaceProductSourceInspectionPolicyEvaluation.NotConfigured;
        }

        var requiredBranches = ReadStepStringListMap(
            launchVariables,
            ProcessRuntimeLaunchVariables.ProductSourceInspectionRequiredBranchOutcomeKeysByStep);
        if (requiredBranches.Issue is not null)
        {
            return Invalid(requiredBranches.Issue);
        }

        var exclusions = ReadStepStringListMap(
            launchVariables,
            ProcessRuntimeLaunchVariables.ProductSourceInspectionExcludedPathFragmentsByStep);
        if (exclusions.Issue is not null)
        {
            return Invalid(exclusions.Issue);
        }

        var isInspectionRequired = !requiredBranches.HasConfiguration ||
            !requiredBranches.Values.TryGetValue(stepKey, out var requiredBranchOutcomeKeys) ||
            !string.IsNullOrWhiteSpace(branchOutcomeKey) &&
            requiredBranchOutcomeKeys.Contains(branchOutcomeKey, StringComparer.OrdinalIgnoreCase);
        var excludedPathFragments = exclusions.Values.TryGetValue(stepKey, out var fragments)
            ? NormalizePathFragments(fragments)
            : [];
        return new WorkspaceProductSourceInspectionPolicyEvaluation(
            true,
            isInspectionRequired,
            excludedPathFragments,
            null);
    }

    private static WorkspaceProductSourceInspectionPolicyEvaluation Invalid(
        WorkspaceProductSourceInspectionPolicyIssue issue)
        => new(true, false, [], issue);

    private static StepKeysResolution ReadStepKeys(IReadOnlyDictionary<string, string> launchVariables)
    {
        if (!launchVariables.TryGetValue(
                ProcessRuntimeLaunchVariables.ProductSourceInspectionRequiredStepKeys,
                out var value) ||
            string.IsNullOrWhiteSpace(value))
        {
            return new StepKeysResolution([], null);
        }

        try
        {
            using var document = JsonDocument.Parse(value);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return StepKeysResolution.Invalid(
                    ProcessRuntimeLaunchVariables.ProductSourceInspectionRequiredStepKeys,
                    "must be a JSON array of non-empty step keys.");
            }

            var stepKeys = new List<string>();
            foreach (var element in document.RootElement.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.String ||
                    string.IsNullOrWhiteSpace(element.GetString()))
                {
                    return StepKeysResolution.Invalid(
                        ProcessRuntimeLaunchVariables.ProductSourceInspectionRequiredStepKeys,
                        "must be a JSON array of non-empty step keys.");
                }

                stepKeys.Add(element.GetString()!.Trim());
            }

            return new StepKeysResolution(
                stepKeys.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                null);
        }
        catch (JsonException)
        {
            return StepKeysResolution.Invalid(
                ProcessRuntimeLaunchVariables.ProductSourceInspectionRequiredStepKeys,
                "must be valid JSON containing an array of non-empty step keys.");
        }
    }

    private static StepStringListMapResolution ReadStepStringListMap(
        IReadOnlyDictionary<string, string> launchVariables,
        string variableName)
    {
        if (!launchVariables.TryGetValue(variableName, out var value) ||
            string.IsNullOrWhiteSpace(value))
        {
            return new StepStringListMapResolution(false, new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase), null);
        }

        try
        {
            using var document = JsonDocument.Parse(value);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return StepStringListMapResolution.Invalid(
                    variableName,
                    "must be a JSON object mapping non-empty step keys to arrays of non-empty strings.");
            }

            var values = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (string.IsNullOrWhiteSpace(property.Name) ||
                    property.Value.ValueKind != JsonValueKind.Array)
                {
                    return StepStringListMapResolution.Invalid(
                        variableName,
                        "must be a JSON object mapping non-empty step keys to arrays of non-empty strings.");
                }

                var items = new List<string>();
                foreach (var item in property.Value.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.String ||
                        string.IsNullOrWhiteSpace(item.GetString()))
                    {
                        return StepStringListMapResolution.Invalid(
                            variableName,
                            "must be a JSON object mapping non-empty step keys to arrays of non-empty strings.");
                    }

                    items.Add(item.GetString()!.Trim());
                }

                if (!values.TryAdd(
                        property.Name.Trim(),
                        items.Distinct(StringComparer.OrdinalIgnoreCase).ToArray()))
                {
                    return StepStringListMapResolution.Invalid(
                        variableName,
                        "must not contain duplicate step keys.");
                }
            }

            return new StepStringListMapResolution(true, values, null);
        }
        catch (JsonException)
        {
            return StepStringListMapResolution.Invalid(
                variableName,
                "must be valid JSON mapping non-empty step keys to arrays of non-empty strings.");
        }
    }

    private static IReadOnlyList<string> NormalizePathFragments(IEnumerable<string> fragments)
        => fragments
            .Where(fragment => !string.IsNullOrWhiteSpace(fragment))
            .Select(fragment => fragment.Trim().Replace('\\', '/'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private sealed record StepKeysResolution(
        IReadOnlyList<string> StepKeys,
        WorkspaceProductSourceInspectionPolicyIssue? Issue)
    {
        internal static StepKeysResolution Invalid(string variableName, string reason)
            => new([], new WorkspaceProductSourceInspectionPolicyIssue(variableName, reason));
    }

    private sealed record StepStringListMapResolution(
        bool HasConfiguration,
        IReadOnlyDictionary<string, IReadOnlyList<string>> Values,
        WorkspaceProductSourceInspectionPolicyIssue? Issue)
    {
        internal static StepStringListMapResolution Invalid(string variableName, string reason)
            => new(
                true,
                new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase),
                new WorkspaceProductSourceInspectionPolicyIssue(variableName, reason));
    }
}
