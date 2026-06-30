using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Templates;
using CanDoItAll.SharedKernel.Configuration;

namespace CanDoItAll.Tests.Unit;

public sealed class WorkflowTemplatePackLoaderTests
{
    [Fact]
    public void Load_default_pack_materializes_every_current_template_and_preview_fixture()
    {
        var pack = new WorkflowTemplatePackLoader().Load();

        Assert.Equal(5, pack.Manifest.WorkflowFiles.Count);
        Assert.NotEmpty(pack.Workflows);
        Assert.All(pack.Workflows, template =>
        {
            Assert.False(string.IsNullOrWhiteSpace(template.Key));
            Assert.True(File.Exists(template.SourcePath), template.SourcePath);

            var component = CreateComponent(pack, template);
            var definition = pack.CreateDefinition(template, component);

            Assert.Equal(template.Graph.StartNodeId, definition.Graph.StartNodeId.Value);
            Assert.Equal(template.Graph.Nodes.Count, definition.Graph.Nodes.Count);
            Assert.Equal(template.Graph.Edges.Count, definition.Graph.Edges.Count);
            Assert.Equal(pack.RuntimePolicy, definition.RuntimePolicy);
            Assert.Equal(pack.CreateInputParameters(template).Count, definition.InputParameters.Count);
        });

        var previewCatalog = new WorkflowPreviewSimulationTemplateLoader().Load(pack.RootPath);
        var projectStructure = Assert.Contains(WorkflowExecutorIds.ProjectStructure.Value, previewCatalog.Executors);
        Assert.Contains(nameof(WorkflowProjectStructureOperation.CreateAsset), projectStructure.Operations.Keys);
        Assert.Contains(nameof(WorkflowProjectStructureOperation.CreateTaskNodes), projectStructure.Operations.Keys);
    }

