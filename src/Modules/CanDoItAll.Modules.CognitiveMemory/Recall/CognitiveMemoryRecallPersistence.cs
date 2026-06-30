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
    private static void AddStageRecords(
        AppDbContext dbContext,
        Guid traceId,
        Guid projectId,
        IReadOnlyList<CognitiveMemoryRecallTraceStage> stages,
        DateTimeOffset nowUtc)
    {
        foreach (var stage in stages)
        {
            dbContext.Add(new CognitiveMemoryRecallTraceStageRecord
            {
                RecallTraceId = traceId,
                ProjectId = projectId,
                StageKind = stage.StageKind,
                ChannelKind = stage.ChannelKind,
                Status = stage.Status,
                CandidateCount = stage.CandidateCount,
                SelectedCount = stage.SelectedCount,
                ExcludedCount = stage.ExcludedCount,
                LimitingBudget = stage.LimitingBudget,
                ProviderTrace = stage.ProviderTrace,
                FailureCode = stage.FailureCode,
                FailureMessage = stage.FailureMessage,
                StartedAtUtc = nowUtc,
                CompletedAtUtc = stage.CompletedAtUtc
            });
        }
    }

    private static void AddCandidateRecords(
        AppDbContext dbContext,
        Guid traceId,
        CognitiveMemoryWorkspaceFrameId? workspaceFrameId,
        IReadOnlyList<EvaluatedRecallCandidate> candidates,
        CognitiveMemoryRecallContextPack contextPack,
        DateTimeOffset nowUtc)
    {
        foreach (var candidate in candidates)
        {
            var refs = contextPack.SourceRefs
                .Where(sourceRef => sourceRef.MemoryRecordId.Value == candidate.Record.Id)
                .ToArray();
            dbContext.Add(new CognitiveMemoryRecallCandidateRecord
            {
                Id = candidate.Id.Value,
                RecallTraceId = traceId,
                ProjectId = candidate.Record.ProjectId,
                PrimaryChannelKind = candidate.PrimaryChannelKind,
                DecisionKind = candidate.DecisionKind,
                ExclusionReasonKind = candidate.ExclusionReasonKind,
                MemoryRecordId = candidate.Record.Id,
                MemoryKind = candidate.Record.Kind,
                ClaimId = candidate.SelectedClaimIds.FirstOrDefault().Value == Guid.Empty ? null : candidate.SelectedClaimIds.First().Value,
                SourceItemId = refs.FirstOrDefault(sourceRef => sourceRef.SourceItemId is not null)?.SourceItemId?.Value,
                EvidenceAnchorId = refs.FirstOrDefault(sourceRef => sourceRef.EvidenceAnchorId is not null)?.EvidenceAnchorId?.Value,
                WorkspaceFrameId = workspaceFrameId?.Value,
                ContextFrameId = candidate.Record.PrimaryContextFrameId,
                ScoreEvaluationTraceId = candidate.ScoreTrace.Id.Value,
                ScoreBucket = candidate.ScoreTrace.ScalarProjection?.Bucket ?? CognitiveMemoryScoreProjectionBucket.Unknown,
                DisplayRankProjection = candidate.DisplayRankProjection?.DisplayScore,
                HasSourceDetail = refs.Any(sourceRef => sourceRef.IncludedInContext),
                SourceRedacted = refs.Any(sourceRef => sourceRef.RedactionState is CognitiveMemoryRedactionState.Redacted or CognitiveMemoryRedactionState.Restricted),
                EstimatedTokenCount = EstimateTokenCount(candidate.Record.SummaryText, candidate.Record.CanonicalText),
                SourceRefCount = refs.Length,
                Title = candidate.Record.Title,
                Summary = candidate.Record.SummaryText,
                Reason = candidate.Reason,
                ChannelTraceJson = JsonSerializer.Serialize(candidate.ChannelKinds.Select(kind => kind.ToString()).ToArray(), CognitiveMemoryJson.SerializerOptions),
                CreatedAtUtc = nowUtc
            });
        }
    }

    private static void AddContextPackRecords(
        AppDbContext dbContext,
        Guid traceId,
        CognitiveMemoryRecallContextPack contextPack,
        int characterBudget,
        DateTimeOffset nowUtc)
    {
        dbContext.Add(new CognitiveMemoryRecallContextPackRecord
        {
            Id = contextPack.Id.Value,
            RecallTraceId = traceId,
            ProjectId = contextPack.ProjectId,
            WorkspaceFrameId = contextPack.WorkspaceFrameId?.Value,
            Title = contextPack.Title,
            Summary = contextPack.Summary,
            CharacterBudget = characterBudget,
            RenderedCharacterCount = contextPack.Sections.Sum(section => section.Content.Length),
            SectionCount = contextPack.Sections.Count,
            SourceRefCount = contextPack.SourceRefs.Count,
            WarningCount = contextPack.Warnings.Count,
            MetadataJson = SerializeMetadata(contextPack.Metadata),
            CreatedAtUtc = nowUtc
        });

        for (var index = 0; index < contextPack.Sections.Count; index++)
        {
            var section = contextPack.Sections[index];
            dbContext.Add(new CognitiveMemoryRecallContextSectionRecord
            {
                ContextPackId = contextPack.Id.Value,
                RecallTraceId = traceId,
                ProjectId = contextPack.ProjectId,
                SectionKind = section.SectionKind,
                Sequence = index,
                SectionKey = section.SectionId.Value,
                Title = section.Title,
                Content = section.Content,
                MemoryRecordId = section.MemoryRecordIds.FirstOrDefault().Value == Guid.Empty ? null : section.MemoryRecordIds.First().Value,
                ClaimId = section.ClaimIds.FirstOrDefault().Value == Guid.Empty ? null : section.ClaimIds.First().Value,
                SourceItemId = section.SourceRefs.FirstOrDefault(sourceRef => sourceRef.SourceItemId is not null)?.SourceItemId?.Value,
                AccessLevel = section.SourceRefs.FirstOrDefault()?.AccessLevel ?? CognitiveMemoryAccessLevel.Project,
                RedactionState = section.SourceRefs.FirstOrDefault()?.RedactionState ?? CognitiveMemoryRedactionState.Safe,
                EstimatedTokenCount = EstimateTokenCount(section.Content),
                CreatedAtUtc = nowUtc
            });
        }

        foreach (var sourceRef in contextPack.SourceRefs)
        {
            dbContext.Add(new CognitiveMemoryRecallSourceRefRecord
            {
                RecallTraceId = traceId,
                ContextPackId = contextPack.Id.Value,
                ProjectId = contextPack.ProjectId,
                MemoryRecordId = sourceRef.MemoryRecordId.Value,
                SourceItemId = sourceRef.SourceItemId?.Value,
                EvidenceAnchorId = sourceRef.EvidenceAnchorId?.Value,
                SourceSystem = sourceRef.SourceSystem,
                Locator = sourceRef.Locator,
                QuoteHash = string.Empty,
                Summary = sourceRef.Summary,
                AccessLevel = sourceRef.AccessLevel,
                RedactionState = sourceRef.RedactionState,
                IncludedInContext = sourceRef.IncludedInContext,
                ExclusionReasonKind = sourceRef.ExclusionReasonKind,
                CreatedAtUtc = nowUtc
            });
        }
    }
}