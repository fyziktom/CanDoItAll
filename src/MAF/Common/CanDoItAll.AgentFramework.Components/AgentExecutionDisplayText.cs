using CanDoItAll.AgentFramework.Core;

namespace CanDoItAll.AgentFramework.Components;

internal static class AgentExecutionDisplayText {
    public static string Format(string? value, int maximumLength = 4096) {
        var redacted = WorkflowExecutorRedaction.RedactText(value);
        return redacted.Length <= maximumLength ? redacted : redacted[..(maximumLength - 1)] + "…";
    }
}
