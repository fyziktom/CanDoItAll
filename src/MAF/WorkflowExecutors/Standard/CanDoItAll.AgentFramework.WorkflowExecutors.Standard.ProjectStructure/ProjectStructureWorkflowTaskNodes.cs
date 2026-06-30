using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.AgentFramework.WorkflowExecutors.Standard.ProjectStructure;

public sealed partial class ProjectStructureWorkflowExecutor
{
    private static readonly JsonSerializerOptions TaskMetadataJsonOptions = new(JsonSerializerDefaults.Web);

    private static async Task<object> CreateTaskNodesAsync(
        IProjectStructureRuntimeGateway gateway,
        WorkflowProjectStructureExecutorSettings settings,
        WorkflowNodeInput input,
        CancellationToken cancellationToken)
    {
        var projectId = RequireProjectId(settings, input);
        var parentNodeId = ResolveOptionalNodeId(settings, input) ??
                           ResolveWorkflowParentNodeId(input) ??
                           throw new InvalidOperationException("Project-structure task creation requires 'NodeId', 'NodeIdJsonPath', or '$.runContext.workflowNodeId'.");
        var tasks = ReadTaskSources(settings, input);
        var createdNodes = new List<ProjectStructureRuntimeNodeSummary>(tasks.Count);
        var agent = BuildAgentContext(input);
        var idempotencyBatchKey = ResolveProjectWriteIdempotencyKey(settings, input);
        var taskIndex = 0;

        foreach (var task in tasks)
        {
            taskIndex++;
            cancellationToken.ThrowIfCancellationRequested();
            var taskIdempotencyKey = BuildTaskIdempotencyKey(idempotencyBatchKey, taskIndex);
            createdNodes.Add(await gateway.CreateNodeAsync(
                projectId,
                new ProjectStructureRuntimeNodeCreateRequest(
                    ProjectObjectType.WorkItem,
                    task.Title,
                    BuildTaskSubtitle(task),
                    BuildTaskNotes(task),
                    parentNodeId,
                    EndUtc: task.DueUtc,
                    ObjectSubtype: NormalizeTaskSubtype(settings.TaskObjectSubtype),
                    MetadataJson: BuildTaskMetadataJson(task, input),
                    IdempotencyKey: taskIdempotencyKey,
                    IdempotencyBatchKey: idempotencyBatchKey),
                agent,
                cancellationToken));
        }

        return new
        {
            projectId,
            parentNodeId,
            idempotencyBatchKey,
            createdTaskCount = createdNodes.Count,
            createdNodeIds = createdNodes.Select(node => node.Id).ToArray(),
            createdNodes = createdNodes.Select(node => new
            {
                node.Id,
                node.ParentId,
                node.ObjectType,
                node.ObjectSubtype,
                node.Title,
                node.Subtitle,
                node.EndUtc
            }).ToArray()
        };
    }

    private static ProjectStructureRuntimeAssetCreateRequest BuildAssetRequest(
        WorkflowProjectStructureExecutorSettings settings,
        WorkflowNodeInput input)
    {
        var objectType = settings.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
            ? ProjectObjectType.ImageAsset
            : ProjectObjectType.File;
        var title = Require(settings.Title, nameof(settings.Title));
        var sourcePath = string.IsNullOrWhiteSpace(settings.SourceWorkspacePath) ? null : settings.SourceWorkspacePath.Trim();
        ProjectStructureRuntimeMediaPayload? media = null;
        var content = WorkflowInputPayloadText.Resolve(settings.Content, settings.ContentFromInput, input);
        var idempotencyKey = ResolveProjectWriteIdempotencyKey(settings, input);

        if (sourcePath is null)
        {
            var bytes = Encoding.UTF8.GetBytes(content);
            media = new ProjectStructureRuntimeMediaPayload(
                $"{SanitizeFileName(title)}.{NormalizeAssetKind(settings.AssetKind)}",
                settings.ContentType,
                Convert.ToBase64String(bytes));
        }

        return new ProjectStructureRuntimeAssetCreateRequest(
            objectType,
            title,
            Subtitle: string.Empty,
            Notes: content,
            media,
            ParentNodeKey: ResolveOptionalNodeId(settings, input) ?? ResolveWorkflowParentNodeId(input),
            ObjectSubtype: NormalizeAssetKind(settings.AssetKind),
            MetadataJson: "{}",
            SourceWorkspacePath: sourcePath,
            SourceFileName: $"{SanitizeFileName(title)}.{NormalizeAssetKind(settings.AssetKind)}",
            SourceContentType: settings.ContentType,
            IdempotencyKey: idempotencyKey,
            IdempotencyBatchKey: idempotencyKey);
    }

