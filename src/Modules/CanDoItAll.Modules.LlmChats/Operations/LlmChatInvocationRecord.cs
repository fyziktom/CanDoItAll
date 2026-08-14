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
        string correlationId)
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

        ArgumentNullException.ThrowIfNull(usage);
        ArgumentOutOfRangeException.ThrowIfLessThan(ordinal, 1);
        if (completedAtUtc < startedAtUtc)
        {
            throw new ArgumentException("Completed time cannot precede started time.", nameof(completedAtUtc));
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
        Ordinal = ordinal;
        Usage = usage;
        Outcome = outcome;
        FailureCode = failureCode?.Trim() ?? string.Empty;
        StartedAtUtc = startedAtUtc;
        CompletedAtUtc = completedAtUtc;
        CorrelationId = correlationId?.Trim() ?? string.Empty;
    }

    public LlmChatOperationId OperationId { get; }

    public Guid ProviderProfileId { get; }

    public ProviderKind ProviderKind { get; }

    public string ProviderName { get; }

    public string Model { get; }

    public AgentReasoningEffortLevel? RequestedThinkingEffort { get; }

    public AgentReasoningEffortLevel? EffectiveThinkingEffort { get; }

    public int Ordinal { get; }

    public LlmUsage Usage { get; }

    public LlmChatInvocationOutcome Outcome { get; }

    public string FailureCode { get; }

    public DateTimeOffset StartedAtUtc { get; }

    public DateTimeOffset CompletedAtUtc { get; }

    public string CorrelationId { get; }
}
