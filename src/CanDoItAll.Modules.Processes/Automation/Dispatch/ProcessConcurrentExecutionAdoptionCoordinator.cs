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
        if (!blockingExecutionRunId.HasValue)
        {
            return null;
        }

        var detail = await executionClient.GetExecutionRunDetailAsync(blockingExecutionRunId.Value, cancellationToken);
        for (var pollIndex = 0; pollIndex < 2 && !IsTerminal(detail.Run); pollIndex++)
        {
            await Task.Delay(PollDelay, cancellationToken);
            detail = await executionClient.GetExecutionRunDetailAsync(blockingExecutionRunId.Value, cancellationToken);
        }

        return new ProcessRunAutomationDispatchService.ConcurrentAutomationExecution(
            blockingExecutionRunId.Value,
            detail,
            resolveResponseText(detail));
    }

    private static bool IsTerminal(ProcessAutomationExecutionRunRecord run)
    {
        return run.State is ProcessAutomationExecutionState.Completed or ProcessAutomationExecutionState.Failed;
    }
}
