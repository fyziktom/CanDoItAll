using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Operations;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Ports;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Application;

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
        LlmChatSendAdmission? accepted = null;
        try
        {
            Result<LlmChatSendAdmission> admissionResult;
            try {
                admissionResult = await admissionService.AdmitAsync(command, cancellationToken).ConfigureAwait(false);
            } catch (LlmChatCommittedException<Result<LlmChatSendAdmission>> exception) when (exception.Accepted.IsSuccess) {
                accepted = exception.Accepted.Value!;
                throw;
            }
            if (admissionResult.IsFailure)
            {
                return Result<LlmChatOperationDetails>.Failure(admissionResult.Errors);
            }

            var admission = admissionResult.Value!;
            accepted = admission;
            if (!admission.Operation.IsTerminal &&
                admission.Operation.Status != LlmChatOperationStatus.RecoveryRequired)
            {
                dispatchSignal.Signal();
            }

            var details = await stateMachine.ResolveExistingAsync(admission.Operation, cancellationToken)
                .ConfigureAwait(false);
            if (details.IsFailure) {
                throw new InvalidOperationException("The admitted operation needs a current-state read.");
            }
            return Result<LlmChatOperationDetails>.Success(details.Value! with { Replayed = !admission.Created });
        }
        catch (Exception exception) when (accepted is not null) {
            if (!accepted.Operation.IsTerminal && accepted.Operation.Status != LlmChatOperationStatus.RecoveryRequired) {
                try {
                    dispatchSignal.Signal();
                } catch (Exception signalFailure) {
                    exception = new AggregateException(exception, signalFailure);
                }
            }
            logger.LogWarning("Operation {OperationId} is admitted; reconciliation failed with {FailureType}.",
                accepted.Operation.Id.Value, exception.GetType().Name);
            var snapshot = new LlmChatOperationDetails(accepted.Operation, null, []) {
                Replayed = !accepted.Created,
                LastEventSequence = accepted.Operation.LastEventSequence
            };
            throw new LlmChatCommittedException<Result<LlmChatOperationDetails>>(Result<LlmChatOperationDetails>.Success(snapshot), exception);
        }
        catch (UnauthorizedAccessException) {
            throw;
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
            "Unexpected LLM Chat send failure for operation {OperationId} and conversation {ConversationId}. FailureType={FailureType}.",
            command.OperationId.Value,
            command.ConversationId.Value,
            exception.GetType().FullName);
}
