using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Plugins.Abstractions;
using CanDoItAll.SharedKernel.Configuration;

namespace CanDoItAll.Modules.Plugins;

public abstract class DockerWorkflowExecutorBase(
    PluginGrantEvaluator grantEvaluator,
    IPluginHostToolService hostToolService) : IWorkflowExecutor
{
    private const string SettingsSchemaJson = "{\"type\":\"object\"}";
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();
    private static readonly WorkflowValueShape JsonShape = new(
        WorkflowValueShapeKind.Json,
        "{}",
        "Docker command JSON result");
    private static readonly WorkflowExecutorSourceDescriptor PluginSource = WorkflowExecutorSourceDescriptor.BundledPlugin(
        DockerPluginConstants.PluginId.Value,
        "1.0.0");

    protected abstract WorkflowExecutorId ExecutorId { get; }

    protected abstract PluginHostToolRecipeId RecipeId { get; }

    protected abstract string Name { get; }

    protected abstract string Description { get; }

    protected virtual WorkflowExecutorExecutionPolicy DefaultPolicy => WorkflowExecutorExecutionPolicy.Default;

    public WorkflowExecutorDescriptor Descriptor => new(
        ExecutorId,
        Name,
        Description,
        WorkflowExecutorCategoryKind.Command,
        "terminal",
        DockerPluginConstants.SettingsRendererKey.Value,
        WorkflowValueShape.Text,
        JsonShape,
        SettingsSchemaJson,
        Serialize(new DockerWorkflowExecutorSettings()),
        DefaultPolicy,
        IsImplemented: true)
    {
        Source = PluginSource,
        Availability = ResolveAvailability(),
        SettingsSchema = WorkflowExecutorSettingsSchemaDescriptor.JsonSchema("1.0", SettingsSchemaJson),
        ConfigurationSchema = CreateConfigurationSchema()
    };

    public async ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
        WorkflowExecutorExecutionContext context,
        WorkflowNodeInput input,
        CancellationToken cancellationToken = default)
    {
        await RequireGrantAsync(PluginCapabilityKind.WorkflowExecutor, recipeId: null, cancellationToken);
        await RequireGrantAsync(PluginCapabilityKind.HostCommand, RecipeId, cancellationToken);
        var settings = Deserialize<DockerWorkflowExecutorSettings>(context.SettingsJson);
        var arguments = CreateArguments(settings, input);
        var hostResult = await hostToolService.ExecuteAsync(
            DockerPluginConstants.PluginId,
            RecipeId,
            arguments,
            context.Policy.TimeoutSeconds,
            settings.MaxOutputCharacters,
            cancellationToken);
        if (!hostResult.Succeeded)
        {
            throw new InvalidOperationException(hostResult.Message);
        }

        var payload = CreatePayload(settings, arguments, hostResult);
        return new WorkflowNodeExecutionResult(
            context.Node.Id,
            Serialize(payload),
            context.Descriptor.ResultShape);
    }

    protected abstract IReadOnlyDictionary<string, string> CreateArguments(
        DockerWorkflowExecutorSettings settings,
        WorkflowNodeInput input);

    protected virtual object CreatePayload(
        DockerWorkflowExecutorSettings settings,
        IReadOnlyDictionary<string, string> arguments,
        PluginHostToolExecutionResult hostResult)
        => new
        {
            pluginId = DockerPluginConstants.PluginId.Value,
            executorId = ExecutorId.Value,
            recipeId = RecipeId.Value,
            hostResult.Succeeded,
            hostResult.ExitCode,
            hostResult.Message,
            hostResult.Stdout,
            hostResult.Stderr,
            hostResult.StdoutTruncated,
            hostResult.StderrTruncated,
            hostResult.BoundaryMode,
            hostResult.BoundaryEnforced,
            hostResult.EnvironmentVariableNames,
            arguments
        };

    protected static string ResolveImage(DockerWorkflowExecutorSettings settings, WorkflowNodeInput input)
        => ResolveInputString(input, "image") is { Length: > 0 } image
            ? image
            : settings.Image;

    protected static string ResolveContainerName(DockerWorkflowExecutorSettings settings, WorkflowNodeInput input)
        => ResolveInputString(input, "containerName") is { Length: > 0 } containerName
            ? containerName
            : settings.ContainerName;

    protected static string ResolveInputString(WorkflowNodeInput input, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(input.PayloadJson))
        {
            return string.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(input.PayloadJson);
            return document.RootElement.ValueKind == JsonValueKind.Object &&
                   document.RootElement.TryGetProperty(propertyName, out var value)
                ? value.ValueKind == JsonValueKind.String
                    ? value.GetString()?.Trim() ?? string.Empty
                    : value.GetRawText()
                : string.Empty;
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    protected static string Serialize<T>(T value) => JsonSerializer.Serialize(value, SerializerOptions);

    protected static T Deserialize<T>(string json)
        => JsonSerializer.Deserialize<T>(json, SerializerOptions)
           ?? throw new InvalidOperationException($"Docker workflow settings could not be deserialized as {typeof(T).Name}.");

    private WorkflowExecutorAvailabilityDescriptor ResolveAvailability()
    {
        var workflowGrant = grantEvaluator.Evaluate(DockerPluginConstants.PluginId, PluginCapabilityKind.WorkflowExecutor);
        if (!workflowGrant.Allowed)
        {
            return WorkflowExecutorAvailabilityDescriptor.Unavailable(workflowGrant.Kind.ToString(), workflowGrant.Message);
        }

        var recipeGrant = grantEvaluator.Evaluate(DockerPluginConstants.PluginId, PluginCapabilityKind.HostCommand, RecipeId);
        return recipeGrant.Allowed
            ? WorkflowExecutorAvailabilityDescriptor.Available()
            : WorkflowExecutorAvailabilityDescriptor.Unavailable(recipeGrant.Kind.ToString(), recipeGrant.Message);
    }

    private async Task RequireGrantAsync(
        PluginCapabilityKind capability,
        PluginHostToolRecipeId? recipeId,
        CancellationToken cancellationToken)
    {
        var decision = await grantEvaluator.EvaluateAsync(
            DockerPluginConstants.PluginId,
            capability,
            recipeId,
            cancellationToken);
        if (!decision.Allowed)
        {
            throw new InvalidOperationException(decision.Message);
        }
    }

    private static ConfigurationSchema CreateConfigurationSchema()
        => new(
            "1.0",
            [
                new ConfigurationFieldDescriptor("image", "Image", ConfigurationFieldType.Text, IsRequired: false, "Docker image reference."),
                new ConfigurationFieldDescriptor("containerName", "Container name", ConfigurationFieldType.Text, IsRequired: false, "Docker container name."),
                new ConfigurationFieldDescriptor("pullIfMissing", "Pull if missing", ConfigurationFieldType.Boolean, IsRequired: false, "Pull the image before creating the container when it is not available locally."),
                new ConfigurationFieldDescriptor("portMappings", "Port mappings", ConfigurationFieldType.Json, IsRequired: false, "JSON array of host:container port mappings."),
                new ConfigurationFieldDescriptor("tail", "Log tail", ConfigurationFieldType.Number, IsRequired: false, "Maximum number of log lines to read."),
                new ConfigurationFieldDescriptor("since", "Logs since", ConfigurationFieldType.Text, IsRequired: false, "Optional docker logs --since value."),
                new ConfigurationFieldDescriptor("maxOutputCharacters", "Output cap", ConfigurationFieldType.Number, IsRequired: false, "Maximum stdout/stderr characters captured.")
            ]);

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}

