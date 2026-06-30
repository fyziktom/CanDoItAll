using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Transforms;

public sealed class JsonTransformWorkflowExecutor : IWorkflowExecutor
{
    public WorkflowExecutorDescriptor Descriptor => BuiltInWorkflowExecutorDescriptors.JsonTransform;

    public ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
        WorkflowExecutorExecutionContext context,
        WorkflowNodeInput input,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var settings = WorkflowExecutorJson.Deserialize<WorkflowJsonTransformExecutorSettings>(context.SettingsJson);
        var current = ParsePayload(input.PayloadJson);

        foreach (var step in settings.Operations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            current = ApplyStep(current, step);
        }

        var output = current?.ToJsonString(WorkflowExecutorJson.Options) ?? "null";
        var maxOutputCharacters = Math.Clamp(settings.MaxOutputCharacters, 1, 2_000_000);
        if (output.Length > maxOutputCharacters)
        {
            throw new InvalidOperationException($"JSON transform output exceeds the configured limit of {maxOutputCharacters} characters.");
        }

        return ValueTask.FromResult(new WorkflowNodeExecutionResult(
            context.Node.Id,
            output,
            context.Descriptor.ResultShape));
    }

    private static JsonNode? ApplyStep(JsonNode? current, WorkflowJsonTransformStep step)
    {
        return step.Operation switch
        {
            WorkflowJsonTransformOperation.Select => Clone(ResolveRequired(current, step.Path)),
            WorkflowJsonTransformOperation.Set => SetValue(current, step.DestinationPath, ParseJsonValue(step.ValueJson)),
            WorkflowJsonTransformOperation.Remove => RemoveValue(current, step.Path),
            WorkflowJsonTransformOperation.Merge => MergeValue(current, step.Path, ParseJsonValue(step.ValueJson)),
            WorkflowJsonTransformOperation.Count => SetValue(current, step.DestinationPath, JsonValue.Create(CountValue(ResolveRequired(current, step.Path)))),
            WorkflowJsonTransformOperation.Template => ApplyTemplate(current, step),
            WorkflowJsonTransformOperation.ArrayMap => MapArray(current, step),
            WorkflowJsonTransformOperation.ArrayFilter => FilterArray(current, step),
            WorkflowJsonTransformOperation.ArraySort => SortArray(current, step),
            WorkflowJsonTransformOperation.ArrayDistinct => DistinctArray(current, step),
            WorkflowJsonTransformOperation.ArrayTake => TakeArray(current, step),
            WorkflowJsonTransformOperation.ValidateSchema => ValidateRequiredPaths(current, step),
            _ => throw new InvalidOperationException($"JSON transform operation '{step.Operation}' is not supported.")
        };
    }

    private static JsonNode? SetValue(JsonNode? root, string destinationPath, JsonNode? value)
    {
        if (IsRootPath(destinationPath))
        {
            return Clone(value);
        }

        var normalizedRoot = root ?? new JsonObject();
        var (parent, segment) = ResolveParent(normalizedRoot, destinationPath, createMissing: true);
        if (segment.PropertyName is not null)
        {
            if (parent is not JsonObject parentObject)
            {
                throw new InvalidOperationException($"JSON transform destination path '{destinationPath}' does not resolve to an object property.");
            }

            parentObject[segment.PropertyName] = Clone(value);
            return normalizedRoot;
        }

        if (parent is not JsonArray parentArray || segment.Index is not { } index)
        {
            throw new InvalidOperationException($"JSON transform destination path '{destinationPath}' does not resolve to an array index.");
        }

        if (index < 0 || index > parentArray.Count)
        {
            throw new InvalidOperationException($"JSON transform destination array index '{index}' is outside the allowed range.");
        }

        if (index == parentArray.Count)
        {
            parentArray.Add(Clone(value));
        }
        else
        {
            parentArray[index] = Clone(value);
        }

        return normalizedRoot;
    }

    private static JsonNode? RemoveValue(JsonNode? root, string path)
    {
        if (IsRootPath(path))
        {
            return new JsonObject();
        }

        var normalizedRoot = root ?? new JsonObject();
        var (parent, segment) = ResolveParent(normalizedRoot, path, createMissing: false);
        if (segment.PropertyName is not null && parent is JsonObject parentObject)
        {
            if (!parentObject.Remove(segment.PropertyName))
            {
                throw new InvalidOperationException($"JSON transform path '{path}' was not found.");
            }

            return normalizedRoot;
        }

        if (segment.Index is { } index && parent is JsonArray parentArray && index >= 0 && index < parentArray.Count)
        {
            parentArray.RemoveAt(index);
            return normalizedRoot;
        }

        throw new InvalidOperationException($"JSON transform path '{path}' was not found.");
    }

    private static JsonNode? MergeValue(JsonNode? root, string path, JsonNode? value)
    {
        var target = ResolveRequired(root, path);
        if (target is not JsonObject targetObject || value is not JsonObject sourceObject)
        {
            throw new InvalidOperationException("JSON transform merge requires both target and value to be JSON objects.");
        }

        foreach (var property in sourceObject)
        {
            targetObject[property.Key] = Clone(property.Value);
        }

        return root;
    }

    private static JsonNode? ApplyTemplate(JsonNode? root, WorkflowJsonTransformStep step)
    {
        var template = new JsonObject();
        foreach (var binding in step.Template)
        {
            template[binding.Key] = Clone(ResolveRequired(root, binding.Value));
        }

        return IsRootPath(step.DestinationPath)
            ? template
            : SetValue(root, step.DestinationPath, template);
    }

    private static JsonNode? MapArray(JsonNode? root, WorkflowJsonTransformStep step)
    {
        var array = ResolveArray(root, step.Path);
        var mapped = new JsonArray();
        foreach (var item in array)
        {
            mapped.Add(string.IsNullOrWhiteSpace(step.Key)
                ? Clone(item)
                : Clone(ResolveRequired(item, NormalizeRelativePath(step.Key))));
        }

        return IsRootPath(step.DestinationPath)
            ? mapped
            : SetValue(root, step.DestinationPath, mapped);
    }

    private static JsonNode? FilterArray(JsonNode? root, WorkflowJsonTransformStep step)
    {
        var array = ResolveArray(root, step.Path);
        var expected = string.IsNullOrWhiteSpace(step.ExpectedValueJson)
            ? null
            : ParseJsonValue(step.ExpectedValueJson);
        var filtered = new JsonArray();

        foreach (var item in array)
        {
            var candidate = string.IsNullOrWhiteSpace(step.PredicatePath)
                ? item
                : ResolveRequired(item, NormalizeRelativePath(step.PredicatePath));
            if (JsonEquals(candidate, expected))
            {
                filtered.Add(Clone(item));
            }
        }

        return IsRootPath(step.DestinationPath)
            ? filtered
            : SetValue(root, step.DestinationPath, filtered);
    }

    private static JsonNode? SortArray(JsonNode? root, WorkflowJsonTransformStep step)
    {
        var array = ResolveArray(root, step.Path);
        var sorted = array
            .Select(item => Clone(item))
            .OrderBy(item => ReadSortKey(string.IsNullOrWhiteSpace(step.PredicatePath)
                ? item
                : ResolveRequired(item, NormalizeRelativePath(step.PredicatePath))), StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var output = new JsonArray(sorted);

        return IsRootPath(step.DestinationPath)
            ? output
            : SetValue(root, step.DestinationPath, output);
    }

    private static JsonNode? DistinctArray(JsonNode? root, WorkflowJsonTransformStep step)
    {
        var array = ResolveArray(root, step.Path);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var output = new JsonArray();
        foreach (var item in array)
        {
            var keyNode = string.IsNullOrWhiteSpace(step.PredicatePath)
                ? item
                : ResolveRequired(item, NormalizeRelativePath(step.PredicatePath));
            var key = keyNode?.ToJsonString(WorkflowExecutorJson.Options) ?? "null";
            if (seen.Add(key))
            {
                output.Add(Clone(item));
            }
        }

        return IsRootPath(step.DestinationPath)
            ? output
            : SetValue(root, step.DestinationPath, output);
    }

    private static JsonNode? TakeArray(JsonNode? root, WorkflowJsonTransformStep step)
    {
        var array = ResolveArray(root, step.Path);
        var output = new JsonArray(array.Take(Math.Max(0, step.Take)).Select(Clone).ToArray());
        return IsRootPath(step.DestinationPath)
            ? output
            : SetValue(root, step.DestinationPath, output);
    }

    private static JsonNode? ValidateRequiredPaths(JsonNode? root, WorkflowJsonTransformStep step)
    {
        foreach (var requiredPath in step.RequiredPaths)
        {
            ResolveRequired(root, requiredPath);
        }

        return root;
    }

    private static int CountValue(JsonNode? node)
        => node switch
        {
            JsonArray array => array.Count,
            JsonObject jsonObject => jsonObject.Count,
            null => 0,
            _ => 1
        };

    private static JsonArray ResolveArray(JsonNode? root, string path)
        => ResolveRequired(root, path) as JsonArray
           ?? throw new InvalidOperationException($"JSON transform path '{path}' did not resolve to an array.");

    private static JsonNode? ResolveRequired(JsonNode? root, string path)
    {
        if (root is null)
        {
            throw new InvalidOperationException("JSON transform input payload is empty.");
        }

        var current = root;
        foreach (var segment in ParsePath(path))
        {
            if (segment.PropertyName is not null)
            {
                if (current is not JsonObject currentObject ||
                    !currentObject.TryGetPropertyValue(segment.PropertyName, out current))
                {
                    throw new InvalidOperationException($"JSON transform path '{path}' was not found.");
                }

                continue;
            }

            if (current is not JsonArray currentArray ||
                segment.Index is not { } index ||
                index < 0 ||
                index >= currentArray.Count)
            {
                throw new InvalidOperationException($"JSON transform path '{path}' was not found.");
            }

            current = currentArray[index];
        }

        return current;
    }

    private static (JsonNode Parent, BuiltInJsonPathSegment Segment) ResolveParent(
        JsonNode root,
        string path,
        bool createMissing)
    {
        var segments = ParsePath(path);
        if (segments.Count == 0)
        {
            throw new InvalidOperationException("JSON transform root path does not have a parent.");
        }

        var current = root;
        foreach (var segment in segments.Take(segments.Count - 1))
        {
            if (segment.PropertyName is not null)
            {
                if (current is not JsonObject currentObject)
                {
                    throw new InvalidOperationException($"JSON transform path '{path}' does not resolve through an object.");
                }

                if (!currentObject.TryGetPropertyValue(segment.PropertyName, out var next) || next is null)
                {
                    if (!createMissing)
                    {
                        throw new InvalidOperationException($"JSON transform path '{path}' was not found.");
                    }

                    next = new JsonObject();
                    currentObject[segment.PropertyName] = next;
                }

                current = next;
                continue;
            }

            if (current is not JsonArray currentArray ||
                segment.Index is not { } index ||
                index < 0 ||
                index >= currentArray.Count ||
                currentArray[index] is not { } nextArrayItem)
            {
                throw new InvalidOperationException($"JSON transform path '{path}' was not found.");
            }

            current = nextArrayItem;
        }

        return (current, segments[^1]);
    }

    private static IReadOnlyList<BuiltInJsonPathSegment> ParsePath(string path)
    {
        var normalized = string.IsNullOrWhiteSpace(path) ? "$" : path.Trim();
        if (!WorkflowRoutingValidation.TryParseJsonPath(normalized, out var segments, out var error))
        {
            throw new InvalidOperationException($"JSON transform path '{path}' is invalid: {error}.");
        }

        return segments;
    }

    private static JsonNode? ParsePayload(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return new JsonObject();
        }

        try
        {
            return JsonNode.Parse(payload);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException($"JSON transform input payload is invalid JSON: {exception.Message}", exception);
        }
    }

    private static JsonNode? ParseJsonValue(string valueJson)
    {
        if (string.IsNullOrWhiteSpace(valueJson))
        {
            return JsonValue.Create(string.Empty);
        }

        try
        {
            return JsonNode.Parse(valueJson);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException($"JSON transform value JSON is invalid: {exception.Message}", exception);
        }
    }

    private static JsonNode? Clone(JsonNode? node)
        => node?.DeepClone();

    private static string NormalizeRelativePath(string path)
        => path.StartsWith('$')
            ? path
            : "$." + path.TrimStart('.');

    private static bool IsRootPath(string path)
        => string.IsNullOrWhiteSpace(path) || string.Equals(path.Trim(), "$", StringComparison.Ordinal);

    private static bool JsonEquals(JsonNode? left, JsonNode? right)
        => string.Equals(
            left?.ToJsonString(WorkflowExecutorJson.Options) ?? "null",
            right?.ToJsonString(WorkflowExecutorJson.Options) ?? "null",
            StringComparison.Ordinal);

    private static string ReadSortKey(JsonNode? node)
    {
        if (node is null)
        {
            return string.Empty;
        }

        if (node is JsonValue value)
        {
            return value.ToJsonString(WorkflowExecutorJson.Options).Trim('"');
        }

        return node.ToJsonString(WorkflowExecutorJson.Options);
    }
}
