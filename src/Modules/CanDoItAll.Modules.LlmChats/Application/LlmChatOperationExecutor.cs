using CanDoItAll.Modules.LlmChats.Common;
using CanDoItAll.Modules.LlmChats.Operations;
using CanDoItAll.Modules.LlmChats.Ports;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.LlmChats.Application;

public sealed class LlmChatOperationExecutor(
    ILlmChatDefinitionRepository definitionRepository,
    ILlmChatConversationRepository conversationRepository,
    ILlmChatConversationEngine conversationEngine,
    ILlmChatExecutionLeaseHeartbeatStore heartbeatStore,
    ILlmChatOperationCancellationRegistry cancellationRegistry,
    ILlmChatOperationScopeAccessor operationScope,
    LlmChatStreamingPipeline streamingPipeline,
    LlmChatOperationStateMachine stateMachine,
    LlmChatExecutionLeaseOptions options,
    TimeProvider timeProvider,
    ILogger<LlmChatOperationExecutor> logger)
{
    public async Task ExecuteAsync(
        LlmChatExecutionClaimResult claim,
        CancellationToken cancellationToken = default)
    {
        if (!claim.Claimed || claim.Operation is null || claim.Lease is not { } lease)
        {
            throw new ArgumentException("A claimed LLM Chat execution lease is required.", nameof(claim));
        }

        var runtimeIdentity = operationScope.Current?.RuntimeIdentity
            ?? throw new InvalidOperationException("An LLM Chat runtime operation scope is required for dispatch.");
        using var executionScope = operationScope.Push(new LlmChatOperationExecutionContext(
            claim.Operation.Id,
            runtimeIdentity)
        {
            ExecutionLease = lease
        });
        using var registration = cancellationRegistry.Register(claim.Operation.Id, cancellationToken);
        if (claim.Operation.CancellationGeneration > 0)
        {
            await stateMachine.ApplyReducerAsync(claim.Operation.Id, null, CancellationToken.None)
                .ConfigureAwait(false);
            return;
        }

        var execution = ExecuteProviderAsync(
            claim.Operation,
            lease,
            runtimeIdentity,
            registration);
        try
        {
            while (!execution.IsCompleted)
            {
                await Task.WhenAny(
                        execution,
                        Task.Delay(options.HeartbeatInterval, timeProvider, cancellationToken))
                    .ConfigureAwait(false);
                if (execution.IsCompleted)
                {
                    break;
                }

                var now = timeProvider.GetUtcNow();
                var observation = await heartbeatStore.RenewAndObserveAsync(
                    lease,
                    runtimeIdentity,
                    now,
                    now + options.LeaseDuration,
                    cancellationToken).ConfigureAwait(false);
                if (!observation.IsCurrentOwner || observation.CancellationRequested)
                {
                    cancellationRegistry.RequestCancellation(claim.Operation.Id);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            cancellationRegistry.RequestCancellation(claim.Operation.Id);
        }

        await execution.ConfigureAwait(false);
    }

    private async Task ExecuteProviderAsync(
        LlmChatOperation operation,
        LlmChatExecutionLeaseIdentity lease,
        LlmChatRuntimeIdentity runtimeIdentity,
        ILlmChatOperationCancellationRegistration registration)
    {
        try
        {
            var conversation = await conversationRepository.TryGetAsync(
                operation.ConversationId,
                registration.CancellationToken).ConfigureAwait(false)
                ?? throw new LlmChatConversationEngineException(
                    LlmChatErrorCodes.StorageCorrupted,
                    "The admitted LLM Chat conversation no longer exists.");
            var definition = await definitionRepository.TryGetAsync(
                conversation.DefinitionId,
                registration.CancellationToken).ConfigureAwait(false)
                ?? throw new LlmChatConversationEngineException(
                    LlmChatErrorCodes.StorageCorrupted,
                    "The admitted LLM Chat definition no longer exists.");
            var revision = await definitionRepository.TryGetRevisionAsync(
                conversation.DefinitionId,
                conversation.DefinitionRevision,
                registration.CancellationToken).ConfigureAwait(false)
                ?? throw new LlmChatConversationEngineException(
                    LlmChatErrorCodes.StorageCorrupted,
                    "The admitted LLM Chat definition revision no longer exists.");
            var admission = await conversationEngine.ResumeAdmittedTurnAsync(
                conversation.Id,
                operation.Id,
                definition,
                revision,
                registration.CancellationToken).ConfigureAwait(false);
            var invocationResult = await streamingPipeline.ConsumeAsync(
                operation.Id,
                conversationEngine.StreamTurnAsync(admission, registration.CancellationToken),
                registration.CancellationToken).ConfigureAwait(false);
            await stateMachine.FinalizeSuccessAsync(
                admission,
                invocationResult,
                CancellationToken.None).ConfigureAwait(false);
        }
        catch (LlmChatRuntimeProfileChangedException)
        {
            logger.LogWarning(
                "LLM Chat execution {OperationId} lost runtime profile ownership for lease owner {OwnerId} epoch {ExecutionEpoch}.",
                operation.Id.Value,
                lease.OwnerId.Value,
                lease.Epoch);
        }
        catch (OperationCanceledException)
        {
            await ApplyCancellationOnlyWhenDurableAsync(
                operation.Id,
                lease,
                runtimeIdentity).ConfigureAwait(false);
        }
        catch (Exception exception) when (LlmChatOperationFailureCodes.TryMap(exception, out var failureCode))
        {
            await stateMachine.ApplyReducerAsync(operation.Id, failureCode, CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Unexpected dispatched LLM Chat failure for operation {OperationId} and conversation {ConversationId}.",
                operation.Id.Value,
                operation.ConversationId.Value);
            await stateMachine.ApplyReducerAsync(
                operation.Id,
                LlmChatErrorCodes.StorageCorrupted,
                CancellationToken.None).ConfigureAwait(false);
        }
    }

    private async Task ApplyCancellationOnlyWhenDurableAsync(
        LlmChatOperationId operationId,
        LlmChatExecutionLeaseIdentity lease,
        LlmChatRuntimeIdentity runtimeIdentity)
    {
        try
        {
            var observation = await heartbeatStore.ObserveAsync(
                lease,
                runtimeIdentity,
                timeProvider.GetUtcNow(),
                CancellationToken.None).ConfigureAwait(false);
            if (observation.IsCurrentOwner && observation.CancellationRequested)
            {
                await stateMachine.ApplyReducerAsync(operationId, null, CancellationToken.None)
                    .ConfigureAwait(false);
            }
        }
        catch (LlmChatRuntimeProfileChangedException)
        {
            logger.LogWarning(
                "LLM Chat cancellation reducer skipped operation {OperationId} because runtime profile ownership changed for lease owner {OwnerId} epoch {ExecutionEpoch}.",
                operationId.Value,
                lease.OwnerId.Value,
                lease.Epoch);
        }
    }
}
