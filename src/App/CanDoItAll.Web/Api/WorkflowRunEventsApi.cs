using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Web.Api.Streaming;

namespace CanDoItAll.Web.Api;

/// <summary>
/// Coarse category of a workflow run notification in the server-sent event streams, written as a camel-case token:
/// <c>started</c> (the run started), <c>progress</c> (any other step, output or bookkeeping event),
/// <c>needsAttention</c> (the run waits for input or reported a warning), <c>completed</c> (the run completed),
/// <c>failed</c> (a node or the run failed) or <c>cancelled</c> (the run was cancelled).
/// </summary>
public enum WorkflowApiEventCategory
{
    Started,
    Progress,
    NeedsAttention,
    Completed,
    Failed,
    Cancelled
}

/// <summary>
/// One workflow run notification, sent as the <c>data</c> of a <c>workflow.run.changed</c> server-sent event. It
/// says that a run changed; it carries no message or payload. Enum members are camel-case tokens in this stream.
/// </summary>
/// <param name="EventId">
/// Identifier of the recorded run event, the same value as <c>id</c> in <c>GET /api/workflows/runs/{runId}/events</c>.
/// </param>
/// <param name="RunId">Identifier of the run that changed, as a GUID string.</param>
/// <param name="Category">
/// Coarse category: <c>started</c>, <c>progress</c>, <c>needsAttention</c>, <c>completed</c>, <c>failed</c> or
/// <c>cancelled</c>.
/// </param>
/// <param name="Kind">
/// The recorded event kind as a camel-case token, for example <c>started</c>, <c>executorInvoked</c>,
/// <c>executorCompleted</c>, <c>executorFailed</c>, <c>output</c>, <c>warning</c>, <c>error</c>,
/// <c>waitingForInput</c>, <c>completed</c> or <c>cancelled</c>.
/// </param>
/// <param name="NodeId">Identifier of the definition node the event belongs to, or null for a run-level event.</param>
/// <param name="OccurredAtUtc">Time the event was recorded, as an ISO 8601 instant with offset.</param>
/// <param name="IsTerminal">
/// True when the run completed or was cancelled, or failed as a whole (a run-level error); no further notifications
/// are expected for the run. A node failure alone is not terminal.
/// </param>
/// <param name="NeedsAttention">
/// True for events in the <c>needsAttention</c> and <c>failed</c> categories, such as a run waiting for an external
/// response.
/// </param>
public sealed record WorkflowApiRunEvent(
    Guid EventId,
    WorkflowRunId RunId,
    WorkflowApiEventCategory Category,
    WorkflowEventKind Kind,
    WorkflowNodeId? NodeId,
    DateTimeOffset OccurredAtUtc,
    bool IsTerminal,
    bool NeedsAttention);

internal sealed class WorkflowApiEventSink(
    ProfileBoundedReplayEventStream<WorkflowApiRunEvent> eventStream,
    ILogger<WorkflowApiEventSink> logger) : IWorkflowEventSink
{
    public Task PublishAsync(
        WorkflowEventRecord workflowEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workflowEvent);

        try
        {
            eventStream.Publish(Map(workflowEvent));
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Failed to publish workflow API event {WorkflowEventId} for run {WorkflowRunId}.",
                workflowEvent.Id,
                workflowEvent.RunId);
        }

        return Task.CompletedTask;
    }

    internal static WorkflowApiRunEvent Map(WorkflowEventRecord workflowEvent)
    {
        var category = workflowEvent.Kind switch
        {
            WorkflowEventKind.Started => WorkflowApiEventCategory.Started,
            WorkflowEventKind.Completed => WorkflowApiEventCategory.Completed,
            WorkflowEventKind.Cancelled => WorkflowApiEventCategory.Cancelled,
            WorkflowEventKind.Error or WorkflowEventKind.ExecutorFailed => WorkflowApiEventCategory.Failed,
            WorkflowEventKind.WaitingForInput or WorkflowEventKind.Warning => WorkflowApiEventCategory.NeedsAttention,
            _ => WorkflowApiEventCategory.Progress
        };

        return new WorkflowApiRunEvent(
            workflowEvent.Id,
            workflowEvent.RunId,
            category,
            workflowEvent.Kind,
            workflowEvent.NodeId,
            workflowEvent.CreatedAtUtc,
            category is WorkflowApiEventCategory.Completed or WorkflowApiEventCategory.Cancelled ||
            workflowEvent.Kind == WorkflowEventKind.Error && workflowEvent.NodeId is null,
            category is WorkflowApiEventCategory.NeedsAttention or WorkflowApiEventCategory.Failed);
    }
}

