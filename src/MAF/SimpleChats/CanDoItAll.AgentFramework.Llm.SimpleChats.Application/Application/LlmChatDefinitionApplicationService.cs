using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Definitions;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Ports;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Application;

public sealed class LlmChatDefinitionApplicationService(
    ILlmChatDefinitionRepository repository,
    ILlmChatDefinitionReadStore readStore,
    ILlmChatUnitOfWork unitOfWork,
    ILlmChatProviderResolver providerResolver,
    TimeProvider timeProvider,
    ILlmChatDefinitionCreateReceiptRepository createReceipts)
    : ILlmChatDefinitionApplicationService, ILlmChatDefinitionCreateReceiptService
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
                var details = await CreateCoreAsync(command, resolved.Value!, id, now, tags,
                    transactionCancellationToken).ConfigureAwait(false);
                return Result<LlmChatDefinitionDetails>.Success(details);
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (ArgumentException exception)
        {
            return Result<LlmChatDefinitionDetails>.Failure(LlmChatErrors.InvalidRequest(exception.Message));
        }
    }

    public async Task<Result<LlmChatDefinitionCreateResponse>> CreateOnceAsync(
        CreateLlmChatDefinitionOnceCommand command,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Key);
        CreateLlmChatDefinitionCommand snapshot;
        LlmChatDefinitionCreateFingerprint fingerprint;
        try {
            snapshot = LlmChatDefinitionCreateSemantics.Snapshot(command.Definition);
            fingerprint = LlmChatDefinitionCreateSemantics.Fingerprint(snapshot);
        } catch (ArgumentException exception) {
            return Result<LlmChatDefinitionCreateResponse>.Failure(LlmChatErrors.InvalidRequest(exception.Message));
        }

        LlmChatDefinitionId? attemptedDefinitionId = null;
        try {
            return await unitOfWork.ExecuteAsync(async token => {
                var existing = await createReceipts.TryGetReceiptAsync(command.Key, token).ConfigureAwait(false);
                if (existing is not null) {
                    return Replay(existing, fingerprint);
                }

                var id = LlmChatDefinitionId.New();
                var now = timeProvider.GetUtcNow();
                var createdAt = new DateTimeOffset(now.Ticks - now.Ticks % TimeSpan.TicksPerMicrosecond, TimeSpan.Zero);
                var receipt = new LlmChatDefinitionCreateReceipt(command.Key, id, new(1), 0, createdAt);
                var claim = new LlmChatDefinitionCreateClaim(receipt, fingerprint);
                if (!await createReceipts.TryClaimAsync(claim, token).ConfigureAwait(false)) {
                    var winner = await createReceipts.TryGetReceiptAsync(command.Key, token).ConfigureAwait(false)
                        ?? throw new InvalidOperationException("A competing definition create committed without its receipt.");
                    return Replay(winner, fingerprint);
                }

                attemptedDefinitionId = id;
                try {
                    var resolved = await providerResolver.ResolveAsync(snapshot.ProviderProfileId, snapshot.Model,
                        snapshot.Settings.ThinkingEffort, token).ConfigureAwait(false);
                    if (resolved.IsFailure) {
                        throw new DefinitionCreateRejectedException(resolved.Errors);
                    }

                    await CreateCoreAsync(snapshot, resolved.Value!, id, receipt.CreatedAtUtc, snapshot.Tags!, token)
                        .ConfigureAwait(false);
                } catch (ArgumentException exception) {
                    throw new DefinitionCreateRejectedException([LlmChatErrors.InvalidRequest(exception.Message)]);
                }

                return Result<LlmChatDefinitionCreateResponse>.Success(new(receipt, WasReplay: false));
            }, cancellationToken).ConfigureAwait(false);
        } catch (DefinitionCreateRejectedException exception) {
            return Result<LlmChatDefinitionCreateResponse>.Failure(exception.Errors);
        } finally {
            if (attemptedDefinitionId is { } id) {
                createReceipts.ForgetAttempt(id);
            }
        }
    }

    public async Task<Result<LlmChatDefinitionCreateReceipt?>> FindReceiptAsync(
        LlmChatDefinitionCreateKey key,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(key);
        var claim = await createReceipts.TryGetReceiptAsync(key, cancellationToken).ConfigureAwait(false);
        return Result<LlmChatDefinitionCreateReceipt?>.Success(claim?.Receipt);
    }

    private static Result<LlmChatDefinitionCreateResponse> Replay(
        LlmChatDefinitionCreateClaim claim,
        LlmChatDefinitionCreateFingerprint fingerprint)
        => claim.Fingerprint == fingerprint
            ? Result<LlmChatDefinitionCreateResponse>.Success(new(claim.Receipt, WasReplay: true))
            : Result<LlmChatDefinitionCreateResponse>.Failure(new Error(
                LlmChatErrorCodes.DefinitionCreateIntentConflict,
                "This definition create intent was already committed with a different request."));

    private sealed class DefinitionCreateRejectedException(IReadOnlyList<Error> errors)
        : Exception("The definition create was rejected after reserving its intent.") {
        public IReadOnlyList<Error> Errors { get; } = errors;
    }

    private async Task<LlmChatDefinitionDetails> CreateCoreAsync(
        CreateLlmChatDefinitionCommand command,
        LlmChatResolvedProvider resolved,
        LlmChatDefinitionId id,
        DateTimeOffset now,
        IReadOnlyList<string> tags,
        CancellationToken cancellationToken) {
        var revisionNumber = new LlmChatDefinitionRevisionNumber(1);
        var revision = CreateRevision(id, revisionNumber, command, resolved, now);
        var definition = new LlmChatDefinition(id, revision.Name, revision.Summary, revision.AvatarImageUrl,
            LlmChatDefinitionStatus.Draft, revisionNumber, now, now, 0);
        await repository.CreateAsync(definition, revision, cancellationToken).ConfigureAwait(false);
        await repository.ReplaceTagsAsync(id, tags, cancellationToken).ConfigureAwait(false);
        return new LlmChatDefinitionDetails(definition, revision, tags);
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
        catch (LlmChatPersistenceConcurrencyException exception)
            when (exception.Resource is LlmChatConcurrencyResource.Definition)
        {
            return Result<LlmChatDefinitionDetails>.Failure(LlmChatErrors.DefinitionConcurrencyConflict());
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

        try
        {
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
        catch (LlmChatPersistenceConcurrencyException exception)
            when (exception.Resource is LlmChatConcurrencyResource.Definition)
        {
            return Result<LlmChatDefinitionDetails>.Failure(LlmChatErrors.DefinitionConcurrencyConflict());
        }
    }

    public async Task<Result<LlmChatDefinitionDetails>> GetAsync(
        LlmChatDefinitionId definitionId,
        CancellationToken cancellationToken = default)
    {
        var definition = await readStore.TryGetAsync(definitionId, cancellationToken).ConfigureAwait(false);
        return definition is null
            ? Result<LlmChatDefinitionDetails>.Failure(LlmChatErrors.DefinitionNotFound())
            : Result<LlmChatDefinitionDetails>.Success(Map(definition));
    }

    public async Task<Result<LlmChatDefinitionRevision>> GetRevisionAsync(
        LlmChatDefinitionId definitionId,
        LlmChatDefinitionRevisionNumber revision,
        CancellationToken cancellationToken = default) {
        var definition = await repository.TryGetAsync(definitionId, cancellationToken).ConfigureAwait(false);
        if (definition is null) {
            return Result<LlmChatDefinitionRevision>.Failure(LlmChatErrors.DefinitionNotFound());
        }

        var original = await repository.TryGetRevisionAsync(definitionId, revision, cancellationToken).ConfigureAwait(false);
        return original is null || original.DefinitionId != definition.Id || original.Revision != revision ||
            revision.Value > definition.CurrentRevision.Value
            ? Result<LlmChatDefinitionRevision>.Failure(LlmChatErrors.StorageCorrupted())
            : Result<LlmChatDefinitionRevision>.Success(original);
    }

    public async Task<Result<IReadOnlyList<LlmChatDefinitionDetails>>> ListAsync(
        LlmChatDefinitionQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var page = await readStore
            .ListPageAsync(query.Take, query.Cursor, query.Status, query.SearchText, query.Tags, cancellationToken)
            .ConfigureAwait(false);
        return Result<IReadOnlyList<LlmChatDefinitionDetails>>.Success([.. page.Items.Select(Map)]);
    }

    public async Task<Result<LlmChatPage<LlmChatDefinitionDetails, LlmChatDefinitionCursor>>> ListPageAsync(
        LlmChatDefinitionQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var page = await readStore
            .ListPageAsync(query.Take, query.Cursor, query.Status, query.SearchText, query.Tags, cancellationToken)
            .ConfigureAwait(false);
        return Result<LlmChatPage<LlmChatDefinitionDetails, LlmChatDefinitionCursor>>.Success(
            new LlmChatPage<LlmChatDefinitionDetails, LlmChatDefinitionCursor>(
                [.. page.Items.Select(Map)],
                page.NextCursor));
    }

    private static LlmChatDefinitionDetails Map(LlmChatDefinitionReadModel model)
        => new(model.Definition, model.Revision, model.Tags);

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
