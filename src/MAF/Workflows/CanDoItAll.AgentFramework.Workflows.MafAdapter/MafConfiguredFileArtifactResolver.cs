using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Maf;

internal static class MafConfiguredFileArtifactResolver
{
    public static IReadOnlyList<WorkflowArtifactRecord> BuildConfiguredFileArtifacts(
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
}
