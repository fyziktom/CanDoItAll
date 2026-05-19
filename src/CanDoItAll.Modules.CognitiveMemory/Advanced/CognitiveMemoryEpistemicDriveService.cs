using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.CognitiveMemory;

public sealed class CognitiveMemoryEpistemicDriveService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ICognitiveMemoryScoreGeometryDriver scoreGeometryDriver,
    IClock clock) : ICognitiveMemoryEpistemicDriveService
{
    public async ValueTask<IReadOnlyList<CognitiveMemoryLearningProposalRecord>> ScanAsync(
        CognitiveMemoryEpistemicScanRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var now = clock.GetUtcNow();
        var proposals = new List<CognitiveMemoryLearningProposalRecord>();
        var answerGateGaps = await dbContext.Set<CognitiveMemoryAnswerGateDecisionRecord>()
            .AsNoTracking()
            .Where(item => item.ProjectId == request.ProjectId &&
                           item.DecisionKind != CognitiveMemoryAnswerGateDecisionKind.Answer &&
                           item.DecisionKind != CognitiveMemoryAnswerGateDecisionKind.Warn)
            .OrderByDescending(item => item.CreatedAtUtc)
            .Take(20)
            .ToListAsync(cancellationToken);
        foreach (var answerGate in answerGateGaps)
        {
            proposals.Add(await CreateProposalAsync(
                dbContext,
                request.ProjectId,
                "answer-gate",
                CognitiveMemoryKnowledgeGapKind.RepeatedAbstention,
                $"Answer gate required {answerGate.DecisionKind}",
                answerGate.Reason,
                0.8,
                0.7,
                now,
                cancellationToken));
        }

        var calibrationGaps = await dbContext.Set<CognitiveMemoryCalibrationAggregateRecord>()
            .AsNoTracking()
            .Where(item => item.ProjectId == request.ProjectId &&
                           (item.OverconfidenceRate >= 0.4 ||
                            item.SourceInsufficientRate >= 0.4 ||
                            item.WrongScopeRate >= 0.4))
            .Take(20)
            .ToListAsync(cancellationToken);
        foreach (var aggregate in calibrationGaps)
        {
            proposals.Add(await CreateProposalAsync(
                dbContext,
                request.ProjectId,
                aggregate.DomainKey,
                CognitiveMemoryKnowledgeGapKind.PoorCalibration,
                $"Calibration gap in {aggregate.DomainKey}/{aggregate.TaskTypeKey}",
                "Repeated overconfidence, wrong-scope, or source-insufficient outcomes need learning/probing.",
                Math.Clamp(aggregate.OverconfidenceRate + aggregate.SourceInsufficientRate + aggregate.WrongScopeRate, 0, 1),
                Math.Clamp(aggregate.SourceInsufficientRate + aggregate.WrongScopeRate, 0, 1),
                now,
                cancellationToken));
        }

        proposals.AddRange(await CreateSourceCoverageProposalsAsync(
            dbContext,
            request.ProjectId,
            now,
            cancellationToken));

        await dbContext.SaveChangesAsync(cancellationToken);
        return proposals;
    }

    public async ValueTask<CognitiveMemoryLearningProposalRecord> DecideProposalAsync(
        Guid proposalId,
        CognitiveMemoryLearningProposalStatus decision,
        string actorId,
        string notes,
        CancellationToken cancellationToken = default)
    {
        if (proposalId == Guid.Empty)
        {
            throw new ArgumentException("Learning proposal id must not be empty.", nameof(proposalId));
        }

        if (decision is CognitiveMemoryLearningProposalStatus.Draft or CognitiveMemoryLearningProposalStatus.PendingApproval)
        {
            throw new ArgumentException("Learning proposal decision must be terminal or approval-like.", nameof(decision));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var proposal = await dbContext.Set<CognitiveMemoryLearningProposalRecord>()
            .SingleOrDefaultAsync(item => item.Id == proposalId, cancellationToken)
            ?? throw new InvalidOperationException($"Learning proposal '{proposalId:D}' was not found.");
        var now = clock.GetUtcNow();
        proposal.Status = decision;
        proposal.DecidedByActorId = CognitiveMemoryGuard.EnsureText(actorId, nameof(actorId));
        proposal.DecisionNotes = notes.Trim();
        proposal.DecidedAtUtc = now;
        if (decision == CognitiveMemoryLearningProposalStatus.Approved)
        {
            dbContext.Add(new CognitiveMemoryLearningTaskRecord
            {
                ProjectId = proposal.ProjectId,
                LearningProposalId = proposal.Id,
                Status = CognitiveMemoryLearningTaskStatus.Planned,
                WorkflowExecutorKey = CognitiveMemoryWorkflowExecutorIds.LearningProposal.Value,
                ApprovalActorId = actorId.Trim(),
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return proposal;
    }

    private async Task<CognitiveMemoryLearningProposalRecord> CreateProposalAsync(
        AppDbContext dbContext,
        Guid projectId,
        string regionKey,
        CognitiveMemoryKnowledgeGapKind gapKind,
        string title,
        string explanation,
        double missingKnowledgePressure,
        double sourceWeakness,
        DateTimeOffset now,
        CancellationToken cancellationToken,
        string evidenceRefsJson = "[]",
        CognitiveMemoryCoverageState? coverageStateOverride = null,
        int sourceEvidenceCount = 0)
    {
        var region = await dbContext.Set<CognitiveMemoryKnowledgeRegionRecord>()
            .SingleOrDefaultAsync(
                item => item.ProjectId == projectId &&
                        item.RegionKind == CognitiveMemoryKnowledgeRegionKind.Domain &&
                        item.RegionKey == regionKey,
                cancellationToken);
        if (region is null)
        {
            region = new CognitiveMemoryKnowledgeRegionRecord
            {
                ProjectId = projectId,
                RegionKind = CognitiveMemoryKnowledgeRegionKind.Domain,
                RegionKey = regionKey,
                DisplayName = regionKey,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            dbContext.Add(region);
        }

        var coverage = await dbContext.Set<CognitiveMemoryCoverageMapRecord>()
            .SingleOrDefaultAsync(item => item.ProjectId == projectId && item.KnowledgeRegionId == region.Id, cancellationToken);
        if (coverage is null)
        {
            dbContext.Add(new CognitiveMemoryCoverageMapRecord
            {
                ProjectId = projectId,
                KnowledgeRegionId = region.Id,
                CoverageState = coverageStateOverride ?? (sourceWeakness >= 0.7 ? CognitiveMemoryCoverageState.Thin : CognitiveMemoryCoverageState.Unknown),
                SourceEvidenceCount = sourceEvidenceCount,
                RefreshedAtUtc = now
            });
        }
        else
        {
            coverage.CoverageState = coverageStateOverride ?? coverage.CoverageState;
            coverage.SourceEvidenceCount = Math.Max(coverage.SourceEvidenceCount, sourceEvidenceCount);
            coverage.RefreshedAtUtc = now;
        }

        var gap = new CognitiveMemoryKnowledgeGapRecord
        {
            ProjectId = projectId,
            KnowledgeRegionId = region.Id,
            GapKind = gapKind,
            Summary = explanation,
            EvidenceRefsJson = evidenceRefsJson,
            CreatedAtUtc = now
        };
        dbContext.Add(gap);
        var trace = await CognitiveMemoryAdvancedScoring.EvaluateAndPersistAsync(
            dbContext,
            scoreGeometryDriver,
            projectId,
            CognitiveMemoryScoreOwnerKind.LearningProposal,
            gap.Id,
            CognitiveMemoryScoreSpaceKind.EpistemicNeed,
            [
                CognitiveMemoryAdvancedScoring.Component(CognitiveMemoryScoreDimensionKind.MissingKnowledgePressure, missingKnowledgePressure),
                CognitiveMemoryAdvancedScoring.Component(CognitiveMemoryScoreDimensionKind.SourceWeakness, sourceWeakness),
                CognitiveMemoryAdvancedScoring.Component(CognitiveMemoryScoreDimensionKind.ExpectedLearningValue, 0.75),
                CognitiveMemoryAdvancedScoring.Component(CognitiveMemoryScoreDimensionKind.ExpectedEffort, 0.45)
            ],
            CognitiveMemoryScoreProjectionBucket.NeedsReview,
            now,
            cancellationToken);
        var proposal = new CognitiveMemoryLearningProposalRecord
        {
            ProjectId = projectId,
            KnowledgeGapId = gap.Id,
            Status = CognitiveMemoryLearningProposalStatus.PendingApproval,
            Title = title,
            Explanation = explanation,
            EvidenceRefsJson = evidenceRefsJson,
            Risks = new CognitiveMemoryRiskNotes("Learning proposals do not create canonical truth until source-backed review accepts outputs."),
            AcceptanceCriteria = "Approved learning must cite source refs and route durable changes through mutation authority or review.",
            NeedScoreEvaluationTraceId = trace.Id.Value,
            NeedBucket = trace.ScalarProjection?.Bucket ?? CognitiveMemoryScoreProjectionBucket.Unknown,
            DisplayPriorityProjection = trace.ScalarProjection?.DisplayScore,
            CreatedAtUtc = now
        };
        dbContext.Add(proposal);
        return proposal;
    }

    private async Task<IReadOnlyList<CognitiveMemoryLearningProposalRecord>> CreateSourceCoverageProposalsAsync(
        AppDbContext dbContext,
        Guid projectId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var sourceItems = await dbContext.Set<CognitiveMemorySourceItemRecord>()
            .AsNoTracking()
            .Where(item => item.ProjectId == projectId)
            .OrderBy(item => item.Id)
            .Take(200)
            .Select(item => new EpistemicSourceCoverageSnapshot(
                item.Id,
                item.SourceSystem,
                item.SourceItemType,
                item.Title,
                item.ContentText,
                item.ContentHash))
            .ToListAsync(cancellationToken);
        var proposals = new List<CognitiveMemoryLearningProposalRecord>();
        foreach (var group in sourceItems
            .SelectMany(source => CognitiveMemoryConsolidationFactExtractor
                .ResolvePlanningDimensions(source.ContentText)
                .Select(dimension => new { Source = source, Dimension = dimension }))
            .GroupBy(item => item.Dimension, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            var regionKey = $"planning:{group.Key}";
            if (await HasCanonicalCoverageAsync(dbContext, projectId, regionKey, cancellationToken) ||
                await HasExistingLearningProposalAsync(dbContext, projectId, regionKey, cancellationToken))
            {
                continue;
            }

            var evidenceSources = group
                .Select(item => item.Source)
                .DistinctBy(source => source.Id)
                .Take(5)
                .ToArray();
            var evidenceRefsJson = JsonSerializer.Serialize(
                evidenceSources.Select(source => $"source-item:{source.Id:D}").ToArray(),
                CognitiveMemoryAdvancedJson.Options);
            var explanation =
                $"Source-backed coverage gap: {evidenceSources.Length} source item(s) discuss planning dimension '{group.Key}', but no canonical reusable memory covers it yet.";
            proposals.Add(await CreateProposalAsync(
                dbContext,
                projectId,
                regionKey,
                CognitiveMemoryKnowledgeGapKind.ProfessorSuggestedExpansion,
                $"Study reusable planning knowledge for {group.Key}",
                explanation,
                missingKnowledgePressure: Math.Clamp(0.45 + evidenceSources.Length * 0.1, 0, 1),
                sourceWeakness: 0.25,
                now,
                cancellationToken,
                evidenceRefsJson,
                CognitiveMemoryCoverageState.Thin,
                evidenceSources.Length));
        }

        return proposals;
    }

    private static async Task<bool> HasCanonicalCoverageAsync(
        AppDbContext dbContext,
        Guid projectId,
        string regionKey,
        CancellationToken cancellationToken)
        => await dbContext.Set<CognitiveMemoryRecord>()
            .AsNoTracking()
            .AnyAsync(record =>
                record.ProjectId == projectId &&
                record.TopicKey == regionKey,
                cancellationToken);

    private static async Task<bool> HasExistingLearningProposalAsync(
        AppDbContext dbContext,
        Guid projectId,
        string regionKey,
        CancellationToken cancellationToken)
        => await (
            from proposal in dbContext.Set<CognitiveMemoryLearningProposalRecord>().AsNoTracking()
            join gap in dbContext.Set<CognitiveMemoryKnowledgeGapRecord>().AsNoTracking()
                on proposal.KnowledgeGapId equals gap.Id
            join region in dbContext.Set<CognitiveMemoryKnowledgeRegionRecord>().AsNoTracking()
                on gap.KnowledgeRegionId equals region.Id
            where proposal.ProjectId == projectId &&
                  region.RegionKey == regionKey &&
                  proposal.Status != CognitiveMemoryLearningProposalStatus.Draft
            select proposal.Id)
            .AnyAsync(cancellationToken);

    private sealed record EpistemicSourceCoverageSnapshot(
        Guid Id,
        string SourceSystem,
        string SourceItemType,
        string Title,
        string ContentText,
        string ContentHash);
}

