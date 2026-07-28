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
                (
                    Guid runId,
                    HttpContext context,
                    ProfileBoundedReplayEventStream<ProcessApiRunEvent> eventStream) =>
                    StreamRunAsync(runId, context, eventStream))
            .WithName("StreamProcessRunEventsByRun")
            .Produces<string>(
                StatusCodes.Status200OK,
                contentType: ServerSentEventResponseWriter.ContentType)
            .ProducesApiErrors(StatusCodes.Status400BadRequest);

        return group;
    }

    private static async Task StreamRunAsync(
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

    private static Task StreamAllAsync(
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
