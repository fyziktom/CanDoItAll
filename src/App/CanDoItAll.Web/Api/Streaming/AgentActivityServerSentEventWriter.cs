using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedKernel.Streaming;

namespace CanDoItAll.Web.Api.Streaming;

internal static class AgentActivityServerSentEventWriter
{
    public static async Task<long> PumpAsync(
        HttpContext context,
        AgentExecutionOperationId operationId,
        ISequencedStreamReader<AgentExecutionActivity> reader,
        SequencedStreamReadResult<AgentExecutionActivity> current,
        TimeSpan heartbeatInterval)
    {
        var lastSequence = 0L;
        while (true)
        {
            switch (current)
            {
                case SequencedStreamEvents<AgentExecutionActivity> events:
                    foreach (var item in events.Items)
                    {
                        lastSequence = item.Sequence.Value;
                        await ServerSentEventResponseWriter.WriteEventAsync(
                            context.Response,
                            item.Sequence.Value,
                            ResolveEventName(item.Event),
                            new AgentActivityApiEvent(operationId, item.Event),
                            context.RequestAborted);
                    }

                    if (events.Items[^1].Event.IsTerminal)
                    {
                        return lastSequence;
                    }

                    break;

                case SequencedStreamGap<AgentExecutionActivity> gap:
                    lastSequence = gap.AvailableFromInclusive.Value - 1;
                    await ServerSentEventResponseWriter.WriteEventAsync(
                        context.Response,
                        lastSequence,
                        AgentServerEventNames.ReplayGap,
                        new AgentActivityReplayGap(
                            operationId,
                            gap.RequestedFromInclusive.Value,
                            gap.AvailableFromInclusive.Value),
                        context.RequestAborted);
                    break;

                case SequencedStreamCompleted<AgentExecutionActivity> completed:
                    return Math.Max(lastSequence, completed.LastSequence.Value);

                case SequencedStreamEvicted<AgentExecutionActivity> evicted:
                    await ServerSentEventResponseWriter.WriteEventAsync(
                        context.Response,
                        lastSequence,
                        AgentServerEventNames.StreamEvicted,
                        new AgentActivityStreamEvicted(
                            operationId,
                            evicted.Reason,
                            evicted.EvictedAtUtc),
                        context.RequestAborted);
                    return lastSequence;

                case SequencedStreamUnknown<AgentExecutionActivity>:
                    return lastSequence;
            }

            current = await ReadWithHeartbeatAsync(
                context,
                reader,
                heartbeatInterval);
        }
    }

    public static async Task<SequencedStreamReadResult<AgentExecutionActivity>>
        ReadWithHeartbeatAsync(
            HttpContext context,
            ISequencedStreamReader<AgentExecutionActivity> reader,
            TimeSpan heartbeatInterval)
    {
        while (true)
        {
            using var heartbeatCancellation =
                CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
            heartbeatCancellation.CancelAfter(heartbeatInterval);
            try
            {
                return await reader.ReadAsync(heartbeatCancellation.Token);
            }
            catch (OperationCanceledException) when (
                !context.RequestAborted.IsCancellationRequested &&
                heartbeatCancellation.IsCancellationRequested)
            {
                await ServerSentEventResponseWriter.WriteHeartbeatAsync(
                    context.Response,
                    context.RequestAborted);
            }
        }
    }

    public static IReadOnlyList<AgentPendingApprovalApiEvent> CreatePendingApprovals(
        IReadOnlyList<ExecutionApprovalRecord> approvals)
    {
        var pending = new List<AgentPendingApprovalApiEvent>(approvals.Count);
        foreach (var approval in approvals)
        {
            if (approval.Status != ExecutionApprovalStatus.Pending)
            {
                continue;
            }

            pending.Add(new AgentPendingApprovalApiEvent(
                approval.ApprovalId,
                approval.ToolName,
                approval.ToolKind,
                approval.RequestedAtUtc));
        }

        return pending;
    }

    private static string ResolveEventName(AgentExecutionActivity activity)
    {
        return activity.Phase switch
        {
            AgentExecutionActivityPhase.AwaitingApproval => AgentServerEventNames.ApprovalWaiting,
            AgentExecutionActivityPhase.Completed => AgentServerEventNames.ActivityCompleted,
            AgentExecutionActivityPhase.Failed => AgentServerEventNames.ActivityFailed,
            AgentExecutionActivityPhase.Cancelled => AgentServerEventNames.ActivityCancelled,
            _ => AgentServerEventNames.Activity
        };
    }
}
