using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.Modules.LlmChats.Common;
using CanDoItAll.Modules.LlmChats.Operations;
using CanDoItAll.Modules.LlmChats.Ports;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.LlmChats.Application;

public sealed class LlmChatOperationStateMachine(
    ILlmChatOperationRepository operationRepository,
    ILlmChatInvocationRecordRepository invocationRepository,
    ILlmChatUnitOfWork unitOfWork,
    ILlmChatConversationEngine conversationEngine,
    ILlmChatOperationEvidenceSink evidenceSink,
    LlmChatOperationDetailsReader detailsReader,
    TimeProvider timeProvider,
    ILogger<LlmChatOperationStateMachine> logger)
{
    internal async Task<Result<LlmChatOperationDetails>> FinalizeSuccessAsync(
        LlmConversationTurnAdmission admission,
        LlmInvocationResult invocationResult,
        CancellationToken cancellationToken)
    {
        var operationId = new LlmChatOperationId(admission.UserEntry.TurnId);
        try
        {
            var operation = await unitOfWork.ExecuteAsync(async token =>
            {
                var current = await RequireLockedAsync(operationId, token).ConfigureAwait(false);
                var decision = await ReduceAsync(
                    current,
                    hasPendingAssistantResult: true,
                    token).ConfigureAwait(false);
                if (decision.Kind != LlmChatOperationDecisionKind.CommitSucceeded)
                {
                    return await ApplyDecisionAsync(current, decision, token).ConfigureAwait(false);
                }

                var turn = await conversationEngine.CompleteTurnAsync(admission, invocationResult, token)
                    .ConfigureAwait(false);
                return await evidenceSink.CompleteTranscriptAsync(
                    current.Id,
                    turn.State.UpdatedAtUtc,
                    turn.State.TranscriptRevision,
                    turn.AssistantEntryId,
                    turn.Model,
                    turn.Usage,
                    token).ConfigureAwait(false);
            }, cancellationToken).ConfigureAwait(false);
            return await detailsReader.BuildAsync(operation, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogError(
                "Failed to atomically finalize LLM Chat operation {OperationId}. FailureType={FailureType}.",
                operationId.Value,
                exception.GetType().FullName);
            return await RequireRecoveryAsync(
                operationId,
                LlmChatErrorCodes.OperationRecoveryRequired,
                CancellationToken.None).ConfigureAwait(false);
        }
    }

    internal async Task<Result<LlmChatOperationDetails>> ResolveExistingAsync(
        LlmChatOperation operation,
        CancellationToken cancellationToken)
    {
        return await detailsReader.BuildAsync(operation, cancellationToken).ConfigureAwait(false);
    }

    internal async Task<Result<LlmChatOperationDetails>> ReconcileAsync(
        LlmChatOperationId operationId,
        CancellationToken cancellationToken)
    {
        var operation = await operationRepository.TryGetAsync(operationId, cancellationToken).ConfigureAwait(false);
        if (operation is null)
        {
            return Result<LlmChatOperationDetails>.Failure(LlmChatErrors.OperationNotFound());
        }

        if (operation.IsTerminal ||
            operation.Status == LlmChatOperationStatus.RecoveryRequired ||
            operation.HasLiveExecutionLease(timeProvider.GetUtcNow()) ||
            operation.ProviderDispatchStartedAtUtc is null)
        {
            return await detailsReader.BuildAsync(operation, cancellationToken).ConfigureAwait(false);
        }

        return await ApplyReducerAsync(operation.Id, null, cancellationToken).ConfigureAwait(false);
    }

    internal async Task<Result<LlmChatOperationDetails>> ApplyReducerAsync(
        LlmChatOperationId operationId,
        string? fallbackFailureCode,
        CancellationToken cancellationToken)
    {
        try
        {
            var operation = await unitOfWork.ExecuteAsync(async token =>
            {
                var current = await RequireLockedAsync(operationId, token).ConfigureAwait(false);
                var decision = await ReduceAsync(current, false, token).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(fallbackFailureCode) &&
                    decision.Kind is LlmChatOperationDecisionKind.CompensateAndFail or
                        LlmChatOperationDecisionKind.MarkFailed)
                {
                    decision = decision with { FailureCode = fallbackFailureCode };
                }

                return await ApplyDecisionAsync(current, decision, token).ConfigureAwait(false);
            }, cancellationToken).ConfigureAwait(false);
            return await detailsReader.BuildAsync(operation, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogError(
                "Failed to atomically reduce LLM Chat operation {OperationId}; recovery is required. FailureType={FailureType}.",
                operationId.Value,
                exception.GetType().FullName);
            return await RequireRecoveryAsync(
                operationId,
                LlmChatErrorCodes.OperationRecoveryRequired,
                CancellationToken.None).ConfigureAwait(false);
        }
    }

    internal async Task<LlmChatOperation?> RequestCancellationAsync(
        LlmChatOperationId operationId,
        CancellationToken cancellationToken)
        => await unitOfWork.ExecuteAsync(async token =>
        {
            var operation = await operationRepository.TryGetForUpdateAsync(operationId, token)
                .ConfigureAwait(false);
            if (operation is null || operation.IsTerminal || operation.Status == LlmChatOperationStatus.RecoveryRequired)
            {
                return operation;
            }

            return await evidenceSink.RequestCancellationAsync(
                operationId,
                timeProvider.GetUtcNow(),
                token).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);

    internal async Task<Result<LlmChatOperationDetails>> AbandonAsync(
        AbandonLlmChatActiveTurnCommand command,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await unitOfWork.ExecuteAsync(async token =>
            {
                var operation = await operationRepository.TryGetForUpdateAsync(command.TurnId, token)
                    .ConfigureAwait(false);
                if (operation is null)
                {
                    return AbandonResult.NotFound;
                }

                if (operation.ConversationId != command.ConversationId ||
                    operation.Status != LlmChatOperationStatus.RecoveryRequired ||
                    operation.HasLiveExecutionLease(timeProvider.GetUtcNow()))
                {
                    return AbandonResult.Invalid;
                }

                var evidence = await conversationEngine.InspectTurnAsync(
                    command.ConversationId,
                    command.TurnId,
                    token).ConfigureAwait(false);
                if (evidence?.HasExactActiveTurn != true)
                {
                    return AbandonResult.Invalid;
                }

                await conversationEngine.CompensateTurnAsync(
                    command.ConversationId,
                    command.TurnId,
                    token).ConfigureAwait(false);
                var failed = await evidenceSink.CompleteFailureAsync(
                    operation.Id,
                    timeProvider.GetUtcNow(),
                    string.IsNullOrWhiteSpace(operation.FailureCode)
                        ? LlmChatErrorCodes.OperationRecoveryRequired
                        : operation.FailureCode,
                    token).ConfigureAwait(false);
                return new AbandonResult(failed, false, false);
            }, cancellationToken).ConfigureAwait(false);

            if (result.Missing)
            {
                return Result<LlmChatOperationDetails>.Failure(LlmChatErrors.OperationNotFound());
            }

            if (result.Rejected || result.Operation is null)
            {
                return Result<LlmChatOperationDetails>.Failure(LlmChatErrors.OperationRecoveryRequired());
            }

            return await detailsReader.BuildAsync(result.Operation, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogError(
                "Failed to atomically abandon LLM Chat operation {OperationId} for conversation {ConversationId}. FailureType={FailureType}.",
                command.TurnId.Value,
                command.ConversationId.Value,
                exception.GetType().FullName);
            return await RequireRecoveryAsync(
                command.TurnId,
                LlmChatErrorCodes.OperationRecoveryRequired,
                CancellationToken.None).ConfigureAwait(false);
        }
    }

    internal async Task<Result<LlmChatOperationDetails>> RequireRecoveryAsync(
        LlmChatOperationId operationId,
        string failureCode,
        CancellationToken cancellationToken)
    {
        var operation = await unitOfWork.ExecuteAsync(async token =>
        {
            var current = await RequireLockedAsync(operationId, token).ConfigureAwait(false);
            return current.IsTerminal || current.Status == LlmChatOperationStatus.RecoveryRequired
                ? current
                : await evidenceSink.RequireRecoveryAsync(current.Id, failureCode, token).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
        return await detailsReader.BuildAsync(operation, cancellationToken).ConfigureAwait(false);
    }

    private async Task<LlmChatOperationDecision> ReduceAsync(
        LlmChatOperation operation,
        bool hasPendingAssistantResult,
        CancellationToken cancellationToken)
    {
        var evidence = await conversationEngine.InspectTurnAsync(
            operation.ConversationId,
            operation.Id,
            cancellationToken).ConfigureAwait(false);
        var invocations = await invocationRepository.ListAsync(operation.Id, cancellationToken).ConfigureAwait(false);
        var lastInvocation = invocations.LastOrDefault();
        return LlmChatOperationReducer.Reduce(new LlmChatOperationDurableEvidence(
            operation,
            evidence?.HasExactActiveTurn == true,
            evidence?.Assistant is not null,
            evidence?.Assistant?.CreatedAtUtc,
            lastInvocation?.Outcome,
            lastInvocation?.FailureCode ?? string.Empty,
            hasPendingAssistantResult));
    }

    private async Task<LlmChatOperation> ApplyDecisionAsync(
        LlmChatOperation operation,
        LlmChatOperationDecision decision,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        switch (decision.Kind)
        {
            case LlmChatOperationDecisionKind.NoChange:
                return operation;
            case LlmChatOperationDecisionKind.MarkSucceeded:
            {
                var evidence = await conversationEngine.InspectTurnAsync(
                    operation.ConversationId,
                    operation.Id,
                    cancellationToken).ConfigureAwait(false);
                var assistant = evidence?.Assistant
                    ?? throw new InvalidOperationException("Succeeded reconciliation requires assistant evidence.");
                return await evidenceSink.CompleteTranscriptAsync(
                    operation.Id,
                    assistant.CreatedAtUtc,
                    evidence.State.TranscriptRevision,
                    assistant.EntryId,
                    assistant.Model,
                    assistant.Usage,
                    cancellationToken).ConfigureAwait(false);
            }
            case LlmChatOperationDecisionKind.CompensateAndFail:
                await conversationEngine.CompensateTurnAsync(
                    operation.ConversationId,
                    operation.Id,
                    cancellationToken).ConfigureAwait(false);
                return await evidenceSink.CompleteFailureAsync(
                    operation.Id,
                    now,
                    decision.FailureCode,
                    cancellationToken).ConfigureAwait(false);
            case LlmChatOperationDecisionKind.CompensateAndCancel:
                await conversationEngine.CompensateTurnAsync(
                    operation.ConversationId,
                    operation.Id,
                    cancellationToken).ConfigureAwait(false);
                return await evidenceSink.CompleteCancellationAsync(
                    operation.Id,
                    now,
                    cancellationToken).ConfigureAwait(false);
            case LlmChatOperationDecisionKind.MarkFailed:
                return await evidenceSink.CompleteFailureAsync(
                    operation.Id,
                    now,
                    decision.FailureCode,
                    cancellationToken).ConfigureAwait(false);
            case LlmChatOperationDecisionKind.MarkCancelled:
                return await evidenceSink.CompleteCancellationAsync(
                    operation.Id,
                    now,
                    cancellationToken).ConfigureAwait(false);
            case LlmChatOperationDecisionKind.RequireRecovery:
                return await evidenceSink.RequireRecoveryAsync(
                    operation.Id,
                    decision.FailureCode,
                    cancellationToken).ConfigureAwait(false);
            case LlmChatOperationDecisionKind.CommitSucceeded:
            default:
                throw new InvalidOperationException(
                    $"Operation decision '{decision.Kind}' is invalid outside live success finalization.");
        }
    }

    private async Task<LlmChatOperation> RequireLockedAsync(
        LlmChatOperationId operationId,
        CancellationToken cancellationToken)
        => await operationRepository.TryGetForUpdateAsync(operationId, cancellationToken).ConfigureAwait(false)
           ?? throw new InvalidOperationException("The admitted LLM Chat operation no longer exists.");

    private sealed record AbandonResult(
        LlmChatOperation? Operation,
        bool Missing,
        bool Rejected)
    {
        public static AbandonResult NotFound { get; } = new(null, true, false);

        public static AbandonResult Invalid { get; } = new(null, false, true);
    }
}
