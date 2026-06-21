using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessDeclaredStepOutcomeRules
{
    internal static bool TryResolve(
        string? responseText,
        out ProcessDeclaredStepOutcomeFacts declaredOutcome,
        out ProcessStepOutcomeResult outcome)
    {
        declaredOutcome = default;
        outcome = default!;
        var result = AgentOutputJson.DeserializeAndValidate(
            responseText,
            new ProcessStepOutcomeValidator());
        if (!result.Succeeded || result.Output is null)
        {
            return false;
        }

        outcome = result.Output;
        declaredOutcome = new ProcessDeclaredStepOutcomeFacts(
            MapStatus(outcome.Status),
            outcome.Reason.Trim(),
            outcome.BranchOutcomeKey.Trim(),
            outcome.BranchOutcomeTitle.Trim());
        return true;
    }

    internal static bool BlockedOutcomeClaimsRequiredToolFailureWithoutReceipt(
        ProcessStepRunStatus declaredStatus,
        string declaredReason,
        string responseInspectionText,
        IReadOnlyList<string> missingRequiredTools,
        IReadOnlyList<ProcessAutomationToolExecutionReceipt> receipts)
    {
        if (declaredStatus != ProcessStepRunStatus.Blocked ||
            missingRequiredTools.Count == 0 ||
            HasFailedReceiptForRequiredTool(receipts, missingRequiredTools))
        {
            return false;
        }

        var normalizedText = CollapseWhitespace($"{declaredReason} {responseInspectionText}");
        if (string.IsNullOrWhiteSpace(normalizedText))
        {
            return false;
        }

        return missingRequiredTools.Any(toolName =>
            normalizedText.Contains(toolName, StringComparison.OrdinalIgnoreCase)) ||
               (normalizedText.Contains("tool", StringComparison.OrdinalIgnoreCase) &&
                (normalizedText.Contains("failed", StringComparison.OrdinalIgnoreCase) ||
                 normalizedText.Contains("failure", StringComparison.OrdinalIgnoreCase) ||
                 normalizedText.Contains("unavailable", StringComparison.OrdinalIgnoreCase) ||
                 normalizedText.Contains("denied", StringComparison.OrdinalIgnoreCase)));
    }

    internal static bool HasFailedReceiptForRequiredTool(
        IReadOnlyList<ProcessAutomationToolExecutionReceipt> receipts,
        IReadOnlyList<string> requiredToolNames)
    {
        if (requiredToolNames.Count == 0)
        {
            return false;
        }

        var required = requiredToolNames
            .Select(ProcessToolReceiptFacts.NormalizeToolToken)
            .Where(toolName => !string.IsNullOrWhiteSpace(toolName))
            .ToHashSet(StringComparer.Ordinal);

        return receipts.Any(receipt =>
            required.Contains(ProcessToolReceiptFacts.NormalizeToolToken(receipt.ToolName)) &&
            ProcessToolReceiptFacts.IsFailedReceipt(receipt));
    }

    internal static string BuildReason(string runTitle, string stepTitle, ProcessStepRunStatus status, string reason)
    {
        var trimmedReason = reason.Trim();
        return status switch
        {
            ProcessStepRunStatus.Completed => string.IsNullOrWhiteSpace(trimmedReason)
                ? $"AgentFramework run '{runTitle}' completed step '{stepTitle}' with an explicit governed outcome."
                : $"AgentFramework run '{runTitle}' completed step '{stepTitle}': {trimmedReason}",
            ProcessStepRunStatus.Blocked => string.IsNullOrWhiteSpace(trimmedReason)
                ? $"AgentFramework run '{runTitle}' blocked step '{stepTitle}' pending remediation."
                : $"AgentFramework run '{runTitle}' blocked step '{stepTitle}': {trimmedReason}",
            ProcessStepRunStatus.WaitingApproval => string.IsNullOrWhiteSpace(trimmedReason)
                ? $"AgentFramework run '{runTitle}' is waiting on approval before '{stepTitle}' can continue."
                : $"AgentFramework run '{runTitle}' is waiting on approval before '{stepTitle}' can continue: {trimmedReason}",
            ProcessStepRunStatus.Refused => string.IsNullOrWhiteSpace(trimmedReason)
                ? $"AgentFramework run '{runTitle}' refused step '{stepTitle}'."
                : $"AgentFramework run '{runTitle}' refused step '{stepTitle}': {trimmedReason}",
            _ => string.IsNullOrWhiteSpace(trimmedReason)
                ? $"AgentFramework run '{runTitle}' failed step '{stepTitle}'."
                : $"AgentFramework run '{runTitle}' failed step '{stepTitle}': {trimmedReason}"
        };
    }

    internal static ProcessStepRunStatus MapStatus(ProcessStepOutcomeStatus status)
    {
        return status switch
        {
            ProcessStepOutcomeStatus.Completed => ProcessStepRunStatus.Completed,
            ProcessStepOutcomeStatus.Blocked => ProcessStepRunStatus.Blocked,
            ProcessStepOutcomeStatus.Failed => ProcessStepRunStatus.Failed,
            ProcessStepOutcomeStatus.WaitingApproval => ProcessStepRunStatus.WaitingApproval,
            ProcessStepOutcomeStatus.Refused => ProcessStepRunStatus.Refused,
            _ => ProcessStepRunStatus.Failed
        };
    }

    private static string CollapseWhitespace(string? text)
    {
        return string.IsNullOrWhiteSpace(text)
            ? string.Empty
            : string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}

internal readonly record struct ProcessDeclaredStepOutcomeFacts(
    ProcessStepRunStatus Status,
    string Reason,
    string BranchOutcomeKey,
    string BranchOutcomeTitle);
