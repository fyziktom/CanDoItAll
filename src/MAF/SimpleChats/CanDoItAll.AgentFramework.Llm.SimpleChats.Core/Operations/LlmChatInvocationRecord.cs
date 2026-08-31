using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Definitions;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Operations;

public enum LlmChatInvocationOutcome
{
    Succeeded,
    Failed,
    Cancelled
}

public enum LlmChatInvocationUsageEvidenceStatus
{
    LegacyKnownTokens = 0,
    Observed = 1,
    MissingAfterProviderActivity = 2,
    UsageUnavailable = 3
}

public enum LlmChatInvocationPricingEvidenceStatus
{
    Unpriced = 0,
    ProviderReported = 1,
    CalculatedAtExecution = 2
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
        string finishReason = "",
        LlmChatInvocationUsageEvidenceStatus usageStatus = LlmChatInvocationUsageEvidenceStatus.LegacyKnownTokens,
        LlmChatInvocationPricingEvidenceStatus pricingStatus = LlmChatInvocationPricingEvidenceStatus.Unpriced,
        decimal? providerCostUsd = null,
        decimal? calculatedCostUsd = null,
        string pricingProfileHash = "",
        string pricingVersion = "")
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

        if (!Enum.IsDefined(usageStatus))
        {
            throw new ArgumentOutOfRangeException(nameof(usageStatus), usageStatus, "Unknown usage evidence status.");
        }

        if (!Enum.IsDefined(pricingStatus))
        {
            throw new ArgumentOutOfRangeException(nameof(pricingStatus), pricingStatus, "Unknown pricing evidence status.");
        }

        ArgumentNullException.ThrowIfNull(usage);
        if (providerCostUsd is < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(providerCostUsd));
        }

        if (calculatedCostUsd is < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(calculatedCostUsd));
        }

        if (pricingStatus == LlmChatInvocationPricingEvidenceStatus.ProviderReported && providerCostUsd is null ||
            pricingStatus == LlmChatInvocationPricingEvidenceStatus.CalculatedAtExecution && calculatedCostUsd is null ||
            pricingStatus == LlmChatInvocationPricingEvidenceStatus.Unpriced &&
            (providerCostUsd is not null || calculatedCostUsd is not null))
        {
            throw new ArgumentException("The pricing status does not match the captured costs.", nameof(pricingStatus));
        }

        var normalizedPricingHash = pricingProfileHash?.Trim() ?? string.Empty;
        var normalizedPricingVersion = pricingVersion?.Trim() ?? string.Empty;
        if (normalizedPricingHash.Length is not 0 and not ProviderPricingSnapshot.ProfileHashLength)
        {
            throw new ArgumentException("The pricing profile hash is invalid.", nameof(pricingProfileHash));
        }

        if (pricingStatus != LlmChatInvocationPricingEvidenceStatus.Unpriced &&
            (normalizedPricingHash.Length == 0 || normalizedPricingVersion.Length == 0))
        {
            throw new ArgumentException("Priced invocation evidence requires a pricing hash and version.", nameof(pricingStatus));
        }
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
        UsageStatus = usageStatus;
        PricingStatus = pricingStatus;
        ProviderCostUsd = providerCostUsd;
        CalculatedCostUsd = calculatedCostUsd;
        PricingProfileHash = normalizedPricingHash;
        PricingVersion = normalizedPricingVersion;
    }

    public IReadOnlyList<CanDoItAll.AgentFramework.ProviderHistory.HistoryEntry> HistoryAttempts { get; init; } = [];

    public const int MaximumFinishReasonLength = 100;
    public const int MaximumPricingVersionLength = 64;

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

    public LlmChatInvocationUsageEvidenceStatus UsageStatus { get; }

    public LlmChatInvocationPricingEvidenceStatus PricingStatus { get; }

    public decimal? ProviderCostUsd { get; }

    public decimal? CalculatedCostUsd { get; }

    public string PricingProfileHash { get; }

    public string PricingVersion { get; }
}
