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
        var derivedMemoryExists = await dbContext.Set<CognitiveMemoryRecord>()
            .AnyAsync(record =>
                record.Id == request.DerivedMemoryRecordId.Value &&
                record.ProjectId == capture.ProjectId &&
                record.ValidationState == CognitiveMemoryValidationState.Approved &&
                (record.StabilityState == CognitiveMemoryStabilityState.Active ||
                 record.StabilityState == CognitiveMemoryStabilityState.Stable),
                cancellationToken);
        if (!derivedMemoryExists)
        {
            throw new InvalidOperationException($"Professor anchor capture '{request.CaptureId:D}' cannot be assimilated without an approved active derived memory.");
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
