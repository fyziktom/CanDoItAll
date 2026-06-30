using System.Text.Json;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Workflows.Templates;

public sealed class WorkflowTemplatePackManifest
{
    public string PackKey { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public string SeedMarker { get; set; } = string.Empty;

    public string SeedVersion { get; set; } = string.Empty;

    public string DefinitionNamePrefix { get; set; } = string.Empty;

    public string ComponentNamePrefix { get; set; } = string.Empty;

    public WorkflowTemplateComponentDefaults Component { get; set; } = new();

    public WorkflowTemplateValueShape JsonShape { get; set; } = new();

    public WorkflowTemplateRuntimePolicy RuntimePolicy { get; set; } = new();

    public Dictionary<string, WorkflowTemplateExecutionPolicy> ExecutorPolicies { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, string> NodeInstructionDefaults { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public List<WorkflowTemplateFileReference> WorkflowFiles { get; set; } = [];
}

public sealed class WorkflowTemplateFileReference
{
    public string RelativePath { get; set; } = string.Empty;
}

public sealed class WorkflowTemplateFile
{
    public List<WorkflowTemplateDefinition> Workflows { get; set; } = [];
}

public sealed class WorkflowTemplateDefinition
{
    public string Key { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string RoutingInstructions { get; set; } = string.Empty;

    public List<WorkflowTemplateInputParameter> InputParameters { get; set; } = [];

    public WorkflowTemplateGraph Graph { get; set; } = new();

    public string SourcePath { get; internal set; } = string.Empty;
}

public sealed class WorkflowTemplateGraph
{
    public string StartNodeId { get; set; } = "start";

    public List<WorkflowTemplateNode> Nodes { get; set; } = [];

    public List<WorkflowTemplateEdge> Edges { get; set; } = [];
}

public sealed class WorkflowTemplateNode
{
    public string Id { get; set; } = string.Empty;

    public string Kind { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Instructions { get; set; } = string.Empty;

    public string ExternalRequestKind { get; set; } = string.Empty;

    public double X { get; set; }

    public double Y { get; set; }

    public WorkflowTemplateExecutor? Executor { get; set; }
}

public sealed class WorkflowTemplateExecutor
{
    public string Id { get; set; } = string.Empty;

    public string Policy { get; set; } = string.Empty;

    public Dictionary<string, object?> Settings { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class WorkflowTemplateEdge
{
    public string Id { get; set; } = string.Empty;

    public string Source { get; set; } = string.Empty;

    public string Target { get; set; } = string.Empty;

    public string Kind { get; set; } = string.Empty;

    public WorkflowTemplateRouting? Routing { get; set; }
}

public sealed class WorkflowTemplateRouting
{
    public string Kind { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string JsonPath { get; set; } = string.Empty;

    public string Operator { get; set; } = string.Empty;

    public string ExpectedValue { get; set; } = string.Empty;

    public string ExpectedValueJson { get; set; } = string.Empty;

    public string ExpectedValueKind { get; set; } = string.Empty;

    public bool CaseSensitive { get; set; }

    public int? FanOutTargetIndex { get; set; }
}

public sealed class WorkflowTemplateComponentDefaults
{
    public WorkflowTemplateModelSettings ModelSettings { get; set; } = new();

    public string InstructionsTemplate { get; set; } = string.Empty;
}

public sealed class WorkflowTemplateModelSettings
{
    public double? Temperature { get; set; }

    public int? MaxOutputTokens { get; set; }

    public bool RequireJsonOutput { get; set; }

    public string ResponseFormatJsonSchema { get; set; } = string.Empty;
}

public sealed class WorkflowTemplateValueShape
{
    public string Kind { get; set; } = string.Empty;

    public string SchemaJson { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;
}

public sealed class WorkflowTemplateRuntimePolicy
{
    public string PreferredBackend { get; set; } = string.Empty;

    public bool AllowInProcessPreviewRuns { get; set; }

    public bool RequireDurableProductionRuns { get; set; }

    public bool ExposeAzureFunctionsStatusEndpoint { get; set; }

    public bool ExposeAzureFunctionsMcpTool { get; set; }
}

public sealed class WorkflowTemplateExecutionPolicy
{
    public int TimeoutSeconds { get; set; }

    public int MaxRetryAttempts { get; set; }

    public int RetryDelayMilliseconds { get; set; }

    public bool CaptureOutputArtifact { get; set; }
}

public sealed class WorkflowPreviewSimulationTemplateCatalog
{
    public Dictionary<string, WorkflowPreviewSimulationExecutorTemplates> Executors { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class WorkflowPreviewSimulationExecutorTemplates
{
    public Dictionary<string, WorkflowPreviewSimulationTemplate> Operations { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class WorkflowPreviewSimulationTemplate
{
    public string Description { get; set; } = string.Empty;

    public JsonElement OutputTemplate { get; set; }
}
