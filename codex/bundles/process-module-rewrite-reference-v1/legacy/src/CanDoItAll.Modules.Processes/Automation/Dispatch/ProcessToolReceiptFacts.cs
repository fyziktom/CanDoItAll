namespace CanDoItAll.Modules.Processes;

internal static class ProcessToolReceiptFacts
{
    internal static IReadOnlyList<ProcessToolReceiptFact> FromDetail(ProcessAutomationExecutionRunDetail detail)
    {
        ArgumentNullException.ThrowIfNull(detail);

        return detail.ToolReceipts
            .Select(FromReceipt)
            .ToList();
    }

    internal static ProcessToolReceiptFact FromReceipt(ProcessAutomationToolExecutionReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);

        return new ProcessToolReceiptFact(
            receipt,
            NormalizeToolToken(receipt.ToolName),
            NormalizeReceiptFamily(receipt.ToolFamily),
            receipt.RequestSummary.Trim(),
            receipt.WorkingDirectory.Trim(),
            receipt.ExitSummary.Trim(),
            IsFailedReceipt(receipt));
    }

    internal static IReadOnlyList<ProcessAutomationToolExecutionReceipt> ResolveSuccessfulReceipts(
        ProcessAutomationExecutionRunDetail detail)
    {
        return FromDetail(detail)
            .Where(fact => !fact.IsFailed)
            .Select(fact => fact.Receipt)
            .ToList();
    }

    internal static ISet<string> ResolveSuccessfulToolNames(ProcessAutomationExecutionRunDetail detail)
    {
        return FromDetail(detail)
            .Where(fact => !fact.IsFailed)
            .Select(fact => fact.ToolName)
            .Where(toolName => !string.IsNullOrWhiteSpace(toolName))
            .ToHashSet(StringComparer.Ordinal);
    }

    internal static IReadOnlyDictionary<string, IReadOnlyList<ProcessAutomationToolExecutionReceipt>> ResolveReceiptFamilies(
        ProcessAutomationExecutionRunDetail detail)
    {
        return FromDetail(detail)
            .GroupBy(fact => fact.ToolFamily, StringComparer.Ordinal)
            .Where(group => !string.IsNullOrWhiteSpace(group.Key))
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<ProcessAutomationToolExecutionReceipt>)group
                    .Select(fact => fact.Receipt)
                    .ToList(),
                StringComparer.Ordinal);
    }

    internal static bool IsCriticalWorkspaceProcessReceipt(
        ProcessAutomationToolExecutionReceipt receipt,
        IReadOnlySet<string> nonCriticalWorkspaceProcessToolNames)
    {
        var fact = FromReceipt(receipt);
        return string.Equals(fact.ToolFamily, "workspace-process", StringComparison.Ordinal) &&
               !string.IsNullOrWhiteSpace(fact.ToolName) &&
               !nonCriticalWorkspaceProcessToolNames.Contains(fact.ToolName);
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

    internal static string NormalizeToolToken(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Replace('-', '_').Trim().ToLowerInvariant();
    }

    internal static string NormalizeReceiptFamily(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant();
    }
}

internal sealed record ProcessToolReceiptFact(
    ProcessAutomationToolExecutionReceipt Receipt,
    string ToolName,
    string ToolFamily,
    string RequestSummary,
    string WorkingDirectory,
    string ExitSummary,
    bool IsFailed);