public sealed class DockerListContainersWorkflowExecutor(
    PluginGrantEvaluator grantEvaluator,
    IPluginHostToolService hostToolService) : DockerWorkflowExecutorBase(grantEvaluator, hostToolService)
{
    protected override WorkflowExecutorId ExecutorId => DockerPluginConstants.ListContainersExecutorId;

    protected override PluginHostToolRecipeId RecipeId => PluginHostToolRecipeIds.DockerListContainers;

    protected override string Name => "Docker containers";

    protected override string Description => "Lists running Docker containers through the guarded Docker plugin.";

    protected override WorkflowExecutorExecutionPolicy DefaultPolicy => WorkflowExecutorExecutionPolicy.Default with { TimeoutSeconds = 20 };

    protected override IReadOnlyDictionary<string, string> CreateArguments(
        DockerWorkflowExecutorSettings settings,
        WorkflowNodeInput input)
        => new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

public sealed class DockerPullImageWorkflowExecutor(
    PluginGrantEvaluator grantEvaluator,
    IPluginHostToolService hostToolService) : DockerWorkflowExecutorBase(grantEvaluator, hostToolService)
{
    protected override WorkflowExecutorId ExecutorId => DockerPluginConstants.PullImageExecutorId;

    protected override PluginHostToolRecipeId RecipeId => PluginHostToolRecipeIds.DockerPullImage;

    protected override string Name => "Docker pull image";

    protected override string Description => "Pulls a validated Docker image through the guarded Docker plugin.";

    protected override WorkflowExecutorExecutionPolicy DefaultPolicy => WorkflowExecutorExecutionPolicy.Default with { TimeoutSeconds = 900, CaptureOutputArtifact = true };

    protected override IReadOnlyDictionary<string, string> CreateArguments(
        DockerWorkflowExecutorSettings settings,
        WorkflowNodeInput input)
        => new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["image"] = ResolveImage(settings, input)
        };

    protected override object CreatePayload(
        DockerWorkflowExecutorSettings settings,
        IReadOnlyDictionary<string, string> arguments,
        PluginHostToolExecutionResult hostResult)
        => new
        {
            pluginId = DockerPluginConstants.PluginId.Value,
            executorId = ExecutorId.Value,
            recipeId = RecipeId.Value,
            image = arguments["image"],
            hostResult.Succeeded,
            hostResult.ExitCode,
            output = hostResult.Stdout,
            errors = hostResult.Stderr,
            hostResult.StdoutTruncated,
            hostResult.StderrTruncated,
            hostResult.BoundaryMode,
            hostResult.BoundaryEnforced
        };
}

