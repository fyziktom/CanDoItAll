using CanDoItAll.Modules.LlmChats.Common;

namespace CanDoItAll.Modules.LlmChats.Operations;

public enum LlmChatOperationDecisionKind
{
    NoChange,
    CommitSucceeded,
    MarkSucceeded,
    CompensateAndFail,
    CompensateAndCancel,
    MarkFailed,
    MarkCancelled,
    RequireRecovery
}

public sealed record LlmChatOperationDurableEvidence(
    LlmChatOperation Operation,
    bool HasExactActiveTurn,
    bool HasAssistant,
    DateTimeOffset? AssistantCreatedAtUtc,
    LlmChatInvocationOutcome? LastInvocationOutcome,
    string LastInvocationFailureCode,
    bool HasPendingAssistantResult = false);

public sealed record LlmChatOperationDecision(
    LlmChatOperationDecisionKind Kind,
    string FailureCode = "");

public static class LlmChatOperationReducer
{
    public static LlmChatOperationDecision Reduce(LlmChatOperationDurableEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        var operation = evidence.Operation;
        if (operation.IsTerminal)
        {
            return new(LlmChatOperationDecisionKind.NoChange);
        }

        if (evidence.HasAssistant)
        {
            if (evidence.HasExactActiveTurn || CancellationPrecedesAssistant(evidence))
            {
                return Recovery();
            }

            return new(LlmChatOperationDecisionKind.MarkSucceeded);
        }

        if (evidence.HasExactActiveTurn)
        {
            if (operation.CancellationGeneration > 0)
            {
                return evidence.LastInvocationOutcome is not null ||
                       operation.ProviderDispatchStartedAtUtc is null
                    ? new(LlmChatOperationDecisionKind.CompensateAndCancel, LlmChatErrorCodes.Cancelled)
                    : Recovery();
            }

            return evidence.LastInvocationOutcome switch
            {
                LlmChatInvocationOutcome.Succeeded when evidence.HasPendingAssistantResult =>
                    new(LlmChatOperationDecisionKind.CommitSucceeded),
                LlmChatInvocationOutcome.Succeeded => Recovery(),
                LlmChatInvocationOutcome.Failed => new(
                    LlmChatOperationDecisionKind.CompensateAndFail,
                    NormalizeFailureCode(evidence.LastInvocationFailureCode)),
                LlmChatInvocationOutcome.Cancelled => new(
                    LlmChatOperationDecisionKind.CompensateAndCancel,
                    LlmChatErrorCodes.Cancelled),
                _ when operation.ProviderDispatchStartedAtUtc is not null => Recovery(),
                _ => new(
                    LlmChatOperationDecisionKind.CompensateAndFail,
                    LlmChatErrorCodes.ProviderUnavailable)
            };
        }

        if (operation.CancellationGeneration > 0 &&
            (operation.ProviderDispatchStartedAtUtc is null ||
             evidence.LastInvocationOutcome == LlmChatInvocationOutcome.Cancelled))
        {
            return new(LlmChatOperationDecisionKind.MarkCancelled, LlmChatErrorCodes.Cancelled);
        }

        return evidence.LastInvocationOutcome switch
        {
            LlmChatInvocationOutcome.Failed => new(
                LlmChatOperationDecisionKind.MarkFailed,
                NormalizeFailureCode(evidence.LastInvocationFailureCode)),
            LlmChatInvocationOutcome.Cancelled => new(
                LlmChatOperationDecisionKind.MarkCancelled,
                LlmChatErrorCodes.Cancelled),
            LlmChatInvocationOutcome.Succeeded => Recovery(),
            _ when operation.ProviderDispatchStartedAtUtc is not null => Recovery(),
            _ => new(LlmChatOperationDecisionKind.MarkFailed, LlmChatErrorCodes.ProviderUnavailable)
        };
    }

    private static bool CancellationPrecedesAssistant(LlmChatOperationDurableEvidence evidence)
        => evidence.Operation.CancellationGeneration > 0 &&
           evidence.Operation.CancellationRequestedAtUtc is { } requestedAtUtc &&
           evidence.AssistantCreatedAtUtc is { } assistantCreatedAtUtc &&
           requestedAtUtc <= assistantCreatedAtUtc;

    private static LlmChatOperationDecision Recovery()
        => new(LlmChatOperationDecisionKind.RequireRecovery, LlmChatErrorCodes.OperationRecoveryRequired);

    private static string NormalizeFailureCode(string failureCode)
        => string.IsNullOrWhiteSpace(failureCode)
            ? LlmChatErrorCodes.ProviderUnavailable
            : failureCode.Trim();
}
