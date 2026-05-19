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
    private sealed class RecallCandidateAccumulator(MemoryRecordSnapshot record)
    {
        public MemoryRecordSnapshot Record { get; } = record;

        public HashSet<CognitiveMemoryRecallChannelKind> Channels { get; } = [];

        public List<string> Reasons { get; } = [];

        public List<Guid> SignalIds { get; } = [];

        public double? SemanticSimilarity { get; set; }

        public double? LexicalMatch { get; set; }

        public double? GraphProximity { get; set; }

        public double? WorkspaceFocusFit { get; set; }

        public double? MemoryActivation { get; set; }

        public double? ContextSeparation { get; set; }

        public double? ContradictionPressure { get; set; }

        public string ProjectionPayloadHash { get; set; } = string.Empty;

        public string ContextBoundaryReason { get; set; } = string.Empty;

        public CognitiveMemoryRecallChannelKind PrimaryChannelKind
            => Channels.Contains(CognitiveMemoryRecallChannelKind.Workspace)
                ? CognitiveMemoryRecallChannelKind.Workspace
                : Channels.Contains(CognitiveMemoryRecallChannelKind.VectorProjection)
                    ? CognitiveMemoryRecallChannelKind.VectorProjection
                    : Channels.Contains(CognitiveMemoryRecallChannelKind.Lexical)
                        ? CognitiveMemoryRecallChannelKind.Lexical
                        : Channels.Contains(CognitiveMemoryRecallChannelKind.Graph)
                            ? CognitiveMemoryRecallChannelKind.Graph
                            : CognitiveMemoryRecallChannelKind.Unknown;
    }

    private sealed record MemoryRecordSnapshot(
        Guid Id,
        Guid? ProjectId,
        CognitiveMemoryRecordKind Kind,
        string Title,
        string SummaryText,
        string CanonicalText,
        string TopicKey,
        CognitiveMemoryValidationState ValidationState,
        CognitiveMemoryStabilityState StabilityState,
        int SourceEvidenceCount,
        int EvidenceAnchorCount,
        Guid? PrimaryClaimId,
        Guid? PrimaryContextFrameId,
        CognitiveMemoryAccessLevel AccessLevel,
        CognitiveMemoryRiskLevel RiskLevel,
        DateTimeOffset UpdatedAtUtc);

    private sealed record SourceTextItemSnapshot(
        Guid Id,
        string Title,
        string ContentText,
        string SourceItemKey,
        string? Locator,
        DateTimeOffset UpdatedAtUtc);

    private sealed record SourceTextLexicalMatch(
        MemoryRecordSnapshot Record,
        double Score);

    private sealed record RelationSnapshot(
        Guid SourceMemoryRecordId,
        Guid TargetMemoryRecordId,
        CognitiveMemoryRelationKind RelationKind,
        double? DisplayStrengthProjection,
        string Reason);

    private sealed record SourceGraphExpansionResult(
        int EdgeCount,
        int RecordCount,
        bool Limited);

    private sealed record SourceGraphItemSnapshot(
        Guid Id,
        Guid SourceManifestId,
        Guid? ProjectId,
        string SourceSystem,
        string SourceItemType,
        string SourceItemKey,
        string Title,
        string? Locator,
        string ProvenanceJson);

    private sealed record ProjectStructureNodeSourceSnapshot(
        string SourceEntityId,
        string ParentId);

    private sealed record ClaimSnapshot(
        Guid Id,
        Guid MemoryRecordId,
        CognitiveMemoryClaimKind ClaimKind,
        CognitiveMemoryBeliefStateKind CurrentBeliefState,
        CognitiveMemoryValidationState ValidationState,
        Guid? PrimaryContextFrameId);

    private sealed record CandidateDecision(
        CognitiveMemoryRecallCandidateDecisionKind DecisionKind,
        CognitiveMemoryRecallExclusionReasonKind ExclusionReasonKind,
        string Reason);

    private sealed record EvaluatedRecallCandidate(
        CognitiveMemoryRecallCandidateId Id,
        MemoryRecordSnapshot Record,
        CognitiveMemoryWorkspaceFrameId? WorkspaceFrameId,
        CognitiveMemoryRecallChannelKind PrimaryChannelKind,
        CognitiveMemoryRecallCandidateDecisionKind DecisionKind,
        CognitiveMemoryRecallExclusionReasonKind ExclusionReasonKind,
        CognitiveMemoryScoreEvaluationTrace ScoreTrace,
        CognitiveMemoryScoreScalarProjection? DisplayRankProjection,
        IReadOnlyList<CognitiveMemoryClaimId> SelectedClaimIds,
        IReadOnlyList<CognitiveMemoryEvidenceAnchorId> EvidenceAnchorIds,
        string Reason,
        IReadOnlyList<CognitiveMemoryRecallChannelKind> ChannelKinds,
        string ContextBoundaryReason,
        IReadOnlyList<string> SourceScopeKeys);

    private sealed record SourceLinkSnapshot(
        Guid MemoryRecordId,
        Guid SourceItemId,
        string? Locator,
        string? QuoteHash,
        string Summary);

    private sealed record SourceItemSnapshot(
        Guid Id,
        Guid? ProjectId,
        string SourceSystem,
        string SourceItemKey,
        string Title,
        string ContentText,
        string? Locator,
        CognitiveMemoryRedactionState RedactionState,
        CognitiveMemoryAccessLevel AccessLevel);

    private sealed record EvidenceAnchorSnapshot(
        Guid Id,
        Guid? SourceItemId,
        string SourceSystem,
        string Locator,
        string QuoteHash,
        CognitiveMemoryRedactionState RedactionState);
}
