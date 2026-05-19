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
    private static CognitiveMemoryScoreVectorSnapshot BuildCandidateVector(
        CognitiveMemoryRecallCandidateId candidateId,
        Guid traceId,
        CognitiveMemoryRecallRequest request,
        RecallCandidateAccumulator candidate,
        IReadOnlyList<ClaimSnapshot> claims,
        IReadOnlyList<Guid> evidenceAnchorIds,
        IReadOnlyList<string> queryTerms,
        DateTimeOffset nowUtc)
    {
        var record = candidate.Record;
        var evidenceRefs = BuildScoreEvidenceRefs(candidateId, traceId, candidate, evidenceAnchorIds, nowUtc);
        var components = new List<CognitiveMemoryScoreComponent>
        {
            Component(CognitiveMemoryScoreDimensionKind.SemanticSimilarity, candidate.SemanticSimilarity ?? candidate.LexicalMatch ?? 0.35, candidate.SemanticSimilarity is null ? 0.35 : 1, evidenceRefs),
            Component(CognitiveMemoryScoreDimensionKind.ContextFit, ResolveContextFit(candidate), 1, evidenceRefs),
            Component(CognitiveMemoryScoreDimensionKind.SourceSufficiency, ResolveSourceSufficiency(record, evidenceAnchorIds), 1, evidenceRefs)
        };

        AddOptional(components, CognitiveMemoryScoreDimensionKind.LexicalMatch, candidate.LexicalMatch, 1, evidenceRefs);
        AddOptional(components, CognitiveMemoryScoreDimensionKind.GraphProximity, candidate.GraphProximity, 1, evidenceRefs);
        AddOptional(components, CognitiveMemoryScoreDimensionKind.WorkspaceFocusFit, candidate.WorkspaceFocusFit, 1, evidenceRefs);
        AddOptional(components, CognitiveMemoryScoreDimensionKind.MemoryActivation, candidate.MemoryActivation, 1, evidenceRefs);
        AddOptional(components, CognitiveMemoryScoreDimensionKind.ContextSeparation, candidate.ContextSeparation, 1, evidenceRefs);
        AddOptional(components, CognitiveMemoryScoreDimensionKind.ContradictionPressure, candidate.ContradictionPressure ?? ResolveContradictionPressure(claims), 1, evidenceRefs);
        AddOptional(components, CognitiveMemoryScoreDimensionKind.StalenessPressure, ResolveStalenessPressure(record), 1, evidenceRefs);
        AddOptional(components, CognitiveMemoryScoreDimensionKind.AccessPolicyRisk, PolicyCanRead(record.AccessLevel, request.PolicyContext) ? 0 : 1, 1, evidenceRefs);
        AddOptional(components, CognitiveMemoryScoreDimensionKind.RedactionPressure, ResolveRedactionPressure(record, request.PolicyContext), 1, evidenceRefs);
        AddOptional(components, CognitiveMemoryScoreDimensionKind.MetadataFit, ResolveMetadataFit(record, request), 1, evidenceRefs);
        AddOptional(components, CognitiveMemoryScoreDimensionKind.TemporalRecency, ResolveTemporalRecency(record, nowUtc), 0.5, evidenceRefs);
        AddOptional(components, CognitiveMemoryScoreDimensionKind.EvidenceSupport, ResolveEvidenceSupport(claims, record), 1, evidenceRefs);
        AddOptional(components, CognitiveMemoryScoreDimensionKind.HumanValidation, ResolveHumanValidation(record, claims), 1, evidenceRefs);

        return new CognitiveMemoryScoreVectorSnapshot(
            CognitiveMemoryScoreSpaceKind.RecallCandidate,
            CognitiveMemoryScoreSpaceRegistry.CurrentSchemaVersion,
            CognitiveMemoryScoreSpaceRegistry.CurrentNormalizationProfile,
            components,
            CognitiveMemoryScoreSpaceRegistry.CurrentAlgorithmVersion,
            nowUtc,
            CognitiveMemoryHash.FromUtf8($"{candidateId}:{record.Id:D}:{request.Mode}:{request.Intent}:{string.Join('|', queryTerms)}"));
    }

    private static IReadOnlyList<CognitiveMemoryScoreEvidenceRef> BuildScoreEvidenceRefs(
        CognitiveMemoryRecallCandidateId candidateId,
        Guid traceId,
        RecallCandidateAccumulator candidate,
        IReadOnlyList<Guid> evidenceAnchorIds,
        DateTimeOffset nowUtc)
    {
        var refs = new List<CognitiveMemoryScoreEvidenceRef>
        {
            new(CognitiveMemoryScoreEvidenceKind.RecallTrace, traceId, 1, nowUtc),
            new(CognitiveMemoryScoreEvidenceKind.MemoryItem, candidate.Record.Id, 1, nowUtc)
        };
        refs.AddRange(evidenceAnchorIds.Select(id => new CognitiveMemoryScoreEvidenceRef(
            CognitiveMemoryScoreEvidenceKind.EvidenceAnchor,
            id,
            1,
            nowUtc)));
        refs.AddRange(candidate.SignalIds.Select(id => new CognitiveMemoryScoreEvidenceRef(
            CognitiveMemoryScoreEvidenceKind.CognitiveSignal,
            id,
            1,
            nowUtc)));
        refs.Add(new CognitiveMemoryScoreEvidenceRef(
            CognitiveMemoryScoreEvidenceKind.RecallTrace,
            candidateId.Value,
            1,
            nowUtc));
        return refs;
    }

    private static IReadOnlyList<CognitiveMemoryScoreShapeSnapshot> BuildRecallCandidateShapes()
    {
        var schema = CognitiveMemoryScoreSpaceRegistry.CurrentSchemaVersion;
        var algorithm = CognitiveMemoryScoreSpaceRegistry.CurrentAlgorithmVersion;
        return
        [
            Shape(CognitiveMemoryScoreProjectionBucket.Inhibit, "Recall candidate is inhibited because semantic similarity conflicts with context separation.",
            [
                Higher(CognitiveMemoryScoreDimensionKind.SemanticSimilarity, 0.7),
                Higher(CognitiveMemoryScoreDimensionKind.ContextSeparation, 0.75)
            ]),
            Shape(CognitiveMemoryScoreProjectionBucket.Inhibit, "Recall candidate is inhibited because policy or redaction pressure is too high.",
            [
                Higher(CognitiveMemoryScoreDimensionKind.AccessPolicyRisk, 0.75)
            ]),
            Shape(CognitiveMemoryScoreProjectionBucket.NeedsReview, "Recall candidate has weak source sufficiency and should not be treated as authoritative.",
            [
                Lower(CognitiveMemoryScoreDimensionKind.SourceSufficiency, 0.35)
            ]),
            Shape(CognitiveMemoryScoreProjectionBucket.StrongAccept, "Recall candidate has source-backed context fit.",
            [
                Higher(CognitiveMemoryScoreDimensionKind.ContextFit, 0.65),
                Higher(CognitiveMemoryScoreDimensionKind.SourceSufficiency, 0.55)
            ]),
            Shape(CognitiveMemoryScoreProjectionBucket.WeakAccept, "Recall candidate is usable as side context with enough source support.",
            [
                Higher(CognitiveMemoryScoreDimensionKind.ContextFit, 0.45),
                Higher(CognitiveMemoryScoreDimensionKind.SourceSufficiency, 0.35)
            ])
        ];

        CognitiveMemoryScoreShapeSnapshot Shape(
            CognitiveMemoryScoreProjectionBucket bucket,
            string explanation,
            IReadOnlyList<CognitiveMemoryScoreShapeComponent> components)
            => new(
                CognitiveMemoryScoreShapeKind.ThresholdEnvelope,
                CognitiveMemoryScoreSpaceKind.RecallCandidate,
                schema,
                components,
                radius: null,
                bucket,
                explanation,
                [],
                algorithm);
    }

    private static CognitiveMemoryScoreShapeComponent Higher(
        CognitiveMemoryScoreDimensionKind dimensionKind,
        double lowerBound)
        => new(dimensionKind, center: lowerBound, lowerBound, upperBound: null, weight: 1);

    private static CognitiveMemoryScoreShapeComponent Lower(
        CognitiveMemoryScoreDimensionKind dimensionKind,
        double upperBound)
        => new(dimensionKind, center: upperBound, lowerBound: null, upperBound, weight: 1);

    private static CognitiveMemoryScoreComponent Component(
        CognitiveMemoryScoreDimensionKind dimensionKind,
        double value,
        double confidence,
        IReadOnlyList<CognitiveMemoryScoreEvidenceRef> evidenceRefs)
        => new(dimensionKind, Math.Clamp(value, 0, 1), Math.Clamp(confidence, 0, 1), evidenceRefs);

    private static void AddOptional(
        List<CognitiveMemoryScoreComponent> components,
        CognitiveMemoryScoreDimensionKind dimensionKind,
        double? value,
        double confidence,
        IReadOnlyList<CognitiveMemoryScoreEvidenceRef> evidenceRefs)
    {
        if (value is null)
        {
            return;
        }

        if (components.Any(component => component.DimensionKind == dimensionKind))
        {
            return;
        }

        components.Add(Component(dimensionKind, value.Value, confidence, evidenceRefs));
    }

    private static CandidateDecision DecideCandidate(
        RecallCandidateAccumulator candidate,
        CognitiveMemoryScoreEvaluationTrace trace,
        CognitiveMemoryRecallRequest request)
    {
        if (!PolicyCanRead(candidate.Record.AccessLevel, request.PolicyContext))
        {
            return new CandidateDecision(
                CognitiveMemoryRecallCandidateDecisionKind.Inhibited,
                CognitiveMemoryRecallExclusionReasonKind.AccessPolicy,
                "Candidate inhibited by recall access policy.");
        }

        if (trace.MissingRequiredDimensions.Count > 0)
        {
            return new CandidateDecision(
                CognitiveMemoryRecallCandidateDecisionKind.Excluded,
                CognitiveMemoryRecallExclusionReasonKind.ScoreGeometryRejected,
                $"Candidate excluded because score geometry is missing required dimensions: {string.Join(", ", trace.MissingRequiredDimensions.Select(dimension => dimension.DimensionKind))}.");
        }

        if (candidate.ContextSeparation is >= 0.75 && request.Mode != CognitiveMemoryRecallMode.CrossProjectAnalogy)
        {
            return new CandidateDecision(
                CognitiveMemoryRecallCandidateDecisionKind.Inhibited,
                CognitiveMemoryRecallExclusionReasonKind.ContextBoundary,
                string.IsNullOrWhiteSpace(candidate.ContextBoundaryReason)
                    ? "Candidate is related but context separated from the active recall goal."
                    : candidate.ContextBoundaryReason);
        }

        if (trace.ScalarProjection?.Bucket is CognitiveMemoryScoreProjectionBucket.Inhibit or CognitiveMemoryScoreProjectionBucket.Reject or CognitiveMemoryScoreProjectionBucket.Abstain)
        {
            return new CandidateDecision(
                CognitiveMemoryRecallCandidateDecisionKind.Inhibited,
                CognitiveMemoryRecallExclusionReasonKind.ScoreGeometryRejected,
                trace.DecisionExplanation);
        }

        if (trace.ScalarProjection?.Bucket == CognitiveMemoryScoreProjectionBucket.NeedsReview)
        {
            return new CandidateDecision(
                CognitiveMemoryRecallCandidateDecisionKind.SideContext,
                CognitiveMemoryRecallExclusionReasonKind.SourceInsufficient,
                "Candidate retained as side context because score geometry marked it review-worthy.");
        }

        return new CandidateDecision(
            CognitiveMemoryRecallCandidateDecisionKind.Selected,
            CognitiveMemoryRecallExclusionReasonKind.None,
            string.Join(" ", candidate.Reasons.Distinct(StringComparer.Ordinal)));
    }
}