public sealed class DockerStartContainerWorkflowExecutor(
    PluginGrantEvaluator grantEvaluator,
    IPluginHostToolService hostToolService) : DockerWorkflowExecutorBase(grantEvaluator, hostToolService)
{
    protected override WorkflowExecutorId ExecutorId => DockerPluginConstants.StartContainerExecutorId;

    protected override PluginHostToolRecipeId RecipeId => PluginHostToolRecipeIds.DockerStartContainer;

    protected override string Name => "Docker start container";

    protected override string Description => "Starts an existing container or creates one from a validated image through the guarded Docker plugin.";

    protected override WorkflowExecutorExecutionPolicy DefaultPolicy => WorkflowExecutorExecutionPolicy.Default with { TimeoutSeconds = 120, CaptureOutputArtifact = true };

    protected override IReadOnlyDictionary<string, string> CreateArguments(
        DockerWorkflowExecutorSettings settings,
        WorkflowNodeInput input)
        => new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["image"] = ResolveImage(settings, input),
            ["containerName"] = ResolveContainerName(settings, input),
            ["pullIfMissing"] = settings.PullIfMissing.ToString(),
            ["portMappings"] = string.Join(',', settings.PortMappings)
        };

    protected override object CreatePayload(
        DockerWorkflowExecutorSettings settings,
        IReadOnlyDictionary<string, string> arguments,
        PluginHostToolExecutionResult hostResult)
        => new
        {
            pluginId = DockerPluginConstants.PluginId.Value,
            executorId = ExecutorId.Value,
            recipeId = RecipeId.Value,
            image = arguments["image"],
            containerName = arguments["containerName"],
            containerIdOrName = hostResult.Stdout,
            hostResult.Succeeded,
            hostResult.ExitCode,
            errors = hostResult.Stderr,
            hostResult.StdoutTruncated,
            hostResult.StderrTruncated,
            hostResult.BoundaryMode,
            hostResult.BoundaryEnforced
        };
}

public sealed class DockerReadLogsWorkflowExecutor(
    PluginGrantEvaluator grantEvaluator,
    IPluginHostToolService hostToolService) : DockerWorkflowExecutorBase(grantEvaluator, hostToolService)
{
    protected override WorkflowExecutorId ExecutorId => DockerPluginConstants.ReadLogsExecutorId;

    protected override PluginHostToolRecipeId RecipeId => PluginHostToolRecipeIds.DockerReadLogs;

    protected override string Name => "Docker logs";

    protected override string Description => "Reads bounded logs from a Docker container through the guarded Docker plugin.";

    protected override WorkflowExecutorExecutionPolicy DefaultPolicy => WorkflowExecutorExecutionPolicy.Default with { TimeoutSeconds = 30, CaptureOutputArtifact = true };

    protected override IReadOnlyDictionary<string, string> CreateArguments(
        DockerWorkflowExecutorSettings settings,
        WorkflowNodeInput input)
        => new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["containerName"] = ResolveContainerName(settings, input),
            ["tail"] = settings.Tail.ToString(),
            ["since"] = settings.Since
        };

    protected override object CreatePayload(
        DockerWorkflowExecutorSettings settings,
        IReadOnlyDictionary<string, string> arguments,
        PluginHostToolExecutionResult hostResult)
        => new
        {
            pluginId = DockerPluginConstants.PluginId.Value,
            executorId = ExecutorId.Value,
            recipeId = RecipeId.Value,
            containerName = arguments["containerName"],
            logs = hostResult.Stdout,
            errors = hostResult.Stderr,
            hostResult.Succeeded,
            hostResult.ExitCode,
            hostResult.StdoutTruncated,
            hostResult.StderrTruncated,
            hostResult.BoundaryMode,
            hostResult.BoundaryEnforced
        };
}
