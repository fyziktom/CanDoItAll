using CanDoItAll.AgentFramework.Core;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.CognitiveMemory;

public sealed class CognitiveMemoryScheduledAutomationRunner(
    ICognitiveMemoryAutomationSettingsService settingsService,
    ICognitiveMemorySourceIngestionService sourceIngestionService,
    ICognitiveMemoryConsolidationEngine consolidationEngine,
    IClock clock,
    ICognitiveMemoryProfessorAnchorService? professorAnchorService = null) : ICognitiveMemoryScheduledAutomationRunner
{
    private const int MaximumTake = 500;
    private const int MaximumCycles = 25;

    public async ValueTask<CognitiveMemoryScheduledAutomationRunResult> RunAsync(
        CognitiveMemoryScheduledAutomationRunRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var actorId = CognitiveMemoryGuard.EnsureText(request.ActorId, nameof(request.ActorId));
        var take = request.Take is > 0 and <= MaximumTake
            ? request.Take
            : throw new ArgumentOutOfRangeException(nameof(request.Take), $"Take must be between 1 and {MaximumTake}.");
        var maxCycles = request.MaxCycles is > 0 and <= MaximumCycles
            ? request.MaxCycles
            : throw new ArgumentOutOfRangeException(nameof(request.MaxCycles), $"MaxCycles must be between 1 and {MaximumCycles}.");
        var cycleId = NormalizeCycleId(request.CycleId);
        var settings = await settingsService.GetAsync(cancellationToken);
        if (!ScheduleAllowsRun(settings.ScheduleMode, request.TriggerKind))
        {
            return new CognitiveMemoryScheduledAutomationRunResult(
                settings.ScheduleMode,
                request.TriggerKind,
                Executed: false,
                SourceIngestionRuns: 0,
                SourceItemsSeen: 0,
                SourceItemsCreated: 0,
                ConsolidationRuns: 0,
                ConsolidationStatus: null,
                [$"Automation trigger {request.TriggerKind} is disabled by schedule mode {settings.ScheduleMode}."],
                cycleId,
                CyclesExecuted: 0,
                FinalCursor: null,
                Cycles: []);
        }

        var warnings = new List<string>();
        var sourceRuns = new List<CognitiveMemorySourceIngestionResult>();
        if (settings.AutoIngestProjectStructure)
        {
            if (request.ProjectId.HasValue)
            {
                sourceRuns.Add(await sourceIngestionService.IngestAsync(
                    new CognitiveMemorySourceIngestionRequest(
                        MemorySourceKind.WorkbenchProjectStructure,
                        request.ProjectId.Value,
                        BuildIdempotencyKey("project-structure", request, actorId, cycleId, 0),
                        Take: take,
                        ProjectId: request.ProjectId),
                    cancellationToken));
            }
            else
            {
                warnings.Add("Project structure ingestion was enabled but no project id was supplied.");
            }
        }

        if (settings.AutoIngestProcessRuntime)
        {
            sourceRuns.Add(await sourceIngestionService.IngestAsync(
                new CognitiveMemorySourceIngestionRequest(
                    MemorySourceKind.ProcessRuntime,
                    request.ProjectId ?? Guid.Empty,
                    BuildIdempotencyKey("process-runtime", request, actorId, cycleId, 0),
                    Take: take,
                    ProjectId: request.ProjectId),
                cancellationToken));
        }

        var cycles = new List<CognitiveMemoryScheduledAutomationCycleResult>();
        CognitiveMemoryConsolidationRunResult? consolidation = null;
        string? cursor = null;
        if (settings.AutoConsolidateAfterIngestion && sourceRuns.Any(run => run.Status == CognitiveMemorySourceIngestionStatus.Ingested))
        {
            for (var cycleSequence = 1; cycleSequence <= maxCycles; cycleSequence++)
            {
                consolidation = await consolidationEngine.RunAsync(
                    new CognitiveMemoryConsolidationRunRequest(
                        request.ProjectId,
                        request.TriggerKind == CognitiveMemoryAutomationTriggerKind.Nightly
                            ? CognitiveMemoryConsolidationMode.ProjectNightly
                            : CognitiveMemoryConsolidationMode.IncrementalRecent,
                        MapTriggerKind(request.TriggerKind),
                        CognitiveMemoryConsolidationProfile.IncrementalRecent,
                        request.PolicyContext ?? CreateDefaultPolicyContext(request.ProjectId, actorId),
                        BuildIdempotencyKey("consolidation", request, actorId, cycleId, cycleSequence),
                        CognitiveMemoryConsolidationBudget.Default,
                        cursor),
                    cancellationToken);
                cycles.Add(new CognitiveMemoryScheduledAutomationCycleResult(
                    cycleSequence,
                    cycleId,
                    consolidation.RunId,
                    consolidation.Status,
                    consolidation.SourceItemsScanned,
                    consolidation.CandidatesCreated,
                    cursor,
                    consolidation.NextCursor,
                    consolidation.Warnings));

                cursor = consolidation.NextCursor;
                if (consolidation.Status != CognitiveMemoryRunStatus.Succeeded ||
                    string.IsNullOrWhiteSpace(cursor) ||
                    (!request.ContinueUntilIdle && cycleSequence >= maxCycles))
                {
                    break;
                }
            }
        }

        if (professorAnchorService is not null &&
            request.ProjectId is { } projectId &&
            cycles.Any(cycle => cycle.Status == CognitiveMemoryRunStatus.Succeeded))
        {
            var assimilationResults = await professorAnchorService.ScanAssimilationAsync(
                new CognitiveMemoryProfessorAnchorAssimilationScanRequest(projectId),
                cancellationToken);
            if (assimilationResults.Count > 0)
            {
                warnings.Add($"Professor anchor assimilation scan resolved {assimilationResults.Count} anchor(s).");
            }
        }

        return new CognitiveMemoryScheduledAutomationRunResult(
            settings.ScheduleMode,
            request.TriggerKind,
            Executed: true,
            sourceRuns.Count,
            sourceRuns.Sum(run => run.SourceItemCount),
            sourceRuns.Sum(run => run.CreatedSourceItemCount),
            cycles.Count,
            consolidation?.Status,
            warnings,
            cycleId,
            cycles.Count,
            cursor,
            cycles);
    }

    private static bool ScheduleAllowsRun(
        CognitiveMemoryAutomationScheduleMode scheduleMode,
        CognitiveMemoryAutomationTriggerKind triggerKind)
        => triggerKind == CognitiveMemoryAutomationTriggerKind.Manual ||
           (scheduleMode, triggerKind) switch
           {
               (CognitiveMemoryAutomationScheduleMode.Nightly, CognitiveMemoryAutomationTriggerKind.Nightly) => true,
               (CognitiveMemoryAutomationScheduleMode.IdleTimeout, CognitiveMemoryAutomationTriggerKind.IdleTimeout) => true,
               (CognitiveMemoryAutomationScheduleMode.ScheduledMoments, CognitiveMemoryAutomationTriggerKind.ScheduledMoment) => true,
               _ => false
           };

    private static CognitiveMemoryConsolidationTriggerKind MapTriggerKind(CognitiveMemoryAutomationTriggerKind triggerKind)
        => triggerKind switch
        {
            CognitiveMemoryAutomationTriggerKind.Nightly => CognitiveMemoryConsolidationTriggerKind.Nightly,
            CognitiveMemoryAutomationTriggerKind.IdleTimeout => CognitiveMemoryConsolidationTriggerKind.Idle,
            CognitiveMemoryAutomationTriggerKind.ScheduledMoment => CognitiveMemoryConsolidationTriggerKind.Nightly,
            _ => CognitiveMemoryConsolidationTriggerKind.Manual
        };

    private static CognitiveMemoryPolicyContext CreateDefaultPolicyContext(
        Guid? projectId,
        string actorId)
        => new(
            projectId,
            actorId,
            CognitiveMemoryAccessLevel.Project,
            new CognitiveMemoryPolicyProfileId("scheduled-automation"),
            CognitiveMemoryRiskLevel.Low,
            AllowRestrictedContent: false);

    private string NormalizeCycleId(string? cycleId)
        => string.IsNullOrWhiteSpace(cycleId)
            ? $"automation-{clock.GetUtcNow():yyyyMMddHHmmssfffffff}"
            : CognitiveMemoryGuard.EnsureText(cycleId, nameof(cycleId));

    private static CognitiveMemoryIdempotencyKey BuildIdempotencyKey(
        string operation,
        CognitiveMemoryScheduledAutomationRunRequest request,
        string actorId,
        string cycleId,
        int cycleSequence)
        => new($"automation:{operation}:{request.TriggerKind}:{request.ProjectId?.ToString("D") ?? "global"}:{actorId}:{cycleId}:{cycleSequence}");
}
