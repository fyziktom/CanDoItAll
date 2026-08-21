using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using Microsoft.Agents.AI.Workflows;

namespace CanDoItAll.AgentFramework.Maf;

internal sealed class MafLegacyWorkflowExecutionDriver
{
    private readonly IMafWorkflowEventNormalizer eventNormalizer;
    private readonly IWorkflowCheckpointFactory checkpointFactory;
    private readonly IWorkflowPayloadPolicyService payloadPolicyService;
    private readonly TimeProvider timeProvider;

    public MafLegacyWorkflowExecutionDriver(
        IMafWorkflowEventNormalizer? eventNormalizer = null,
        IWorkflowCheckpointFactory? checkpointFactory = null,
        IWorkflowPayloadPolicyService? payloadPolicyService = null,
        TimeProvider? timeProvider = null)
    {
        this.eventNormalizer = eventNormalizer ?? new MafWorkflowEventNormalizer();
        this.checkpointFactory = checkpointFactory ?? new WorkflowCheckpointFactory();
        this.payloadPolicyService = payloadPolicyService ?? new WorkflowPayloadPolicyService();
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<WorkflowBackendStartResult> StartAsync(
        WorkflowDefinition definition,
        WorkflowRunStartRequest request,
        WorkflowRunId runId,
        MafWorkflowBuildResult build,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(build);

        var now = timeProvider.GetUtcNow();
        if (!build.Compilation.Succeeded || build.Workflow is null)
        {
            var diagnostic = MafWorkflowAdapterFailureDiagnostics.CompilationFailed(
                definition,
                runId,
                WorkflowRuntimeBackendKind.InProcess,
                build.Compilation,
                now);
            var diagnosticJson = WorkflowRuntimeFailureDiagnosticMapper.Serialize(diagnostic);
            var failurePayload = await payloadPolicyService.ApplyAsync(new WorkflowPayloadPolicyRequest(
                runId,
                WorkflowPayloadPolicyScope.ExecutorError,
                build.Compilation.ErrorMessage,
                WorkflowArtifactKind.Text,
                "workflow-compilation-error.txt",
                "text/plain",
                now)
            {
                CaptureArtifact = true
            }, cancellationToken);
            var failed = new WorkflowRunSnapshot(
                runId,
                definition.Id,
                definition.VersionId,
                WorkflowRunState.Failed,
                WorkflowRuntimeBackendKind.InProcess,
                BackendRunId: runId.ToString(),
                Summary: diagnostic.Message,
                CreatedAtUtc: now,
                UpdatedAtUtc: now)
            {
                TerminalAtUtc = now,
                Origin = request.Origin
            };
            var failedEvent = new WorkflowEventRecord(
                Guid.NewGuid(),
                runId,
                WorkflowEventKind.Error,
                NodeId: null,
                diagnostic.Message,
                WorkflowEventPayloads.Serialize(
                    WorkflowEventPayloadSource.Runtime,
                    "WorkflowCompilationFailed",
                    inlineJson: diagnosticJson,
                    reference: failurePayload.Reference,
                    originalInlineCharacters: diagnosticJson.Length),
                now);

            var failedCheckpoint = checkpointFactory.CreateMetadataCheckpoint(
                new WorkflowCheckpointCreateRequest(
                    definition,
                    runId,
                    WorkflowRuntimeBackendKind.InProcess,
                    WorkflowCheckpointKind.Failed,
                    now)
                {
                    Summary = diagnostic.Message
                });

            var failureArtifacts = failurePayload.Artifact is null
                ? []
                : new[] { failurePayload.Artifact };

            return new WorkflowBackendStartResult(failed, [failedEvent], [], failureArtifacts)
            {
                Checkpoints = [failedCheckpoint]
            };
        }

        var eventBindings = MafWorkflowEventBindingIndex.FromDefinition(definition);
        using var auditScope = WorkflowExecutorExecutionAuditScope.Push(runId);
        var externalRequestCapture = new WorkflowBackendExternalRequestCapture();
        using var externalRequestScope = WorkflowExternalRequestCaptureScope.Push(externalRequestCapture);
        var progressObserver = new WorkflowBackendProgressEventObserver(
            runId,
            definition,
            request.PreviewSimulationPlan,
            payloadPolicyService,
            request.Origin,
            WorkflowNodeExecutionProgressScope.Current);
        Run run;
        try
        {
            run = await RunWithProgressObserverAsync(
                build.Workflow,
                request,
                runId,
                progressObserver,
                cancellationToken);
        }
        catch (Exception exception) when (WorkflowExternalRequestPendingException.TryFind(exception, out _))
        {
            if (!WorkflowExternalRequestPendingException.TryFind(exception, out var pendingException) ||
                pendingException is null)
            {
                throw;
            }

            return await CreateWaitingForExternalRequestResultAsync(
                definition,
                runId,
                WorkflowRuntimeBackendKind.InProcess,
                pendingException.Request,
                progressObserver,
                request.Origin,
                now,
                cancellationToken);
        }

        if (externalRequestCapture.Requests.LastOrDefault() is { } capturedRequest)
        {
            return await CreateWaitingForExternalRequestResultAsync(
                definition,
                runId,
                WorkflowRuntimeBackendKind.InProcess,
                capturedRequest,
                progressObserver,
                request.Origin,
                now,
                cancellationToken);
        }

        await using (run)
        {
            var status = await run.GetStatusAsync(cancellationToken);
            var mappedState = MafWorkflowStatusMapper.MapRunStatus(status);
            var finalState = mappedState == WorkflowRunState.Idle
                ? WorkflowRunState.Completed
                : mappedState;
            var events = progressObserver.Events
                .Concat(run.OutgoingEvents
                    .Select(workflowEvent => eventNormalizer.Normalize(
                        runId,
                        workflowEvent,
                        eventBindings,
                        timeProvider.GetUtcNow()))
                    .Where(workflowEvent => !IsDuplicateProgressEvent(progressObserver.Events, workflowEvent))
                    .ToList())
                .OrderBy(workflowEvent => workflowEvent.CreatedAtUtc)
                .ToList();
            var failureEvent = events.LastOrDefault(workflowEvent =>
                workflowEvent.Kind is WorkflowEventKind.Error or WorkflowEventKind.ExecutorFailed);
            if (failureEvent is not null)
            {
                finalState = WorkflowRunState.Failed;
            }

            var inputPayload = await payloadPolicyService.ApplyAsync(new WorkflowPayloadPolicyRequest(
                runId,
                WorkflowPayloadPolicyScope.RunInput,
                request.InputJson,
                WorkflowArtifactKind.Json,
                "workflow-input.json",
                "application/json",
                now)
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
                now);
            var startedEventIndex = events.FindIndex(workflowEvent => workflowEvent.Kind == WorkflowEventKind.Started);
            if (startedEventIndex >= 0)
            {
                events[startedEventIndex] = startedEvent;
            }
            else
            {
                events.Insert(0, startedEvent);
            }

            progressObserver.AddArtifact(inputPayload.Artifact);

            if (finalState == WorkflowRunState.Completed)
            {
                events.Add(new WorkflowEventRecord(
                    Guid.NewGuid(),
                    runId,
                    WorkflowEventKind.Completed,
                    NodeId: null,
                    $"Workflow '{definition.Name}' completed.",
                    WorkflowEventPayloads.Serialize(
                        WorkflowEventPayloadSource.Runtime,
                        "WorkflowCompleted"),
                    timeProvider.GetUtcNow()));
            }

            var snapshotUpdatedAtUtc = timeProvider.GetUtcNow();
            var snapshot = new WorkflowRunSnapshot(
                runId,
                definition.Id,
                definition.VersionId,
                finalState,
                WorkflowRuntimeBackendKind.InProcess,
                BackendRunId: run.SessionId,
                Summary: failureEvent is not null
                    ? WorkflowFailureDisplayFormatter.ToUserMessage(failureEvent)
                    : finalState == WorkflowRunState.Completed
                        ? $"Workflow '{definition.Name}' completed."
                        : $"Workflow '{definition.Name}' is {finalState}.",
                CreatedAtUtc: now,
                UpdatedAtUtc: snapshotUpdatedAtUtc)
            {
                TerminalAtUtc = IsTerminal(finalState) ? snapshotUpdatedAtUtc : null,
                Origin = request.Origin
            };
            var artifacts = MergeArtifacts(
                progressObserver.Artifacts,
                finalState == WorkflowRunState.Completed
                    ? MafConfiguredFileArtifactResolver.BuildConfiguredFileArtifacts(definition, runId, timeProvider.GetUtcNow())
                    : []);
            var checkpoint = checkpointFactory.CreateMetadataCheckpoint(
                new WorkflowCheckpointCreateRequest(
                    definition,
                    runId,
                    WorkflowRuntimeBackendKind.InProcess,
                    MapCheckpointKind(finalState),
                    snapshot.UpdatedAtUtc)
                {
                    Summary = snapshot.Summary
                });

            return new WorkflowBackendStartResult(snapshot, events, [], artifacts)
            {
                Checkpoints = [checkpoint],
                UsageObservations = progressObserver.UsageObservations
            };
        }
    }

    private async Task<WorkflowBackendStartResult> CreateWaitingForExternalRequestResultAsync(
        WorkflowDefinition definition,
        WorkflowRunId runId,
        WorkflowRuntimeBackendKind backend,
        WorkflowExternalRequestRecord request,
        WorkflowBackendProgressEventObserver progressObserver,
        WorkflowLaunchOrigin? origin,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var summary = request.Kind == WorkflowExternalRequestKind.Approval
            ? $"Workflow is waiting for approval at node '{request.NodeId}'."
            : $"Workflow is waiting for input at node '{request.NodeId}'.";
        var requestPayload = await payloadPolicyService.ApplyAsync(new WorkflowPayloadPolicyRequest(
            runId,
            WorkflowPayloadPolicyScope.ExternalRequest,
            request.RequestJson,
            WorkflowArtifactKind.Json,
            $"workflow-external-request-{request.Id.Value:N}.json",
            "application/json",
            now)
        {
            NodeId = request.NodeId,
            CaptureArtifact = true
        }, cancellationToken);
        var waitingRun = new WorkflowRunSnapshot(
            runId,
            definition.Id,
            definition.VersionId,
            WorkflowRunState.WaitingForInput,
            backend,
            BackendRunId: runId.ToString(),
            Summary: summary,
            CreatedAtUtc: createdAtUtc,
            UpdatedAtUtc: now)
        {
            Origin = origin
        };
        var waitingEvent = new WorkflowEventRecord(
            Guid.NewGuid(),
            runId,
            WorkflowEventKind.WaitingForInput,
            request.NodeId,
            summary,
            WorkflowEventPayloads.Serialize(
                WorkflowEventPayloadSource.ExternalRequest,
                "WorkflowExternalRequest",
                request.NodeId,
                requestId: request.Id,
                requestKind: request.Kind,
                inlineJson: requestPayload.InlinePayload,
                reference: requestPayload.Reference,
                originalInlineCharacters: requestPayload.OriginalPayloadCharacters,
                inlineTruncated: requestPayload.InlineTruncated,
                maxInlinePayloadCharacters: requestPayload.MaxInlinePayloadCharacters),
            now);
        progressObserver.AddArtifact(requestPayload.Artifact);
        var events = progressObserver.Events
            .Concat([waitingEvent])
            .OrderBy(workflowEvent => workflowEvent.CreatedAtUtc)
            .ToArray();
        var checkpoint = checkpointFactory.CreateMetadataCheckpoint(
            new WorkflowCheckpointCreateRequest(
                definition,
                runId,
                backend,
                WorkflowCheckpointKind.WaitingForInput,
                now)
            {
                NodeId = request.NodeId,
                ExternalRequestId = request.Id,
                Summary = summary
            });

        return new WorkflowBackendStartResult(waitingRun, events, [request], progressObserver.Artifacts)
        {
            Checkpoints = [checkpoint],
            UsageObservations = progressObserver.UsageObservations
        };
    }

    private static WorkflowCheckpointKind MapCheckpointKind(WorkflowRunState state)
        => state switch
        {
            WorkflowRunState.Completed => WorkflowCheckpointKind.Completed,
            WorkflowRunState.Failed => WorkflowCheckpointKind.Failed,
            WorkflowRunState.Cancelled => WorkflowCheckpointKind.Cancelled,
            _ => WorkflowCheckpointKind.RuntimeBoundary
        };

    private static bool IsTerminal(WorkflowRunState state)
        => state is WorkflowRunState.Completed or WorkflowRunState.Failed or WorkflowRunState.Cancelled;

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

    private static async Task<Run> RunWithProgressObserverAsync(
        Workflow workflow,
        WorkflowRunStartRequest request,
        WorkflowRunId runId,
        WorkflowBackendProgressEventObserver progressObserver,
        CancellationToken cancellationToken)
    {
        using var progressScope = WorkflowNodeExecutionProgressScope.Push(progressObserver);
        return await InProcessExecution.RunAsync(
            workflow,
            new WorkflowNodeInput(request.InputJson),
            runId.ToString(),
            cancellationToken);
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

}
