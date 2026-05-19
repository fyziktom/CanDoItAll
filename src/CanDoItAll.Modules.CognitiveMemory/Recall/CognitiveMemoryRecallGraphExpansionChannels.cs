using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.CognitiveMemory;

public sealed partial class CognitiveMemoryRecallOrchestrator
{
    private async Task AddGraphExpansionCandidatesAsync(
        AppDbContext dbContext,
        CognitiveMemoryRecallRequest request,
        Dictionary<Guid, RecallCandidateAccumulator> candidates,
        List<CognitiveMemoryRecallTraceStage> stages,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        if (request.Budget.GraphExpansionDepth == 0 || candidates.Count == 0)
        {
            stages.Add(Stage(
                CognitiveMemoryRecallTraceStageKind.AssociationExpansion,
                CognitiveMemoryRecallChannelKind.Graph,
                CognitiveMemoryRecallStageStatus.Skipped,
                0,
                0,
                0,
                "graph:disabled-or-empty",
                completedAtUtc: nowUtc));
            return;
        }

        var frontier = candidates.Keys.ToArray();
        var relationLimit = Math.Max(request.Budget.CoarseCandidateLimit * Math.Max(1, request.Budget.GraphExpansionDepth), 1);
        var relations = await dbContext.Set<CognitiveMemoryRelationRecord>()
            .AsNoTracking()
            .Where(relation =>
                relation.ProjectId == request.ProjectId &&
                (frontier.Contains(relation.SourceMemoryRecordId) || frontier.Contains(relation.TargetMemoryRecordId)))
            .OrderBy(relation => relation.RelationKind)
            .Take(relationLimit)
            .Select(relation => new RelationSnapshot(
                relation.SourceMemoryRecordId,
                relation.TargetMemoryRecordId,
                relation.RelationKind,
                relation.DisplayStrengthProjection,
                relation.Reason))
            .ToListAsync(cancellationToken);
        var neighborIds = relations
            .Select(relation => frontier.Contains(relation.SourceMemoryRecordId) ? relation.TargetMemoryRecordId : relation.SourceMemoryRecordId)
            .Distinct()
            .Where(id => !candidates.ContainsKey(id))
            .ToArray();
        var records = await LoadRecordsByIdAsync(dbContext, request, neighborIds, cancellationToken);
        var recordsById = records.ToDictionary(record => record.Id);

        foreach (var relation in relations)
        {
            var neighborId = frontier.Contains(relation.SourceMemoryRecordId)
                ? relation.TargetMemoryRecordId
                : relation.SourceMemoryRecordId;
            if (!recordsById.TryGetValue(neighborId, out var record) && !candidates.TryGetValue(neighborId, out _))
            {
                continue;
            }

            var candidate = recordsById.TryGetValue(neighborId, out var loaded)
                ? GetCandidate(candidates, loaded)
                : candidates[neighborId];
            candidate.Channels.Add(CognitiveMemoryRecallChannelKind.Graph);
            candidate.GraphProximity = Math.Max(candidate.GraphProximity ?? 0, relation.DisplayStrengthProjection ?? 0.65);
            if (relation.RelationKind == CognitiveMemoryRelationKind.SemanticallyRelatedButContextSeparated)
            {
                candidate.ContextSeparation = Math.Max(candidate.ContextSeparation ?? 0, 0.95);
                candidate.ContextBoundaryReason = string.IsNullOrWhiteSpace(relation.Reason)
                    ? "Graph relation marks this memory as related but context separated."
                    : relation.Reason;
            }

            if (relation.RelationKind == CognitiveMemoryRelationKind.Contradicts)
            {
                candidate.ContradictionPressure = Math.Max(candidate.ContradictionPressure ?? 0, 0.8);
            }

            candidate.Reasons.Add($"Graph expansion followed relation {relation.RelationKind}.");
        }

        var sourceGraphExpansion = await AddSourceGraphExpansionCandidatesAsync(
            dbContext,
            request,
            candidates,
            cancellationToken);

        stages.Add(Stage(
            CognitiveMemoryRecallTraceStageKind.AssociationExpansion,
            CognitiveMemoryRecallChannelKind.Graph,
            CognitiveMemoryRecallStageStatus.Completed,
            relations.Count + sourceGraphExpansion.EdgeCount,
            records.Count + sourceGraphExpansion.RecordCount,
            0,
            $"graph:relations:{relations.Count}:source-edges:{sourceGraphExpansion.EdgeCount}:source-records:{sourceGraphExpansion.RecordCount}",
            limitingBudget: relations.Count >= relationLimit || sourceGraphExpansion.Limited ? CognitiveMemoryBudgetLimit.ItemCount : null,
            completedAtUtc: nowUtc));
    }

