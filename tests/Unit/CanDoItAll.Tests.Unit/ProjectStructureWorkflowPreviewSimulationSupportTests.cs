using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Builder;
using CanDoItAll.AgentFramework.Workflows.Templates;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Workbench;

namespace CanDoItAll.Tests.Unit.Projects;

public sealed class ProjectStructureWorkflowPreviewSimulationSupportTests
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    [Fact]
    public void Failed_workflow_status_message_prefers_typed_event_diagnostic()
    {
        var diagnostic = WorkflowFixtureFactory.CreateExecutorFailureDiagnostic(
            new WorkflowNodeId("store-project"),
            WorkflowExecutorIds.ProjectStructure,
            "corr-workbench-status");
        var payloadJson = WorkflowEventPayloads.Serialize(
            WorkflowEventPayloadSource.Runtime,
            "WorkflowExecutorFailed",
            nodeId: new WorkflowNodeId("store-project"),
            executorId: WorkflowExecutorIds.ProjectStructure,
            inlineJson: WorkflowRuntimeFailureDiagnosticMapper.Serialize(diagnostic));
        var events = new[]
        {
            new WorkflowEventRecord(
                Guid.NewGuid(),
                WorkflowRunId.New(),
                WorkflowEventKind.ExecutorFailed,
                new WorkflowNodeId("store-project"),
                "Project-structure executor leaked raw-token-value.",
                payloadJson,
                DateTimeOffset.UtcNow)
        };

        var message = ProjectStructureWorkflowNodeService.ResolveWorkflowStatusMessage(
            WorkflowRunState.Failed,
            "Raw run summary with raw-token-value.",
            events);

        Assert.Contains("store-project", message, StringComparison.Ordinal);
        Assert.Contains(WorkflowExecutorIds.ProjectStructure.Value, message, StringComparison.Ordinal);
        Assert.Contains("Fix the executor settings JSON", message, StringComparison.Ordinal);
        Assert.DoesNotContain("raw-token-value", message, StringComparison.Ordinal);
    }

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
    public void Analyze_returns_office365_default_workflow_store_skip_option()
    {
        var pack = new WorkflowTemplatePackLoader().Load();
        var template = Assert.Single(pack.Workflows, item => item.Key == "office365-category-email-summary-to-project");
        var now = DateTimeOffset.UtcNow;
        var definition = new WorkflowDefinition(
            WorkflowId.New(),
            WorkflowVersionId.New(),
            template.Name,
            template.Description,
            WorkflowLifecycleStatus.Active,
            pack.CreateGraph(template, WorkflowComponentId.New()),
            pack.RuntimePolicy,
            now,
            now);

        var options = ProjectStructureWorkflowPreviewSimulationSupport.Analyze(definition);

        Assert.Contains(options, option =>
            option.NodeId == "store-office365-summary" &&
            option.NodeName == "Store Office365 summary" &&
            option.Operation == WorkflowProjectStructureOperation.CreateAsset);
    }

    [Fact]
    public void Default_template_pack_loads_file_backed_workflow_examples()
    {
        var pack = new WorkflowTemplatePackLoader().Load();

        AssertTemplateGraph(pack, "gmail-label-email-summary-to-project");
        AssertTemplateGraph(pack, "office365-category-email-summary-to-project");
        var office365WatchSummary = AssertTemplateGraph(pack, "office365-email-watch-summary-to-project");
        var office365WatchTasks = AssertTemplateGraph(pack, "office365-email-watch-tasks-to-project");
        var gmailTasks = AssertTemplateGraph(pack, "gmail-label-email-tasks-to-project");
        var officeTasks = AssertTemplateGraph(pack, "office365-category-email-tasks-to-project");
        var mermaid = AssertTemplateGraph(pack, "file-to-mermaid-graph-asset");
        var sourceCode = AssertTemplateGraph(pack, "source-code-file-summary-to-project");

        AssertOffice365WatchDownloadSettings(office365WatchSummary, "download-office365-watch");
        AssertOffice365WatchDownloadSettings(office365WatchTasks, "download-office365-watch");
        AssertProjectStructureOperation(
            office365WatchSummary,
            "store-office365-watch-summary",
            WorkflowProjectStructureOperation.CreateAsset,
            includeInputPayload: true,
            idempotencyKeySuffix: "summary");
        AssertProjectStructureOperation(
            office365WatchTasks,
            "create-office365-watch-task-nodes",
            WorkflowProjectStructureOperation.CreateTaskNodes,
            includeInputPayload: true,
            idempotencyKeySuffix: "tasks");
        AssertProjectStructureOperation(
            office365WatchTasks,
            "store-office365-watch-no-task-summary",
            WorkflowProjectStructureOperation.CreateAsset,
            includeInputPayload: true,
            idempotencyKeySuffix: "tasks");
        AssertOffice365WatchMarkSettings(office365WatchSummary, "mark-office365-watch-summary-processed");
        AssertOffice365WatchMarkSettings(office365WatchTasks, "mark-office365-watch-tasks-processed");
        AssertEdge(office365WatchSummary, "store-office365-watch-summary", "mark-office365-watch-summary-processed");
        AssertEdge(office365WatchTasks, "create-office365-watch-task-nodes", "mark-office365-watch-tasks-processed");
        AssertEdge(office365WatchTasks, "store-office365-watch-no-task-summary", "mark-office365-watch-tasks-processed");
        AssertNoMessageBranch(office365WatchSummary, "office365-watch-message-switch", "compact-office365-summary-no-message", "summarize-office365-watch");
        AssertNoMessageBranch(office365WatchTasks, "office365-watch-message-switch", "compact-office365-tasks-no-message", "extract-office365-watch-tasks");

        Assert.Contains(gmailTasks.Nodes, node => node.Settings.ExecutorId == new WorkflowExecutorId("gmail.messages-by-label"));
        Assert.Contains(gmailTasks.Nodes, node => node.Settings.ExecutorId == new WorkflowExecutorId("gmail.mark-message-processed"));
        AssertProjectStructureOperation(gmailTasks, "create-gmail-task-nodes", WorkflowProjectStructureOperation.CreateTaskNodes, includeInputPayload: true);
        AssertProjectStructureOperation(gmailTasks, "store-gmail-no-task-summary", WorkflowProjectStructureOperation.CreateAsset, includeInputPayload: true);

        Assert.Contains(officeTasks.Nodes, node => node.Settings.ExecutorId == new WorkflowExecutorId("office365.messages-by-category"));
        Assert.Contains(officeTasks.Nodes, node => node.Settings.ExecutorId == new WorkflowExecutorId("office365.mark-message-processed"));
        AssertProjectStructureOperation(officeTasks, "create-office365-task-nodes", WorkflowProjectStructureOperation.CreateTaskNodes, includeInputPayload: true);
        AssertProjectStructureOperation(officeTasks, "store-office365-no-task-summary", WorkflowProjectStructureOperation.CreateAsset, includeInputPayload: true);

        AssertSourceIngestionAllows(mermaid, "ingest-graph-sources", ".cs", ".razor", ".ts", ".md");
        AssertProjectStructureOperation(mermaid, "store-mermaid-asset", WorkflowProjectStructureOperation.CreateAsset, includeInputPayload: true);

        AssertSourceIngestionAllows(sourceCode, "ingest-code-sources", ".cs", ".razor", ".tsx", ".py");
        AssertProjectStructureOperation(sourceCode, "store-code-summary", WorkflowProjectStructureOperation.CreateAsset, includeInputPayload: true);
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

    private static WorkflowGraph AssertTemplateGraph(
        WorkflowTemplatePack pack,
        string key)
    {
        var template = Assert.Single(pack.Workflows, item => item.Key == key);
        var graph = pack.CreateGraph(template, WorkflowComponentId.New());

        Assert.NotEmpty(graph.Nodes);
        Assert.NotEmpty(graph.Edges);
        return graph;
    }

    private static void AssertProjectStructureOperation(
        WorkflowGraph graph,
        string nodeId,
        WorkflowProjectStructureOperation operation,
        bool includeInputPayload,
        string? idempotencyKeySuffix = null)
    {
        var node = Assert.Single(graph.Nodes, item => item.Id.Value == nodeId);
        Assert.Equal(WorkflowExecutorIds.ProjectStructure, node.Settings.ExecutorId);

        var settings = JsonSerializer.Deserialize<WorkflowProjectStructureExecutorSettings>(
            node.Settings.ExecutorSettingsJson,
            JsonOptions);

        Assert.NotNull(settings);
        Assert.Equal(operation, settings.Operation);
        Assert.Equal(includeInputPayload, settings.IncludeInputPayload);

        if (idempotencyKeySuffix is not null)
        {
            Assert.Equal("$.runContext.office365Processing.idempotencyKey", settings.IdempotencyKeyJsonPath);
            Assert.Equal(idempotencyKeySuffix, settings.IdempotencyKeySuffix);
        }
    }

    private static void AssertOffice365WatchDownloadSettings(
        WorkflowGraph graph,
        string nodeId)
    {
        var node = AssertSingleExecutorNode(graph, nodeId, "office365.message-by-address-unprocessed");
        using var document = JsonDocument.Parse(node.Settings.ExecutorSettingsJson);
        var root = document.RootElement;

        Assert.Equal("$.connectionId", root.GetProperty("connectionIdJsonPath").GetString());
        Assert.Equal("$.emailAddress", root.GetProperty("emailAddressJsonPath").GetString());
        Assert.Equal(string.Empty, root.GetProperty("processedCategory").GetString());
        Assert.Equal("$.processedCategory", root.GetProperty("processedCategoryJsonPath").GetString());
        Assert.Equal("$.lookbackHours", root.GetProperty("lookbackHoursJsonPath").GetString());
        Assert.Equal("SuccessNoMessages", root.GetProperty("noMessageBehavior").GetString());
    }

    private static void AssertOffice365WatchMarkSettings(
        WorkflowGraph graph,
        string nodeId)
    {
        var node = AssertSingleExecutorNode(graph, nodeId, "office365.mark-message-processed");
        using var document = JsonDocument.Parse(node.Settings.ExecutorSettingsJson);
        var root = document.RootElement;

        Assert.Equal("$.inputPayload.runContext.office365Processing.connectionId", root.GetProperty("connectionIdJsonPath").GetString());
        Assert.Equal(string.Empty, root.GetProperty("sourceCategory").GetString());
        Assert.Equal(string.Empty, root.GetProperty("processedCategory").GetString());
        Assert.Equal("$.inputPayload.runContext.office365Processing.processedCategory", root.GetProperty("processedCategoryJsonPath").GetString());
        Assert.Equal("$.inputPayload.runContext.office365Processing.selectedMessageId", root.GetProperty("messageIdJsonPath").GetString());
    }

    private static WorkflowNode AssertSingleExecutorNode(
        WorkflowGraph graph,
        string nodeId,
        string executorId)
    {
        var node = Assert.Single(graph.Nodes, item => item.Id.Value == nodeId);
        Assert.Equal(new WorkflowExecutorId(executorId), node.Settings.ExecutorId);
        return node;
    }

    private static void AssertEdge(
        WorkflowGraph graph,
        string sourceNodeId,
        string targetNodeId)
        => Assert.Contains(
            graph.Edges,
            edge => edge.SourceNodeId.Value == sourceNodeId &&
                    edge.TargetNodeId.Value == targetNodeId);

    private static void AssertNoMessageBranch(
        WorkflowGraph graph,
        string switchNodeId,
        string noMessageTargetNodeId,
        string messageTargetNodeId)
    {
        Assert.Contains(
            graph.Edges,
            edge => edge.SourceNodeId.Value == switchNodeId &&
                    edge.TargetNodeId.Value == noMessageTargetNodeId &&
                    edge.Routing.Kind == WorkflowRouteKind.SwitchCase &&
                    edge.Routing.ExpectedValueJson == "\"no_messages\"");
        Assert.Contains(
            graph.Edges,
            edge => edge.SourceNodeId.Value == switchNodeId &&
                    edge.TargetNodeId.Value == messageTargetNodeId &&
                    edge.Routing.Kind == WorkflowRouteKind.SwitchDefault);
    }

    private static void AssertSourceIngestionAllows(
        WorkflowGraph graph,
        string nodeId,
        params string[] extensions)
    {
        var node = Assert.Single(graph.Nodes, item => item.Id.Value == nodeId);
        Assert.Equal(WorkflowExecutorIds.SourceIngestion, node.Settings.ExecutorId);

        var settings = JsonSerializer.Deserialize<WorkflowSourceIngestionExecutorSettings>(
            node.Settings.ExecutorSettingsJson,
            JsonOptions);

        Assert.NotNull(settings);
        foreach (var extension in extensions)
        {
            Assert.Contains(extension, settings.AllowedExtensions, StringComparer.OrdinalIgnoreCase);
        }
    }

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
