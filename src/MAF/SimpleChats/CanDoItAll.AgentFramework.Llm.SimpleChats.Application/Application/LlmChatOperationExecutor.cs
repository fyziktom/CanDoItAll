using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Operations;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Ports;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Application;

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
        var remainingDuration = claim.Operation.StartedAtUtc + options.MaximumOperationDuration -
            timeProvider.GetUtcNow();
        using var operationDeadline = new CancellationTokenSource(
            remainingDuration > TimeSpan.Zero ? remainingDuration : TimeSpan.Zero,
            timeProvider);
        using var executionLifetime = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            operationDeadline.Token);
        using var registration = cancellationRegistry.Register(
            claim.Operation.Id,
            executionLifetime.Token);
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
        Exception? controlFailure = null;
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

                cancellationToken.ThrowIfCancellationRequested();
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
        catch (Exception exception)
        {
            controlFailure = exception;
            cancellationRegistry.RequestCancellation(claim.Operation.Id);
        }

        var providerExit = LlmChatProviderExecutionExit.Completed;
        Exception? providerDrainFailure = null;
        try
        {
            providerExit = await execution.ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            providerDrainFailure = exception;
        }

        if (controlFailure is not null)
        {
            if (providerDrainFailure is not null)
            {
                logger.LogWarning(
                    "LLM Chat provider execution {OperationId} failed while draining after a control failure. ControlFailureType={ControlFailureType} DrainFailureType={DrainFailureType}.",
                    claim.Operation.Id.Value,
                    controlFailure.GetType().FullName,
                    providerDrainFailure.GetType().FullName);
            }

            var failureCode = LlmChatOperationFailureCodes.TryMap(controlFailure, out var mappedCode)
                ? mappedCode
                : LlmChatErrorCodes.StorageCorrupted;
            await stateMachine.ResolveControlFailureAsync(
                claim.Operation.Id,
                failureCode,
                controlFailure is LlmChatRuntimeProfileChangedException,
                CancellationToken.None).ConfigureAwait(false);
        }
        else if (providerDrainFailure is not null)
        {
            logger.LogError(
                "Failed while draining LLM Chat provider execution {OperationId}. FailureType={FailureType}.",
                claim.Operation.Id.Value,
                providerDrainFailure.GetType().FullName);
            await stateMachine.ApplyReducerAsync(
                claim.Operation.Id,
                LlmChatErrorCodes.StorageCorrupted,
                CancellationToken.None).ConfigureAwait(false);
        }
        else if (providerExit == LlmChatProviderExecutionExit.ProfileChanged)
        {
            await stateMachine.ResolveControlFailureAsync(
                claim.Operation.Id,
                LlmChatErrorCodes.RuntimeProfileChanged,
                preservePostDispatchRecovery: true,
                CancellationToken.None).ConfigureAwait(false);
        }
        else if (operationDeadline.IsCancellationRequested)
        {
            await stateMachine.ResolveControlFailureAsync(
                claim.Operation.Id,
                LlmChatErrorCodes.OperationDurationExceeded,
                preservePostDispatchRecovery: true,
                CancellationToken.None).ConfigureAwait(false);
        }
    }

    private async Task<LlmChatProviderExecutionExit> ExecuteProviderAsync(
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
            return LlmChatProviderExecutionExit.Completed;
        }
        catch (LlmChatRuntimeProfileChangedException)
        {
            logger.LogWarning(
                "LLM Chat execution {OperationId} lost runtime profile ownership for lease owner {OwnerId} epoch {ExecutionEpoch}.",
                operation.Id.Value,
                lease.OwnerId.Value,
                lease.Epoch);
            return LlmChatProviderExecutionExit.ProfileChanged;
        }
        catch (OperationCanceledException)
        {
            await ApplyCancellationOnlyWhenDurableAsync(
                operation.Id,
                lease,
                runtimeIdentity).ConfigureAwait(false);
            return LlmChatProviderExecutionExit.Completed;
        }
        catch (Exception exception) when (LlmChatOperationFailureCodes.TryMap(exception, out var failureCode))
        {
            await stateMachine.ApplyReducerAsync(operation.Id, failureCode, CancellationToken.None)
                .ConfigureAwait(false);
            return LlmChatProviderExecutionExit.Completed;
        }
        catch (Exception exception)
        {
            logger.LogError(
                "Unexpected dispatched LLM Chat failure for operation {OperationId} and conversation {ConversationId}. FailureType={FailureType}.",
                operation.Id.Value,
                operation.ConversationId.Value,
                exception.GetType().FullName);
            await stateMachine.ApplyReducerAsync(
                operation.Id,
                LlmChatErrorCodes.StorageCorrupted,
                CancellationToken.None).ConfigureAwait(false);
            return LlmChatProviderExecutionExit.Completed;
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

    private enum LlmChatProviderExecutionExit
    {
        Completed,
        ProfileChanged
    }
}
