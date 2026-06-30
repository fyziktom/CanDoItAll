using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public enum WorkflowExecutorExecutionAuditStatus
{
    Started,
    Completed,
    Failed
}

public sealed record WorkflowExecutorExecutionAuditRecord(
    WorkflowId WorkflowId,
    WorkflowVersionId VersionId,
    WorkflowRunId? RunId,
    WorkflowNodeId NodeId,
    WorkflowExecutorId ExecutorId,
    WorkflowExecutorSourceKind SourceKind,
    string PluginId,
    string PackageId,
    string PluginConnectionId,
    WorkflowExecutorExecutionAuditStatus Status,
    int AttemptNumber,
    int MaxAttempts,
    int TimeoutSeconds,
    bool CaptureOutputArtifact,
    string RedactedSettingsSummary,
    string RedactedMessage,
    int? PayloadCharacters,
    DateTimeOffset OccurredAtUtc);

public interface IWorkflowExecutorExecutionObserver
{
    ValueTask RecordAsync(
        WorkflowExecutorExecutionAuditRecord auditRecord,
        CancellationToken cancellationToken = default);
}

public interface IWorkflowExecutorExecutionAuditSink
{
    ValueTask RecordAsync(
        WorkflowExecutorExecutionAuditRecord auditRecord,
        CancellationToken cancellationToken = default);
}
