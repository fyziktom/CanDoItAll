using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.AgentFramework.Maf;

public sealed class ProjectStructureWorkflowExecutor(IServiceScopeFactory scopeFactory) : IWorkflowExecutor
{
    public WorkflowExecutorDescriptor Descriptor => BuiltInWorkflowExecutorDescriptors.ProjectStructure;

    public async ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
        WorkflowExecutorExecutionContext context,
        WorkflowNodeInput input,
        CancellationToken cancellationToken = default)
    {
        var settings = WorkflowExecutorJson.Deserialize<WorkflowProjectStructureExecutorSettings>(context.SettingsJson);
        using var scope = scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetService<ProjectStructureAgentService>()
            ?? throw new InvalidOperationException("Project-structure executor requires ProjectStructureAgentService, but it is not registered in this host.");

        object result = settings.Operation switch
        {
            WorkflowProjectStructureOperation.ListProjects => await service.ListProjectsAsync(cancellationToken),
            WorkflowProjectStructureOperation.ReadTree => await service.GetStructureAsync(
                RequireProjectId(settings, input),
                new ProjectStructureReadRequest(
                    IncludeLinks: true,
                    IncludeLayout: true,
                    IncludeMetadata: true,
                    IncludeNotes: true,
                    IncludeAssets: true,
                    Take: 250),
                cancellationToken),
            WorkflowProjectStructureOperation.ReadNode => await service.GetStructureAsync(
                RequireProjectId(settings, input),
                new ProjectStructureReadRequest(
                    NodeIds: [RequireNodeId(settings, input)],
                    IncludeLinks: true,
                    IncludeLayout: true,
                    IncludeMetadata: true,
                    IncludeNotes: true,
                    IncludeAssets: true),
                cancellationToken),
            WorkflowProjectStructureOperation.CreateAsset => await service.CreateAssetAsync(
                RequireProjectId(settings, input),
                BuildAssetRequest(settings, input),
                BuildAgentContext(input),
                cancellationToken),
            WorkflowProjectStructureOperation.CreateTaskNodes => await CreateTaskNodesAsync(
                service,
                settings,
                input,
                cancellationToken),
            _ => throw new InvalidOperationException($"Project-structure operation '{settings.Operation}' is not supported.")
        };

        return WorkflowExecutorJson.Result(context, result);
    }

    private static async Task<object> CreateTaskNodesAsync(
        ProjectStructureAgentService service,
        WorkflowProjectStructureExecutorSettings settings,
        WorkflowNodeInput input,
        CancellationToken cancellationToken)
    {
        var projectId = RequireProjectId(settings, input);
        var parentNodeId = ResolveOptionalNodeId(settings, input) ??
                           ResolveWorkflowParentNodeId(input) ??
                           throw new InvalidOperationException("Project-structure task creation requires 'NodeId', 'NodeIdJsonPath', or '$.runContext.workflowNodeId'.");
        var tasks = ReadTaskSources(settings, input);
        var createdNodes = new List<ProjectStructureNodeSummary>(tasks.Count);
        var agent = BuildAgentContext(input);

        foreach (var task in tasks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            createdNodes.Add(await service.CreateNodeAsync(
                projectId,
                new ProjectStructureNodeCreateInput(
                    ProjectObjectType.WorkItem,
                    task.Title,
                    BuildTaskSubtitle(task),
                    BuildTaskNotes(task),
                    parentNodeId,
                    EndUtc: task.DueUtc,
                    ObjectSubtype: NormalizeTaskSubtype(settings.TaskObjectSubtype),
                    MetadataJson: BuildTaskMetadataJson(task, input)),
                agent,
                cancellationToken));
        }

        return new
        {
            projectId,
            parentNodeId,
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

    private static ProjectStructureAssetCreateInput BuildAssetRequest(
        WorkflowProjectStructureExecutorSettings settings,
        WorkflowNodeInput input)
    {
        var objectType = settings.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
            ? ProjectObjectType.ImageAsset
            : ProjectObjectType.File;
        var title = Require(settings.Title, nameof(settings.Title));
        var sourcePath = string.IsNullOrWhiteSpace(settings.SourceWorkspacePath) ? null : settings.SourceWorkspacePath.Trim();
        ProjectObjectMediaPayload? media = null;
        var content = WorkflowInputPayloadText.Resolve(settings.Content, settings.ContentFromInput, input);

        if (sourcePath is null)
        {
            var bytes = Encoding.UTF8.GetBytes(content);
            media = new ProjectObjectMediaPayload(
                $"{SanitizeFileName(title)}.{NormalizeAssetKind(settings.AssetKind)}",
                settings.ContentType,
                Convert.ToBase64String(bytes));
        }

        return new ProjectStructureAssetCreateInput(
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
            SourceContentType: settings.ContentType);
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
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

    private static string NormalizeTaskSubtype(string value)
        => string.IsNullOrWhiteSpace(value) ? "task" : value.Trim().ToLowerInvariant();

    private static JsonElement ResolveInputJsonElement(
        WorkflowNodeInput input,
        string jsonPath,
        string settingName)
    {
        if (string.IsNullOrWhiteSpace(jsonPath))
        {
            throw new InvalidOperationException($"Project-structure executor setting '{settingName}' is required.");
        }

        if (!WorkflowRoutingValidation.TryParseJsonPath(jsonPath.Trim(), out var path, out var pathError))
        {
            throw new InvalidOperationException($"Project-structure executor setting '{settingName}' has invalid JSON path: {pathError}.");
        }

        if (string.IsNullOrWhiteSpace(input.PayloadJson))
        {
            throw new InvalidOperationException($"Project-structure executor setting '{settingName}' requires a workflow JSON payload.");
        }

        using var document = JsonDocument.Parse(input.PayloadJson);
        if (!TryResolve(document.RootElement, path, out var value))
        {
            throw new InvalidOperationException($"Project-structure executor setting '{settingName}' path '{jsonPath}' was not found in the workflow payload.");
        }

        return value.Clone();
    }

    private static string ReadRequiredTaskString(JsonElement element, string propertyName, int index)
    {
        var value = ReadOptionalString(element, propertyName);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Task item {index} requires non-empty '{propertyName}'.");
        }

        return value.Trim();
    }

    private static string ReadOptionalString(JsonElement element, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (!element.TryGetProperty(propertyName, out var property) ||
                property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                continue;
            }

            return property.ValueKind == JsonValueKind.String
                ? property.GetString() ?? string.Empty
                : property.GetRawText();
        }

        return string.Empty;
    }

    private static bool ReadOptionalBoolean(JsonElement element, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (!element.TryGetProperty(propertyName, out var property))
            {
                continue;
            }

            if (property.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                return property.GetBoolean();
            }

            if (property.ValueKind == JsonValueKind.String &&
                bool.TryParse(property.GetString(), out var parsed))
            {
                return parsed;
            }
        }

        return false;
    }

    private static DateTimeOffset? ReadOptionalDueUtc(JsonElement element, int index)
    {
        var value = ReadOptionalString(element, "dueUtc", "dueDateUtc", "dueDate");
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (DateTimeOffset.TryParse(value, out var parsed))
        {
            return parsed.ToUniversalTime();
        }

        throw new InvalidOperationException($"Task item {index} has invalid due date '{value}'.");
    }

    private static IReadOnlyList<string> ReadOptionalStringArray(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return property.EnumerateArray()
            .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() ?? string.Empty : item.GetRawText())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .ToArray();
    }

    private static Guid RequireProjectId(
        WorkflowProjectStructureExecutorSettings settings,
        WorkflowNodeInput input)
    {
        if (settings.ProjectId is { } projectId && projectId != Guid.Empty)
        {
            return projectId;
        }

        var rawProjectId = ResolveInputJsonString(input, settings.ProjectIdJsonPath, nameof(settings.ProjectIdJsonPath));
        if (Guid.TryParse(rawProjectId, out var parsed) && parsed != Guid.Empty)
        {
            return parsed;
        }

        if (TryResolveInputJsonString(input, "$.project.id", out rawProjectId) &&
            Guid.TryParse(rawProjectId, out parsed) &&
            parsed != Guid.Empty)
        {
            return parsed;
        }

        throw new InvalidOperationException("Project-structure executor setting 'ProjectId' or 'ProjectIdJsonPath' is required unless the workflow input includes '$.project.id'.");
    }

    private static string RequireNodeId(
        WorkflowProjectStructureExecutorSettings settings,
        WorkflowNodeInput input)
        => Require(ResolveOptionalNodeId(settings, input) ?? string.Empty, nameof(settings.NodeId));

    private static string? ResolveOptionalNodeId(
        WorkflowProjectStructureExecutorSettings settings,
        WorkflowNodeInput input)
    {
        if (!string.IsNullOrWhiteSpace(settings.NodeId))
        {
            return settings.NodeId.Trim();
        }

        return ResolveInputJsonString(input, settings.NodeIdJsonPath, nameof(settings.NodeIdJsonPath));
    }

    private static string? ResolveWorkflowParentNodeId(WorkflowNodeInput input)
        => TryResolveInputJsonString(input, "$.runContext.workflowNodeId", out var workflowNodeId) &&
           !string.IsNullOrWhiteSpace(workflowNodeId)
            ? workflowNodeId.Trim()
            : null;

    private static string? ResolveInputJsonString(
        WorkflowNodeInput input,
        string jsonPath,
        string settingName)
    {
        if (string.IsNullOrWhiteSpace(jsonPath))
        {
            return null;
        }

        if (!WorkflowRoutingValidation.TryParseJsonPath(jsonPath.Trim(), out var path, out var pathError))
        {
            throw new InvalidOperationException($"Project-structure executor setting '{settingName}' has invalid JSON path: {pathError}.");
        }

        if (string.IsNullOrWhiteSpace(input.PayloadJson))
        {
            throw new InvalidOperationException($"Project-structure executor setting '{settingName}' requires a workflow JSON payload.");
        }

        using var document = JsonDocument.Parse(input.PayloadJson);
        if (!TryResolve(document.RootElement, path, out var value))
        {
            throw new InvalidOperationException($"Project-structure executor setting '{settingName}' path '{jsonPath}' was not found in the workflow payload.");
        }

        return value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : value.GetRawText();
    }

    private static bool TryResolveInputJsonString(
        WorkflowNodeInput input,
        string jsonPath,
        out string? resolvedValue)
    {
        resolvedValue = null;
        if (string.IsNullOrWhiteSpace(jsonPath))
        {
            return false;
        }

        if (!WorkflowRoutingValidation.TryParseJsonPath(jsonPath.Trim(), out var path, out var pathError))
        {
            throw new InvalidOperationException($"Project-structure executor has invalid JSON path '{jsonPath}': {pathError}.");
        }

        if (string.IsNullOrWhiteSpace(input.PayloadJson))
        {
            return false;
        }

        using var document = JsonDocument.Parse(input.PayloadJson);
        if (!TryResolve(document.RootElement, path, out var value))
        {
            return false;
        }

        resolvedValue = value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : value.GetRawText();
        return true;
    }

    private static bool TryResolve(
        JsonElement root,
        IReadOnlyList<BuiltInJsonPathSegment> path,
        out JsonElement value)
    {
        value = root;
        foreach (var segment in path)
        {
            if (segment.PropertyName is not null)
            {
                if (value.ValueKind != JsonValueKind.Object ||
                    !value.TryGetProperty(segment.PropertyName, out value))
                {
                    return false;
                }

                continue;
            }

            if (segment.Index is not { } targetIndex || value.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            var currentIndex = 0;
            var matched = false;
            foreach (var item in value.EnumerateArray())
            {
                if (currentIndex == targetIndex)
                {
                    value = item;
                    matched = true;
                    break;
                }

                currentIndex++;
            }

            if (!matched)
            {
                return false;
            }
        }

        return true;
    }

    private static ProjectStructureAgentContext BuildAgentContext(WorkflowNodeInput input)
    {
        var fallback = new ProjectStructureAgentContext(
            "workflow-executor",
            "Workflow executor",
            Environment.MachineName,
            string.Empty,
            string.Empty,
            Guid.NewGuid().ToString("N"));

        return string.IsNullOrWhiteSpace(ReadRunContextString(input, "agentId"))
            ? fallback
            : new ProjectStructureAgentContext(
                ReadRunContextString(input, "agentId"),
                ReadRunContextString(input, "agentName", fallback.AgentName),
                ReadRunContextString(input, "machineName", fallback.MachineName),
                ReadRunContextString(input, "repositoryRoot", fallback.RepositoryRoot),
                ReadRunContextString(input, "branchName", fallback.BranchName),
                ReadRunContextString(input, "sessionId", fallback.SessionId));
    }

    private static string ReadRunContextString(
        WorkflowNodeInput input,
        string propertyName,
        string fallback = "")
        => TryResolveInputJsonString(input, $"$.runContext.{propertyName}", out var value) &&
           !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : fallback;

    private static string NormalizeAssetKind(string value)
        => string.IsNullOrWhiteSpace(value) ? "md" : value.Trim().TrimStart('.').ToLowerInvariant();

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var sanitized = new string(value.Select(character => invalid.Contains(character) ? '-' : character).ToArray()).Trim('-');
        return string.IsNullOrWhiteSpace(sanitized) ? "asset" : sanitized;
    }

    private static string Require(string value, string name)
        => string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"Project-structure executor setting '{name}' is required.")
            : value.Trim();

    private sealed record WorkflowTaskNodeSource(
        string Title,
        string Summary,
        string Owner,
        DateTimeOffset? DueUtc,
        string Urgency,
        bool RequiresResponse,
        bool Asap,
        string SourceEmailId,
        IReadOnlyList<string> Evidence);
}

