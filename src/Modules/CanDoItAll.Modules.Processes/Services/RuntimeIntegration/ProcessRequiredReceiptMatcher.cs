using System.Globalization;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Hosting;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Builder;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Drivers.Standard;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Processes.Templates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using static CanDoItAll.Modules.Processes.ProcessCompletionRuleParser;
using static CanDoItAll.Modules.Processes.ProcessProductCompletionRuleParser;
using static CanDoItAll.Modules.Processes.ProcessProductRootResolver;
using static CanDoItAll.Modules.Processes.ProcessCompletionText;
using static CanDoItAll.Modules.Processes.ProcessExecutionMetadataBuilder;
using static CanDoItAll.Modules.Processes.ProcessExecutionResultFactory;
using static CanDoItAll.Modules.Processes.ProcessManagedArtifactEvidence;
using static CanDoItAll.Modules.Processes.ProcessManagedArtifactService;
using static CanDoItAll.Modules.Processes.ProcessManagedArtifactFormatter;
using static CanDoItAll.Modules.Processes.ProcessManagedArtifactOutcomeParser;
using static CanDoItAll.Modules.Processes.ProcessRuntimeLifecycleReceiptFacts;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessRequiredReceiptMatcher
{
    internal static IReadOnlyList<ProductCompletionRequiredToolReceiptRule> ResolveApplicableProductCompletionRequiredToolReceiptRules(
        ProcessRuntimeStepAssignment assignment,
        string branchOutcomeKey)
        => ResolveProductCompletionRequiredToolReceiptRules(assignment.LaunchVariables, assignment.StepKey)
            .Where(rule => IsApplicableToBranchOutcome(
                rule.ApplicableBranchOutcomeKeys,
                rule.SkippedBranchOutcomeKeys,
                branchOutcomeKey))
            .ToArray();

    internal static IReadOnlySet<string> ResolveProductCoveredRuntimeToolNames(
        IReadOnlyList<ProductCompletionRequiredToolReceiptRule> rules)
        => ProcessRequiredRuntimeToolNames
            .FromProductCompletionRequiredToolReceipts(rules.Select(rule => rule.ToolReceipt))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    internal static IReadOnlySet<string> ResolveEnforcedProductCoveredRuntimeToolNames(
        ProcessRuntimeStepAssignment assignment,
        IReadOnlyList<ProductCompletionRequiredToolReceiptRule> rules)
    {
        var requiredToolReceipts = rules
            .Select(rule => rule.ToolReceipt)
            .Where(receipt => !string.IsNullOrWhiteSpace(receipt))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return ShouldEnforceRequiredProductToolReceipts(assignment, requiredToolReceipts)
            ? ResolveProductCoveredRuntimeToolNames(rules)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    internal static bool IsApplicableToBranchOutcome(
        IReadOnlyList<string> applicableBranchOutcomeKeys,
        string branchOutcomeKey)
        => IsApplicableToBranchOutcome(applicableBranchOutcomeKeys, [], branchOutcomeKey);

    internal static bool IsApplicableToBranchOutcome(
        IReadOnlyList<string> applicableBranchOutcomeKeys,
        IReadOnlyList<string> skippedBranchOutcomeKeys,
        string branchOutcomeKey)
    {
        if (string.IsNullOrWhiteSpace(branchOutcomeKey))
        {
            return applicableBranchOutcomeKeys.Count == 0 && skippedBranchOutcomeKeys.Count == 0;
        }

        var normalizedBranch = branchOutcomeKey.Trim();
        if (skippedBranchOutcomeKeys.Contains(normalizedBranch, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        return applicableBranchOutcomeKeys.Count == 0 ||
               applicableBranchOutcomeKeys.Contains(normalizedBranch, StringComparer.OrdinalIgnoreCase);
    }

    internal static bool ShouldEnforceRequiredProductToolReceipts(
        ProcessRuntimeStepAssignment assignment,
        IReadOnlyList<string> requiredToolReceipts)
    {
        if (requiredToolReceipts.Count == 0)
        {
            return false;
        }

        var operations = NormalizeOperations(assignment.AllowedOperations);
        return AllowsProductMutation(operations, assignment.OperationTargetScope) ||
               operations.Contains(ProcessOperationContractNames.RunValidation, StringComparer.OrdinalIgnoreCase) ||
               operations.Contains(ProcessOperationContractNames.LaunchRuntime, StringComparer.OrdinalIgnoreCase) ||
               operations.Contains(ProcessOperationContractNames.CaptureRuntimeProof, StringComparer.OrdinalIgnoreCase) ||
               operations.Contains(ProcessOperationContractNames.ExecuteExternalAction, StringComparer.OrdinalIgnoreCase);
    }

    internal static bool AllowsFailedRequiredToolReceipt(ProcessRuntimeStepAssignment assignment)
    {
        var operations = NormalizeOperations(assignment.AllowedOperations);
        return operations.Contains(ProcessOperationContractNames.RunValidation, StringComparer.OrdinalIgnoreCase) &&
               !AllowsProductMutation(operations, assignment.OperationTargetScope);
    }

    internal static bool HasRequiredToolReceipt(
        IReadOnlyList<ToolExecutionReceiptRecord> toolReceipts,
        string requiredToolReceipt,
        bool allowFailedExecutionReceipt,
        ProcessToolReceiptPolicyCatalog toolReceiptPolicies)
    {
        var normalizedRequirement = requiredToolReceipt.Trim();
        var semanticRequirement = toolReceipts
            .Select(receipt => (receipt, match: toolReceiptPolicies.MatchRequirement(receipt, normalizedRequirement)))
            .Where(candidate => candidate.match.IsHandled)
            .ToArray();
        if (semanticRequirement.Length > 0)
        {
            return semanticRequirement.Any(candidate =>
                candidate.match.IsMatch &&
                IsSuccessfulReceipt(candidate.receipt.ExitSummary));
        }

        return !string.IsNullOrWhiteSpace(normalizedRequirement) &&
               toolReceipts.Any(receipt =>
                   IsRequiredToolReceiptUsable(receipt, allowFailedExecutionReceipt) &&
                   IsRequiredToolReceiptMatch(receipt, normalizedRequirement, toolReceiptPolicies));
    }

    internal static bool IsRequiredToolReceiptMatch(
        ToolExecutionReceiptRecord receipt,
        string normalizedRequirement,
        ProcessToolReceiptPolicyCatalog toolReceiptPolicies)
    {
        if (string.IsNullOrWhiteSpace(normalizedRequirement))
        {
            return false;
        }

        var policyMatch = toolReceiptPolicies.MatchRequirement(receipt, normalizedRequirement);
        if (policyMatch.IsHandled)
        {
            return policyMatch.IsMatch;
        }

        if (LooksLikeConcreteToolName(normalizedRequirement))
        {
            return string.Equals(receipt.ToolName, normalizedRequirement, StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(receipt.ToolName, normalizedRequirement, StringComparison.OrdinalIgnoreCase) ||
               ReceiptText(receipt).Contains(normalizedRequirement, StringComparison.OrdinalIgnoreCase);
    }

    internal static bool LooksLikeConcreteToolName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return Regex.IsMatch(
            value.Trim(),
            @"^[a-z][a-z0-9]*(?:_[a-z0-9]+)+$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    internal static string BuildFailedRequiredToolReceiptGuidance(
        ProcessRuntimeStepAssignment assignment,
        IReadOnlyList<ToolExecutionReceiptRecord> observedToolReceipts,
        IReadOnlyList<string> missingToolReceipts,
        bool allowFailedExecutionReceipt,
        ProcessToolReceiptPolicyCatalog toolReceiptPolicies)
    {
        if (allowFailedExecutionReceipt || missingToolReceipts.Count == 0 || observedToolReceipts.Count == 0)
        {
            return string.Empty;
        }

        var failedMatches = missingToolReceipts
            .SelectMany(requiredToolReceipt =>
            {
                var normalizedRequirement = requiredToolReceipt.Trim();
                if (string.IsNullOrWhiteSpace(normalizedRequirement))
                {
                    return Array.Empty<string>();
                }

                return observedToolReceipts
                    .Where(receipt =>
                        IsRequiredToolReceiptMatch(receipt, normalizedRequirement, toolReceiptPolicies) &&
                        !IsSuccessfulReceipt(receipt.ExitSummary) &&
                        !IsConcreteToolBoundaryReceipt(receipt))
                    .Select(receipt => $"{receipt.ToolName} ({SummarizeReceiptExit(receipt.ExitSummary)})");
            })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToArray();
        if (failedMatches.Length == 0)
        {
            return string.Empty;
        }

        var operations = NormalizeOperations(assignment.AllowedOperations);
        var repairGuidance = AllowsProductMutation(operations, assignment.OperationTargetScope)
            ? " For product-mutating steps, inspect the failing command output, mutate the product target before rerunning validation, and complete only after the required receipts succeed."
            : " Retry the required commands and complete only after the required receipts succeed.";

        return $" Matching current-run receipt(s) were present but failed: {string.Join("; ", failedMatches)}.{repairGuidance}";
    }

    internal static string BuildMissingRequiredToolReceiptGuidance(
        ProcessRuntimeStepAssignment assignment,
        IReadOnlyList<string> missingToolReceipts,
        ProcessToolReceiptPolicyCatalog toolReceiptPolicies)
    {
        if (!missingToolReceipts.Any(required =>
                string.Equals(required.Trim(), "workspace_pwsh_run_script", StringComparison.OrdinalIgnoreCase)))
        {
            return string.Empty;
        }

        if (!toolReceiptPolicies.TryResolveScriptHelper(assignment, out var scriptHelper))
        {
            return " Before retrying, invoke the reviewed current-run helper with workspace_pwsh_run_script and read back the affected product files before rewriting the primary managed artifact.";
        }

        var scriptRef = assignment.LaunchVariables.TryGetValue(scriptHelper.ScriptRefVariableName, out var configuredScriptRef) &&
                        !string.IsNullOrWhiteSpace(configuredScriptRef)
            ? configuredScriptRef.Trim()
            : scriptHelper.ScriptRefVariableName;
        var manifestGuidance = string.IsNullOrWhiteSpace(scriptHelper.ManifestVariableName)
            ? string.Empty
            : $" and sideEffectManifest from {scriptHelper.ManifestVariableName}";

        return $" Before retrying, write launch variable {scriptHelper.ScriptVariableName} verbatim to '{scriptRef}', verify that .ps1 ref, invoke workspace_pwsh_run_script with path '{scriptRef}'{manifestGuidance}, then read back the product files and rewrite the primary managed artifact only after the script receipt exists.";
    }

    internal static string SummarizeReceiptExit(string exitSummary)
    {
        var normalized = NormalizeReceiptCommandText(exitSummary);
        return normalized.Length <= 160 ? normalized : normalized[..160];
    }

    internal static string NormalizeReceiptCommandText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value
            .Replace('"', ' ')
            .Replace('\'', ' ')
            .ReplaceLineEndings(" ")
            .Trim();
        return string.Join(
            " ",
            normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    internal static string ReceiptText(ToolExecutionReceiptRecord receipt)
        => $"{receipt.ToolName} {receipt.RequestSummary} {receipt.WorkingDirectory} {receipt.ExitSummary}";
}
