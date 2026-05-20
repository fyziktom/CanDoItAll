using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.CognitiveMemory;
public sealed class CognitiveMemoryAggregateMemoryApplicator(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ICognitiveMemoryRecordValidator recordValidator,
    IClock clock) : ICognitiveMemoryAggregateMemoryApplicator
{
    private const string AlgorithmVersion = "quality-aggregate-apply-v1";

    public async ValueTask<CognitiveMemoryAggregateMemoryApplyResult> ApplyAsync(
        CognitiveMemoryAggregateMemoryApplyRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ActorId);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var candidate = await dbContext.Set<CognitiveMemoryDreamAggregateCandidateRecord>()
            .SingleOrDefaultAsync(candidate => candidate.Id == request.AggregateCandidateId.Value, cancellationToken)
            ?? throw new InvalidOperationException($"Dream aggregate candidate '{request.AggregateCandidateId}' was not found.");
        if (candidate.MemoryRecordId is { } existingMemoryId)
        {
            var existingClaims = await dbContext.Set<CognitiveMemoryClaimRecord>()
                .AsNoTracking()
                .Where(claim => claim.MemoryRecordId == existingMemoryId)
                .Select(claim => new CognitiveMemoryClaimId(claim.Id))
                .ToArrayAsync(cancellationToken);
            return new CognitiveMemoryAggregateMemoryApplyResult(new CognitiveMemoryRecordId(existingMemoryId), existingClaims, Created: false);
        }

        if (candidate.Status != CognitiveMemoryDreamAggregateCandidateStatus.Approved)
        {
            throw new InvalidOperationException($"Dream aggregate candidate '{candidate.Id:D}' must be approved before it can be applied.");
        }

        var validation = (await dbContext.Set<CognitiveMemoryDreamValidationRecord>()
            .AsNoTracking()
            .Where(validation => validation.AggregateCandidateId == candidate.Id)
            .ToListAsync(cancellationToken))
            .OrderByDescending(validation => validation.CreatedAtUtc)
            .FirstOrDefault()
            ?? throw new InvalidOperationException($"Dream aggregate candidate '{candidate.Id:D}' has no validation record.");
        if (validation.Decision != CognitiveMemoryDreamValidationDecision.Approved)
        {
            throw new InvalidOperationException($"Dream aggregate candidate '{candidate.Id:D}' validation decision is '{validation.Decision}'.");
        }

        var claims = await dbContext.Set<CognitiveMemoryDreamAggregateClaimRecord>()
            .Where(claim => claim.AggregateCandidateId == candidate.Id)
            .OrderBy(claim => claim.Sequence)
            .ToListAsync(cancellationToken);
        var sourceMaps = await dbContext.Set<CognitiveMemoryDreamAggregateClaimSourceMapRecord>()
            .Where(sourceMap => sourceMap.AggregateCandidateId == candidate.Id)
            .ToListAsync(cancellationToken);
        if (claims.Count == 0 || claims.Any(claim => sourceMaps.All(sourceMap => sourceMap.AggregateClaimId != claim.Id)))
        {
            throw new InvalidOperationException($"Dream aggregate candidate '{candidate.Id:D}' cannot be applied because claim source maps are incomplete.");
        }

        var sourceItemIds = sourceMaps
            .Select(sourceMap => sourceMap.SourceItemId)
            .Where(id => id is not null)
            .Select(id => id!.Value)
            .Distinct()
            .ToArray();
        if (sourceItemIds.Length == 0)
        {
            throw new InvalidOperationException($"Dream aggregate candidate '{candidate.Id:D}' cannot be applied without source item mappings.");
        }

        var sourceItems = await dbContext.Set<CognitiveMemorySourceItemRecord>()
            .AsNoTracking()
            .Where(item => sourceItemIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        if (sourceItems.Count != sourceItemIds.Length)
        {
            throw new InvalidOperationException($"Dream aggregate candidate '{candidate.Id:D}' references missing source items.");
        }

        var nowUtc = clock.GetUtcNow();
        var stableContentHash = CognitiveMemoryHash.FromUtf8($"dream-aggregate|{candidate.PayloadHash}|{candidate.CanonicalText}").Value;
        var duplicateMemory = await dbContext.Set<CognitiveMemoryRecord>()
            .AsNoTracking()
            .Where(record =>
                record.ProjectId == candidate.ProjectId &&
                record.Origin == CognitiveMemoryRecordOrigin.MachineGenerated &&
                record.ContentHash == stableContentHash &&
                record.StabilityState != CognitiveMemoryStabilityState.Deprecated)
            .FirstOrDefaultAsync(cancellationToken);
        if (duplicateMemory is not null)
        {
            var existingClaims = await dbContext.Set<CognitiveMemoryClaimRecord>()
                .AsNoTracking()
                .Where(claim => claim.MemoryRecordId == duplicateMemory.Id)
                .Select(claim => new CognitiveMemoryClaimId(claim.Id))
                .ToArrayAsync(cancellationToken);
            candidate.Status = CognitiveMemoryDreamAggregateCandidateStatus.Applied;
            candidate.MemoryRecordId = duplicateMemory.Id;
            candidate.UpdatedAtUtc = nowUtc;
            candidate.ConcurrencyToken = Guid.NewGuid();
            await dbContext.SaveChangesAsync(cancellationToken);
            return new CognitiveMemoryAggregateMemoryApplyResult(new CognitiveMemoryRecordId(duplicateMemory.Id), existingClaims, Created: false);
        }

        var distinctSourceItemCount = sourceItemIds.Length;
        var confidenceScore = CalibrateConfidence(validation, claims.Count, distinctSourceItemCount);
        var confidenceBucket = confidenceScore >= 0.86
            ? CognitiveMemoryScoreProjectionBucket.StrongAccept
            : CognitiveMemoryScoreProjectionBucket.WeakAccept;
        var contextFrame = new CognitiveMemoryContextFrameRecord
        {
            Id = Guid.NewGuid(),
            ProjectId = candidate.ProjectId,
            FrameKind = CognitiveMemoryContextFrameKind.Composite,
            DisplayName = candidate.Title,
            ConfidenceBucket = confidenceBucket,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
            ConcurrencyToken = Guid.NewGuid()
        };
        var memory = new CognitiveMemoryRecord
        {
            Id = Guid.NewGuid(),
            ProjectId = candidate.ProjectId,
            Kind = ResolveRecordKind(candidate.Mode),
            Origin = CognitiveMemoryRecordOrigin.MachineGenerated,
            Title = candidate.Title,
            CanonicalText = candidate.CanonicalText,
            SummaryText = candidate.SummaryText,
            TopicKey = CognitiveMemoryQualityText.TrimText(CognitiveMemoryQualityText.NormalizeKey(candidate.Title), 240),
            ValidationState = CognitiveMemoryValidationState.Approved,
            StabilityState = CognitiveMemoryStabilityState.Active,
            CreatedInMode = CognitiveMemoryOperationMode.Consolidate,
            AlgorithmVersion = AlgorithmVersion,
            ContentHash = stableContentHash,
            SourceEvidenceCount = sourceItemIds.Length,
            EvidenceAnchorCount = sourceMaps.Select(sourceMap => sourceMap.EvidenceAnchorId).Where(id => id is not null).Distinct().Count(),
            GeneratedReason = CognitiveMemoryQualityText.TrimText($"Approved dream aggregate candidate {candidate.Id:D}; calibrated confidence {confidenceScore:0.###} from {distinctSourceItemCount} independent source item(s).", 500),
            PrimaryContextFrameId = contextFrame.Id,
            ConfidenceBucket = confidenceBucket,
            ActivationBucket = confidenceBucket,
            AccessLevel = candidate.AccessLevel,
            RiskLevel = candidate.RiskLevel,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
            ConcurrencyToken = Guid.NewGuid()
        };
        var validationResult = recordValidator.ValidateForPersistence(memory);
        if (validationResult.IsFailure)
        {
            throw new InvalidOperationException($"Generated aggregate memory record is invalid: {string.Join(", ", validationResult.Errors.Select(error => error.Code))}.");
        }

        dbContext.Add(contextFrame);
        dbContext.Add(memory);
        var createdClaimIds = new List<CognitiveMemoryClaimId>();
        foreach (var aggregateClaim in claims)
        {
            var claim = new CognitiveMemoryClaimRecord
            {
                Id = Guid.NewGuid(),
                ProjectId = candidate.ProjectId,
                MemoryRecordId = memory.Id,
                ClaimKind = aggregateClaim.ClaimKind,
                ClaimText = aggregateClaim.ClaimText,
                SubjectKey = aggregateClaim.SubjectKey,
                PredicateKey = aggregateClaim.PredicateKey,
                ObjectKey = aggregateClaim.ObjectKey,
                PrimaryContextFrameId = contextFrame.Id,
                CurrentBeliefState = CognitiveMemoryBeliefStateKind.Validated,
                CurrentBeliefBucket = confidenceBucket,
                DisplayBeliefScore = confidenceScore,
                ValidationState = CognitiveMemoryValidationState.Approved,
                StabilityState = CognitiveMemoryStabilityState.Active,
                AlgorithmVersion = AlgorithmVersion,
                CreatedAtUtc = nowUtc,
                UpdatedAtUtc = nowUtc,
                ConcurrencyToken = Guid.NewGuid()
            };
            dbContext.Add(claim);
            createdClaimIds.Add(new CognitiveMemoryClaimId(claim.Id));
            foreach (var sourceMap in sourceMaps.Where(sourceMap => sourceMap.AggregateClaimId == aggregateClaim.Id && sourceMap.EvidenceAnchorId is not null))
            {
                dbContext.Add(new CognitiveMemoryClaimEvidenceLinkRecord
                {
                    Id = Guid.NewGuid(),
                    ClaimId = claim.Id,
                    EvidenceAnchorId = sourceMap.EvidenceAnchorId!.Value,
                    Direction = sourceMap.Direction,
                    Explanation = sourceMap.Summary,
                    CreatedAtUtc = nowUtc
                });
            }
        }

        foreach (var sourceItemId in sourceItemIds)
        {
            var sourceItem = sourceItems[sourceItemId];
            var firstMap = sourceMaps.First(sourceMap => sourceMap.SourceItemId == sourceItemId);
            dbContext.Add(new CognitiveMemorySourceLinkRecord
            {
                Id = Guid.NewGuid(),
                MemoryRecordId = memory.Id,
                SourceManifestId = sourceItem.SourceManifestId,
                SourceItemId = sourceItem.Id,
                EvidenceRole = CognitiveMemoryEvidenceRole.SupportingSource,
                Locator = sourceItem.Locator,
                Summary = firstMap.Summary,
                CreatedAtUtc = nowUtc
            });
        }

        foreach (var sourceMap in sourceMaps.Where(sourceMap => sourceMap.EvidenceAnchorId is not null)
            .GroupBy(sourceMap => sourceMap.EvidenceAnchorId!.Value)
            .Select(group => group.First()))
        {
            dbContext.Add(new CognitiveMemoryRecordEvidenceAnchorRecord
            {
                Id = Guid.NewGuid(),
                MemoryRecordId = memory.Id,
                EvidenceAnchorId = sourceMap.EvidenceAnchorId!.Value,
                EvidenceRole = CognitiveMemoryEvidenceRole.SupportingSource,
                Summary = sourceMap.Summary,
                CreatedAtUtc = nowUtc
            });
        }

        dbContext.Add(new CognitiveMemoryMutationCommandRecord
        {
            Id = Guid.NewGuid(),
            ProjectId = candidate.ProjectId,
            CommandKind = CognitiveMemoryMutationCommandKind.ValidateClaim,
            Status = CognitiveMemoryMutationCommandStatus.Accepted,
            ActorKind = CognitiveMemoryActorKind.System,
            ActorId = request.ActorId.Trim(),
            IdempotencyKey = $"dream-aggregate-apply:{candidate.Id:D}",
            AffectedMemoryRecordIdsJson = JsonSerializer.Serialize(new[] { memory.Id }, CognitiveMemoryJsonSerializerContext.Default.GuidArray),
            AffectedClaimIdsJson = JsonSerializer.Serialize(createdClaimIds.Select(id => id.Value).ToArray(), CognitiveMemoryJsonSerializerContext.Default.GuidArray),
            EvidenceAnchorIdsJson = JsonSerializer.Serialize(sourceMaps.Select(sourceMap => sourceMap.EvidenceAnchorId).Where(id => id is not null).Select(id => id!.Value).Distinct().ToArray(), CognitiveMemoryJsonSerializerContext.Default.GuidArray),
            PayloadJson = "{}",
            ExpectedVersionToken = string.Empty,
            RequiresHumanReview = false,
            ReviewReason = string.Empty,
            ResultVersionToken = memory.ConcurrencyToken.ToString("D"),
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
            ConcurrencyToken = Guid.NewGuid()
        });

        candidate.Status = CognitiveMemoryDreamAggregateCandidateStatus.Applied;
        candidate.MemoryRecordId = memory.Id;
        candidate.UpdatedAtUtc = nowUtc;
        candidate.ConcurrencyToken = Guid.NewGuid();
        await dbContext.SaveChangesAsync(cancellationToken);
        return new CognitiveMemoryAggregateMemoryApplyResult(new CognitiveMemoryRecordId(memory.Id), createdClaimIds, Created: true);
    }

    private static double CalibrateConfidence(
        CognitiveMemoryDreamValidationRecord validation,
        int claimCount,
        int distinctSourceItemCount)
    {
        var evidenceScore = Math.Clamp(distinctSourceItemCount / 4d, 0, 0.12);
        var claimPenalty = Math.Clamp((claimCount - 1) * 0.015, 0, 0.06);
        var issuePenalty = Math.Clamp(validation.IssueCount * 0.05, 0, 0.2);
        return Math.Round(Math.Clamp(0.78 + evidenceScore - claimPenalty - issuePenalty, 0.55, 0.92), 3, MidpointRounding.AwayFromZero);
    }

    private static CognitiveMemoryRecordKind ResolveRecordKind(CognitiveMemoryConsolidationMode mode)
        => mode switch
        {
            CognitiveMemoryConsolidationMode.ProcedureMining => CognitiveMemoryRecordKind.Procedural,
            CognitiveMemoryConsolidationMode.FailureLearning => CognitiveMemoryRecordKind.Reflection,
            _ => CognitiveMemoryRecordKind.Semantic
        };
}
