using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.LlmChats.Common;
using CanDoItAll.Modules.LlmChats.Definitions;

namespace CanDoItAll.Modules.LlmChats.Operations;

public enum LlmChatInvocationOutcome
{
    Succeeded,
    Failed,
    Cancelled
}

public sealed record LlmChatInvocationRecord
{
    public LlmChatInvocationRecord(
        LlmChatOperationId operationId,
        Guid providerProfileId,
        ProviderKind providerKind,
        string providerName,
        string model,
        AgentReasoningEffortLevel? requestedThinkingEffort,
        AgentReasoningEffortLevel? effectiveThinkingEffort,
        int ordinal,
        LlmUsage usage,
        LlmChatInvocationOutcome outcome,
        string failureCode,
        DateTimeOffset startedAtUtc,
        DateTimeOffset completedAtUtc,
        string correlationId,
        LlmStreamingDeliveryMode deliveryMode = LlmStreamingDeliveryMode.CompletedFallback,
        string finishReason = "")
    {
        if (operationId.Value == Guid.Empty)
        {
            throw new ArgumentException("An invocation record requires an operation id.", nameof(operationId));
        }

        ArgumentOutOfRangeException.ThrowIfEqual(providerProfileId, Guid.Empty);
        if (!Enum.IsDefined(providerKind))
        {
            throw new ArgumentOutOfRangeException(nameof(providerKind), providerKind, "Unknown provider kind.");
        }

        if (!Enum.IsDefined(outcome))
        {
            throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Unknown invocation outcome.");
        }

        if (!Enum.IsDefined(deliveryMode))
        {
            throw new ArgumentOutOfRangeException(nameof(deliveryMode), deliveryMode, "Unknown delivery mode.");
        }

        ArgumentNullException.ThrowIfNull(usage);
        ArgumentOutOfRangeException.ThrowIfLessThan(ordinal, 1);
        if (completedAtUtc < startedAtUtc)
        {
            throw new ArgumentException("Completed time cannot precede started time.", nameof(completedAtUtc));
        }

        var normalizedFinishReason = finishReason?.Trim() ?? string.Empty;
        if (outcome == LlmChatInvocationOutcome.Succeeded && normalizedFinishReason.Length == 0)
        {
            normalizedFinishReason = "completed";
        }
        if (normalizedFinishReason.Length > MaximumFinishReasonLength)
        {
            throw new ArgumentException("An invocation finish reason is too long.", nameof(finishReason));
        }

        if ((outcome == LlmChatInvocationOutcome.Succeeded) != (normalizedFinishReason.Length > 0))
        {
            throw new ArgumentException("The finish reason does not match the invocation outcome.", nameof(finishReason));
        }

        var redactedFailureCode = LlmChatOperationStateChangedEvent.RedactFailureCode(failureCode ?? string.Empty);
        if ((outcome == LlmChatInvocationOutcome.Succeeded) == !string.IsNullOrEmpty(redactedFailureCode))
        {
            throw new ArgumentException("The failure code does not match the invocation outcome.", nameof(failureCode));
        }

        OperationId = operationId;
        ProviderProfileId = providerProfileId;
        ProviderKind = providerKind;
        ProviderName = LlmChatDefinitionValidation.NormalizeRequired(
            providerName,
            LlmChatDefinitionValidation.MaximumProviderNameLength,
            nameof(providerName));
        Model = LlmChatDefinitionValidation.NormalizeRequired(
            model,
            LlmChatDefinitionValidation.MaximumModelLength,
            nameof(model));
        RequestedThinkingEffort = requestedThinkingEffort;
        EffectiveThinkingEffort = effectiveThinkingEffort;
        DeliveryMode = deliveryMode;
        FinishReason = normalizedFinishReason;
        Ordinal = ordinal;
        Usage = usage;
        Outcome = outcome;
        FailureCode = redactedFailureCode;
        StartedAtUtc = startedAtUtc;
        CompletedAtUtc = completedAtUtc;
        CorrelationId = correlationId?.Trim() ?? string.Empty;
    }

    public const int MaximumFinishReasonLength = 100;

    public LlmChatOperationId OperationId { get; }

    public Guid ProviderProfileId { get; }

    public ProviderKind ProviderKind { get; }

    public string ProviderName { get; }

    public string Model { get; }

    public AgentReasoningEffortLevel? RequestedThinkingEffort { get; }

    public AgentReasoningEffortLevel? EffectiveThinkingEffort { get; }

    public LlmStreamingDeliveryMode DeliveryMode { get; }

    public string FinishReason { get; }

    public int Ordinal { get; }

    public LlmUsage Usage { get; }

    public LlmChatInvocationOutcome Outcome { get; }

    public string FailureCode { get; }

    public DateTimeOffset StartedAtUtc { get; }

    public DateTimeOffset CompletedAtUtc { get; }

    public string CorrelationId { get; }
}
