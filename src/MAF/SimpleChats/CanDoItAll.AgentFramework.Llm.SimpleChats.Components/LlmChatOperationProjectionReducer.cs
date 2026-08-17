using CanDoItAll.AgentFramework.Llm.SimpleChats.Operations;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Components;

public sealed record LlmChatOperationProjectionState(
    Guid OperationId,
    long Cursor,
    LlmChatOperationStatus Status,
    int? ActiveAttemptOrdinal,
    string TransientAssistantText,
    string Model,
    bool IsTerminal,
    bool RequiresAuthoritativeRefresh,
    LlmChatUiFailure? Failure)
{
    public static LlmChatOperationProjectionState Initial(Guid operationId)
    {
        if (operationId == Guid.Empty)
        {
            throw new ArgumentException("An operation projection requires an operation id.", nameof(operationId));
        }

        return new(
            operationId,
            0,
            LlmChatOperationStatus.Pending,
            null,
            string.Empty,
            string.Empty,
            false,
            false,
            null);
    }
}

public interface ILlmChatOperationProjectionReducer
{
    LlmChatOperationProjectionState Reduce(
        LlmChatOperationProjectionState state,
        LlmChatUiOperationEventPage page);
}

public sealed class LlmChatOperationProjectionReducer : ILlmChatOperationProjectionReducer
{
    public LlmChatOperationProjectionState Reduce(
        LlmChatOperationProjectionState state,
        LlmChatUiOperationEventPage page)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(page);
        if (state.OperationId != page.OperationId)
        {
            throw new ArgumentException("The event page belongs to another operation.", nameof(page));
        }

        if (HasCursorGap(state.Cursor, page))
        {
            return RequireRefresh(state, page);
        }

        var next = state;
        foreach (var operationEvent in page.Events.OrderBy(item => item.Sequence))
        {
            if (operationEvent.OperationId.Value != state.OperationId)
            {
                throw new ArgumentException("The event page contains an event for another operation.", nameof(page));
            }

            if (operationEvent.Sequence <= next.Cursor)
            {
                continue;
            }

            if (operationEvent.Sequence != next.Cursor + 1)
            {
                return RequireRefresh(next, page);
            }

            next = Apply(next, operationEvent) with { Cursor = operationEvent.Sequence };
        }

        var failure = string.IsNullOrWhiteSpace(page.FailureCode)
            ? next.Failure
            : LlmChatUiResultMapper.FromFailureCode(page.FailureCode);
        var requiresRefresh = next.RequiresAuthoritativeRefresh ||
                              page.IsTerminal ||
                              page.Status == LlmChatOperationStatus.RecoveryRequired;
        return next with
        {
            Cursor = Math.Max(next.Cursor, page.LatestSequence),
            Status = page.Status,
            IsTerminal = page.IsTerminal,
            RequiresAuthoritativeRefresh = requiresRefresh,
            Failure = failure
        };
    }

    private static bool HasCursorGap(long cursor, LlmChatUiOperationEventPage page)
        => cursor > page.LatestSequence ||
           page.EarliestRetainedSequence is { } earliest && cursor < earliest - 1;

    private static LlmChatOperationProjectionState Apply(
        LlmChatOperationProjectionState state,
        LlmChatOperationEvent operationEvent)
        => operationEvent switch
        {
            LlmChatOperationAttemptStartedEvent started => state with
            {
                ActiveAttemptOrdinal = started.AttemptOrdinal,
                TransientAssistantText = string.Empty,
                Model = started.Model,
                Failure = null
            },
            LlmChatOperationTextDeltaEvent delta when delta.AttemptOrdinal == state.ActiveAttemptOrdinal => state with
            {
                TransientAssistantText = state.TransientAssistantText + delta.Text
            },
            LlmChatOperationTextDeltaEvent => state with
            {
                ActiveAttemptOrdinal = null,
                TransientAssistantText = string.Empty,
                RequiresAuthoritativeRefresh = true
            },
            LlmChatOperationAttemptFinishedEvent finished => state with
            {
                ActiveAttemptOrdinal = null,
                Failure = string.IsNullOrWhiteSpace(finished.FailureCode)
                    ? null
                    : LlmChatUiResultMapper.FromFailureCode(finished.FailureCode)
            },
            LlmChatOperationStateChangedEvent changed => state with
            {
                Status = changed.Status,
                IsTerminal = changed.Status is
                    LlmChatOperationStatus.Succeeded or
                    LlmChatOperationStatus.Failed or
                    LlmChatOperationStatus.Cancelled,
                RequiresAuthoritativeRefresh = state.RequiresAuthoritativeRefresh ||
                    changed.Status is
                        LlmChatOperationStatus.Succeeded or
                        LlmChatOperationStatus.Failed or
                        LlmChatOperationStatus.Cancelled or
                        LlmChatOperationStatus.RecoveryRequired,
                Failure = string.IsNullOrWhiteSpace(changed.FailureCode)
                    ? null
                    : LlmChatUiResultMapper.FromFailureCode(changed.FailureCode)
            },
            _ => throw new ArgumentOutOfRangeException(nameof(operationEvent), operationEvent.Kind, "Unknown operation event.")
        };

    private static LlmChatOperationProjectionState RequireRefresh(
        LlmChatOperationProjectionState state,
        LlmChatUiOperationEventPage page)
        => state with
        {
            Cursor = page.LatestSequence,
            Status = page.Status,
            ActiveAttemptOrdinal = null,
            TransientAssistantText = string.Empty,
            IsTerminal = page.IsTerminal,
            RequiresAuthoritativeRefresh = true,
            Failure = string.IsNullOrWhiteSpace(page.FailureCode)
                ? null
                : LlmChatUiResultMapper.FromFailureCode(page.FailureCode)
        };
}
