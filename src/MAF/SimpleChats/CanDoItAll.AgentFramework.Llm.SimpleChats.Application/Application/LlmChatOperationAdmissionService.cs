using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Conversations;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Definitions;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Operations;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Ports;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Application;

public sealed class LlmChatOperationAdmissionService(
    ILlmChatDefinitionRepository definitionRepository,
    ILlmChatConversationRepository conversationRepository,
    ILlmChatOperationRepository operationRepository,
    ILlmChatTurnStateRepository turnStateRepository,
    ILlmChatOperationDispatchSignal dispatchSignal,
    ILlmChatUnitOfWork unitOfWork,
    ILlmChatConversationEngine conversationEngine,
    ILlmChatOperationEvidenceSink evidenceSink,
    TimeProvider timeProvider,
    LlmChatOperationEventJournal eventJournal)
{
    internal Task<Result<LlmChatSendAdmission>> AdmitAsync(
        SendLlmChatTurnCommand command,
        CancellationToken cancellationToken)
        => unitOfWork.ExecuteAsync(async token =>
        {
            var turnState = await turnStateRepository.LockAsync(command.ConversationId, token)
                .ConfigureAwait(false);
            if (!turnState.Exists)
            {
                return Result<LlmChatSendAdmission>.Failure(LlmChatErrors.ConversationNotFound());
            }

            var conversation = await conversationRepository.TryGetAsync(command.ConversationId, token)
                .ConfigureAwait(false);
            if (conversation is null)
            {
                return Result<LlmChatSendAdmission>.Failure(LlmChatErrors.StorageCorrupted());
            }

            var definition = await definitionRepository.TryGetAsync(conversation.DefinitionId, token)
                .ConfigureAwait(false);
            var revision = await definitionRepository.TryGetRevisionAsync(
                conversation.DefinitionId,
                conversation.DefinitionRevision,
                token).ConfigureAwait(false);
            if (definition is null || revision is null)
            {
                return Result<LlmChatSendAdmission>.Failure(LlmChatErrors.StorageCorrupted());
            }

            var requestFingerprint = LlmChatFingerprints.CreateRequest(
                command.ConversationId,
                command.ExpectedTranscriptRevision,
                command.Message,
                revision.SettingsFingerprint,
                command.AttributionScope);
            var existing = await operationRepository.TryGetForUpdateAsync(command.OperationId, token)
                .ConfigureAwait(false);
            if (existing is not null)
            {
                return Matches(existing, command, requestFingerprint)
                    ? Result<LlmChatSendAdmission>.Success(new(existing, false))
                    : Result<LlmChatSendAdmission>.Failure(LlmChatErrors.OperationIdConflict());
            }

            if (!dispatchSignal.HasAvailableExecutor)
            {
                return Result<LlmChatSendAdmission>.Failure(LlmChatErrors.DispatcherUnavailable());
            }

            if (conversation.Status == LlmChatConversationStatus.Archived)
            {
                return Result<LlmChatSendAdmission>.Failure(LlmChatErrors.ConversationArchived());
            }

            if (definition.Status != LlmChatDefinitionStatus.Active)
            {
                return Result<LlmChatSendAdmission>.Failure(LlmChatErrors.DefinitionNotActive());
            }

            if (turnState.HasActiveTurn || turnState.HasNonterminalOperation)
            {
                return Result<LlmChatSendAdmission>.Failure(LlmChatErrors.ActiveTurnConflict());
            }

            var proposed = new LlmChatOperation(
                command.OperationId,
                command.ConversationId,
                LlmChatOperationKind.SendTurn,
                requestFingerprint,
                command.ExpectedTranscriptRevision,
                LlmChatOperationStatus.Pending,
                timeProvider.GetUtcNow(),
                0,
                command.AttributionScope);
            var operationAdmission = await operationRepository.AdmitAsync(proposed, token).ConfigureAwait(false);
            if (!operationAdmission.Created)
            {
                return Matches(operationAdmission.Operation, command, requestFingerprint)
                    ? Result<LlmChatSendAdmission>.Success(new(operationAdmission.Operation, false))
                    : Result<LlmChatSendAdmission>.Failure(LlmChatErrors.OperationIdConflict());
            }

            await eventJournal.AppendStateChangedAsync(operationAdmission.Operation, cancellationToken: token)
                .ConfigureAwait(false);

            await conversationEngine.AdmitTurnAsync(
                conversation.Id,
                command.OperationId,
                definition,
                revision,
                command.Message,
                command.ExpectedTranscriptRevision,
                token).ConfigureAwait(false);
            var admittedOperation = await evidenceSink.MarkTurnAdmittedAsync(
                operationAdmission.Operation.Id,
                timeProvider.GetUtcNow(),
                token).ConfigureAwait(false);
            return Result<LlmChatSendAdmission>.Success(new(admittedOperation, true));
        }, cancellationToken);

    private static bool Matches(
        LlmChatOperation operation,
        SendLlmChatTurnCommand command,
        LlmChatRequestFingerprint requestFingerprint)
        => operation.RequestFingerprint == requestFingerprint &&
           operation.ConversationId == command.ConversationId &&
           operation.AttributionScope == command.AttributionScope &&
           operation.Kind == LlmChatOperationKind.SendTurn;
}

internal sealed record LlmChatSendAdmission(
    LlmChatOperation Operation,
    bool Created);
