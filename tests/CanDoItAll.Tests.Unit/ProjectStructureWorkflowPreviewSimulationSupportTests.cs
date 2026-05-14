using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Workbench;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectStructureWorkflowPreviewSimulationSupportTests
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    [Fact]
    public void Analyze_returns_generic_project_structure_write_options()
    {
        var definition = CreateDefinition([
            CreateNode("start", WorkflowNodeKind.Start),
            CreateProjectStructureNode("create-summary", WorkflowProjectStructureOperation.CreateAsset),
            CreateProjectStructureNode("create-tasks", WorkflowProjectStructureOperation.CreateTaskNodes),
            CreateProjectStructureNode("read-tree", WorkflowProjectStructureOperation.ReadTree),
            CreateNode("end", WorkflowNodeKind.End)
        ]);

        var options = ProjectStructureWorkflowPreviewSimulationSupport.Analyze(definition);

        Assert.Equal(2, options.Count);
        Assert.Contains(options, option => option.NodeId == "create-summary" && option.Operation == WorkflowProjectStructureOperation.CreateAsset);
        Assert.Contains(options, option => option.NodeId == "create-tasks" && option.Operation == WorkflowProjectStructureOperation.CreateTaskNodes);
    }

    [Fact]
    public void BuildPlan_rejects_non_write_project_structure_node_ids()
    {
        var definition = CreateDefinition([
            CreateNode("start", WorkflowNodeKind.Start),
            CreateProjectStructureNode("read-tree", WorkflowProjectStructureOperation.ReadTree),
            CreateNode("end", WorkflowNodeKind.End)
        ]);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ProjectStructureWorkflowPreviewSimulationSupport.BuildPlan(definition, ["read-tree"]));

        Assert.Contains("not skippable", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildPlan_creates_preview_steps_for_selected_write_nodes()
    {
        var definition = CreateDefinition([
            CreateNode("start", WorkflowNodeKind.Start),
            CreateProjectStructureNode("create-summary", WorkflowProjectStructureOperation.CreateAsset),
            CreateProjectStructureNode("create-tasks", WorkflowProjectStructureOperation.CreateTaskNodes),
            CreateNode("end", WorkflowNodeKind.End)
        ]);

        var plan = ProjectStructureWorkflowPreviewSimulationSupport.BuildPlan(
            definition,
            ["create-summary", "create-tasks"]);

        Assert.Equal(2, plan.Steps.Count);
        Assert.All(plan.Steps, step => Assert.Equal(WorkflowExecutorIds.ProjectStructure, step.SourceExecutorId));
        Assert.Contains(plan.Steps, step => step.NodeId.Value == "create-summary" &&
                                           step.OutputTemplateJson.Contains("preview-asset", StringComparison.Ordinal) &&
                                           step.OutputTemplateJson.Contains("{{inputPath:$.runContext.workflowNodeId}}", StringComparison.Ordinal));
        Assert.Contains(plan.Steps, step => step.NodeId.Value == "create-tasks" &&
                                           step.OutputTemplateJson.Contains("preview-task", StringComparison.Ordinal) &&
                                           step.OutputTemplateJson.Contains("{{inputPath:$.project.id}}", StringComparison.Ordinal) &&
                                           step.OutputTemplateJson.Contains("{{inputPath:$.runContext.workflowNodeId}}", StringComparison.Ordinal));
    }

    private static WorkflowDefinition CreateDefinition(IReadOnlyList<WorkflowNode> nodes)
        => new(
            WorkflowId.New(),
            WorkflowVersionId.New(),
            "Project Structure Preview Simulation Test",
            "Test definition.",
            WorkflowLifecycleStatus.Active,
            new WorkflowGraph(
                new WorkflowNodeId("start"),
                nodes,
                []),
            new WorkflowRuntimePolicy(
                WorkflowRuntimeBackendKind.InProcess,
                AllowInProcessPreviewRuns: true,
                RequireDurableProductionRuns: false,
                ExposeAzureFunctionsStatusEndpoint: false,
                ExposeAzureFunctionsMcpTool: false),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

    private static WorkflowNode CreateProjectStructureNode(
        string id,
        WorkflowProjectStructureOperation operation)
        => CreateNode(id, WorkflowNodeKind.Executor) with
        {
            Settings = new WorkflowNodeSettings(
                ComponentId: null,
                AgentId: null,
                SubworkflowId: null,
                ExternalRequestKind: null,
                Instructions: string.Empty,
                InputShape: WorkflowValueShape.Text,
                ResultShape: new WorkflowValueShape(WorkflowValueShapeKind.Json, "{}", "JSON"))
            {
                ExecutorId = WorkflowExecutorIds.ProjectStructure,
                ExecutorSettingsJson = JsonSerializer.Serialize(
                    new WorkflowProjectStructureExecutorSettings { Operation = operation },
                    JsonOptions)
            }
        };

    private static WorkflowNode CreateNode(
        string id,
        WorkflowNodeKind kind)
        => new(
            new WorkflowNodeId(id),
            kind,
            id,
            [],
            new WorkflowNodeSettings(
                ComponentId: null,
                AgentId: null,
                SubworkflowId: null,
                ExternalRequestKind: null,
                Instructions: string.Empty,
                InputShape: WorkflowValueShape.Text,
                ResultShape: WorkflowValueShape.Text));

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
