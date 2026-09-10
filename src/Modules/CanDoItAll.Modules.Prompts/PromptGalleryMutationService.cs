using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Prompts;

public sealed class PromptDraftCreationPreparation {
    internal PromptDraftCreationPreparation(PromptDraftSaveReceipt receipt, Guid? projectId, string title) {
        Receipt = receipt;
        ProjectId = projectId;
        Title = title;
    }

    public PromptDraftSaveReceipt Receipt { get; }
    public Guid? ProjectId { get; }
    public string Title { get; }
}

public interface IPromptGalleryMutationService {
    Task<Result<PromptDraftCreationPreparation>> StageCreateDraftAsync(PromptGalleryDraft draft, CancellationToken cancellationToken = default);
    Task CompleteDraftCreationAsync(PromptDraftCreationPreparation preparation, CancellationToken cancellationToken = default);
}

public sealed class PromptGalleryMutationService(
    PromptsService service,
    IDbContextFactory<PromptsDbContext> factory,
    DbContextOptions<PromptsDbContext> options,
    CoordinatedDatabaseTransaction transactions) : IPromptGalleryMutationService {
    public async Task<Result<PromptDraftCreationPreparation>> StageCreateDraftAsync(PromptGalleryDraft draft,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(draft);
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
        return Result<PromptDraftCreationPreparation>.Success(new(new(entity.Id, entity.UpdatedAtUtc), entity.ProjectId, entity.Title));
    }

    public async Task CompleteDraftCreationAsync(PromptDraftCreationPreparation preparation,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(preparation);
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        if (!await context.Set<PromptArtifact>().AnyAsync(item => item.Id == preparation.Receipt.PromptArtifactId, cancellationToken)) {
            throw new InvalidOperationException("The Prompt Gallery creation must commit before its follow-up work can run.");
        }
        await service.NotifyDraftSaveAsync(preparation.Receipt.PromptArtifactId, preparation.ProjectId, preparation.Title,
            isNew: true, cancellationToken);
    }
}
