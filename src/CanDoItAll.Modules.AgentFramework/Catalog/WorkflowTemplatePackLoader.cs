using System.Reflection;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedKernel;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class WorkflowTemplatePackLoader
{
    private const string ManifestFileName = "manifest.yaml";

    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    private readonly string? configuredPackRoot;
    private readonly IWorkflowExecutorCatalog? executorCatalog;
    private readonly Lazy<WorkflowTemplatePack> pack;

    public WorkflowTemplatePackLoader(string? packRoot = null)
        : this(packRoot, executorCatalog: null)
    {
    }

    public WorkflowTemplatePackLoader(IWorkflowExecutorCatalog executorCatalog)
        : this(packRoot: null, executorCatalog)
    {
    }

    private WorkflowTemplatePackLoader(string? packRoot, IWorkflowExecutorCatalog? executorCatalog)
    {
        configuredPackRoot = packRoot;
        this.executorCatalog = executorCatalog;
        pack = new Lazy<WorkflowTemplatePack>(LoadCore, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public WorkflowTemplatePack Load() => pack.Value;

    public static string FindPackRoot(string? packRoot = null) => ResolvePackRoot(packRoot);

    private WorkflowTemplatePack LoadCore()
    {
        var root = ResolvePackRoot(configuredPackRoot);
        var manifestPath = Path.Combine(root, ManifestFileName);
        var manifest = ReadYaml<WorkflowTemplatePackManifest>(manifestPath);
        var workflows = new List<WorkflowTemplateDefinition>();

        foreach (var file in manifest.WorkflowFiles)
        {
            var workflowFilePath = Path.GetFullPath(Path.Combine(root, Require(file.RelativePath, "workflow file relative path", manifestPath)));
            var workflowFile = ReadYaml<WorkflowTemplateFile>(workflowFilePath);
            foreach (var workflow in workflowFile.Workflows)
            {
                workflow.SourcePath = workflowFilePath;
                workflows.Add(workflow);
            }
        }

        var duplicateKeys = workflows
            .GroupBy(workflow => workflow.Key, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (duplicateKeys.Length > 0)
        {
            throw new InvalidOperationException(
                $"Workflow template pack '{root}' contains duplicate workflow key(s): {string.Join(", ", duplicateKeys)}.");
        }

        var templatePack = new WorkflowTemplatePack(root, manifest, workflows);
        ValidateTemplateGraphs(templatePack);
        return templatePack;
    }

    private void ValidateTemplateGraphs(WorkflowTemplatePack templatePack)
    {
        var validator = executorCatalog is null
            ? new WorkflowDefinitionValidator()
            : new WorkflowDefinitionValidator(
                executorCatalog,
                WorkflowDefinitionValidationOptions.RegisteredExecutorsOnly);
        foreach (var template in templatePack.Workflows)
        {
            var component = CreateTemplateValidationComponent(templatePack, template);
            var context = WorkflowTemplateValidationContext.Create(template);
            WorkflowGraph graph;
            try
            {
                graph = templatePack.CreateGraph(template, component.Id);
            }
            catch (InvalidOperationException exception)
            {
                throw new InvalidOperationException(
                    $"Workflow template '{context}' failed graph conversion: {exception.Message}",
                    exception);
            }

            var definition = new WorkflowDefinition(
                WorkflowId.New(),
                WorkflowVersionId.New(),
                string.IsNullOrWhiteSpace(template.Name) ? template.Key : template.Name,
                template.Description,
                WorkflowLifecycleStatus.Active,
                graph,
                templatePack.RuntimePolicy,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow);
            var validation = validator.Validate(definition, [component]);
            if (!validation.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Workflow template '{context}' failed semantic validation: {FormatValidationIssues(validation.Issues)}");
            }
        }
    }

    private static LlmCallComponent CreateTemplateValidationComponent(
        WorkflowTemplatePack templatePack,
        WorkflowTemplateDefinition template)
        => new(
            WorkflowComponentId.New(),
            string.IsNullOrWhiteSpace(template.Name) ? template.Key : template.Name,
            ProviderProfileId: null,
            Model: "template-validation",
            WorkflowModality.Text,
            templatePack.CreateModelSettings(),
            templatePack.CreateComponentInstructions(template),
            templatePack.JsonShape,
            templatePack.JsonShape,
            AgentPermissionsPolicy.Default,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

    private static string FormatValidationIssues(IReadOnlyList<WorkflowValidationIssue> issues)
        => string.Join(
            " ",
            issues.Select(issue =>
            {
                var location = issue.NodeId is { } nodeId
                    ? $"node '{nodeId}'"
                    : issue.EdgeId is { } edgeId
                        ? $"edge '{edgeId}'"
                        : "template";
                return $"{location}: {issue.Message}";
            }));

    private static T ReadYaml<T>(string path)
        where T : class, new()
    {
        try
        {
            using var reader = File.OpenText(path);
            return YamlDeserializer.Deserialize<T>(reader) ?? new T();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or YamlDotNet.Core.YamlException)
        {
            throw new InvalidOperationException(
                $"Workflow template YAML file '{path}' could not be loaded: {exception.Message}",
                exception);
        }
    }

    private static string ResolvePackRoot(string? explicitRoot)
    {
        if (!string.IsNullOrWhiteSpace(explicitRoot))
        {
            var normalizedExplicitRoot = Path.GetFullPath(explicitRoot);
            if (File.Exists(Path.Combine(normalizedExplicitRoot, ManifestFileName)))
            {
                return normalizedExplicitRoot;
            }

            if (File.Exists(normalizedExplicitRoot) &&
                string.Equals(Path.GetFileName(normalizedExplicitRoot), ManifestFileName, StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetDirectoryName(normalizedExplicitRoot)!;
            }
        }

        var relativeManifestPath = Path.Combine(
            WorkflowTemplatePackOptions.TemplatesRootDirectoryName,
            WorkflowTemplatePackOptions.WorkflowsDirectoryName,
            ManifestFileName);
        var discoveredRoot = AncestorFileLocator.FindContainingDirectory(
            relativeManifestPath,
            Directory.GetCurrentDirectory(),
            AppContext.BaseDirectory,
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
        if (!string.IsNullOrWhiteSpace(discoveredRoot))
        {
            return discoveredRoot;
        }

        throw new InvalidOperationException(
            $"Unable to locate {WorkflowTemplatePackOptions.DefaultRelativePackRoot}/{ManifestFileName} from the current execution root. " +
            $"Configure a workflow template pack root when the template pack lives outside the repository default layout.");
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    internal static string SerializeSettings(IDictionary<string, object?> settings)
        => settings.Count == 0
            ? string.Empty
            : JsonSerializer.Serialize(
                settings.ToDictionary(
                    item => item.Key,
                    item => NormalizeSettingValue(item.Value),
                    StringComparer.OrdinalIgnoreCase),
                JsonOptions);

    private static object? NormalizeSettingValue(object? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value is string text)
        {
            return NormalizeStringSettingValue(text);
        }

        if (value is IDictionary<string, object?> objectDictionary)
        {
            return objectDictionary.ToDictionary(
                item => item.Key,
                item => NormalizeSettingValue(item.Value),
                StringComparer.OrdinalIgnoreCase);
        }

        if (value is IEnumerable<object?> list)
        {
            return list.Select(NormalizeSettingValue).ToArray();
        }

        return value;
    }

    private static object NormalizeStringSettingValue(string value)
    {
        var trimmed = value.Trim();
        if (bool.TryParse(trimmed, out var boolean))
        {
            return boolean;
        }

        if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
        {
            return integer;
        }

        if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
        {
            return number;
        }

        return value;
    }

    internal static string Require(string? value, string fieldName, string path)
        => string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"Workflow template '{path}' is missing required field '{fieldName}'.")
            : value.Trim();

    private static class WorkflowTemplateValidationContext
    {
        public static string Create(WorkflowTemplateDefinition template)
            => string.IsNullOrWhiteSpace(template.SourcePath)
                ? template.Key
                : $"{template.SourcePath}#{template.Key}";
    }
}

public static class WorkflowTemplatePackOptions
{
    public const string TemplatesRootDirectoryName = "Templates";
    public const string WorkflowsDirectoryName = "Workflows";
    public const string DefaultRelativePackRoot = "Templates/Workflows";
}

public sealed record WorkflowTemplatePack(
    string RootPath,
    WorkflowTemplatePackManifest Manifest,
    IReadOnlyList<WorkflowTemplateDefinition> Workflows)
{
    public WorkflowValueShape JsonShape => Manifest.JsonShape.ToModel("manifest.yaml/jsonShape");

    public WorkflowRuntimePolicy RuntimePolicy => Manifest.RuntimePolicy.ToModel("manifest.yaml/runtimePolicy");

    public WorkflowGraph CreateGraph(WorkflowTemplateDefinition template, WorkflowComponentId componentId)
    {
        ArgumentNullException.ThrowIfNull(template);

        var context = string.IsNullOrWhiteSpace(template.SourcePath)
            ? template.Key
            : $"{template.SourcePath}#{template.Key}";
        return template.Graph.ToModel(
            context,
            componentId,
            JsonShape,
            Manifest.NodeInstructionDefaults,
            Manifest.ExecutorPolicies);
    }

    public WorkflowModelSettings CreateModelSettings()
        => Manifest.Component.ModelSettings.ToModel("manifest.yaml/component/modelSettings");

    public string CreateComponentInstructions(WorkflowTemplateDefinition template)
    {
        var templateText = WorkflowTemplatePackLoader.Require(
            Manifest.Component.InstructionsTemplate,
            "component.instructionsTemplate",
            "manifest.yaml");
        return templateText
            .Replace("{name}", template.Name, StringComparison.Ordinal)
            .Replace("{routingInstructions}", template.RoutingInstructions, StringComparison.Ordinal);
    }
}

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

    public WorkflowTemplateGraph Graph { get; set; } = new();

    internal string SourcePath { get; set; } = string.Empty;
}

public sealed class WorkflowTemplateGraph
{
    public string StartNodeId { get; set; } = "start";

    public List<WorkflowTemplateNode> Nodes { get; set; } = [];

    public List<WorkflowTemplateEdge> Edges { get; set; } = [];

    public WorkflowGraph ToModel(
        string context,
        WorkflowComponentId componentId,
        WorkflowValueShape jsonShape,
        IReadOnlyDictionary<string, string> nodeInstructionDefaults,
        IReadOnlyDictionary<string, WorkflowTemplateExecutionPolicy> executorPolicies)
    {
        if (Nodes.Count == 0)
        {
            throw new InvalidOperationException($"Workflow template '{context}' must define at least one node.");
        }

        return new WorkflowGraph(
            new WorkflowNodeId(WorkflowTemplatePackLoader.Require(StartNodeId, "graph.startNodeId", context)),
            Nodes.Select(node => node.ToModel(context, componentId, jsonShape, nodeInstructionDefaults, executorPolicies)).ToArray(),
            Edges.Select(edge => edge.ToModel(context)).ToArray());
    }
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

    public WorkflowNode ToModel(
        string context,
        WorkflowComponentId componentId,
        WorkflowValueShape jsonShape,
        IReadOnlyDictionary<string, string> nodeInstructionDefaults,
        IReadOnlyDictionary<string, WorkflowTemplateExecutionPolicy> executorPolicies)
    {
        var nodeId = new WorkflowNodeId(WorkflowTemplatePackLoader.Require(Id, "node.id", context));
        var kind = ParseEnum<WorkflowNodeKind>(Kind, $"node '{nodeId}' kind", context);
        var instructions = string.IsNullOrWhiteSpace(Instructions)
            ? ResolveDefaultInstruction(kind, nodeInstructionDefaults)
            : Instructions.Trim();
        WorkflowExecutorId? executorId = Executor is null
            ? null
            : new WorkflowExecutorId(WorkflowTemplatePackLoader.Require(Executor.Id, $"node '{nodeId}' executor.id", context));
        var executionPolicy = Executor is null
            ? null
            : ResolveExecutionPolicy(Executor.Policy, executorPolicies, context, nodeId);

        return new WorkflowNode(
            nodeId,
            kind,
            WorkflowTemplatePackLoader.Require(Name, $"node '{nodeId}' name", context),
            BuildPorts(kind, jsonShape),
            new WorkflowNodeSettings(
                ComponentId: kind == WorkflowNodeKind.LlmCall ? componentId : null,
                AgentId: null,
                SubworkflowId: null,
                ExternalRequestKind: string.IsNullOrWhiteSpace(ExternalRequestKind)
                    ? null
                    : ParseEnum<WorkflowExternalRequestKind>(ExternalRequestKind, $"node '{nodeId}' externalRequestKind", context),
                Instructions: instructions,
                InputShape: kind == WorkflowNodeKind.Start ? null : jsonShape,
                ResultShape: kind == WorkflowNodeKind.End ? null : jsonShape)
            {
                ExecutorId = executorId,
                ExecutorSettingsJson = Executor is null ? string.Empty : WorkflowTemplatePackLoader.SerializeSettings(Executor.Settings),
                ExecutionPolicy = executionPolicy
            },
            X,
            Y);
    }

    private static string ResolveDefaultInstruction(
        WorkflowNodeKind kind,
        IReadOnlyDictionary<string, string> defaults)
        => defaults.TryGetValue(kind.ToString(), out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : string.Empty;

    private static WorkflowExecutorExecutionPolicy ResolveExecutionPolicy(
        string policy,
        IReadOnlyDictionary<string, WorkflowTemplateExecutionPolicy> executorPolicies,
        string context,
        WorkflowNodeId nodeId)
    {
        var policyKey = string.IsNullOrWhiteSpace(policy) ? "slow" : policy.Trim();
        if (!executorPolicies.TryGetValue(policyKey, out var templatePolicy))
        {
            throw new InvalidOperationException(
                $"Workflow template '{context}' node '{nodeId}' references unknown executor policy '{policyKey}'.");
        }

        return templatePolicy.ToModel($"{context} node '{nodeId}' executor policy");
    }

    private static IReadOnlyList<WorkflowPort> BuildPorts(WorkflowNodeKind kind, WorkflowValueShape jsonShape)
    {
        var ports = new List<WorkflowPort>();
        if (kind != WorkflowNodeKind.Start)
        {
            ports.Add(new WorkflowPort(
                new WorkflowPortId("workflow:input"),
                "Input",
                WorkflowPortDirection.Input,
                jsonShape,
                Required: true));
        }

        if (kind != WorkflowNodeKind.End)
        {
            ports.Add(new WorkflowPort(
                new WorkflowPortId("workflow:output"),
                "Output",
                WorkflowPortDirection.Output,
                jsonShape,
                Required: true));
        }

        return ports;
    }

    private static TEnum ParseEnum<TEnum>(string value, string fieldName, string context)
        where TEnum : struct, Enum
        => Enum.TryParse<TEnum>(value, ignoreCase: true, out var result) && Enum.IsDefined(result)
            ? result
            : throw new InvalidOperationException(
                $"Workflow template '{context}' has invalid {fieldName} '{value}'.");
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

    public WorkflowEdge ToModel(string context)
    {
        var source = new WorkflowNodeId(WorkflowTemplatePackLoader.Require(Source, "edge.source", context));
        var target = new WorkflowNodeId(WorkflowTemplatePackLoader.Require(Target, "edge.target", context));
        var id = string.IsNullOrWhiteSpace(Id)
            ? new WorkflowEdgeId($"{source}-to-{target}")
            : new WorkflowEdgeId(Id);
        var kind = string.IsNullOrWhiteSpace(Kind)
            ? WorkflowEdgeKind.Direct
            : ParseEnum<WorkflowEdgeKind>(Kind, $"edge '{id}' kind", context);

        return new WorkflowEdge(
            id,
            source,
            new WorkflowPortId("workflow:output"),
            target,
            new WorkflowPortId("workflow:input"),
            kind,
            ConditionExpression: string.Empty)
        {
            Routing = Routing?.ToModel(context, id) ?? WorkflowEdgeRouting.Always
        };
    }

    private static TEnum ParseEnum<TEnum>(string value, string fieldName, string context)
        where TEnum : struct, Enum
        => Enum.TryParse<TEnum>(value, ignoreCase: true, out var result) && Enum.IsDefined(result)
            ? result
            : throw new InvalidOperationException(
                $"Workflow template '{context}' has invalid {fieldName} '{value}'.");
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

    public WorkflowEdgeRouting ToModel(string context, WorkflowEdgeId edgeId)
    {
        var kind = ParseEnum<WorkflowRouteKind>(Kind, $"edge '{edgeId}' routing.kind", context);
        var valueKind = string.IsNullOrWhiteSpace(ExpectedValueKind)
            ? WorkflowRouteValueKind.Json
            : ParseEnum<WorkflowRouteValueKind>(ExpectedValueKind, $"edge '{edgeId}' routing.expectedValueKind", context);
        return new WorkflowEdgeRouting(
            kind,
            Label.Trim(),
            JsonPath.Trim(),
            string.IsNullOrWhiteSpace(Operator)
                ? WorkflowRouteOperator.Exists
                : ParseEnum<WorkflowRouteOperator>(Operator, $"edge '{edgeId}' routing.operator", context),
            ResolveExpectedValueJson(valueKind),
            valueKind,
            CaseSensitive,
            FanOutTargetIndex,
            WorkflowRoutingLanguages.BuiltInJsonV1);
    }

    private string ResolveExpectedValueJson(WorkflowRouteValueKind valueKind)
    {
        if (!string.IsNullOrWhiteSpace(ExpectedValueJson))
        {
            return ExpectedValueJson.Trim();
        }

        if (string.IsNullOrWhiteSpace(ExpectedValue))
        {
            return string.Empty;
        }

        return valueKind == WorkflowRouteValueKind.String
            ? JsonSerializer.Serialize(ExpectedValue)
            : ExpectedValue.Trim();
    }

    private static TEnum ParseEnum<TEnum>(string value, string fieldName, string context)
        where TEnum : struct, Enum
        => Enum.TryParse<TEnum>(value, ignoreCase: true, out var result) && Enum.IsDefined(result)
            ? result
            : throw new InvalidOperationException(
                $"Workflow template '{context}' has invalid {fieldName} '{value}'.");
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

    public WorkflowModelSettings ToModel(string context)
        => new(Temperature, MaxOutputTokens, RequireJsonOutput, ResponseFormatJsonSchema);
}

public sealed class WorkflowTemplateValueShape
{
    public string Kind { get; set; } = string.Empty;

    public string SchemaJson { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public WorkflowValueShape ToModel(string context)
        => new(
            ParseEnum<WorkflowValueShapeKind>(Kind, "value shape kind", context),
            SchemaJson,
            WorkflowTemplatePackLoader.Require(Description, "value shape description", context));

    private static TEnum ParseEnum<TEnum>(string value, string fieldName, string context)
        where TEnum : struct, Enum
        => Enum.TryParse<TEnum>(value, ignoreCase: true, out var result) && Enum.IsDefined(result)
            ? result
            : throw new InvalidOperationException(
                $"Workflow template '{context}' has invalid {fieldName} '{value}'.");
}

public sealed class WorkflowTemplateRuntimePolicy
{
    public string PreferredBackend { get; set; } = string.Empty;

    public bool AllowInProcessPreviewRuns { get; set; }

    public bool RequireDurableProductionRuns { get; set; }

    public bool ExposeAzureFunctionsStatusEndpoint { get; set; }

    public bool ExposeAzureFunctionsMcpTool { get; set; }

    public WorkflowRuntimePolicy ToModel(string context)
        => new(
            ParseEnum<WorkflowRuntimeBackendKind>(PreferredBackend, "runtime preferredBackend", context),
            AllowInProcessPreviewRuns,
            RequireDurableProductionRuns,
            ExposeAzureFunctionsStatusEndpoint,
            ExposeAzureFunctionsMcpTool);

    private static TEnum ParseEnum<TEnum>(string value, string fieldName, string context)
        where TEnum : struct, Enum
        => Enum.TryParse<TEnum>(value, ignoreCase: true, out var result) && Enum.IsDefined(result)
            ? result
            : throw new InvalidOperationException(
                $"Workflow template '{context}' has invalid {fieldName} '{value}'.");
}

public sealed class WorkflowTemplateExecutionPolicy
{
    public int TimeoutSeconds { get; set; }

    public int MaxRetryAttempts { get; set; }

    public int RetryDelayMilliseconds { get; set; }

    public bool CaptureOutputArtifact { get; set; }

    public WorkflowExecutorExecutionPolicy ToModel(string context)
    {
        var policy = new WorkflowExecutorExecutionPolicy(
            TimeoutSeconds,
            MaxRetryAttempts,
            RetryDelayMilliseconds,
            CaptureOutputArtifact);
        if (!WorkflowExecutorPolicyLimits.IsValid(policy))
        {
            throw new InvalidOperationException($"Workflow template '{context}' defines an invalid executor policy.");
        }

        return policy;
    }
}
