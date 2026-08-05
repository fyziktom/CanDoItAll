namespace CanDoItAll.AgentFramework.Core;

internal enum AgentExecutionGovernanceFailureKind
{
    StructuredOutputContract,
    StructuredOutputValidation,
    FinalizerValidation,
    FinalizerSequenceValidation
}

internal sealed class AgentExecutionGovernanceException : InvalidOperationException
{
    private const int MaximumDisplayMessageLength = 2_048;
    private const string DefaultDisplayMessage =
        "The agent run failed an execution governance check.";

    public AgentExecutionGovernanceException(
        AgentExecutionGovernanceFailureKind failureKind,
        string displayMessage)
        : base(SanitizeDisplayMessage(displayMessage))
    {
        FailureKind = failureKind;
        SanitizedDisplayMessage = Message;
    }

    public AgentExecutionGovernanceFailureKind FailureKind { get; }

    public string SanitizedDisplayMessage { get; }

    private static string SanitizeDisplayMessage(string? displayMessage)
    {
        var sanitized = WorkflowExecutorRedaction
            .RedactText(displayMessage)
            .Trim()
            .ReplaceLineEndings(" ");
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            return DefaultDisplayMessage;
        }

        return sanitized.Length <= MaximumDisplayMessageLength
            ? sanitized
            : $"{sanitized[..(MaximumDisplayMessageLength - 3)]}...";
    }
}
