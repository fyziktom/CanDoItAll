using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Agents.AI.Workflows;

namespace CanDoItAll.AgentFramework.Maf;

public sealed class MafInProcessWorkflowExecutionBackend : IWorkflowExecutionBackend
{
    private readonly MafWorkflowCompiler compiler;
    private readonly IReadOnlyList<LlmCallComponent>? components;
    private readonly IWorkflowComponentLibraryService? componentLibrary;

    public MafInProcessWorkflowExecutionBackend(
        MafWorkflowCompiler compiler,
        IReadOnlyList<LlmCallComponent> components)
    {
        ArgumentNullException.ThrowIfNull(compiler);
        ArgumentNullException.ThrowIfNull(components);

        this.compiler = compiler;
        this.components = components;
    }

    public MafInProcessWorkflowExecutionBackend(
        MafWorkflowCompiler compiler,
        IWorkflowComponentLibraryService componentLibrary)
    {
        ArgumentNullException.ThrowIfNull(compiler);
        ArgumentNullException.ThrowIfNull(componentLibrary);

        this.compiler = compiler;
        this.componentLibrary = componentLibrary;
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
        var build = compiler.Compile(definition, FilterReferencedComponents(definition, resolvedComponents));
        if (!build.Compilation.Succeeded || build.Workflow is null)
        {
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
                PayloadJson: string.Empty,
                now);

            return new WorkflowBackendStartResult(failed, [failedEvent], [], []);
        }

        await using var run = await InProcessExecution.RunAsync(
            build.Workflow,
            new WorkflowNodeInput(request.InputJson),
            runId.ToString(),
            cancellationToken);
        var status = await run.GetStatusAsync(cancellationToken);
        var mappedState = MafWorkflowStatusMapper.MapRunStatus(status);
        var finalState = mappedState == WorkflowRunState.Idle
            ? WorkflowRunState.Completed
            : mappedState;
        var events = run.OutgoingEvents
            .Select(workflowEvent => new WorkflowEventRecord(
                Guid.NewGuid(),
                runId,
                MafWorkflowStatusMapper.MapEventKind(workflowEvent),
                NodeId: null,
                workflowEvent.ToString(),
                PayloadJson: string.Empty,
                DateTimeOffset.UtcNow))
            .ToList();
        var failureEvent = events.LastOrDefault(workflowEvent =>
            workflowEvent.Kind is WorkflowEventKind.Error or WorkflowEventKind.ExecutorFailed);
        if (failureEvent is not null)
        {
            finalState = WorkflowRunState.Failed;
        }

        if (!events.Any(workflowEvent => workflowEvent.Kind == WorkflowEventKind.Started))
        {
            events.Insert(0, new WorkflowEventRecord(
                Guid.NewGuid(),
                runId,
                WorkflowEventKind.Started,
                NodeId: null,
                $"Workflow '{definition.Name}' started.",
                PayloadJson: request.InputJson,
                now));
        }

        if (finalState == WorkflowRunState.Completed)
        {
            events.Add(new WorkflowEventRecord(
                Guid.NewGuid(),
                runId,
                WorkflowEventKind.Completed,
                NodeId: null,
                $"Workflow '{definition.Name}' completed.",
                PayloadJson: string.Empty,
                DateTimeOffset.UtcNow));
        }

        var snapshot = new WorkflowRunSnapshot(
            runId,
            definition.Id,
            definition.VersionId,
            finalState,
            Descriptor.Kind,
            BackendRunId: run.SessionId,
            Summary: failureEvent?.Message ??
                     (finalState == WorkflowRunState.Completed
                         ? $"Workflow '{definition.Name}' completed."
                         : $"Workflow '{definition.Name}' is {finalState}."),
            CreatedAtUtc: now,
            UpdatedAtUtc: DateTimeOffset.UtcNow);
        var artifacts = finalState == WorkflowRunState.Completed
            ? BuildConfiguredFileArtifacts(definition, runId, DateTimeOffset.UtcNow)
            : [];

        return new WorkflowBackendStartResult(snapshot, events, [], artifacts);
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
}
