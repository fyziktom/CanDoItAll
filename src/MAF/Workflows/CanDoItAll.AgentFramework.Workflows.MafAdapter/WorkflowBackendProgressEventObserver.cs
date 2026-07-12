using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Maf;

internal sealed class WorkflowBackendProgressEventObserver(
    WorkflowRunId runId,
    WorkflowDefinition definition,
    WorkflowPreviewSimulationPlan previewSimulationPlan,
    IWorkflowPayloadPolicyService payloadPolicyService,
    WorkflowLaunchOrigin? origin,
    IWorkflowNodeExecutionProgressObserver? next) : IWorkflowNodeExecutionProgressObserver
{
    private readonly List<WorkflowEventRecord> events = [];
    private readonly List<WorkflowArtifactRecord> artifacts = [];
    private readonly Dictionary<WorkflowUsageObservationId, WorkflowUsageObservation> usageObservations = [];
    private readonly IReadOnlyDictionary<WorkflowNodeId, WorkflowNode> nodesById = definition.Graph.Nodes.ToDictionary(node => node.Id);
    private readonly HashSet<WorkflowNodeId> previewSimulationNodeIds = previewSimulationPlan.Steps
        .Select(step => step.NodeId)
        .ToHashSet();

    public IReadOnlyList<WorkflowEventRecord> Events => events;

    public IReadOnlyList<WorkflowArtifactRecord> Artifacts => artifacts;

    public IReadOnlyList<WorkflowUsageObservation> UsageObservations => usageObservations.Values.ToArray();

    public void AddArtifact(WorkflowArtifactRecord? artifact)
    {
        if (artifact is null || artifacts.Any(item => string.Equals(item.StoragePath, artifact.StoragePath, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        artifacts.Add(artifact);
    }

    public async ValueTask RecordAsync(
        WorkflowNodeExecutionProgress progress,
        CancellationToken cancellationToken = default)
    {
        CaptureUsageObservations(progress);
        var message = string.IsNullOrWhiteSpace(progress.ExecutorId?.Value)
            ? $"Workflow node '{progress.NodeId}' {progress.State.ToString().ToLowerInvariant()}."
            : $"Workflow node '{progress.NodeId}' {progress.State.ToString().ToLowerInvariant()} for executor '{progress.ExecutorId}'.";
        var payloadResult = await ApplyPayloadPolicyAsync(progress, cancellationToken);
        events.Add(new WorkflowEventRecord(
            Guid.NewGuid(),
            runId,
            MapProgressState(progress.State),
            progress.NodeId,
            message,
            WorkflowEventPayloads.Serialize(
                WorkflowEventPayloadSource.CanDoItAllProgress,
                $"WorkflowNode{progress.State}",
                progress.NodeId,
                progress.ExecutorId,
                inlineJson: payloadResult.InlinePayload,
                reference: payloadResult.Reference,
                originalInlineCharacters: payloadResult.OriginalPayloadCharacters,
                inlineTruncated: payloadResult.InlineTruncated,
                maxInlinePayloadCharacters: payloadResult.MaxInlinePayloadCharacters,
                usage: progress.Usage),
            progress.OccurredAtUtc));
        AddArtifact(payloadResult.Artifact);

        if (next is not null)
        {
            var safeProgress = progress.State switch
            {
                WorkflowNodeExecutionProgressState.Completed => progress with
                {
                    PayloadJson = payloadResult.InlinePayload
                },
                WorkflowNodeExecutionProgressState.Failed => progress with
                {
                    ErrorMessage = payloadResult.InlinePayload
                },
                _ => progress
            };
            await next.RecordAsync(safeProgress, cancellationToken);
        }
    }

    private void CaptureUsageObservations(WorkflowNodeExecutionProgress progress)
    {
        foreach (var observation in progress.UsageObservations)
        {
            var correlated = WorkflowUsageObservationCorrelator.Correlate(
                observation,
                runId,
                definition.Id,
                definition.VersionId,
                origin,
                progress.NodeId);
            if (usageObservations.TryGetValue(correlated.Id, out var stored))
            {
                if (stored != correlated)
                {
                    throw new WorkflowUsageObservationConflictException(correlated.Id);
                }

                continue;
            }

            usageObservations.Add(correlated.Id, correlated);
        }
    }

    private async ValueTask<WorkflowPayloadPolicyResult> ApplyPayloadPolicyAsync(
        WorkflowNodeExecutionProgress progress,
        CancellationToken cancellationToken)
    {
        if (progress.State == WorkflowNodeExecutionProgressState.Completed)
        {
            var isPreviewOutput = previewSimulationNodeIds.Contains(progress.NodeId);
            return await payloadPolicyService.ApplyAsync(new WorkflowPayloadPolicyRequest(
                runId,
                isPreviewOutput
                    ? WorkflowPayloadPolicyScope.PreviewSimulationOutput
                    : WorkflowPayloadPolicyScope.ExecutorOutput,
                progress.PayloadJson,
                isPreviewOutput
                    ? WorkflowArtifactKind.PreviewSimulation
                    : ResolveArtifactKind(progress.PayloadJson),
                isPreviewOutput
                    ? $"workflow-preview-output-{progress.NodeId.Value}.json"
                    : $"workflow-node-output-{progress.NodeId.Value}.json",
                LooksLikeJson(progress.PayloadJson) ? "application/json" : "text/plain",
                progress.OccurredAtUtc)
            {
                NodeId = progress.NodeId,
                CaptureArtifact = true,
                ForceArtifact = nodesById.ContainsKey(progress.NodeId)
            }, cancellationToken);
        }

        if (progress.State == WorkflowNodeExecutionProgressState.Failed)
        {
            return await payloadPolicyService.ApplyAsync(new WorkflowPayloadPolicyRequest(
                runId,
                WorkflowPayloadPolicyScope.ExecutorError,
                progress.ErrorMessage,
                WorkflowArtifactKind.Text,
                $"workflow-node-error-{progress.NodeId.Value}.txt",
                "text/plain",
                progress.OccurredAtUtc)
            {
                NodeId = progress.NodeId,
                CaptureArtifact = true
            }, cancellationToken);
        }

        return await payloadPolicyService.ApplyAsync(new WorkflowPayloadPolicyRequest(
            runId,
            WorkflowPayloadPolicyScope.EventPayload,
            string.Empty,
            WorkflowArtifactKind.Text,
            string.Empty,
            "text/plain",
            progress.OccurredAtUtc), cancellationToken);
    }

    private static WorkflowArtifactKind ResolveArtifactKind(string payload)
        => LooksLikeJson(payload)
            ? WorkflowArtifactKind.Json
            : WorkflowArtifactKind.Text;

    private static bool LooksLikeJson(string value)
    {
        var trimmed = value.AsSpan().TrimStart();
        return trimmed.Length > 0 && trimmed[0] is '{' or '[';
    }

    private static WorkflowEventKind MapProgressState(WorkflowNodeExecutionProgressState state)
        => state switch
        {
            WorkflowNodeExecutionProgressState.Started => WorkflowEventKind.ExecutorInvoked,
            WorkflowNodeExecutionProgressState.Completed => WorkflowEventKind.ExecutorCompleted,
            WorkflowNodeExecutionProgressState.Failed => WorkflowEventKind.ExecutorFailed,
            _ => WorkflowEventKind.Unknown
        };
}
