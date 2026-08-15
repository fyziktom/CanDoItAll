using CanDoItAll.Modules.LlmChats.Common;

namespace CanDoItAll.Modules.LlmChats.Operations;

internal static class LlmChatOperationTransitions
{
    public static LlmChatOperation ClaimExecution(
        LlmChatOperation operation,
        LlmChatExecutionOwnerId ownerId,
        DateTimeOffset claimedAtUtc,
        DateTimeOffset leaseExpiresAtUtc)
    {
        RequireStatus(
            operation,
            LlmChatOperationStatus.Pending,
            LlmChatOperationStatus.Running,
            LlmChatOperationStatus.CancellationRequested);
        if (operation.ProviderDispatchStartedAtUtc is not null)
        {
            throw new InvalidOperationException("An operation with provider-dispatch evidence cannot be reclaimed.");
        }

        if (leaseExpiresAtUtc <= claimedAtUtc)
        {
            throw new ArgumentOutOfRangeException(
                nameof(leaseExpiresAtUtc),
                "An execution lease must expire after it is claimed.");
        }

        return Advance(
            operation,
            operation.Status == LlmChatOperationStatus.CancellationRequested
                ? operation.Status
                : LlmChatOperationStatus.Running) with
        {
            ExecutionOwnerId = ownerId,
            ExecutionEpoch = checked(operation.ExecutionEpoch + 1),
            ClaimedAtUtc = claimedAtUtc,
            HeartbeatAtUtc = claimedAtUtc,
            LeaseExpiresAtUtc = leaseExpiresAtUtc,
            DispatchPhase = LlmChatDispatchPhase.Claimed
        };
    }

    public static LlmChatOperation MarkTurnAdmitted(
        LlmChatOperation operation,
        DateTimeOffset admittedAtUtc)
    {
        if (operation.TurnAdmittedAtUtc is not null)
        {
            return operation;
        }

        RequireStatus(
            operation,
            LlmChatOperationStatus.Pending,
            LlmChatOperationStatus.Running);
        return Advance(operation, operation.Status) with { TurnAdmittedAtUtc = admittedAtUtc };
    }

    public static LlmChatOperation MarkProviderDispatchStarted(
        LlmChatOperation operation,
        DateTimeOffset startedAtUtc)
    {
        if (operation.ProviderDispatchStartedAtUtc is not null)
        {
            return operation;
        }

        RequireStatus(operation, LlmChatOperationStatus.Running);
        if (operation.TurnAdmittedAtUtc is not { } admittedAtUtc || startedAtUtc < admittedAtUtc)
        {
            throw new InvalidOperationException("Provider dispatch cannot start before turn admission evidence exists.");
        }

        return Advance(operation, operation.Status) with
        {
            ProviderDispatchStartedAtUtc = startedAtUtc,
            DispatchPhase = LlmChatDispatchPhase.ProviderDispatchStarted
        };
    }

    public static LlmChatOperation MarkProviderDispatchReturned(
        LlmChatOperation operation,
        DateTimeOffset returnedAtUtc)
    {
        if (operation.ProviderDispatchReturnedAtUtc is not null)
        {
            return operation;
        }

        RequireStatus(
            operation,
            LlmChatOperationStatus.Running,
            LlmChatOperationStatus.CancellationRequested);
        if (operation.ProviderDispatchStartedAtUtc is not { } startedAtUtc || returnedAtUtc < startedAtUtc)
        {
            throw new InvalidOperationException("Provider dispatch return evidence requires an earlier dispatch start.");
        }

        return Advance(operation, operation.Status) with
        {
            ProviderDispatchReturnedAtUtc = returnedAtUtc,
            DispatchPhase = LlmChatDispatchPhase.ProviderDispatchReturned
        };
    }

    public static LlmChatOperation CompleteTranscript(
        LlmChatOperation operation,
        DateTimeOffset completedAtUtc,
        long resultingTranscriptRevision,
        Guid assistantEntryId)
    {
        if (operation.Status == LlmChatOperationStatus.Succeeded)
        {
            return operation;
        }

        RequireStatus(
            operation,
            LlmChatOperationStatus.Running,
            LlmChatOperationStatus.RecoveryRequired);
        if (assistantEntryId == Guid.Empty || resultingTranscriptRevision <= operation.ExpectedTranscriptRevision)
        {
            throw new InvalidOperationException("Transcript completion requires an assistant entry and an advanced revision.");
        }

        return ReleaseExecution(Advance(operation, LlmChatOperationStatus.Succeeded) with
        {
            TranscriptCompletedAtUtc = completedAtUtc,
            CompletedAtUtc = completedAtUtc,
            ResultingTranscriptRevision = resultingTranscriptRevision,
            AssistantEntryId = assistantEntryId,
            FailureCode = string.Empty
        });
    }

