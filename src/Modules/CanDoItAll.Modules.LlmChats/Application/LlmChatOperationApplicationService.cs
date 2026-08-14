using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.Modules.LlmChats.Common;
using CanDoItAll.Modules.LlmChats.Conversations;
using CanDoItAll.Modules.LlmChats.Definitions;
using CanDoItAll.Modules.LlmChats.Operations;
using CanDoItAll.Modules.LlmChats.Ports;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.LlmChats.Application;

public sealed class LlmChatOperationApplicationService(
    ILlmChatDefinitionRepository definitionRepository,
    ILlmChatConversationRepository conversationRepository,
    ILlmChatOperationRepository operationRepository,
    ILlmChatInvocationRecordRepository invocationRepository,
    ILlmChatUnitOfWork unitOfWork,
    ILlmChatConversationEngine conversationEngine,
    ILlmChatOperationEvidenceSink evidenceSink,
    ILlmChatOperationCancellationRegistry cancellationRegistry,
    TimeProvider timeProvider,
    ILogger<LlmChatOperationApplicationService> logger) : ILlmChatOperationApplicationService
{
    public async Task<Result<LlmChatOperationDetails>> SendAsync(
        SendLlmChatTurnCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        try
        {
            var context = await ResolveSendContextAsync(command, cancellationToken).ConfigureAwait(false);
            if (context.IsFailure)
            {
                return Result<LlmChatOperationDetails>.Failure(context.Errors);
            }

            var (conversation, definition, revision) = context.Value!;
            var requestFingerprint = LlmChatFingerprints.CreateRequest(
                command.ConversationId,
                command.ExpectedTranscriptRevision,
                command.Message,
                revision.SettingsFingerprint);
            var proposed = new LlmChatOperation(
                command.OperationId,
                command.ConversationId,
                LlmChatOperationKind.SendTurn,
                requestFingerprint,
                command.ExpectedTranscriptRevision,
                LlmChatOperationStatus.Pending,
                timeProvider.GetUtcNow(),
                0);
            var admission = await unitOfWork.ExecuteAsync(
                token => operationRepository.AdmitAsync(proposed, token),
                cancellationToken).ConfigureAwait(false);
            if (admission.Operation.RequestFingerprint != requestFingerprint ||
                admission.Operation.ConversationId != command.ConversationId ||
                admission.Operation.Kind != LlmChatOperationKind.SendTurn)
            {
                return Result<LlmChatOperationDetails>.Failure(LlmChatErrors.OperationIdConflict());
            }

            if (admission.Operation.Status != LlmChatOperationStatus.Pending)
            {
                return await ResolveExistingAsync(admission.Operation, cancellationToken).ConfigureAwait(false);
            }

            ILlmChatOperationCancellationRegistration registration;
            try
            {
                registration = cancellationRegistry.Register(command.OperationId, cancellationToken);
            }
            catch (InvalidOperationException)
            {
                var current = await RequireOperationAsync(command.OperationId, cancellationToken).ConfigureAwait(false);
                return await BuildDetailsAsync(current, cancellationToken).ConfigureAwait(false);
            }

            using (registration)
            {
                var claimed = await unitOfWork.ExecuteAsync(
                    token => operationRepository.TryClaimDispatchAsync(
                        command.OperationId,
                        requestFingerprint,
                        token),
                    cancellationToken).ConfigureAwait(false);
                if (claimed is null)
                {
                    var current = await RequireOperationAsync(command.OperationId, CancellationToken.None)
                        .ConfigureAwait(false);
                    if (current.Status == LlmChatOperationStatus.CancellationRequested)
                    {
                        current = await evidenceSink.CompleteCancellationAsync(
                            current.Id,
                            timeProvider.GetUtcNow(),
                            CancellationToken.None).ConfigureAwait(false);
                    }

                    return await ResolveExistingAsync(current, CancellationToken.None).ConfigureAwait(false);
                }

                try
                {
                    var turn = await conversationEngine.SendAsync(
                        conversation.Id,
                        command.OperationId,
                        definition,
                        revision,
                        command.Message,
                        command.ExpectedTranscriptRevision,
                        registration.CancellationToken).ConfigureAwait(false);
                    var completed = await evidenceSink.CompleteTranscriptAsync(
                        command.OperationId,
                        timeProvider.GetUtcNow(),
                        turn.State.TranscriptRevision,
                        turn.AssistantEntryId,
                        CancellationToken.None).ConfigureAwait(false);
                    return await BuildDetailsAsync(
                        completed,
                        new LlmChatAssistantMessage(
                            turn.AssistantEntryId,
                            command.OperationId,
                            turn.AssistantText,
                            turn.Model,
                            turn.Usage,
                            turn.State.UpdatedAtUtc),
                        CancellationToken.None).ConfigureAwait(false);
                }
                catch (LlmChatRuntimeProfileChangedException)
                {
                    var current = await RequireOperationAsync(command.OperationId, CancellationToken.None)
                        .ConfigureAwait(false);
                    var failed = current.ProviderDispatchStartedAtUtc is null
                        ? await evidenceSink.CompleteFailureAsync(
                            current.Id,
                            timeProvider.GetUtcNow(),
                            LlmChatErrorCodes.RuntimeProfileChanged,
                            CancellationToken.None).ConfigureAwait(false)
                        : await evidenceSink.RequireRecoveryAsync(
                            current.Id,
                            LlmChatErrorCodes.RuntimeProfileChanged,
                            CancellationToken.None).ConfigureAwait(false);
                    return await BuildDetailsAsync(failed, CancellationToken.None).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    var cancelled = await evidenceSink.CompleteCancellationAsync(
                        command.OperationId,
                        timeProvider.GetUtcNow(),
                        CancellationToken.None).ConfigureAwait(false);
                    return await BuildDetailsAsync(cancelled, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception exception) when (TryMapFailureCode(exception, out var failureCode))
                {
                    var failed = await evidenceSink.CompleteFailureAsync(
                        command.OperationId,
                        timeProvider.GetUtcNow(),
                        failureCode,
                        CancellationToken.None).ConfigureAwait(false);
                    return await BuildDetailsAsync(failed, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    LogUnexpectedFailure(exception, command);
                    var failed = await evidenceSink.CompleteFailureAsync(
                        command.OperationId,
                        timeProvider.GetUtcNow(),
                        LlmChatErrorCodes.StorageCorrupted,
                        CancellationToken.None).ConfigureAwait(false);
                    return await BuildDetailsAsync(failed, CancellationToken.None).ConfigureAwait(false);
                }
            }
        }
        catch (ArgumentException exception)
        {
            return Result<LlmChatOperationDetails>.Failure(LlmChatErrors.InvalidRequest(exception.Message));
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

    public async Task<Result<LlmChatOperationDetails>> GetAsync(
        LlmChatOperationId operationId,
        CancellationToken cancellationToken = default)
    {
        var operation = await operationRepository.TryGetAsync(operationId, cancellationToken).ConfigureAwait(false);
        return operation is null
            ? Result<LlmChatOperationDetails>.Failure(LlmChatErrors.OperationNotFound())
            : await BuildDetailsAsync(operation, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<LlmChatOperationDetails>> CancelAsync(
        LlmChatOperationId operationId,
        CancellationToken cancellationToken = default)
    {
        var operation = await operationRepository.TryGetAsync(operationId, cancellationToken).ConfigureAwait(false);
        if (operation is null)
        {
            return Result<LlmChatOperationDetails>.Failure(LlmChatErrors.OperationNotFound());
        }

        if (operation.IsTerminal || operation.Status == LlmChatOperationStatus.RecoveryRequired)
        {
            return await BuildDetailsAsync(operation, cancellationToken).ConfigureAwait(false);
        }

        var requested = await evidenceSink.RequestCancellationAsync(
            operationId,
            timeProvider.GetUtcNow(),
            cancellationToken).ConfigureAwait(false);
        cancellationRegistry.RequestCancellation(operationId);
        if (!cancellationRegistry.IsRegistered(operationId) && requested.ProviderDispatchStartedAtUtc is null)
        {
            requested = await evidenceSink.CompleteCancellationAsync(
                operationId,
                timeProvider.GetUtcNow(),
                cancellationToken).ConfigureAwait(false);
        }

        return await BuildDetailsAsync(requested, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<LlmChatOperationDetails>> ReconcileAsync(
        LlmChatOperationId operationId,
        CancellationToken cancellationToken = default)
    {
        var operation = await operationRepository.TryGetAsync(operationId, cancellationToken).ConfigureAwait(false);
        return operation is null
            ? Result<LlmChatOperationDetails>.Failure(LlmChatErrors.OperationNotFound())
            : await ReconcileCoreAsync(operation, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<LlmChatOperationDetails>> AbandonActiveTurnAsync(
        AbandonLlmChatActiveTurnCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var operation = await operationRepository.TryGetAsync(command.TurnId, cancellationToken)
            .ConfigureAwait(false);
        if (operation is null)
        {
            return Result<LlmChatOperationDetails>.Failure(LlmChatErrors.OperationNotFound());
        }

        if (operation.ConversationId != command.ConversationId ||
            operation.Status != LlmChatOperationStatus.RecoveryRequired ||
            cancellationRegistry.IsRegistered(operation.Id))
        {
            return Result<LlmChatOperationDetails>.Failure(LlmChatErrors.OperationRecoveryRequired());
        }

        var evidence = await conversationEngine.InspectTurnAsync(
            command.ConversationId,
            command.TurnId,
            cancellationToken).ConfigureAwait(false);
        if (evidence?.HasExactActiveTurn != true)
        {
            return Result<LlmChatOperationDetails>.Failure(LlmChatErrors.OperationRecoveryRequired());
        }

        await conversationEngine.AbandonActiveTurnAsync(
            command.ConversationId,
            command.TurnId,
            cancellationToken).ConfigureAwait(false);
        var failed = await evidenceSink.CompleteFailureAsync(
            operation.Id,
            timeProvider.GetUtcNow(),
            string.IsNullOrWhiteSpace(operation.FailureCode)
                ? LlmChatErrorCodes.OperationRecoveryRequired
                : operation.FailureCode,
            cancellationToken).ConfigureAwait(false);
        return await BuildDetailsAsync(failed, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<(LlmChatConversation Conversation, LlmChatDefinition Definition, LlmChatDefinitionRevision Revision)>>
        ResolveSendContextAsync(
            SendLlmChatTurnCommand command,
            CancellationToken cancellationToken)
    {
        var conversation = await conversationRepository.TryGetAsync(command.ConversationId, cancellationToken)
            .ConfigureAwait(false);
        if (conversation is null)
        {
            return Result<(LlmChatConversation, LlmChatDefinition, LlmChatDefinitionRevision)>.Failure(
                LlmChatErrors.ConversationNotFound());
        }

        if (conversation.Status == LlmChatConversationStatus.Archived)
        {
            return Result<(LlmChatConversation, LlmChatDefinition, LlmChatDefinitionRevision)>.Failure(
                LlmChatErrors.ConversationArchived());
        }

        var definition = await definitionRepository.TryGetAsync(conversation.DefinitionId, cancellationToken)
            .ConfigureAwait(false);
        if (definition is null)
        {
            return Result<(LlmChatConversation, LlmChatDefinition, LlmChatDefinitionRevision)>.Failure(
                LlmChatErrors.StorageCorrupted());
        }

        if (definition.Status != LlmChatDefinitionStatus.Active)
        {
            return Result<(LlmChatConversation, LlmChatDefinition, LlmChatDefinitionRevision)>.Failure(
                LlmChatErrors.DefinitionNotActive());
        }

        var revision = await definitionRepository.TryGetRevisionAsync(
            conversation.DefinitionId,
            conversation.DefinitionRevision,
            cancellationToken).ConfigureAwait(false);
        return revision is null
            ? Result<(LlmChatConversation, LlmChatDefinition, LlmChatDefinitionRevision)>.Failure(
                LlmChatErrors.StorageCorrupted())
            : Result<(LlmChatConversation, LlmChatDefinition, LlmChatDefinitionRevision)>.Success(
                (conversation, definition, revision));
    }

    private async Task<Result<LlmChatOperationDetails>> ResolveExistingAsync(
        LlmChatOperation operation,
        CancellationToken cancellationToken)
    {
        if (operation.IsTerminal || cancellationRegistry.IsRegistered(operation.Id))
        {
            return await BuildDetailsAsync(operation, cancellationToken).ConfigureAwait(false);
        }

        return await ReconcileCoreAsync(operation, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<LlmChatOperationDetails>> ReconcileCoreAsync(
        LlmChatOperation operation,
        CancellationToken cancellationToken)
    {
        if (operation.IsTerminal || cancellationRegistry.IsRegistered(operation.Id))
        {
            return await BuildDetailsAsync(operation, cancellationToken).ConfigureAwait(false);
        }

        var transcriptEvidence = await conversationEngine.InspectTurnAsync(
            operation.ConversationId,
            operation.Id,
            cancellationToken).ConfigureAwait(false);
        if (transcriptEvidence?.Assistant is { } assistant)
        {
            var succeeded = await evidenceSink.CompleteTranscriptAsync(
                operation.Id,
                assistant.CreatedAtUtc,
                transcriptEvidence.State.TranscriptRevision,
                assistant.EntryId,
                cancellationToken).ConfigureAwait(false);
            return await BuildDetailsAsync(
                succeeded,
                MapAssistant(operation.Id, assistant),
                cancellationToken).ConfigureAwait(false);
        }

        if (transcriptEvidence?.HasExactActiveTurn == true)
        {
            var recovery = await evidenceSink.RequireRecoveryAsync(
                operation.Id,
                LlmChatErrorCodes.OperationRecoveryRequired,
                cancellationToken).ConfigureAwait(false);
            return await BuildDetailsAsync(recovery, cancellationToken).ConfigureAwait(false);
        }

        var invocations = await invocationRepository.ListAsync(operation.Id, cancellationToken).ConfigureAwait(false);
        var lastInvocation = invocations.LastOrDefault();
        LlmChatOperation reconciled;
        if (lastInvocation?.Outcome == LlmChatInvocationOutcome.Failed)
        {
            reconciled = await evidenceSink.CompleteFailureAsync(
                operation.Id,
                lastInvocation.CompletedAtUtc,
                string.IsNullOrWhiteSpace(lastInvocation.FailureCode)
                    ? LlmChatErrorCodes.ProviderUnavailable
                    : lastInvocation.FailureCode,
                cancellationToken).ConfigureAwait(false);
        }
        else if (lastInvocation?.Outcome == LlmChatInvocationOutcome.Cancelled ||
                 operation.Status == LlmChatOperationStatus.CancellationRequested &&
                 operation.ProviderDispatchStartedAtUtc is null)
        {
            reconciled = await evidenceSink.CompleteCancellationAsync(
                operation.Id,
                lastInvocation?.CompletedAtUtc ?? timeProvider.GetUtcNow(),
                cancellationToken).ConfigureAwait(false);
        }
        else if (operation.ProviderDispatchStartedAtUtc is not null)
        {
            reconciled = await evidenceSink.RequireRecoveryAsync(
                operation.Id,
                LlmChatErrorCodes.OperationRecoveryRequired,
                cancellationToken).ConfigureAwait(false);
        }
        else
        {
            reconciled = await evidenceSink.CompleteFailureAsync(
                operation.Id,
                timeProvider.GetUtcNow(),
                LlmChatErrorCodes.ProviderUnavailable,
                cancellationToken).ConfigureAwait(false);
        }

        return await BuildDetailsAsync(reconciled, transcriptEvidence, invocations, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<Result<LlmChatOperationDetails>> BuildDetailsAsync(
        LlmChatOperation operation,
        CancellationToken cancellationToken)
    {
        var evidence = await conversationEngine.InspectTurnAsync(
            operation.ConversationId,
            operation.Id,
            cancellationToken).ConfigureAwait(false);
        return await BuildDetailsAsync(operation, evidence, null, cancellationToken).ConfigureAwait(false);
    }

    private Task<Result<LlmChatOperationDetails>> BuildDetailsAsync(
        LlmChatOperation operation,
        LlmChatAssistantMessage assistant,
        CancellationToken cancellationToken)
        => BuildDetailsAsync(operation, null, null, cancellationToken, assistant);

    private async Task<Result<LlmChatOperationDetails>> BuildDetailsAsync(
        LlmChatOperation operation,
        LlmChatConversationTurnEvidence? evidence,
        IReadOnlyList<LlmChatInvocationRecord>? knownInvocations,
        CancellationToken cancellationToken,
        LlmChatAssistantMessage? knownAssistant = null)
    {
        var invocations = knownInvocations ??
            await invocationRepository.ListAsync(operation.Id, cancellationToken).ConfigureAwait(false);
        var assistant = knownAssistant ?? (evidence?.Assistant is { } item
            ? MapAssistant(operation.Id, item)
            : null);
        return Result<LlmChatOperationDetails>.Success(new LlmChatOperationDetails(
            operation,
            assistant,
            invocations));
    }

    private async Task<LlmChatOperation> RequireOperationAsync(
        LlmChatOperationId operationId,
        CancellationToken cancellationToken)
        => await operationRepository.TryGetAsync(operationId, cancellationToken).ConfigureAwait(false)
           ?? throw new InvalidOperationException("The admitted LLM Chat operation no longer exists.");

    private static LlmChatAssistantMessage MapAssistant(
        LlmChatOperationId operationId,
        LlmChatAssistantTurnEvidence assistant)
        => new(
            assistant.EntryId,
            operationId,
            assistant.Text,
            assistant.Model,
            assistant.Usage,
            assistant.CreatedAtUtc);

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
            "Unexpected LLM Chat send failure of type {ExceptionType} for operation {OperationId} and conversation {ConversationId}.",
            exception.GetType().FullName,
            command.OperationId.Value,
            command.ConversationId.Value);
}
