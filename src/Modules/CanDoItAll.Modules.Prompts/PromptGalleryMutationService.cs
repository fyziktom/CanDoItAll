using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Editing;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Prompts;

public sealed class PromptDraftCreationPreparation {
    private int completed;
    internal PromptDraftCreationPreparation(PromptDraftSaveReceipt receipt, Guid? projectId, string title, EditingOperationIdentity? origin) {
        Receipt = receipt;
        ProjectId = projectId;
        Title = title;
        Origin = origin;
    }

    public PromptDraftSaveReceipt Receipt { get; }
    public Guid? ProjectId { get; }
    public string Title { get; }
    internal EditingOperationIdentity? Origin { get; }

    internal bool TryComplete() => Interlocked.CompareExchange(ref completed, 1, 0) == 0;
}

public interface IPromptGalleryMutationService {
    Task<Result<PromptDraftCreationPreparation>> StageCreateDraftAsync(PromptGalleryDraft draft, CancellationToken cancellationToken = default);
    Task CompleteDraftCreationAsync(PromptDraftCreationPreparation preparation, CancellationToken cancellationToken = default);
    Task<Result<PromptVersionPreparation>> StageUpdateAndVersionAsync(PromptGalleryDraft draft, PromptImportVersionRequest version, CancellationToken cancellationToken = default);
    Task<Result<PromptVersionPreparation>> StageCreateImportedVersionAsync(PromptGalleryImportRequest request, CancellationToken cancellationToken = default);
    Task CompleteVersionAsync(PromptVersionPreparation preparation, CancellationToken cancellationToken = default);
}

public sealed class PromptVersionPreparation {
    private int completed;

    internal PromptVersionPreparation(PromptsService.PromptVersionMutationState state,
        PromptGalleryItemDetails item, EditingOperationIdentity? origin) {
        State = state;
        Item = item;
        Origin = origin;
    }

    public PromptVersionSnapshot Snapshot => State.Snapshot;
    public PromptGalleryItemDetails Item { get; }
    internal PromptsService.PromptVersionMutationState State { get; }
    internal EditingOperationIdentity? Origin { get; }
    internal bool TryComplete() => Interlocked.CompareExchange(ref completed, 1, 0) == 0;
}

public sealed class PromptGalleryMutationService(
    PromptsService service,
    IDbContextFactory<PromptsDbContext> factory,
    DbContextOptions<PromptsDbContext> options,
    CoordinatedDatabaseTransaction transactions) : IPromptGalleryMutationService {
    public async Task<Result<PromptVersionPreparation>> StageUpdateAndVersionAsync(PromptGalleryDraft draft,
        PromptImportVersionRequest version, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(version);
        if (!draft.Id.HasValue) {
            return Result<PromptVersionPreparation>.Failure(Error.Validation(
                "An enlisted Prompt update requires an existing item.", "prompts.gallery.update-only"));
        }
        var origin = service.CurrentOperation;
        await using var context = await transactions.CreateEnlistedAsync(options, static value => new PromptsDbContext(value), cancellationToken);
        PromptGalleryPersistence.DemandSnapshotIsolation(context);
        var saved = await service.SaveDraftInContextAsync(context, draft, cancellationToken);
        if (saved.IsFailure) {
            return Result<PromptVersionPreparation>.Failure(saved.Errors);
        }
        var result = await service.CreateVersionInContextAsync(context, saved.Value!.Artifact.Id,
            new(version.CreationReason, saved.Value.Artifact.UpdatedAtUtc, version.OutputFormat), cancellationToken);
        return await PrepareVersionAsync(context, result, origin, cancellationToken);
    }

    public async Task<Result<PromptVersionPreparation>> StageCreateImportedVersionAsync(PromptGalleryImportRequest request,
        CancellationToken cancellationToken = default) {
        var origin = service.CurrentOperation;
        await using var context = await transactions.CreateEnlistedAsync(options, static value => new PromptsDbContext(value), cancellationToken);
        PromptGalleryPersistence.DemandSnapshotIsolation(context);
        var result = await service.ImportVersionInContextAsync(context, request, createOnly: true, cancellationToken);
        return await PrepareVersionAsync(context, result, origin, cancellationToken);
    }

    private static async Task<Result<PromptVersionPreparation>> PrepareVersionAsync(PromptsDbContext context,
        Result<PromptsService.PromptVersionMutationState> result, EditingOperationIdentity? origin, CancellationToken cancellationToken) {
        if (result.IsFailure) {
            return Result<PromptVersionPreparation>.Failure(result.Errors);
        }
        if (result.Value!.Kind is not (PromptsService.PromptVersionMutationKind.Finalized or PromptsService.PromptVersionMutationKind.ImportedNew)) {
            throw new InvalidOperationException("An enlisted version needs a new immutable version as its commit witness.");
        }
        var item = await PromptGalleryPersistence.LoadItemDetailsAsync(context, result.Value!.Snapshot.PromptArtifactId, cancellationToken)
            ?? throw new InvalidOperationException("The staged Prompt version has no item.");
        return Result<PromptVersionPreparation>.Success(new(result.Value, item, origin));
    }

    public async Task CompleteVersionAsync(PromptVersionPreparation preparation, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(preparation);
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        if (!await context.Set<PromptVersion>().AnyAsync(item => item.Id == preparation.Snapshot.PromptVersionId &&
                item.PromptArtifactId == preparation.Snapshot.PromptArtifactId, cancellationToken)) {
            throw new InvalidOperationException("The Prompt version must commit before its follow-up work can run.");
        }
        if (preparation.TryComplete()) {
            await service.NotifyVersionMutationAsync(preparation.State, preparation.Origin, cancellationToken);
        }
    }

    public async Task<Result<PromptDraftCreationPreparation>> StageCreateDraftAsync(PromptGalleryDraft draft,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(draft);
        var origin = service.CurrentOperation;
        if (draft.Id.HasValue) {
            return Result<PromptDraftCreationPreparation>.Failure(Error.Validation(
                "An enlisted draft creation cannot update an existing Prompt Gallery item.", "prompts.gallery.creation-only"));
        }
        await using var context = await transactions.CreateEnlistedAsync(options, static value => new PromptsDbContext(value), cancellationToken);
        var saved = await service.SaveDraftInContextAsync(context, draft, cancellationToken);
        if (saved.IsFailure) {
            return Result<PromptDraftCreationPreparation>.Failure(saved.Errors);
        }
        var entity = saved.Value!.Artifact;
        return Result<PromptDraftCreationPreparation>.Success(new(new(entity.Id, entity.UpdatedAtUtc), entity.ProjectId, entity.Title, origin));
    }

    public async Task CompleteDraftCreationAsync(PromptDraftCreationPreparation preparation,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(preparation);
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        if (!await context.Set<PromptArtifact>().AnyAsync(item => item.Id == preparation.Receipt.PromptArtifactId, cancellationToken)) {
            throw new InvalidOperationException("The Prompt Gallery creation must commit before its follow-up work can run.");
        }
        if (!preparation.TryComplete()) {
            return;
        }
        await service.NotifyDraftSaveAsync(preparation.Receipt.PromptArtifactId, preparation.ProjectId, preparation.Title,
            isNew: true, cancellationToken, preparation.Origin);
    }
}
