using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Web.Api.Streaming;

namespace CanDoItAll.Web.Api;

public enum WorkflowApiEventCategory
{
    Started,
    Progress,
    NeedsAttention,
    Completed,
    Failed,
    Cancelled
}

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
        var workflows = group.MapGroup("/workflows")
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
                (
                    Guid runId,
                    HttpContext context,
                    ProfileBoundedReplayEventStream<WorkflowApiRunEvent> eventStream) =>
                    StreamRunAsync(runId, context, eventStream))
            .WithName("StreamWorkflowRunEventsByRun")
            .Produces<string>(
                StatusCodes.Status200OK,
                contentType: ServerSentEventResponseWriter.ContentType)
            .ProducesApiErrors(StatusCodes.Status400BadRequest);

        return group;
    }

    private static async Task StreamRunAsync(
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

    private static Task StreamAllAsync(
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
