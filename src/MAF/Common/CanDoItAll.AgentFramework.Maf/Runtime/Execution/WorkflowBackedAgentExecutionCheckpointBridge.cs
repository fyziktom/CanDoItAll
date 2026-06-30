using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Agents.AI.Workflows.Checkpointing;

namespace CanDoItAll.AgentFramework.Maf;

public sealed class WorkflowBackedAgentExecutionCheckpointBridge : IAgentExecutionCheckpointBridge, IDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly ISandboxWorkspaceStore store;
    private readonly DirectoryInfo checkpointRoot;
    private readonly DirectoryInfo checkpointPayloadRoot;
    private readonly SemaphoreSlim gate = new(1, 1);

    public WorkflowBackedAgentExecutionCheckpointBridge(
        ISandboxWorkspaceStore store,
        string workspaceRoot,
        WorkspaceScopeDescriptor? workspaceScope = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);

        this.store = store;
        var resolvedScope = workspaceScope ?? WorkspaceScopeDescriptor.Sandbox;
        checkpointRoot = new DirectoryInfo(Path.Combine(resolvedScope.ResolveDataRoot(workspaceRoot), "execution", "workflow-checkpoints"));
        checkpointRoot.Create();
        checkpointPayloadRoot = new DirectoryInfo(Path.Combine(checkpointRoot.FullName, "payloads"));
        checkpointPayloadRoot.Create();
    }

    public async Task<ExecutionWorkflowCheckpointRecord?> CapturePendingApprovalCheckpointAsync(
        ExecutionRunRecord run,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(run);

        if (run.PendingApprovals.Count == 0)
        {
            return null;
        }

        var capturedAtUtc = DateTimeOffset.UtcNow;
        var activity = System.Diagnostics.Activity.Current;
        var payload = new StoredExecutionCheckpointPayload(
            RuntimeSessionKey: run.RuntimeSessionKey,
            SerializedSessionStateJson: run.SerializedSessionStateJson,
            PendingApprovals: run.PendingApprovals,
            AutoApprovePendingToolCalls: run.AutoApprovePendingToolCalls,
            ProviderName: run.ProviderName,
            Model: run.Model,
            StructuredOutputContractKey: run.StructuredOutputContractKey,
            StructuredOutputTypeName: run.StructuredOutputTypeName,
            StructuredOutputSchemaName: run.StructuredOutputSchemaName,
            StructuredOutputSchemaDescription: run.StructuredOutputSchemaDescription,
            CapturedAtUtc: capturedAtUtc);

        var checkpointInfo = await CreateCheckpointAsync(GetWorkflowSessionId(run.Id), payload, cancellationToken);
        await PersistPayloadShadowAsync(checkpointInfo, payload, cancellationToken);
        var record = new ExecutionWorkflowCheckpointRecord(
            Id: Guid.NewGuid(),
            ExecutionRunId: run.Id,
            WorkflowSessionId: checkpointInfo.SessionId,
            WorkflowCheckpointId: checkpointInfo.CheckpointId,
            CheckpointKind: "approval-wait",
            RunState: run.State,
            PendingApprovalIds: run.PendingApprovals
                .Select(item => item.ApprovalId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            CapturedAtUtc: capturedAtUtc,
            ResumedAtUtc: null,
            CorrelationId: run.CorrelationId,
            SourceKind: run.SourceKind,
            SourceId: run.SourceId,
            ProcessRunId: run.ProcessRunId,
            ProcessStepId: run.ProcessStepId,
            SchedulerRunId: run.SchedulerRunId,
            MessageId: run.MessageId,
            TraceId: activity?.TraceId.ToString() ?? string.Empty,
            SpanId: activity?.SpanId.ToString() ?? string.Empty);

        if (store is ISandboxWorkspaceExecutionRunStore executionRunStore)
        {
            var detail = await executionRunStore.GetExecutionRunDetailAsync(run.Id, cancellationToken)
                ?? throw new InvalidOperationException("Execution run was not found.");
            await executionRunStore.SaveExecutionRunDetailAsync(
                detail with
                {
                    Checkpoints = detail.Checkpoints
                        .Where(item => item.ExecutionRunId != run.Id || item.ResumedAtUtc.HasValue)
                        .Append(record)
                        .OrderByDescending(item => item.CapturedAtUtc)
                        .ToList()
                },
                cancellationToken);
        }
        else
        {
            await store.UpdateExecutionAsync(executionState => executionState with
            {
                ExecutionWorkflowCheckpoints = executionState.ExecutionWorkflowCheckpoints
                    .Where(item => item.ExecutionRunId != run.Id || item.ResumedAtUtc.HasValue)
                    .Append(record)
                    .OrderByDescending(item => item.CapturedAtUtc)
                    .ToList()
            }, cancellationToken);
        }

        return record;
    }

    public async Task<ExecutionWorkflowCheckpointRecord?> ValidatePendingApprovalResumeAsync(
        ExecutionRunRecord run,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(run);

        var checkpoints = store is ISandboxWorkspaceExecutionRunStore executionRunStore
            ? (await executionRunStore.GetExecutionRunDetailAsync(run.Id, cancellationToken)
                ?? throw new InvalidOperationException("Execution run was not found.")).Checkpoints
            : (await store.LoadExecutionAsync(cancellationToken)).ExecutionWorkflowCheckpoints
                .Where(item => item.ExecutionRunId == run.Id)
                .ToList();
        var checkpoint = checkpoints
            .Where(item => item.ExecutionRunId == run.Id && !item.ResumedAtUtc.HasValue)
            .OrderByDescending(item => item.CapturedAtUtc)
            .FirstOrDefault();

        if (checkpoint is null)
        {
            return null;
        }

        var payload = await LoadPayloadAsync(checkpoint, cancellationToken);
        EnsurePayloadMatchesRun(run, checkpoint, payload);
        return checkpoint;
    }

    public async Task MarkCheckpointResumedAsync(
        Guid executionRunId,
        DateTimeOffset resumedAtUtc,
        CancellationToken cancellationToken = default)
    {
        if (store is ISandboxWorkspaceExecutionRunStore executionRunStore)
        {
            var detail = await executionRunStore.GetExecutionRunDetailAsync(executionRunId, cancellationToken)
                ?? throw new InvalidOperationException("Execution run was not found.");
            await executionRunStore.SaveExecutionRunDetailAsync(
                detail with
                {
                    Checkpoints = detail.Checkpoints
                        .Select(item => item.ExecutionRunId == executionRunId && !item.ResumedAtUtc.HasValue
                            ? item with { ResumedAtUtc = resumedAtUtc }
                            : item)
                        .OrderByDescending(item => item.CapturedAtUtc)
                        .ToList()
                },
                cancellationToken);
        }
        else
        {
            await store.UpdateExecutionAsync(executionState => executionState with
            {
                ExecutionWorkflowCheckpoints = executionState.ExecutionWorkflowCheckpoints
                    .Select(item => item.ExecutionRunId == executionRunId && !item.ResumedAtUtc.HasValue
                        ? item with { ResumedAtUtc = resumedAtUtc }
                        : item)
                    .OrderByDescending(item => item.CapturedAtUtc)
                    .ToList()
            }, cancellationToken);
        }
    }

    public void Dispose()
    {
        gate.Dispose();
    }

    private async Task<CheckpointInfo> CreateCheckpointAsync(
        string workflowSessionId,
        StoredExecutionCheckpointPayload payload,
        CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            using var checkpointStore = CreateCheckpointStore();
            using var document = JsonSerializer.SerializeToDocument(payload, SerializerOptions);
            return await checkpointStore.CreateCheckpointAsync(
                workflowSessionId,
                document.RootElement.Clone(),
                null!);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<StoredExecutionCheckpointPayload> LoadPayloadAsync(
        ExecutionWorkflowCheckpointRecord checkpoint,
        CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            using var checkpointStore = CreateCheckpointStore();
            var knownCheckpoints = await checkpointStore.RetrieveIndexAsync(checkpoint.WorkflowSessionId, null!);
            var exists = knownCheckpoints.Any(item =>
                string.Equals(item.SessionId, checkpoint.WorkflowSessionId, StringComparison.Ordinal) &&
                string.Equals(item.CheckpointId, checkpoint.WorkflowCheckpointId, StringComparison.Ordinal));
            if (!exists)
            {
                throw new KeyNotFoundException(
                    $"Workflow checkpoint '{checkpoint.WorkflowCheckpointId}' was not present in the workflow checkpoint index for session '{checkpoint.WorkflowSessionId}'.");
            }

            var payloadPath = GetPayloadPath(checkpoint.WorkflowSessionId, checkpoint.WorkflowCheckpointId);
            if (!File.Exists(payloadPath))
            {
                throw new KeyNotFoundException(
                    $"Workflow checkpoint payload '{checkpoint.WorkflowCheckpointId}' was not found at '{payloadPath}'.");
            }

            await using var stream = File.OpenRead(payloadPath);
            return await JsonSerializer.DeserializeAsync<StoredExecutionCheckpointPayload>(stream, SerializerOptions, cancellationToken)
                ?? throw new InvalidOperationException($"Workflow checkpoint '{checkpoint.WorkflowCheckpointId}' could not be deserialized.");
        }
        finally
        {
            gate.Release();
        }
    }

    private FileSystemJsonCheckpointStore CreateCheckpointStore()
    {
        return new FileSystemJsonCheckpointStore(checkpointRoot);
    }

    private async Task PersistPayloadShadowAsync(
        CheckpointInfo checkpointInfo,
        StoredExecutionCheckpointPayload payload,
        CancellationToken cancellationToken)
    {
        var payloadPath = GetPayloadPath(checkpointInfo.SessionId, checkpointInfo.CheckpointId);
        Directory.CreateDirectory(Path.GetDirectoryName(payloadPath)!);
        await File.WriteAllTextAsync(
            payloadPath,
            JsonSerializer.Serialize(payload, SerializerOptions),
            cancellationToken);
    }

    private string GetPayloadPath(string workflowSessionId, string workflowCheckpointId)
    {
        return Path.Combine(
            checkpointPayloadRoot.FullName,
            $"{Uri.EscapeDataString(workflowSessionId)}_{Uri.EscapeDataString(workflowCheckpointId)}.json");
    }

    private static void EnsurePayloadMatchesRun(
        ExecutionRunRecord run,
        ExecutionWorkflowCheckpointRecord checkpoint,
        StoredExecutionCheckpointPayload payload)
    {
        var persistedApprovalIds = payload.PendingApprovals
            .Select(item => item.ApprovalId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var currentApprovalIds = run.PendingApprovals
            .Select(item => item.ApprovalId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var approvalIdsMatch = persistedApprovalIds.SequenceEqual(currentApprovalIds, StringComparer.OrdinalIgnoreCase);
        if (approvalIdsMatch
            && string.Equals(payload.RuntimeSessionKey, run.RuntimeSessionKey, StringComparison.Ordinal)
            && string.Equals(payload.SerializedSessionStateJson, run.SerializedSessionStateJson, StringComparison.Ordinal)
            && payload.AutoApprovePendingToolCalls == run.AutoApprovePendingToolCalls
            && string.Equals(payload.StructuredOutputContractKey ?? string.Empty, run.StructuredOutputContractKey, StringComparison.Ordinal)
            && string.Equals(payload.StructuredOutputTypeName ?? string.Empty, run.StructuredOutputTypeName, StringComparison.Ordinal)
            && string.Equals(payload.StructuredOutputSchemaName ?? string.Empty, run.StructuredOutputSchemaName, StringComparison.Ordinal)
            && string.Equals(payload.StructuredOutputSchemaDescription ?? string.Empty, run.StructuredOutputSchemaDescription, StringComparison.Ordinal))
        {
            return;
        }

        throw new InvalidOperationException(
            $"Workflow checkpoint '{checkpoint.WorkflowCheckpointId}' for execution run '{run.Id:N}' no longer matches the durable pending-approval state.");
    }

    private static string GetWorkflowSessionId(Guid executionRunId)
    {
        return executionRunId.ToString("N");
    }

    private sealed record StoredExecutionCheckpointPayload(
        string RuntimeSessionKey,
        string? SerializedSessionStateJson,
        IReadOnlyList<PendingToolApprovalRecord> PendingApprovals,
        bool AutoApprovePendingToolCalls,
        string ProviderName,
        string Model,
        string StructuredOutputContractKey,
        string StructuredOutputTypeName,
        string StructuredOutputSchemaName,
        string StructuredOutputSchemaDescription,
        DateTimeOffset CapturedAtUtc);
}
