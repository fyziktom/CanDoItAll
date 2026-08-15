using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.Modules.LlmChats.Common;

namespace CanDoItAll.Modules.LlmChats.Operations;

public enum LlmChatOperationEventKind
{
    StateChanged,
    AttemptStarted,
    AttemptFinished,
    TextDelta
}

public abstract record LlmChatOperationEvent
{
    protected LlmChatOperationEvent(
        LlmChatOperationId operationId,
        long sequence,
        DateTimeOffset occurredAtUtc)
    {
        if (operationId.Value == Guid.Empty)
        {
            throw new ArgumentException("An operation event requires an operation id.", nameof(operationId));
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(sequence, 1);
        OperationId = operationId;
        Sequence = sequence;
        OccurredAtUtc = occurredAtUtc;
    }

    public LlmChatOperationId OperationId { get; }

    public long Sequence { get; }

    public DateTimeOffset OccurredAtUtc { get; }

    public abstract LlmChatOperationEventKind Kind { get; }
}

public sealed record LlmChatOperationStateChangedEvent : LlmChatOperationEvent
{
    public LlmChatOperationStateChangedEvent(
        LlmChatOperationId operationId,
        long sequence,
        LlmChatOperationStatus status,
        DateTimeOffset occurredAtUtc,
        string failureCode = "",
        string model = "",
        LlmUsage? usage = null) : base(operationId, sequence, occurredAtUtc)
    {
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown operation status.");
        }

        if (failureCode.Length > MaximumFailureCodeLength)
        {
            throw new ArgumentException("An event failure code is too long.", nameof(failureCode));
        }

        if (model.Length > MaximumModelLength)
        {
            throw new ArgumentException("An event model is too long.", nameof(model));
        }

        var requiresFailureCode = status is
            LlmChatOperationStatus.Failed or
            LlmChatOperationStatus.Cancelled or
            LlmChatOperationStatus.RecoveryRequired;
        var redactedFailureCode = RedactFailureCode(failureCode);
        if (requiresFailureCode != !string.IsNullOrWhiteSpace(redactedFailureCode))
        {
            throw new ArgumentException("The terminal event failure code does not match its status.", nameof(failureCode));
        }

        if (status == LlmChatOperationStatus.Succeeded &&
            (usage is null || string.IsNullOrWhiteSpace(model)))
        {
            throw new ArgumentException("A succeeded event requires model and usage evidence.");
        }

        if (status != LlmChatOperationStatus.Succeeded && !string.IsNullOrEmpty(model))
        {
            throw new ArgumentException("Only a succeeded state event can carry model evidence.");
        }

        var carriesTerminalUsage = status is
            LlmChatOperationStatus.Succeeded or
            LlmChatOperationStatus.Failed or
            LlmChatOperationStatus.Cancelled;
        if (carriesTerminalUsage != (usage is not null))
        {
            throw new ArgumentException("Terminal state events require usage evidence and nonterminal events forbid it.");
        }

        Status = status;
        FailureCode = redactedFailureCode;
        Model = model;
        Usage = usage;
    }

    public const int MaximumFailureCodeLength = 200;

    public const int MaximumModelLength = 500;

    public override LlmChatOperationEventKind Kind => LlmChatOperationEventKind.StateChanged;

    public LlmChatOperationStatus Status { get; }

    public string FailureCode { get; }

    public string Model { get; }

    public LlmUsage? Usage { get; }

    public bool IsOutputIncomplete => Status is
        LlmChatOperationStatus.Failed or
        LlmChatOperationStatus.Cancelled or
        LlmChatOperationStatus.RecoveryRequired;

    internal static string RedactFailureCode(string failureCode)
    {
        var normalized = failureCode.Trim();
        return normalized.Length == 0 || normalized.StartsWith(LlmChatErrorCodes.Prefix, StringComparison.Ordinal)
            ? normalized
            : LlmChatErrorCodes.StorageCorrupted;
    }
}

public sealed record LlmChatOperationAttemptStartedEvent : LlmChatOperationEvent
{
    public LlmChatOperationAttemptStartedEvent(
        LlmChatOperationId operationId,
        long sequence,
        int attemptOrdinal,
        string model,
        LlmStreamingDeliveryMode deliveryMode,
        DateTimeOffset occurredAtUtc) : base(operationId, sequence, occurredAtUtc)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(attemptOrdinal, 1);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        if (model.Length > LlmChatOperationStateChangedEvent.MaximumModelLength)
        {
            throw new ArgumentException("An event model is too long.", nameof(model));
        }

        if (!Enum.IsDefined(deliveryMode))
        {
            throw new ArgumentOutOfRangeException(nameof(deliveryMode), deliveryMode, "Unknown delivery mode.");
        }

        AttemptOrdinal = attemptOrdinal;
        Model = model;
        DeliveryMode = deliveryMode;
    }

    public override LlmChatOperationEventKind Kind => LlmChatOperationEventKind.AttemptStarted;

    public int AttemptOrdinal { get; }

    public string Model { get; }

    public LlmStreamingDeliveryMode DeliveryMode { get; }
}

public sealed record LlmChatOperationAttemptFinishedEvent : LlmChatOperationEvent
{
    public LlmChatOperationAttemptFinishedEvent(
        LlmChatOperationId operationId,
        long sequence,
        int attemptOrdinal,
        LlmChatInvocationOutcome outcome,
        LlmUsage usage,
        DateTimeOffset occurredAtUtc,
        string failureCode = "") : base(operationId, sequence, occurredAtUtc)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(attemptOrdinal, 1);
        if (!Enum.IsDefined(outcome))
        {
            throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Unknown invocation outcome.");
        }

        ArgumentNullException.ThrowIfNull(usage);
        if (failureCode.Length > LlmChatOperationStateChangedEvent.MaximumFailureCodeLength)
        {
            throw new ArgumentException("An event failure code is too long.", nameof(failureCode));
        }

        var redactedFailureCode = LlmChatOperationStateChangedEvent.RedactFailureCode(failureCode);
        if ((outcome == LlmChatInvocationOutcome.Succeeded) == !string.IsNullOrWhiteSpace(redactedFailureCode))
        {
            throw new ArgumentException("The attempt failure code does not match its outcome.", nameof(failureCode));
        }

        AttemptOrdinal = attemptOrdinal;
        Outcome = outcome;
        Usage = usage;
        FailureCode = redactedFailureCode;
    }

    public override LlmChatOperationEventKind Kind => LlmChatOperationEventKind.AttemptFinished;

    public int AttemptOrdinal { get; }

    public LlmChatInvocationOutcome Outcome { get; }

    public LlmUsage Usage { get; }

    public string FailureCode { get; }
}

public sealed record LlmChatOperationTextDeltaEvent : LlmChatOperationEvent
{
    public LlmChatOperationTextDeltaEvent(
        LlmChatOperationId operationId,
        long sequence,
        int attemptOrdinal,
        string text,
        DateTimeOffset occurredAtUtc) : base(operationId, sequence, occurredAtUtc)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(attemptOrdinal, 1);
        ArgumentException.ThrowIfNullOrEmpty(text);
        AttemptOrdinal = attemptOrdinal;
        Text = text;
    }

    public override LlmChatOperationEventKind Kind => LlmChatOperationEventKind.TextDelta;

    public int AttemptOrdinal { get; }

    public string Text { get; }
}
