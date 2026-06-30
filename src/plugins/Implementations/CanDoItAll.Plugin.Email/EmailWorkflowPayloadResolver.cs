using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.Plugins;

public static class EmailWorkflowPayloadResolver
{
    public static string ResolveInputJsonString(
        WorkflowNodeInput input,
        string jsonPath,
        string settingName,
        string executorName)
    {
        if (string.IsNullOrWhiteSpace(jsonPath))
        {
            throw new InvalidOperationException($"{executorName} setting '{settingName}' is required.");
        }

        if (!WorkflowRoutingValidation.TryParseJsonPath(jsonPath.Trim(), out var path, out var pathError))
        {
            throw new InvalidOperationException($"{executorName} setting '{settingName}' has invalid JSON path: {pathError}.");
        }

        if (string.IsNullOrWhiteSpace(input.PayloadJson))
        {
            throw new InvalidOperationException($"{executorName} setting '{settingName}' requires a workflow JSON payload.");
        }

        using var document = JsonDocument.Parse(input.PayloadJson);
        if (!TryResolve(document.RootElement, path, out var value))
        {
            throw new InvalidOperationException($"{executorName} setting '{settingName}' path '{jsonPath}' was not found in the workflow payload.");
        }

        return value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : value.GetRawText();
    }

    public static string ResolveOptionalInputJsonString(
        WorkflowNodeInput input,
        string configuredValue,
        string jsonPath,
        string settingName,
        string executorName)
    {
        if (!string.IsNullOrWhiteSpace(configuredValue))
        {
            return configuredValue.Trim();
        }

        return TryResolveInputJsonElement(input, jsonPath, settingName, executorName, out var value)
            ? value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? string.Empty
                : value.GetRawText()
            : string.Empty;
    }

    public static int ResolveOptionalInputJsonInt32(
        WorkflowNodeInput input,
        int configuredValue,
        string jsonPath,
        string settingName,
        string executorName)
    {
        if (!TryResolveInputJsonElement(input, jsonPath, settingName, executorName, out var value))
        {
            return configuredValue;
        }

        if (value.ValueKind == JsonValueKind.Number &&
            value.TryGetInt32(out var number))
        {
            return number;
        }

        if (value.ValueKind == JsonValueKind.String &&
            int.TryParse(value.GetString(), out number))
        {
            return number;
        }

        throw new InvalidOperationException($"{executorName} setting '{settingName}' path '{jsonPath}' must resolve to an integer.");
    }

    private static bool TryResolveInputJsonElement(
        WorkflowNodeInput input,
        string jsonPath,
        string settingName,
        string executorName,
        out JsonElement value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(jsonPath))
        {
            return false;
        }

        if (!WorkflowRoutingValidation.TryParseJsonPath(jsonPath.Trim(), out var path, out var pathError))
        {
            throw new InvalidOperationException($"{executorName} setting '{settingName}' has invalid JSON path: {pathError}.");
        }

        if (string.IsNullOrWhiteSpace(input.PayloadJson))
        {
            return false;
        }

        using var document = JsonDocument.Parse(input.PayloadJson);
        if (!TryResolve(document.RootElement, path, out var resolved))
        {
            return false;
        }

        value = resolved.Clone();
        return true;
    }

    private static bool TryResolve(
        JsonElement current,
        IReadOnlyList<BuiltInJsonPathSegment> path,
        out JsonElement value)
    {
        value = current;
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

            if (segment.Index is not { } index ||
                value.ValueKind != JsonValueKind.Array ||
                index < 0 ||
                index >= value.GetArrayLength())
            {
                return false;
            }

            value = value[index];
        }

        return true;
    }
}
