using CanDoItAll.AgentFramework.Core;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessImplementationReceiptTimeline
{
    internal static ProcessAutomationToolExecutionReceipt? ResolveLatestImplementationProofReadReceipt(
        bool requiresSourceOrProjectImplementationProof,
        IEnumerable<ProcessAutomationToolExecutionReceipt> successfulReceipts)
    {
        return successfulReceipts
            .Where(receipt => string.Equals(ProcessToolReceiptFacts.NormalizeToolToken(receipt.ToolName), "workspace_read_file", StringComparison.Ordinal))
            .Where(ProcessConcreteProductPathRules.HasConcreteProductPath)
            .Where(receipt => ProcessConcreteProductPathRules.HasConcreteProductImplementationPath(
                requiresSourceOrProjectImplementationProof,
                receipt))
            .OrderByDescending(receipt => receipt.CompletedAtUtc)
            .ThenByDescending(receipt => receipt.StartedAtUtc)
            .FirstOrDefault();
    }

    internal static bool HasBuildValidationReceipt(IReadOnlyList<ProcessAutomationToolExecutionReceipt> successfulReceipts)
    {
        return successfulReceipts.Any(receipt =>
        {
            var toolName = ProcessToolReceiptFacts.NormalizeToolToken(receipt.ToolName);
            return IsBuildValidationToolName(toolName);
        });
    }

    internal static bool IsBuildValidationToolName(string normalizedToolName)
    {
        return normalizedToolName.EndsWith("_build", StringComparison.Ordinal) ||
               normalizedToolName.EndsWith("_test", StringComparison.Ordinal) ||
               normalizedToolName.EndsWith("_publish", StringComparison.Ordinal);
    }

    internal static bool IsRunValidationToolName(string normalizedToolName)
    {
        return normalizedToolName.EndsWith("_run", StringComparison.Ordinal);
    }

    internal static ProcessAutomationToolExecutionReceipt? ResolveLatestRequiredImplementationValidationReceipt(
        IReadOnlySet<string> requiredToolNames,
        IReadOnlyList<ProcessAutomationToolExecutionReceipt> successfulReceipts)
    {
        if (requiredToolNames.Count == 0)
        {
            return null;
        }

        return successfulReceipts
            .Where(receipt =>
            {
                var normalizedToolName = ProcessToolReceiptFacts.NormalizeToolToken(receipt.ToolName);
                return requiredToolNames.Contains(normalizedToolName) &&
                       IsImplementationValidationToolName(normalizedToolName);
            })
            .OrderByDescending(receipt => receipt.CompletedAtUtc)
            .ThenByDescending(receipt => receipt.StartedAtUtc)
            .FirstOrDefault();
    }

    internal static ProcessAutomationToolExecutionReceipt? ResolveLatestReceipt(
        IEnumerable<ProcessAutomationToolExecutionReceipt> receipts,
        string normalizedToolName,
        bool requireConcreteProductPath,
        bool requireConcreteDeliverableOrSourcePath)
    {
        return ResolveLatestReceipt(
            receipts,
            toolName => string.Equals(toolName, normalizedToolName, StringComparison.Ordinal),
            requireConcreteProductPath,
            requireConcreteDeliverableOrSourcePath);
    }

    internal static ProcessAutomationToolExecutionReceipt? ResolveLatestReceipt(
        IEnumerable<ProcessAutomationToolExecutionReceipt> receipts,
        Func<string, bool> matchesToolName,
        bool requireConcreteProductPath,
        bool requireConcreteDeliverableOrSourcePath)
    {
        return receipts
            .Where(receipt => matchesToolName(ProcessToolReceiptFacts.NormalizeToolToken(receipt.ToolName)))
            .Where(receipt => !requireConcreteProductPath || ProcessConcreteProductPathRules.HasConcreteProductPath(receipt))
            .Where(receipt => !requireConcreteDeliverableOrSourcePath ||
                              ProcessConcreteProductPathRules.HasConcreteProductDeliverableOrSourcePath(receipt))
            .OrderByDescending(receipt => receipt.CompletedAtUtc)
            .ThenByDescending(receipt => receipt.StartedAtUtc)
            .FirstOrDefault();
    }

    internal static bool IsConcreteProductMutationToolName(
        IReadOnlyCollection<string> concreteProductMutationToolNames,
        string normalizedToolName)
    {
        return concreteProductMutationToolNames.Contains(normalizedToolName) ||
               IsImplementationBootstrapToolName(normalizedToolName);
    }

    internal static bool IsImplementationBootstrapToolName(string normalizedToolName)
    {
        return normalizedToolName.StartsWith("workspace_", StringComparison.Ordinal) &&
               normalizedToolName.EndsWith("_new", StringComparison.Ordinal);
    }

    internal static bool IsImplementationValidationToolName(string normalizedToolName)
    {
        return normalizedToolName.EndsWith("_build", StringComparison.Ordinal) ||
               normalizedToolName.EndsWith("_test", StringComparison.Ordinal) ||
               normalizedToolName.EndsWith("_run", StringComparison.Ordinal) ||
               normalizedToolName.EndsWith("_publish", StringComparison.Ordinal) ||
               normalizedToolName.EndsWith("_validate", StringComparison.Ordinal) ||
               normalizedToolName.EndsWith("_lint", StringComparison.Ordinal) ||
               normalizedToolName.EndsWith("_check", StringComparison.Ordinal) ||
               normalizedToolName.StartsWith("browser_", StringComparison.Ordinal);
    }

    internal static bool IsReceiptAfter(
        ProcessAutomationToolExecutionReceipt candidate,
        ProcessAutomationToolExecutionReceipt baseline)
    {
        return candidate.CompletedAtUtc > baseline.CompletedAtUtc ||
               candidate.CompletedAtUtc == baseline.CompletedAtUtc &&
               candidate.StartedAtUtc > baseline.StartedAtUtc;
    }
}
