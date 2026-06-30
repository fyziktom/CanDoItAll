using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.CognitiveMemory;

public sealed class CognitiveMemorySourceIngestionService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IProjectStructureSourceSnapshotProvider projectStructureSourceSnapshotProvider,
    IProcessRuntimeEvidenceSourceProvider processRuntimeEvidenceSourceProvider,
    IWorkflowRuntimeEvidenceSourceProvider workflowRuntimeEvidenceSourceProvider,
    IClock clock,
    ILogger<CognitiveMemorySourceIngestionService> logger) : ICognitiveMemorySourceIngestionService
{
    public async ValueTask<CognitiveMemorySourceIngestionResult> IngestAsync(
        CognitiveMemorySourceIngestionRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var projectId = ResolveProjectId(request);
        var nowUtc = clock.GetUtcNow();
        var sourceSystem = request.SourceKind.ToString();
        var scopeKey = request.ScopeId.ToString("D");
        var idempotencyKey = request.IdempotencyKey.Value;

        var existingRun = await dbContext.Set<CognitiveMemoryRunRecord>()
            .AsNoTracking()
            .SingleOrDefaultAsync(run => run.IdempotencyKey == idempotencyKey, cancellationToken);
        if (existingRun is not null)
        {
            logger.LogWarning(
                "Rejected duplicate cognitive memory source ingestion request. SourceKind={SourceKind} ScopeId={ScopeId} IdempotencyKey={IdempotencyKey} ExistingRunId={RunId}",
                request.SourceKind,
                request.ScopeId,
                idempotencyKey,
                existingRun.Id);

            return new CognitiveMemorySourceIngestionResult(
                CognitiveMemorySourceIngestionStatus.DuplicateRejected,
                existingRun.Id,
                ManifestId: null,
                NextCursor: request.Cursor,
                HasMore: false,
                SourceItemCount: 0,
                CreatedSourceItemCount: 0,
                UpdatedSourceItemCount: 0,
                CreatedEvidenceAnchorCount: 0,
                CreatedContextHintCount: 0,
                CreatedLayoutCount: 0,
                CreatedGraphLinkCount: 0,
                CreatedTombstoneCount: 0,
                FailureId: null,
                FailureCode: "DuplicateIdempotencyKey");
        }

        var run = new CognitiveMemoryRunRecord
        {
            ProjectId = projectId,
            RunKind = CognitiveMemoryRunKind.SourceScan,
            Status = CognitiveMemoryRunStatus.Running,
            OperationMode = CognitiveMemoryOperationMode.Observe,
            IdempotencyKey = idempotencyKey,
            InputHash = ComputeRequestHash(request),
            AlgorithmVersion = "source-ingestion-v1",
            Cursor = request.Cursor?.Value ?? string.Empty,
            StartedAtUtc = nowUtc,
            ConcurrencyToken = Guid.NewGuid()
        };
        dbContext.Add(run);
        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            var snapshot = await ReadSnapshotAsync(request, cancellationToken);
            var persistence = await PersistSnapshotAsync(dbContext, request, snapshot, projectId, nowUtc, cancellationToken);

            run.Status = snapshot.Manifest.HasMore ? CognitiveMemoryRunStatus.Running : CognitiveMemoryRunStatus.Succeeded;
            run.Cursor = snapshot.Manifest.NextCursor?.Value ?? string.Empty;
            run.CompletedAtUtc = snapshot.Manifest.HasMore ? null : nowUtc;
            await dbContext.SaveChangesAsync(cancellationToken);

            return new CognitiveMemorySourceIngestionResult(
                CognitiveMemorySourceIngestionStatus.Ingested,
                run.Id,
                persistence.ManifestId,
                snapshot.Manifest.NextCursor,
                snapshot.Manifest.HasMore,
                snapshot.Items.Count,
                persistence.CreatedSourceItemCount,
                persistence.UpdatedSourceItemCount,
                persistence.CreatedEvidenceAnchorCount,
                persistence.CreatedContextHintCount,
                persistence.CreatedLayoutCount,
                persistence.CreatedGraphLinkCount,
                persistence.CreatedTombstoneCount,
                FailureId: null,
                FailureCode: null);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            var failure = new CognitiveMemorySourceScanFailureRecord
            {
                RunId = run.Id,
                ProjectId = projectId,
                SourceSystem = sourceSystem,
                SourceScopeKey = scopeKey,
                CursorHash = CognitiveMemoryHash.FromUtf8(request.Cursor?.Value ?? "no-cursor").Value,
                ExceptionCategory = exception.GetType().Name,
                RetryPolicy = exception is MemorySourceSnapshotCursorException
                    ? CognitiveMemorySourceScanFailureRetryPolicy.NotRetryable
                    : CognitiveMemorySourceScanFailureRetryPolicy.Retryable,
                Message = Truncate(exception.Message, 2000),
                CreatedAtUtc = nowUtc
            };

            run.Status = CognitiveMemoryRunStatus.Failed;
            run.CompletedAtUtc = nowUtc;
            run.FailureCode = failure.ExceptionCategory;
            run.FailureMessage = failure.Message;
            dbContext.Add(failure);
            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogError(
                exception,
                "Cognitive memory source ingestion failed. SourceKind={SourceKind} ScopeId={ScopeId} RunId={RunId} FailureId={FailureId} RetryPolicy={RetryPolicy}",
                request.SourceKind,
                request.ScopeId,
                run.Id,
                failure.Id,
                failure.RetryPolicy);

            return new CognitiveMemorySourceIngestionResult(
                CognitiveMemorySourceIngestionStatus.Failed,
                run.Id,
                ManifestId: null,
                NextCursor: request.Cursor,
                HasMore: false,
                SourceItemCount: 0,
                CreatedSourceItemCount: 0,
                UpdatedSourceItemCount: 0,
                CreatedEvidenceAnchorCount: 0,
                CreatedContextHintCount: 0,
                CreatedLayoutCount: 0,
                CreatedGraphLinkCount: 0,
                CreatedTombstoneCount: 0,
                failure.Id,
                failure.ExceptionCategory);
        }
    }

    private async Task<CognitiveMemorySourceIngestionPersistenceResult> PersistSnapshotAsync(
        AppDbContext dbContext,
        CognitiveMemorySourceIngestionRequest request,
        MemorySourceSnapshot snapshot,
        Guid? projectId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        var manifest = await UpsertManifestAsync(dbContext, snapshot.Manifest, projectId, nowUtc, cancellationToken);
        var contextFrame = await GetOrCreateContextFrameAsync(dbContext, request, projectId, nowUtc, cancellationToken);
        var existingItems = await LoadExistingSourceItemsAsync(dbContext, manifest.Id, snapshot.Items, cancellationToken);
        var existingLayouts = await LoadExistingLayoutsAsync(dbContext, existingItems.Values.Select(item => item.Id).ToList(), cancellationToken);
        var existingAnchorKeys = await LoadExistingAnchorKeysAsync(dbContext, existingItems.Values.Select(item => item.Id).ToList(), cancellationToken);
        var existingGraphLinkKeys = await LoadExistingGraphLinkKeysAsync(dbContext, manifest.Id, cancellationToken);
        var existingContextHintKeys = await LoadExistingContextHintKeysAsync(dbContext, contextFrame.Frame.Id, existingItems.Values.Select(item => item.Id).ToList(), cancellationToken);

        var createdItems = 0;
        var updatedItems = 0;
        var createdEvidenceAnchors = 0;
        var createdContextHints = 0;
        var createdLayouts = 0;
        var createdGraphLinks = 0;

        foreach (var sourceItem in snapshot.Items)
        {
            var sourceItemKey = sourceItem.Id.Value;
            var locator = ResolveLocator(sourceItem);
            var contentHash = NormalizeHash(sourceItem.ContentHash);
            if (!existingItems.TryGetValue(sourceItemKey, out var itemRecord))
            {
                itemRecord = new CognitiveMemorySourceItemRecord
                {
                    SourceManifestId = manifest.Id,
                    ProjectId = projectId,
                    SourceSystem = snapshot.Manifest.SourceKind.ToString(),
                    SourceItemKey = sourceItemKey,
                    SourceItemType = sourceItem.EntityKind.ToString(),
                    Title = Truncate(sourceItem.Title, 300),
                    ContentText = sourceItem.Content,
                    Locator = locator,
                    ContentHash = contentHash,
                    RedactionState = MapRedactionState(sourceItem.Permission),
                    AccessLevel = MapAccessLevel(sourceItem.Permission),
                    AccessScope = Truncate(sourceItem.Permission.AllowedFutureUsageSummary, 240),
                    ProvenanceJson = SerializeProvenance(sourceItem),
                    ObservedAtUtc = snapshot.Manifest.CapturedAtUtc,
                    CreatedAtUtc = nowUtc,
                    UpdatedAtUtc = nowUtc,
                    ConcurrencyToken = Guid.NewGuid()
                };
                dbContext.Add(itemRecord);
                existingItems[sourceItemKey] = itemRecord;
                createdItems++;
            }
            else
            {
                itemRecord.Title = Truncate(sourceItem.Title, 300);
                itemRecord.ContentText = sourceItem.Content;
                itemRecord.Locator = locator;
                itemRecord.ContentHash = contentHash;
                itemRecord.RedactionState = MapRedactionState(sourceItem.Permission);
                itemRecord.AccessLevel = MapAccessLevel(sourceItem.Permission);
                itemRecord.AccessScope = Truncate(sourceItem.Permission.AllowedFutureUsageSummary, 240);
                itemRecord.ProvenanceJson = SerializeProvenance(sourceItem);
                itemRecord.ObservedAtUtc = snapshot.Manifest.CapturedAtUtc;
                itemRecord.UpdatedAtUtc = nowUtc;
                itemRecord.ConcurrencyToken = Guid.NewGuid();
                updatedItems++;
            }

            if (TryAddEvidenceAnchor(dbContext, snapshot, sourceItem, itemRecord, projectId, nowUtc, existingAnchorKeys))
            {
                createdEvidenceAnchors++;
            }

            if (TryAddOrUpdateLayout(dbContext, sourceItem, itemRecord, projectId, nowUtc, existingLayouts))
            {
                createdLayouts++;
            }

            createdGraphLinks += AddMissingGraphLinks(
                dbContext,
                sourceItem,
                itemRecord,
                manifest.Id,
                projectId,
                nowUtc,
                existingGraphLinkKeys);

            if (TryAddContextHint(dbContext, itemRecord, contextFrame, projectId, nowUtc, existingContextHintKeys))
            {
                createdContextHints++;
            }
        }

        var currentSourceItemKeys = existingItems.Keys.ToHashSet(StringComparer.Ordinal);
        var createdTombstones = await AddTombstonesAsync(dbContext, manifest, projectId, snapshot.Manifest, currentSourceItemKeys, nowUtc, cancellationToken);

        return new CognitiveMemorySourceIngestionPersistenceResult(
            manifest.Id,
            createdItems,
            updatedItems,
            createdEvidenceAnchors,
            createdContextHints,
            createdLayouts,
            createdGraphLinks,
            createdTombstones);
    }

    private async Task<MemorySourceSnapshot> ReadSnapshotAsync(
        CognitiveMemorySourceIngestionRequest request,
        CancellationToken cancellationToken)
        => request.SourceKind switch
        {
            MemorySourceKind.WorkbenchProjectStructure => await projectStructureSourceSnapshotProvider.ReadSnapshotAsync(
                new ProjectStructureSourceSnapshotRequest(request.ScopeId, request.Cursor, request.Take),
                cancellationToken),
            MemorySourceKind.ProcessRuntime => await processRuntimeEvidenceSourceProvider.ReadSnapshotAsync(
                new ProcessRuntimeEvidenceSourceRequest(NormalizeOptionalScope(request.ScopeId), request.Cursor, request.Take),
                cancellationToken),
            MemorySourceKind.WorkflowRuntime => await workflowRuntimeEvidenceSourceProvider.ReadSnapshotAsync(
                new WorkflowRuntimeEvidenceSourceRequest(NormalizeOptionalScope(request.ScopeId) is Guid runId ? new WorkflowRunId(runId) : null, request.Cursor, request.Take),
                cancellationToken),
            _ => throw new NotSupportedException($"Source kind '{request.SourceKind}' is not supported by cognitive memory source ingestion.")
        };

    private static async Task<CognitiveMemorySourceManifestRecord> UpsertManifestAsync(
        AppDbContext dbContext,
        MemorySourceSnapshotManifest manifest,
        Guid? projectId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        var sourceSystem = manifest.SourceKind.ToString();
        var sourceScopeKey = manifest.ScopeId.ToString("D");
        var sourceSnapshotId = manifest.SnapshotId.Value;
        var manifestRecord = await dbContext.Set<CognitiveMemorySourceManifestRecord>()
            .SingleOrDefaultAsync(item =>
                item.SourceSystem == sourceSystem &&
                item.SourceScopeKey == sourceScopeKey &&
                item.SourceSnapshotId == sourceSnapshotId,
                cancellationToken);
        if (manifestRecord is null)
        {
            manifestRecord = new CognitiveMemorySourceManifestRecord
            {
                ProjectId = projectId,
                SourceSystem = sourceSystem,
                SourceScopeKey = sourceScopeKey,
                SourceSnapshotId = sourceSnapshotId,
                SnapshotHash = NormalizeHash(sourceSnapshotId),
                ProviderVersion = manifest.ProviderVersion,
                Cursor = manifest.NextCursor?.Value,
                ScanStatus = manifest.HasMore ? CognitiveMemoryRunStatus.Running : CognitiveMemoryRunStatus.Succeeded,
                ObservedAtUtc = manifest.CapturedAtUtc,
                CreatedAtUtc = nowUtc,
                UpdatedAtUtc = nowUtc,
                ConcurrencyToken = Guid.NewGuid()
            };
            dbContext.Add(manifestRecord);
            return manifestRecord;
        }

        manifestRecord.ProjectId = projectId;
        manifestRecord.SnapshotHash = NormalizeHash(sourceSnapshotId);
        manifestRecord.ProviderVersion = manifest.ProviderVersion;
        manifestRecord.Cursor = manifest.NextCursor?.Value;
        manifestRecord.ScanStatus = manifest.HasMore ? CognitiveMemoryRunStatus.Running : CognitiveMemoryRunStatus.Succeeded;
        manifestRecord.ObservedAtUtc = manifest.CapturedAtUtc;
        manifestRecord.UpdatedAtUtc = nowUtc;
        manifestRecord.ConcurrencyToken = Guid.NewGuid();
        return manifestRecord;
    }

    private async Task<CognitiveMemorySourceContextFrame> GetOrCreateContextFrameAsync(
        AppDbContext dbContext,
        CognitiveMemorySourceIngestionRequest request,
        Guid? projectId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        var displayName = $"{request.SourceKind}:{request.ScopeId:D}";
        var frameKind = request.SourceKind switch
        {
            MemorySourceKind.WorkbenchProjectStructure => CognitiveMemoryContextFrameKind.Project,
            MemorySourceKind.ProcessRuntime => CognitiveMemoryContextFrameKind.Process,
            MemorySourceKind.WorkflowRuntime => CognitiveMemoryContextFrameKind.Runtime,
            _ => CognitiveMemoryContextFrameKind.Composite
        };
        var dimensionKind = request.SourceKind switch
        {
            MemorySourceKind.WorkbenchProjectStructure => CognitiveMemoryContextDimensionKind.Project,
            MemorySourceKind.ProcessRuntime => CognitiveMemoryContextDimensionKind.Process,
            MemorySourceKind.WorkflowRuntime => CognitiveMemoryContextDimensionKind.Runtime,
            _ => CognitiveMemoryContextDimensionKind.SourceTrust
        };
        var valueKey = NormalizeKey(displayName);

        var frame = await dbContext.Set<CognitiveMemoryContextFrameRecord>()
            .SingleOrDefaultAsync(item =>
                item.ProjectId == projectId &&
                item.FrameKind == frameKind &&
                item.DisplayName == displayName,
                cancellationToken);
        if (frame is null)
        {
            frame = new CognitiveMemoryContextFrameRecord
            {
                ProjectId = projectId,
                FrameKind = frameKind,
                DisplayName = displayName,
                CreatedAtUtc = nowUtc,
                UpdatedAtUtc = nowUtc,
                ConcurrencyToken = Guid.NewGuid()
            };
            dbContext.Add(frame);
        }
        else
        {
            frame.UpdatedAtUtc = nowUtc;
            frame.ConcurrencyToken = Guid.NewGuid();
        }

        var hasDimension = await dbContext.Set<CognitiveMemoryContextFrameDimensionRecord>()
            .AnyAsync(item =>
                item.ContextFrameId == frame.Id &&
                item.DimensionKind == dimensionKind &&
                item.ValueKey == valueKey,
                cancellationToken);
        if (!hasDimension)
        {
            dbContext.Add(new CognitiveMemoryContextFrameDimensionRecord
            {
                ContextFrameId = frame.Id,
                ProjectId = projectId,
                DimensionKind = dimensionKind,
                Value = displayName,
                ValueKey = valueKey,
                CreatedAtUtc = nowUtc
            });
        }

        return new CognitiveMemorySourceContextFrame(frame, dimensionKind, valueKey);
    }

    private static async Task<Dictionary<string, CognitiveMemorySourceItemRecord>> LoadExistingSourceItemsAsync(
        AppDbContext dbContext,
        Guid sourceManifestId,
        IReadOnlyList<MemorySourceItem> sourceItems,
        CancellationToken cancellationToken)
    {
        var keys = sourceItems
            .Select(item => item.Id.Value)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        return await dbContext.Set<CognitiveMemorySourceItemRecord>()
            .Where(item => item.SourceManifestId == sourceManifestId && keys.Contains(item.SourceItemKey))
            .ToDictionaryAsync(item => item.SourceItemKey, StringComparer.Ordinal, cancellationToken);
    }

    private static async Task<Dictionary<Guid, CognitiveMemorySourceItemLayoutRecord>> LoadExistingLayoutsAsync(
        AppDbContext dbContext,
        IReadOnlyList<Guid> sourceItemIds,
        CancellationToken cancellationToken)
        => await dbContext.Set<CognitiveMemorySourceItemLayoutRecord>()
            .Where(item => sourceItemIds.Contains(item.SourceItemId))
            .ToDictionaryAsync(item => item.SourceItemId, cancellationToken);

    private static async Task<HashSet<string>> LoadExistingAnchorKeysAsync(
        AppDbContext dbContext,
        IReadOnlyList<Guid> sourceItemIds,
        CancellationToken cancellationToken)
        => (await dbContext.Set<CognitiveMemoryEvidenceAnchorRecord>()
                .Where(item => item.SourceItemId.HasValue && sourceItemIds.Contains(item.SourceItemId.Value))
                .Select(item => new { SourceItemId = item.SourceItemId!.Value, item.SourceHash })
                .ToListAsync(cancellationToken))
            .Select(item => AnchorKey(item.SourceItemId, item.SourceHash))
            .ToHashSet(StringComparer.Ordinal);

    private static async Task<HashSet<string>> LoadExistingGraphLinkKeysAsync(
        AppDbContext dbContext,
        Guid sourceManifestId,
        CancellationToken cancellationToken)
        => (await dbContext.Set<CognitiveMemorySourceItemGraphLinkRecord>()
                .Where(item => item.SourceManifestId == sourceManifestId)
                .Select(item => new { item.SourceItemKey, item.TargetSourceItemKey, item.LinkKind })
                .ToListAsync(cancellationToken))
            .Select(item => GraphLinkKey(item.SourceItemKey, item.TargetSourceItemKey, item.LinkKind.Value))
            .ToHashSet(StringComparer.Ordinal);

    private static async Task<HashSet<string>> LoadExistingContextHintKeysAsync(
        AppDbContext dbContext,
        Guid contextFrameId,
        IReadOnlyList<Guid> sourceItemIds,
        CancellationToken cancellationToken)
        => (await dbContext.Set<CognitiveMemorySourceItemContextHintRecord>()
                .Where(item => item.ContextFrameId == contextFrameId && sourceItemIds.Contains(item.SourceItemId))
                .Select(item => item.SourceItemId)
                .ToListAsync(cancellationToken))
            .Select(sourceItemId => ContextHintKey(sourceItemId, contextFrameId))
            .ToHashSet(StringComparer.Ordinal);

    private static bool TryAddEvidenceAnchor(
        AppDbContext dbContext,
        MemorySourceSnapshot snapshot,
        MemorySourceItem sourceItem,
        CognitiveMemorySourceItemRecord itemRecord,
        Guid? projectId,
        DateTimeOffset nowUtc,
        HashSet<string> existingAnchorKeys)
    {
        var sourceHash = NormalizeHash(sourceItem.ContentHash);
        if (!existingAnchorKeys.Add(AnchorKey(itemRecord.Id, sourceHash)))
        {
            return false;
        }

        dbContext.Add(new CognitiveMemoryEvidenceAnchorRecord
        {
            ProjectId = projectId,
            AnchorKind = MapAnchorKind(sourceItem.EntityKind),
            SourceManifestId = itemRecord.SourceManifestId,
            SourceItemId = itemRecord.Id,
            SourceSystem = snapshot.Manifest.SourceKind.ToString(),
            Locator = ResolveLocator(sourceItem),
            StructuredPath = sourceItem.Provenance.SourceRoute,
            TextStart = null,
            TextEnd = null,
            QuoteHash = sourceHash,
            TrustLevel = CognitiveMemorySourceTrustLevel.RuntimeSource,
            RedactionState = MapRedactionState(sourceItem.Permission),
            SourceHash = sourceHash,
            ObservedAtUtc = snapshot.Manifest.CapturedAtUtc,
            CreatedAtUtc = nowUtc,
            ConcurrencyToken = Guid.NewGuid()
        });
        return true;
    }

    private static bool TryAddOrUpdateLayout(
        AppDbContext dbContext,
        MemorySourceItem sourceItem,
        CognitiveMemorySourceItemRecord itemRecord,
        Guid? projectId,
        DateTimeOffset nowUtc,
        Dictionary<Guid, CognitiveMemorySourceItemLayoutRecord> existingLayouts)
    {
        if (sourceItem.Layout is null)
        {
            return false;
        }

        if (!existingLayouts.TryGetValue(itemRecord.Id, out var layout))
        {
            layout = new CognitiveMemorySourceItemLayoutRecord
            {
                SourceItemId = itemRecord.Id,
                ProjectId = projectId,
                CreatedAtUtc = nowUtc
            };
            dbContext.Add(layout);
            existingLayouts[itemRecord.Id] = layout;
            ApplyLayout(layout, sourceItem.Layout, nowUtc);
            return true;
        }

        ApplyLayout(layout, sourceItem.Layout, nowUtc);
        return false;
    }

    private static int AddMissingGraphLinks(
        AppDbContext dbContext,
        MemorySourceItem sourceItem,
        CognitiveMemorySourceItemRecord itemRecord,
        Guid sourceManifestId,
        Guid? projectId,
        DateTimeOffset nowUtc,
        HashSet<string> existingGraphLinkKeys)
    {
        var count = 0;
        foreach (var link in sourceItem.Links)
        {
            var sourceItemKey = Truncate(sourceItem.Id.Value, 500);
            var targetSourceItemKey = Truncate(link.TargetId.Value, 500);
            var linkKind = CognitiveMemorySourceLinkKind.Required(Truncate(link.Kind, 120));
            var key = GraphLinkKey(sourceItemKey, targetSourceItemKey, linkKind.Value);
            if (!existingGraphLinkKeys.Add(key))
            {
                continue;
            }

            dbContext.Add(new CognitiveMemorySourceItemGraphLinkRecord
            {
                SourceManifestId = sourceManifestId,
                SourceItemId = itemRecord.Id,
                ProjectId = projectId,
                SourceItemKey = sourceItemKey,
                TargetSourceItemKey = targetSourceItemKey,
                LinkKind = linkKind,
                IsUserAuthored = link.IsUserAuthored,
                CreatedAtUtc = nowUtc
            });
            count++;
        }

        return count;
    }

    private static bool TryAddContextHint(
        AppDbContext dbContext,
        CognitiveMemorySourceItemRecord itemRecord,
        CognitiveMemorySourceContextFrame contextFrame,
        Guid? projectId,
        DateTimeOffset nowUtc,
        HashSet<string> existingContextHintKeys)
    {
        var key = ContextHintKey(itemRecord.Id, contextFrame.Frame.Id);
        if (!existingContextHintKeys.Add(key))
        {
            return false;
        }

        dbContext.Add(new CognitiveMemorySourceItemContextHintRecord
        {
            SourceItemId = itemRecord.Id,
            ContextFrameId = contextFrame.Frame.Id,
            ProjectId = projectId,
            DimensionKind = contextFrame.DimensionKind,
            ValueKey = contextFrame.ValueKey,
            CreatedAtUtc = nowUtc
        });
        return true;
    }

    private static async Task<int> AddTombstonesAsync(
        AppDbContext dbContext,
        CognitiveMemorySourceManifestRecord currentManifest,
        Guid? projectId,
        MemorySourceSnapshotManifest manifest,
        IReadOnlySet<string> currentSourceItemKeys,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        if (manifest.PageStatus != MemorySourceSnapshotPageStatus.EndOfSource)
        {
            return 0;
        }

        var previousManifests = await dbContext.Set<CognitiveMemorySourceManifestRecord>()
            .AsNoTracking()
            .Where(item =>
                item.Id != currentManifest.Id &&
                item.SourceSystem == currentManifest.SourceSystem &&
                item.SourceScopeKey == currentManifest.SourceScopeKey)
            .ToListAsync(cancellationToken);
        var previousManifest = previousManifests
            .OrderByDescending(item => item.ObservedAtUtc)
            .FirstOrDefault();
        if (previousManifest is null)
        {
            return 0;
        }

        var removedItems = await dbContext.Set<CognitiveMemorySourceItemRecord>()
            .AsNoTracking()
            .Where(item => item.SourceManifestId == previousManifest.Id)
            .Where(item => !currentSourceItemKeys.Contains(item.SourceItemKey))
            .ToListAsync(cancellationToken);
        if (removedItems.Count == 0)
        {
            return 0;
        }

        var removedKeys = removedItems
            .Select(item => Truncate(item.SourceItemKey, 500))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var existingTombstoneKeys = await dbContext.Set<CognitiveMemorySourceTombstoneRecord>()
            .Where(item =>
                item.DetectedInManifestId == currentManifest.Id &&
                removedKeys.Contains(item.SourceItemKey))
            .Select(item => item.SourceItemKey)
            .ToListAsync(cancellationToken);
        var existingKeySet = existingTombstoneKeys.ToHashSet(StringComparer.Ordinal);

        var count = 0;
        foreach (var removedItem in removedItems)
        {
            var sourceItemKey = Truncate(removedItem.SourceItemKey, 500);
            if (!existingKeySet.Add(sourceItemKey))
            {
                continue;
            }

            dbContext.Add(new CognitiveMemorySourceTombstoneRecord
            {
                ProjectId = projectId,
                SourceSystem = currentManifest.SourceSystem,
                SourceScopeKey = currentManifest.SourceScopeKey,
                SourceItemKey = sourceItemKey,
                PreviousSourceItemId = removedItem.Id,
                DetectedInManifestId = currentManifest.Id,
                TombstonedAtUtc = nowUtc,
                Reason = "Source snapshot no longer contains the previous source item key at end-of-source.",
                ConcurrencyToken = Guid.NewGuid()
            });
            count++;
        }

        return count;
    }

    private static void ApplyLayout(
        CognitiveMemorySourceItemLayoutRecord layout,
        MemorySourceLayoutMetadata sourceLayout,
        DateTimeOffset nowUtc)
    {
        layout.X = sourceLayout.X;
        layout.Y = sourceLayout.Y;
        layout.ZIndex = sourceLayout.ZIndex;
        layout.StartUtc = sourceLayout.StartUtc;
        layout.EndUtc = sourceLayout.EndUtc;
        layout.DurationSeconds = sourceLayout.DurationSeconds;
        layout.SurfaceKind = CognitiveMemorySourceSurfaceKind.Required(Truncate(sourceLayout.SurfaceKind, 120));
        layout.MetadataJson = string.IsNullOrWhiteSpace(sourceLayout.MetadataJson) ? "{}" : sourceLayout.MetadataJson;
        layout.UpdatedAtUtc = nowUtc;
    }

    private static string SerializeProvenance(MemorySourceItem sourceItem)
    {
        var storage = sourceItem.StorageReference;
        var layout = sourceItem.Layout;
        var payload = new CognitiveMemorySourceItemProvenancePayload(
            sourceItem.Id.Value,
            sourceItem.Provenance.SourceRoute,
            sourceItem.Provenance.SourceEntityId,
            sourceItem.SourceKind,
            sourceItem.EntityKind,
            sourceItem.HashPolicy.Classification,
            sourceItem.HashPolicy.PayloadBasis,
            sourceItem.HashPolicy.UsageSummary,
            sourceItem.Permission.AccessMode,
            sourceItem.Permission.Sensitivity,
            sourceItem.Permission.ContainsSensitivePayload,
            sourceItem.Permission.RedactionPolicy,
            sourceItem.Permission.AllowedFutureUsageSummary,
            storage?.Provider ?? string.Empty,
            new CognitiveMemorySourceStorageLocatorKind(storage?.LocatorKind),
            storage?.Locator ?? string.Empty,
            storage?.ContentType ?? string.Empty,
            storage?.OriginalFileName ?? string.Empty,
            new CognitiveMemorySourceSurfaceKind(layout?.SurfaceKind),
            layout?.MetadataJson ?? "{}",
            sourceItem.Metadata.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal),
            sourceItem.References
                .Select(reference => new CognitiveMemorySourceReferencePayload(new CognitiveMemorySourceReferenceKind(reference.ReferenceKind), reference.ReferenceId, reference.OrderIndex))
                .ToList(),
            sourceItem.Links
                .Select(link => new CognitiveMemorySourceLinkPayload(sourceItem.Id.Value, link.TargetId.Value, new CognitiveMemorySourceLinkKind(link.Kind), link.IsUserAuthored))
                .ToList());

        return JsonSerializer.Serialize(
            payload,
            CognitiveMemoryJsonSerializerContext.Default.CognitiveMemorySourceItemProvenancePayload);
    }

    private static CognitiveMemoryEvidenceAnchorKind MapAnchorKind(MemorySourceEntityKind entityKind)
        => entityKind switch
        {
            MemorySourceEntityKind.ProjectNode => CognitiveMemoryEvidenceAnchorKind.MindMapNode,
            MemorySourceEntityKind.ProjectLink => CognitiveMemoryEvidenceAnchorKind.StructuredPath,
            MemorySourceEntityKind.ProcessRun or
            MemorySourceEntityKind.ProcessStepEvidence or
            MemorySourceEntityKind.ProcessRunAssignment or
            MemorySourceEntityKind.ProcessWorkBrief or
            MemorySourceEntityKind.ProcessDecision or
            MemorySourceEntityKind.ProcessJournal or
            MemorySourceEntityKind.ProcessConformanceObservation or
            MemorySourceEntityKind.ProcessImprovementCandidate or
            MemorySourceEntityKind.ProcessWorkflowRunLink => CognitiveMemoryEvidenceAnchorKind.ProcessEvent,
            MemorySourceEntityKind.ProcessArtifact or
            MemorySourceEntityKind.WorkflowArtifact => CognitiveMemoryEvidenceAnchorKind.WorkflowArtifact,
            MemorySourceEntityKind.WorkflowRun or
            MemorySourceEntityKind.WorkflowEvent or
            MemorySourceEntityKind.WorkflowExternalRequest => CognitiveMemoryEvidenceAnchorKind.WorkflowArtifact,
            _ => CognitiveMemoryEvidenceAnchorKind.StructuredPath
        };

    private static CognitiveMemoryRedactionState MapRedactionState(MemorySourcePermissionContext permission)
        => permission switch
        {
            { ContainsSensitivePayload: true } => CognitiveMemoryRedactionState.Redacted,
            { AccessMode: MemorySourceAccessMode.Redacted } => CognitiveMemoryRedactionState.Redacted,
            { Sensitivity: MemorySourceSensitivity.Sensitive } => CognitiveMemoryRedactionState.Restricted,
            _ => CognitiveMemoryRedactionState.Safe
        };

    private static CognitiveMemoryAccessLevel MapAccessLevel(MemorySourcePermissionContext permission)
        => permission.Sensitivity switch
        {
            MemorySourceSensitivity.Public => CognitiveMemoryAccessLevel.Public,
            MemorySourceSensitivity.Sensitive => CognitiveMemoryAccessLevel.Restricted,
            _ => CognitiveMemoryAccessLevel.Project
        };

    private static string ResolveLocator(MemorySourceItem sourceItem)
        => sourceItem.StorageReference?.Locator ?? sourceItem.Provenance.SourceRoute;

    private static Guid? ResolveProjectId(CognitiveMemorySourceIngestionRequest request)
        => request.ProjectId ?? (request.SourceKind == MemorySourceKind.WorkbenchProjectStructure ? request.ScopeId : null);

    private static Guid? NormalizeOptionalScope(Guid scopeId)
        => scopeId == Guid.Empty ? null : scopeId;

    private static void ValidateRequest(CognitiveMemorySourceIngestionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.SourceKind == MemorySourceKind.WorkbenchProjectStructure && request.ScopeId == Guid.Empty)
        {
            throw new ArgumentException("Workbench project structure ingestion requires a non-empty project scope id.", nameof(request));
        }
    }

    private static string ComputeRequestHash(CognitiveMemorySourceIngestionRequest request)
        => CognitiveMemoryHash.FromUtf8(string.Join(
            "|",
            request.SourceKind,
            request.ScopeId.ToString("D"),
            request.ProjectId?.ToString("D") ?? string.Empty,
            request.Cursor?.Value ?? string.Empty,
            request.Take?.ToString() ?? string.Empty)).Value;

    private static string NormalizeHash(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var candidate = value.Split(':', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? value;
        return candidate.Length == 64 && candidate.All(Uri.IsHexDigit)
            ? candidate.ToLowerInvariant()
            : CognitiveMemoryHash.FromUtf8(value).Value;
    }

    private static string NormalizeKey(string value)
        => CognitiveMemoryGuard.EnsureText(value, nameof(value)).ToLowerInvariant();

    private static string AnchorKey(Guid sourceItemId, string sourceHash)
        => $"{sourceItemId:D}|{sourceHash}";

    private static string GraphLinkKey(string sourceItemKey, string targetSourceItemKey, string linkKind)
        => $"{sourceItemKey}|{targetSourceItemKey}|{linkKind}";

    private static string ContextHintKey(Guid sourceItemId, Guid contextFrameId)
        => $"{sourceItemId:D}|{contextFrameId:D}";

    private static string Truncate(string value, int maxLength)
    {
        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private sealed record CognitiveMemorySourceIngestionPersistenceResult(
        Guid ManifestId,
        int CreatedSourceItemCount,
        int UpdatedSourceItemCount,
        int CreatedEvidenceAnchorCount,
        int CreatedContextHintCount,
        int CreatedLayoutCount,
        int CreatedGraphLinkCount,
        int CreatedTombstoneCount);

    private sealed record CognitiveMemorySourceContextFrame(
        CognitiveMemoryContextFrameRecord Frame,
        CognitiveMemoryContextDimensionKind DimensionKind,
        string ValueKey);
}
