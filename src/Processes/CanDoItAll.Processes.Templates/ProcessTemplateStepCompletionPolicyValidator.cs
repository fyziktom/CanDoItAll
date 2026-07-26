namespace CanDoItAll.Processes.Templates;

internal static class ProcessTemplateStepCompletionPolicyValidator
{
    public static void Validate(
        ProcessTemplateDefinitionStepDocument step,
        string stepPath)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentException.ThrowIfNullOrWhiteSpace(stepPath);

        if (step.CompletionPolicy is not { } policy)
        {
            return;
        }

        var declaredBranchOutcomeKeys = step.BranchOutcomes
            .Select(outcome => outcome.Key?.Trim() ?? string.Empty)
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var runtimeRoutedBranchOutcomeKeys = ValidatePolicyBranchOutcomeKeys(
            policy.RuntimeRoutedBranchOutcomeKeys,
            declaredBranchOutcomeKeys,
            nameof(policy.RuntimeRoutedBranchOutcomeKeys),
            stepPath);
        ValidatePolicyBranchOutcomeKeys(
            policy.AcceptanceCriteriaRequiredBranchOutcomeKeys,
            declaredBranchOutcomeKeys,
            nameof(policy.AcceptanceCriteriaRequiredBranchOutcomeKeys),
            stepPath);
        ValidatePolicyBranchOutcomeKeys(
            policy.ProductSourceInspectionRequiredBranchOutcomeKeys,
            declaredBranchOutcomeKeys,
            nameof(policy.ProductSourceInspectionRequiredBranchOutcomeKeys),
            stepPath);
        ValidatePolicyBranchOutcomeKeys(
            policy.ProductMutationRequiredBranchOutcomeKeys,
            declaredBranchOutcomeKeys,
            nameof(policy.ProductMutationRequiredBranchOutcomeKeys),
            stepPath);
        ValidateCompletionIssueRoutes(
            policy.CompletionIssueRoutes,
            declaredBranchOutcomeKeys,
            runtimeRoutedBranchOutcomeKeys,
            stepPath);

        var effectiveRuleKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rules = new List<ValidatedReceiptRule>(policy.RequiredProductToolReceipts.Count);

        foreach (var requirement in policy.RequiredProductToolReceipts)
        {
            var toolName = Require(
                requirement.ToolName,
                "required product tool receipt tool name",
                stepPath);
            var key = requirement.Key?.Trim() ?? string.Empty;
            var effectiveKey = string.IsNullOrWhiteSpace(key) ? toolName : key;
            if (!effectiveRuleKeys.Add(effectiveKey))
            {
                throw new InvalidOperationException(
                    $"Process template step '{stepPath}' declares duplicate required product tool receipt rule key '{effectiveKey}'. Rules for the same tool and different branches require distinct Key values.");
            }

            var branchOutcomeKeys = ValidateBranchOutcomeKeys(
                requirement.EnforceBranchOutcomeKeys,
                declaredBranchOutcomeKeys,
                toolName,
                stepPath);
            if (requirement.AllowFailedExecutionReceipt && branchOutcomeKeys.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Process template step '{stepPath}' allows failed execution receipt evidence for tool '{toolName}' without EnforceBranchOutcomeKeys. Failed receipt evidence must be scoped to explicit branch outcomes.");
            }

            if (declaredBranchOutcomeKeys.Count > 0 && branchOutcomeKeys.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Process template step '{stepPath}' declares branch outcomes but required product tool receipt '{toolName}' has no EnforceBranchOutcomeKeys. Branch-aware receipt rules must enumerate every branch where the receipt is required.");
            }

            rules.Add(new ValidatedReceiptRule(
                toolName,
                effectiveKey,
                branchOutcomeKeys));
        }

