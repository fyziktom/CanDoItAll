using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Web.Api.Streaming;

namespace CanDoItAll.Web.Api;

public enum ProcessApiEventCategory
{
    Started,
    Progress,
    NeedsAttention,
    Completed,
    Failed,
    Cancelled
}

public enum ProcessApiEventSensitivity
{
    Normal,
    Restricted
}

public sealed record ProcessApiRunEvent(
    RuntimeEventId EventId,
    long GlobalSequence,
    long RootSequence,
    ProcessRunId RootRunId,
    ProcessRunId RunId,
    ProcessApiEventCategory Category,
    string EventType,
    ProcessApiEventSensitivity Sensitivity,
    DateTimeOffset OccurredAtUtc,
    bool IsTerminal,
    bool NeedsAttention);

internal sealed class ApiNotifyingProcessRuntimeProjector(
    IProcessRuntimeProjector inner,
    ProfileBoundedReplayEventStream<ProcessApiRunEvent> eventStream,
    ILogger<ApiNotifyingProcessRuntimeProjector> logger) : IProcessRuntimeProjector
{
    public ProcessProjectorName ProjectorName => inner.ProjectorName;

    public async Task ProjectAsync(
        ProcessStoredRuntimeEvent runtimeEvent,
        ProcessProjectionExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(runtimeEvent);
        ArgumentNullException.ThrowIfNull(context);

        await inner.ProjectAsync(runtimeEvent, context, cancellationToken).ConfigureAwait(false);

        try
        {
            eventStream.Publish(Map(runtimeEvent));
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Failed to publish process API event {ProcessEventId} at global sequence {GlobalSequence}.",
                runtimeEvent.Envelope.EventId,
                runtimeEvent.GlobalSequence);
        }
    }

    internal static ProcessApiRunEvent Map(ProcessStoredRuntimeEvent runtimeEvent)
    {
        var category = ResolveCategory(runtimeEvent.Envelope.EventType);
        var restricted = runtimeEvent.Envelope.Sensitivity == ProcessEventSensitivity.Restricted;

        return new ProcessApiRunEvent(
            runtimeEvent.Envelope.EventId,
            runtimeEvent.GlobalSequence,
            runtimeEvent.RootSequence,
            runtimeEvent.Envelope.RootRunId,
            runtimeEvent.Envelope.RunId,
            category,
            restricted ? ProcessApiEventTypeNames.Restricted : runtimeEvent.Envelope.EventType.Value,
            restricted ? ProcessApiEventSensitivity.Restricted : ProcessApiEventSensitivity.Normal,
            runtimeEvent.Envelope.OccurredAtUtc,
            category is ProcessApiEventCategory.Completed
                or ProcessApiEventCategory.Failed
                or ProcessApiEventCategory.Cancelled,
            category is ProcessApiEventCategory.NeedsAttention or ProcessApiEventCategory.Failed);
    }

    private static ProcessApiEventCategory ResolveCategory(ProcessEventType eventType)
    {
        if (eventType == ProcessRuntimeEventTypes.ProcessRunCreated ||
            eventType == ProcessRuntimeEventTypes.ProcessRunActivated ||
            eventType == ProcessRuntimeEventTypes.ProcessRunReactivated)
        {
            return ProcessApiEventCategory.Started;
        }

        return eventType.Value switch
        {
            ProcessRuntimeProjectionEventTypeNames.ProcessRunCompleted => ProcessApiEventCategory.Completed,
            ProcessRuntimeProjectionEventTypeNames.ProcessRunFailed => ProcessApiEventCategory.Failed,
            ProcessRuntimeProjectionEventTypeNames.ProcessRunCancelled => ProcessApiEventCategory.Cancelled,
            ProcessRuntimeProjectionEventTypeNames.ProcessRunBlocked
                or ProcessRuntimeProjectionEventTypeNames.StepBlocked
                or ProcessRuntimeProjectionEventTypeNames.StepReworkRequested
                or ProcessRuntimeProjectionEventTypeNames.ManagerIncidentRaised
                or ProcessRuntimeProjectionEventTypeNames.ManagerLoopBudgetEscalated
                or ProcessRuntimeProjectionEventTypeNames.ManagerRecoveryDenied
                or ProcessRuntimeProjectionEventTypeNames.ManagerBranchDecisionRejected
                => ProcessApiEventCategory.NeedsAttention,
            _ => ProcessApiEventCategory.Progress
        };
    }
}

public static class ProcessApiEventTypeNames
{
    public const string Restricted = "RestrictedRuntimeEvent";
}

internal static class ProcessRunEventsApi
{
    public const string EventName = "process.run.changed";
    private const string InvalidRunIdCode = "processes.run-id-invalid";

