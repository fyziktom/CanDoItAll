using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.CognitiveMemory;

public sealed class CognitiveMemoryProfessorAnchorService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IClock clock) : ICognitiveMemoryProfessorAnchorService
{
    public async ValueTask<CognitiveMemoryProfessorAnchorResult> MarkAssimilatedAsync(
        CognitiveMemoryProfessorAnchorAssimilationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.CaptureId == Guid.Empty)
        {
            throw new ArgumentException("Professor anchor assimilation requires a capture id.", nameof(request));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var capture = await dbContext.Set<CognitiveMemoryCuratorCapturedImprovementRecord>()
            .SingleOrDefaultAsync(item => item.Id == request.CaptureId, cancellationToken)
            ?? throw new InvalidOperationException($"Professor anchor capture '{request.CaptureId:D}' was not found.");
        if (capture.AppliedMemoryRecordId == request.DerivedMemoryRecordId.Value)
        {
            throw new InvalidOperationException($"Professor anchor capture '{request.CaptureId:D}' cannot use its direct capture memory as assimilation proof.");
        }

        var derivedMemory = await dbContext.Set<CognitiveMemoryRecord>()
            .SingleOrDefaultAsync(record => record.Id == request.DerivedMemoryRecordId.Value, cancellationToken);
        if (derivedMemory is null ||
            derivedMemory.ProjectId != capture.ProjectId ||
            derivedMemory.ValidationState != CognitiveMemoryValidationState.Approved ||
            derivedMemory.StabilityState is not (CognitiveMemoryStabilityState.Active or CognitiveMemoryStabilityState.Stable))
        {
            throw new InvalidOperationException($"Professor anchor capture '{request.CaptureId:D}' cannot be assimilated without an approved active derived memory.");
        }

        var sourceLinks = await dbContext.Set<CognitiveMemorySourceLinkRecord>()
            .AsNoTracking()
            .Where(link => link.MemoryRecordId == derivedMemory.Id)
            .ToListAsync(cancellationToken);
        var evidenceLinks = await dbContext.Set<CognitiveMemoryRecordEvidenceAnchorRecord>()
            .AsNoTracking()
            .Where(link => link.MemoryRecordId == derivedMemory.Id)
            .ToListAsync(cancellationToken);
        var hasAnchorLineage = capture.SourceItemId is { } sourceItemId && sourceLinks.Any(link => link.SourceItemId == sourceItemId) ||
                               capture.EvidenceAnchorId is { } evidenceAnchorId && evidenceLinks.Any(link => link.EvidenceAnchorId == evidenceAnchorId);
        if (!hasAnchorLineage)
        {
            throw new InvalidOperationException($"Professor anchor capture '{request.CaptureId:D}' cannot be assimilated because the derived memory does not retain anchor lineage.");
        }

        var hasIndependentSupport = sourceLinks.Any(link => capture.SourceItemId is null || link.SourceItemId != capture.SourceItemId.Value) ||
                                    evidenceLinks.Any(link => capture.EvidenceAnchorId is null || link.EvidenceAnchorId != capture.EvidenceAnchorId.Value);
        if (!hasIndependentSupport)
        {
            throw new InvalidOperationException($"Professor anchor capture '{request.CaptureId:D}' cannot be assimilated without independent derived support.");
        }

        capture.AssimilatedMemoryRecordId = request.DerivedMemoryRecordId.Value;
        capture.AnchorState = request.FadeAnchor
            ? CognitiveMemoryProfessorAnchorState.Faded
            : CognitiveMemoryProfessorAnchorState.Assimilated;
        capture.AnchorRetiredAtUtc = request.FadeAnchor ? clock.GetUtcNow() : null;
        capture.ConcurrencyToken = Guid.NewGuid();
        await dbContext.SaveChangesAsync(cancellationToken);
        return new CognitiveMemoryProfessorAnchorResult(
            capture.Id,
            capture.AnchorState,
            new CognitiveMemoryRecordId(request.DerivedMemoryRecordId.Value));
    }

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

        capture.AnchorState = CognitiveMemoryProfessorAnchorState.Faded;
        capture.AnchorRetiredAtUtc = clock.GetUtcNow();
        capture.ConcurrencyToken = Guid.NewGuid();
        await dbContext.SaveChangesAsync(cancellationToken);
        return new CognitiveMemoryProfessorAnchorResult(
            capture.Id,
            capture.AnchorState,
            new CognitiveMemoryRecordId(capture.AssimilatedMemoryRecordId.Value));
    }
}
