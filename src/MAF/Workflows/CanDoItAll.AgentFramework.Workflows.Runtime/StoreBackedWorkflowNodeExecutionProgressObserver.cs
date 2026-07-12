using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.AgentFramework.Core;

internal sealed class StoreBackedWorkflowNodeExecutionProgressObserver(
    WorkflowRunSnapshot run,
    IWorkflowRunStore store,
    IWorkflowUsageObservationStore? usageStore,
    IWorkflowEventSink eventSink) : IWorkflowNodeExecutionProgressObserver
{
    private const int MaxMessageCharacters = 1_000;

    public async ValueTask RecordAsync(
        WorkflowNodeExecutionProgress progress,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(progress);
        if (progress.UsageObservations.Count > 0)
        {
            if (usageStore is null)
            {
                throw new InvalidOperationException(
                    "Workflow usage observations were produced, but no usage observation store is configured.");
            }

            var correlatedObservations = progress.UsageObservations
                .Select(observation => WorkflowUsageObservationCorrelator.Correlate(
                    observation,
                    run.RunId,
                    run.WorkflowId,
                    run.VersionId,
                    run.Origin,
                    progress.NodeId))
                .ToArray();
            await usageStore.AppendRangeAsync(correlatedObservations, cancellationToken);
        }

        var eventKind = progress.State switch
        {
            WorkflowNodeExecutionProgressState.Started => WorkflowEventKind.ExecutorInvoked,
            WorkflowNodeExecutionProgressState.Completed => WorkflowEventKind.ExecutorCompleted,
            WorkflowNodeExecutionProgressState.Failed => WorkflowEventKind.ExecutorFailed,
            _ => WorkflowEventKind.Unknown
        };
        var message = CreateSafeMessage(progress);
        var inlinePayload = progress.State switch
        {
            WorkflowNodeExecutionProgressState.Completed => progress.PayloadJson,
            WorkflowNodeExecutionProgressState.Failed => progress.ErrorMessage,
            _ => string.Empty
        };
        var workflowEvent = new WorkflowEventRecord(
            Guid.NewGuid(),
            run.RunId,
            eventKind,
            progress.NodeId,
            message,
            WorkflowEventPayloads.Serialize(
                WorkflowEventPayloadSource.CanDoItAllProgress,
                $"WorkflowNode{progress.State}",
                progress.NodeId,
                progress.ExecutorId,
                inlineJson: inlinePayload,
                usage: progress.Usage),
            progress.OccurredAtUtc);

        await store.SaveEventAsync(workflowEvent, cancellationToken);
        await eventSink.PublishAsync(workflowEvent, cancellationToken);
    }

    private static string CreateSafeMessage(WorkflowNodeExecutionProgress progress)
    {
        var message = progress.State == WorkflowNodeExecutionProgressState.Failed &&
                      !string.IsNullOrWhiteSpace(progress.ErrorMessage)
            ? $"Workflow node '{progress.NodeId}' failed. {WorkflowExecutorRedaction.RedactText(progress.ErrorMessage)}"
            : string.IsNullOrWhiteSpace(progress.ExecutorId?.Value)
                ? $"Workflow node '{progress.NodeId}' {progress.State.ToString().ToLowerInvariant()}."
                : $"Workflow node '{progress.NodeId}' {progress.State.ToString().ToLowerInvariant()} for executor '{progress.ExecutorId}'.";
        return message.Length <= MaxMessageCharacters
            ? message
            : message[..MaxMessageCharacters];
    }
}
