using CanDoItAll.Processes.Drivers.SoftwareDeliveryEvidence;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessCriticalToolFailureRules
{
    internal static IReadOnlyList<ProcessAutomationToolExecutionReceipt> ResolveUnresolvedCriticalToolFailures(
        ProcessAutomationExecutionRunDetail detail,
        IReadOnlySet<string> nonCriticalWorkspaceProcessToolNames,
        Func<ProcessAutomationExecutionRunDetail, ProcessAutomationToolExecutionReceipt, bool> shouldIgnoreSupersededFailure)
    {
        ArgumentNullException.ThrowIfNull(detail);
        ArgumentNullException.ThrowIfNull(nonCriticalWorkspaceProcessToolNames);
        ArgumentNullException.ThrowIfNull(shouldIgnoreSupersededFailure);

        var latestCriticalReceipts = detail.ToolReceipts
            .Where(receipt => ProcessToolReceiptFacts.IsCriticalWorkspaceProcessReceipt(receipt, nonCriticalWorkspaceProcessToolNames))
            .GroupBy(
                receipt =>
                {
                    var fact = ProcessToolReceiptFacts.FromReceipt(receipt);
                    return string.Join("|", fact.ToolName, fact.RequestSummary, fact.WorkingDirectory);
                },
                StringComparer.Ordinal)
            .Select(group => group
                .OrderByDescending(receipt => receipt.CompletedAtUtc)
                .ThenByDescending(receipt => receipt.StartedAtUtc)
                .First())
            .ToList();

        return latestCriticalReceipts
            .Where(ProcessToolReceiptFacts.IsFailedReceipt)
            .Where(receipt => !shouldIgnoreSupersededFailure(detail, receipt))
            .ToList();
    }

    internal static bool ShouldIgnoreStackInapplicableCriticalToolFailure(
        ProcessCriticalToolFailureStackContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var toolName = ProcessToolReceiptFacts.NormalizeToolToken(context.Receipt.ToolName);
        if (!SoftwareDeliveryContractRules.IsDotNetWorkspaceToolName(toolName) ||
            context.RequiredToolNames.Contains(toolName, StringComparer.Ordinal))
        {
            return false;
        }

        return !context.ImplementationContractMentionsDotNet &&
               (context.ImplementationContractMentionsJavaScript ||
                context.ImplementationContractNegatesDotNet);
    }
}

internal sealed record ProcessCriticalToolFailureStackContext(
    ProcessAutomationToolExecutionReceipt Receipt,
    IReadOnlyList<string> RequiredToolNames,
    bool ImplementationContractMentionsDotNet,
    bool ImplementationContractMentionsJavaScript,
    bool ImplementationContractNegatesDotNet);
