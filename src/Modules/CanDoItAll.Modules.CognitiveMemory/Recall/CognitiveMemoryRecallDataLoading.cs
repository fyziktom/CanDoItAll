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
    private IQueryable<CognitiveMemoryRecord> BuildRecordQuery(
        AppDbContext dbContext,
        CognitiveMemoryRecallRequest request)
    {
        var query = dbContext.Set<CognitiveMemoryRecord>()
            .AsNoTracking()
            .Where(record =>
                record.ProjectId == request.ProjectId &&
                record.ValidationState != CognitiveMemoryValidationState.Rejected &&
                record.ValidationState != CognitiveMemoryValidationState.Retired &&
                record.ValidationState != CognitiveMemoryValidationState.Superseded);
        var preferredKinds = NormalizePreferredKinds(request.PreferredRecordKinds);
        if (preferredKinds.Count > 0)
        {
            query = query.Where(record => preferredKinds.Contains(record.Kind));
        }

        if (!request.PolicyContext.AllowRestrictedContent)
        {
            query = query.Where(record => record.AccessLevel <= request.PolicyContext.AccessLevel);
        }

        return ExcludeActiveProfessorAnchorRecords(dbContext, request, query);
    }

    private static IQueryable<CognitiveMemoryRecord> BuildRecordQuery(
        AppDbContext dbContext,
        CognitiveMemoryRecallRequest request,
        IReadOnlyList<Guid> recordIds)
        => BuildRecordQueryStatic(dbContext, request, recordIds);

    private static IQueryable<CognitiveMemoryRecord> BuildRecordQueryStatic(
        AppDbContext dbContext,
        CognitiveMemoryRecallRequest request,
        IReadOnlyList<Guid> recordIds)
    {
        var query = dbContext.Set<CognitiveMemoryRecord>()
            .AsNoTracking()
            .Where(record =>
                recordIds.Contains(record.Id) &&
                record.ProjectId == request.ProjectId &&
                record.ValidationState != CognitiveMemoryValidationState.Rejected &&
                record.ValidationState != CognitiveMemoryValidationState.Retired &&
                record.ValidationState != CognitiveMemoryValidationState.Superseded);
        if (!request.PolicyContext.AllowRestrictedContent)
        {
            query = query.Where(record => record.AccessLevel <= request.PolicyContext.AccessLevel);
        }

        return ExcludeActiveProfessorAnchorRecords(dbContext, request, query);
    }

    private static IQueryable<CognitiveMemoryRecord> ExcludeActiveProfessorAnchorRecords(
        AppDbContext dbContext,
        CognitiveMemoryRecallRequest request,
        IQueryable<CognitiveMemoryRecord> query)
    {
        if (ShouldIncludeActiveProfessorAnchors(request))
        {
            return query;
        }

        var activeAnchorMemoryRecordIds = dbContext.Set<CognitiveMemoryCuratorCapturedImprovementRecord>()
            .AsNoTracking()
            .Where(capture =>
                capture.ProjectId == request.ProjectId &&
                capture.AppliedMemoryRecordId != null &&
                (capture.AnchorState == CognitiveMemoryProfessorAnchorState.Active ||
                 capture.AnchorState == CognitiveMemoryProfessorAnchorState.Comparing))
            .Select(capture => capture.AppliedMemoryRecordId!.Value);
        return query.Where(record => !activeAnchorMemoryRecordIds.Contains(record.Id));
    }

    private static bool ShouldIncludeActiveProfessorAnchors(CognitiveMemoryRecallRequest request)
        => request.Metadata is not null &&
           request.Metadata.TryGetValue("includeProfessorAnchors", out var value) &&
           bool.TryParse(value, out var include) &&
           include;

    private async Task<IReadOnlyList<MemoryRecordSnapshot>> LoadRecordsByIdAsync(
        AppDbContext dbContext,
        CognitiveMemoryRecallRequest request,
        IReadOnlyList<Guid> recordIds,
        CancellationToken cancellationToken)
    {
        if (recordIds.Count == 0)
        {
            return [];
        }

        return await BuildRecordQuery(dbContext, request, recordIds)
            .Select(record => new MemoryRecordSnapshot(
                record.Id,
                record.ProjectId,
                record.Kind,
                record.Title,
                record.SummaryText,
                record.CanonicalText,
                record.TopicKey,
                record.ValidationState,
                record.StabilityState,
                record.SourceEvidenceCount,
                record.EvidenceAnchorCount,
                record.PrimaryClaimId,
                record.PrimaryContextFrameId,
                record.AccessLevel,
                record.RiskLevel,
                record.UpdatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<MemoryRecordSnapshot>> LoadRecordsByIdAsync(
        AppDbContext dbContext,
        IReadOnlyList<Guid> recordIds,
        CancellationToken cancellationToken)
    {
        if (recordIds.Count == 0)
        {
            return [];
        }

        return await dbContext.Set<CognitiveMemoryRecord>()
            .AsNoTracking()
            .Where(record => recordIds.Contains(record.Id))
            .Select(record => new MemoryRecordSnapshot(
                record.Id,
                record.ProjectId,
                record.Kind,
                record.Title,
                record.SummaryText,
                record.CanonicalText,
                record.TopicKey,
                record.ValidationState,
                record.StabilityState,
                record.SourceEvidenceCount,
                record.EvidenceAnchorCount,
                record.PrimaryClaimId,
                record.PrimaryContextFrameId,
                record.AccessLevel,
                record.RiskLevel,
                record.UpdatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    private static async Task<IReadOnlyDictionary<Guid, IReadOnlyList<ClaimSnapshot>>> LoadClaimsAsync(
        AppDbContext dbContext,
        IReadOnlyList<Guid> recordIds,
        CancellationToken cancellationToken)
    {
        if (recordIds.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<ClaimSnapshot>>();
        }

        var claims = await dbContext.Set<CognitiveMemoryClaimRecord>()
            .AsNoTracking()
            .Where(claim => claim.MemoryRecordId != null && recordIds.Contains(claim.MemoryRecordId.Value))
            .OrderBy(claim => claim.ClaimKind)
            .Take(recordIds.Count * 4)
            .Select(claim => new ClaimSnapshot(
                claim.Id,
                claim.MemoryRecordId!.Value,
                claim.ClaimKind,
                claim.CurrentBeliefState,
                claim.ValidationState,
                claim.PrimaryContextFrameId))
            .ToListAsync(cancellationToken);
        return claims
            .GroupBy(claim => claim.MemoryRecordId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<ClaimSnapshot>)group.ToArray());
    }

    private static async Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> LoadEvidenceAnchorIdsAsync(
        AppDbContext dbContext,
        IReadOnlyList<Guid> recordIds,
        IReadOnlyDictionary<Guid, IReadOnlyList<ClaimSnapshot>> claimsByRecordId,
        CancellationToken cancellationToken)
    {
        if (recordIds.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<Guid>>();
        }

        var recordEvidence = await dbContext.Set<CognitiveMemoryRecordEvidenceAnchorRecord>()
            .AsNoTracking()
            .Where(link => recordIds.Contains(link.MemoryRecordId))
            .Select(link => new
            {
                link.MemoryRecordId,
                link.EvidenceAnchorId
            })
            .ToListAsync(cancellationToken);
        var claimIds = claimsByRecordId.Values.SelectMany(claims => claims.Select(claim => claim.Id)).Distinct().ToArray();
        var claimEvidence = claimIds.Length == 0
            ? []
            : await dbContext.Set<CognitiveMemoryClaimEvidenceLinkRecord>()
                .AsNoTracking()
                .Where(link => claimIds.Contains(link.ClaimId))
                .Select(link => new
                {
                    link.ClaimId,
                    link.EvidenceAnchorId
                })
                .ToListAsync(cancellationToken);
        var recordIdByClaimId = claimsByRecordId.Values
            .SelectMany(claims => claims)
            .ToDictionary(claim => claim.Id, claim => claim.MemoryRecordId);
        var map = new Dictionary<Guid, HashSet<Guid>>();
        foreach (var item in recordEvidence)
        {
            GetEvidenceSet(map, item.MemoryRecordId).Add(item.EvidenceAnchorId);
        }

        foreach (var item in claimEvidence)
        {
            if (recordIdByClaimId.TryGetValue(item.ClaimId, out var memoryRecordId))
            {
                GetEvidenceSet(map, memoryRecordId).Add(item.EvidenceAnchorId);
            }
        }

        return map.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<Guid>)pair.Value.ToArray());
    }

    private static HashSet<Guid> GetEvidenceSet(Dictionary<Guid, HashSet<Guid>> map, Guid recordId)
    {
        if (map.TryGetValue(recordId, out var existing))
        {
            return existing;
        }

        var created = new HashSet<Guid>();
        map[recordId] = created;
        return created;
    }
}
