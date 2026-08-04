using CanDoItAll.AgentFramework.Models;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.AgentFramework.Core;

internal sealed partial class AgentFrameworkWorkspaceExecutionService
{
    private async Task CleanupTerminalExecutionRunProcessesAsync(
        ExecutionRunRecord run,
        bool terminalRunPersisted)
    {
        var terminalRun = run;
        if (!terminalRunPersisted)
        {
            try
            {
                var persistedRun = TryGetExecutionRunStore() is { } executionRunStore
                    ? await executionRunStore
                        .GetExecutionRunAsync(run.Id, CancellationToken.None)
                        .ConfigureAwait(false)
                    : null;
                if (persistedRun?.State is not ExecutionState.Completed
                    and not ExecutionState.Failed)
                {
                    return;
                }

                terminalRun = persistedRun;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    "Could not verify whether execution run {ExecutionRunId} reached a persisted terminal state before workspace process lease cleanup. FailureType={FailureType}.",
                    run.Id,
                    exception.GetType().Name);
                return;
            }
        }

        try
        {
            using var auditScope = WorkspaceExecutionAuditContext.BeginScope(terminalRun);
            var result = await workspaceProcessLeaseCleaner
                .CleanupAsync(terminalRun.Id)
                .ConfigureAwait(false);

            if (result.CleanedStartupReceiptPaths.Count > 0)
            {
                logger.LogInformation(
                    "Cleaned {WorkspaceProcessLeaseCount} ExecutionRun workspace process lease(s) after execution run {ExecutionRunId} reached a persisted terminal state. Startup receipts: {StartupReceiptPaths}.",
                    result.CleanedStartupReceiptPaths.Count,
                    terminalRun.Id,
                    result.CleanedStartupReceiptPaths);
            }

            foreach (var failure in result.Failures)
            {
                logger.LogError(
                    "Failed to clean ExecutionRun workspace process lease for execution run {ExecutionRunId} and startup receipt {StartupReceiptPath}. The durable lease remains available for retry. Failure: {CleanupFailure}.",
                    terminalRun.Id,
                    failure.StartupReceiptPath,
                    WorkflowExecutorRedaction.RedactText(failure.Message));
            }
        }
        catch (Exception exception)
        {
            logger.LogError(
                "ExecutionRun workspace process lease cleanup failed unexpectedly after execution run {ExecutionRunId} reached a persisted terminal state. The primary execution outcome remains authoritative. FailureType={FailureType}.",
                terminalRun.Id,
                exception.GetType().Name);
        }
    }
}
