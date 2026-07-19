using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Prompts;

public sealed class PromptsService : IPromptGalleryService, IPromptGalleryImportService
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly IClock _clock;
    private readonly IActivityStream _activityStream;
    private readonly IPromptGallerySearchDriver _searchDriver;
    private readonly IPromptGalleryProjectionCoordinator _projectionCoordinator;
    private readonly PromptGalleryCompatibilityEvaluator _compatibilityEvaluator;
    private readonly ILogger<PromptsService> _logger;

    public PromptsService(
        IDbContextFactory<AppDbContext> dbContextFactory,
        IClock clock,
        IActivityStream activityStream,
        IPromptGallerySearchDriver searchDriver,
        IPromptGalleryProjectionCoordinator projectionCoordinator,
        PromptGalleryCompatibilityEvaluator compatibilityEvaluator,
        ILogger<PromptsService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _clock = clock;
        _activityStream = activityStream;
        _searchDriver = searchDriver;
        _projectionCoordinator = projectionCoordinator;
        _compatibilityEvaluator = compatibilityEvaluator;
        _logger = logger;
    }

    public Task<PromptGalleryPage<PromptGallerySearchItem>> SearchAsync(
        PromptGalleryQuery query,
        CancellationToken cancellationToken = default)
        => _searchDriver.SearchAsync(query, cancellationToken);

    public async Task<Result<PromptGalleryItemDetails>> GetItemAsync(
        Guid promptArtifactId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var item = await PromptGalleryPersistence.LoadItemDetailsAsync(dbContext, promptArtifactId, cancellationToken);
        return item is null
            ? Result<PromptGalleryItemDetails>.Failure(NotFound(promptArtifactId))
            : Result<PromptGalleryItemDetails>.Success(item);
    }

    public async Task<Result<PromptDraftSaveReceipt>> SaveDraftAsync(
        PromptGalleryDraft draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var errors = PromptGalleryPersistence.ValidateDraft(draft);
        if (errors.Count > 0)
        {
            return Result<PromptDraftSaveReceipt>.Failure(errors);
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        PromptArtifact entity;
        var isNew = draft.Id is null;
        if (draft.Id is not Guid existingId)
        {
            entity = new PromptArtifact
            {
                CreatedAtUtc = _clock.GetUtcNow(),
                Provenance = PromptArtifactProvenance.User
            };
            await dbContext.Set<PromptArtifact>().AddAsync(entity, cancellationToken);
        }
        else
        {
            var existing = await dbContext.Set<PromptArtifact>()
                .FirstOrDefaultAsync(item => item.Id == existingId, cancellationToken);
            if (existing is null)
            {
                return Result<PromptDraftSaveReceipt>.Failure(NotFound(existingId));
            }

            if (existing.UpdatedAtUtc != draft.ExpectedUpdatedAtUtc)
            {
                return Result<PromptDraftSaveReceipt>.Failure(Error.Failure(
                    "The Prompt Gallery item changed after it was loaded. Reload it before saving.",
                    "prompts.gallery.concurrency-conflict"));
            }

            entity = existing;
        }

        entity.ProjectId = draft.ProjectId;
        entity.CollectionId = draft.CollectionId;
        entity.Title = draft.Title.Trim();
        entity.Summary = draft.Summary.Trim();
        entity.Kind = draft.Kind;
        entity.Phase = draft.Phase.Trim();
        entity.CurrentDraftText = draft.Content;
        entity.SearchText = PromptGalleryPersistence.BuildSearchText(entity);
        entity.Status = PromptArtifactStatus.Draft;
        entity.IsArchived = false;
        entity.ArchivedAtUtc = null;
        entity.RecommendedTemperature = draft.Recommendations?.Temperature;
        entity.RecommendedMaxOutputTokens = draft.Recommendations?.MaxOutputTokens;
        entity.RecommendedTopP = draft.Recommendations?.TopP;
        entity.UpdatedAtUtc = NextUpdatedAt(entity.UpdatedAtUtc);

        await PromptGalleryPersistence.SyncTagsAsync(dbContext, entity.Id, draft.Tags ?? [], isNew, cancellationToken);
        await PromptGalleryPersistence.SyncSupportedModelsAsync(
            dbContext,
            entity.Id,
            draft.SupportedModels ?? [],
            isNew,
            cancellationToken);
        await PromptGalleryPersistence.SyncSupportedConsumersAsync(
            dbContext,
            entity.Id,
            draft.SupportedConsumers ?? [],
            isNew,
            cancellationToken);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<PromptDraftSaveReceipt>.Failure(Error.Failure(
                "The Prompt Gallery item changed while it was being saved. Reload it before retrying.",
                "prompts.gallery.concurrency-conflict"));
        }

        await ProjectCanonicalChangeAsync(entity.Id, cancellationToken);
        await RecordActivityAsync(
            isNew ? "create-draft" : "update-draft",
            isNew ? "Created prompt draft" : "Updated prompt draft",
            entity,
            cancellationToken);
        return Result<PromptDraftSaveReceipt>.Success(new(entity.Id, entity.UpdatedAtUtc));
    }

    public async Task<Result<PromptVersionSnapshot>> CreateVersionAsync(
        Guid promptArtifactId,
        PromptVersionCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.CreationReason) || request.CreationReason.Trim().Length > 200)
        {
            return Result<PromptVersionSnapshot>.Failure(
                Error.Validation(
                    "A version creation reason containing 1 to 200 characters is required.",
                    "prompts.version.reason-invalid"));
        }

        if (string.IsNullOrWhiteSpace(request.OutputFormat) || request.OutputFormat.Length > 80)
        {
            return Result<PromptVersionSnapshot>.Failure(
                Error.Validation("Output format must contain 1 to 80 characters.", "prompts.version.output-format-invalid"));
        }

        if (request.ExpectedUpdatedAtUtc == default)
        {
            return Result<PromptVersionSnapshot>.Failure(Error.Validation(
                "ExpectedUpdatedAtUtc is required when creating a Prompt Gallery version.",
                "prompts.version.expected-updated-at-required"));
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await dbContext.Set<PromptArtifact>()
            .FirstOrDefaultAsync(item => item.Id == promptArtifactId, cancellationToken);
        if (entity is null)
        {
            return Result<PromptVersionSnapshot>.Failure(NotFound(promptArtifactId));
        }

        if (entity.IsArchived)
        {
            return Result<PromptVersionSnapshot>.Failure(
                Error.Failure("Archived Gallery items cannot create new versions.", "prompts.version.artifact-archived"));
        }

        if (entity.UpdatedAtUtc != request.ExpectedUpdatedAtUtc)
        {
            return Result<PromptVersionSnapshot>.Failure(Error.Failure(
                "The Prompt Gallery item changed after it was reviewed. Reload it before creating a version.",
                "prompts.gallery.concurrency-conflict"));
        }

        if (string.IsNullOrWhiteSpace(entity.CurrentDraftText))
        {
            return Result<PromptVersionSnapshot>.Failure(
                Error.Validation("Prompt content is required before creating a version.", "prompts.version.content-required"));
        }

        if (entity.CurrentDraftText.Length > PromptGalleryLimits.MaximumContentLength)
        {
            return Result<PromptVersionSnapshot>.Failure(
                Error.Validation(
                    $"Prompt content cannot exceed {PromptGalleryLimits.MaximumContentLength:N0} characters.",
                    "prompts.version.content-too-long"));
        }

        entity.CurrentVersionNumber += 1;
        entity.Status = PromptArtifactStatus.Final;
        entity.UpdatedAtUtc = NextUpdatedAt(entity.UpdatedAtUtc);
        var version = new PromptVersion
        {
            PromptArtifactId = entity.Id,
            VersionNumber = entity.CurrentVersionNumber,
            Content = entity.CurrentDraftText,
            CreationReason = request.CreationReason.Trim(),
            OutputFormat = request.OutputFormat.Trim(),
            TitleSnapshot = entity.Title,
            SummarySnapshot = entity.Summary,
            KindSnapshot = entity.Kind,
            RecommendedTemperatureSnapshot = entity.RecommendedTemperature,
            RecommendedMaxOutputTokensSnapshot = entity.RecommendedMaxOutputTokens,
            RecommendedTopPSnapshot = entity.RecommendedTopP,
            CreatedAtUtc = _clock.GetUtcNow()
        };
        await dbContext.Set<PromptVersion>().AddAsync(version, cancellationToken);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<PromptVersionSnapshot>.Failure(Error.Failure(
                "The Prompt Gallery item changed while its version was being created. Reload it before retrying.",
                "prompts.gallery.concurrency-conflict"));
        }

        await ProjectCanonicalChangeAsync(entity.Id, cancellationToken);
        await RecordActivityAsync(
            "finalize",
            $"Finalized prompt version {version.VersionNumber}",
            entity,
            cancellationToken);
        return Result<PromptVersionSnapshot>.Success(ToSnapshot(version));
    }

    public async Task<Result<PromptVersionSnapshot>> ImportVersionAsync(
        PromptGalleryImportRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Draft);
        ArgumentNullException.ThrowIfNull(request.Version);

        var errors = PromptGalleryPersistence.ValidateDraft(request.Draft).ToList();
        if (request.Draft.Id.HasValue)
        {
            errors.Add(Error.Validation(
                "Imported Gallery drafts cannot specify an artifact ID.",
                "prompts.import.artifact-id-invalid"));
        }

        if (!Enum.IsDefined(request.Provenance) ||
            request.Provenance is PromptArtifactProvenance.User or PromptArtifactProvenance.PackagedComponentCatalog)
        {
            errors.Add(Error.Validation(
                "Prompt import provenance is invalid.",
                "prompts.import.provenance-invalid"));
        }

        if (string.IsNullOrWhiteSpace(request.SourceKey) || request.SourceKey.Trim().Length > 200)
        {
            errors.Add(Error.Validation(
                "Prompt import source key must contain 1 to 200 characters.",
                "prompts.import.source-key-invalid"));
        }

        if (string.IsNullOrWhiteSpace(request.SourceCatalog) || request.SourceCatalog.Trim().Length > 120)
        {
            errors.Add(Error.Validation(
                "Prompt import source catalog must contain 1 to 120 characters.",
                "prompts.import.source-catalog-invalid"));
        }

        if (string.IsNullOrWhiteSpace(request.Draft.Content))
        {
            errors.Add(Error.Validation(
                "Imported prompt content is required.",
                "prompts.import.content-required"));
        }

        if (string.IsNullOrWhiteSpace(request.Version.CreationReason) ||
            request.Version.CreationReason.Trim().Length > 200 ||
            string.IsNullOrWhiteSpace(request.Version.OutputFormat) ||
            request.Version.OutputFormat.Trim().Length > 80)
        {
            errors.Add(Error.Validation(
                "Imported prompt version metadata is invalid.",
                "prompts.import.version-invalid"));
        }

        if (errors.Count > 0)
        {
            return Result<PromptVersionSnapshot>.Failure(errors);
        }

        var draft = request.Draft;
        var sourceKey = request.SourceKey.Trim();
        var now = _clock.GetUtcNow();
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await dbContext.Set<PromptArtifact>()
            .SingleOrDefaultAsync(
                artifact => artifact.Provenance == request.Provenance && artifact.SourceKey == sourceKey,
                cancellationToken);
        var isNew = entity is null;
        if (entity is null)
        {
            entity = new PromptArtifact
            {
                CreatedAtUtc = now,
                Provenance = request.Provenance,
                SourceKey = sourceKey
            };
            await dbContext.Set<PromptArtifact>().AddAsync(entity, cancellationToken);
        }
        else if (entity.IsArchived)
        {
            return Result<PromptVersionSnapshot>.Failure(Error.Failure(
                "An archived Gallery import cannot be updated automatically.",
                "prompts.import.artifact-archived"));
        }

        var createVersion = isNew || !MatchesCurrentSnapshot(entity, draft);
        entity.ProjectId = draft.ProjectId;
        entity.CollectionId = draft.CollectionId;
        entity.Title = draft.Title.Trim();
        entity.Summary = draft.Summary.Trim();
        entity.Kind = draft.Kind;
        entity.Phase = draft.Phase.Trim();
        entity.CurrentDraftText = draft.Content;
        entity.Status = PromptArtifactStatus.Final;
        entity.SourceCatalog = request.SourceCatalog.Trim();
        entity.RecommendedTemperature = draft.Recommendations?.Temperature;
        entity.RecommendedMaxOutputTokens = draft.Recommendations?.MaxOutputTokens;
        entity.RecommendedTopP = draft.Recommendations?.TopP;
        entity.SearchText = PromptGalleryPersistence.BuildSearchText(entity);
        entity.UpdatedAtUtc = NextUpdatedAt(entity.UpdatedAtUtc);

        await PromptGalleryPersistence.SyncTagsAsync(dbContext, entity.Id, draft.Tags ?? [], isNew, cancellationToken);
        await PromptGalleryPersistence.SyncSupportedModelsAsync(
            dbContext,
            entity.Id,
            draft.SupportedModels ?? [],
            isNew,
            cancellationToken);
        await PromptGalleryPersistence.SyncSupportedConsumersAsync(
            dbContext,
            entity.Id,
            draft.SupportedConsumers ?? [],
            isNew,
            cancellationToken);

        PromptVersion version;
        if (createVersion)
        {
            entity.CurrentVersionNumber += 1;
            version = new PromptVersion
            {
                PromptArtifactId = entity.Id,
                VersionNumber = entity.CurrentVersionNumber,
                Content = entity.CurrentDraftText,
                CreationReason = request.Version.CreationReason.Trim(),
                OutputFormat = request.Version.OutputFormat.Trim(),
                TitleSnapshot = entity.Title,
                SummarySnapshot = entity.Summary,
                KindSnapshot = entity.Kind,
                RecommendedTemperatureSnapshot = entity.RecommendedTemperature,
                RecommendedMaxOutputTokensSnapshot = entity.RecommendedMaxOutputTokens,
                RecommendedTopPSnapshot = entity.RecommendedTopP,
                CreatedAtUtc = now
            };
            await dbContext.Set<PromptVersion>().AddAsync(version, cancellationToken);
        }
        else
        {
            version = await dbContext.Set<PromptVersion>()
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    item => item.PromptArtifactId == entity.Id &&
                            item.VersionNumber == entity.CurrentVersionNumber,
                    cancellationToken)
                ?? throw new InvalidOperationException(
                    $"Imported Gallery item '{entity.Id}' has no current immutable version.");
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await ProjectCanonicalChangeAsync(entity.Id, cancellationToken);
        await RecordActivityAsync(
            createVersion ? "import-version" : "verify-import",
            createVersion ? "Imported prompt version" : "Verified imported prompt version",
            entity,
            cancellationToken);
        return Result<PromptVersionSnapshot>.Success(ToSnapshot(version));
    }

    public async Task<Result<PromptVersionSnapshot>> GetVersionSnapshotAsync(
        Guid promptVersionId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var version = await dbContext.Set<PromptVersion>()
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == promptVersionId, cancellationToken);
        return version is null
            ? Result<PromptVersionSnapshot>.Failure(
                Error.Failure($"Prompt version '{promptVersionId}' was not found.", "prompts.version.not-found"))
            : Result<PromptVersionSnapshot>.Success(ToSnapshot(version));
    }

    public async Task<Result<PromptVersionSnapshot>> GetVersionSnapshotAsync(
        Guid promptArtifactId,
        int versionNumber,
        CancellationToken cancellationToken = default)
    {
        if (versionNumber <= 0)
        {
            return Result<PromptVersionSnapshot>.Failure(
                Error.Validation("Version number must be greater than zero.", "prompts.version.number-invalid"));
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var version = await dbContext.Set<PromptVersion>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.PromptArtifactId == promptArtifactId && item.VersionNumber == versionNumber,
                cancellationToken);
        return version is null
            ? Result<PromptVersionSnapshot>.Failure(
                Error.Failure(
                    $"Prompt version '{promptArtifactId}/{versionNumber}' was not found.",
                    "prompts.version.not-found"))
            : Result<PromptVersionSnapshot>.Success(ToSnapshot(version));
    }

    public async Task<Result<IReadOnlyList<PromptVersionSnapshot>>> GetVersionSnapshotsAsync(
        IReadOnlyCollection<Guid> promptVersionIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(promptVersionIds);
        if (promptVersionIds.Any(id => id == Guid.Empty))
        {
            return Result<IReadOnlyList<PromptVersionSnapshot>>.Failure(
                Error.Validation("Prompt version IDs cannot be empty.", "prompts.version.ids-invalid"));
        }

        var ids = promptVersionIds.Distinct().ToArray();
        if (ids.Length == 0)
        {
            return Result<IReadOnlyList<PromptVersionSnapshot>>.Success([]);
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var versions = await dbContext.Set<PromptVersion>()
            .AsNoTracking()
            .Where(version => ids.Contains(version.Id))
            .ToListAsync(cancellationToken);
        if (versions.Count != ids.Length)
        {
            var foundIds = versions.Select(version => version.Id).ToHashSet();
            var missing = ids.Where(id => !foundIds.Contains(id)).Take(10);
            return Result<IReadOnlyList<PromptVersionSnapshot>>.Failure(Error.Failure(
                $"Prompt versions were not found: {string.Join(", ", missing)}.",
                "prompts.version.not-found"));
        }

        return Result<IReadOnlyList<PromptVersionSnapshot>>.Success(
            versions.Select(ToSnapshot).ToArray());
    }

    public async Task<Result<IReadOnlyDictionary<Guid, PromptGalleryCompatibilitySnapshot>>> GetCompatibilitySnapshotsAsync(
        IReadOnlyCollection<Guid> promptArtifactIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(promptArtifactIds);
        if (promptArtifactIds.Any(id => id == Guid.Empty))
        {
            return Result<IReadOnlyDictionary<Guid, PromptGalleryCompatibilitySnapshot>>.Failure(
                Error.Validation("Prompt artifact IDs cannot be empty.", "prompts.gallery.ids-invalid"));
        }

        var ids = promptArtifactIds.Distinct().ToArray();
        if (ids.Length == 0)
        {
            return Result<IReadOnlyDictionary<Guid, PromptGalleryCompatibilitySnapshot>>.Success(
                new Dictionary<Guid, PromptGalleryCompatibilitySnapshot>());
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var artifacts = await dbContext.Set<PromptArtifact>()
            .AsNoTracking()
            .Where(artifact => ids.Contains(artifact.Id))
            .Select(artifact => new
            {
                artifact.Id,
                artifact.Kind,
                artifact.IsArchived,
                artifact.CurrentVersionNumber
            })
            .ToArrayAsync(cancellationToken);
        if (artifacts.Length != ids.Length)
        {
            var foundIds = artifacts.Select(artifact => artifact.Id).ToHashSet();
            var missing = ids.Where(id => !foundIds.Contains(id)).Take(10);
            return Result<IReadOnlyDictionary<Guid, PromptGalleryCompatibilitySnapshot>>.Failure(Error.Failure(
                $"Prompt Gallery items were not found: {string.Join(", ", missing)}.",
                "prompts.gallery.not-found"));
        }

        var supportedModels = await dbContext.Set<PromptSupportedProviderModel>()
            .AsNoTracking()
            .Where(model => ids.Contains(model.PromptArtifactId))
            .OrderByDescending(model => model.IsPreferred)
            .ThenBy(model => model.Provider)
            .ThenBy(model => model.Model)
            .Select(model => new { model.PromptArtifactId, model.Provider, model.Model, model.IsPreferred })
            .ToArrayAsync(cancellationToken);
        var modelsByArtifact = supportedModels
            .GroupBy(model => model.PromptArtifactId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<PromptProviderModel>)group
                    .Select(model => new PromptProviderModel(model.Provider, model.Model, model.IsPreferred))
                    .ToArray());
        var supportedConsumers = await dbContext.Set<PromptSupportedConsumer>()
            .AsNoTracking()
            .Where(consumer => ids.Contains(consumer.PromptArtifactId))
            .OrderBy(consumer => consumer.Consumer)
            .Select(consumer => new { consumer.PromptArtifactId, consumer.Consumer })
            .ToArrayAsync(cancellationToken);
        var consumersByArtifact = supportedConsumers
            .GroupBy(consumer => consumer.PromptArtifactId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<PromptGalleryConsumer>)group
                    .Select(consumer => consumer.Consumer)
                    .ToArray());
        IReadOnlyDictionary<Guid, PromptGalleryCompatibilitySnapshot> snapshots = artifacts.ToDictionary(
            artifact => artifact.Id,
            artifact => new PromptGalleryCompatibilitySnapshot(
                artifact.Id,
                artifact.Kind,
                artifact.IsArchived,
                artifact.CurrentVersionNumber,
                modelsByArtifact.GetValueOrDefault(artifact.Id) ?? [],
                consumersByArtifact.GetValueOrDefault(artifact.Id) ?? []));
        return Result<IReadOnlyDictionary<Guid, PromptGalleryCompatibilitySnapshot>>.Success(snapshots);
    }

    public async Task<Result> ArchiveAsync(
        Guid promptArtifactId,
        bool archived,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await dbContext.Set<PromptArtifact>()
            .FirstOrDefaultAsync(item => item.Id == promptArtifactId, cancellationToken);
        if (entity is null)
        {
            return Result.Failure(NotFound(promptArtifactId));
        }

        if (entity.IsArchived == archived)
        {
            return Result.Success();
        }

        entity.IsArchived = archived;
        entity.ArchivedAtUtc = archived ? _clock.GetUtcNow() : null;
        entity.UpdatedAtUtc = NextUpdatedAt(entity.UpdatedAtUtc);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure(ConcurrentMutation("archive or restore"));
        }

        await ProjectCanonicalChangeAsync(entity.Id, cancellationToken);
        await RecordActivityAsync(
            archived ? "archive" : "restore",
            archived ? "Archived prompt" : "Restored prompt",
            entity,
            cancellationToken);
        return Result.Success();
    }

    public async Task<Result> SetFavoriteAsync(
        Guid promptArtifactId,
        bool favorite,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await dbContext.Set<PromptArtifact>()
            .FirstOrDefaultAsync(item => item.Id == promptArtifactId, cancellationToken);
        if (entity is null)
        {
            return Result.Failure(NotFound(promptArtifactId));
        }

        if (entity.IsFavorite == favorite)
        {
            return Result.Success();
        }

        entity.IsFavorite = favorite;
        entity.UpdatedAtUtc = NextUpdatedAt(entity.UpdatedAtUtc);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure(ConcurrentMutation("favorite update"));
        }

        await ProjectCanonicalChangeAsync(entity.Id, cancellationToken);
        await RecordActivityAsync(
            favorite ? "favorite" : "unfavorite",
            favorite ? "Marked prompt as favorite" : "Removed prompt from favorites",
            entity,
            cancellationToken);
        return Result.Success();
    }

    public async Task<Result<PromptCompatibilityResult>> EvaluateCompatibilityAsync(
        Guid promptArtifactId,
        PromptGalleryConsumerContext context,
        CancellationToken cancellationToken = default)
    {
        if (context is null ||
            !Enum.IsDefined(context.Consumer) ||
            !Enum.IsDefined(context.Purpose) ||
            (context.RequiredKind.HasValue && !Enum.IsDefined(context.RequiredKind.Value)) ||
            context.Provider?.Length > 120 ||
            context.Model?.Length > 200)
        {
            return Result<PromptCompatibilityResult>.Failure(Error.Validation(
                "Prompt compatibility context is invalid.",
                "prompts.compatibility.context-invalid"));
        }

        var itemResult = await GetItemAsync(promptArtifactId, cancellationToken);
        if (itemResult.IsFailure || itemResult.Value is null)
        {
            return Result<PromptCompatibilityResult>.Failure(itemResult.Errors);
        }

        var suppressed = itemResult.Value.WarningSuppressions
            .Where(preference => preference.Consumer == context.Consumer)
            .Select(preference => preference.IssueCode)
            .ToHashSet();
        return Result<PromptCompatibilityResult>.Success(
            _compatibilityEvaluator.Evaluate(itemResult.Value, context, suppressed));
    }

    public async Task<Result> SetWarningSuppressionAsync(
        Guid promptArtifactId,
        PromptGalleryConsumer consumer,
        PromptCompatibilityIssueCode issueCode,
        bool suppressed,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(consumer) ||
            !Enum.IsDefined(issueCode) ||
            !PromptGalleryCompatibilityEvaluator.CanSuppress(issueCode))
        {
            return Result.Failure(Error.Validation(
                $"Compatibility issue '{issueCode}' cannot be suppressed.",
                "prompts.compatibility.issue-not-suppressible"));
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (!await dbContext.Set<PromptArtifact>().AnyAsync(item => item.Id == promptArtifactId, cancellationToken))
        {
            return Result.Failure(NotFound(promptArtifactId));
        }

        var preference = await dbContext.Set<PromptCompatibilityWarningPreference>()
            .FirstOrDefaultAsync(item =>
                item.PromptArtifactId == promptArtifactId &&
                item.Consumer == consumer &&
                item.IssueCode == issueCode,
                cancellationToken);

        if (!suppressed)
        {
            if (preference is not null)
            {
                dbContext.Remove(preference);
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            return Result.Success();
        }

        if (preference is null)
        {
            preference = new PromptCompatibilityWarningPreference
            {
                PromptArtifactId = promptArtifactId,
                Consumer = consumer,
                IssueCode = issueCode
            };
            await dbContext.Set<PromptCompatibilityWarningPreference>().AddAsync(preference, cancellationToken);
        }

        preference.IsSuppressed = true;
        preference.UpdatedAtUtc = _clock.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task RecordActivityAsync(
        string action,
        string title,
        PromptArtifact entity,
        CancellationToken cancellationToken)
    {
        try
        {
            await _activityStream.RecordAsync(new ActivityWriteRequest(
                "prompts",
                action,
                title,
                entity.Title,
                ProjectId: entity.ProjectId,
                ArtifactKind: "prompt",
                ArtifactId: entity.Id,
                Route: $"/prompt-gallery?promptId={entity.Id}"), cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Prompt Gallery activity recording failed after canonical item {PromptArtifactId} committed action {Action}.",
                entity.Id,
                action);
        }
    }

    private DateTimeOffset NextUpdatedAt(DateTimeOffset current)
    {
        var now = NormalizeDatabaseTimestamp(_clock.GetUtcNow());
        var normalizedCurrent = NormalizeDatabaseTimestamp(current);
        return now > normalizedCurrent
            ? now
            : normalizedCurrent.AddTicks(TimeSpan.TicksPerMicrosecond);
    }

    private static DateTimeOffset NormalizeDatabaseTimestamp(DateTimeOffset value)
    {
        var utc = value.ToUniversalTime();
        return new DateTimeOffset(
            utc.Ticks - (utc.Ticks % TimeSpan.TicksPerMicrosecond),
            TimeSpan.Zero);
    }

    private static Error ConcurrentMutation(string operation)
        => Error.Failure(
            $"The Prompt Gallery item changed during the {operation}. Reload it before retrying.",
            "prompts.gallery.concurrency-conflict");

    private async Task ProjectCanonicalChangeAsync(
        Guid promptArtifactId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _projectionCoordinator.UpsertAsync(promptArtifactId, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Prompt Gallery projection failed after canonical item {PromptArtifactId} committed. Run a projection rebuild to repair the derivative index.",
                promptArtifactId);
        }
    }

    private static PromptVersionSnapshot ToSnapshot(PromptVersion version)
        => new(
            version.PromptArtifactId,
            version.Id,
            version.VersionNumber,
            version.TitleSnapshot,
            version.SummarySnapshot,
            version.KindSnapshot,
            version.Content,
            version.OutputFormat,
            new PromptModelRecommendations(
                version.RecommendedTemperatureSnapshot,
                version.RecommendedMaxOutputTokensSnapshot,
                version.RecommendedTopPSnapshot),
            version.CreatedAtUtc);

    private static bool MatchesCurrentSnapshot(PromptArtifact entity, PromptGalleryDraft draft)
        => entity.CurrentVersionNumber > 0 &&
           string.Equals(entity.Title, draft.Title.Trim(), StringComparison.Ordinal) &&
           string.Equals(entity.Summary, draft.Summary.Trim(), StringComparison.Ordinal) &&
           entity.Kind == draft.Kind &&
           string.Equals(entity.CurrentDraftText, draft.Content, StringComparison.Ordinal) &&
           entity.RecommendedTemperature == draft.Recommendations?.Temperature &&
           entity.RecommendedMaxOutputTokens == draft.Recommendations?.MaxOutputTokens &&
           entity.RecommendedTopP == draft.Recommendations?.TopP;

    private static Error NotFound(Guid promptArtifactId)
        => Error.Failure(
            $"Prompt Gallery item '{promptArtifactId}' was not found.",
            "prompts.gallery.not-found");
}
