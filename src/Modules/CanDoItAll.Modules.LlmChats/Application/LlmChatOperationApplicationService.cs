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
    ILlmChatOperationDispatchSignal dispatchSignal,
    ILogger<LlmChatOperationApplicationService> logger) : ILlmChatOperationApplicationService
{
    public async Task<Result<LlmChatOperationDetails>> SendAsync(
        SendLlmChatTurnCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        try
        {
            if (!dispatchSignal.HasAvailableExecutor)
            {
                return Result<LlmChatOperationDetails>.Failure(LlmChatErrors.DispatcherUnavailable());
            }

            var admissionResult = await admissionService.AdmitAsync(command, cancellationToken).ConfigureAwait(false);
            if (admissionResult.IsFailure)
            {
                return Result<LlmChatOperationDetails>.Failure(admissionResult.Errors);
            }

            var admission = admissionResult.Value!;
            if (!admission.Operation.IsTerminal &&
                admission.Operation.Status != LlmChatOperationStatus.RecoveryRequired)
            {
                dispatchSignal.Signal();
            }

            return await stateMachine.ResolveExistingAsync(admission.Operation, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (ArgumentException exception)
        {
            return Result<LlmChatOperationDetails>.Failure(LlmChatErrors.InvalidRequest(exception.Message));
        }
        catch (Exception exception) when (LlmChatOperationFailureCodes.TryMap(exception, out var failureCode))
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
        dispatchSignal.Signal();
        return await detailsReader.BuildAsync(requested, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<LlmChatOperationDetails>> ReconcileAsync(
        LlmChatOperationId operationId,
        CancellationToken cancellationToken = default)
    {
        var result = await stateMachine.ReconcileAsync(operationId, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess &&
            result.Value?.Operation is { IsTerminal: false, Status: not LlmChatOperationStatus.RecoveryRequired })
        {
            dispatchSignal.Signal();
        }

        return result;
    }

    public Task<Result<LlmChatOperationDetails>> AbandonActiveTurnAsync(
        AbandonLlmChatActiveTurnCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return stateMachine.AbandonAsync(command, cancellationToken);
    }

    private void LogUnexpectedFailure(Exception exception, SendLlmChatTurnCommand command)
        => logger.LogError(
            exception,
            "Unexpected LLM Chat send failure for operation {OperationId} and conversation {ConversationId}.",
            command.OperationId.Value,
            command.ConversationId.Value);
}
