using CanDoItAll.Modules.LlmChats.Common;
using CanDoItAll.Modules.LlmChats.Definitions;
using CanDoItAll.Modules.LlmChats.Ports;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.LlmChats.Application;

public sealed class LlmChatDefinitionApplicationService(
    ILlmChatDefinitionRepository repository,
    ILlmChatUnitOfWork unitOfWork,
    ILlmChatProviderResolver providerResolver,
    TimeProvider timeProvider) : ILlmChatDefinitionApplicationService
{
    public async Task<Result<LlmChatDefinitionDetails>> CreateAsync(
        CreateLlmChatDefinitionCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var resolved = await providerResolver.ResolveAsync(
            command.ProviderProfileId,
            command.Model,
            command.Settings.ThinkingEffort,
            cancellationToken).ConfigureAwait(false);
        if (resolved.IsFailure)
        {
            return Result<LlmChatDefinitionDetails>.Failure(resolved.Errors);
        }

        try
        {
            return await unitOfWork.ExecuteAsync(async transactionCancellationToken =>
            {
                var now = timeProvider.GetUtcNow();
                var tags = LlmChatDefinitionValidation.NormalizeTags(command.Tags);
                var id = LlmChatDefinitionId.New();
                var revisionNumber = new LlmChatDefinitionRevisionNumber(1);
                var revision = CreateRevision(id, revisionNumber, command, resolved.Value!, now);
                var definition = new LlmChatDefinition(
                    id,
                    revision.Name,
                    revision.Summary,
                    revision.AvatarImageUrl,
                    LlmChatDefinitionStatus.Draft,
                    revisionNumber,
                    now,
                    now,
                    0);
                await repository.CreateAsync(definition, revision, transactionCancellationToken).ConfigureAwait(false);
                await repository.ReplaceTagsAsync(id, tags, transactionCancellationToken).ConfigureAwait(false);
                return Result<LlmChatDefinitionDetails>.Success(new LlmChatDefinitionDetails(definition, revision, tags));
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (ArgumentException exception)
        {
            return Result<LlmChatDefinitionDetails>.Failure(LlmChatErrors.InvalidRequest(exception.Message));
        }
    }

    public async Task<Result<LlmChatDefinitionDetails>> UpdateAsync(
        UpdateLlmChatDefinitionCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var current = await repository.TryGetAsync(command.DefinitionId, cancellationToken).ConfigureAwait(false);
        if (current is null)
        {
            return Result<LlmChatDefinitionDetails>.Failure(LlmChatErrors.DefinitionNotFound());
        }

        if (current.Status == LlmChatDefinitionStatus.Archived)
        {
            return Result<LlmChatDefinitionDetails>.Failure(
                LlmChatErrors.DefinitionNotActive("An archived LLM Chat definition is read-only."));
        }

        if (current.ConcurrencyToken != command.ExpectedConcurrencyToken)
        {
            return Result<LlmChatDefinitionDetails>.Failure(LlmChatErrors.DefinitionConcurrencyConflict());
        }

        var resolved = await providerResolver.ResolveAsync(
            command.ProviderProfileId,
            command.Model,
            command.Settings.ThinkingEffort,
            cancellationToken).ConfigureAwait(false);
        if (resolved.IsFailure)
        {
            return Result<LlmChatDefinitionDetails>.Failure(resolved.Errors);
        }

        try
        {
            return await unitOfWork.ExecuteAsync(async transactionCancellationToken =>
            {
                var now = timeProvider.GetUtcNow();
                var tags = LlmChatDefinitionValidation.NormalizeTags(command.Tags);
                var nextRevision = current.CurrentRevision.Next();
                var revision = CreateRevision(command, nextRevision, resolved.Value!, now);
                var updated = new LlmChatDefinition(
                    current.Id,
                    revision.Name,
                    revision.Summary,
                    revision.AvatarImageUrl,
                    current.Status,
                    nextRevision,
                    current.CreatedAtUtc,
                    now,
                    checked(current.ConcurrencyToken + 1));
                await repository.ReplaceAsync(
                    updated,
                    command.ExpectedConcurrencyToken,
                    revision,
                    transactionCancellationToken).ConfigureAwait(false);
                await repository.ReplaceTagsAsync(updated.Id, tags, transactionCancellationToken).ConfigureAwait(false);
                return Result<LlmChatDefinitionDetails>.Success(new LlmChatDefinitionDetails(updated, revision, tags));
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (ArgumentException exception)
        {
            return Result<LlmChatDefinitionDetails>.Failure(LlmChatErrors.InvalidRequest(exception.Message));
        }
    }

    public async Task<Result<LlmChatDefinitionDetails>> ChangeStatusAsync(
        ChangeLlmChatDefinitionStatusCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var current = await repository.TryGetAsync(command.DefinitionId, cancellationToken).ConfigureAwait(false);
        if (current is null)
        {
            return Result<LlmChatDefinitionDetails>.Failure(LlmChatErrors.DefinitionNotFound());
        }

        if (current.ConcurrencyToken != command.ExpectedConcurrencyToken)
        {
            return Result<LlmChatDefinitionDetails>.Failure(LlmChatErrors.DefinitionConcurrencyConflict());
        }

        if (current.Status == command.Status)
        {
            return await GetAsync(current.Id, cancellationToken).ConfigureAwait(false);
        }

        if (!CanTransition(current.Status, command.Status))
        {
            return Result<LlmChatDefinitionDetails>.Failure(LlmChatErrors.DefinitionNotActive(
                $"The definition cannot transition from {current.Status} to {command.Status}."));
        }

        var revision = await repository.TryGetRevisionAsync(
            current.Id,
            current.CurrentRevision,
            cancellationToken).ConfigureAwait(false);
        if (revision is null)
        {
            return Result<LlmChatDefinitionDetails>.Failure(LlmChatErrors.StorageCorrupted());
        }

        return await unitOfWork.ExecuteAsync(async transactionCancellationToken =>
        {
            var updated = new LlmChatDefinition(
                current.Id,
                current.Name,
                current.Summary,
                current.AvatarImageUrl,
                command.Status,
                current.CurrentRevision,
                current.CreatedAtUtc,
                timeProvider.GetUtcNow(),
                checked(current.ConcurrencyToken + 1));
            await repository.ReplaceAsync(
                updated,
                command.ExpectedConcurrencyToken,
                appendedRevision: null,
                cancellationToken: transactionCancellationToken).ConfigureAwait(false);
            var tags = await repository.ListTagsAsync(updated.Id, transactionCancellationToken).ConfigureAwait(false);
            return Result<LlmChatDefinitionDetails>.Success(new LlmChatDefinitionDetails(updated, revision, tags));
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<LlmChatDefinitionDetails>> GetAsync(
        LlmChatDefinitionId definitionId,
        CancellationToken cancellationToken = default)
    {
        var definition = await repository.TryGetAsync(definitionId, cancellationToken).ConfigureAwait(false);
        if (definition is null)
        {
            return Result<LlmChatDefinitionDetails>.Failure(LlmChatErrors.DefinitionNotFound());
        }

        var revision = await repository.TryGetRevisionAsync(
            definition.Id,
            definition.CurrentRevision,
            cancellationToken).ConfigureAwait(false);
        if (revision is null)
        {
            return Result<LlmChatDefinitionDetails>.Failure(LlmChatErrors.StorageCorrupted());
        }

        var tags = await repository.ListTagsAsync(definition.Id, cancellationToken).ConfigureAwait(false);
        return Result<LlmChatDefinitionDetails>.Success(new LlmChatDefinitionDetails(definition, revision, tags));
    }

    public async Task<Result<IReadOnlyList<LlmChatDefinitionDetails>>> ListAsync(
        LlmChatDefinitionQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var definitions = await repository
            .ListPageAsync(query.Take, query.Offset, query.Status, cancellationToken)
            .ConfigureAwait(false);
        var details = new List<LlmChatDefinitionDetails>(definitions.Count);
        foreach (var definition in definitions)
        {
            var revision = await repository.TryGetRevisionAsync(
                definition.Id,
                definition.CurrentRevision,
                cancellationToken).ConfigureAwait(false);
            if (revision is null)
            {
                return Result<IReadOnlyList<LlmChatDefinitionDetails>>.Failure(LlmChatErrors.StorageCorrupted());
            }

            var tags = await repository.ListTagsAsync(definition.Id, cancellationToken).ConfigureAwait(false);
            details.Add(new LlmChatDefinitionDetails(definition, revision, tags));
        }

        return Result<IReadOnlyList<LlmChatDefinitionDetails>>.Success(details);
    }

    public async Task<Result<LlmChatPage<LlmChatDefinitionDetails>>> ListPageAsync(
        LlmChatDefinitionQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var requested = new LlmChatDefinitionQuery(query.Take, query.Status, query.Offset);
        var definitions = await repository
            .ListPageAsync(checked(query.Take + 1), query.Offset, query.Status, cancellationToken)
            .ConfigureAwait(false);
        var hasMore = definitions.Count > query.Take;
        var pageDefinitions = definitions.Take(query.Take).ToArray();
        var details = new List<LlmChatDefinitionDetails>(pageDefinitions.Length);
        foreach (var definition in pageDefinitions)
        {
            var revision = await repository.TryGetRevisionAsync(
                definition.Id,
                definition.CurrentRevision,
                cancellationToken).ConfigureAwait(false);
            if (revision is null)
            {
                return Result<LlmChatPage<LlmChatDefinitionDetails>>.Failure(LlmChatErrors.StorageCorrupted());
            }

            var tags = await repository.ListTagsAsync(definition.Id, cancellationToken).ConfigureAwait(false);
            details.Add(new LlmChatDefinitionDetails(definition, revision, tags));
        }

        return Result<LlmChatPage<LlmChatDefinitionDetails>>.Success(new LlmChatPage<LlmChatDefinitionDetails>(
            details,
            hasMore ? checked(requested.Offset + requested.Take) : null));
    }

    private static LlmChatDefinitionRevision CreateRevision(
        LlmChatDefinitionId id,
        LlmChatDefinitionRevisionNumber revision,
        CreateLlmChatDefinitionCommand command,
        LlmChatResolvedProvider provider,
        DateTimeOffset now)
        => new(
            id,
            revision,
            command.Name,
            command.Summary,
            command.AvatarImageUrl,
            command.SystemPrompt,
            provider.ProviderProfileId,
            provider.ProviderKind,
            provider.ProviderName,
            provider.Model,
            command.Settings,
            command.Timeout,
            command.ResponseFormat,
            now,
            command.RevisionReason);

    private static LlmChatDefinitionRevision CreateRevision(
        UpdateLlmChatDefinitionCommand command,
        LlmChatDefinitionRevisionNumber revision,
        LlmChatResolvedProvider provider,
        DateTimeOffset now)
        => new(
            command.DefinitionId,
            revision,
            command.Name,
            command.Summary,
            command.AvatarImageUrl,
            command.SystemPrompt,
            provider.ProviderProfileId,
            provider.ProviderKind,
            provider.ProviderName,
            provider.Model,
            command.Settings,
            command.Timeout,
            command.ResponseFormat,
            now,
            command.RevisionReason);

    private static bool CanTransition(LlmChatDefinitionStatus current, LlmChatDefinitionStatus target)
        => (current, target) switch
        {
            (LlmChatDefinitionStatus.Draft, LlmChatDefinitionStatus.Active or LlmChatDefinitionStatus.Archived) => true,
            (LlmChatDefinitionStatus.Active, LlmChatDefinitionStatus.Suspended or LlmChatDefinitionStatus.Archived) => true,
            (LlmChatDefinitionStatus.Suspended, LlmChatDefinitionStatus.Active or LlmChatDefinitionStatus.Archived) => true,
            _ => false
        };
}
