using System.Text;
using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.CognitiveMemory;

public sealed record CognitiveMemoryConsolidationCandidateApplyResult(
    Guid MemoryRecordId,
    Guid? ClaimId,
    bool Created);

public interface ICognitiveMemoryConsolidationCandidateApplicator
{
    ValueTask<CognitiveMemoryConsolidationCandidateApplyResult> ApplyAsync(
        AppDbContext dbContext,
        CognitiveMemoryConsolidationCandidateRecord candidate,
        CognitiveMemoryConsolidationCandidatePayload payload,
        CognitiveMemoryValidationState validationState,
        CognitiveMemoryStabilityState stabilityState,
        string actorId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);
}

public sealed class CognitiveMemoryConsolidationCandidateApplicator(
    ICognitiveMemoryRecordValidator recordValidator) : ICognitiveMemoryConsolidationCandidateApplicator
{
    private const string AppliedAuditMessagePrefix = "Consolidation candidate materialized canonical memory record";
    private const string EvidencePredicateKey = "is-grounded-by";
    private const int MaximumTitleLength = 300;
    private const int MaximumTopicKeyLength = 240;
    private const int MaximumPredicateKeyLength = 160;
    private const int MaximumGeneratedReasonLength = 500;
    private const int MaximumSummaryLength = 1200;

    public async ValueTask<CognitiveMemoryConsolidationCandidateApplyResult> ApplyAsync(
        AppDbContext dbContext,
        CognitiveMemoryConsolidationCandidateRecord candidate,
        CognitiveMemoryConsolidationCandidatePayload payload,
        CognitiveMemoryValidationState validationState,
        CognitiveMemoryStabilityState stabilityState,
        string actorId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);

        if (candidate.MemoryRecordId is { } existingMemoryRecordId)
        {
            return new CognitiveMemoryConsolidationCandidateApplyResult(existingMemoryRecordId, null, Created: false);
        }

        if (candidate.SourceItemId is not { } sourceItemId)
        {
            throw new InvalidOperationException($"Consolidation candidate '{candidate.Id:D}' cannot be applied without a source item.");
        }

        var sourceItem = await dbContext.Set<CognitiveMemorySourceItemRecord>()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == sourceItemId, cancellationToken)
            ?? throw new InvalidOperationException($"Consolidation candidate source item '{sourceItemId:D}' was not found.");
        var evidenceAnchors = await LoadEvidenceAnchorsAsync(dbContext, sourceItemId, candidate.EvidenceAnchorId, cancellationToken);
        if (evidenceAnchors.Count == 0)
        {
            throw new InvalidOperationException($"Consolidation candidate '{candidate.Id:D}' cannot be applied without evidence anchors.");
        }

        var memoryRecord = CreateMemoryRecord(candidate, payload, sourceItem, evidenceAnchors.Count, validationState, stabilityState, nowUtc);
        var claim = CreateClaim(memoryRecord, candidate, payload, validationState, stabilityState, nowUtc);
        var sourceLink = CreateSourceLink(memoryRecord, sourceItem, evidenceAnchors[0], nowUtc);
        var recordEvidenceLinks = evidenceAnchors
            .Select(anchor => CreateRecordEvidenceLink(memoryRecord, anchor, payload, nowUtc))
            .ToArray();
        var claimEvidenceLinks = evidenceAnchors
            .Select(anchor => CreateClaimEvidenceLink(claim, anchor, payload, nowUtc))
            .ToArray();

        var validation = recordValidator.ValidateForPersistence(memoryRecord);
        if (validation.IsFailure)
        {
            throw new InvalidOperationException($"Generated cognitive memory record is invalid: {string.Join(", ", validation.Errors.Select(error => error.Code))}.");
        }

        dbContext.Add(memoryRecord);
        dbContext.Add(claim);
        dbContext.Add(sourceLink);
        dbContext.AddRange(recordEvidenceLinks);
        dbContext.AddRange(claimEvidenceLinks);

        candidate.MemoryRecordId = memoryRecord.Id;
        candidate.Status = CognitiveMemoryConsolidationCandidateStatus.MutationSubmitted;
        candidate.ConcurrencyToken = Guid.NewGuid();

        await UpdateMutationCommandAsync(dbContext, candidate, memoryRecord.Id, claim.Id, actorId, nowUtc, cancellationToken);

        return new CognitiveMemoryConsolidationCandidateApplyResult(memoryRecord.Id, claim.Id, Created: true);
    }

    private static async Task<IReadOnlyList<CognitiveMemoryEvidenceAnchorRecord>> LoadEvidenceAnchorsAsync(
        AppDbContext dbContext,
        Guid sourceItemId,
        Guid? primaryEvidenceAnchorId,
        CancellationToken cancellationToken)
    {
        var anchors = await dbContext.Set<CognitiveMemoryEvidenceAnchorRecord>()
            .AsNoTracking()
            .Where(anchor => anchor.SourceItemId == sourceItemId)
            .OrderBy(anchor => anchor.Id)
            .ToListAsync(cancellationToken);
        if (anchors.Count > 0 || primaryEvidenceAnchorId is not { } anchorId)
        {
            return anchors;
        }

        var primaryAnchor = await dbContext.Set<CognitiveMemoryEvidenceAnchorRecord>()
            .AsNoTracking()
            .SingleOrDefaultAsync(anchor => anchor.Id == anchorId, cancellationToken)
            ?? throw new InvalidOperationException($"Consolidation candidate evidence anchor '{anchorId:D}' was not found.");
        return [primaryAnchor];
    }

    private static CognitiveMemoryRecord CreateMemoryRecord(
        CognitiveMemoryConsolidationCandidateRecord candidate,
        CognitiveMemoryConsolidationCandidatePayload payload,
        CognitiveMemorySourceItemRecord sourceItem,
        int evidenceAnchorCount,
        CognitiveMemoryValidationState validationState,
        CognitiveMemoryStabilityState stabilityState,
        DateTimeOffset nowUtc)
    {
        var canonicalText = string.IsNullOrWhiteSpace(payload.Summary)
            ? sourceItem.ContentText.Trim()
            : payload.Summary.Trim();
        var title = TrimText(string.IsNullOrWhiteSpace(payload.Title) ? sourceItem.Title : payload.Title, MaximumTitleLength);
        var topicKey = CreateTopicKey(sourceItem, payload.CandidateKind);
        return new CognitiveMemoryRecord
        {
            Id = Guid.NewGuid(),
            ProjectId = candidate.ProjectId ?? sourceItem.ProjectId,
            Kind = ToRecordKind(payload.CandidateKind),
            Origin = CognitiveMemoryRecordOrigin.MachineGenerated,
            Title = title,
            CanonicalText = canonicalText,
            SummaryText = TrimText(canonicalText, MaximumSummaryLength),
            TopicKey = topicKey,
            ValidationState = validationState,
            StabilityState = stabilityState,
            CreatedInMode = CognitiveMemoryOperationMode.Consolidate,
            AlgorithmVersion = candidate.AlgorithmVersion,
            ContentHash = CognitiveMemoryHash.FromUtf8($"{payload.CandidateKind}|{sourceItem.ContentHash}|{canonicalText}").Value,
            SourceEvidenceCount = 1,
            EvidenceAnchorCount = evidenceAnchorCount,
            GeneratedReason = TrimText(payload.Reason, MaximumGeneratedReasonLength),
            ConfidenceBucket = candidate.ScoreBucket,
            ActivationBucket = candidate.ScoreBucket,
            AccessLevel = sourceItem.AccessLevel,
            RiskLevel = ResolveRiskLevel(sourceItem, payload.CandidateKind),
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
            ConcurrencyToken = Guid.NewGuid()
        };
    }

    private static CognitiveMemoryClaimRecord CreateClaim(
        CognitiveMemoryRecord memoryRecord,
        CognitiveMemoryConsolidationCandidateRecord candidate,
        CognitiveMemoryConsolidationCandidatePayload payload,
        CognitiveMemoryValidationState validationState,
        CognitiveMemoryStabilityState stabilityState,
        DateTimeOffset nowUtc)
        => new()
        {
            Id = Guid.NewGuid(),
            ProjectId = memoryRecord.ProjectId,
            MemoryRecordId = memoryRecord.Id,
            ClaimKind = ToClaimKind(payload.CandidateKind),
            ClaimText = memoryRecord.SummaryText,
            SubjectKey = TrimText(memoryRecord.TopicKey, MaximumTopicKeyLength),
            PredicateKey = TrimText(EvidencePredicateKey, MaximumPredicateKeyLength),
            ObjectKey = TrimText(candidate.SourceContentHash, MaximumTopicKeyLength),
            CurrentBeliefState = validationState == CognitiveMemoryValidationState.Approved
                ? CognitiveMemoryBeliefStateKind.Supported
                : CognitiveMemoryBeliefStateKind.Unexamined,
            CurrentBeliefBucket = candidate.ScoreBucket,
            DisplayBeliefScore = candidate.DisplayPriorityProjection,
            ValidationState = validationState,
            StabilityState = stabilityState,
            AlgorithmVersion = candidate.AlgorithmVersion,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
            ConcurrencyToken = Guid.NewGuid()
        };

    private static CognitiveMemorySourceLinkRecord CreateSourceLink(
        CognitiveMemoryRecord memoryRecord,
        CognitiveMemorySourceItemRecord sourceItem,
        CognitiveMemoryEvidenceAnchorRecord primaryAnchor,
        DateTimeOffset nowUtc)
        => new()
        {
            Id = Guid.NewGuid(),
            MemoryRecordId = memoryRecord.Id,
            SourceManifestId = sourceItem.SourceManifestId,
            SourceItemId = sourceItem.Id,
            EvidenceRole = CognitiveMemoryEvidenceRole.PrimarySource,
            Locator = string.IsNullOrWhiteSpace(primaryAnchor.Locator) ? sourceItem.Locator : primaryAnchor.Locator,
            QuoteHash = string.IsNullOrWhiteSpace(primaryAnchor.QuoteHash) ? null : primaryAnchor.QuoteHash,
            Summary = memoryRecord.SummaryText,
            CreatedAtUtc = nowUtc
        };

    private static CognitiveMemoryRecordEvidenceAnchorRecord CreateRecordEvidenceLink(
        CognitiveMemoryRecord memoryRecord,
        CognitiveMemoryEvidenceAnchorRecord evidenceAnchor,
        CognitiveMemoryConsolidationCandidatePayload payload,
        DateTimeOffset nowUtc)
        => new()
        {
            Id = Guid.NewGuid(),
            MemoryRecordId = memoryRecord.Id,
            EvidenceAnchorId = evidenceAnchor.Id,
            EvidenceRole = payload.CandidateKind == CognitiveMemoryConsolidationCandidateKind.Contradiction
                ? CognitiveMemoryEvidenceRole.ContradictingSource
                : CognitiveMemoryEvidenceRole.PrimarySource,
            Summary = memoryRecord.SummaryText,
            CreatedAtUtc = nowUtc
        };

    private static CognitiveMemoryClaimEvidenceLinkRecord CreateClaimEvidenceLink(
        CognitiveMemoryClaimRecord claim,
        CognitiveMemoryEvidenceAnchorRecord evidenceAnchor,
        CognitiveMemoryConsolidationCandidatePayload payload,
        DateTimeOffset nowUtc)
        => new()
        {
            Id = Guid.NewGuid(),
            ClaimId = claim.Id,
            EvidenceAnchorId = evidenceAnchor.Id,
            Direction = payload.CandidateKind == CognitiveMemoryConsolidationCandidateKind.Contradiction
                ? CognitiveMemoryEvidenceDirection.Attacks
                : CognitiveMemoryEvidenceDirection.Supports,
            Explanation = payload.Reason,
            CreatedAtUtc = nowUtc
        };

    private static async Task UpdateMutationCommandAsync(
        AppDbContext dbContext,
        CognitiveMemoryConsolidationCandidateRecord candidate,
        Guid memoryRecordId,
        Guid claimId,
        string actorId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        if (candidate.MutationCommandId is not { } mutationCommandId)
        {
            return;
        }

        var mutationCommand = await dbContext.Set<CognitiveMemoryMutationCommandRecord>()
            .SingleOrDefaultAsync(command => command.Id == mutationCommandId, cancellationToken)
            ?? throw new InvalidOperationException($"Consolidation candidate mutation command '{mutationCommandId:D}' was not found.");
        mutationCommand.Status = CognitiveMemoryMutationCommandStatus.Accepted;
        mutationCommand.RequiresHumanReview = false;
        mutationCommand.ReviewReason = string.Empty;
        mutationCommand.AffectedMemoryRecordIdsJson = SerializeGuidList([memoryRecordId]);
        mutationCommand.AffectedClaimIdsJson = SerializeGuidList([claimId]);
        mutationCommand.UpdatedAtUtc = nowUtc;
        mutationCommand.ConcurrencyToken = Guid.NewGuid();

        var existingSequences = await dbContext.Set<CognitiveMemoryMutationAuditEventRecord>()
            .AsNoTracking()
            .Where(auditEvent => auditEvent.MutationCommandId == mutationCommandId)
            .Select(auditEvent => auditEvent.Sequence)
            .ToListAsync(cancellationToken);
        dbContext.Add(new CognitiveMemoryMutationAuditEventRecord
        {
            Id = Guid.NewGuid(),
            MutationCommandId = mutationCommandId,
            ProjectId = candidate.ProjectId,
            Sequence = existingSequences.Count == 0 ? 1 : existingSequences.Max() + 1,
            EventKind = CognitiveMemoryMutationAuditEventKind.AcceptedForHandler,
            Message = $"{AppliedAuditMessagePrefix} '{memoryRecordId:D}' by '{actorId.Trim()}'.",
            CreatedAtUtc = nowUtc
        });
    }

    private static CognitiveMemoryRecordKind ToRecordKind(CognitiveMemoryConsolidationCandidateKind candidateKind)
        => candidateKind switch
        {
            CognitiveMemoryConsolidationCandidateKind.Episode => CognitiveMemoryRecordKind.Episodic,
            CognitiveMemoryConsolidationCandidateKind.Procedure => CognitiveMemoryRecordKind.Procedural,
            CognitiveMemoryConsolidationCandidateKind.Decision => CognitiveMemoryRecordKind.Decision,
            CognitiveMemoryConsolidationCandidateKind.Reflection => CognitiveMemoryRecordKind.Reflection,
            CognitiveMemoryConsolidationCandidateKind.Contradiction => CognitiveMemoryRecordKind.Reflection,
            CognitiveMemoryConsolidationCandidateKind.ProjectionInvalidation => CognitiveMemoryRecordKind.Metacognitive,
            CognitiveMemoryConsolidationCandidateKind.ReviewRequired => CognitiveMemoryRecordKind.Reflection,
            _ => CognitiveMemoryRecordKind.Semantic
        };

    private static CognitiveMemoryClaimKind ToClaimKind(CognitiveMemoryConsolidationCandidateKind candidateKind)
        => candidateKind switch
        {
            CognitiveMemoryConsolidationCandidateKind.Procedure => CognitiveMemoryClaimKind.ProcedureConstraint,
            CognitiveMemoryConsolidationCandidateKind.Decision => CognitiveMemoryClaimKind.Decision,
            CognitiveMemoryConsolidationCandidateKind.Contradiction => CognitiveMemoryClaimKind.Observation,
            CognitiveMemoryConsolidationCandidateKind.Episode => CognitiveMemoryClaimKind.Observation,
            _ => CognitiveMemoryClaimKind.Fact
        };

    private static CognitiveMemoryRiskLevel ResolveRiskLevel(
        CognitiveMemorySourceItemRecord sourceItem,
        CognitiveMemoryConsolidationCandidateKind candidateKind)
    {
        if (sourceItem.AccessLevel == CognitiveMemoryAccessLevel.Restricted ||
            sourceItem.RedactionState is CognitiveMemoryRedactionState.Redacted or CognitiveMemoryRedactionState.Restricted)
        {
            return CognitiveMemoryRiskLevel.High;
        }

        return candidateKind == CognitiveMemoryConsolidationCandidateKind.Contradiction
            ? CognitiveMemoryRiskLevel.Medium
            : CognitiveMemoryRiskLevel.Low;
    }

    private static string CreateTopicKey(
        CognitiveMemorySourceItemRecord sourceItem,
        CognitiveMemoryConsolidationCandidateKind candidateKind)
    {
        var raw = $"{sourceItem.SourceSystem}.{sourceItem.SourceItemType}.{candidateKind}.{sourceItem.Title}";
        var builder = new StringBuilder(raw.Length);
        foreach (var character in raw.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                continue;
            }

            if (builder.Length > 0 && builder[^1] != '.')
            {
                builder.Append('.');
            }
        }

        var value = builder.ToString().Trim('.');
        return TrimText(string.IsNullOrWhiteSpace(value) ? "cognitive-memory.consolidated" : value, MaximumTopicKeyLength);
    }

    private static string TrimText(string? value, int maxLength)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        return normalized.Length <= maxLength
            ? normalized
            : normalized[..maxLength];
    }

    private static string SerializeGuidList(IReadOnlyList<Guid> values)
        => JsonSerializer.Serialize(
            values.ToArray(),
            CognitiveMemoryJsonSerializerContext.Default.GuidArray);
}
