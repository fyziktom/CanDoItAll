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
    private async Task<List<EvaluatedRecallCandidate>> EvaluateCandidatesAsync(
        AppDbContext dbContext,
        Guid traceId,
        CognitiveMemoryRecallRequest request,
        CognitiveMemoryWorkspaceFrameId? workspaceFrameId,
        IReadOnlyList<string> queryTerms,
        IReadOnlyList<RecallCandidateAccumulator> candidates,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        var candidateIds = candidates.Select(candidate => candidate.Record.Id).Distinct().ToArray();
        var claimsByRecordId = await LoadClaimsAsync(dbContext, candidateIds, cancellationToken);
        var evidenceByRecordId = await LoadEvidenceAnchorIdsAsync(dbContext, candidateIds, claimsByRecordId, cancellationToken);
        var sourceScopeKeysByRecordId = await LoadSourceScopeKeysAsync(dbContext, candidateIds, cancellationToken);
        var preferredScopeKey = ResolvePreferredSourceScopeKey(request);
        var evaluated = new List<EvaluatedRecallCandidate>(candidates.Count);

        foreach (var candidate in candidates)
        {
            var candidateId = CognitiveMemoryRecallCandidateId.New();
            var claims = claimsByRecordId.GetValueOrDefault(candidate.Record.Id) ?? [];
            var evidenceAnchorIds = evidenceByRecordId.GetValueOrDefault(candidate.Record.Id) ?? [];
            var sourceScopeKeys = ResolveSourceScopeKeys(candidate.Record, sourceScopeKeysByRecordId);
            var vector = BuildCandidateVector(candidateId, traceId, request, candidate, claims, evidenceAnchorIds, queryTerms, nowUtc);
            var trace = await scoreGeometryDriver.EvaluateAsync(
                new CognitiveMemoryScoreEvaluationRequest(
                    request.ProjectId,
                    CognitiveMemoryScoreOwnerKind.RecallCandidate,
                    candidateId.Value,
                    CognitiveMemoryScoreSpaceKind.RecallCandidate,
                    CognitiveMemoryScoreSpaceRegistry.CurrentSchemaVersion,
                    [vector],
                    BuildRecallCandidateShapes()),
                cancellationToken);
            await CognitiveMemoryScoreTracePersistence.AddIfMissingAsync(dbContext, trace, nowUtc, cancellationToken);

            var decision = DecideCandidate(candidate, trace, request);
            evaluated.Add(new EvaluatedRecallCandidate(
                candidateId,
                candidate.Record,
                workspaceFrameId,
                candidate.PrimaryChannelKind,
                decision.DecisionKind,
                decision.ExclusionReasonKind,
                trace,
                trace.ScalarProjection,
                claims.Select(claim => new CognitiveMemoryClaimId(claim.Id)).ToArray(),
                evidenceAnchorIds.Select(id => new CognitiveMemoryEvidenceAnchorId(id)).ToArray(),
                decision.Reason,
                candidate.Channels.ToArray(),
                candidate.ContextBoundaryReason,
                sourceScopeKeys));
        }

        return evaluated
            .OrderByDescending(candidate => ResolveFocusOrderingPriority(candidate, preferredScopeKey))
            .ThenByDescending(candidate => candidate.ScoreTrace.ScalarProjection?.DisplayScore ?? 0)
            .ThenBy(candidate => candidate.Record.Title, StringComparer.Ordinal)
            .ToList();
    }

    private static IReadOnlyList<EvaluatedRecallCandidate> SelectFocus(
        IReadOnlyList<EvaluatedRecallCandidate> evaluatedCandidates,
        CognitiveMemoryRecallBudget budget,
        List<string> warnings)
    {
        var selectedCount = 0;
        var selectedFocusKeys = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<EvaluatedRecallCandidate>(evaluatedCandidates.Count);
        foreach (var candidate in evaluatedCandidates)
        {
            if (candidate.DecisionKind == CognitiveMemoryRecallCandidateDecisionKind.Inhibited)
            {
                result.Add(candidate);
                continue;
            }

            var focusKey = CreateFocusDedupeKey(candidate);
            if (!selectedFocusKeys.Add(focusKey))
            {
                warnings.Add($"Recall focus skipped duplicate '{candidate.Record.Title}'.");
                result.Add(candidate with
                {
                    DecisionKind = CognitiveMemoryRecallCandidateDecisionKind.Excluded,
                    ExclusionReasonKind = CognitiveMemoryRecallExclusionReasonKind.NotInFocus,
                    Reason = "Candidate excluded because an equivalent memory record was already selected."
                });
                continue;
            }

            if (selectedCount >= budget.FocusLimit)
            {
                warnings.Add($"Recall focus budget excluded '{candidate.Record.Title}'.");
                result.Add(candidate with
                {
                    DecisionKind = CognitiveMemoryRecallCandidateDecisionKind.Excluded,
                    ExclusionReasonKind = CognitiveMemoryRecallExclusionReasonKind.BudgetLimit,
                    Reason = "Candidate excluded by recall focus item budget."
                });
                continue;
            }

            selectedCount++;
            result.Add(candidate with
            {
                DecisionKind = CognitiveMemoryRecallCandidateDecisionKind.Selected
            });
        }

        return result;
    }

    private static string CreateFocusDedupeKey(EvaluatedRecallCandidate candidate)
    {
        var record = candidate.Record;
        var durableText = FirstNonEmpty(record.CanonicalText, record.SummaryText, record.TopicKey);
        return $"{NormalizeContextBlock(record.Title).ToLowerInvariant()}|{NormalizeContextBlock(durableText).ToLowerInvariant()}";
    }

    private static async Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> LoadSourceScopeKeysAsync(
        AppDbContext dbContext,
        IReadOnlyList<Guid> recordIds,
        CancellationToken cancellationToken)
    {
        if (recordIds.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<string>>(0);
        }

        var rows = await dbContext.Set<CognitiveMemorySourceLinkRecord>()
            .AsNoTracking()
            .Where(link => recordIds.Contains(link.MemoryRecordId))
            .Join(
                dbContext.Set<CognitiveMemorySourceItemRecord>().AsNoTracking(),
                link => link.SourceItemId,
                item => item.Id,
                (link, item) => new
                {
                    link.MemoryRecordId,
                    item.Title,
                    item.Locator,
                    item.ProvenanceJson
                })
            .ToListAsync(cancellationToken);
        var scopeKeysByRecordId = new Dictionary<Guid, HashSet<string>>();
        foreach (var row in rows)
        {
            var scopeKeys = ExtractSourceScopeKeys(row.Title, row.Locator, row.ProvenanceJson);
            if (scopeKeys.Count == 0)
            {
                continue;
            }

            if (!scopeKeysByRecordId.TryGetValue(row.MemoryRecordId, out var existingScopeKeys))
            {
                existingScopeKeys = new HashSet<string>(StringComparer.Ordinal);
                scopeKeysByRecordId[row.MemoryRecordId] = existingScopeKeys;
            }

            foreach (var scopeKey in scopeKeys)
            {
                existingScopeKeys.Add(scopeKey);
            }
        }

        return scopeKeysByRecordId.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<string>)pair.Value.ToArray());
    }

    private static IReadOnlyList<string> ResolveSourceScopeKeys(
        MemoryRecordSnapshot record,
        IReadOnlyDictionary<Guid, IReadOnlyList<string>> sourceScopeKeysByRecordId)
    {
        var scopeKeys = new HashSet<string>(
            ExtractSourceScopeKeys(record.Title, record.SummaryText, record.CanonicalText, record.TopicKey),
            StringComparer.Ordinal);
        if (sourceScopeKeysByRecordId.TryGetValue(record.Id, out var sourceScopeKeys))
        {
            foreach (var sourceScopeKey in sourceScopeKeys)
            {
                scopeKeys.Add(sourceScopeKey);
            }
        }

        return scopeKeys.ToArray();
    }
}