using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.WorkflowExecutors.Plugins;
using CanDoItAll.Plugins.Abstractions;

namespace CanDoItAll.Modules.Plugins;

public abstract class DockerWorkflowExecutorBase(
    IPluginWorkflowExecutorGrantEvaluator grantEvaluator,
    IPluginHostToolService hostToolService) : IWorkflowExecutor
{
    private static readonly JsonSerializerOptions SerializerOptions = DockerWorkflowJson.Options;

    protected abstract WorkflowExecutorDescriptor DescriptorDefinition { get; }

    protected WorkflowExecutorId ExecutorId => DescriptorDefinition.Id;

    protected abstract PluginHostToolRecipeId RecipeId { get; }

    public WorkflowExecutorDescriptor Descriptor => DescriptorDefinition with
    {
        Availability = ResolveAvailability()
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
        cancellationToken.ThrowIfCancellationRequested();
        var decision = grantEvaluator.Evaluate(
            DockerPluginConstants.PluginId,
            capability,
            recipeId);
        if (!decision.Allowed)
        {
            throw new InvalidOperationException(decision.Message);
        }
    }

}

internal static class DockerWorkflowSimulationTemplates
{
    public static WorkflowExecutorSimulationDescriptor CommandResult { get; } = WorkflowExecutorSimulationDescriptor.JsonTemplate(
        """
        {
          "pluginId": "candoitall.docker",
          "executorId": "{{source.executor.id}}",
          "recipeId": "simulated-preview-recipe",
          "succeeded": true,
          "exitCode": 0,
          "message": "Docker workflow executor was simulated for Run Preview.",
          "stdout": "simulated docker output",
          "stderr": "",
          "stdoutTruncated": false,
          "stderrTruncated": false,
          "boundaryMode": "preview-simulation",
          "boundaryEnforced": true,
          "environmentVariableNames": [],
          "arguments": {},
          "inputPayload": "{{inputPayload}}",
          "simulation": {
            "nodeId": "{{node.id}}",
            "nodeName": "{{node.name}}",
            "sourceExecutorId": "{{source.executor.id}}",
            "reason": "{{simulation.reason}}",
            "generatedAtUtc": "{{utcNow}}"
          }
        }
        """,
        "Simulate Docker host-tool output without running Docker.");
}

public sealed class DockerListContainersWorkflowExecutor(
    IPluginWorkflowExecutorGrantEvaluator grantEvaluator,
    IPluginHostToolService hostToolService) : DockerWorkflowExecutorBase(grantEvaluator, hostToolService)
{
    protected override WorkflowExecutorDescriptor DescriptorDefinition => DockerWorkflowExecutorDescriptors.ListContainers;

    protected override PluginHostToolRecipeId RecipeId => PluginHostToolRecipeIds.DockerListContainers;

    protected override IReadOnlyDictionary<string, string> CreateArguments(
        DockerWorkflowExecutorSettings settings,
        WorkflowNodeInput input)
        => new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

public sealed class DockerPullImageWorkflowExecutor(
    IPluginWorkflowExecutorGrantEvaluator grantEvaluator,
    IPluginHostToolService hostToolService) : DockerWorkflowExecutorBase(grantEvaluator, hostToolService)
{
    protected override WorkflowExecutorDescriptor DescriptorDefinition => DockerWorkflowExecutorDescriptors.PullImage;

    protected override PluginHostToolRecipeId RecipeId => PluginHostToolRecipeIds.DockerPullImage;

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
    IPluginWorkflowExecutorGrantEvaluator grantEvaluator,
    IPluginHostToolService hostToolService) : DockerWorkflowExecutorBase(grantEvaluator, hostToolService)
{
    protected override WorkflowExecutorDescriptor DescriptorDefinition => DockerWorkflowExecutorDescriptors.StartContainer;

    protected override PluginHostToolRecipeId RecipeId => PluginHostToolRecipeIds.DockerStartContainer;

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
    IPluginWorkflowExecutorGrantEvaluator grantEvaluator,
    IPluginHostToolService hostToolService) : DockerWorkflowExecutorBase(grantEvaluator, hostToolService)
{
    protected override WorkflowExecutorDescriptor DescriptorDefinition => DockerWorkflowExecutorDescriptors.ReadLogs;

    protected override PluginHostToolRecipeId RecipeId => PluginHostToolRecipeIds.DockerReadLogs;

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
