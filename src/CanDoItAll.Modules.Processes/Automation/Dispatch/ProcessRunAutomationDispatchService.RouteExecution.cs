using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace CanDoItAll.Modules.Processes;

internal sealed partial class ProcessRunAutomationDispatchService
{
    private async Task<ProcessClaimedDispatchResult> RunClaimedDispatchAsync(
        ProcessDispatchClaimCoordinator claimCoordinator,
        Guid processRunId,
        Guid? triggerStepRunId,
        string trigger,
        ProcessStepDispatchClaim dispatchClaim,
        Func<CancellationToken, Task>? renewLeaseAsync,
        CancellationToken cancellationToken)
    {
        var dispatchRenewLeaseAsync = CreateDispatchRenewLeaseCallback(claimCoordinator, dispatchClaim, renewLeaseAsync);
        var execution = new ProcessRouteExecutionContext(
            processRunId,
            triggerStepRunId,
            trigger,
            ProcessDispatchRouteModelAdapters.FromDispatcherClaim(dispatchClaim),
            dispatchRenewLeaseAsync,
            cancellationToken);

        try
        {
            execution.DispatchHeartbeat = claimCoordinator.StartHeartbeat(
                dispatchClaim,
                dispatchRenewLeaseAsync,
                cancellationToken);
            execution.DispatchCancellationToken = execution.DispatchHeartbeat.DispatchCancellationToken;
            return await ExecuteClaimedDispatchRouteAsync(execution);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException) when (execution.DispatchHeartbeat?.ClaimLost == true)
        {
            return HandleDispatchHeartbeatClaimLost(execution);
        }
        catch (ProcessDispatchClaimLostException exception)
        {
            return HandleDispatchClaimLost(execution, exception);
        }
        catch (Exception exception)
        {
            return await HandleDispatchFailureAsync(execution, exception);
        }
        finally
        {
            if (execution.DispatchHeartbeat is not null)
            {
                await execution.DispatchHeartbeat.DisposeAsync();
            }

            await claimCoordinator.ReleaseAsync(dispatchClaim, cancellationToken);
        }
    }

    private async Task<ProcessClaimedDispatchResult> ExecuteClaimedDispatchRouteAsync(
        ProcessRouteExecutionContext execution)
    {
        var candidateHydrationStarted = Stopwatch.GetTimestamp();
        var candidate = await CreateCandidateHydrationService().LoadAsync(
            execution.ProcessRunId,
            execution.DispatchClaim.StepRunId,
            execution.Trigger,
            execution.DispatchCancellationToken);
        execution.Candidate = candidate is null
            ? null
            : ProcessDispatchRouteModelAdapters.FromDispatcherCandidate(candidate);
        logger.LogDebug(
            "Hydrated claimed dispatch candidate for process run {ProcessRunId}, step {StepRunId}. CandidateFound={CandidateFound} ElapsedMilliseconds={ElapsedMilliseconds}.",
            execution.ProcessRunId,
            execution.DispatchClaim.StepRunId,
            execution.Candidate is not null,
            GetElapsedMilliseconds(candidateHydrationStarted));
        if (execution.Candidate is null)
        {
            return ProcessClaimedDispatchResult.ContinueCandidates;
        }

        return await CreateClaimedDispatchRouteHandlerPipeline().ExecuteAsync(
            new ProcessDispatchRouteContext(execution, execution.Candidate));
    }
}
