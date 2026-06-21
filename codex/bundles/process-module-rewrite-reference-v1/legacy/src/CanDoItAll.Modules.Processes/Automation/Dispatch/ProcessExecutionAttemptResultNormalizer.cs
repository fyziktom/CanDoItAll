using CanDoItAll.AgentFramework.Core;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessExecutionAttemptResultNormalizer
{
    internal static async Task<ProcessExecutionAttemptResult> NormalizeAsync(
        IProcessAutomationExecutionClient executionClient,
        ProcessRunAutomationDispatchService.DispatchCandidate candidate,
        ProcessAutomationExecutionRunResult? executionResult,
        ProcessRunAutomationDispatchService.ConcurrentAutomationExecution? adoptedConcurrentExecution,
        ProcessExecutionAttemptResult? failedExecution,
        Func<ProcessRunAutomationDispatchService.DispatchCandidate, string?, ProcessAutomationExecutionRunDetail, string> resolvePreferredResponseText,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(executionClient);
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(resolvePreferredResponseText);

        if (adoptedConcurrentExecution is not null)
        {
            return new ProcessExecutionAttemptResult(
                adoptedConcurrentExecution.ExecutionRunId,
                adoptedConcurrentExecution.Detail,
                adoptedConcurrentExecution.ResponseText,
                adoptedConcurrentExecution.Detail.Run.ChatSessionId);
        }

        if (failedExecution is not null)
        {
            return failedExecution;
        }

        if (executionResult is null)
        {
            throw new InvalidOperationException(
                $"AgentFramework execution start did not return a result for process step '{candidate.StepRun.Id:D}'.");
        }

        var detail = await executionClient.GetExecutionRunDetailAsync(executionResult.ExecutionRunId, cancellationToken);
        return new ProcessExecutionAttemptResult(
            executionResult.ExecutionRunId,
            detail,
            resolvePreferredResponseText(candidate, executionResult.ResponseText, detail),
            executionResult.ChatSessionId);
    }
}

internal sealed record ProcessExecutionAttemptResult(
    Guid ExecutionRunId,
    ProcessAutomationExecutionRunDetail Detail,
    string ResponseText,
    Guid? ChatSessionId);