    public static RouteGroupBuilder MapProcessRunEventsApi(this RouteGroupBuilder group)
    {
        var processes = group.MapGroup("/processes")
            .WithTags("Processes")
            .DisableAntiforgery();

        processes.MapGet(
                "/events/stream",
                StreamAllAsync)
            .WithName("StreamProcessRunEvents")
            .Produces<string>(
                StatusCodes.Status200OK,
                contentType: ServerSentEventResponseWriter.ContentType)
            .ProducesApiErrors(StatusCodes.Status400BadRequest);

        processes.MapGet(
                "/runs/{runId:guid}/events/stream",
                StreamRunAsync)
            .WithName("StreamProcessRunEventsByRun")
            .Produces<string>(
                StatusCodes.Status200OK,
                contentType: ServerSentEventResponseWriter.ContentType)
            .ProducesApiErrors(StatusCodes.Status400BadRequest);

        return group;
    }

    /// <summary>
    /// Stream lifecycle change signals of one process run as server-sent events.
    /// </summary>
    /// <remarks>
    /// Same stream as <c>GET /api/processes/events/stream</c>, filtered to events whose <c>runId</c> equals the route
    /// value. Events of child runs (subprocesses) are not included: a child run shares the parent's
    /// <c>rootRunId</c> but has its own <c>runId</c>, so subscribe to it separately or use the all-runs stream.
    ///
    /// The run is not checked for existence. An unknown identifier gets an open stream with heartbeats and possibly
    /// <c>stream.gap</c> frames, but never a <c>process.run.changed</c> event. Frame <c>id</c> values of this stream
    /// have gaps, because events of other runs are skipped; resume with the last <c>id</c> you received.
    ///
    /// Frames, the <c>data</c> object, resuming with <c>Last-Event-ID</c> or <c>after</c>, delivery guarantees and
    /// the recommended read-back are exactly as described for <c>GET /api/processes/events/stream</c>. A terminal
    /// event (<c>isTerminal</c> true) does not close this stream; close it yourself when you no longer need it.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; otherwise the route
    /// is open.
    /// </remarks>
    /// <param name="runId">
    /// Identifier of the process run to observe, for example <c>runId</c> from <c>POST /api/processes/launch</c> or
    /// from <c>GET /api/processes/live</c>. The all-zero GUID is rejected.
    /// </param>
    /// <response code="200">
    /// The open event stream (<c>text/event-stream; charset=utf-8</c>). It stays open until the client disconnects or
    /// the server ends it.
    /// </response>
    /// <response code="400">
    /// Rejected before the stream starts, as a JSON error envelope: <c>processes.run-id-invalid</c> (the all-zero
    /// GUID) or <c>sse.cursor-invalid</c> (<c>Last-Event-ID</c> or <c>after</c> is repeated, is not a non-negative
    /// integer, or the two values differ).
    /// </response>
    internal static async Task StreamRunAsync(
        Guid runId,
        HttpContext context,
        ProfileBoundedReplayEventStream<ProcessApiRunEvent> eventStream)
    {
        if (runId == Guid.Empty)
        {
            await ApiEndpointResults
                .BadRequest("Process run id cannot be empty.", InvalidRunIdCode)
                .ExecuteAsync(context);
            return;
        }

        var processRunId = new ProcessRunId(runId);
        var lease = eventStream.OpenCurrent();
        await ServerSentEventResponseWriter.WriteAsync(
            context,
            lease.Reader,
            EventName,
            processEvent => processEvent.RunId == processRunId,
            lease.ProfileLifetime);
    }

