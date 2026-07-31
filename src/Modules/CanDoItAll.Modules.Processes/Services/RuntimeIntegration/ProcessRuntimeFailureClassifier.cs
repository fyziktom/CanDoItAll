using static CanDoItAll.Modules.Processes.ProcessAgentRightsDiagnosticPolicy;
using static CanDoItAll.Modules.Processes.ProcessCompletionText;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessRuntimeFailureClassifier
{
    internal static bool LooksLikeAgentOutputContractFailure(Exception exception)
    {
        var text = exception.ToString();
        return ContainsAny(
            text,
            "submit_process_step_outcome",
            "Required finalizer tool",
            "process_step_outcome_result",
            "ProcessStepOutcomeResult",
            "process.step_outcome",
            "agent.finalizer",
            "agent.output");
    }

    internal static bool LooksLikeTransientAgentExecutionFailure(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var hasTransientMarker = ContainsAny(
            text,
            "Service request failed",
            "Status: 408",
            "Status: 429",
            "Status: 500",
            "Status: 502",
            "Status: 503",
            "Status: 504",
            "Status: 520",
            "Status: 529",
            "temporarily unavailable",
            "temporary failure",
            "transient",
            "rate limit",
            "timeout",
            "timed out",
            "connection reset",
            "connection refused",
            "transport error");
        if (!hasTransientMarker)
        {
            return false;
        }

        return !LooksLikeRightsOrToolBoundary(text) ||
               LooksLikeProviderRuntimeTransientFailure(text);
    }

    internal static bool LooksLikeProviderRuntimeTransientFailure(string text)
    {
        return ContainsAny(
            text,
            "provider detail",
            "provider runtime",
            "service request failed",
            "initialization timed out",
            "initialisation timed out",
            "runtime initialization timed out",
            "runtime initialisation timed out");
    }

    internal static string LimitDiagnosticText(string text, int maxLength = 800)
    {
        var normalized = text.ReplaceLineEndings(" ").Trim();
        return normalized.Length <= maxLength
            ? normalized
            : normalized[..maxLength] + "...";
    }
}
