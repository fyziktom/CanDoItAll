using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessNoProgressRetryJournalWriter(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IClock clock)
{
    public Task RecordCompressedDiagnosticAsync(
        ProcessRunAutomationDispatchService.DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail,
        IReadOnlyList<string> missingRequiredTools,
        IReadOnlyList<string> retryReasons,
        ProcessRunAutomationDispatchService.NoProgressRetrySignal signal,
        CancellationToken cancellationToken)
    {
        return RecordAsync(
            candidate,
            detail,
            missingRequiredTools,
            retryReasons,
            signal,
            ProcessRuntimeEventTypes.NoProgressRetryCompressed,
            "No-progress retry compressed",
            $"Execution run '{detail.Run.Id:D}' repeated the same unsatisfied process requirements without new satisfied evidence. Fingerprint: {signal.Fingerprint}.",
            cancellationToken);
    }

    public Task RecordObservedAsync(
        ProcessRunAutomationDispatchService.DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail,
        IReadOnlyList<string> missingRequiredTools,
        IReadOnlyList<string> retryReasons,
        ProcessRunAutomationDispatchService.NoProgressRetrySignal signal,
        CancellationToken cancellationToken)
    {
        return RecordAsync(
            candidate,
            detail,
            missingRequiredTools,
            retryReasons,
            signal,
            ProcessRuntimeEventTypes.NoProgressRetryObserved,
            "No-progress retry observed",
            $"Execution run '{detail.Run.Id:D}' produced an unsatisfied no-progress retry fingerprint before another governed attempt. Fingerprint: {signal.Fingerprint}.",
            cancellationToken);
    }

    private async Task RecordAsync(
        ProcessRunAutomationDispatchService.DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail,
        IReadOnlyList<string> missingRequiredTools,
        IReadOnlyList<string> retryReasons,
        ProcessRunAutomationDispatchService.NoProgressRetrySignal signal,
        string eventType,
        string title,
        string description,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await dbContext.Set<ProcessJournalEntry>().AddAsync(
            new ProcessJournalEntry
            {
                ProcessRunId = candidate.Run.Id,
                StepRunId = candidate.StepRun.Id,
                EventType = eventType,
                Title = title,
                Description = description,
                CorrelationId = signal.Fingerprint,
                OperatingMode = candidate.Run.OperatingMode,
                PolicyVersion = $"definition-version:{candidate.Run.ProcessDefinitionVersionId:D}",
                EnvironmentMode = candidate.Run.OperatingMode.ToString(),
                ReplayContextJson = JsonSerializer.Serialize(
                    CreatePayload(candidate, detail, missingRequiredTools, retryReasons, signal),
                    AgentOutputJson.SerializerOptions),
                OccurredAtUtc = clock.GetUtcNow()
            },
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static object CreatePayload(
        ProcessRunAutomationDispatchService.DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail,
        IReadOnlyList<string> missingRequiredTools,
        IReadOnlyList<string> retryReasons,
        ProcessRunAutomationDispatchService.NoProgressRetrySignal signal)
    {
        var retryFacts = ProcessRecoveryRetryDecisionRules.CreateFacts(
            detail,
            missingRequiredTools,
            []);
        return new
        {
            ProcessRunId = candidate.Run.Id,
            StepRunId = candidate.StepRun.Id,
            signal.ExecutionRunId,
            signal.Fingerprint,
            signal.ToolSignature,
            signal.ArtifactValidationFingerprint,
            signal.MutationDelta,
            signal.ProofDelta,
            MissingRequiredTools = missingRequiredTools,
            RetryReasons = retryReasons,
            retryFacts.FailedToolNames
        };
    }
}
