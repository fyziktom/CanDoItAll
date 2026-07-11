using System.Text.Json;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessProductSourceInspectionPolicy
{
    internal static bool IsRequired(
        IReadOnlyDictionary<string, string> launchVariables,
        string stepKey,
        string branchOutcomeKey)
    {
        if (!IsConfiguredForStep(launchVariables, stepKey))
        {
            return false;
        }

        return IsRequiredForBranch(launchVariables, stepKey, branchOutcomeKey);
    }

    internal static bool IsConfiguredForStep(
        IReadOnlyDictionary<string, string> launchVariables,
        string stepKey)
    {
        if (!launchVariables.TryGetValue(
                ProcessRuntimeLaunchVariables.ProductSourceInspectionRequiredStepKeys,
                out var configuredStepKeys) ||
            string.IsNullOrWhiteSpace(configuredStepKeys))
        {
            return false;
        }

        try
        {
            var stepKeys = JsonSerializer.Deserialize<string[]>(configuredStepKeys);
            return stepKeys?.Contains(stepKey, StringComparer.OrdinalIgnoreCase) == true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool IsRequiredForBranch(
        IReadOnlyDictionary<string, string> launchVariables,
        string stepKey,
        string branchOutcomeKey)
    {
        if (!launchVariables.TryGetValue(
                ProcessRuntimeLaunchVariables.ProductSourceInspectionRequiredBranchOutcomeKeysByStep,
                out var configuredRules) ||
            string.IsNullOrWhiteSpace(configuredRules))
        {
            return true;
        }

        try
        {
            var rules = JsonSerializer.Deserialize<Dictionary<string, string[]>>(configuredRules);
            var match = rules?.FirstOrDefault(pair =>
                string.Equals(pair.Key, stepKey, StringComparison.OrdinalIgnoreCase));
            if (match is null)
            {
                return true;
            }

            return !string.IsNullOrWhiteSpace(branchOutcomeKey) &&
                   match.Value.Value.Contains(branchOutcomeKey, StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return true;
        }
    }

    internal static IReadOnlyList<string> ResolveExcludedPathFragments(
        IReadOnlyDictionary<string, string> launchVariables,
        string stepKey)
    {
        if (!launchVariables.TryGetValue(
                ProcessRuntimeLaunchVariables.ProductSourceInspectionExcludedPathFragmentsByStep,
                out var configuredRules) ||
            string.IsNullOrWhiteSpace(configuredRules))
        {
            return [];
        }

        try
        {
            var rules = JsonSerializer.Deserialize<Dictionary<string, string[]>>(configuredRules);
            var match = rules?.FirstOrDefault(pair =>
                string.Equals(pair.Key, stepKey, StringComparison.OrdinalIgnoreCase));
            return match?.Value?
                .Where(fragment => !string.IsNullOrWhiteSpace(fragment))
                .Select(fragment => fragment.Trim().Replace('\\', '/'))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray() ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