    /// <summary>
    /// Stream lifecycle change signals of all process runs as server-sent events.
    /// </summary>
    /// <remarks>
    /// Opens a long-lived <c>text/event-stream</c> response. Each time this server has projected a runtime event of any
    /// process run into its read models, it sends one <c>process.run.changed</c> event. The event is a signal, not
    /// the state: after receiving one, read the current state with <c>GET /api/processes/runs/{runId}</c> (live
    /// detail), <c>GET /api/processes/live</c> or <c>GET /api/processes/runs/{runId}/history</c>. Use
    /// <c>GET /api/processes/runs/{runId}/events/stream</c> to receive the events of a single run only.
    ///
    /// Frames (LF line endings; each JSON object on a single <c>data:</c> line):
    ///
    /// - A change signal: <c>id: 1287</c>, <c>event: process.run.changed</c>, <c>data: {JSON object}</c>. The
    /// <c>id</c> is a sequence number of this stream in this server process, not the event's
    /// <c>globalSequence</c>. It increases while the process runs and starts again at 1 after a restart.
    /// - A gap notice: <c>event: stream.gap</c> with <c>data</c> members <c>reason</c>, <c>requestedAfterSequence</c>,
    /// <c>firstAvailableSequence</c>, <c>lastAvailableSequence</c>, <c>resumeAfterSequence</c> and
    /// <c>snapshotUrl</c> (always null here). <c>cursorBeforeRetention</c> means that older events were dropped and
    /// replay continues with the oldest retained one; <c>cursorAheadOfStream</c> (for example after a server
    /// restart) means that only new events follow. After a gap, read the state again instead of relying on the
    /// missed events.
    /// - A heartbeat comment, <c>: heartbeat</c> followed by the current UTC instant, while no event is pending
    /// (every 15 seconds by default; host setting <c>Api:ServerSentEvents:HeartbeatIntervalSeconds</c>).
    ///
    /// The <c>data</c> of <c>process.run.changed</c> is a camel-case JSON object with these members:
    ///
    /// - <c>eventId</c>, <c>rootRunId</c>, <c>runId</c>: objects of the form <c>{"value":"GUID"}</c>, not plain
    /// strings. <c>rootRunId</c> is the top-level run of a subprocess tree and equals <c>runId</c> for a top-level
    /// run.
    /// - <c>globalSequence</c>: position of the runtime event in the durable event log across all runs.
    /// <c>rootSequence</c>: 1-based position of the event within its root run.
    /// - <c>category</c>: <c>started</c> (run created, activated or reactivated), <c>completed</c>, <c>failed</c>,
    /// <c>cancelled</c> (the run reached that terminal state), <c>needsAttention</c> (the run or a step is blocked,
    /// step rework was requested, or the process manager raised an incident, escalated a loop budget, denied a
    /// recovery or rejected a branch decision) or <c>progress</c> (every other event, including a failed step attempt
    /// and a cancellation request).
    /// - <c>eventType</c>: the runtime event type name, for example <c>ProcessRunCompleted</c>, or
    /// <c>RestrictedRuntimeEvent</c> when the event is restricted. <c>sensitivity</c>: <c>normal</c> or
    /// <c>restricted</c>.
    /// - <c>occurredAtUtc</c>: instant of the runtime event. <c>isTerminal</c>: true for completed, failed and
    /// cancelled. <c>needsAttention</c>: true for needsAttention and failed.
    ///
    /// Example <c>data</c>: <c>{"eventId":{"value":"6f1d2c3b-8a4e-4f0b-9c55-2b7e1a9d4c10"},"globalSequence":90412,
    /// "rootSequence":37,"rootRunId":{"value":"0b8e6f3a-5d2c-4e71-a1f9-7c3d2e4b5a60"},
    /// "runId":{"value":"0b8e6f3a-5d2c-4e71-a1f9-7c3d2e4b5a60"},"category":"completed",
    /// "eventType":"ProcessRunCompleted","sensitivity":"normal","occurredAtUtc":"2026-09-19T08:15:30.1234567+00:00",
    /// "isTerminal":true,"needsAttention":false}</c>
    ///
    /// Resuming: send the last received <c>id</c> in the <c>Last-Event-ID</c> header (browser <c>EventSource</c>
    /// clients do this when they reconnect) or in the <c>after</c> query parameter; if both are sent they must be
    /// equal. Retained events with a larger <c>id</c> are replayed first, then live events follow. Without a cursor,
    /// every retained event is replayed first. The server retains the most recent events of all runs together (1,024
    /// by default; host setting <c>Api:ServerSentEvents:ReplayCapacity</c>).
    ///
    /// Delivery is best effort. Events are kept only in the memory of this server process; an event whose publication
    /// fails is skipped; an event can occasionally arrive twice (de-duplicate on <c>eventId</c>); a switch of the
    /// active database profile ends the response, so reconnect. The stream does not end by itself, not even after a
    /// terminal event.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host, and the stream then
    /// shows every run to that caller; otherwise the route is open.
    /// </remarks>
    /// <response code="200">
    /// The open event stream (<c>text/event-stream; charset=utf-8</c>, not cached). It stays open until the client
    /// disconnects or the server ends it.
    /// </response>
    /// <response code="400">
    /// The cursor is invalid (<c>sse.cursor-invalid</c>): <c>Last-Event-ID</c> or <c>after</c> is repeated, is not a
    /// non-negative integer, or the two values differ. Returned as a JSON error envelope before the stream starts.
    /// </response>
    internal static Task StreamAllAsync(
        HttpContext context,
        ProfileBoundedReplayEventStream<ProcessApiRunEvent> eventStream)
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
