using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Agents.AI.Workflows;

namespace CanDoItAll.AgentFramework.Maf;

public sealed class MafInProcessWorkflowExecutionBackend : IWorkflowExecutionBackend
{
    private readonly IWorkflowMafCompiler compiler;
    private readonly IMafWorkflowEventNormalizer eventNormalizer;
    private readonly IWorkflowCheckpointFactory checkpointFactory;
    private readonly IWorkflowPayloadPolicyService payloadPolicyService;
    private readonly IReadOnlyList<LlmCallComponent>? components;
    private readonly IWorkflowComponentLibraryService? componentLibrary;

    public MafInProcessWorkflowExecutionBackend(
        IWorkflowMafCompiler compiler,
        IReadOnlyList<LlmCallComponent> components,
        IMafWorkflowEventNormalizer? eventNormalizer = null,
        IWorkflowCheckpointFactory? checkpointFactory = null,
        IWorkflowPayloadPolicyService? payloadPolicyService = null)
    {
        ArgumentNullException.ThrowIfNull(compiler);
        ArgumentNullException.ThrowIfNull(components);

        this.compiler = compiler;
        this.components = components;
        this.eventNormalizer = eventNormalizer ?? new MafWorkflowEventNormalizer();
        this.checkpointFactory = checkpointFactory ?? new WorkflowCheckpointFactory();
        this.payloadPolicyService = payloadPolicyService ?? new WorkflowPayloadPolicyService();
    }

    public MafInProcessWorkflowExecutionBackend(
        IWorkflowMafCompiler compiler,
        IWorkflowComponentLibraryService componentLibrary,
        IMafWorkflowEventNormalizer? eventNormalizer = null,
        IWorkflowCheckpointFactory? checkpointFactory = null,
        IWorkflowPayloadPolicyService? payloadPolicyService = null)
    {
        ArgumentNullException.ThrowIfNull(compiler);
        ArgumentNullException.ThrowIfNull(componentLibrary);

        this.compiler = compiler;
        this.componentLibrary = componentLibrary;
        this.eventNormalizer = eventNormalizer ?? new MafWorkflowEventNormalizer();
        this.checkpointFactory = checkpointFactory ?? new WorkflowCheckpointFactory();
        this.payloadPolicyService = payloadPolicyService ?? new WorkflowPayloadPolicyService();
    }

    public WorkflowRuntimeBackendDescriptor Descriptor { get; } = new(
        WorkflowRuntimeBackendKind.InProcess,
        "MAF in-process workflow runtime",
        IsDurable: false,
        SupportsStreaming: true,
        SupportsExternalRequests: true,
        SupportsDashboardObservability: false,
        OperationalNotes: "Use for local development, tests, previews, and approved short non-durable runs only.");

