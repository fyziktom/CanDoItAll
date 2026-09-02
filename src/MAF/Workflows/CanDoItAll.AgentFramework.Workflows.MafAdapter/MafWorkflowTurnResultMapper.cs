using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using Microsoft.Agents.AI.Workflows;

namespace CanDoItAll.AgentFramework.Maf;

internal sealed class MafWorkflowTurnResultMapper(
    IWorkflowBackendCheckpointPayloadStore checkpointPayloadStore,
    MafWorkflowExternalRequestMapper requestMapper,
    IMafWorkflowEventNormalizer eventNormalizer,
    IWorkflowCheckpointFactory checkpointFactory,
    IWorkflowPayloadPolicyService payloadPolicyService,
    TimeProvider timeProvider)
{
    private readonly IWorkflowBackendCheckpointPayloadStore payloadStore = checkpointPayloadStore ?? throw new ArgumentNullException(nameof(checkpointPayloadStore));
    private readonly MafWorkflowExternalRequestMapper externalRequestMapper = requestMapper ?? throw new ArgumentNullException(nameof(requestMapper));
    private readonly IMafWorkflowEventNormalizer normalizer = eventNormalizer ?? throw new ArgumentNullException(nameof(eventNormalizer));
    private readonly IWorkflowCheckpointFactory metadataCheckpointFactory = checkpointFactory ?? throw new ArgumentNullException(nameof(checkpointFactory));
    private readonly IWorkflowPayloadPolicyService payloadPolicy = payloadPolicyService ?? throw new ArgumentNullException(nameof(payloadPolicyService));
    private readonly TimeProvider clock = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    public async Task<WorkflowBackendStartResult> MapTurnAsync(
        WorkflowDefinition definition,
        WorkflowRunId runId,
        WorkflowLaunchOrigin? origin,
        DateTimeOffset createdAtUtc,
        MafWorkflowStreamTurn turn,
        WorkflowBackendProgressEventObserver progressObserver,
        WorkflowRunStartRequest? startRequest,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(turn);
        ArgumentNullException.ThrowIfNull(progressObserver);

        var eventBindings = MafWorkflowEventBindingIndex.FromDefinition(definition);
        var events = progressObserver.Events
            .Concat(turn.Events
                .Select(workflowEvent => normalizer.Normalize(
                    runId,
                    workflowEvent,
                    eventBindings,
                    clock.GetUtcNow()))
                .Where(workflowEvent => !IsDuplicateProgressEvent(progressObserver.Events, workflowEvent)))
            .OrderBy(workflowEvent => workflowEvent.CreatedAtUtc)
            .ToList();

        if (startRequest is not null)
        {
            await AddStartedInputAsync(
                definition,
                runId,
                startRequest,
                createdAtUtc,
                events,
                progressObserver,
                cancellationToken);
        }

        if (turn.PendingRequest is not null || turn.WaitingCheckpoint is not null)
        {
            if (turn.PendingRequest is null || turn.WaitingCheckpoint is null)
            {
                throw new InvalidOperationException(
                    "MAF workflow turn did not provide a complete request/checkpoint boundary.");
            }

            return await MapWaitingTurnAsync(
                definition,
                runId,
                origin,
                createdAtUtc,
                turn,
                progressObserver,
                events,
                cancellationToken);
        }

        if (turn.Status == RunStatus.PendingRequests)
        {
            throw new InvalidOperationException(
                "MAF workflow reported pending requests without a correlated request/checkpoint boundary.");
        }

        return MapTerminalTurn(
            definition,
            runId,
            origin,
            createdAtUtc,
            turn,
            progressObserver,
            events);
    }

    public WorkflowBackendProgressEventObserver CreateProgressObserver(
        WorkflowRunId runId,
        WorkflowDefinition definition,
        WorkflowPreviewSimulationPlan previewSimulationPlan,
        WorkflowLaunchOrigin? origin)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(previewSimulationPlan);

        return new WorkflowBackendProgressEventObserver(
            runId,
            definition,
            previewSimulationPlan,
            payloadPolicy,
            origin,
            WorkflowNodeExecutionProgressScope.Current);
    }

    private async Task<WorkflowBackendStartResult> MapWaitingTurnAsync(
        WorkflowDefinition definition,
        WorkflowRunId runId,
        WorkflowLaunchOrigin? origin,
        DateTimeOffset createdAtUtc,
        MafWorkflowStreamTurn turn,
        WorkflowBackendProgressEventObserver progressObserver,
        List<WorkflowEventRecord> events,
        CancellationToken cancellationToken)
    {
        var checkpointLink = new WorkflowBackendCheckpointLink(
            new WorkflowBackendSessionId(turn.WaitingCheckpoint!.SessionId),
            new WorkflowBackendCheckpointId(turn.WaitingCheckpoint.CheckpointId));
        var checkpointRead = await payloadStore.ReadAsync(checkpointLink, cancellationToken);
        if (!checkpointRead.Succeeded || checkpointRead.Checkpoint is null)
        {
            throw new InvalidOperationException(
                $"Correlated MAF checkpoint payload could not be loaded with outcome '{checkpointRead.Outcome}'.");
        }

        var mapped = externalRequestMapper.Map(
            runId,
            origin,
            turn.PendingRequest!,
            checkpointRead.Checkpoint);
        var requestPayload = await payloadPolicy.ApplyAsync(new WorkflowPayloadPolicyRequest(
            runId,
            WorkflowPayloadPolicyScope.ExternalRequest,
            mapped.Request.RequestJson,
            WorkflowArtifactKind.Json,
            $"workflow-external-request-{mapped.Request.Id.Value:N}.json",
            "application/json",
            clock.GetUtcNow())
        {
            NodeId = mapped.Request.NodeId,
            CaptureArtifact = true
        }, cancellationToken);
        progressObserver.AddArtifact(requestPayload.Artifact);
        var summary = mapped.Checkpoint.Summary;
        events.Add(new WorkflowEventRecord(
            Guid.NewGuid(),
            runId,
            WorkflowEventKind.WaitingForInput,
            mapped.Request.NodeId,
            summary,
            WorkflowEventPayloads.Serialize(
                WorkflowEventPayloadSource.ExternalRequest,
                "WorkflowExternalRequest",
                mapped.Request.NodeId,
                requestId: mapped.Request.Id,
                requestKind: mapped.Request.Kind,
                inlineJson: requestPayload.InlinePayload,
                reference: requestPayload.Reference,
                originalInlineCharacters: requestPayload.OriginalPayloadCharacters,
                inlineTruncated: requestPayload.InlineTruncated,
                maxInlinePayloadCharacters: requestPayload.MaxInlinePayloadCharacters),
            clock.GetUtcNow()));
        var now = clock.GetUtcNow();
        var run = new WorkflowRunSnapshot(
            runId,
            definition.Id,
            definition.VersionId,
            WorkflowRunState.WaitingForInput,
            WorkflowRuntimeBackendKind.InProcess,
            turn.SessionId,
            summary,
            createdAtUtc,
            now)
        {
            Origin = origin
        };

        return new WorkflowBackendStartResult(
            run,
            events.OrderBy(workflowEvent => workflowEvent.CreatedAtUtc).ToArray(),
            [mapped.Request],
            progressObserver.Artifacts)
        {
            Checkpoints = [mapped.Checkpoint],
            UsageObservations = progressObserver.UsageObservations
        };
    }

    private WorkflowBackendStartResult MapTerminalTurn(
        WorkflowDefinition definition,
        WorkflowRunId runId,
        WorkflowLaunchOrigin? origin,
        DateTimeOffset createdAtUtc,
        MafWorkflowStreamTurn turn,
        WorkflowBackendProgressEventObserver progressObserver,
        List<WorkflowEventRecord> events)
    {
        var state = MafWorkflowStatusMapper.MapRunStatus(turn.Status);
        if (state == WorkflowRunState.Idle)
        {
            state = WorkflowRunState.Completed;
        }

        var failureEvent = events.LastOrDefault(workflowEvent =>
            workflowEvent.Kind is WorkflowEventKind.Error or WorkflowEventKind.ExecutorFailed);
        if (failureEvent is not null)
        {
            state = WorkflowRunState.Failed;
        }

        var now = clock.GetUtcNow();
        var summary = failureEvent is not null
            ? WorkflowFailureDisplayFormatter.ToUserMessage(failureEvent)
            : state == WorkflowRunState.Completed
                ? $"Workflow '{definition.Name}' completed."
                : $"Workflow '{definition.Name}' is {state}.";
        if (state == WorkflowRunState.Completed)
        {
            events.Add(new WorkflowEventRecord(
                Guid.NewGuid(),
                runId,
                WorkflowEventKind.Completed,
                NodeId: null,
                summary,
                WorkflowEventPayloads.Serialize(
                    WorkflowEventPayloadSource.Runtime,
                    "WorkflowCompleted"),
                now));
        }

        var run = new WorkflowRunSnapshot(
            runId,
            definition.Id,
            definition.VersionId,
            state,
            WorkflowRuntimeBackendKind.InProcess,
            turn.SessionId,
            summary,
            createdAtUtc,
            now)
        {
            TerminalAtUtc = IsTerminal(state) ? now : null,
            Origin = origin
        };
        var artifacts = MergeArtifacts(
            progressObserver.Artifacts,
            state == WorkflowRunState.Completed
                ? MafConfiguredFileArtifactResolver.BuildConfiguredFileArtifacts(definition, runId, now)
                : []);
        var checkpoint = metadataCheckpointFactory.CreateMetadataCheckpoint(
            new WorkflowCheckpointCreateRequest(
                definition,
                runId,
                WorkflowRuntimeBackendKind.InProcess,
                MapCheckpointKind(state),
                now)
            {
                Summary = summary
            });

        return new WorkflowBackendStartResult(run, events, [], artifacts)
        {
            Checkpoints = [checkpoint],
            UsageObservations = progressObserver.UsageObservations
        };
    }

    private async Task AddStartedInputAsync(
        WorkflowDefinition definition,
        WorkflowRunId runId,
        WorkflowRunStartRequest request,
        DateTimeOffset createdAtUtc,
        List<WorkflowEventRecord> events,
        WorkflowBackendProgressEventObserver progressObserver,
        CancellationToken cancellationToken)
    {
        var inputPayload = await payloadPolicy.ApplyAsync(new WorkflowPayloadPolicyRequest(
            runId,
            WorkflowPayloadPolicyScope.RunInput,
            request.InputJson,
            WorkflowArtifactKind.Json,
            "workflow-input.json",
            "application/json",
            createdAtUtc)
        {
            CaptureArtifact = true
        }, cancellationToken);
        var startedEvent = new WorkflowEventRecord(
            Guid.NewGuid(),
            runId,
            WorkflowEventKind.Started,
            NodeId: null,
            $"Workflow '{definition.Name}' started.",
            WorkflowEventPayloads.Serialize(
                WorkflowEventPayloadSource.Runtime,
                "WorkflowStarted",
                inlineJson: inputPayload.InlinePayload,
                reference: inputPayload.Reference,
                originalInlineCharacters: inputPayload.OriginalPayloadCharacters,
                inlineTruncated: inputPayload.InlineTruncated,
                maxInlinePayloadCharacters: inputPayload.MaxInlinePayloadCharacters),
            createdAtUtc);
        var existingIndex = events.FindIndex(workflowEvent => workflowEvent.Kind == WorkflowEventKind.Started);
        if (existingIndex >= 0)
        {
            events[existingIndex] = startedEvent;
        }
        else
        {
            events.Insert(0, startedEvent);
        }

        progressObserver.AddArtifact(inputPayload.Artifact);
    }

    private static bool IsDuplicateProgressEvent(
        IReadOnlyList<WorkflowEventRecord> progressEvents,
        WorkflowEventRecord workflowEvent)
    {
        return workflowEvent.Kind is WorkflowEventKind.ExecutorInvoked or WorkflowEventKind.ExecutorCompleted or WorkflowEventKind.ExecutorFailed &&
               workflowEvent.NodeId.HasValue &&
               progressEvents.Any(progressEvent =>
                   progressEvent.Kind == workflowEvent.Kind &&
                   progressEvent.NodeId == workflowEvent.NodeId);
    }

    private static bool IsTerminal(WorkflowRunState state)
        => state is WorkflowRunState.Completed or WorkflowRunState.Failed or WorkflowRunState.Cancelled;

    private static WorkflowCheckpointKind MapCheckpointKind(WorkflowRunState state)
        => state switch
        {
            WorkflowRunState.Completed => WorkflowCheckpointKind.Completed,
            WorkflowRunState.Failed => WorkflowCheckpointKind.Failed,
            WorkflowRunState.Cancelled => WorkflowCheckpointKind.Cancelled,
            _ => WorkflowCheckpointKind.RuntimeBoundary
        };

    private static IReadOnlyList<WorkflowArtifactRecord> MergeArtifacts(
        params IEnumerable<WorkflowArtifactRecord>[] artifactGroups)
    {
        var artifactsByPath = new Dictionary<string, WorkflowArtifactRecord>(StringComparer.Ordinal);
        foreach (var artifact in artifactGroups.SelectMany(group => group))
        {
            if (!artifactsByPath.ContainsKey(artifact.StoragePath))
            {
                artifactsByPath.Add(artifact.StoragePath, artifact);
            }
        }

        return artifactsByPath.Values
            .OrderBy(artifact => artifact.CreatedAtUtc)
            .ThenBy(artifact => artifact.StoragePath, StringComparer.Ordinal)
            .ToArray();
    }
}
