using CanDoItAll.Modules.LlmChats.Common;
using CanDoItAll.Modules.LlmChats.Conversations;
using CanDoItAll.Modules.LlmChats.Definitions;
using CanDoItAll.Modules.LlmChats.Ports;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.LlmChats.Application;

public sealed class LlmChatConversationApplicationService(
    ILlmChatDefinitionRepository definitionRepository,
    ILlmChatConversationRepository conversationRepository,
    ILlmChatUnitOfWork unitOfWork,
    ILlmChatConversationEngine conversationEngine,
    TimeProvider timeProvider) : ILlmChatConversationApplicationService
{
    public async Task<Result<LlmChatConversationDetails>> CreateAsync(
        CreateLlmChatConversationCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var definition = await definitionRepository.TryGetAsync(command.DefinitionId, cancellationToken)
            .ConfigureAwait(false);
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
            cancellationToken).ConfigureAwait(false);
        if (revision is null)
        {
            return Result<LlmChatConversationDetails>.Failure(LlmChatErrors.StorageCorrupted());
        }

        try
        {
            return await unitOfWork.ExecuteAsync(async transactionCancellationToken =>
            {
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
    }

    public async Task<Result<LlmChatConversationDetails>> ArchiveAsync(
        ArchiveLlmChatConversationCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var current = await conversationRepository.TryGetAsync(command.ConversationId, cancellationToken)
            .ConfigureAwait(false);
        if (current is null)
        {
            return Result<LlmChatConversationDetails>.Failure(LlmChatErrors.ConversationNotFound());
        }

        if (current.ConcurrencyToken != command.ExpectedConcurrencyToken)
        {
            return Result<LlmChatConversationDetails>.Failure(Error.Failure(
                "The LLM Chat conversation changed after it was read.",
                LlmChatErrorCodes.StorageConflict));
        }

        var transcript = await conversationEngine.TryGetAsync(current.Id, cancellationToken).ConfigureAwait(false);
        if (transcript is null)
        {
            return Result<LlmChatConversationDetails>.Failure(LlmChatErrors.StorageCorrupted());
        }

        if (current.Status == LlmChatConversationStatus.Archived)
        {
            return await BuildDetailsAsync(current, transcript, cancellationToken).ConfigureAwait(false);
        }

        return await unitOfWork.ExecuteAsync(async transactionCancellationToken =>
        {
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
        var conversation = await conversationRepository.TryGetAsync(conversationId, cancellationToken)
            .ConfigureAwait(false);
        if (conversation is null)
        {
            return Result<LlmChatConversationDetails>.Failure(LlmChatErrors.ConversationNotFound());
        }

        var transcript = await conversationEngine.TryGetAsync(conversation.Id, cancellationToken).ConfigureAwait(false);
        return transcript is null
            ? Result<LlmChatConversationDetails>.Failure(LlmChatErrors.StorageCorrupted())
            : await BuildDetailsAsync(conversation, transcript, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<LlmChatConversationDetails>> GetAsync(
        LlmChatConversationId conversationId,
        LlmChatTranscriptQuery transcriptQuery,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transcriptQuery);
        var conversation = await conversationRepository.TryGetAsync(conversationId, cancellationToken)
            .ConfigureAwait(false);
        if (conversation is null)
        {
            return Result<LlmChatConversationDetails>.Failure(LlmChatErrors.ConversationNotFound());
        }

        var transcript = await conversationEngine.TryGetTranscriptPageAsync(
            conversation.Id,
            transcriptQuery.Take,
            transcriptQuery.Offset,
            cancellationToken).ConfigureAwait(false);
        if (transcript is null)
        {
            return Result<LlmChatConversationDetails>.Failure(LlmChatErrors.StorageCorrupted());
        }

        var definition = await definitionRepository.TryGetAsync(conversation.DefinitionId, cancellationToken)
            .ConfigureAwait(false);
        return definition is null
            ? Result<LlmChatConversationDetails>.Failure(LlmChatErrors.StorageCorrupted())
            : Result<LlmChatConversationDetails>.Success(new LlmChatConversationDetails(
                conversation,
                definition.Name,
                transcript.State,
                transcript.Entries,
                transcript.NextOffset));
    }

    public async Task<Result<IReadOnlyList<LlmChatConversationDetails>>> ListAsync(
        LlmChatConversationQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var conversations = await conversationRepository
            .ListPageAsync(query.Take, query.Offset, query.DefinitionId, cancellationToken)
            .ConfigureAwait(false);
        var details = new List<LlmChatConversationDetails>(conversations.Count);
        foreach (var conversation in conversations)
        {
            var transcript = await conversationEngine.TryGetAsync(conversation.Id, cancellationToken).ConfigureAwait(false);
            if (transcript is null)
            {
                return Result<IReadOnlyList<LlmChatConversationDetails>>.Failure(LlmChatErrors.StorageCorrupted());
            }

            var detail = await BuildDetailsAsync(conversation, transcript, cancellationToken).ConfigureAwait(false);
            if (detail.IsFailure)
            {
                return Result<IReadOnlyList<LlmChatConversationDetails>>.Failure(detail.Errors);
            }

            details.Add(detail.Value!);
        }

        return Result<IReadOnlyList<LlmChatConversationDetails>>.Success(details);
    }

    public async Task<Result<LlmChatPage<LlmChatConversationDetails>>> ListPageAsync(
        LlmChatConversationQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var conversations = await conversationRepository
            .ListPageAsync(checked(query.Take + 1), query.Offset, query.DefinitionId, cancellationToken)
            .ConfigureAwait(false);
        var hasMore = conversations.Count > query.Take;
        var pageConversations = conversations.Take(query.Take).ToArray();
        var details = new List<LlmChatConversationDetails>(pageConversations.Length);
        foreach (var conversation in pageConversations)
        {
            var transcript = await conversationEngine.TryGetAsync(conversation.Id, cancellationToken).ConfigureAwait(false);
            if (transcript is null)
            {
                return Result<LlmChatPage<LlmChatConversationDetails>>.Failure(LlmChatErrors.StorageCorrupted());
            }

            var detail = await BuildDetailsAsync(conversation, transcript, cancellationToken).ConfigureAwait(false);
            if (detail.IsFailure)
            {
                return Result<LlmChatPage<LlmChatConversationDetails>>.Failure(detail.Errors);
            }

            details.Add(detail.Value!);
        }

        return Result<LlmChatPage<LlmChatConversationDetails>>.Success(new LlmChatPage<LlmChatConversationDetails>(
            details,
            hasMore ? checked(query.Offset + query.Take) : null));
    }

    private async Task<Result<LlmChatConversationDetails>> BuildDetailsAsync(
        LlmChatConversation conversation,
        LlmChatConversationEngineState transcript,
        CancellationToken cancellationToken)
    {
        var definition = await definitionRepository.TryGetAsync(conversation.DefinitionId, cancellationToken)
            .ConfigureAwait(false);
        return definition is null
            ? Result<LlmChatConversationDetails>.Failure(LlmChatErrors.StorageCorrupted())
            : Result<LlmChatConversationDetails>.Success(new LlmChatConversationDetails(
                conversation,
                definition.Name,
                transcript));
    }
}
