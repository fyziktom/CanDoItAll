using CanDoItAll.Infrastructure.ControlPlane;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.CognitiveMemory;

public sealed class CognitiveMemorySourceTruthDatabaseTransferHandler : IDatabaseTransferHandler
{
    public DatabaseTransferItemDescriptor Descriptor { get; } = new(
        "cognitive-memory-source-truth",
        "Cognitive Memory source truth",
        "Copies cognitive-memory source manifests, source items, evidence anchors, and external source ingestion rows.",
        SortOrder: 45);

    public async Task<DatabaseTransferItemPreview> PreviewAsync(
        DatabaseTransferContext context,
        CancellationToken cancellationToken = default)
    {
        var sourceCounts = await CognitiveMemorySourceTruthTransferDataSet.CountAsync(context.SourceDbContext, cancellationToken);
        var targetCounts = await CognitiveMemorySourceTruthTransferDataSet.CountAsync(context.TargetDbContext, cancellationToken);
        var sourceSummary = $"{sourceCounts.Manifests} manifest(s), {sourceCounts.SourceItems} source item(s), {sourceCounts.EvidenceAnchors} evidence anchor(s), and {sourceCounts.ExternalIngestions} external ingestion row(s) are available.";
        var warning = sourceCounts.Total == 0
            ? "The source database does not contain cognitive-memory source truth."
            : null;

        return new DatabaseTransferItemPreview(
            Descriptor,
            sourceCounts.Total > 0,
            sourceSummary,
            warning,
            sourceCounts.Total,
            targetCounts.Total);
    }

    public async Task<DatabaseTransferItemResult> TransferAsync(
        DatabaseTransferContext context,
        CancellationToken cancellationToken = default)
    {
        var sourceData = await CognitiveMemorySourceTruthTransferDataSet.LoadAsync(context.SourceDbContext, cancellationToken);
        if (sourceData.Counts.Total == 0)
        {
            return new DatabaseTransferItemResult(
                Descriptor.Key,
                Descriptor.Label,
                false,
                "The source database has no cognitive-memory source truth to transfer.",
                0);
        }

        var targetCounts = await CognitiveMemorySourceTruthTransferDataSet.CountAsync(context.TargetDbContext, cancellationToken);
        if (!context.ReplaceExisting && targetCounts.Total > 0)
        {
            return new DatabaseTransferItemResult(
                Descriptor.Key,
                Descriptor.Label,
                false,
                "The target database already has cognitive-memory source truth. Enable replacement before transferring it.",
                0);
        }

        if (context.ReplaceExisting && targetCounts.Total > 0 && await HasDependentMemoryDataAsync(context.TargetDbContext, cancellationToken))
        {
            return new DatabaseTransferItemResult(
                Descriptor.Key,
                Descriptor.Label,
                false,
                "The target database already has generated cognitive-memory records that depend on source truth. Use a clean target database or delete generated memory first.",
                0);
        }

        if (context.ReplaceExisting)
        {
            await CognitiveMemorySourceTruthTransferDataSet.ClearAsync(context.TargetDbContext, cancellationToken);
        }

        await CognitiveMemorySourceTruthTransferDataSet.SaveAsync(context.TargetDbContext, sourceData, cancellationToken);

        return new DatabaseTransferItemResult(
            Descriptor.Key,
            Descriptor.Label,
            true,
            $"Copied cognitive-memory source truth with {sourceData.Counts.SourceItems} source item(s) and {sourceData.Counts.EvidenceAnchors} evidence anchor(s).",
            sourceData.Counts.Total);
    }

    private static async Task<bool> HasDependentMemoryDataAsync(
        DbContext dbContext,
        CancellationToken cancellationToken)
        => await dbContext.Set<CognitiveMemoryRecord>().AnyAsync(cancellationToken) ||
           await dbContext.Set<CognitiveMemorySourceLinkRecord>().AnyAsync(cancellationToken) ||
           await dbContext.Set<CognitiveMemoryClaimEvidenceLinkRecord>().AnyAsync(cancellationToken) ||
           await dbContext.Set<CognitiveMemoryRecordEvidenceAnchorRecord>().AnyAsync(cancellationToken);
}