    public async Task<WorkflowBackendStartResult> StartAsync(
        WorkflowDefinition definition,
        WorkflowRunStartRequest request,
        WorkflowRunId runId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(request);

        var now = DateTimeOffset.UtcNow;
        var resolvedComponents = componentLibrary is null
            ? components ?? []
            : await componentLibrary.ListComponentsAsync(cancellationToken);
        var build = compiler.Compile(
            definition,
            FilterReferencedComponents(definition, resolvedComponents),
            request.PreviewSimulationPlan);
        if (!build.Compilation.Succeeded || build.Workflow is null)
        {
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
                Summary: build.Compilation.ErrorMessage,
                CreatedAtUtc: now,
                UpdatedAtUtc: now);
            var failedEvent = new WorkflowEventRecord(
                Guid.NewGuid(),
                runId,
                WorkflowEventKind.Error,
                NodeId: null,
                build.Compilation.ErrorMessage,
                WorkflowEventPayloads.Serialize(
                    WorkflowEventPayloadSource.Runtime,
                    "WorkflowCompilationFailed",
                    inlineJson: failurePayload.InlinePayload,
                    reference: failurePayload.Reference,
                    originalInlineCharacters: failurePayload.OriginalPayloadCharacters,
                    inlineTruncated: failurePayload.InlineTruncated,
                    maxInlinePayloadCharacters: failurePayload.MaxInlinePayloadCharacters),
                now);

            var failedCheckpoint = checkpointFactory.CreateMetadataCheckpoint(
                new WorkflowCheckpointCreateRequest(
                    definition,
                    runId,
                    Descriptor.Kind,
                    WorkflowCheckpointKind.Failed,
                    now)
                {
                    Summary = build.Compilation.ErrorMessage
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
                        DateTimeOffset.UtcNow))
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
                    DateTimeOffset.UtcNow));
            }

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
                UpdatedAtUtc: DateTimeOffset.UtcNow);
            var artifacts = MergeArtifacts(
                progressObserver.Artifacts,
                finalState == WorkflowRunState.Completed
                    ? BuildConfiguredFileArtifacts(definition, runId, DateTimeOffset.UtcNow)
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
                Checkpoints = [checkpoint]
            };
        }
    }

    private async Task<WorkflowBackendStartResult> CreateWaitingForExternalRequestResultAsync(
        WorkflowDefinition definition,
        WorkflowRunId runId,
        WorkflowRuntimeBackendKind backend,
        WorkflowExternalRequestRecord request,
        WorkflowBackendProgressEventObserver progressObserver,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
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
            UpdatedAtUtc: now);
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
            Checkpoints = [checkpoint]
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

    private static IReadOnlyList<WorkflowArtifactRecord> BuildConfiguredFileArtifacts(
        WorkflowDefinition definition,
        WorkflowRunId runId,
        DateTimeOffset createdAtUtc)
    {
        var artifactsByPath = new Dictionary<string, WorkflowArtifactRecord>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in definition.Graph.Nodes)
        {
            var artifact = TryCreateConfiguredFileArtifact(node, runId, createdAtUtc);
            if (artifact is null || artifactsByPath.ContainsKey(artifact.StoragePath))
            {
                continue;
            }

            artifactsByPath.Add(artifact.StoragePath, artifact);
        }

        return artifactsByPath.Values.ToList();
    }

    private static WorkflowArtifactRecord? TryCreateConfiguredFileArtifact(
        WorkflowNode node,
        WorkflowRunId runId,
        DateTimeOffset createdAtUtc)
    {
        if (node.Settings.ExecutorId == WorkflowExecutorIds.StorageFile)
        {
            var settings = WorkflowExecutorJson.Deserialize<WorkflowStorageFileExecutorSettings>(node.Settings.ExecutorSettingsJson);
            return settings.Operation is WorkflowStorageFileOperation.WriteText or WorkflowStorageFileOperation.AppendText &&
                   !string.IsNullOrWhiteSpace(settings.Path)
                ? CreateFileArtifact(runId, node.Id, settings.Path.Trim(), "text/plain", createdAtUtc)
                : null;
        }

        if (node.Settings.ExecutorId == WorkflowExecutorIds.Spreadsheet)
        {
            var settings = WorkflowExecutorJson.Deserialize<WorkflowSpreadsheetExecutorSettings>(node.Settings.ExecutorSettingsJson);
            var outputPath = string.IsNullOrWhiteSpace(settings.OutputWorkbookPath)
                ? settings.WorkbookPath
                : settings.OutputWorkbookPath;
            return settings.Operation is WorkflowSpreadsheetOperation.WriteCell or WorkflowSpreadsheetOperation.WriteRange or WorkflowSpreadsheetOperation.ApplyBatch &&
                   !string.IsNullOrWhiteSpace(outputPath)
                ? CreateFileArtifact(
                    runId,
                    node.Id,
                    outputPath.Trim(),
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    createdAtUtc)
                : null;
        }

        if (node.Settings.ExecutorId == WorkflowExecutorIds.MarkdownRender)
        {
            var settings = WorkflowExecutorJson.Deserialize<WorkflowMarkdownRenderExecutorSettings>(node.Settings.ExecutorSettingsJson);
            return !string.IsNullOrWhiteSpace(settings.OutputPath)
                ? CreateFileArtifact(runId, node.Id, settings.OutputPath.Trim(), "text/markdown", createdAtUtc)
                : null;
        }

        if (node.Settings.ExecutorId == WorkflowExecutorIds.HttpFetch)
        {
            var settings = WorkflowExecutorJson.Deserialize<WorkflowHttpExecutorSettings>(node.Settings.ExecutorSettingsJson);
            return settings.DownloadToWorkspace && !string.IsNullOrWhiteSpace(settings.OutputPath)
                ? CreateFileArtifact(runId, node.Id, settings.OutputPath.Trim(), "application/octet-stream", createdAtUtc)
                : null;
        }

        return null;
    }

    private static WorkflowArtifactRecord CreateFileArtifact(
        WorkflowRunId runId,
        WorkflowNodeId nodeId,
        string storagePath,
        string contentType,
        DateTimeOffset createdAtUtc)
    {
        var name = Path.GetFileName(storagePath);
        return new WorkflowArtifactRecord(
            WorkflowArtifactId.New(),
            runId,
            WorkflowArtifactKind.File,
            nodeId,
            string.IsNullOrWhiteSpace(name) ? storagePath : name,
            contentType,
            storagePath,
            "Workflow file operation wrote or updated this path.",
            createdAtUtc);
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

    private sealed class WorkflowBackendProgressEventObserver(
        WorkflowRunId runId,
        WorkflowDefinition definition,
        WorkflowPreviewSimulationPlan previewSimulationPlan,
        IWorkflowPayloadPolicyService payloadPolicyService,
        IWorkflowNodeExecutionProgressObserver? next) : IWorkflowNodeExecutionProgressObserver
    {
        private readonly List<WorkflowEventRecord> events = [];
        private readonly List<WorkflowArtifactRecord> artifacts = [];
        private readonly IReadOnlyDictionary<WorkflowNodeId, WorkflowNode> nodesById = definition.Graph.Nodes.ToDictionary(node => node.Id);
        private readonly HashSet<WorkflowNodeId> previewSimulationNodeIds = previewSimulationPlan.Steps
            .Select(step => step.NodeId)
            .ToHashSet();

        public IReadOnlyList<WorkflowEventRecord> Events => events;

        public IReadOnlyList<WorkflowArtifactRecord> Artifacts => artifacts;

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
                await next.RecordAsync(progress, cancellationToken);
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

    private sealed class WorkflowBackendExternalRequestCapture : IWorkflowExternalRequestCapture
    {
        private readonly List<WorkflowExternalRequestRecord> requests = [];

        public IReadOnlyList<WorkflowExternalRequestRecord> Requests => requests;

        public void Record(WorkflowExternalRequestRecord request)
        {
            requests.Add(request);
        }
    }
}
