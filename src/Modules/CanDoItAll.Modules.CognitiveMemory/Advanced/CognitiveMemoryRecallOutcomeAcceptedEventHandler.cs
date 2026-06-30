using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.CognitiveMemory;

public sealed class CognitiveMemoryRecallOutcomeAcceptedEventHandler(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ICognitiveMemoryProfessorAcceptedUseSignalEmitter acceptedUseSignalEmitter) : ICognitiveMemoryRecallOutcomeAcceptedEventHandler
{
    public async ValueTask<CognitiveMemoryRecallOutcomeAcceptedEventResult> HandleAsync(
        CognitiveMemoryRecallOutcomeAcceptedEvent acceptedEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(acceptedEvent);
        if (!string.Equals(acceptedEvent.PolicyContext.ActorId, acceptedEvent.ActorId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Accepted outcome actor id must match the policy context actor id.");
        }

        var signalRequest = new CognitiveMemoryProfessorAcceptedUseSignalRequest(
            acceptedEvent.ProjectId,
            acceptedEvent.ActorId,
            acceptedEvent.PolicyContext,
            acceptedEvent.RecallTraceId,
            acceptedEvent.SynthesisId,
            acceptedEvent.StatementId,
            acceptedEvent.DerivedMemoryRecordId,
            acceptedEvent.AcceptedOutcomeId,
            acceptedEvent.OutcomeSummary);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existingSignal = await CognitiveMemoryProfessorAcceptedUseSignalEmitter.FindExistingAcceptedUseSignalAsync(
            dbContext,
            signalRequest,
            cancellationToken);
        if (existingSignal is not null)
        {
            return new CognitiveMemoryRecallOutcomeAcceptedEventResult(
                AcceptedUseSignalEmitted: false,
                existingSignal,
                [],
                "Accepted outcome event was already recorded for this statement and derived memory.");
        }

        var exactStatementEvidenceExists = await dbContext.Set<CognitiveMemorySynthesizedStatementSourceMapRecord>()
            .AsNoTracking()
            .AnyAsync(sourceMap =>
                sourceMap.ProjectId == acceptedEvent.ProjectId &&
                sourceMap.SynthesisId == acceptedEvent.SynthesisId.Value &&
                sourceMap.StatementId == acceptedEvent.StatementId.Value &&
                sourceMap.MemoryRecordId == acceptedEvent.DerivedMemoryRecordId.Value &&
                sourceMap.EvidenceAnchorId != null,
                cancellationToken);
        if (!exactStatementEvidenceExists)
        {
            throw new InvalidOperationException(
                $"Accepted outcome '{acceptedEvent.AcceptedOutcomeId:D}' cannot count broad recall lineage for derived memory '{acceptedEvent.DerivedMemoryRecordId}'.");
        }

        var emission = await acceptedUseSignalEmitter.EmitAsync(signalRequest, cancellationToken);
        return new CognitiveMemoryRecallOutcomeAcceptedEventResult(
            AcceptedUseSignalEmitted: true,
            emission.Signal,
            emission.AssimilationResults,
            "Accepted outcome event emitted a professor accepted-use signal.");
    }
}
