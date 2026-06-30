using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public static class WorkflowPreviewSimulationTemplateTokens
{
    public const string InputPayload = "{{inputPayload}}";
    public const string NodeId = "{{node.id}}";
    public const string NodeName = "{{node.name}}";
    public const string SourceExecutorId = "{{source.executor.id}}";
    public const string SimulationReason = "{{simulation.reason}}";
    public const string UtcNow = "{{utcNow}}";
    public const string InputPathPrefix = "{{inputPath:";
    public const string SettingsPathPrefix = "{{settingsPath:";
    public const string TokenSuffix = "}}";
}

public static class WorkflowPreviewSimulationRenderer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string Render(
        WorkflowPreviewSimulationStep step,
        WorkflowDefinition definition,
        WorkflowNode node,
        WorkflowNodeInput input,
        DateTimeOffset? generatedAtUtc = null)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(input);

        if (string.IsNullOrWhiteSpace(step.OutputTemplateJson))
        {
            throw new InvalidOperationException($"Preview simulation for workflow node '{node.Id}' does not define an output template.");
        }

        JsonNode template;
        try
        {
            template = JsonNode.Parse(step.OutputTemplateJson) ??
                       throw new InvalidOperationException($"Preview simulation for workflow node '{node.Id}' must be a JSON object, array, or value.");
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                $"Preview simulation for workflow node '{node.Id}' has invalid output template JSON: {exception.Message}",
                exception);
        }

        var context = new RenderContext(
            step,
            definition,
            node,
            ParseInputPayload(input.PayloadJson),
            ParseSettings(node),
            generatedAtUtc ?? DateTimeOffset.UtcNow);
        var rendered = RenderNode(template, context);
        return rendered?.ToJsonString(JsonOptions) ?? "null";
    }

    private static JsonNode? RenderNode(
        JsonNode? template,
        RenderContext context)
    {
        return template switch
        {
            JsonObject sourceObject => RenderObject(sourceObject, context),
            JsonArray sourceArray => RenderArray(sourceArray, context),
            JsonValue sourceValue => RenderValue(sourceValue, context),
            null => null,
            _ => template.DeepClone()
        };
    }

    private static JsonObject RenderObject(
        JsonObject source,
        RenderContext context)
    {
        var target = new JsonObject();
        foreach (var item in source)
        {
            target[item.Key] = RenderNode(item.Value, context);
        }

        return target;
    }

    private static JsonArray RenderArray(
        JsonArray source,
        RenderContext context)
    {
        var target = new JsonArray();
        foreach (var item in source)
        {
            target.Add(RenderNode(item, context));
        }

        return target;
    }

    private static JsonNode? RenderValue(
        JsonValue source,
        RenderContext context)
    {
        if (!source.TryGetValue<string>(out var text))
        {
            return source.DeepClone();
        }

        if (string.Equals(text, WorkflowPreviewSimulationTemplateTokens.InputPayload, StringComparison.Ordinal))
        {
            return context.InputPayload?.DeepClone();
        }

        if (TryReadPathToken(text, WorkflowPreviewSimulationTemplateTokens.InputPathPrefix, out var inputPath))
        {
            return ResolvePath(context.InputPayload, inputPath);
        }

        if (TryReadPathToken(text, WorkflowPreviewSimulationTemplateTokens.SettingsPathPrefix, out var settingsPath))
        {
            return ResolvePath(context.Settings, settingsPath);
        }

        return JsonValue.Create(ReplaceScalarTokens(text, context));
    }

    private static string ReplaceScalarTokens(
        string value,
        RenderContext context)
        => value
            .Replace(WorkflowPreviewSimulationTemplateTokens.NodeId, context.Node.Id.Value, StringComparison.Ordinal)
            .Replace(WorkflowPreviewSimulationTemplateTokens.NodeName, context.Node.Name, StringComparison.Ordinal)
            .Replace(
                WorkflowPreviewSimulationTemplateTokens.SourceExecutorId,
                context.Step.SourceExecutorId?.Value ?? string.Empty,
                StringComparison.Ordinal)
            .Replace(WorkflowPreviewSimulationTemplateTokens.SimulationReason, context.Step.Reason, StringComparison.Ordinal)
            .Replace(WorkflowPreviewSimulationTemplateTokens.UtcNow, context.GeneratedAtUtc.ToString("O"), StringComparison.Ordinal);

    private static bool TryReadPathToken(
        string value,
        string prefix,
        out string jsonPath)
    {
        if (value.StartsWith(prefix, StringComparison.Ordinal) &&
            value.EndsWith(WorkflowPreviewSimulationTemplateTokens.TokenSuffix, StringComparison.Ordinal))
        {
            jsonPath = value[prefix.Length..^WorkflowPreviewSimulationTemplateTokens.TokenSuffix.Length].Trim();
            return true;
        }

        jsonPath = string.Empty;
        return false;
    }

    private static JsonNode? ResolvePath(
        JsonNode? root,
        string jsonPath)
    {
        if (root is null)
        {
            return null;
        }

        if (!WorkflowRoutingValidation.TryParseJsonPath(jsonPath, out var path, out var pathError))
        {
            throw new InvalidOperationException($"Preview simulation JSON path '{jsonPath}' is invalid: {pathError}.");
        }

        var current = root;
        foreach (var segment in path)
        {
            if (segment.PropertyName is not null)
            {
                if (current is not JsonObject currentObject ||
                    !currentObject.TryGetPropertyValue(segment.PropertyName, out current))
                {
                    return null;
                }

                continue;
            }

            if (segment.Index is not { } index ||
                current is not JsonArray currentArray ||
                index < 0 ||
                index >= currentArray.Count)
            {
                return null;
            }

            current = currentArray[index];
        }

        return current?.DeepClone();
    }

    private static JsonNode? ParseInputPayload(string payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(payloadJson);
        }
        catch (JsonException)
        {
            return JsonValue.Create(payloadJson);
        }
    }

    private static JsonNode? ParseSettings(WorkflowNode node)
    {
        if (string.IsNullOrWhiteSpace(node.Settings.ExecutorSettingsJson))
        {
            return new JsonObject();
        }

        try
        {
            return JsonNode.Parse(node.Settings.ExecutorSettingsJson) ?? new JsonObject();
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                $"Preview simulation for workflow node '{node.Id}' cannot read executor settings JSON: {exception.Message}",
                exception);
        }
    }

    private sealed record RenderContext(
        WorkflowPreviewSimulationStep Step,
        WorkflowDefinition Definition,
        WorkflowNode Node,
        JsonNode? InputPayload,
        JsonNode? Settings,
        DateTimeOffset GeneratedAtUtc);
}
