using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.CognitiveMemory;

public sealed class CognitiveMemoryProfessorAnchorService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IClock clock,
    ICognitiveMemoryProfessorAssimilationEvaluator? assimilationEvaluator = null) : ICognitiveMemoryProfessorAnchorService
{
    private const int MaximumScanAnchors = 200;
    private readonly ICognitiveMemoryProfessorAssimilationEvaluator assimilationEvaluator = assimilationEvaluator ?? new CognitiveMemoryProfessorAssimilationEvaluator(dbContextFactory);

    public async ValueTask<CognitiveMemoryProfessorAnchorResult> MarkAssimilatedAsync(
        CognitiveMemoryProfessorAnchorAssimilationRequest request,
        CancellationToken cancellationToken = default)
        => await MarkAssimilatedCoreAsync(request, requireUsageAndIntegration: false, cancellationToken);

    public async ValueTask<CognitiveMemoryProfessorAnchorResult> FadeAsync(
        Guid captureId,
        CancellationToken cancellationToken = default)
    {
        if (captureId == Guid.Empty)
        {
            throw new ArgumentException("Professor anchor fade requires a capture id.", nameof(captureId));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var capture = await dbContext.Set<CognitiveMemoryCuratorCapturedImprovementRecord>()
            .SingleOrDefaultAsync(item => item.Id == captureId, cancellationToken)
            ?? throw new InvalidOperationException($"Professor anchor capture '{captureId:D}' was not found.");
        if (capture.AnchorState != CognitiveMemoryProfessorAnchorState.Assimilated || capture.AssimilatedMemoryRecordId is null)
        {
            throw new InvalidOperationException($"Professor anchor capture '{captureId:D}' cannot fade before assimilation links an approved derived memory.");
        }

        var now = clock.GetUtcNow();
        capture.AnchorState = CognitiveMemoryProfessorAnchorState.Faded;
        capture.AnchorRetiredAtUtc = now;
        capture.ConcurrencyToken = Guid.NewGuid();
        await DemoteDirectCaptureMemoryAsync(dbContext, capture, now, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new CognitiveMemoryProfessorAnchorResult(
            capture.Id,
            capture.AnchorState,
            new CognitiveMemoryRecordId(capture.AssimilatedMemoryRecordId.Value));
    }

    public async ValueTask<IReadOnlyList<CognitiveMemoryProfessorAnchorResult>> ScanAssimilationAsync(
        CognitiveMemoryProfessorAnchorAssimilationScanRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ProjectId == Guid.Empty)
        {
            throw new ArgumentException("Professor anchor assimilation scan requires a project id.", nameof(request));
        }

        if (request.MaxAnchors <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Professor anchor assimilation scan max anchors must be positive.");
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var captures = await dbContext.Set<CognitiveMemoryCuratorCapturedImprovementRecord>()
            .AsNoTracking()
            .Where(capture =>
                capture.ProjectId == request.ProjectId &&
                capture.AppliedMemoryRecordId != null &&
                capture.AnchorState != CognitiveMemoryProfessorAnchorState.Assimilated &&
                capture.AnchorState != CognitiveMemoryProfessorAnchorState.Faded &&
                capture.AnchorState != CognitiveMemoryProfessorAnchorState.Rejected)
            .OrderBy(capture => capture.CreatedAtUtc)
            .Take(Math.Min(request.MaxAnchors, MaximumScanAnchors))
            .ToListAsync(cancellationToken);
        var results = new List<CognitiveMemoryProfessorAnchorResult>();
        foreach (var capture in captures)
        {
            var candidateIds = await FindDerivedCandidateMemoryIdsAsync(dbContext, capture, cancellationToken);
            foreach (var candidateId in candidateIds)
            {
                var evaluation = await assimilationEvaluator.EvaluateAsync(
                    new CognitiveMemoryProfessorAnchorAssimilationEvaluationRequest(
                        capture.Id,
                        new CognitiveMemoryRecordId(candidateId),
                        RequireUsageAndIntegration: true),
                    cancellationToken);
                if (!evaluation.CanAssimilate)
                {
                    continue;
                }

                results.Add(await MarkAssimilatedCoreAsync(
                    new CognitiveMemoryProfessorAnchorAssimilationRequest(
                        capture.Id,
                        new CognitiveMemoryRecordId(candidateId),
                        request.FadeAnchor),
                    requireUsageAndIntegration: true,
                    cancellationToken));
                break;
            }
        }

        return results;
    }

    private async ValueTask<CognitiveMemoryProfessorAnchorResult> MarkAssimilatedCoreAsync(
        CognitiveMemoryProfessorAnchorAssimilationRequest request,
        bool requireUsageAndIntegration,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.CaptureId == Guid.Empty)
        {
            throw new ArgumentException("Professor anchor assimilation requires a capture id.", nameof(request));
        }

        var evaluation = await assimilationEvaluator.EvaluateAsync(
            new CognitiveMemoryProfessorAnchorAssimilationEvaluationRequest(
                request.CaptureId,
                request.DerivedMemoryRecordId,
                requireUsageAndIntegration),
            cancellationToken);
        if (!evaluation.CanAssimilate)
        {
            throw new InvalidOperationException($"Professor anchor capture '{request.CaptureId:D}' cannot be assimilated: {evaluation.Reason}");
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var capture = await dbContext.Set<CognitiveMemoryCuratorCapturedImprovementRecord>()
            .SingleOrDefaultAsync(item => item.Id == request.CaptureId, cancellationToken)
            ?? throw new InvalidOperationException($"Professor anchor capture '{request.CaptureId:D}' was not found.");

        var now = clock.GetUtcNow();
        capture.AssimilatedMemoryRecordId = request.DerivedMemoryRecordId.Value;
        capture.AnchorState = request.FadeAnchor
            ? CognitiveMemoryProfessorAnchorState.Faded
            : CognitiveMemoryProfessorAnchorState.Assimilated;
        capture.AnchorRetiredAtUtc = request.FadeAnchor ? now : null;
        capture.ConcurrencyToken = Guid.NewGuid();
        if (request.FadeAnchor)
        {
            await DemoteDirectCaptureMemoryAsync(dbContext, capture, now, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return new CognitiveMemoryProfessorAnchorResult(
            capture.Id,
            capture.AnchorState,
            new CognitiveMemoryRecordId(request.DerivedMemoryRecordId.Value));
    }

    private static async Task<IReadOnlyList<Guid>> FindDerivedCandidateMemoryIdsAsync(
        AppDbContext dbContext,
        CognitiveMemoryCuratorCapturedImprovementRecord capture,
        CancellationToken cancellationToken = default)
    {
        var candidateIds = new HashSet<Guid>();
        if (capture.SourceItemId is { } sourceItemId)
        {
            var linked = await dbContext.Set<CognitiveMemorySourceLinkRecord>()
                .AsNoTracking()
                .Where(link => link.SourceItemId == sourceItemId)
                .Select(link => link.MemoryRecordId)
                .ToListAsync(cancellationToken);
            foreach (var memoryRecordId in linked)
            {
                candidateIds.Add(memoryRecordId);
            }
        }

        if (capture.EvidenceAnchorId is { } evidenceAnchorId)
        {
            var linked = await dbContext.Set<CognitiveMemoryRecordEvidenceAnchorRecord>()
                .AsNoTracking()
                .Where(link => link.EvidenceAnchorId == evidenceAnchorId)
                .Select(link => link.MemoryRecordId)
                .ToListAsync(cancellationToken);
            foreach (var memoryRecordId in linked)
            {
                candidateIds.Add(memoryRecordId);
            }
        }

        if (capture.AppliedMemoryRecordId is { } appliedMemoryRecordId)
        {
            candidateIds.Remove(appliedMemoryRecordId);
        }

        if (candidateIds.Count == 0)
        {
            return [];
        }

        return await dbContext.Set<CognitiveMemoryRecord>()
            .AsNoTracking()
            .Where(record =>
                candidateIds.Contains(record.Id) &&
                record.ProjectId == capture.ProjectId &&
                record.ValidationState == CognitiveMemoryValidationState.Approved &&
                (record.StabilityState == CognitiveMemoryStabilityState.Active ||
                 record.StabilityState == CognitiveMemoryStabilityState.Stable))
            .OrderByDescending(record => record.UpdatedAtUtc)
            .Select(record => record.Id)
            .ToListAsync(cancellationToken);
    }

    private static async Task DemoteDirectCaptureMemoryAsync(
        AppDbContext dbContext,
        CognitiveMemoryCuratorCapturedImprovementRecord capture,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (capture.AppliedMemoryRecordId is not { } appliedMemoryRecordId ||
            capture.AssimilatedMemoryRecordId == appliedMemoryRecordId)
        {
            return;
        }

        var directMemory = await dbContext.Set<CognitiveMemoryRecord>()
            .SingleOrDefaultAsync(record => record.Id == appliedMemoryRecordId, cancellationToken);
        if (directMemory is null)
        {
            return;
        }

        directMemory.ValidationState = CognitiveMemoryValidationState.Retired;
        directMemory.StabilityState = CognitiveMemoryStabilityState.Deprecated;
        directMemory.GeneratedReason = $"Professor direct quote faded after assimilation into memory '{capture.AssimilatedMemoryRecordId:D}'.";
        directMemory.UpdatedAtUtc = now;
        directMemory.ConcurrencyToken = Guid.NewGuid();

        var directClaims = await dbContext.Set<CognitiveMemoryClaimRecord>()
            .Where(claim => claim.MemoryRecordId == directMemory.Id)
            .ToListAsync(cancellationToken);
        foreach (var claim in directClaims)
        {
            claim.ValidationState = CognitiveMemoryValidationState.Retired;
            claim.StabilityState = CognitiveMemoryStabilityState.Deprecated;
            claim.UpdatedAtUtc = now;
            claim.ConcurrencyToken = Guid.NewGuid();
        }
    }
}