internal static class WorkflowRunEventsApi
{
    public const string EventName = "workflow.run.changed";
    private const string InvalidRunIdCode = "workflows.run-id-invalid";

    public static RouteGroupBuilder MapWorkflowRunEventsApi(this RouteGroupBuilder group)
    {
        var workflows = group.MapGroup("/workflows").WithApiSection(ApiAccessScopeNames.ReadWorkflows, ApiAccessScopeNames.WriteWorkflows)
            .WithTags("Workflows")
            .DisableAntiforgery();

        workflows.MapGet(
                "/events/stream",
                StreamAllAsync)
            .WithName("StreamWorkflowRunEvents")
            .Produces<string>(
                StatusCodes.Status200OK,
                contentType: ServerSentEventResponseWriter.ContentType)
            .ProducesApiErrors(StatusCodes.Status400BadRequest);

        workflows.MapGet(
                "/runs/{runId:guid}/events/stream",
                StreamRunAsync)
            .WithName("StreamWorkflowRunEventsByRun")
            .Produces<string>(
                StatusCodes.Status200OK,
                contentType: ServerSentEventResponseWriter.ContentType)
            .ProducesApiErrors(StatusCodes.Status400BadRequest);

        return group;
    }

    /// <summary>
    /// Subscribe to change notifications of one workflow run as server-sent events.
    /// </summary>
    /// <remarks>
    /// Opens a <c>text/event-stream</c> response that stays open and sends a notification each time an event is
    /// recorded for this run. A notification tells what changed, not the details: read
    /// <c>GET /api/workflows/runs/{runId}</c> or the run's events when you need them. The stream does not end when the
    /// run finishes; close it after a notification whose <c>isTerminal</c> is true. An unknown run identifier is
    /// accepted, and the stream then sends only heartbeats and gap notices.
    ///
    /// Frames are UTF-8 text separated by a blank line:
    ///
    /// - A notification: <c>id: {position}</c>, <c>event: workflow.run.changed</c> and <c>data:</c> with one JSON
    /// object: <c>eventId</c> (the event's <c>id</c> in the run event lists), <c>runId</c>, <c>category</c>
    /// (<c>started</c>, <c>progress</c>, <c>needsAttention</c>, <c>completed</c>, <c>failed</c> or
    /// <c>cancelled</c>), <c>kind</c> (the event kind as a camel-case token, for example <c>executorCompleted</c> or
    /// <c>waitingForInput</c>), <c>nodeId</c> (null for run-level events), <c>occurredAtUtc</c>,
    /// <c>isTerminal</c> (the run completed, was cancelled or failed as a whole) and <c>needsAttention</c> (input is
    /// awaited, or a warning or failure occurred).
    /// - A gap notice: <c>event: stream.gap</c> when notifications before the requested position were discarded or
    /// the position lies ahead of the stream. Its <c>data</c> has <c>reason</c> (<c>cursorBeforeRetention</c> or
    /// <c>cursorAheadOfStream</c>), <c>requestedAfterSequence</c>, <c>firstAvailableSequence</c>,
    /// <c>lastAvailableSequence</c>, <c>resumeAfterSequence</c> and <c>snapshotUrl</c> (null here); its <c>id</c> is
    /// the position the stream continues from. Changes in the gap were missed, so re-read the run.
    /// - A heartbeat comment, <c>: heartbeat {time}</c>, every 15 seconds by default while nothing else is sent.
    ///
    /// Positions (<c>id</c>) belong to the host-wide notification stream, so they are increasing but not consecutive
    /// for one run. To resume after a disconnect, send the last received position as the <c>Last-Event-ID</c> header
    /// (browsers do this automatically) or as the <c>after</c> query parameter; if both are sent they must be equal.
    /// Without either, the stream first repeats the retained notifications of the run (from the last 1,024
    /// notifications of the host by default) and then continues live. Notifications are held only in memory: a host
    /// restart starts the positions over, and a change of the active database profile closes open streams and
    /// discards the retained notifications. The durable record is <c>GET /api/workflows/runs/{runId}/events</c>.
    ///
    /// Authority: when API authorization is enabled, a valid bearer token satisfying this operation's capability policy; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="runId">
    /// Identifier of the run to follow, as returned in <c>run.runId</c> by a start operation. It must not be the
    /// empty GUID.
    /// </param>
    /// <response code="200">
    /// The event stream (<c>text/event-stream; charset=utf-8</c>). It stays open until the client disconnects or the
    /// host closes it.
    /// </response>
    /// <response code="400">
    /// The run identifier is the empty GUID (<c>workflows.run-id-invalid</c>), or the <c>after</c> query parameter or
    /// <c>Last-Event-ID</c> header is not a single non-negative integer or the two differ (<c>sse.cursor-invalid</c>).
    /// No stream is opened.
    /// </response>
    internal static async Task StreamRunAsync(
        Guid runId,
        HttpContext context,
        ProfileBoundedReplayEventStream<WorkflowApiRunEvent> eventStream)
    {
        if (runId == Guid.Empty)
        {
            await ApiEndpointResults
                .BadRequest("Workflow run id cannot be empty.", InvalidRunIdCode)
                .ExecuteAsync(context);
            return;
        }

        var workflowRunId = new WorkflowRunId(runId);
        var lease = eventStream.OpenCurrent();
        await ServerSentEventResponseWriter.WriteAsync(
            context,
            lease.Reader,
            EventName,
            workflowEvent => workflowEvent.RunId == workflowRunId,
            lease.ProfileLifetime);
    }