        foreach (var toolRules in rules.GroupBy(rule => rule.ToolName, StringComparer.OrdinalIgnoreCase))
        {
            var materializedRules = toolRules.ToArray();
            for (var leftIndex = 0; leftIndex < materializedRules.Length; leftIndex++)
            {
                for (var rightIndex = leftIndex + 1; rightIndex < materializedRules.Length; rightIndex++)
                {
                    var left = materializedRules[leftIndex];
                    var right = materializedRules[rightIndex];
                    if (!ScopesOverlap(left.BranchOutcomeKeys, right.BranchOutcomeKeys))
                    {
                        continue;
                    }

                    throw new InvalidOperationException(
                        $"Process template step '{stepPath}' declares overlapping required product tool receipt rules '{left.EffectiveKey}' and '{right.EffectiveKey}' for tool '{toolRules.Key}'. Overlapping rules are ambiguous because all applicable rules are consolidated into one completion requirement.");
                }
            }
        }
    }

    private static void ValidateCompletionIssueRoutes(
        IReadOnlyList<ProcessTemplateCompletionIssueRouteDocument> routes,
        IReadOnlySet<string> declaredBranchOutcomeKeys,
        IReadOnlySet<string> runtimeRoutedBranchOutcomeKeys,
        string stepPath)
    {
        foreach (var route in routes)
        {
            var issueCode = Require(
                route.IssueCode,
                "completion issue route issue code",
                stepPath);
            var targetBranchOutcomeKey = Require(
                route.TargetBranchOutcomeKey,
                $"completion issue route '{issueCode}' target branch outcome key",
                stepPath);
            if (!declaredBranchOutcomeKeys.Contains(targetBranchOutcomeKey))
            {
                throw new InvalidOperationException(
                    $"Process template step '{stepPath}' completion issue route '{issueCode}' targets unknown branch outcome '{targetBranchOutcomeKey}'.");
            }

            var sourceBranchOutcomeKeys = ValidatePolicyBranchOutcomeKeys(
                route.SourceBranchOutcomeKeys,
                declaredBranchOutcomeKeys,
                $"completion issue route '{issueCode}' SourceBranchOutcomeKeys",
                stepPath);
            if (!runtimeRoutedBranchOutcomeKeys.Contains(targetBranchOutcomeKey))
            {
                continue;
            }

            var agentSelectableBranchOutcomeCount = declaredBranchOutcomeKeys.Count(
                branchOutcomeKey => !runtimeRoutedBranchOutcomeKeys.Contains(branchOutcomeKey));
            if (sourceBranchOutcomeKeys.Count == 0 && agentSelectableBranchOutcomeCount > 0)
            {
                throw new InvalidOperationException(
                    $"Process template step '{stepPath}' completion issue route '{issueCode}' targets runtime-routed branch '{targetBranchOutcomeKey}' without explicit SourceBranchOutcomeKeys. Runtime-routed branches require explicit, non-self source branches.");
            }

            if (sourceBranchOutcomeKeys.Contains(targetBranchOutcomeKey))
            {
                throw new InvalidOperationException(
                    $"Process template step '{stepPath}' completion issue route '{issueCode}' routes runtime-routed branch '{targetBranchOutcomeKey}' to itself.");
            }
        }
    }

    private static IReadOnlySet<string> ValidateBranchOutcomeKeys(
        IReadOnlyList<string> configuredKeys,
        IReadOnlySet<string> declaredKeys,
        string toolName,
        string stepPath)
    {
        var normalizedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var configuredKey in configuredKeys)
        {
            var branchOutcomeKey = configuredKey?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(branchOutcomeKey))
            {
                throw new InvalidOperationException(
                    $"Process template step '{stepPath}' declares an empty EnforceBranchOutcomeKeys value for required product tool receipt '{toolName}'.");
            }

            if (!normalizedKeys.Add(branchOutcomeKey))
            {
                throw new InvalidOperationException(
                    $"Process template step '{stepPath}' declares branch outcome '{branchOutcomeKey}' more than once for required product tool receipt '{toolName}'.");
            }

            if (!declaredKeys.Contains(branchOutcomeKey))
            {
                throw new InvalidOperationException(
                    $"Process template step '{stepPath}' scopes required product tool receipt '{toolName}' to unknown branch outcome '{branchOutcomeKey}'.");
            }
        }

        return normalizedKeys;
    }

    private static IReadOnlySet<string> ValidatePolicyBranchOutcomeKeys(
        IReadOnlyList<string> configuredKeys,
        IReadOnlySet<string> declaredKeys,
        string field,
        string stepPath)
    {
        var normalizedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var configuredKey in configuredKeys)
        {
            var branchOutcomeKey = configuredKey?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(branchOutcomeKey))
            {
                throw new InvalidOperationException(
                    $"Process template step '{stepPath}' declares an empty {field} value.");
            }

            if (!normalizedKeys.Add(branchOutcomeKey))
            {
                throw new InvalidOperationException(
                    $"Process template step '{stepPath}' declares branch outcome '{branchOutcomeKey}' more than once in {field}.");
            }

            if (!declaredKeys.Contains(branchOutcomeKey))
            {
                throw new InvalidOperationException(
                    $"Process template step '{stepPath}' references unknown branch outcome '{branchOutcomeKey}' in {field}.");
            }
        }

        return normalizedKeys;
    }

    private static bool ScopesOverlap(
        IReadOnlySet<string> left,
        IReadOnlySet<string> right)
        => left.Count == 0 ||
           right.Count == 0 ||
           left.Overlaps(right);

    private static string Require(
        string? value,
        string field,
        string stepPath)
        => string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException(
                $"Process template step '{stepPath}' requires a non-empty {field}.")
            : value.Trim();

    private sealed record ValidatedReceiptRule(
        string ToolName,
        string EffectiveKey,
        IReadOnlySet<string> BranchOutcomeKeys);
}
