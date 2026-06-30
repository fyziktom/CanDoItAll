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
    private async Task AddWorkspaceCandidatesAsync(
        AppDbContext dbContext,
        CognitiveMemoryWorkspaceFrameId? workspaceFrameId,
        Dictionary<Guid, RecallCandidateAccumulator> candidates,
        List<CognitiveMemoryRecallTraceStage> stages,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        if (workspaceFrameId is null)
        {
            stages.Add(Stage(
                CognitiveMemoryRecallTraceStageKind.CoarseCandidateActivation,
                CognitiveMemoryRecallChannelKind.Workspace,
                CognitiveMemoryRecallStageStatus.Skipped,
                0,
                0,
                0,
                "workspace:not-provided",
                completedAtUtc: nowUtc));
            return;
        }

        var slots = await dbContext.Set<CognitiveMemoryWorkingMemorySlotRecord>()
            .AsNoTracking()
            .Where(slot => slot.WorkspaceFrameId == workspaceFrameId.Value.Value && slot.MemoryRecordId != null)
            .OrderBy(slot => slot.Id)
            .Take(50)
            .Select(slot => new
            {
                slot.MemoryRecordId,
                slot.SourceSufficiency,
                slot.DisplayAttentionScore,
                slot.InclusionReason
            })
            .ToListAsync(cancellationToken);
        var records = await LoadRecordsByIdAsync(
            dbContext,
            slots.Select(slot => slot.MemoryRecordId!.Value).Distinct().ToArray(),
            cancellationToken);
        var recordsById = records.ToDictionary(record => record.Id);
        foreach (var slot in slots)
        {
            if (!recordsById.TryGetValue(slot.MemoryRecordId!.Value, out var record))
            {
                continue;
            }

            var candidate = GetCandidate(candidates, record);
            candidate.Channels.Add(CognitiveMemoryRecallChannelKind.Workspace);
            candidate.WorkspaceFocusFit = Math.Max(candidate.WorkspaceFocusFit ?? 0, slot.DisplayAttentionScore ?? 0.85);
            candidate.Reasons.Add($"Workspace focus carried candidate forward: {slot.InclusionReason}");
        }

        stages.Add(Stage(
            CognitiveMemoryRecallTraceStageKind.CoarseCandidateActivation,
            CognitiveMemoryRecallChannelKind.Workspace,
            CognitiveMemoryRecallStageStatus.Completed,
            slots.Count,
            records.Count,
            slots.Count - records.Count,
            "workspace:focus-slots",
            completedAtUtc: nowUtc));
    }

    private async Task AddSignalActivationCandidatesAsync(
        CognitiveMemoryRecallRequest request,
        Dictionary<Guid, RecallCandidateAccumulator> candidates,
        List<CognitiveMemoryRecallTraceStage> stages,
        List<string> warnings,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        try
        {
            var signalResult = await signalLedger.QueryAsync(
                new CognitiveMemorySignalQuery(
                    request.ProjectId,
                    request.PolicyContext,
                    new CognitiveMemoryPageRequest(take: Math.Min(50, CognitiveMemoryPageRequest.MaxTake)),
                    ConsumerKinds:
                    [
                        CognitiveMemorySignalConsumerKind.ActivationEngine,
                        CognitiveMemorySignalConsumerKind.RecallRanking
                    ]),
                cancellationToken);

            var linkedSignals = signalResult.Signals
                .Where(signal => signal.MemoryRecordId is not null)
                .ToList();
            foreach (var signal in linkedSignals)
            {
                if (!candidates.TryGetValue(signal.MemoryRecordId!.Value, out var candidate))
                {
                    continue;
                }

                candidate.Channels.Add(CognitiveMemoryRecallChannelKind.SignalActivation);
                candidate.MemoryActivation = Math.Max(candidate.MemoryActivation ?? 0, signal.DisplayMagnitudeProjection ?? 0.65);
                candidate.SignalIds.Add(signal.Id);
                candidate.Reasons.Add($"Signal activation channel contributed {signal.SignalKind} evidence.");
            }

            stages.Add(Stage(
                CognitiveMemoryRecallTraceStageKind.CoarseCandidateActivation,
                CognitiveMemoryRecallChannelKind.SignalActivation,
                CognitiveMemoryRecallStageStatus.Completed,
                signalResult.Signals.Count,
                linkedSignals.Count,
                signalResult.Signals.Count - linkedSignals.Count,
                "signals:recall-consumers",
                completedAtUtc: nowUtc));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(
                exception,
                "Cognitive memory signal activation recall channel unavailable for ProjectId={ProjectId}.",
                request.ProjectId);
            warnings.Add($"Signal activation channel unavailable: {exception.GetType().Name}.");
            stages.Add(Stage(
                CognitiveMemoryRecallTraceStageKind.CoarseCandidateActivation,
                CognitiveMemoryRecallChannelKind.SignalActivation,
                CognitiveMemoryRecallStageStatus.Unavailable,
                0,
                0,
                0,
                "signals:unavailable",
                failureCode: exception.GetType().Name,
                failureMessage: exception.Message,
                completedAtUtc: nowUtc));
        }
    }
}
