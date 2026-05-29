using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;

namespace CanDoItAll.Tests.Unit;

public sealed class WorkflowTemplatePackLoaderTests
{
    [Fact]
    public void Load_default_pack_includes_workflow_executor_catalog_examples()
    {
        var pack = new WorkflowTemplatePackLoader().Load();
        var templates = pack.Workflows.ToDictionary(template => template.Key, StringComparer.OrdinalIgnoreCase);

        Assert.Contains("local-folder-summary-markdown-report", templates.Keys);
        Assert.Contains("file-diff-markdown-report", templates.Keys);
        Assert.Contains("http-download-document-extraction-report", templates.Keys);
        Assert.Contains("json-transform-project-task-creation", templates.Keys);
        Assert.Contains("approval-gated-http-action", templates.Keys);
        Assert.Contains(
            templates["json-transform-project-task-creation"].Graph.Nodes,
            node => node.Executor?.Id == "json.transform");
        Assert.Contains(
            templates["local-folder-summary-markdown-report"].Graph.Nodes,
            node => node.Executor?.Id == "markdown.render");
        Assert.Contains(
            templates["approval-gated-http-action"].Graph.Nodes,
            node => node.Executor?.Id == "human.approval");
    }

    [Fact]
    public void Load_rejects_semantically_invalid_template_graph_with_source_context()
    {
        using var packDirectory = new TemporaryWorkflowTemplatePack(
            "invalid-graph.yaml",
            """
            workflows:
              - key: invalid-missing-target
                name: Invalid missing target
                description: Invalid test workflow.
                routingInstructions: Return JSON.
                graph:
                  startNodeId: start
                  nodes:
                    - id: start
                      kind: Start
                      name: Start
                      x: 0
                      y: 0
                    - id: end
                      kind: End
                      name: End
                      x: 300
                      y: 0
                  edges:
                    - id: start-to-missing
                      source: start
                      target: missing-node
            """);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new WorkflowTemplatePackLoader(packDirectory.RootPath).Load());

        Assert.Contains("invalid-missing-target", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("invalid-graph.yaml", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("start-to-missing", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_parses_office365_watch_input_parameters()
    {
        var pack = new WorkflowTemplatePackLoader().Load();
        var summary = Assert.Single(
            pack.Workflows,
            template => string.Equals(template.Key, "office365-email-watch-summary-to-project", StringComparison.OrdinalIgnoreCase));
        var tasks = Assert.Single(
            pack.Workflows,
            template => string.Equals(template.Key, "office365-email-watch-tasks-to-project", StringComparison.OrdinalIgnoreCase));

        AssertOffice365WatchInputParameters(pack.CreateInputParameters(summary));
        AssertOffice365WatchInputParameters(pack.CreateInputParameters(tasks));
    }

    private static void AssertOffice365WatchInputParameters(IReadOnlyList<WorkflowInputParameterDescriptor> parameters)
    {
        Assert.Equal(6, parameters.Count);
        var email = Assert.Single(parameters, parameter => parameter.Key == "emailAddress");
        Assert.Equal(WorkflowInputParameterKind.EmailAddress, email.Kind);
        Assert.True(email.IsRequired);
        Assert.Equal("$.emailAddress", email.JsonPath);
        Assert.Equal(WorkflowInputParameterOptionSourceKind.CrmContacts, email.OptionSource.Kind);

        var project = Assert.Single(parameters, parameter => parameter.Key == "projectId");
        Assert.Equal(WorkflowInputParameterKind.ProjectId, project.Kind);
        Assert.True(project.IsRequired);
        Assert.Equal(WorkflowInputParameterOptionSourceKind.ProjectStructureProjects, project.OptionSource.Kind);

        var node = Assert.Single(parameters, parameter => parameter.Key == "nodeId");
        Assert.Equal(WorkflowInputParameterKind.ProjectNodeId, node.Kind);
        Assert.True(node.IsRequired);
        Assert.Equal(WorkflowInputParameterOptionSourceKind.ProjectStructureNodes, node.OptionSource.Kind);
        Assert.Equal("projectId", node.OptionSource.DependsOnParameterKey);

        var processedCategory = Assert.Single(parameters, parameter => parameter.Key == "processedCategory");
        Assert.Equal(WorkflowInputParameterKind.Category, processedCategory.Kind);
        Assert.False(processedCategory.IsRequired);
        Assert.Equal("CanDoItAllProcessed", processedCategory.DefaultValue);

        var lookback = Assert.Single(parameters, parameter => parameter.Key == "lookbackHours");
        Assert.Equal(WorkflowInputParameterKind.Integer, lookback.Kind);
        Assert.Equal("336", lookback.DefaultValue);
        Assert.Equal(1, lookback.MinimumValue);
        Assert.Equal(720, lookback.MaximumValue);
    }

    private sealed class TemporaryWorkflowTemplatePack : IDisposable
    {
        public TemporaryWorkflowTemplatePack(string workflowFileName, string workflowYaml)
        {
            RootPath = Path.Combine(Path.GetTempPath(), $"workflow-template-pack-{Guid.NewGuid():N}");
            var workflowsPath = Path.Combine(RootPath, "workflows");
            Directory.CreateDirectory(workflowsPath);

            File.WriteAllText(
                Path.Combine(RootPath, "manifest.yaml"),
                $$"""
                packKey: test-pack
                name: Test Pack
                version: 1.0.0
                seedMarker: TEST-SEED
                seedVersion: test
                definitionNamePrefix: ""
                componentNamePrefix: ""
                component:
                  modelSettings:
                    temperature: 0.2
                    maxOutputTokens: 256
                    requireJsonOutput: true
                    responseFormatJsonSchema: "{}"
                  instructionsTemplate: "{name}\n{routingInstructions}"
                jsonShape:
                  kind: Json
                  schemaJson: "{}"
                  description: JSON payload
                runtimePolicy:
                  preferredBackend: InProcess
                  allowInProcessPreviewRuns: true
                  requireDurableProductionRuns: false
                  exposeAzureFunctionsStatusEndpoint: false
                  exposeAzureFunctionsMcpTool: false
                executorPolicies:
                  slow:
                    timeoutSeconds: 30
                    maxRetryAttempts: 0
                    retryDelayMilliseconds: 250
                    captureOutputArtifact: false
                nodeInstructionDefaults: {}
                workflowFiles:
                  - relativePath: workflows/{{workflowFileName}}
                """);
            File.WriteAllText(Path.Combine(workflowsPath, workflowFileName), workflowYaml);
        }

        public string RootPath { get; }

        public void Dispose()
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, recursive: true);
            }
        }
    }
}
