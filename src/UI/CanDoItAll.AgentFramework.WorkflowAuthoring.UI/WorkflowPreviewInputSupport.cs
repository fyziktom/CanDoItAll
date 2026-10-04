using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Definitions;

namespace CanDoItAll.AgentFramework.WorkflowAuthoring.UI;

public static class WorkflowPreviewInputSupport {
    public const string DefaultInputJson = "{\"prompt\":\"Summarize this workflow input.\"}";
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

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

        var needsObjectInput = state.Requirements.NeedsProjectContext || !string.IsNullOrWhiteSpace(state.ParentNodeId);
        var root = rootNode as JsonObject;
        if (needsObjectInput && root is null)
        {
            error = "Preview input must be a JSON object when the workflow needs project or parent-node context.";
            return false;
        }

        if (state.Requirements.NeedsProjectContext)
        {
            if (!Guid.TryParse(state.ProjectId, out var projectId) || projectId == Guid.Empty)
            {
                error = "Choose a project or paste a valid project id before running this preview.";
                return false;
            }

            root!["projectId"] = projectId.ToString("D");
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
            root!["nodeId"] = nodeId;
            var runContext = EnsureObject(root, "runContext", out error);
            if (runContext is null)
            {
                return false;
            }

            runContext["workflowNodeId"] = nodeId;
        }

        inputJson = (root ?? rootNode)?.ToJsonString(JsonOptions) ?? "{}";
        return true;
    }

    public static WorkflowPreviewSimulationPlan BuildSimulationPlan(
        WorkflowPreviewInputState state)
    {
        var steps = state.Requirements.SimulationRequirements
            .Where(requirement => state.SimulatedNodeIds.Contains(requirement.NodeId.Value))
            .Select(requirement => new WorkflowPreviewSimulationStep(
                requirement.NodeId,
                requirement.SourceExecutorId,
                requirement.Description,
                requirement.OutputTemplateJson))
            .ToArray();

        return steps.Length == 0
            ? WorkflowPreviewSimulationPlan.Empty
            : new WorkflowPreviewSimulationPlan(steps);
    }

    public static string BuildRequirementSummary(WorkflowPreviewRequirements requirements)
    {
        if (!requirements.NeedsProjectContext && requirements.SimulationRequirements.Count == 0)
        {
            return "This workflow can run with the JSON input below.";
        }

        var summaryParts = new List<string>();
        if (requirements.NeedsProjectContext)
        {
            var nodes = string.Join(", ", requirements.ProjectRequirements
                .Where(requirement => requirement.Operation != WorkflowProjectStructureOperation.ListProjects)
                .Select(requirement => $"{requirement.NodeName} ({requirement.Operation})"));
            summaryParts.Add($"This workflow uses project-structure step(s): {nodes}. Provide project context before preview starts.");
        }

        if (requirements.SimulationRequirements.Count > 0)
        {
            summaryParts.Add("You can simulate selected steps so preview runs avoid external mutations while preserving downstream payload shape.");
        }

        return string.Join(" ", summaryParts);
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
