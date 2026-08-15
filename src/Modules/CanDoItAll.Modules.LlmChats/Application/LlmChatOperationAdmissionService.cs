using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.Modules.LlmChats.Common;
using CanDoItAll.Modules.LlmChats.Conversations;
using CanDoItAll.Modules.LlmChats.Definitions;
using CanDoItAll.Modules.LlmChats.Operations;
using CanDoItAll.Modules.LlmChats.Ports;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.LlmChats.Application;

public sealed class LlmChatOperationAdmissionService(
    ILlmChatDefinitionRepository definitionRepository,
    ILlmChatConversationRepository conversationRepository,
    ILlmChatOperationRepository operationRepository,
    ILlmChatTurnStateRepository turnStateRepository,
    ILlmChatUnitOfWork unitOfWork,
    ILlmChatConversationEngine conversationEngine,
    ILlmChatOperationEvidenceSink evidenceSink,
    TimeProvider timeProvider)
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
                revision.SettingsFingerprint);
            var existing = await operationRepository.TryGetForUpdateAsync(command.OperationId, token)
                .ConfigureAwait(false);
            if (existing is not null)
            {
                return Matches(existing, command, requestFingerprint)
                    ? Result<LlmChatSendAdmission>.Success(new(existing, null, false))
                    : Result<LlmChatSendAdmission>.Failure(LlmChatErrors.OperationIdConflict());
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
                0);
            var operationAdmission = await operationRepository.AdmitAsync(proposed, token).ConfigureAwait(false);
            if (!operationAdmission.Created)
            {
                return Matches(operationAdmission.Operation, command, requestFingerprint)
                    ? Result<LlmChatSendAdmission>.Success(new(operationAdmission.Operation, null, false))
                    : Result<LlmChatSendAdmission>.Failure(LlmChatErrors.OperationIdConflict());
            }

            var claimed = await operationRepository.TryClaimDispatchAsync(
                command.OperationId,
                requestFingerprint,
                token).ConfigureAwait(false)
                ?? throw new InvalidOperationException("A newly admitted LLM Chat operation could not be claimed.");
            var turn = await conversationEngine.AdmitTurnAsync(
                conversation.Id,
                command.OperationId,
                definition,
                revision,
                command.Message,
                command.ExpectedTranscriptRevision,
                token).ConfigureAwait(false);
            var admittedOperation = await evidenceSink.MarkTurnAdmittedAsync(
                claimed.Id,
                timeProvider.GetUtcNow(),
                token).ConfigureAwait(false);
            return Result<LlmChatSendAdmission>.Success(new(admittedOperation, turn, true));
        }, cancellationToken);

    private static bool Matches(
        LlmChatOperation operation,
        SendLlmChatTurnCommand command,
        LlmChatRequestFingerprint requestFingerprint)
        => operation.RequestFingerprint == requestFingerprint &&
           operation.ConversationId == command.ConversationId &&
           operation.Kind == LlmChatOperationKind.SendTurn;
}

internal sealed record LlmChatSendAdmission(
    LlmChatOperation Operation,
    LlmConversationTurnAdmission? OptionalTurn,
    bool Created)
{
    public LlmConversationTurnAdmission Turn => OptionalTurn
        ?? throw new InvalidOperationException("A new operation requires turn admission state.");
}
