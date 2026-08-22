using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Network;

internal static class WorkflowHttpRequestUriResolver
{
    public static Uri Resolve(
        WorkflowHttpExecutorSettings settings,
        WorkflowNodeInput input)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(input);

        var resolvedUrl = ResolveUrl(settings, input);
        if (!Uri.TryCreate(resolvedUrl, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https"))
        {
            throw new InvalidOperationException("HTTP executor requires an absolute http or https URL.");
        }

        var queryParameters = ResolveQueryParameters(settings, input);
        return queryParameters.Count == 0
            ? uri
            : AppendQueryParameters(uri, queryParameters);
    }

    private static string ResolveUrl(
        WorkflowHttpExecutorSettings settings,
        WorkflowNodeInput input)
    {
        if (!string.IsNullOrWhiteSpace(settings.Url))
        {
            return settings.Url.Trim();
        }

        var url = ResolveInputJsonString(input, settings.UrlJsonPath, nameof(settings.UrlJsonPath));
        return string.IsNullOrWhiteSpace(url)
            ? throw new InvalidOperationException("HTTP executor setting 'Url' or 'UrlJsonPath' is required.")
            : url.Trim();
    }

    private static IReadOnlyDictionary<string, string> ResolveQueryParameters(
        WorkflowHttpExecutorSettings settings,
        WorkflowNodeInput input)
    {
        var merged = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var staticParameters = settings.QueryParameters ?? throw new InvalidOperationException(
            "HTTP executor setting 'QueryParameters' cannot be null.");
        foreach (var parameter in staticParameters)
        {
            AddQueryParameter(merged, parameter.Key, parameter.Value, nameof(settings.QueryParameters));
        }

        if (string.IsNullOrWhiteSpace(settings.QueryParametersJsonPath))
        {
            return merged;
        }

        using var document = ParseInputPayload(input, nameof(settings.QueryParametersJsonPath));
        var value = ResolveInputJsonValue(
            document.RootElement,
            settings.QueryParametersJsonPath,
            nameof(settings.QueryParametersJsonPath));
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException(
                $"HTTP executor setting '{nameof(settings.QueryParametersJsonPath)}' must resolve to a JSON object.");
        }

        foreach (var property in value.EnumerateObject())
        {
            AddQueryParameter(
                merged,
                property.Name,
                ReadScalarQueryValue(property, settings.QueryParametersJsonPath),
                nameof(settings.QueryParametersJsonPath));
        }

        return merged;
    }

    private static void AddQueryParameter(
        IDictionary<string, string> parameters,
        string key,
        string? value,
        string settingName)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException(
                $"HTTP executor setting '{settingName}' contains an empty query parameter name.");
        }

        if (value is null)
        {
            throw new InvalidOperationException(
                $"HTTP executor setting '{settingName}' contains a null value for query parameter '{key}'.");
        }

        if (!parameters.TryAdd(key, value))
        {
            throw new InvalidOperationException(
                $"HTTP executor query parameter '{key}' is configured more than once.");
        }
    }

    private static string ReadScalarQueryValue(
        JsonProperty property,
        string jsonPath)
        => property.Value.ValueKind switch
        {
            JsonValueKind.String => property.Value.GetString() ?? string.Empty,
            JsonValueKind.Number => property.Value.GetRawText(),
            JsonValueKind.True => bool.TrueString.ToLowerInvariant(),
            JsonValueKind.False => bool.FalseString.ToLowerInvariant(),
            _ => throw new InvalidOperationException(
                $"HTTP executor setting '{nameof(WorkflowHttpExecutorSettings.QueryParametersJsonPath)}' path '{jsonPath}' contains non-scalar query parameter '{property.Name}'.")
        };

    private static Uri AppendQueryParameters(
        Uri uri,
        IReadOnlyDictionary<string, string> queryParameters)
    {
        var encodedParameters = queryParameters
            .Select(parameter => $"{Uri.EscapeDataString(parameter.Key)}={Uri.EscapeDataString(parameter.Value)}");
        var appendedQuery = string.Join("&", encodedParameters);
        var existingQuery = uri.Query.TrimStart('?');
        var builder = new UriBuilder(uri)
        {
            Query = string.IsNullOrEmpty(existingQuery)
                ? appendedQuery
                : $"{existingQuery}&{appendedQuery}"
        };
        return builder.Uri;
    }

    private static string? ResolveInputJsonString(
        WorkflowNodeInput input,
        string jsonPath,
        string settingName)
    {
        if (string.IsNullOrWhiteSpace(jsonPath))
        {
            return null;
        }

        using var document = ParseInputPayload(input, settingName);
        var value = ResolveInputJsonValue(document.RootElement, jsonPath, settingName);
        return value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : value.GetRawText();
    }

    private static JsonDocument ParseInputPayload(
        WorkflowNodeInput input,
        string settingName)
    {
        if (string.IsNullOrWhiteSpace(input.PayloadJson))
        {
            throw new InvalidOperationException(
                $"HTTP executor setting '{settingName}' requires a workflow JSON payload.");
        }

        try
        {
            return JsonDocument.Parse(input.PayloadJson);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                $"HTTP executor setting '{settingName}' requires a valid workflow JSON payload.",
                exception);
        }
    }

    private static JsonElement ResolveInputJsonValue(
        JsonElement root,
        string jsonPath,
        string settingName)
    {
        if (!WorkflowRoutingValidation.TryParseJsonPath(jsonPath.Trim(), out var path, out var pathError))
        {
            throw new InvalidOperationException(
                $"HTTP executor setting '{settingName}' has invalid JSON path: {pathError}.");
        }

        if (!TryResolve(root, path, out var value))
        {
            throw new InvalidOperationException(
                $"HTTP executor setting '{settingName}' path '{jsonPath}' was not found in the workflow payload.");
        }

        return value;
    }

    private static bool TryResolve(
        JsonElement root,
        IReadOnlyList<BuiltInJsonPathSegment> path,
        out JsonElement value)
    {
        value = root;
        foreach (var segment in path)
        {
            if (segment.PropertyName is not null)
            {
                if (value.ValueKind != JsonValueKind.Object ||
                    !value.TryGetProperty(segment.PropertyName, out value))
                {
                    return false;
                }

                continue;
            }

            if (segment.Index is not { } targetIndex || value.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            var currentIndex = 0;
            var matched = false;
            foreach (var item in value.EnumerateArray())
            {
                if (currentIndex == targetIndex)
                {
                    value = item;
                    matched = true;
                    break;
                }

                currentIndex++;
            }

            if (!matched)
            {
                return false;
            }
        }

        return true;
    }
}
