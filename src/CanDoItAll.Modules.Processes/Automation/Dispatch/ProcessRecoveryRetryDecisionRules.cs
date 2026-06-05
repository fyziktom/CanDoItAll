using CanDoItAll.AgentFramework.Core;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessRecoveryRetryDecisionRules
{
    internal static ProcessRecoveryRetryFacts CreateFacts(
        ProcessAutomationExecutionRunDetail detail,
        IReadOnlyList<string> missingRequiredTools,
        IReadOnlyList<ProcessAutomationToolExecutionReceipt> unresolvedCriticalToolFailures)
    {
        ArgumentNullException.ThrowIfNull(detail);
        ArgumentNullException.ThrowIfNull(missingRequiredTools);
        ArgumentNullException.ThrowIfNull(unresolvedCriticalToolFailures);

        return new ProcessRecoveryRetryFacts(
            missingRequiredTools,
            unresolvedCriticalToolFailures,
            detail.ToolReceipts
                .Where(ProcessToolReceiptFacts.IsFailedReceipt)
                .Select(receipt => ProcessToolReceiptFacts.NormalizeToolToken(receipt.ToolName))
                .Where(toolName => !string.IsNullOrWhiteSpace(toolName))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(toolName => toolName, StringComparer.Ordinal)
                .ToArray());
    }

    internal static bool TryResolveToolFailureCategory(
        ProcessRecoveryRetryFacts facts,
        out AgentFailureCategory category)
    {
        ArgumentNullException.ThrowIfNull(facts);

        if (facts.HasTestFailure)
        {
            category = AgentFailureCategory.TestFailure;
            return true;
        }

        if (facts.HasBuildFailure)
        {
            category = AgentFailureCategory.BuildFailure;
            return true;
        }

        category = AgentFailureCategory.Unknown;
        return false;
    }

    internal static string ResolveMissingRequiredToolsReason(ProcessRecoveryRetryFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);

        return $"Missing required tool execution(s): {string.Join(", ", facts.MissingRequiredTools)}.";
    }

    internal static string ResolveCriticalToolFailureReason(ProcessRecoveryRetryFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);

        return string.Join(
            "; ",
            facts.UnresolvedCriticalToolFailures.Take(2).Select(receipt => $"{receipt.ToolName}: {receipt.ExitSummary}"));
    }
}

internal sealed record ProcessRecoveryRetryFacts(
    IReadOnlyList<string> MissingRequiredTools,
    IReadOnlyList<ProcessAutomationToolExecutionReceipt> UnresolvedCriticalToolFailures,
    IReadOnlyList<string> FailedToolNames)
{
    internal bool HasMissingRequiredTools => MissingRequiredTools.Count > 0;

    internal bool HasUnresolvedCriticalToolFailures => UnresolvedCriticalToolFailures.Count > 0;

    internal bool HasTestFailure => UnresolvedCriticalToolFailures.Any(receipt =>
        ProcessToolReceiptFacts.NormalizeToolToken(receipt.ToolName).EndsWith("_test", StringComparison.Ordinal));

    internal bool HasBuildFailure => UnresolvedCriticalToolFailures.Any(receipt =>
        ProcessToolReceiptFacts.NormalizeToolToken(receipt.ToolName).EndsWith("_build", StringComparison.Ordinal));
}
