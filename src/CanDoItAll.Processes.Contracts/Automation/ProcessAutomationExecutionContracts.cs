namespace CanDoItAll.Processes.Contracts;

public enum ProcessAutomationFinalizerMode
{
    Required
}

public enum ProcessAutomationStructuredOutputKind
{
    None,
    ProcessStepOutcomeResult
}

public sealed record ProcessAutomationExecutionRequest(
    Guid AgentId,
    string Prompt,
    ProcessAutomationInvocationSource Source,
    ProcessAutomationInvocationPolicy Policy,
    bool AutoApprovePendingToolCalls,
    ProcessAutomationStructuredOutputKind StructuredOutputKind);

public sealed record ProcessAutomationInvocationSource(
    string SourceKind,
    string SourceId,
    string CorrelationId,
    string CausationId,
    string RequestedBy,
    string RequestedByKind,
    string MetadataJson,
    string ProcessRunId,
    string ProcessStepId,
    string SchedulerRunId = "",
    string MessageId = "");

public sealed record ProcessAutomationInvocationPolicy(
    ProcessAutomationFinalizerMode? FinalizerMode,
    int? MaxStructuredOutputRepairAttempts,
    bool RequireStructuredOutputValidation);
