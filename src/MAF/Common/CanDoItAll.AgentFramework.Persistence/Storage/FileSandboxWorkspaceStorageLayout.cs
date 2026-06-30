using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Persistence;

internal sealed class FileSandboxWorkspaceStorageLayout(string rootPath, WorkspaceScopeDescriptor? scope = null)
{
    public string RootPath { get; } = rootPath;

    public WorkspaceScopeDescriptor Scope { get; } = scope ?? WorkspaceScopeDescriptor.Sandbox;

    public string DataRoot => Scope.ResolveDataRoot(RootPath);

    public string CatalogPath => Path.Combine(DataRoot, "workspace.json");

    public string WorkspaceIndexPath => Path.Combine(DataRoot, "workspace.index.json");

    public string WorkspaceLockPath => Path.Combine(DataRoot, "workspace.lock");

    public string LegacyExecutionPath => Path.Combine(DataRoot, "workspace.execution.json");

    public string ExecutionStorageRoot => Path.Combine(DataRoot, "execution");

    public string ExecutionIndexPath => Path.Combine(ExecutionStorageRoot, "index.json");

    public string ExecutionChatIndexPath => Path.Combine(ExecutionStorageRoot, "chat-index.json");

    public string ExecutionUsageIndexPath => Path.Combine(ExecutionStorageRoot, "usage-index.json");

    public string ExecutionSessionsRoot => Path.Combine(ExecutionStorageRoot, "sessions");

    public string ExecutionRunsRoot => Path.Combine(ExecutionStorageRoot, "runs");

    public string ExecutionOrphansRoot => Path.Combine(ExecutionStorageRoot, "orphans");

    public string SessionPath(Guid chatSessionId) => Path.Combine(ExecutionSessionsRoot, $"{chatSessionId:N}.json");

    public string RunRoot(Guid executionRunId) => Path.Combine(ExecutionRunsRoot, executionRunId.ToString("N"));

    public string RunPath(Guid executionRunId) => Path.Combine(RunRoot(executionRunId), "run.json");

    public string RunLogsRoot(Guid executionRunId) => Path.Combine(RunRoot(executionRunId), "logs");

    public string RunMetricsRoot(Guid executionRunId) => Path.Combine(RunRoot(executionRunId), "metrics");

    public string RunUsageRoot(Guid executionRunId) => Path.Combine(RunRoot(executionRunId), "usage");

    public string RunApprovalsRoot(Guid executionRunId) => Path.Combine(RunRoot(executionRunId), "approvals");

    public string RunArtifactsRoot(Guid executionRunId) => Path.Combine(RunRoot(executionRunId), "audit", "artifacts");

    public string RunWorkflowCheckpointsRoot(Guid executionRunId) => Path.Combine(RunRoot(executionRunId), "workflow-checkpoints", "records");

    public string RunReceiptsRoot(Guid executionRunId) => Path.Combine(RunRoot(executionRunId), "audit", "receipts");

    public string OrphanLogsRoot => Path.Combine(ExecutionOrphansRoot, "logs");

    public string OrphanMetricsRoot => Path.Combine(ExecutionOrphansRoot, "metrics");

    public string OrphanUsageRoot => Path.Combine(ExecutionOrphansRoot, "usage");

    public string OrphanApprovalsRoot => Path.Combine(ExecutionOrphansRoot, "approvals");

    public string OrphanArtifactsRoot => Path.Combine(ExecutionOrphansRoot, "artifacts");

    public string OrphanReceiptsRoot => Path.Combine(ExecutionOrphansRoot, "receipts");

    public bool ExecutionStorageExists()
    {
        return File.Exists(ExecutionIndexPath)
               || Directory.Exists(ExecutionSessionsRoot)
               || Directory.Exists(ExecutionRunsRoot)
               || Directory.Exists(ExecutionOrphansRoot);
    }
}
