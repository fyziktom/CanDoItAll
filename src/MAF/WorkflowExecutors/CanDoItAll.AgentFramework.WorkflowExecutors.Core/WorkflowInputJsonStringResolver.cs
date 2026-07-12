using System.Globalization;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public static class WorkflowInputJsonStringResolver
{
    public static string ResolveRequired(
        string configuredValue,
        string inputJsonPath,
        WorkflowNodeInput input,
        string executorName,
        string configuredSettingName,
        string jsonPathSettingName)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentException.ThrowIfNullOrWhiteSpace(executorName);
        ArgumentException.ThrowIfNullOrWhiteSpace(configuredSettingName);
        ArgumentException.ThrowIfNullOrWhiteSpace(jsonPathSettingName);

        if (!string.IsNullOrWhiteSpace(configuredValue))
        {
            return configuredValue.Trim();
        }

        if (string.IsNullOrWhiteSpace(inputJsonPath))
        {
            throw new InvalidOperationException(
                $"{executorName} executor setting '{configuredSettingName}' or '{jsonPathSettingName}' is required.");
        }

        if (string.IsNullOrWhiteSpace(input.PayloadJson))
        {
            throw new InvalidOperationException(
                $"{executorName} executor setting '{jsonPathSettingName}' requires a workflow JSON payload.");
        }

        using var document = ParsePayload(input.PayloadJson, executorName, jsonPathSettingName);
        var normalizedPath = inputJsonPath.Trim();
        var value = ResolvePath(document.RootElement, normalizedPath, executorName, jsonPathSettingName);
        if (value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new InvalidOperationException(
                $"{executorName} executor setting '{jsonPathSettingName}' path '{normalizedPath}' must resolve to a non-empty JSON string.");
        }

        return value.GetString()!.Trim();
    }

    private static JsonDocument ParsePayload(
        string payloadJson,
        string executorName,
        string jsonPathSettingName)
    {
        try
        {
            return JsonDocument.Parse(payloadJson);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                $"{executorName} executor setting '{jsonPathSettingName}' requires valid workflow JSON: {exception.Message}",
                exception);
        }
    }

    private static JsonElement ResolvePath(
        JsonElement root,
        string jsonPath,
        string executorName,
        string jsonPathSettingName)
    {
        var segments = ParsePath(jsonPath, executorName, jsonPathSettingName);
        var current = root;
        foreach (var segment in segments)
        {
            if (segment.PropertyName is not null)
            {
                if (current.ValueKind != JsonValueKind.Object ||
                    !current.TryGetProperty(segment.PropertyName, out current))
                {
                    throw CreateMissingPathException(executorName, jsonPathSettingName, jsonPath);
                }

                continue;
            }

            if (segment.Index is not { } index ||
                current.ValueKind != JsonValueKind.Array ||
                index >= current.GetArrayLength())
            {
                throw CreateMissingPathException(executorName, jsonPathSettingName, jsonPath);
            }

            current = current[index];
        }

        return current;
    }

    private static IReadOnlyList<JsonPathSegment> ParsePath(
        string jsonPath,
        string executorName,
        string jsonPathSettingName)
    {
        if (jsonPath.Length == 0 || jsonPath[0] != '$')
        {
            throw CreateInvalidPathException(executorName, jsonPathSettingName, jsonPath);
        }

        var segments = new List<JsonPathSegment>();
        var index = 1;
        while (index < jsonPath.Length)
        {
            if (jsonPath[index] == '.')
            {
                var propertyStart = ++index;
                while (index < jsonPath.Length && jsonPath[index] is not ('.' or '['))
                {
                    index++;
                }

                if (propertyStart == index)
                {
                    throw CreateInvalidPathException(executorName, jsonPathSettingName, jsonPath);
                }

                segments.Add(new JsonPathSegment(jsonPath[propertyStart..index], Index: null));
                continue;
            }

            if (jsonPath[index] == '[')
            {
                var bracketEnd = jsonPath.IndexOf(']', index + 1);
                if (bracketEnd < 0 ||
                    !int.TryParse(
                        jsonPath[(index + 1)..bracketEnd],
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out var arrayIndex))
                {
                    throw CreateInvalidPathException(executorName, jsonPathSettingName, jsonPath);
                }

                segments.Add(new JsonPathSegment(PropertyName: null, arrayIndex));
                index = bracketEnd + 1;
                continue;
            }

            throw CreateInvalidPathException(executorName, jsonPathSettingName, jsonPath);
        }

        return segments;
    }

    private static InvalidOperationException CreateInvalidPathException(
        string executorName,
        string jsonPathSettingName,
        string jsonPath)
        => new($"{executorName} executor setting '{jsonPathSettingName}' has invalid JSON path '{jsonPath}'.");

    private static InvalidOperationException CreateMissingPathException(
        string executorName,
        string jsonPathSettingName,
        string jsonPath)
        => new($"{executorName} executor setting '{jsonPathSettingName}' path '{jsonPath}' was not found in the workflow payload.");

    private readonly record struct JsonPathSegment(string? PropertyName, int? Index);
}