    private static IReadOnlyList<WorkflowTaskNodeSource> ReadTaskSources(
        WorkflowProjectStructureExecutorSettings settings,
        WorkflowNodeInput input)
    {
        if (settings.MaxTaskNodes <= 0)
        {
            throw new InvalidOperationException("Project-structure executor setting 'MaxTaskNodes' must be greater than zero.");
        }

        var tasksElement = ResolveInputJsonElement(input, settings.TaskItemsJsonPath, nameof(settings.TaskItemsJsonPath));
        if (tasksElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException($"Project-structure executor setting '{nameof(settings.TaskItemsJsonPath)}' must resolve to a JSON array.");
        }

        var tasks = new List<WorkflowTaskNodeSource>();
        var index = 0;
        foreach (var item in tasksElement.EnumerateArray())
        {
            index++;
            if (index > settings.MaxTaskNodes)
            {
                throw new InvalidOperationException($"Project-structure task creation received more than the configured MaxTaskNodes value of {settings.MaxTaskNodes}.");
            }

            if (item.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException($"Task item {index} must be a JSON object.");
            }

            tasks.Add(new WorkflowTaskNodeSource(
                ReadRequiredTaskString(item, "title", index),
                ReadOptionalString(item, "summary", "notes", "description"),
                ReadOptionalString(item, "owner", "assignee"),
                ReadOptionalDueUtc(item, index),
                ReadOptionalString(item, "urgency", "priority"),
                ReadOptionalBoolean(item, "requiresResponse", "responseRequired"),
                ReadOptionalBoolean(item, "asap", "needsAsapResponse"),
                ReadOptionalString(item, "sourceEmailId", "emailId"),
                ReadOptionalStringArray(item, "evidence")));
        }

        if (tasks.Count == 0)
        {
            throw new InvalidOperationException("Project-structure task creation requires at least one task item.");
        }

        return tasks;
    }

    private static string BuildTaskSubtitle(WorkflowTaskNodeSource task)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(task.Urgency))
        {
            parts.Add(task.Urgency.Trim());
        }

        if (task.Asap)
        {
            parts.Add("asap");
        }

        if (task.RequiresResponse)
        {
            parts.Add("response required");
        }

        if (!string.IsNullOrWhiteSpace(task.Owner))
        {
            parts.Add($"owner: {task.Owner.Trim()}");
        }

        if (task.DueUtc is not null)
        {
            parts.Add($"due: {task.DueUtc:yyyy-MM-dd HH:mm 'UTC'}");
        }

        return string.Join(" | ", parts);
    }

    private static string BuildTaskNotes(WorkflowTaskNodeSource task)
    {
        var builder = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(task.Summary))
        {
            builder.AppendLine(task.Summary.Trim());
        }

        if (!string.IsNullOrWhiteSpace(task.SourceEmailId))
        {
            builder.AppendLine();
            builder.AppendLine($"Source email: {task.SourceEmailId.Trim()}");
        }

        if (task.Evidence.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Evidence:");
            foreach (var evidence in task.Evidence)
            {
                builder.AppendLine($"- {evidence}");
            }
        }

        return builder.ToString().Trim();
    }

    private static string BuildTaskMetadataJson(WorkflowTaskNodeSource task, WorkflowNodeInput input)
        => JsonSerializer.Serialize(
            new Dictionary<string, object?>
            {
                ["source"] = "workflow-email-intake",
                ["sourceEmailId"] = task.SourceEmailId,
                ["urgency"] = task.Urgency,
                ["owner"] = task.Owner,
                ["requiresResponse"] = task.RequiresResponse,
                ["asap"] = task.Asap,
                ["workflowRunId"] = ReadRunContextString(input, "runId"),
                ["workflowNodeId"] = ReadRunContextString(input, "workflowNodeId")
            },
            TaskMetadataJsonOptions);

    private static string? ResolveProjectWriteIdempotencyKey(
        WorkflowProjectStructureExecutorSettings settings,
        WorkflowNodeInput input)
    {
        var configuredKey = !string.IsNullOrWhiteSpace(settings.IdempotencyKey)
            ? settings.IdempotencyKey.Trim()
            : ResolveIdempotencyKeyFromJsonPath(settings, input);
        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            return null;
        }

        var suffix = settings.IdempotencyKeySuffix.Trim();
        return string.IsNullOrWhiteSpace(suffix)
            ? configuredKey
            : $"{configuredKey}:{suffix}";
    }

    private static string? ResolveIdempotencyKeyFromJsonPath(
        WorkflowProjectStructureExecutorSettings settings,
        WorkflowNodeInput input)
    {
        if (string.IsNullOrWhiteSpace(settings.IdempotencyKeyJsonPath))
        {
            return null;
        }

        var resolved = ResolveInputJsonString(input, settings.IdempotencyKeyJsonPath, nameof(settings.IdempotencyKeyJsonPath));
        if (string.IsNullOrWhiteSpace(resolved))
        {
            throw new InvalidOperationException(
                $"Project-structure executor setting '{nameof(settings.IdempotencyKeyJsonPath)}' resolved an empty idempotency key.");
        }

        return resolved.Trim();
    }

    private static string? BuildTaskIdempotencyKey(string? idempotencyBatchKey, int taskIndex)
        => string.IsNullOrWhiteSpace(idempotencyBatchKey)
            ? null
            : $"{idempotencyBatchKey}:{taskIndex.ToString("D3", CultureInfo.InvariantCulture)}";

    private static string NormalizeTaskSubtype(string value)
        => string.IsNullOrWhiteSpace(value) ? "task" : value.Trim().ToLowerInvariant();

}
