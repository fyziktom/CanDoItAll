namespace CanDoItAll.Modules.Processes;

internal static class ProcessAutomationReceiptObservationHelper
{
    internal static IReadOnlyList<ProcessAutomationToolExecutionReceipt> ResolveSuccessfulReceipts(
        ProcessAutomationExecutionRunDetail detail)
    {
        return ProcessToolReceiptFacts.ResolveSuccessfulReceipts(detail);
    }

    internal static ISet<string> ResolveSuccessfulToolNames(ProcessAutomationExecutionRunDetail detail)
    {
        return ProcessToolReceiptFacts.ResolveSuccessfulToolNames(detail);
    }

    internal static IReadOnlyDictionary<string, IReadOnlyList<ProcessAutomationToolExecutionReceipt>> ResolveReceiptFamilies(
        ProcessAutomationExecutionRunDetail detail)
    {
        return ProcessToolReceiptFacts.ResolveReceiptFamilies(detail);
    }

    internal static IReadOnlyList<ProcessAutomationReceiptProviderMetadata> ResolveProviderMetadata(
        ProcessAutomationExecutionRunDetail detail)
    {
        return ResolveSuccessfulReceipts(detail)
            .Select(receipt => new ProcessAutomationReceiptProviderMetadata(
                ProcessToolReceiptFacts.NormalizeToolToken(receipt.ToolName),
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
        return ProcessToolReceiptFacts.IsFailedReceipt(receipt);
    }
}

internal sealed record ProcessAutomationReceiptProviderMetadata(
    string ToolName,
    string RuntimeToolProviderKey,
    string RuntimeToolProviderName);
