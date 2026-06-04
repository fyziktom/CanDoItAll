namespace CanDoItAll.Modules.Processes;

internal static class ProcessAutomationReceiptObservationHelper
{
    internal static IReadOnlyList<ProcessAutomationToolExecutionReceipt> ResolveSuccessfulReceipts(
        ProcessAutomationExecutionRunDetail detail)
    {
        ArgumentNullException.ThrowIfNull(detail);

        return detail.ToolReceipts
            .Where(receipt => !IsFailedReceipt(receipt))
            .ToList();
    }

    internal static ISet<string> ResolveSuccessfulToolNames(ProcessAutomationExecutionRunDetail detail)
    {
        return ResolveSuccessfulReceipts(detail)
            .Select(receipt => NormalizeToolToken(receipt.ToolName))
            .Where(toolName => !string.IsNullOrWhiteSpace(toolName))
            .ToHashSet(StringComparer.Ordinal);
    }

    internal static IReadOnlyDictionary<string, IReadOnlyList<ProcessAutomationToolExecutionReceipt>> ResolveReceiptFamilies(
        ProcessAutomationExecutionRunDetail detail)
    {
        ArgumentNullException.ThrowIfNull(detail);

        return detail.ToolReceipts
            .GroupBy(
                receipt => NormalizeReceiptFamily(receipt.ToolFamily),
                StringComparer.Ordinal)
            .Where(group => !string.IsNullOrWhiteSpace(group.Key))
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<ProcessAutomationToolExecutionReceipt>)group.ToList(),
                StringComparer.Ordinal);
    }

    internal static IReadOnlyList<ProcessAutomationReceiptProviderMetadata> ResolveProviderMetadata(
        ProcessAutomationExecutionRunDetail detail)
    {
        return ResolveSuccessfulReceipts(detail)
            .Select(receipt => new ProcessAutomationReceiptProviderMetadata(
                NormalizeToolToken(receipt.ToolName),
                receipt.RuntimeToolProviderKey.Trim(),
                receipt.RuntimeToolProviderName.Trim()))
            .Where(metadata =>
                !string.IsNullOrWhiteSpace(metadata.ToolName) &&
                (!string.IsNullOrWhiteSpace(metadata.RuntimeToolProviderKey) ||
                 !string.IsNullOrWhiteSpace(metadata.RuntimeToolProviderName)))
            .GroupBy(
                metadata => string.Join(
                    "|",
                    metadata.ToolName,
                    metadata.RuntimeToolProviderKey,
                    metadata.RuntimeToolProviderName),
                StringComparer.Ordinal)
            .Select(group => group.First())
            .ToList();
    }

    internal static bool IsFailedReceipt(ProcessAutomationToolExecutionReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);

        if (string.IsNullOrWhiteSpace(receipt.ExitSummary))
        {
            return false;
        }

        return receipt.ExitSummary.StartsWith("Failed", StringComparison.OrdinalIgnoreCase) ||
               receipt.ExitSummary.StartsWith("Denied", StringComparison.OrdinalIgnoreCase) ||
               receipt.ExitSummary.StartsWith("TimedOut", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeReceiptFamily(string value)
    {
        return value.Trim().ToLowerInvariant();
    }

    private static string NormalizeToolToken(string value)
    {
        return value.Trim().Replace('-', '_').ToLowerInvariant();
    }
}

internal sealed record ProcessAutomationReceiptProviderMetadata(
    string ToolName,
    string RuntimeToolProviderKey,
    string RuntimeToolProviderName);
