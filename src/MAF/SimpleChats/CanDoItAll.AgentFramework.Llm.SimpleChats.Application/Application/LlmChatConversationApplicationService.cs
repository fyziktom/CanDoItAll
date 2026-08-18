using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Conversations;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Definitions;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Ports;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Application;

public sealed class LlmChatConversationApplicationService(
    ILlmChatDefinitionRepository definitionRepository,
    ILlmChatConversationRepository conversationRepository,
    ILlmChatConversationReadStore readStore,
    ILlmChatTurnStateRepository turnStateRepository,
    ILlmChatUnitOfWork unitOfWork,
    ILlmChatConversationEngine conversationEngine,
    TimeProvider timeProvider) : ILlmChatConversationApplicationService
{
    public async Task<Result<LlmChatConversationDetails>> CreateAsync(
        CreateLlmChatConversationCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        try
        {
            return await unitOfWork.ExecuteAsync(async transactionCancellationToken =>
            {
                var definition = await definitionRepository.TryGetForUpdateAsync(
                    command.DefinitionId,
                    transactionCancellationToken).ConfigureAwait(false);
                if (definition is null)
                {
                    return Result<LlmChatConversationDetails>.Failure(LlmChatErrors.DefinitionNotFound());
                }

                if (definition.Status != LlmChatDefinitionStatus.Active)
                {
                    return Result<LlmChatConversationDetails>.Failure(LlmChatErrors.DefinitionNotActive());
                }

                var revision = await definitionRepository.TryGetRevisionAsync(
                    definition.Id,
                    definition.CurrentRevision,
                    transactionCancellationToken).ConfigureAwait(false);
                if (revision is null)
                {
                    return Result<LlmChatConversationDetails>.Failure(LlmChatErrors.StorageCorrupted());
                }

                var id = LlmChatConversationId.New();
                var title = LlmChatConversationTitlePolicy.Normalize(command.Title);
                var now = timeProvider.GetUtcNow();
                var conversation = new LlmChatConversation(
                    id,
                    definition.Id,
                    revision.Revision,
                    title,
                    LlmChatConversationStatus.Active,
                    command.Origin,
                    now,
                    now,
                    0);
                await conversationRepository.CreateAsync(conversation, transactionCancellationToken).ConfigureAwait(false);
                var transcript = await conversationEngine.CreateAsync(
                    id,
                    revision,
                    title,
                    transactionCancellationToken).ConfigureAwait(false);
                return Result<LlmChatConversationDetails>.Success(new LlmChatConversationDetails(
                    conversation,
                    definition.Name,
                    ToProviderModel(revision),
                    transcript));
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (ArgumentException exception)
        {
            return Result<LlmChatConversationDetails>.Failure(LlmChatErrors.InvalidRequest(exception.Message));
        }
    }

    public async Task<Result<LlmChatConversationDetails>> RenameAsync(
        RenameLlmChatConversationCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var current = await conversationRepository.TryGetAsync(command.ConversationId, cancellationToken)
            .ConfigureAwait(false);
        if (current is null)
        {
            return Result<LlmChatConversationDetails>.Failure(LlmChatErrors.ConversationNotFound());
        }

        if (current.Status == LlmChatConversationStatus.Archived)
        {
            return Result<LlmChatConversationDetails>.Failure(LlmChatErrors.ConversationArchived());
        }

        if (current.ConcurrencyToken != command.ExpectedConcurrencyToken)
        {
            return Result<LlmChatConversationDetails>.Failure(Error.Failure(
                "The LLM Chat conversation changed after it was read.",
                LlmChatErrorCodes.StorageConflict));
        }

        try
        {
            return await unitOfWork.ExecuteAsync(async transactionCancellationToken =>
            {
                var title = LlmChatConversationTitlePolicy.Normalize(command.Title);
                var updated = new LlmChatConversation(
                    current.Id,
                    current.DefinitionId,
                    current.DefinitionRevision,
                    title,
                    current.Status,
                    current.Origin,
                    current.CreatedAtUtc,
                    timeProvider.GetUtcNow(),
                    checked(current.ConcurrencyToken + 1));
                await conversationRepository.ReplaceAsync(
                    updated,
                    command.ExpectedConcurrencyToken,
                    transactionCancellationToken).ConfigureAwait(false);
                var transcript = await conversationEngine.RenameAsync(
                    current.Id,
                    title,
                    command.ExpectedTranscriptRevision,
                    transactionCancellationToken).ConfigureAwait(false);
                return await BuildDetailsAsync(updated, transcript, transactionCancellationToken).ConfigureAwait(false);
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (ArgumentException exception)
        {
            return Result<LlmChatConversationDetails>.Failure(LlmChatErrors.InvalidRequest(exception.Message));
        }
        catch (LlmChatPersistenceConcurrencyException exception)
            when (exception.Resource is LlmChatConcurrencyResource.Conversation)
        {
            return Result<LlmChatConversationDetails>.Failure(LlmChatErrors.StorageConflict());
        }
    }

    public async Task<Result<LlmChatConversationDetails>> ArchiveAsync(
        ArchiveLlmChatConversationCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return await unitOfWork.ExecuteAsync(async transactionCancellationToken =>
        {
            var turnState = await turnStateRepository.LockAsync(
                command.ConversationId,
                transactionCancellationToken).ConfigureAwait(false);
            if (!turnState.Exists)
            {
                return Result<LlmChatConversationDetails>.Failure(LlmChatErrors.ConversationNotFound());
            }

            var current = await conversationRepository.TryGetAsync(command.ConversationId, transactionCancellationToken)
                .ConfigureAwait(false);
            if (current is null)
            {
                return Result<LlmChatConversationDetails>.Failure(LlmChatErrors.StorageCorrupted());
            }

            if (current.ConcurrencyToken != command.ExpectedConcurrencyToken)
            {
                return Result<LlmChatConversationDetails>.Failure(Error.Failure(
                    "The LLM Chat conversation changed after it was read.",
                    LlmChatErrorCodes.StorageConflict));
            }

            var transcript = await conversationEngine.TryGetAsync(current.Id, transactionCancellationToken)
                .ConfigureAwait(false);
            if (transcript is null)
            {
                return Result<LlmChatConversationDetails>.Failure(LlmChatErrors.StorageCorrupted());
            }

            if (current.Status == LlmChatConversationStatus.Archived)
            {
                return await BuildDetailsAsync(current, transcript, transactionCancellationToken)
                    .ConfigureAwait(false);
            }

            if (turnState.HasActiveTurn || turnState.HasNonterminalOperation)
            {
                return Result<LlmChatConversationDetails>.Failure(LlmChatErrors.ActiveTurnConflict());
            }

            var archived = new LlmChatConversation(
                current.Id,
                current.DefinitionId,
                current.DefinitionRevision,
                current.Title,
                LlmChatConversationStatus.Archived,
                current.Origin,
                current.CreatedAtUtc,
                timeProvider.GetUtcNow(),
                checked(current.ConcurrencyToken + 1));
            await conversationRepository.ReplaceAsync(
                archived,
                command.ExpectedConcurrencyToken,
                transactionCancellationToken).ConfigureAwait(false);
            return await BuildDetailsAsync(archived, transcript, transactionCancellationToken).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<LlmChatConversationDetails>> GetAsync(
        LlmChatConversationId conversationId,
        CancellationToken cancellationToken = default)
    {
        var conversation = await readStore.TryGetAsync(conversationId, cancellationToken).ConfigureAwait(false);
        return conversation is null
            ? Result<LlmChatConversationDetails>.Failure(LlmChatErrors.ConversationNotFound())
            : Result<LlmChatConversationDetails>.Success(Map(conversation));
    }

    public async Task<Result<LlmChatConversationDetails>> GetAsync(
        LlmChatConversationId conversationId,
        LlmChatTranscriptQuery transcriptQuery,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transcriptQuery);
        var transcript = await readStore.TryGetTranscriptPageAsync(
            conversationId,
            transcriptQuery.Take,
            transcriptQuery.Cursor,
            cancellationToken).ConfigureAwait(false);
        if (transcript is null)
        {
            return Result<LlmChatConversationDetails>.Failure(LlmChatErrors.ConversationNotFound());
        }

        return Result<LlmChatConversationDetails>.Success(new LlmChatConversationDetails(
            transcript.Conversation.Conversation,
            transcript.Conversation.DefinitionName,
            transcript.Conversation.ProviderModel,
            transcript.Conversation.Transcript,
            transcript.Entries,
            transcript.NextCursor));
    }

    public async Task<Result<IReadOnlyList<LlmChatConversationDetails>>> ListAsync(
        LlmChatConversationQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var page = await readStore
            .ListPageAsync(query.Take, query.Cursor, query.DefinitionId, cancellationToken)
            .ConfigureAwait(false);
        return Result<IReadOnlyList<LlmChatConversationDetails>>.Success([.. page.Items.Select(Map)]);
    }

    public async Task<Result<LlmChatPage<LlmChatConversationDetails, LlmChatConversationCursor>>> ListPageAsync(
        LlmChatConversationQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var page = await readStore
            .ListPageAsync(query.Take, query.Cursor, query.DefinitionId, cancellationToken)
            .ConfigureAwait(false);
        return Result<LlmChatPage<LlmChatConversationDetails, LlmChatConversationCursor>>.Success(
            new LlmChatPage<LlmChatConversationDetails, LlmChatConversationCursor>(
                [.. page.Items.Select(Map)],
                page.NextCursor));
    }

    private static LlmChatConversationDetails Map(LlmChatConversationReadModel model)
        => new(model.Conversation, model.DefinitionName, model.ProviderModel, model.Transcript);

    private async Task<Result<LlmChatConversationDetails>> BuildDetailsAsync(
        LlmChatConversation conversation,
        LlmChatConversationEngineState transcript,
        CancellationToken cancellationToken)
    {
        var definition = await definitionRepository.TryGetAsync(conversation.DefinitionId, cancellationToken)
            .ConfigureAwait(false);
        var revision = await definitionRepository.TryGetRevisionAsync(
            conversation.DefinitionId,
            conversation.DefinitionRevision,
            cancellationToken).ConfigureAwait(false);
        return definition is null || revision is null
            ? Result<LlmChatConversationDetails>.Failure(LlmChatErrors.StorageCorrupted())
            : Result<LlmChatConversationDetails>.Success(new LlmChatConversationDetails(
                conversation,
                definition.Name,
                ToProviderModel(revision),
                transcript));
    }

    private static LlmConversationProviderSnapshot ToProviderModel(LlmChatDefinitionRevision revision)
        => new(
            revision.ProviderProfileId,
            revision.ProviderName,
            revision.ProviderKind,
            revision.Model);
}