    [Fact]
    public void Load_default_pack_validates_current_executor_references_against_descriptor_catalog()
    {
        var pack = new WorkflowTemplatePackLoader().Load();
        var executorIds = pack.Workflows
            .SelectMany(template => template.Graph.Nodes)
            .Select(node => node.Executor?.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(id => id!)
            .ToArray();
        var catalog = WorkflowExecutorCatalog.FromDescriptors(executorIds.Select(id => CreateDescriptor(id)));

        var validatedPack = new WorkflowTemplatePackLoader(pack.RootPath, catalog).Load();

        Assert.Equal(
            pack.Workflows.Select(template => template.Key).OrderBy(key => key, StringComparer.OrdinalIgnoreCase),
            validatedPack.Workflows.Select(template => template.Key).OrderBy(key => key, StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void Load_rejects_missing_executor_with_typed_repairable_context()
    {
        using var packDirectory = TemporaryWorkflowTemplatePack.Create(
            "missing-executor.yaml",
            CreateLinearExecutorWorkflow("missing-executor", "missing.executor"));
        var catalog = WorkflowExecutorCatalog.FromDescriptors([CreateDescriptor("known.executor")]);

        var exception = Assert.Throws<WorkflowTemplatePackException>(() =>
            new WorkflowTemplatePackLoader(packDirectory.RootPath, catalog).Load());

        Assert.Equal(WorkflowTemplateFailureKind.DescriptorValidationFailed, exception.FailureKind);
        Assert.Equal("missing.executor", exception.Diagnostic.ExecutorId);
        Assert.Equal("execute", exception.Diagnostic.NodeId);
        Assert.Contains("missing-executor.yaml", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("missing-executor", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("repair hint", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_rejects_invalid_routing_with_yaml_path_context()
    {
        using var packDirectory = TemporaryWorkflowTemplatePack.Create(
            "invalid-route.yaml",
            """
            workflows:
              - key: invalid-route
                name: Invalid route
                description: Invalid test workflow.
                routingInstructions: Return JSON.
                graph:
                  startNodeId: start
                  nodes:
                    - { id: start, kind: Start, name: Start, x: 0, y: 0 }
                    - { id: gate, kind: Triage, name: Gate, x: 200, y: 0 }
                    - { id: end, kind: End, name: End, x: 400, y: 0 }
                  edges:
                    - { id: start-to-gate, source: start, target: gate }
                    - id: gate-to-end
                      source: gate
                      target: end
                      kind: Conditional
                      routing: { kind: Predicate, jsonPath: "$.ready", operator: NotAnOperator, expectedValue: "true", expectedValueKind: Boolean }
            """);

        var exception = Assert.Throws<WorkflowTemplatePackException>(() =>
            new WorkflowTemplatePackLoader(packDirectory.RootPath).Load());

        Assert.Equal(WorkflowTemplateFailureKind.GraphMaterializationFailed, exception.FailureKind);
        Assert.Contains("graph.edges[1].routing.operator", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("invalid-route", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_rejects_invalid_input_parameter_with_typed_context()
    {
        using var packDirectory = TemporaryWorkflowTemplatePack.Create(
            "invalid-input.yaml",
            """
            workflows:
              - key: invalid-input
                name: Invalid input
                description: Invalid test workflow.
                routingInstructions: Return JSON.
                inputParameters:
                  - key: project
                    label: Project
                    kind: NotAParameterKind
                    required: true
                graph:
                  startNodeId: start
                  nodes:
                    - { id: start, kind: Start, name: Start, x: 0, y: 0 }
                    - { id: end, kind: End, name: End, x: 300, y: 0 }
                  edges:
                    - { id: start-to-end, source: start, target: end }
            """);

        var exception = Assert.Throws<WorkflowTemplatePackException>(() =>
            new WorkflowTemplatePackLoader(packDirectory.RootPath).Load());

        Assert.Equal(WorkflowTemplateFailureKind.InputParameterInvalid, exception.FailureKind);
        Assert.Contains("inputParameters[0].kind", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("NotAParameterKind", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_rejects_invalid_runtime_policy()
    {
        using var packDirectory = TemporaryWorkflowTemplatePack.Create(
            "runtime.yaml",
            CreateLinearExecutorWorkflow("invalid-runtime", "known.executor"),
            preferredBackend: "MadeUpBackend");

        var exception = Assert.Throws<WorkflowTemplatePackException>(() =>
            new WorkflowTemplatePackLoader(packDirectory.RootPath).Load());

        Assert.Equal(WorkflowTemplateFailureKind.GraphMaterializationFailed, exception.FailureKind);
        Assert.Contains("runtimePolicy.preferredBackend", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("MadeUpBackend", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_rejects_invalid_executor_settings_against_descriptor_schema()
    {
        using var packDirectory = TemporaryWorkflowTemplatePack.Create(
            "invalid-settings.yaml",
            CreateLinearExecutorWorkflow(
                "invalid-settings",
                "known.executor",
                """
                    count: not-a-number
                """));
        var catalog = WorkflowExecutorCatalog.FromDescriptors([
            CreateDescriptor(
                "known.executor",
                new ConfigurationSchema(
                    "1.0",
                    [new ConfigurationFieldDescriptor("count", "Count", ConfigurationFieldType.Number, IsRequired: true, "Required count.")]))
        ]);

        var exception = Assert.Throws<WorkflowTemplatePackException>(() =>
            new WorkflowTemplatePackLoader(packDirectory.RootPath, catalog).Load());

        Assert.Equal(WorkflowTemplateFailureKind.SemanticValidationFailed, exception.FailureKind);
        Assert.Equal("known.executor", exception.Diagnostic.ExecutorId);
        Assert.Contains("invalid setting 'count'", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_rejects_invalid_yaml_with_file_context()
    {
        using var packDirectory = TemporaryWorkflowTemplatePack.Create(
            "invalid-yaml.yaml",
            "workflows:\n  - key: [broken");

        var exception = Assert.Throws<WorkflowTemplatePackException>(() =>
            new WorkflowTemplatePackLoader(packDirectory.RootPath).Load());

        Assert.Equal(WorkflowTemplateFailureKind.WorkflowLoadFailed, exception.FailureKind);
        Assert.Contains("invalid-yaml.yaml", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Preview_loader_rejects_invalid_preview_simulation_json()
    {
        using var packDirectory = TemporaryWorkflowTemplatePack.Create(
            "valid.yaml",
            CreateLinearExecutorWorkflow("valid", "known.executor"),
            previewSimulationJson: "{ invalid json");

        var exception = Assert.Throws<WorkflowTemplatePackException>(() =>
            new WorkflowPreviewSimulationTemplateLoader().Load(packDirectory.RootPath));

        Assert.Equal(WorkflowTemplateFailureKind.PreviewSimulationInvalid, exception.FailureKind);
        Assert.Contains("preview-simulations/executors.json", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Template_loading_is_owned_by_workflow_template_project_without_ui_fallback()
    {
        var root = FindRepositoryRoot();
        var moduleLoaderPath = Path.Combine(
            root,
            "src",
            "CanDoItAll.Modules.AgentFramework",
            "Catalog",
            "WorkflowTemplatePackLoader.cs");
        var moduleProject = File.ReadAllText(Path.Combine(
            root,
            "src",
            "CanDoItAll.Modules.AgentFramework",
            "CanDoItAll.Modules.AgentFramework.csproj"));
        var templateProject = File.ReadAllText(Path.Combine(
            root,
            "src",
            "CanDoItAll.AgentFramework.Workflows.Templates",
            "CanDoItAll.AgentFramework.Workflows.Templates.csproj"));

        Assert.False(File.Exists(moduleLoaderPath), moduleLoaderPath);
        Assert.DoesNotContain("YamlDotNet", moduleProject, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CanDoItAll.Modules.AgentFramework", templateProject, StringComparison.Ordinal);
        Assert.DoesNotContain("CanDoItAll.AgentFramework.Maf", templateProject, StringComparison.Ordinal);
        Assert.DoesNotContain("CanDoItAll.Web", templateProject, StringComparison.Ordinal);
    }

    private static LlmCallComponent CreateComponent(
        WorkflowTemplatePack pack,
        WorkflowTemplateDefinition template)
        => new(
            WorkflowComponentId.New(),
            string.IsNullOrWhiteSpace(template.Name) ? template.Key : template.Name,
            ProviderProfileId: null,
            Model: "template-validation",
            WorkflowModality.Text,
            pack.CreateModelSettings(),
            pack.CreateComponentInstructions(template),
            pack.JsonShape,
            pack.JsonShape,
            AgentPermissionsPolicy.Default,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

    private static WorkflowExecutorDescriptor CreateDescriptor(
        string id,
        ConfigurationSchema? configurationSchema = null)
        => new(
            new WorkflowExecutorId(id),
            id,
            $"{id} test descriptor",
            WorkflowExecutorCategoryKind.Utility,
            "test",
            "test",
            new WorkflowValueShape(WorkflowValueShapeKind.Json, "{}", "JSON payload"),
            new WorkflowValueShape(WorkflowValueShapeKind.Json, "{}", "JSON payload"),
            SettingsSchemaJson: "{}",
            DefaultSettingsJson: "{}",
            WorkflowExecutorExecutionPolicy.Default,
            IsImplemented: true)
        {
            ConfigurationSchema = configurationSchema ?? ConfigurationSchema.Empty()
        };

    private static string CreateLinearExecutorWorkflow(
        string key,
        string executorId,
        string settingsYaml = "mode: configured")
        => $$"""
        workflows:
          - key: {{key}}
            name: {{key}}
            description: Test workflow.
            routingInstructions: Return JSON.
            graph:
              startNodeId: start
              nodes:
                - { id: start, kind: Start, name: Start, x: 0, y: 0 }
                - id: execute
                  kind: Executor
                  name: Execute
                  x: 250
                  y: 0
                  executor:
                    id: {{executorId}}
                    policy: slow
                    settings:
        {{Indent(settingsYaml, 14)}}
                - { id: end, kind: End, name: End, x: 500, y: 0 }
              edges:
                - { id: start-to-execute, source: start, target: execute }
                - { id: execute-to-end, source: execute, target: end }
        """;

    private static string Indent(string value, int spaces)
    {
        var prefix = new string(' ', spaces);
        return string.Join(
            Environment.NewLine,
            value.Replace("\r\n", "\n", StringComparison.Ordinal)
                .Split('\n')
                .Select(line => $"{prefix}{line}"));
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "CanDoItAll.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }

    private sealed class TemporaryWorkflowTemplatePack : IDisposable
    {
        private TemporaryWorkflowTemplatePack(string rootPath)
        {
            RootPath = rootPath;
        }

        public string RootPath { get; }

        public static TemporaryWorkflowTemplatePack Create(
            string workflowFileName,
            string workflowYaml,
            string preferredBackend = "InProcess",
            string? previewSimulationJson = null)
        {
            var root = Path.Combine(Path.GetTempPath(), $"workflow-template-pack-{Guid.NewGuid():N}");
            var workflowsPath = Path.Combine(root, "workflows");
            Directory.CreateDirectory(workflowsPath);

            File.WriteAllText(
                Path.Combine(root, "manifest.yaml"),
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
                  preferredBackend: {{preferredBackend}}
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

            if (previewSimulationJson is not null)
            {
                var previewPath = Path.Combine(root, "preview-simulations");
                Directory.CreateDirectory(previewPath);
                File.WriteAllText(Path.Combine(previewPath, "executors.json"), previewSimulationJson);
            }

            return new TemporaryWorkflowTemplatePack(root);
        }

        public void Dispose()
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, recursive: true);
            }
        }
    }
}
