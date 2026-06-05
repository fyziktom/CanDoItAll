using CanDoItAll.AgentFramework.Core;
using System.Text.RegularExpressions;

namespace CanDoItAll.Modules.Processes;

internal sealed partial class ProcessRunAutomationDispatchService
{
    private static class ProcessRecoverableProviderFailureRules
    {
        public static bool TryResolve(
            ProcessAutomationExecutionRunDetail detail,
            string? responseText,
            out string failureSummary)
        {
            failureSummary = string.Empty;
            if (detail.Run.State == ProcessAutomationExecutionState.Completed &&
                detail.Run.Outcome == ProcessAutomationRunOutcome.Succeeded &&
                TryReadProcessStepOutcome(responseText, out _, out _))
            {
                return false;
            }

            var candidateTexts = new[]
            {
                responseText,
                detail.ChatSession?.Messages.LastOrDefault(item => item.Role == ProcessAutomationChatMessageRole.Assistant)?.Content,
                ResolveLatestAssistantErrorSummary(detail.Run.SerializedSessionStateJson),
                ResolveLatestAssistantResponseText(detail.Run.SerializedSessionStateJson),
                detail.Run.ResultSummary
            };

            foreach (var candidateText in candidateTexts)
            {
                if (TryMapSummary(candidateText, out failureSummary))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool TryMapSummary(
            string? candidateText,
            out string failureSummary)
        {
            failureSummary = string.Empty;
            if (string.IsNullOrWhiteSpace(candidateText))
            {
                return false;
            }

            var normalizedText = Regex.Replace(
                    candidateText,
                    @"\s+",
                    " ",
                    RegexOptions.CultureInvariant)
                .Trim();
            if (string.IsNullOrWhiteSpace(normalizedText))
            {
                return false;
            }

            if (normalizedText.Contains("insufficient_quota", StringComparison.OrdinalIgnoreCase) ||
                normalizedText.Contains("exceeded your current quota", StringComparison.OrdinalIgnoreCase))
            {
                failureSummary = "Provider quota was exhausted before the agent returned a usable response.";
                return true;
            }

            if (normalizedText.Contains("rate_limit", StringComparison.OrdinalIgnoreCase) ||
                normalizedText.Contains("rate limit", StringComparison.OrdinalIgnoreCase))
            {
                failureSummary = "The assigned provider hit a rate limit before the agent returned a usable response.";
                return true;
            }

            var missingProviderCredential =
                ((normalizedText.Contains("Environment variable '", StringComparison.OrdinalIgnoreCase) &&
                  normalizedText.Contains("' is not set.", StringComparison.OrdinalIgnoreCase) &&
                  !normalizedText.Contains("memory capability", StringComparison.OrdinalIgnoreCase)) ||
                 normalizedText.Contains("No API key environment variable is configured for this provider", StringComparison.OrdinalIgnoreCase) ||
                 normalizedText.Contains("No secret record or API key environment variable is configured for this provider", StringComparison.OrdinalIgnoreCase) ||
                 (normalizedText.Contains("Secret record '", StringComparison.OrdinalIgnoreCase) &&
                  (normalizedText.Contains("was not found.", StringComparison.OrdinalIgnoreCase) ||
                   normalizedText.Contains("could not be decrypted", StringComparison.OrdinalIgnoreCase))));
            if (missingProviderCredential)
            {
                failureSummary = "The assigned provider did not have usable credentials in the current environment.";
                return true;
            }

            if (normalizedText.Contains("The provider completed without returning text.", StringComparison.OrdinalIgnoreCase) ||
                normalizedText.Contains("provider completed without returning text", StringComparison.OrdinalIgnoreCase) ||
                normalizedText.Contains("provider returned an empty response", StringComparison.OrdinalIgnoreCase))
            {
                failureSummary = "The assigned provider completed without returning text.";
                return true;
            }

            if (normalizedText.Contains("ResponseEnded", StringComparison.OrdinalIgnoreCase) ||
                normalizedText.Contains("response ended prematurely", StringComparison.OrdinalIgnoreCase))
            {
                failureSummary = "The assigned provider response ended before the agent produced a usable response.";
                return true;
            }

            if ((normalizedText.Contains("cannot enforce structured output contract", StringComparison.OrdinalIgnoreCase) ||
                 normalizedText.Contains("cannot enforce structured-output contract", StringComparison.OrdinalIgnoreCase)) &&
                (normalizedText.Contains("Choose a structured-output capable", StringComparison.OrdinalIgnoreCase) ||
                 normalizedText.Contains("structured-output capable OpenAI", StringComparison.OrdinalIgnoreCase)))
            {
                failureSummary = "The assigned provider cannot enforce the required structured output contract.";
                return true;
            }

            if (Regex.IsMatch(
                    normalizedText,
                    @"Response status code does not indicate success:\s*5\d\d\b",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) ||
                normalizedText.Contains("Internal Server Error", StringComparison.OrdinalIgnoreCase) ||
                normalizedText.Contains("Bad Gateway", StringComparison.OrdinalIgnoreCase) ||
                normalizedText.Contains("Service Unavailable", StringComparison.OrdinalIgnoreCase) ||
                normalizedText.Contains("Gateway Timeout", StringComparison.OrdinalIgnoreCase))
            {
                failureSummary = "The assigned provider returned an upstream server error before the agent produced a usable response.";
                return true;
            }

            return false;
        }
    }
}
