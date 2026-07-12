using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using Microsoft.Agents.AI.Workflows;

namespace CanDoItAll.AgentFramework.Maf;

public sealed class MafInProcessWorkflowExecutionBackend : IWorkflowExecutionBackend
{
    private readonly IWorkflowMafCompiler compiler;
    private readonly IMafWorkflowEventNormalizer eventNormalizer;
    private readonly IWorkflowCheckpointFactory checkpointFactory;
    private readonly IWorkflowPayloadPolicyService payloadPolicyService;
    private readonly TimeProvider timeProvider;
    private readonly IReadOnlyList<LlmCallComponent>? components;
    private readonly IWorkflowComponentLibraryService? componentLibrary;

    public MafInProcessWorkflowExecutionBackend(
        IWorkflowMafCompiler compiler,
        IReadOnlyList<LlmCallComponent> components,
        IMafWorkflowEventNormalizer? eventNormalizer = null,
        IWorkflowCheckpointFactory? checkpointFactory = null,
        IWorkflowPayloadPolicyService? payloadPolicyService = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(compiler);
        ArgumentNullException.ThrowIfNull(components);

        this.compiler = compiler;
        this.components = components;
        this.eventNormalizer = eventNormalizer ?? new MafWorkflowEventNormalizer();
        this.checkpointFactory = checkpointFactory ?? new WorkflowCheckpointFactory();
        this.payloadPolicyService = payloadPolicyService ?? new WorkflowPayloadPolicyService();
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public MafInProcessWorkflowExecutionBackend(
        IWorkflowMafCompiler compiler,
        IWorkflowComponentLibraryService componentLibrary,
        IMafWorkflowEventNormalizer? eventNormalizer = null,
        IWorkflowCheckpointFactory? checkpointFactory = null,
        IWorkflowPayloadPolicyService? payloadPolicyService = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(compiler);
        ArgumentNullException.ThrowIfNull(componentLibrary);

        this.compiler = compiler;
        this.componentLibrary = componentLibrary;
        this.eventNormalizer = eventNormalizer ?? new MafWorkflowEventNormalizer();
        this.checkpointFactory = checkpointFactory ?? new WorkflowCheckpointFactory();
        this.payloadPolicyService = payloadPolicyService ?? new WorkflowPayloadPolicyService();
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public WorkflowRuntimeBackendDescriptor Descriptor { get; } = new(
        WorkflowRuntimeBackendKind.InProcess,
        "MAF in-process workflow runtime",
        IsDurable: false,
        SupportsStreaming: true,
        SupportsExternalRequests: true,
        SupportsDashboardObservability: false,
        OperationalNotes: "Use for local development, tests, previews, and approved short non-durable runs only.")
    {
        SupportsExternalResponseResume = false,
        SupportsActiveCancellation = true
    };

    public async Task<WorkflowBackendStartResult> StartAsync(
        WorkflowDefinition definition,
        WorkflowRunStartRequest request,
        WorkflowRunId runId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(request);

        var now = timeProvider.GetUtcNow();
        var resolvedComponents = componentLibrary is null
            ? components ?? []
            : await componentLibrary.ListComponentsAsync(cancellationToken);
        var build = compiler.Compile(
            definition,
            FilterReferencedComponents(definition, resolvedComponents),
            request.PreviewSimulationPlan);
        if (!build.Compilation.Succeeded || build.Workflow is null)
        {
            var diagnostic = MafWorkflowAdapterFailureDiagnostics.CompilationFailed(
                definition,
                runId,
                Descriptor.Kind,
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
                Descriptor.Kind,
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
                    Descriptor.Kind,
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
                Descriptor.Kind,
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
                Descriptor.Kind,
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
                Descriptor.Kind,
                BackendRunId: run.SessionId,
                Summary: failureEvent is not null
                    ? WorkflowFailureDisplayFormatter.ToUserMessage(failureEvent.Message)
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
                    Descriptor.Kind,
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
        var artifactsByPath = new Dictionary<string, WorkflowArtifactRecord>(StringComparer.OrdinalIgnoreCase);
        foreach (var artifact in artifactGroups.SelectMany(group => group))
        {
            if (!artifactsByPath.ContainsKey(artifact.StoragePath))
            {
                artifactsByPath.Add(artifact.StoragePath, artifact);
            }
        }

        return artifactsByPath.Values
            .OrderBy(artifact => artifact.CreatedAtUtc)
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

    private static IReadOnlyList<LlmCallComponent> FilterReferencedComponents(
        WorkflowDefinition definition,
        IReadOnlyList<LlmCallComponent> resolvedComponents)
    {
        var referencedComponentIds = definition.Graph.Nodes
            .Where(node => node.Kind == WorkflowNodeKind.LlmCall && node.Settings.ComponentId.HasValue)
            .Select(node => node.Settings.ComponentId!.Value)
            .ToHashSet();
        if (referencedComponentIds.Count == 0)
        {
            return [];
        }

        return resolvedComponents
            .Where(component => referencedComponentIds.Contains(component.Id))
            .ToArray();
    }
}