    /// <summary>
    /// Subscribe to change notifications of all workflow runs as server-sent events.
    /// </summary>
    /// <remarks>
    /// Opens a <c>text/event-stream</c> response that stays open and sends a notification each time an event is
    /// recorded for any workflow run of this host. A notification tells which run changed and how, not the details:
    /// read the run or its events when you need them. Use it to keep a run list or dashboard current; follow a single
    /// run with <c>GET /api/workflows/runs/{runId}/events/stream</c>.
    ///
    /// Frames are UTF-8 text separated by a blank line:
    ///
    /// - A notification: <c>id: {position}</c>, <c>event: workflow.run.changed</c> and <c>data:</c> with one JSON
    /// object: <c>eventId</c> (the event's <c>id</c> in the run event lists), <c>runId</c>, <c>category</c>
    /// (<c>started</c>, <c>progress</c>, <c>needsAttention</c>, <c>completed</c>, <c>failed</c> or
    /// <c>cancelled</c>), <c>kind</c> (the event kind as a camel-case token, for example <c>executorCompleted</c> or
    /// <c>waitingForInput</c>), <c>nodeId</c> (null for run-level events), <c>occurredAtUtc</c>,
    /// <c>isTerminal</c> (the run completed, was cancelled or failed as a whole) and <c>needsAttention</c> (input is
    /// awaited, or a warning or failure occurred).
    /// - A gap notice: <c>event: stream.gap</c> when notifications before the requested position were discarded or
    /// the position lies ahead of the stream. Its <c>data</c> has <c>reason</c> (<c>cursorBeforeRetention</c> or
    /// <c>cursorAheadOfStream</c>), <c>requestedAfterSequence</c>, <c>firstAvailableSequence</c>,
    /// <c>lastAvailableSequence</c>, <c>resumeAfterSequence</c> and <c>snapshotUrl</c> (null here); its <c>id</c> is
    /// the position the stream continues from. Changes in the gap were missed, so re-read the runs you follow.
    /// - A heartbeat comment, <c>: heartbeat {time}</c>, every 15 seconds by default while nothing else is sent.
    ///
    /// To resume after a disconnect, send the last received position as the <c>Last-Event-ID</c> header (browsers do
    /// this automatically) or as the <c>after</c> query parameter; if both are sent they must be equal. Without either,
    /// the stream first repeats the retained notifications (the last 1,024 by default) and then continues live.
    /// Notifications are held only in memory: a host restart starts the positions over, and a change of the active
    /// database profile closes open streams and discards the retained notifications. The durable record is
    /// <c>GET /api/workflows/runs/{runId}/events</c>.
    ///
    /// Authority: when API authorization is enabled, a valid bearer token satisfying this operation's capability policy; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <response code="200">
    /// The event stream (<c>text/event-stream; charset=utf-8</c>). It stays open until the client disconnects or the
    /// host closes it.
    /// </response>
    /// <response code="400">
    /// The <c>after</c> query parameter or <c>Last-Event-ID</c> header is not a single non-negative integer, or the two
    /// differ (<c>sse.cursor-invalid</c>). No stream is opened.
    /// </response>
    internal static Task StreamAllAsync(
        HttpContext context,
        ProfileBoundedReplayEventStream<WorkflowApiRunEvent> eventStream)
    {
        var lease = eventStream.OpenCurrent();
        return ServerSentEventResponseWriter.WriteAsync(
            context,
            lease.Reader,
            EventName,
            static _ => true,
            lease.ProfileLifetime);
    }
}
