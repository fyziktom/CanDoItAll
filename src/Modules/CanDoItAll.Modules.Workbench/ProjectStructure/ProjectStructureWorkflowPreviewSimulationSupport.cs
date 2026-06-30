using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.Workbench;

internal static class ProjectStructureWorkflowPreviewSimulationSupport
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private const string CreateAssetDescription = "Skip project-structure asset creation and keep the step input as preview output.";
    private const string CreateTaskNodesDescription = "Skip project-structure task-node creation and keep the step input as preview output.";

    private const string CreateAssetTemplateJson = """
        {
          "result": {
            "id": "preview-asset-{{node.id}}",
            "parentId": "{{inputPath:$.runContext.workflowNodeId}}",
            "objectType": "File",
            "objectSubtype": "md",
            "title": "Simulated project-structure asset",
            "summary": "Project-structure asset creation was skipped for this Run Preview.",
            "simulated": true
          },
          "inputPayload": "{{inputPayload}}",
          "simulation": {
            "nodeId": "{{node.id}}",
            "nodeName": "{{node.name}}",
            "sourceExecutorId": "{{source.executor.id}}",
            "reason": "{{simulation.reason}}",
            "generatedAtUtc": "{{utcNow}}"
          }
        }
        """;

    private const string CreateTaskNodesTemplateJson = """
        {
          "result": {
            "projectId": "{{inputPath:$.project.id}}",
            "parentNodeId": "{{inputPath:$.runContext.workflowNodeId}}",
            "createdTaskCount": 1,
            "createdNodeIds": [
              "preview-task-{{node.id}}"
            ],
            "createdNodes": [
              {
                "id": "preview-task-{{node.id}}",
                "parentId": "{{inputPath:$.runContext.workflowNodeId}}",
                "objectType": "WorkItem",
                "objectSubtype": "task",
                "title": "Simulated project-structure task",
                "subtitle": "Run Preview simulation",
                "endUtc": null
              }
            ],
            "simulated": true
          },
          "inputPayload": "{{inputPayload}}",
          "simulation": {
            "nodeId": "{{node.id}}",
            "nodeName": "{{node.name}}",
            "sourceExecutorId": "{{source.executor.id}}",
            "reason": "{{simulation.reason}}",
            "generatedAtUtc": "{{utcNow}}"
          }
        }
        """;

    public static IReadOnlyList<ProjectStructureWorkflowPreviewSimulationOption> Analyze(
        WorkflowDefinition definition)
        => definition.Graph.Nodes
            .Select(TryCreateOption)
            .Where(option => option is not null)
            .Select(option => option!)
            .ToArray();

    public static WorkflowPreviewSimulationPlan BuildPlan(
        WorkflowDefinition definition,
        IReadOnlyList<string>? simulatedNodeIds)
    {
        if (simulatedNodeIds is null || simulatedNodeIds.Count == 0)
        {
            return WorkflowPreviewSimulationPlan.Empty;
        }

        var requestedIds = simulatedNodeIds
            .Where(nodeId => !string.IsNullOrWhiteSpace(nodeId))
            .Select(nodeId => nodeId.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (requestedIds.Count == 0)
        {
            return WorkflowPreviewSimulationPlan.Empty;
        }

        var optionsByNodeId = Analyze(definition).ToDictionary(option => option.NodeId, StringComparer.OrdinalIgnoreCase);
        var invalidIds = requestedIds
            .Where(nodeId => !optionsByNodeId.ContainsKey(nodeId))
            .OrderBy(nodeId => nodeId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (invalidIds.Length > 0)
        {
            throw new InvalidOperationException(
                $"Workflow preview simulation node id(s) are not skippable project-structure write steps: {string.Join(", ", invalidIds)}.");
        }

        var steps = requestedIds
            .Select(nodeId => optionsByNodeId[nodeId])
            .Select(option => new WorkflowPreviewSimulationStep(
                new WorkflowNodeId(option.NodeId),
                new WorkflowExecutorId(option.ExecutorId),
                option.Description,
                ResolveTemplateJson(option.Operation)))
            .ToArray();
        return steps.Length == 0
            ? WorkflowPreviewSimulationPlan.Empty
            : new WorkflowPreviewSimulationPlan(steps);
    }

    private static ProjectStructureWorkflowPreviewSimulationOption? TryCreateOption(
        WorkflowNode node)
    {
        if (node.Settings.ExecutorId != WorkflowExecutorIds.ProjectStructure)
        {
            return null;
        }

        var settings = string.IsNullOrWhiteSpace(node.Settings.ExecutorSettingsJson)
            ? new WorkflowProjectStructureExecutorSettings()
            : JsonSerializer.Deserialize<WorkflowProjectStructureExecutorSettings>(
                node.Settings.ExecutorSettingsJson,
                JsonOptions) ?? new WorkflowProjectStructureExecutorSettings();
        if (settings.Operation is not (WorkflowProjectStructureOperation.CreateAsset or WorkflowProjectStructureOperation.CreateTaskNodes))
        {
            return null;
        }

        return new ProjectStructureWorkflowPreviewSimulationOption(
            node.Id.Value,
            node.Name,
            WorkflowExecutorIds.ProjectStructure.Value,
            settings.Operation,
            ResolveDescription(settings.Operation));
    }

    private static string ResolveDescription(WorkflowProjectStructureOperation operation)
        => operation switch
        {
            WorkflowProjectStructureOperation.CreateTaskNodes => CreateTaskNodesDescription,
            _ => CreateAssetDescription
        };

    private static string ResolveTemplateJson(WorkflowProjectStructureOperation operation)
        => operation switch
        {
            WorkflowProjectStructureOperation.CreateTaskNodes => CreateTaskNodesTemplateJson,
            _ => CreateAssetTemplateJson
        };

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