    public static LlmChatOperation RequestCancellation(
        LlmChatOperation operation,
        DateTimeOffset requestedAtUtc)
    {
        if (operation.IsTerminal || operation.Status == LlmChatOperationStatus.RecoveryRequired)
        {
            return operation;
        }

        if (operation.Status == LlmChatOperationStatus.CancellationRequested)
        {
            return operation;
        }

        RequireStatus(
            operation,
            LlmChatOperationStatus.Pending,
            LlmChatOperationStatus.Running);
        return Advance(operation, LlmChatOperationStatus.CancellationRequested) with
        {
            CancellationRequestedAtUtc = requestedAtUtc,
            CancellationGeneration = checked(operation.CancellationGeneration + 1)
        };
    }

    public static LlmChatOperation CompleteCancellation(
        LlmChatOperation operation,
        DateTimeOffset completedAtUtc)
    {
        if (operation.Status == LlmChatOperationStatus.Cancelled)
        {
            return operation;
        }

        RequireStatus(
            operation,
            LlmChatOperationStatus.Pending,
            LlmChatOperationStatus.Running,
            LlmChatOperationStatus.CancellationRequested,
            LlmChatOperationStatus.RecoveryRequired);
        return ReleaseExecution(Advance(operation, LlmChatOperationStatus.Cancelled) with
        {
            CancellationRequestedAtUtc = operation.CancellationRequestedAtUtc ?? completedAtUtc,
            CompletedAtUtc = completedAtUtc,
            FailureCode = LlmChatErrorCodes.Cancelled
        });
    }

    public static LlmChatOperation CompleteFailure(
        LlmChatOperation operation,
        DateTimeOffset completedAtUtc,
        string failureCode)
    {
        if (operation.Status == LlmChatOperationStatus.Failed)
        {
            return operation;
        }

        RequireStatus(
            operation,
            LlmChatOperationStatus.Pending,
            LlmChatOperationStatus.Running,
            LlmChatOperationStatus.CancellationRequested,
            LlmChatOperationStatus.RecoveryRequired);
        return ReleaseExecution(Advance(operation, LlmChatOperationStatus.Failed) with
        {
            CompletedAtUtc = completedAtUtc,
            FailureCode = NormalizeFailureCode(failureCode)
        });
    }

    public static LlmChatOperation RequireRecovery(
        LlmChatOperation operation,
        string failureCode)
    {
        if (operation.Status == LlmChatOperationStatus.RecoveryRequired)
        {
            return operation;
        }

        RequireStatus(
            operation,
            LlmChatOperationStatus.Pending,
            LlmChatOperationStatus.Running,
            LlmChatOperationStatus.CancellationRequested);
        return ReleaseExecution(Advance(operation, LlmChatOperationStatus.RecoveryRequired) with
        {
            FailureCode = NormalizeFailureCode(failureCode)
        });
    }

    private static LlmChatOperation ReleaseExecution(LlmChatOperation operation)
        => operation with
        {
            ExecutionOwnerId = null,
            ClaimedAtUtc = null,
            HeartbeatAtUtc = null,
            LeaseExpiresAtUtc = null
        };

    private static LlmChatOperation Advance(
        LlmChatOperation operation,
        LlmChatOperationStatus status)
        => operation with
        {
            Status = status,
            ConcurrencyToken = checked(operation.ConcurrencyToken + 1)
        };

    private static void RequireStatus(
        LlmChatOperation operation,
        params LlmChatOperationStatus[] allowed)
    {
        if (!allowed.Contains(operation.Status))
        {
            throw new InvalidOperationException(
                $"Operation status '{operation.Status}' does not allow this transition.");
        }
    }

    private static string NormalizeFailureCode(string failureCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(failureCode);
        var normalized = failureCode.Trim();
        if (normalized.Length > 200)
        {
            throw new ArgumentException("An operation failure code cannot exceed 200 characters.", nameof(failureCode));
        }

        return normalized;
    }
}