    private async Task<SourceGraphExpansionResult> AddSourceGraphExpansionCandidatesAsync(
        AppDbContext dbContext,
        CognitiveMemoryRecallRequest request,
        Dictionary<Guid, RecallCandidateAccumulator> candidates,
        CancellationToken cancellationToken)
    {
        var sourceExpansionSeedRecordIds = candidates.Values
            .Where(IsSourceGraphExpansionSeed)
            .Select(candidate => candidate.Record.Id)
            .Distinct()
            .ToArray();
        var frontierItems = (await LoadSourceGraphItemsForRecordsAsync(
                dbContext,
                sourceExpansionSeedRecordIds,
                cancellationToken))
            .Where(CanUseAsSourceGraphFrontier)
            .GroupBy(item => item.SourceItemKey, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToList();
        if (frontierItems.Count == 0)
        {
            return new SourceGraphExpansionResult(0, 0, Limited: false);
        }

        var visitedSourceItemKeys = frontierItems
            .Select(item => item.SourceItemKey)
            .ToHashSet(StringComparer.Ordinal);
        var edgeCount = 0;
        var recordCount = 0;
        var limited = false;
        var expansionLimit = Math.Max(request.Budget.CoarseCandidateLimit * Math.Max(1, request.Budget.GraphExpansionDepth), 1);

        for (var depth = 1; depth <= request.Budget.GraphExpansionDepth; depth++)
        {
            var nextItems = await LoadNeighborSourceGraphItemsAsync(
                dbContext,
                request,
                frontierItems,
                expansionLimit,
                cancellationToken);
            var unseenItems = nextItems
                .Where(item => visitedSourceItemKeys.Add(item.SourceItemKey))
                .Take(expansionLimit)
                .ToList();
            if (unseenItems.Count == 0)
            {
                break;
            }

            edgeCount += unseenItems.Count;
            limited |= nextItems.Count >= expansionLimit;
            var linkedRecordIds = await dbContext.Set<CognitiveMemorySourceLinkRecord>()
                .AsNoTracking()
                .Where(link => unseenItems.Select(item => item.Id).Contains(link.SourceItemId))
                .Select(link => link.MemoryRecordId)
                .Distinct()
                .ToListAsync(cancellationToken);
            var records = await LoadRecordsByIdAsync(dbContext, request, linkedRecordIds, cancellationToken);
            foreach (var record in records)
            {
                var candidate = GetCandidate(candidates, record);
                candidate.Channels.Add(CognitiveMemoryRecallChannelKind.Graph);
                candidate.GraphProximity = Math.Max(candidate.GraphProximity ?? 0, ResolveSourceGraphProximity(depth));
                candidate.Reasons.Add("Graph expansion followed source item structure.");
            }

            recordCount += records.Count;
            frontierItems = unseenItems;
        }

        return new SourceGraphExpansionResult(edgeCount, recordCount, limited);
    }

    private static async Task<IReadOnlyList<SourceGraphItemSnapshot>> LoadSourceGraphItemsForRecordsAsync(
        AppDbContext dbContext,
        IReadOnlyList<Guid> recordIds,
        CancellationToken cancellationToken)
    {
        if (recordIds.Count == 0)
        {
            return [];
        }

        var sourceItemIds = await dbContext.Set<CognitiveMemorySourceLinkRecord>()
            .AsNoTracking()
            .Where(link => recordIds.Contains(link.MemoryRecordId))
            .Select(link => link.SourceItemId)
            .Distinct()
            .ToListAsync(cancellationToken);
        return await LoadSourceGraphItemsByIdAsync(dbContext, sourceItemIds, cancellationToken);
    }

    private static async Task<IReadOnlyList<SourceGraphItemSnapshot>> LoadSourceGraphItemsByIdAsync(
        AppDbContext dbContext,
        IReadOnlyList<Guid> sourceItemIds,
        CancellationToken cancellationToken)
    {
        if (sourceItemIds.Count == 0)
        {
            return [];
        }

        return await dbContext.Set<CognitiveMemorySourceItemRecord>()
            .AsNoTracking()
            .Where(item => sourceItemIds.Contains(item.Id))
            .Select(item => new SourceGraphItemSnapshot(
                item.Id,
                item.SourceManifestId,
                item.ProjectId,
                item.SourceSystem,
                item.SourceItemType,
                item.SourceItemKey,
                item.Title,
                item.Locator,
                item.ProvenanceJson))
            .ToListAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<SourceGraphItemSnapshot>> LoadNeighborSourceGraphItemsAsync(
        AppDbContext dbContext,
        CognitiveMemoryRecallRequest request,
        IReadOnlyList<SourceGraphItemSnapshot> frontierItems,
        int expansionLimit,
        CancellationToken cancellationToken)
    {
        var structuralItems = await LoadProjectStructureNeighborItemsAsync(
            dbContext,
            request,
            frontierItems,
            expansionLimit,
            cancellationToken);
        var externalFileItems = await LoadExternalFileNeighborItemsAsync(
            dbContext,
            request,
            frontierItems,
            expansionLimit,
            cancellationToken);
        return structuralItems
            .Concat(externalFileItems)
            .GroupBy(item => item.SourceItemKey, StringComparer.Ordinal)
            .Select(group => group.First())
            .Take(expansionLimit)
            .ToList();
    }

    private static async Task<IReadOnlyList<SourceGraphItemSnapshot>> LoadExplicitSourceGraphNeighborItemsAsync(
        AppDbContext dbContext,
        CognitiveMemoryRecallRequest request,
        IReadOnlyList<SourceGraphItemSnapshot> frontierItems,
        int expansionLimit,
        CancellationToken cancellationToken)
    {
        var sourceItemKeys = frontierItems.Select(item => item.SourceItemKey).Distinct(StringComparer.Ordinal).ToArray();
        var sourceManifestIds = frontierItems.Select(item => item.SourceManifestId).Distinct().ToArray();
        if (sourceItemKeys.Length == 0 || sourceManifestIds.Length == 0)
        {
            return [];
        }

        var links = await dbContext.Set<CognitiveMemorySourceItemGraphLinkRecord>()
            .AsNoTracking()
            .Where(link =>
                link.ProjectId == request.ProjectId &&
                sourceManifestIds.Contains(link.SourceManifestId) &&
                (sourceItemKeys.Contains(link.SourceItemKey) || sourceItemKeys.Contains(link.TargetSourceItemKey)))
            .Take(expansionLimit)
            .Select(link => new
            {
                link.SourceManifestId,
                link.SourceItemKey,
                link.TargetSourceItemKey
            })
            .ToListAsync(cancellationToken);
        var neighborKeys = links
            .Select(link => sourceItemKeys.Contains(link.SourceItemKey) ? link.TargetSourceItemKey : link.SourceItemKey)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (neighborKeys.Length == 0)
        {
            return [];
        }

        return await dbContext.Set<CognitiveMemorySourceItemRecord>()
            .AsNoTracking()
            .Where(item =>
                item.ProjectId == request.ProjectId &&
                sourceManifestIds.Contains(item.SourceManifestId) &&
                neighborKeys.Contains(item.SourceItemKey))
            .Take(expansionLimit)
            .Select(item => new SourceGraphItemSnapshot(
                item.Id,
                item.SourceManifestId,
                item.ProjectId,
                item.SourceSystem,
                item.SourceItemType,
                item.SourceItemKey,
                item.Title,
                item.Locator,
                item.ProvenanceJson))
            .ToListAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<SourceGraphItemSnapshot>> LoadProjectStructureNeighborItemsAsync(
        AppDbContext dbContext,
        CognitiveMemoryRecallRequest request,
        IReadOnlyList<SourceGraphItemSnapshot> frontierItems,
        int expansionLimit,
        CancellationToken cancellationToken)
    {
        var projectStructureFrontier = frontierItems
            .Where(item => item.SourceSystem == WorkbenchProjectStructureSourceSystem &&
                           item.SourceItemType == ProjectNodeSourceItemType)
            .Select(item => new
            {
                Item = item,
                Node = TryReadProjectStructureNode(item.ProvenanceJson)
            })
            .Where(item => item.Node is not null)
            .ToList();
        if (projectStructureFrontier.Count == 0)
        {
            return [];
        }

        var manifestIds = projectStructureFrontier
            .Select(item => item.Item.SourceManifestId)
            .Distinct()
            .ToArray();
        var frontierEntityIds = projectStructureFrontier
            .Select(item => item.Node!.SourceEntityId)
            .ToHashSet(StringComparer.Ordinal);
        var frontierParentIds = projectStructureFrontier
            .Select(item => item.Node!.ParentId)
            .Where(parentId => !string.IsNullOrWhiteSpace(parentId))
            .ToHashSet(StringComparer.Ordinal);
        var sourceItems = await dbContext.Set<CognitiveMemorySourceItemRecord>()
            .AsNoTracking()
            .Where(item =>
                item.ProjectId == request.ProjectId &&
                manifestIds.Contains(item.SourceManifestId) &&
                item.SourceSystem == WorkbenchProjectStructureSourceSystem &&
                item.SourceItemType == ProjectNodeSourceItemType)
            .Select(item => new SourceGraphItemSnapshot(
                item.Id,
                item.SourceManifestId,
                item.ProjectId,
                item.SourceSystem,
                item.SourceItemType,
                item.SourceItemKey,
                item.Title,
                item.Locator,
                item.ProvenanceJson))
            .ToListAsync(cancellationToken);

        return sourceItems
            .Select(item => new
            {
                Item = item,
                Node = TryReadProjectStructureNode(item.ProvenanceJson)
            })
            .Where(item => item.Node is not null &&
                           (frontierEntityIds.Contains(item.Node.ParentId) ||
                            frontierParentIds.Contains(item.Node.SourceEntityId) &&
                            !string.IsNullOrWhiteSpace(item.Node.ParentId)))
            .Select(item => item.Item)
            .GroupBy(item => item.SourceItemKey, StringComparer.Ordinal)
            .Select(group => group.First())
            .Take(expansionLimit)
            .ToList();
    }

    private static async Task<IReadOnlyList<SourceGraphItemSnapshot>> LoadExternalFileNeighborItemsAsync(
        AppDbContext dbContext,
        CognitiveMemoryRecallRequest request,
        IReadOnlyList<SourceGraphItemSnapshot> frontierItems,
        int expansionLimit,
        CancellationToken cancellationToken)
    {
        var externalFrontier = frontierItems
            .Where(item => item.SourceSystem == ExternalFileSourceSystem &&
                           !string.IsNullOrWhiteSpace(item.Locator))
            .Select(item => new
            {
                item.SourceManifestId,
                DocumentLocator = ResolveDocumentLocator(item.Locator)
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.DocumentLocator))
            .Distinct()
            .ToList();
        if (externalFrontier.Count == 0)
        {
            return [];
        }

        var manifestIds = externalFrontier.Select(item => item.SourceManifestId).Distinct().ToArray();
        var documentLocators = externalFrontier
            .Select(item => item.DocumentLocator)
            .ToHashSet(StringComparer.Ordinal);
        var sourceItems = await dbContext.Set<CognitiveMemorySourceItemRecord>()
            .AsNoTracking()
            .Where(item =>
                item.ProjectId == request.ProjectId &&
                manifestIds.Contains(item.SourceManifestId) &&
                item.SourceSystem == ExternalFileSourceSystem &&
                item.Locator != null)
            .Select(item => new SourceGraphItemSnapshot(
                item.Id,
                item.SourceManifestId,
                item.ProjectId,
                item.SourceSystem,
                item.SourceItemType,
                item.SourceItemKey,
                item.Title,
                item.Locator,
                item.ProvenanceJson))
            .ToListAsync(cancellationToken);

        return sourceItems
            .Where(item => documentLocators.Contains(ResolveDocumentLocator(item.Locator)))
            .GroupBy(item => item.SourceItemKey, StringComparer.Ordinal)
            .Select(group => group.First())
            .Take(expansionLimit)
            .ToList();
    }
}