internal sealed record CognitiveMemorySourceTruthTransferCounts(
    int Manifests,
    int SourceItems,
    int EvidenceAnchors,
    int ExternalIngestions)
{
    public int Total => Manifests + SourceItems + EvidenceAnchors + ExternalIngestions;
}

internal sealed record CognitiveMemorySourceTruthTransferDataSet(
    IReadOnlyList<CognitiveMemorySourceManifestRecord> Manifests,
    IReadOnlyList<CognitiveMemorySourceItemRecord> SourceItems,
    IReadOnlyList<CognitiveMemoryEvidenceAnchorRecord> EvidenceAnchors,
    IReadOnlyList<CognitiveMemoryExternalSourceIngestionRecord> ExternalIngestions)
{
    public CognitiveMemorySourceTruthTransferCounts Counts { get; } = new(
        Manifests.Count,
        SourceItems.Count,
        EvidenceAnchors.Count,
        ExternalIngestions.Count);

    public static async Task<CognitiveMemorySourceTruthTransferCounts> CountAsync(
        DbContext dbContext,
        CancellationToken cancellationToken)
        => new(
            await dbContext.Set<CognitiveMemorySourceManifestRecord>().CountAsync(cancellationToken),
            await dbContext.Set<CognitiveMemorySourceItemRecord>().CountAsync(cancellationToken),
            await dbContext.Set<CognitiveMemoryEvidenceAnchorRecord>().CountAsync(cancellationToken),
            await dbContext.Set<CognitiveMemoryExternalSourceIngestionRecord>().CountAsync(cancellationToken));

    public static async Task<CognitiveMemorySourceTruthTransferDataSet> LoadAsync(
        DbContext dbContext,
        CancellationToken cancellationToken)
    {
        var manifests = await dbContext.Set<CognitiveMemorySourceManifestRecord>()
            .AsNoTracking()
            .OrderBy(item => item.Id)
            .ToListAsync(cancellationToken);
        var sourceItems = await dbContext.Set<CognitiveMemorySourceItemRecord>()
            .AsNoTracking()
            .OrderBy(item => item.Id)
            .ToListAsync(cancellationToken);
        var evidenceAnchors = await dbContext.Set<CognitiveMemoryEvidenceAnchorRecord>()
            .AsNoTracking()
            .OrderBy(item => item.Id)
            .ToListAsync(cancellationToken);
        var externalIngestions = await dbContext.Set<CognitiveMemoryExternalSourceIngestionRecord>()
            .AsNoTracking()
            .OrderBy(item => item.Id)
            .ToListAsync(cancellationToken);

        return new CognitiveMemorySourceTruthTransferDataSet(
            manifests.Select(CloneManifest).ToArray(),
            sourceItems.Select(CloneSourceItem).ToArray(),
            evidenceAnchors.Select(CloneEvidenceAnchor).ToArray(),
            externalIngestions.Select(CloneExternalIngestion).ToArray());
    }

    public static async Task ClearAsync(
        DbContext dbContext,
        CancellationToken cancellationToken)
    {
        await RemoveAndSaveAsync<CognitiveMemoryExternalSourceIngestionRecord>(dbContext, cancellationToken);
        await RemoveAndSaveAsync<CognitiveMemoryEvidenceAnchorRecord>(dbContext, cancellationToken);
        await RemoveAndSaveAsync<CognitiveMemorySourceItemRecord>(dbContext, cancellationToken);
        await RemoveAndSaveAsync<CognitiveMemorySourceManifestRecord>(dbContext, cancellationToken);
    }

    public static async Task SaveAsync(
        DbContext dbContext,
        CognitiveMemorySourceTruthTransferDataSet dataSet,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dataSet);
        await AddAndSaveAsync(dbContext, dataSet.Manifests, cancellationToken);
        await AddAndSaveAsync(dbContext, dataSet.SourceItems, cancellationToken);
        await AddAndSaveAsync(dbContext, dataSet.EvidenceAnchors, cancellationToken);
        await AddAndSaveAsync(dbContext, dataSet.ExternalIngestions, cancellationToken);
    }

    private static CognitiveMemorySourceManifestRecord CloneManifest(CognitiveMemorySourceManifestRecord item)
        => new()
        {
            Id = item.Id,
            ProjectId = item.ProjectId,
            SourceSystem = item.SourceSystem,
            SourceScopeKey = item.SourceScopeKey,
            SourceSnapshotId = item.SourceSnapshotId,
            SnapshotHashAlgorithm = item.SnapshotHashAlgorithm,
            SnapshotHash = item.SnapshotHash,
            ProviderVersion = item.ProviderVersion,
            Cursor = item.Cursor,
            ScanStatus = item.ScanStatus,
            ObservedAtUtc = item.ObservedAtUtc,
            CreatedAtUtc = item.CreatedAtUtc,
            UpdatedAtUtc = item.UpdatedAtUtc,
            ConcurrencyToken = item.ConcurrencyToken
        };

    private static CognitiveMemorySourceItemRecord CloneSourceItem(CognitiveMemorySourceItemRecord item)
        => new()
        {
            Id = item.Id,
            SourceManifestId = item.SourceManifestId,
            ProjectId = item.ProjectId,
            SourceSystem = item.SourceSystem,
            SourceItemKey = item.SourceItemKey,
            SourceItemType = item.SourceItemType,
            Title = item.Title,
            ContentText = item.ContentText,
            Locator = item.Locator,
            ContentHashAlgorithm = item.ContentHashAlgorithm,
            ContentHash = item.ContentHash,
            RedactionState = item.RedactionState,
            AccessLevel = item.AccessLevel,
            AccessScope = item.AccessScope,
            ProvenanceJson = item.ProvenanceJson,
            ObservedAtUtc = item.ObservedAtUtc,
            CreatedAtUtc = item.CreatedAtUtc,
            UpdatedAtUtc = item.UpdatedAtUtc,
            ConcurrencyToken = item.ConcurrencyToken
        };

    private static CognitiveMemoryEvidenceAnchorRecord CloneEvidenceAnchor(CognitiveMemoryEvidenceAnchorRecord item)
        => new()
        {
            Id = item.Id,
            ProjectId = item.ProjectId,
            AnchorKind = item.AnchorKind,
            SourceManifestId = item.SourceManifestId,
            SourceItemId = item.SourceItemId,
            SourceSystem = item.SourceSystem,
            Locator = item.Locator,
            StructuredPath = item.StructuredPath,
            TextStart = item.TextStart,
            TextEnd = item.TextEnd,
            QuoteHash = item.QuoteHash,
            TrustLevel = item.TrustLevel,
            RedactionState = item.RedactionState,
            SourceHashAlgorithm = item.SourceHashAlgorithm,
            SourceHash = item.SourceHash,
            ObservedAtUtc = item.ObservedAtUtc,
            CreatedAtUtc = item.CreatedAtUtc,
            ConcurrencyToken = item.ConcurrencyToken
        };

    private static CognitiveMemoryExternalSourceIngestionRecord CloneExternalIngestion(CognitiveMemoryExternalSourceIngestionRecord item)
        => new()
        {
            Id = item.Id,
            ProjectId = item.ProjectId,
            SourceKind = item.SourceKind,
            Status = item.Status,
            Title = item.Title,
            Locator = item.Locator,
            ContentType = item.ContentType,
            ContentLength = item.ContentLength,
            ProgressPercent = item.ProgressPercent,
            StatusMessage = item.StatusMessage,
            SourceManifestId = item.SourceManifestId,
            SourceItemId = item.SourceItemId,
            EvidenceAnchorId = item.EvidenceAnchorId,
            FailureMessage = item.FailureMessage,
            CreatedAtUtc = item.CreatedAtUtc,
            UpdatedAtUtc = item.UpdatedAtUtc,
            CompletedAtUtc = item.CompletedAtUtc,
            ConcurrencyToken = item.ConcurrencyToken
        };

    private static async Task AddAndSaveAsync<T>(
        DbContext dbContext,
        IReadOnlyCollection<T> entities,
        CancellationToken cancellationToken)
        where T : class
    {
        if (entities.Count == 0)
        {
            return;
        }

        await dbContext.Set<T>().AddRangeAsync(entities, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task RemoveAndSaveAsync<T>(
        DbContext dbContext,
        CancellationToken cancellationToken)
        where T : class
    {
        var entities = await dbContext.Set<T>().ToListAsync(cancellationToken);
        if (entities.Count == 0)
        {
            return;
        }

        dbContext.RemoveRange(entities);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
