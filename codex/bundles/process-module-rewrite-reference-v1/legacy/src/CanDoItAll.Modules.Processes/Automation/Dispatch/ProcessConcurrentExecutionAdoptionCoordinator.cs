using CanDoItAll.AgentFramework.Core;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessConcurrentExecutionAdoptionCoordinator
{
    private static readonly TimeSpan PollDelay = TimeSpan.FromMilliseconds(250);

    internal static async Task<ProcessRunAutomationDispatchService.ConcurrentAutomationExecution?> TryAdoptAsync(
        IProcessAutomationExecutionClient executionClient,
        ProcessRunAutomationDispatchService.DispatchCandidate candidate,
        DateTimeOffset now,
        string automationActor,
        TimeSpan staleExecutionRunTimeout,
        Func<ProcessAutomationExecutionRunDetail, string> resolveResponseText,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(executionClient);
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentException.ThrowIfNullOrWhiteSpace(automationActor);
        ArgumentNullException.ThrowIfNull(resolveResponseText);

        var executionRuns = await executionClient.ListExecutionRunsAsync(
            ProcessExecutionRunQueryBuilder.ForCandidate(candidate),
            cancellationToken);
        var blockingExecutionRunId = ProcessAutomationExecutionRunSelection.ResolveBlockingAutomationExecutionRunId(
            candidate.StepRun.StartedAtUtc,
            executionRuns,
            now,
            automationActor,
            staleExecutionRunTimeout);
        var recoverableExecutionRunId = blockingExecutionRunId ??
            ProcessAutomationExecutionRunSelection.ResolveRecoverableAutomationExecutionRunId(
                candidate.StepRun.Status,
                candidate.StepRun.StartedAtUtc,
                executionRuns,
                automationActor);
        if (!recoverableExecutionRunId.HasValue)
        {
            return null;
        }

        var detail = await executionClient.GetExecutionRunDetailAsync(recoverableExecutionRunId.Value, cancellationToken);
        for (var pollIndex = 0; pollIndex < 2 && !IsTerminal(detail.Run); pollIndex++)
        {
            await Task.Delay(PollDelay, cancellationToken);
            detail = await executionClient.GetExecutionRunDetailAsync(recoverableExecutionRunId.Value, cancellationToken);
        }

        return new ProcessRunAutomationDispatchService.ConcurrentAutomationExecution(
            recoverableExecutionRunId.Value,
            detail,
            resolveResponseText(detail));
    }

    private static bool IsTerminal(ProcessAutomationExecutionRunRecord run)
    {
        return run.State is ProcessAutomationExecutionState.Completed or ProcessAutomationExecutionState.Failed;
    }
}
