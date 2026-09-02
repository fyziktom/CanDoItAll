using CanDoItAll.AgentFramework.Models;
using Microsoft.Agents.AI.Workflows;

namespace CanDoItAll.AgentFramework.Maf;

internal sealed record MafWorkflowStreamTurn(
    string SessionId,
    RunStatus Status,
    IReadOnlyList<WorkflowEvent> Events,
    ExternalRequest? PendingRequest,
    CheckpointInfo? WaitingCheckpoint);

internal sealed class MafWorkflowStreamingRunDriver
{
    public async Task<MafWorkflowStreamTurn> StartAsync(
        Workflow workflow,
        WorkflowNodeInput input,
        CheckpointManager checkpointManager,
        string sessionId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(checkpointManager);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        await using var run = await InProcessExecution.RunStreamingAsync(
            workflow,
            input,
            checkpointManager,
            sessionId,
            cancellationToken);
        return await ConsumeTurnAsync(run, cancellationToken);
    }

    public async Task<MafWorkflowStreamTurn> ResumeAndRespondAsync(
        Workflow workflow,
        CheckpointInfo checkpoint,
        CheckpointManager checkpointManager,
        WorkflowBackendExternalRequestLink expectedRequest,
        Func<ExternalRequest, ExternalResponse> responseFactory,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(checkpointManager);
        ArgumentNullException.ThrowIfNull(expectedRequest);
        ArgumentNullException.ThrowIfNull(responseFactory);

        await using var run = await InProcessExecution.ResumeStreamingAsync(
            workflow,
            checkpoint,
            checkpointManager,
            cancellationToken);
        try
        {
            var restoredRequest = await ReadRestoredRequestAsync(run, cancellationToken);
            ValidateRestoredRequest(run.SessionId, checkpoint.SessionId, restoredRequest, expectedRequest);
            await run.SendResponseAsync(responseFactory(restoredRequest));
            return await ConsumeTurnAsync(run, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await run.CancelRunAsync();
            throw;
        }
    }

    private static async Task<MafWorkflowStreamTurn> ConsumeTurnAsync(
        StreamingRun run,
        CancellationToken cancellationToken)
    {
        var events = new List<WorkflowEvent>();
        var nativeRequests = new List<ExternalRequest>();
        var correlator = new MafHumanInputCheckpointCorrelator();
        var pendingCheckpointCount = 0;
        var faulted = false;
        try
        {
            await foreach (var workflowEvent in run.WatchStreamAsync(
                blockOnPendingRequest: false,
                cancellationToken))
            {
                events.Add(workflowEvent);
                switch (workflowEvent)
                {
                    case RequestInfoEvent requestEvent:
                        nativeRequests.Add(requestEvent.Request);
                        correlator.ObserveRequest(new MafHumanInputRequestFact(
                            new MafWorkflowSessionId(run.SessionId),
                            new MafNativeRequestId(requestEvent.Request.RequestId),
                            new MafRequestPortId(requestEvent.Request.PortInfo.PortId)));
                        break;
                    case SuperStepCompletedEvent
                    {
                        CompletionInfo:
                        {
                            HasPendingRequests: true,
                            Checkpoint: { } checkpoint
                        }
                    }:
                        pendingCheckpointCount++;
                        correlator.ObserveCheckpoint(new MafWorkflowCheckpointFact(
                            new MafWorkflowSessionId(checkpoint.SessionId),
                            new MafCheckpointId(checkpoint.CheckpointId),
                            HasPendingRequest: true));
                        break;
                    case WorkflowErrorEvent:
                        faulted = true;
                        break;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await run.CancelRunAsync();
            throw;
        }

        MafHumanInputCheckpointCorrelationResult? correlation = null;
        if (!faulted || nativeRequests.Count > 0 || pendingCheckpointCount > 0)
        {
            correlation = correlator.CompleteBoundary(
                faulted
                    ? MafWorkflowStreamCompletionKind.Faulted
                    : MafWorkflowStreamCompletionKind.Completed);
            if (correlation.Status == MafHumanInputCheckpointCorrelationStatus.Rejected)
            {
                throw new MafHumanInputCheckpointCorrelationException(correlation.FailureKind!.Value);
            }
        }

        ExternalRequest? pendingRequest = null;
        CheckpointInfo? waitingCheckpoint = null;
        if (correlation?.Boundary is { } boundary)
        {
            pendingRequest = nativeRequests.Single(request =>
                string.Equals(
                    request.RequestId,
                    boundary.Request.NativeRequestId.Value,
                    StringComparison.Ordinal) &&
                string.Equals(
                    request.PortInfo.PortId,
                    boundary.Request.PortId.Value,
                    StringComparison.Ordinal));
            waitingCheckpoint = new CheckpointInfo(
                boundary.Checkpoint.SessionId.Value,
                boundary.Checkpoint.CheckpointId.Value);
        }

        return new MafWorkflowStreamTurn(
            run.SessionId,
            await run.GetStatusAsync(cancellationToken),
            events,
            pendingRequest,
            waitingCheckpoint);
    }

    private static async Task<ExternalRequest> ReadRestoredRequestAsync(
        StreamingRun run,
        CancellationToken cancellationToken)
    {
        var requests = new List<ExternalRequest>();
        await foreach (var workflowEvent in run.WatchStreamAsync(
            blockOnPendingRequest: false,
            cancellationToken))
        {
            switch (workflowEvent)
            {
                case RequestInfoEvent requestEvent:
                    requests.Add(requestEvent.Request);
                    break;
                case WorkflowErrorEvent errorEvent:
                    throw errorEvent.Exception ?? new InvalidOperationException(
                        "MAF checkpoint resume failed before the pending request was restored.");
            }
        }

        return requests.Count switch
        {
            1 => requests[0],
            0 => throw new InvalidOperationException(
                "MAF checkpoint resume did not restore the expected pending request."),
            _ => throw new InvalidOperationException(
                "MAF checkpoint resume restored multiple pending requests; this backend supports one request per boundary.")
        };
    }

    private static void ValidateRestoredRequest(
        string sessionId,
        string expectedSessionId,
        ExternalRequest restored,
        WorkflowBackendExternalRequestLink expected)
    {
        if (!string.Equals(sessionId, expectedSessionId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Restored MAF session does not match the persisted checkpoint session.");
        }

        if (!string.Equals(restored.RequestId, expected.BackendRequestId.Value, StringComparison.Ordinal) ||
            !string.Equals(restored.PortInfo.PortId, expected.BackendRequestPortId.Value, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Restored MAF request identity does not match persisted request metadata.");
        }
    }
}
