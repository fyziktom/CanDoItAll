using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessRuntimeOwnedToolReceiptFactory
{
    internal static ToolExecutionReceiptRecord From(
        Guid executionRunId,
        WorkspaceFileMutationResult result)
        => FromWorkspaceReceipt(executionRunId, result.Receipt, result.Path, result.Succeeded, result.Message);

    internal static ToolExecutionReceiptRecord From(
        Guid executionRunId,
        WorkspacePathStatResult result)
        => FromWorkspaceReceipt(executionRunId, result.Receipt, result.Path, result.Succeeded, result.Message);

    internal static ToolExecutionReceiptRecord From(
        Guid executionRunId,
        WorkspaceTextFileReadResult result)
        => FromWorkspaceReceipt(executionRunId, result.Receipt, result.Path, result.Succeeded, result.Message);

    internal static ToolExecutionReceiptRecord From(
        Guid executionRunId,
        WorkspaceCommandExecutionResult result)
        => new(
            Guid.NewGuid(),
            executionRunId,
            "workspace-process",
            result.ToolName,
            result.RiskClass,
            result.ApprovalRequired ? "Required" : "NotRequired",
            result.Boundary.Notes,
            result.ArgumentsSummary,
            result.WorkingDirectory,
            result.Succeeded
                ? $"Succeeded (exit {result.ExitCode}): {result.Message}"
                : $"Failed (exit {result.ExitCode}): {result.Message}",
            result.Receipt.StartedAtUtc,
            result.Receipt.CompletedAtUtc);

    private static ToolExecutionReceiptRecord FromWorkspaceReceipt(
        Guid executionRunId,
        WorkspaceToolReceipt receipt,
        string path,
        bool succeeded,
        string message)
        => new(
            Guid.NewGuid(),
            executionRunId,
            "workspace-file",
            receipt.Operation,
            receipt.MutatesWorkspace ? "WorkspaceMutation" : "ReadOnlyWorkspace",
            "NotRequired",
            receipt.Boundary,
            path,
            ".",
            succeeded ? $"Succeeded: {message}" : $"Failed: {message}",
            receipt.StartedAtUtc,
            receipt.CompletedAtUtc);
}
