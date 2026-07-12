using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.SharedKernel.Configuration;

namespace CanDoItAll.Tests.Unit;

public sealed class SettingsSchemaTests
{
    [Fact]
    public void SettingsSchemaValidator_rejects_required_and_type_errors_without_raw_values()
    {
        var validator = new ConfigurationSchemaValidator();
        var schema = new ConfigurationSchema("1.0",
        [
            new ConfigurationFieldDescriptor("endpointUrl", "Endpoint", ConfigurationFieldType.Url, IsRequired: true, "Endpoint URL"),
            new ConfigurationFieldDescriptor("maxResults", "Max results", ConfigurationFieldType.Number, IsRequired: false, "Limit"),
            new ConfigurationFieldDescriptor("payload", "Payload", ConfigurationFieldType.Json, IsRequired: false, "JSON payload"),
            new ConfigurationFieldDescriptor("enabled", "Enabled", ConfigurationFieldType.Boolean, IsRequired: false, "Enabled flag")
        ]);
        var state = new ConfigurationState(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["maxResults"] = "not-number",
            ["payload"] = "{bad",
            ["enabled"] = "sometimes"
        });

        var result = validator.Validate(schema, state);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Issues, issue => issue.FieldKey == "endpointUrl" && issue.Message.Contains("required", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Issues, issue => issue.FieldKey == "maxResults" && issue.Message.Contains("number", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Issues, issue => issue.FieldKey == "payload" && issue.Message.Contains("valid JSON", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Issues, issue => issue.FieldKey == "enabled" && issue.Message.Contains("true or false", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.Issues, issue => issue.Message.Contains("not-number", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.Issues, issue => issue.Message.Contains("{bad", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ConnectorSchemaAndState_use_canonical_configuration_contracts()
    {
        var schema = new ConnectorConfigurationSchema("1.0",
        [
            new ConnectorConfigFieldDescriptor("endpointUrl", "Endpoint", ConnectorConfigFieldType.Url, IsRequired: true, "Endpoint URL")
        ]);
        var state = new ConnectorConfigState();

        state.SetText("endpointUrl", "https://example.test/hooks");
        var roundTripped = ConnectorConfigState.FromJson(state.ToJson());

        Assert.IsAssignableFrom<ConfigurationSchema>(schema);
        Assert.IsAssignableFrom<ConfigurationState>(state);
        Assert.Equal(ConfigurationFieldType.Url, schema.Fields[0].FieldType);
        Assert.Equal("https://example.test/hooks", roundTripped.GetText("endpointUrl"));
    }

    [Fact]
    public void SettingsSchemaValidator_accepts_explicit_select_aliases()
    {
        var validator = new ConfigurationSchemaValidator();
        var schema = new ConfigurationSchema("1.0",
        [
            new ConfigurationFieldDescriptor("operation", "Operation", ConfigurationFieldType.Select, IsRequired: true, "Operation")
            {
                Options =
                [
                    new ConfigurationFieldOption("WriteText", "WriteText")
                    {
                        AcceptedValues = ["3"]
                    }
                ]
            }
        ]);
        var state = new ConfigurationState(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["operation"] = "3"
        });

        var result = validator.Validate(schema, state);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void SettingsSchemaValidator_rejects_explicit_empty_guid_for_optional_and_required_fields()
    {
        var validator = new ConfigurationSchemaValidator();
        var optionalSchema = new ConfigurationSchema("1.0",
        [
            new ConfigurationFieldDescriptor("projectId", "Project", ConfigurationFieldType.Guid, IsRequired: false, "Optional project id.")
        ]);
        var requiredSchema = new ConfigurationSchema("1.0",
        [
            new ConfigurationFieldDescriptor("projectId", "Project", ConfigurationFieldType.Guid, IsRequired: true, "Required project id.")
        ]);
        var state = new ConfigurationState(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["projectId"] = Guid.Empty.ToString()
        });

        var optionalResult = validator.Validate(optionalSchema, state);
        var requiredResult = validator.Validate(requiredSchema, state);

        Assert.Contains(optionalResult.Issues, issue =>
            issue.FieldKey == "projectId" &&
            issue.Message.Contains("non-empty GUID", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(requiredResult.Issues, issue =>
            issue.FieldKey == "projectId" &&
            issue.Message.Contains("non-empty GUID", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void WorkflowDefinitionValidator_treats_json_null_as_unset_for_optional_fields()
    {
        var executor = new OptionalGuidExecutor();
        var validator = new WorkflowDefinitionValidator(new WorkflowExecutorCatalog([executor]));
        var definition = CreateDefinition(
        [
            CreateNode("start", WorkflowNodeKind.Start),
            CreateExecutorNode("tool", executor.Descriptor.Id) with
            {
                Settings = CreateSettings(executor.Descriptor.Id) with
                {
                    ExecutorSettingsJson = """
                        {
                          "projectId": null,
                          "projectIdJsonPath": "$.projectId"
                        }
                        """
                }
            },
            CreateNode("end", WorkflowNodeKind.End)
        ], [
            CreateEdge("start-tool", "start", "tool"),
            CreateEdge("tool-end", "tool", "end")
        ]);

        var result = validator.Validate(definition, []);

        Assert.DoesNotContain(result.Issues, issue => issue.Code == WorkflowValidationIssueCode.InvalidExecutorSettings);
    }

    [Fact]
    public void SettingsSchemaValidator_rejects_undefined_field_and_number_metadata()
    {
        var validator = new ConfigurationSchemaValidator();
        var schema = new ConfigurationSchema("1.0",
        [
            new ConfigurationFieldDescriptor(
                "unknownType",
                "Unknown type",
                (ConfigurationFieldType)999,
                IsRequired: false,
                "Invalid field metadata."),
            new ConfigurationFieldDescriptor(
                "unknownNumber",
                "Unknown number",
                ConfigurationFieldType.Number,
                IsRequired: false,
                "Invalid number metadata.")
            {
                NumberKind = (ConfigurationNumberKind)999
            }
        ]);

        var result = validator.Validate(schema, new ConfigurationState());

        Assert.False(result.Succeeded);
        Assert.Contains(result.Issues, issue =>
            issue.FieldKey == "unknownType" &&
            issue.Message.Contains("undefined field type", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Issues, issue =>
            issue.FieldKey == "unknownNumber" &&
            issue.Message.Contains("undefined number kind", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void WorkflowDefinitionValidator_applies_executor_configuration_schema()
    {
        var executor = new SchemaBackedExecutor();
        var catalog = new WorkflowExecutorCatalog([executor]);
        var validator = new WorkflowDefinitionValidator(catalog);
        var definition = CreateDefinition(
        [
            CreateNode("start", WorkflowNodeKind.Start),
            CreateExecutorNode("tool", executor.Descriptor.Id) with
            {
                Settings = CreateSettings(executor.Descriptor.Id) with
                {
                    ExecutorSettingsJson = """
                        {
                          "endpointUrl": "not-a-url",
                          "maxResults": "many"
                        }
                        """
                }
            },
            CreateNode("end", WorkflowNodeKind.End)
        ], [
            CreateEdge("start-tool", "start", "tool"),
            CreateEdge("tool-end", "tool", "end")
        ]);

        var result = validator.Validate(definition, []);

        Assert.Contains(result.Issues, issue =>
            issue.Code == WorkflowValidationIssueCode.InvalidExecutorSettings &&
            issue.Message.Contains("endpointUrl", StringComparison.Ordinal));
        Assert.Contains(result.Issues, issue =>
            issue.Code == WorkflowValidationIssueCode.InvalidExecutorSettings &&
            issue.Message.Contains("maxResults", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Issues, issue => issue.Message.Contains("not-a-url", StringComparison.OrdinalIgnoreCase));
    }

    private static WorkflowDefinition CreateDefinition(
        IReadOnlyList<WorkflowNode> nodes,
        IReadOnlyList<WorkflowEdge> edges)
        => new(
            WorkflowId.New(),
            WorkflowVersionId.New(),
            "Settings schema workflow",
            "Settings schema workflow for tests.",
            WorkflowLifecycleStatus.Draft,
            new WorkflowGraph(new WorkflowNodeId("start"), nodes, edges),
            new WorkflowRuntimePolicy(
                WorkflowRuntimeBackendKind.InProcess,
                AllowInProcessPreviewRuns: true,
                RequireDurableProductionRuns: false,
                ExposeAzureFunctionsStatusEndpoint: false,
                ExposeAzureFunctionsMcpTool: false),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

    private static WorkflowNode CreateExecutorNode(string id, WorkflowExecutorId executorId)
        => new(
            new WorkflowNodeId(id),
            WorkflowNodeKind.Executor,
            id,
            [],
            CreateSettings(executorId));

    private static WorkflowNodeSettings CreateSettings(WorkflowExecutorId executorId)
        => new WorkflowNodeSettings(
            ComponentId: null,
            AgentId: null,
            SubworkflowId: null,
            ExternalRequestKind: null,
            Instructions: string.Empty,
            InputShape: WorkflowValueShape.Text,
            ResultShape: new WorkflowValueShape(WorkflowValueShapeKind.Json, "{}", "JSON")) with
        {
            ExecutorId = executorId,
            ExecutorSettingsJson = "{}",
            ExecutionPolicy = WorkflowExecutorExecutionPolicy.Default
        };

    private static WorkflowNode CreateNode(string id, WorkflowNodeKind kind)
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
                InputShape: kind == WorkflowNodeKind.End
                    ? new WorkflowValueShape(WorkflowValueShapeKind.Object, "{}", "Any result")
                    : WorkflowValueShape.Text,
                ResultShape: WorkflowValueShape.Text));

    private static WorkflowEdge CreateEdge(string id, string source, string target)
        => new(
            new WorkflowEdgeId(id),
            new WorkflowNodeId(source),
            SourcePortId: null,
            new WorkflowNodeId(target),
            TargetPortId: null,
            WorkflowEdgeKind.Direct,
            ConditionExpression: string.Empty)
        {
            Routing = WorkflowEdgeRouting.Always
        };

    private sealed class SchemaBackedExecutor : IWorkflowExecutor
    {
        public WorkflowExecutorDescriptor Descriptor { get; } = new(
            new WorkflowExecutorId("test.schema"),
            "Schema test",
            "Schema test executor.",
            WorkflowExecutorCategoryKind.Data,
            "data_object",
            "test.schema",
            WorkflowValueShape.Text,
            new WorkflowValueShape(WorkflowValueShapeKind.Json, "{}", "JSON"),
            "{\"type\":\"object\"}",
            "{}",
            WorkflowExecutorExecutionPolicy.Default,
            IsImplemented: true)
        {
            ConfigurationSchema = new ConfigurationSchema("1.0",
            [
                new ConfigurationFieldDescriptor("endpointUrl", "Endpoint", ConfigurationFieldType.Url, IsRequired: true, "Endpoint URL"),
                new ConfigurationFieldDescriptor("maxResults", "Max results", ConfigurationFieldType.Number, IsRequired: false, "Limit")
            ])
        };

        public ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
            WorkflowExecutorExecutionContext context,
            WorkflowNodeInput input,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(new WorkflowNodeExecutionResult(
                context.Node.Id,
                "{}",
                context.Descriptor.ResultShape));
    }

    private sealed class OptionalGuidExecutor : IWorkflowExecutor
    {
        public WorkflowExecutorDescriptor Descriptor { get; } = new(
            new WorkflowExecutorId("test.optional-guid"),
            "Optional GUID test",
            "Optional GUID schema test executor.",
            WorkflowExecutorCategoryKind.Data,
            "data_object",
            "test.optional-guid",
            WorkflowValueShape.Text,
            new WorkflowValueShape(WorkflowValueShapeKind.Json, "{}", "JSON"),
            "{\"type\":\"object\"}",
            "{}",
            WorkflowExecutorExecutionPolicy.Default,
            IsImplemented: true)
        {
            ConfigurationSchema = new ConfigurationSchema("1.0",
            [
                new ConfigurationFieldDescriptor("projectId", "Project", ConfigurationFieldType.Guid, IsRequired: false, "Optional project id."),
                new ConfigurationFieldDescriptor("projectIdJsonPath", "Project JSON path", ConfigurationFieldType.Text, IsRequired: false, "Project id input path.")
            ])
        };

        public ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
            WorkflowExecutorExecutionContext context,
            WorkflowNodeInput input,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(new WorkflowNodeExecutionResult(
                context.Node.Id,
                "{}",
                context.Descriptor.ResultShape));
    }
}
