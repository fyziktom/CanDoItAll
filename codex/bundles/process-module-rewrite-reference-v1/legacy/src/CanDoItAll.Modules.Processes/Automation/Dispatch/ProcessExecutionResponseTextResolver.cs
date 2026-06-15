using CanDoItAll.AgentFramework.Core;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessExecutionResponseTextResolver
{
    internal static string ResolveRecovered(
        ProcessAutomationExecutionRunDetail detail,
        Func<string?, string?> resolveLatestAssistantResponseText)
    {
        ArgumentNullException.ThrowIfNull(detail);
        ArgumentNullException.ThrowIfNull(resolveLatestAssistantResponseText);

        var assistantMessage = detail.ChatSession?.Messages.LastOrDefault(item => item.Role == ProcessAutomationChatMessageRole.Assistant);
        if (!string.IsNullOrWhiteSpace(assistantMessage?.Content))
        {
            return assistantMessage.Content;
        }

        var serializedResponseText = resolveLatestAssistantResponseText(detail.Run.SerializedSessionStateJson);
        return string.IsNullOrWhiteSpace(serializedResponseText)
            ? detail.Run.ResultSummary
            : serializedResponseText;
    }

    internal static string ResolvePreferred(
        bool requiresGovernedStepOutcome,
        string? responseText,
        ProcessAutomationExecutionRunDetail detail,
        Func<string?, bool> hasDeclaredStepOutcome,
        Func<ProcessAutomationExecutionRunDetail, string> resolveRecovered)
    {
        ArgumentNullException.ThrowIfNull(detail);
        ArgumentNullException.ThrowIfNull(hasDeclaredStepOutcome);
        ArgumentNullException.ThrowIfNull(resolveRecovered);

        var primaryResponse = string.IsNullOrWhiteSpace(responseText)
            ? string.Empty
            : responseText.Trim();
        var recoveredResponse = resolveRecovered(detail).Trim();
        if (string.IsNullOrWhiteSpace(primaryResponse))
        {
            return recoveredResponse;
        }

        if (!requiresGovernedStepOutcome)
        {
            return primaryResponse;
        }

        var primaryHasDeclaredOutcome = hasDeclaredStepOutcome(primaryResponse);
        var resultSummary = string.IsNullOrWhiteSpace(detail.Run.ResultSummary)
            ? string.Empty
            : detail.Run.ResultSummary.Trim();
        var resultSummaryHasDeclaredOutcome = hasDeclaredStepOutcome(resultSummary);
        var recoveredHasDeclaredOutcome = hasDeclaredStepOutcome(recoveredResponse);
        if (resultSummaryHasDeclaredOutcome)
        {
            return resultSummary;
        }

        return !primaryHasDeclaredOutcome && recoveredHasDeclaredOutcome
            ? recoveredResponse
            : primaryResponse;
    }
}
