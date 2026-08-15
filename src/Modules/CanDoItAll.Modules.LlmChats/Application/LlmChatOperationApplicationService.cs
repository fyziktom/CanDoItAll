using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.Modules.LlmChats.Common;
using CanDoItAll.Modules.LlmChats.Operations;
using CanDoItAll.Modules.LlmChats.Ports;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.LlmChats.Application;

public sealed class LlmChatOperationApplicationService(
    LlmChatOperationAdmissionService admissionService,
    LlmChatOperationStateMachine stateMachine,
    LlmChatOperationDetailsReader detailsReader,
    ILlmChatOperationCancellationRegistry cancellationRegistry,
    ILlmChatConversationEngine conversationEngine,
    ILogger<LlmChatOperationApplicationService> logger) : ILlmChatOperationApplicationService
{
    public async Task<Result<LlmChatOperationDetails>> SendAsync(
        SendLlmChatTurnCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        try
        {
            var admissionResult = await admissionService.AdmitAsync(command, cancellationToken).ConfigureAwait(false);
            if (admissionResult.IsFailure)
            {
                return Result<LlmChatOperationDetails>.Failure(admissionResult.Errors);
            }

            var admission = admissionResult.Value!;
            if (!admission.Created)
            {
                return await stateMachine.ResolveExistingAsync(admission.Operation, cancellationToken)
                    .ConfigureAwait(false);
            }

            ILlmChatOperationCancellationRegistration registration;
            try
            {
                registration = cancellationRegistry.Register(command.OperationId, cancellationToken);
            }
            catch (InvalidOperationException)
            {
                var current = await detailsReader.RequireAsync(command.OperationId, cancellationToken)
                    .ConfigureAwait(false);
                return await detailsReader.BuildAsync(current, cancellationToken).ConfigureAwait(false);
            }

            using (registration)
            {
                try
                {
                    var invocationResult = await conversationEngine.InvokeTurnAsync(
                        admission.Turn,
                        registration.CancellationToken).ConfigureAwait(false);
                    return await stateMachine.FinalizeSuccessAsync(
                        admission.Turn,
                        invocationResult,
                        CancellationToken.None).ConfigureAwait(false);
                }
                catch (LlmChatRuntimeProfileChangedException)
                {
                    return await stateMachine.RequireRecoveryAsync(
                        command.OperationId,
                        LlmChatErrorCodes.RuntimeProfileChanged,
                        CancellationToken.None).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    await stateMachine.RequestCancellationAsync(command.OperationId, CancellationToken.None)
                        .ConfigureAwait(false);
                    return await stateMachine.ApplyReducerAsync(command.OperationId, null, CancellationToken.None)
                        .ConfigureAwait(false);
                }
                catch (Exception exception) when (TryMapFailureCode(exception, out var failureCode))
                {
                    return await stateMachine.ApplyReducerAsync(
                        command.OperationId,
                        failureCode,
                        CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    LogUnexpectedFailure(exception, command);
                    return await stateMachine.ApplyReducerAsync(
                        command.OperationId,
                        LlmChatErrorCodes.StorageCorrupted,
                        CancellationToken.None).ConfigureAwait(false);
                }
            }
        }
        catch (ArgumentException exception)
        {
            return Result<LlmChatOperationDetails>.Failure(LlmChatErrors.InvalidRequest(exception.Message));
        }
        catch (Exception exception) when (TryMapFailureCode(exception, out var failureCode))
        {
            return Result<LlmChatOperationDetails>.Failure(LlmChatErrors.OperationFailure(failureCode));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            LogUnexpectedFailure(exception, command);
            return Result<LlmChatOperationDetails>.Failure(LlmChatErrors.StorageCorrupted());
        }
    }

    public Task<Result<LlmChatOperationDetails>> GetAsync(
        LlmChatOperationId operationId,
        CancellationToken cancellationToken = default)
        => detailsReader.GetAsync(operationId, cancellationToken);

    public async Task<Result<LlmChatOperationDetails>> CancelAsync(
        LlmChatOperationId operationId,
        CancellationToken cancellationToken = default)
    {
        var requested = await stateMachine.RequestCancellationAsync(operationId, cancellationToken)
            .ConfigureAwait(false);
        if (requested is null)
        {
            return Result<LlmChatOperationDetails>.Failure(LlmChatErrors.OperationNotFound());
        }

        if (requested.IsTerminal || requested.Status == LlmChatOperationStatus.RecoveryRequired)
        {
            return await detailsReader.BuildAsync(requested, cancellationToken).ConfigureAwait(false);
        }

        cancellationRegistry.RequestCancellation(operationId);
        return cancellationRegistry.IsRegistered(operationId)
            ? await detailsReader.BuildAsync(requested, cancellationToken).ConfigureAwait(false)
            : await stateMachine.ApplyReducerAsync(operationId, null, cancellationToken).ConfigureAwait(false);
    }

    public Task<Result<LlmChatOperationDetails>> ReconcileAsync(
        LlmChatOperationId operationId,
        CancellationToken cancellationToken = default)
        => stateMachine.ReconcileAsync(operationId, cancellationToken);

    public Task<Result<LlmChatOperationDetails>> AbandonActiveTurnAsync(
        AbandonLlmChatActiveTurnCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return stateMachine.AbandonAsync(command, cancellationToken);
    }

    private static bool TryMapFailureCode(Exception exception, out string failureCode)
    {
        failureCode = exception switch
        {
            LlmChatConversationEngineException engineException => engineException.Code,
            LlmInvocationException { Kind: LlmInvocationFailureKind.InvalidRequest } =>
                LlmChatErrorCodes.ModelSettingsInvalid,
            LlmInvocationException { Kind: LlmInvocationFailureKind.DeadlineExceeded } =>
                LlmChatErrorCodes.DeadlineExceeded,
            LlmInvocationException => LlmChatErrorCodes.ProviderUnavailable,
            LlmConversationException { Kind: LlmConversationFailureKind.RevisionConflict } =>
                LlmChatErrorCodes.TranscriptRevisionConflict,
            LlmConversationException { Kind: LlmConversationFailureKind.TurnAlreadyActive } =>
                LlmChatErrorCodes.ActiveTurnConflict,
            LlmConversationException { Kind: LlmConversationFailureKind.ConcurrencyConflict } =>
                LlmChatErrorCodes.StorageConflict,
            _ => string.Empty
        };
        return failureCode.Length > 0;
    }

    private void LogUnexpectedFailure(Exception exception, SendLlmChatTurnCommand command)
        => logger.LogError(
            exception,
            "Unexpected LLM Chat send failure for operation {OperationId} and conversation {ConversationId}.",
            command.OperationId.Value,
            command.ConversationId.Value);
}
