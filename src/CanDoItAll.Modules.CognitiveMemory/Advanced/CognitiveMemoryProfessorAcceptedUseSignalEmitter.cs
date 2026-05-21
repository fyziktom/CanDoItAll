using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.CognitiveMemory;

public sealed class CognitiveMemoryProfessorAcceptedUseSignalEmitter(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ICognitiveMemorySignalLedger signalLedger,
    ICognitiveMemoryProfessorAnchorService professorAnchorService) : ICognitiveMemoryProfessorAcceptedUseSignalEmitter
{
    public async ValueTask<CognitiveMemoryProfessorAcceptedUseSignalResult> EmitAsync(
        CognitiveMemoryProfessorAcceptedUseSignalRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        CognitiveMemoryGuard.EnsureNonEmpty(request.ProjectId, nameof(request.ProjectId));
        var actorId = CognitiveMemoryGuard.EnsureText(request.ActorId, nameof(request.ActorId));
        CognitiveMemoryGuard.EnsureNonEmpty(request.RecallTraceId, nameof(request.RecallTraceId));
        CognitiveMemoryGuard.EnsureNonEmpty(request.AcceptedOutcomeId, nameof(request.AcceptedOutcomeId));
        var outcomeSummary = CognitiveMemoryGuard.EnsureText(request.OutcomeSummary, nameof(request.OutcomeSummary));
        if (!string.Equals(request.PolicyContext.ActorId, actorId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Accepted-use signal actor id must match the policy context actor id.");
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var memory = await dbContext.Set<CognitiveMemoryRecord>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                record => record.Id == request.DerivedMemoryRecordId.Value && record.ProjectId == request.ProjectId,
                cancellationToken)
            ?? throw new InvalidOperationException($"Derived memory '{request.DerivedMemoryRecordId}' was not found in project '{request.ProjectId:D}'.");
        var directCapture = await dbContext.Set<CognitiveMemoryCuratorCapturedImprovementRecord>()
            .AsNoTracking()
            .Where(capture =>
                capture.ProjectId == request.ProjectId &&
                capture.AppliedMemoryRecordId == request.DerivedMemoryRecordId.Value)
            .Select(capture => capture.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (directCapture != Guid.Empty)
        {
            throw new InvalidOperationException($"Professor capture '{directCapture:D}' direct memory cannot be counted as accepted derived use.");
        }

        if (memory.ValidationState != CognitiveMemoryValidationState.Approved ||
            memory.StabilityState is not (CognitiveMemoryStabilityState.Active or CognitiveMemoryStabilityState.Stable))
        {
            throw new InvalidOperationException($"Derived memory '{request.DerivedMemoryRecordId}' must be approved and active before accepted use can be emitted.");
        }

        var synthesis = await dbContext.Set<CognitiveMemorySynthesizedRecallRecord>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                record =>
                    record.Id == request.SynthesisId.Value &&
                    record.ProjectId == request.ProjectId &&
                    record.RecallTraceId == request.RecallTraceId,
                cancellationToken)
            ?? throw new InvalidOperationException($"Recall synthesis '{request.SynthesisId}' was not found for recall trace '{request.RecallTraceId:D}'.");
        var statementExists = await dbContext.Set<CognitiveMemorySynthesizedStatementRecord>()
            .AsNoTracking()
            .AnyAsync(
                statement =>
                    statement.Id == request.StatementId.Value &&
                    statement.SynthesisId == synthesis.Id &&
                    statement.ProjectId == request.ProjectId,
                cancellationToken);
        if (!statementExists)
        {
            throw new InvalidOperationException($"Synthesized statement '{request.StatementId}' was not found in synthesis '{request.SynthesisId}'.");
        }

        var statementSourceMaps = await dbContext.Set<CognitiveMemorySynthesizedStatementSourceMapRecord>()
            .AsNoTracking()
            .Where(sourceMap =>
                sourceMap.ProjectId == request.ProjectId &&
                sourceMap.SynthesisId == request.SynthesisId.Value &&
                sourceMap.StatementId == request.StatementId.Value &&
                sourceMap.MemoryRecordId == request.DerivedMemoryRecordId.Value)
            .ToListAsync(cancellationToken);
        if (statementSourceMaps.Count == 0)
        {
            throw new InvalidOperationException($"Synthesized statement '{request.StatementId}' did not use derived memory '{request.DerivedMemoryRecordId}'.");
        }

        var evidenceAnchorIds = statementSourceMaps
            .Select(sourceMap => sourceMap.EvidenceAnchorId)
            .OfType<Guid>()
            .Distinct()
            .Select(evidenceAnchorId => new CognitiveMemoryEvidenceAnchorId(evidenceAnchorId))
            .ToArray();
        if (evidenceAnchorIds.Length == 0)
        {
            var linkedEvidenceAnchorIds = await dbContext.Set<CognitiveMemoryRecordEvidenceAnchorRecord>()
                .AsNoTracking()
                .Where(anchor => anchor.MemoryRecordId == request.DerivedMemoryRecordId.Value)
                .Select(anchor => anchor.EvidenceAnchorId)
                .Distinct()
                .ToListAsync(cancellationToken);
            evidenceAnchorIds = linkedEvidenceAnchorIds
                .Select(evidenceAnchorId => new CognitiveMemoryEvidenceAnchorId(evidenceAnchorId))
                .ToArray();
        }

        if (evidenceAnchorIds.Length == 0)
        {
            throw new InvalidOperationException($"Accepted-use signal for derived memory '{request.DerivedMemoryRecordId}' requires persisted source evidence.");
        }

        var signalContract = new
        {
            SignalKind = CognitiveMemorySignalKind.ProfessorAnchorAcceptedUse,
            SourceKind = CognitiveMemorySignalSourceKind.RecallTrace
        };
        var result = await signalLedger.PublishAsync(
            new CognitiveMemorySignalPublicationRequest(
                request.ProjectId,
                signalContract.SignalKind,
                signalContract.SourceKind,
                CognitiveMemoryActorKind.User,
                actorId,
                request.PolicyContext,
                $"Professor-derived memory was accepted in outcome {request.AcceptedOutcomeId:D}: {outcomeSummary}",
                [
                    new CognitiveMemorySignalComponentDraft(CognitiveMemoryScoreDimensionKind.Usefulness, 0.92),
                    new CognitiveMemorySignalComponentDraft(CognitiveMemoryScoreDimensionKind.Reward, 0.85),
                    new CognitiveMemorySignalComponentDraft(CognitiveMemoryScoreDimensionKind.StrategicAlignment, 0.8)
                ],
                [
                    CognitiveMemorySignalConsumerKind.RecallRanking,
                    CognitiveMemorySignalConsumerKind.ActivationEngine,
                    CognitiveMemorySignalConsumerKind.ConfidenceCalibration
                ],
                evidenceAnchorIds,
                MemoryRecordId: request.DerivedMemoryRecordId,
                SourceItemId: ResolveSourceItemId(statementSourceMaps),
                Metadata: new Dictionary<string, string>
                {
                    ["recallTraceId"] = request.RecallTraceId.ToString("D"),
                    ["synthesisId"] = request.SynthesisId.Value.ToString("D"),
                    ["statementId"] = request.StatementId.Value.ToString("D"),
                    ["derivedMemoryRecordId"] = request.DerivedMemoryRecordId.Value.ToString("D"),
                    ["acceptedOutcomeId"] = request.AcceptedOutcomeId.ToString("D"),
                    ["outcomeSummary"] = outcomeSummary,
                    ["producer"] = nameof(CognitiveMemoryProfessorAcceptedUseSignalEmitter)
                }),
            cancellationToken);

        var assimilationResults = await professorAnchorService.ScanAssimilationAsync(
            new CognitiveMemoryProfessorAnchorAssimilationScanRequest(request.ProjectId),
            cancellationToken);
        return new CognitiveMemoryProfessorAcceptedUseSignalResult(result.Signal, assimilationResults);
    }

    private static CognitiveMemorySourceItemId? ResolveSourceItemId(
        IReadOnlyList<CognitiveMemorySynthesizedStatementSourceMapRecord> statementSourceMaps)
    {
        var sourceItemIds = statementSourceMaps
            .Select(sourceMap => sourceMap.SourceItemId)
            .OfType<Guid>()
            .Distinct()
            .OrderBy(sourceItemId => sourceItemId)
            .ToArray();
        return sourceItemIds.Length == 0
            ? null
            : new CognitiveMemorySourceItemId(sourceItemIds[0]);
    }
}
