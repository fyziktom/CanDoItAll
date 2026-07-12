using System.Text.Json;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Processes.Templates;

namespace CanDoItAll.Processes.Application;

internal static class ProcessTemplateStepCompletionPolicyMaterializer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    internal static void Apply(
        IDictionary<string, string> variables,
        ProcessTemplateDefinitionStepDocument step)
    {
        ArgumentNullException.ThrowIfNull(variables);
        ArgumentNullException.ThrowIfNull(step);

        RemoveCanonical(variables, ProcessRuntimeLaunchVariables.AcceptanceCriteriaAcceptedBranchOutcomeKeys);
        if (step.CompletionPolicy is null)
        {
            return;
        }

        var policy = step.CompletionPolicy;
        RemoveCanonical(variables, ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceipts);
        RemoveCanonical(variables, ProcessRuntimeLaunchVariables.ProductMutationRequiredBranchOutcomeKeys);
        RemoveCanonical(variables, ProcessRuntimeLaunchVariables.ProductMutationBeforeManagedOutputRequiredStepKeys);
        RemoveCanonical(variables, ProcessRuntimeLaunchVariables.ProductMutationToolNames);
        RemoveCanonical(variables, ProcessRuntimeLaunchVariables.RuntimeRoutedBranchOutcomeKeys);
        RemoveCanonical(variables, ProcessRuntimeLaunchVariables.ProductSourceInspectionRequiredStepKeys);
        RemoveCanonical(variables, ProcessRuntimeLaunchVariables.ProductSourceInspectionRequiredBranchOutcomeKeysByStep);
        RemoveCanonical(variables, ProcessRuntimeLaunchVariables.CompletionIssueRoutes);

        if (policy.RequiredProductToolReceipts.Count > 0)
        {
            SetCanonical(
                variables,
                ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceipts,
                JsonSerializer.Serialize(policy.RequiredProductToolReceipts, JsonOptions));
        }

        if (policy.CompletionIssueRoutes.Count > 0)
        {
            SetCanonical(
                variables,
                ProcessRuntimeLaunchVariables.CompletionIssueRoutes,
                JsonSerializer.Serialize(policy.CompletionIssueRoutes, JsonOptions));
        }

        SetStringList(
            variables,
            ProcessRuntimeLaunchVariables.ProductMutationRequiredBranchOutcomeKeys,
            policy.ProductMutationRequiredBranchOutcomeKeys);
        SetStringList(
            variables,
            ProcessRuntimeLaunchVariables.RuntimeRoutedBranchOutcomeKeys,
            policy.RuntimeRoutedBranchOutcomeKeys);
        SetStringList(
            variables,
            ProcessRuntimeLaunchVariables.ProductMutationToolNames,
            policy.ProductMutationToolNames);
        SetStringList(
            variables,
            ProcessRuntimeLaunchVariables.AcceptanceCriteriaAcceptedBranchOutcomeKeys,
            policy.AcceptanceCriteriaRequiredBranchOutcomeKeys);

        if (policy.RequiresProductMutationBeforeManagedOutput)
        {
            SetCanonical(
                variables,
                ProcessRuntimeLaunchVariables.ProductMutationBeforeManagedOutputRequiredStepKeys,
                JsonSerializer.Serialize(new[] { step.Key }, JsonOptions));
        }

        if (!policy.RequiresProductSourceInspection)
        {
            return;
        }

        SetCanonical(
            variables,
            ProcessRuntimeLaunchVariables.ProductSourceInspectionRequiredStepKeys,
            JsonSerializer.Serialize(new[] { step.Key }, JsonOptions));
        if (policy.ProductSourceInspectionRequiredBranchOutcomeKeys.Count == 0)
        {
            return;
        }

        var branchMap = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            [step.Key] = NormalizeStringList(policy.ProductSourceInspectionRequiredBranchOutcomeKeys)
        };
        SetCanonical(
            variables,
            ProcessRuntimeLaunchVariables.ProductSourceInspectionRequiredBranchOutcomeKeysByStep,
            JsonSerializer.Serialize(branchMap, JsonOptions));
    }

    private static void SetStringList(
        IDictionary<string, string> variables,
        string key,
        IReadOnlyList<string> values)
    {
        var normalized = NormalizeStringList(values);
        if (normalized.Count == 0)
        {
            return;
        }

        SetCanonical(variables, key, JsonSerializer.Serialize(normalized, JsonOptions));
    }

    private static IReadOnlyList<string> NormalizeStringList(IEnumerable<string>? values)
        => (values ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static void SetCanonical(
        IDictionary<string, string> variables,
        string key,
        string value)
    {
        RemoveCanonical(variables, key);
        variables[key] = value;
    }

    private static void RemoveCanonical(
        IDictionary<string, string> variables,
        string key)
    {
        foreach (var existingKey in variables.Keys
            .Where(candidate => string.Equals(candidate, key, StringComparison.OrdinalIgnoreCase))
            .ToArray())
        {
            variables.Remove(existingKey);
        }
    }
}
