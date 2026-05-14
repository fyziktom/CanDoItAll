using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.AgentFramework.Pages;

internal sealed record WorkflowPreviewProjectRequirement(
    WorkflowNodeId NodeId,
    string NodeName,
    WorkflowProjectStructureOperation Operation,
    bool IsWriteOperation);

internal sealed record WorkflowPreviewRequirements(
    IReadOnlyList<WorkflowPreviewProjectRequirement> ProjectRequirements)
{
    public static WorkflowPreviewRequirements Empty { get; } = new([]);

    public bool NeedsProjectContext => ProjectRequirements.Any(requirement =>
        requirement.Operation != WorkflowProjectStructureOperation.ListProjects);

    public bool HasProjectStructureWrites => ProjectRequirements.Any(requirement => requirement.IsWriteOperation);
}

internal sealed class WorkflowPreviewInputState
{
    public string InputJson { get; set; } = WorkflowPreviewInputSupport.DefaultInputJson;

    public string ProjectId { get; set; } = string.Empty;

    public string ParentNodeId { get; set; } = string.Empty;

    public bool SkipProjectStructureWrites { get; set; }

    public WorkflowPreviewRequirements Requirements { get; set; } = WorkflowPreviewRequirements.Empty;

    public string ProjectLoadError { get; set; } = string.Empty;
}

internal static class WorkflowPreviewInputSupport
{
    public const string DefaultInputJson = "{\"prompt\":\"Summarize this workflow input.\"}";

    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public static WorkflowPreviewRequirements Analyze(WorkflowDefinition definition)
    {
        var requirements = definition.Graph.Nodes
            .Where(node => node.Settings.ExecutorId == WorkflowExecutorIds.ProjectStructure)
            .Select(CreateProjectRequirement)
            .Where(requirement => requirement is not null)
            .Select(requirement => requirement!)
            .ToArray();

        return requirements.Length == 0
            ? WorkflowPreviewRequirements.Empty
            : new WorkflowPreviewRequirements(requirements);
    }

    public static bool TryBuildInputJson(
        WorkflowPreviewInputState state,
        out string inputJson,
        out string error)
    {
        inputJson = string.Empty;
        error = string.Empty;

        JsonNode? rootNode;
        try
        {
            rootNode = JsonNode.Parse(string.IsNullOrWhiteSpace(state.InputJson) ? "{}" : state.InputJson);
        }
        catch (JsonException exception)
        {
            error = $"Preview input JSON is invalid: {exception.Message}";
            return false;
        }

        var root = rootNode as JsonObject;
        if (root is null)
        {
            error = "Preview input must be a JSON object when the workflow needs project context.";
            return false;
        }

        if (state.Requirements.NeedsProjectContext)
        {
            if (!Guid.TryParse(state.ProjectId, out var projectId) || projectId == Guid.Empty)
            {
                error = "Choose a project or paste a valid project id before running this preview.";
                return false;
            }

            root["projectId"] = projectId.ToString("D");
            var project = EnsureObject(root, "project", out error);
            if (project is null)
            {
                return false;
            }

            project["id"] = projectId.ToString("D");
        }

        if (!string.IsNullOrWhiteSpace(state.ParentNodeId))
        {
            var nodeId = state.ParentNodeId.Trim();
            root["nodeId"] = nodeId;
            var runContext = EnsureObject(root, "runContext", out error);
            if (runContext is null)
            {
                return false;
            }

            runContext["workflowNodeId"] = nodeId;
        }

        inputJson = root.ToJsonString(JsonOptions);
        return true;
    }

    public static WorkflowDefinition ApplyPreviewOptions(
        WorkflowDefinition definition,
        WorkflowPreviewInputState state)
    {
        if (!state.SkipProjectStructureWrites || !state.Requirements.HasProjectStructureWrites)
        {
            return definition;
        }

        var skippedNodeIds = state.Requirements.ProjectRequirements
            .Where(requirement => requirement.IsWriteOperation)
            .Select(requirement => requirement.NodeId)
            .ToHashSet();
        var nodes = definition.Graph.Nodes
            .Select(node => skippedNodeIds.Contains(node.Id) ? CreateSkippedNode(node) : node)
            .ToArray();

        return definition with
        {
            Graph = definition.Graph with
            {
                Nodes = nodes
            }
        };
    }

    public static string BuildRequirementSummary(WorkflowPreviewRequirements requirements)
    {
        if (!requirements.NeedsProjectContext)
        {
            return "This workflow can run with the JSON input below.";
        }

        var nodes = string.Join(", ", requirements.ProjectRequirements
            .Where(requirement => requirement.Operation != WorkflowProjectStructureOperation.ListProjects)
            .Select(requirement => $"{requirement.NodeName} ({requirement.Operation})"));
        return $"This workflow uses project-structure step(s): {nodes}. Provide project context before preview starts.";
    }

    public static string? TryReadJsonString(
        string json,
        string jsonPath)
    {
        if (string.IsNullOrWhiteSpace(json) ||
            !WorkflowRoutingValidation.TryParseJsonPath(jsonPath, out var path, out _))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var value = document.RootElement;
            foreach (var segment in path)
            {
                if (segment.PropertyName is not null)
                {
                    if (value.ValueKind != JsonValueKind.Object ||
                        !value.TryGetProperty(segment.PropertyName, out value))
                    {
                        return null;
                    }

                    continue;
                }

                if (segment.Index is not { } index ||
                    value.ValueKind != JsonValueKind.Array ||
                    index < 0 ||
                    index >= value.GetArrayLength())
                {
                    return null;
                }

                value = value[index];
            }

            return value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : value.GetRawText();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static WorkflowPreviewProjectRequirement? CreateProjectRequirement(WorkflowNode node)
    {
        WorkflowProjectStructureExecutorSettings settings;
        try
        {
            settings = string.IsNullOrWhiteSpace(node.Settings.ExecutorSettingsJson)
                ? new WorkflowProjectStructureExecutorSettings()
                : JsonSerializer.Deserialize<WorkflowProjectStructureExecutorSettings>(node.Settings.ExecutorSettingsJson, JsonOptions) ?? new WorkflowProjectStructureExecutorSettings();
        }
        catch (JsonException)
        {
            settings = new WorkflowProjectStructureExecutorSettings();
        }

        return new WorkflowPreviewProjectRequirement(
            node.Id,
            node.Name,
            settings.Operation,
            settings.Operation is WorkflowProjectStructureOperation.CreateAsset or WorkflowProjectStructureOperation.CreateTaskNodes);
    }

    private static WorkflowNode CreateSkippedNode(WorkflowNode node)
        => node with
        {
            Kind = WorkflowNodeKind.StrictLogic,
            Name = $"{node.Name} (skipped)",
            Settings = new WorkflowNodeSettings(
                ComponentId: null,
                AgentId: null,
                SubworkflowId: null,
                ExternalRequestKind: null,
                Instructions: $"Project-structure write step '{node.Id}' was skipped for this preview.",
                InputShape: node.Settings.InputShape,
                ResultShape: node.Settings.ResultShape)
        };

    private static JsonObject? EnsureObject(
        JsonObject root,
        string propertyName,
        out string error)
    {
        error = string.Empty;
        if (!root.TryGetPropertyValue(propertyName, out var existing) || existing is null)
        {
            var created = new JsonObject();
            root[propertyName] = created;
            return created;
        }

        if (existing is JsonObject existingObject)
        {
            return existingObject;
        }

        error = $"Preview input property '{propertyName}' must be a JSON object.";
        return null;
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
