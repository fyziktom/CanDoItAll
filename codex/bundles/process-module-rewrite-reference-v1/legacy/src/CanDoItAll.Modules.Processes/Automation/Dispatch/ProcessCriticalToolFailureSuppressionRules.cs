using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.Processes;

internal sealed record ProcessCriticalToolFailureSuppressionContext(
    Func<string, bool> IsImplementationBootstrapToolName,
    Func<string, bool> IsConcreteProductMutationToolName,
    Func<string, bool> IsImplementationValidationToolName,
    IReadOnlyCollection<string> ImplementationProofToolNames,
    Func<ProcessAutomationExecutionRunDetail, bool> HasCompletedDeclaredStepOutcome,
    Func<ProcessAutomationExecutionRunDetail, ProcessAutomationToolExecutionReceipt, bool> ShouldIgnoreProviderNativeBrowserOutputFileProbeFailure);

internal static class ProcessCriticalToolFailureSuppressionRules
{
    internal static bool ShouldIgnoreSupersededCriticalToolFailure(
        ProcessAutomationExecutionRunDetail detail,
        ProcessAutomationToolExecutionReceipt receipt,
        ProcessCriticalToolFailureSuppressionContext context)
    {
        ArgumentNullException.ThrowIfNull(detail);
        ArgumentNullException.ThrowIfNull(receipt);
        ArgumentNullException.ThrowIfNull(context);

        if (ShouldIgnoreRecoveredImplementationScaffoldFailure(detail, receipt, context))
        {
            return true;
        }

        if (context.ShouldIgnoreProviderNativeBrowserOutputFileProbeFailure(detail, receipt))
        {
            return true;
        }

        if (!receipt.ExitSummary.StartsWith("Denied", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var normalizedToolName = ProcessToolReceiptFacts.NormalizeToolToken(receipt.ToolName);
        if (string.IsNullOrWhiteSpace(normalizedToolName) ||
            !ProcessArtifactQualityValidationRules.IsPlaceholderCriticalToolRequestSummary(
                normalizedToolName,
                receipt.RequestSummary))
        {
            return false;
        }

        return detail.ToolReceipts.Any(item =>
            !ReferenceEquals(item, receipt) &&
            string.Equals(ProcessToolReceiptFacts.NormalizeToolToken(item.ToolName), normalizedToolName, StringComparison.Ordinal) &&
            !ProcessToolReceiptFacts.IsFailedReceipt(item) &&
            !ProcessArtifactQualityValidationRules.IsPlaceholderCriticalToolRequestSummary(
                normalizedToolName,
                item.RequestSummary));
    }

    private static bool ShouldIgnoreRecoveredImplementationScaffoldFailure(
        ProcessAutomationExecutionRunDetail detail,
        ProcessAutomationToolExecutionReceipt receipt,
        ProcessCriticalToolFailureSuppressionContext context)
    {
        if ((!receipt.ExitSummary.StartsWith("Failed", StringComparison.OrdinalIgnoreCase) &&
             !receipt.ExitSummary.StartsWith("Denied", StringComparison.OrdinalIgnoreCase)) ||
            !context.IsImplementationBootstrapToolName(ProcessToolReceiptFacts.NormalizeToolToken(receipt.ToolName)))
        {
            return false;
        }

        if (detail.Run.State != ProcessAutomationExecutionState.Completed ||
            detail.Run.Outcome != ProcessAutomationRunOutcome.Succeeded)
        {
            return false;
        }

        if (!context.HasCompletedDeclaredStepOutcome(detail))
        {
            return false;
        }

        var successfulReceipts = detail.ToolReceipts
            .Where(item =>
            {
                if (ReferenceEquals(item, receipt) || ProcessToolReceiptFacts.IsFailedReceipt(item))
                {
                    return false;
                }

                var normalizedToolName = ProcessToolReceiptFacts.NormalizeToolToken(item.ToolName);
                return !ProcessArtifactQualityValidationRules.IsPlaceholderCriticalToolRequestSummary(
                    normalizedToolName,
                    item.RequestSummary);
            })
            .ToList();
        var hasProductCreationOrMutation = successfulReceipts.Any(item =>
            context.IsConcreteProductMutationToolName(ProcessToolReceiptFacts.NormalizeToolToken(item.ToolName)));
        var hasValidationOrProof = successfulReceipts.Any(item =>
        {
            var normalizedToolName = ProcessToolReceiptFacts.NormalizeToolToken(item.ToolName);
            return context.ImplementationProofToolNames.Contains(normalizedToolName, StringComparer.Ordinal) ||
                   context.IsImplementationValidationToolName(normalizedToolName);
        });

        return hasProductCreationOrMutation && hasValidationOrProof;
    }
}
