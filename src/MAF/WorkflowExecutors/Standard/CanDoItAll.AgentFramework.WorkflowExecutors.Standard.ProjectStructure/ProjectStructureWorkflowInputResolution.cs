using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.AgentFramework.WorkflowExecutors.Standard.ProjectStructure;

public sealed partial class ProjectStructureWorkflowExecutor
{
    private static JsonElement ResolveInputJsonElement(
        WorkflowNodeInput input,
        string jsonPath,
        string settingName)
    {
        if (string.IsNullOrWhiteSpace(jsonPath))
        {
            throw new InvalidOperationException($"Project-structure executor setting '{settingName}' is required.");
        }

        if (!WorkflowRoutingValidation.TryParseJsonPath(jsonPath.Trim(), out var path, out var pathError))
        {
            throw new InvalidOperationException($"Project-structure executor setting '{settingName}' has invalid JSON path: {pathError}.");
        }

        if (string.IsNullOrWhiteSpace(input.PayloadJson))
        {
            throw new InvalidOperationException($"Project-structure executor setting '{settingName}' requires a workflow JSON payload.");
        }

        using var document = JsonDocument.Parse(input.PayloadJson);
        if (!TryResolve(document.RootElement, path, out var value))
        {
            throw new InvalidOperationException($"Project-structure executor setting '{settingName}' path '{jsonPath}' was not found in the workflow payload.");
        }

        return value.Clone();
    }

    private static string ReadRequiredTaskString(JsonElement element, string propertyName, int index)
    {
        var value = ReadOptionalString(element, propertyName);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Task item {index} requires non-empty '{propertyName}'.");
        }

        return value.Trim();
    }

    private static string ReadOptionalString(JsonElement element, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (!element.TryGetProperty(propertyName, out var property) ||
                property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                continue;
            }

            return property.ValueKind == JsonValueKind.String
                ? property.GetString() ?? string.Empty
                : property.GetRawText();
        }

        return string.Empty;
    }

    private static bool ReadOptionalBoolean(JsonElement element, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (!element.TryGetProperty(propertyName, out var property))
            {
                continue;
            }

            if (property.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                return property.GetBoolean();
            }

            if (property.ValueKind == JsonValueKind.String &&
                bool.TryParse(property.GetString(), out var parsed))
            {
                return parsed;
            }
        }

        return false;
    }

    private static DateTimeOffset? ReadOptionalDueUtc(JsonElement element, int index)
    {
        var value = ReadOptionalString(element, "dueUtc", "dueDateUtc", "dueDate");
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (DateTimeOffset.TryParse(value, out var parsed))
        {
            return parsed.ToUniversalTime();
        }

        throw new InvalidOperationException($"Task item {index} has invalid due date '{value}'.");
    }

    private static IReadOnlyList<string> ReadOptionalStringArray(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return property.EnumerateArray()
            .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() ?? string.Empty : item.GetRawText())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .ToArray();
    }

    private static Guid RequireProjectId(
        WorkflowProjectStructureExecutorSettings settings,
        WorkflowNodeInput input)
    {
        if (settings.ProjectId is { } projectId && projectId != Guid.Empty)
        {
            return projectId;
        }

        if (TryResolveInputJsonString(input, settings.ProjectIdJsonPath, out var rawProjectId) &&
            !string.IsNullOrWhiteSpace(rawProjectId))
        {
            if (Guid.TryParse(rawProjectId, out var parsedProjectId) && parsedProjectId != Guid.Empty)
            {
                return parsedProjectId;
            }

            throw new InvalidOperationException(
                $"Project-structure executor setting '{nameof(settings.ProjectIdJsonPath)}' resolved invalid project id '{rawProjectId}'.");
        }

        if (TryResolveInputJsonString(input, "$.project.id", out rawProjectId) &&
            Guid.TryParse(rawProjectId, out var parsed) &&
            parsed != Guid.Empty)
        {
            return parsed;
        }

        throw new InvalidOperationException("Project-structure executor setting 'ProjectId' or 'ProjectIdJsonPath' is required unless the workflow input includes '$.project.id'.");
    }

    private static string RequireNodeId(
        WorkflowProjectStructureExecutorSettings settings,
        WorkflowNodeInput input)
        => Require(ResolveOptionalNodeId(settings, input) ?? string.Empty, nameof(settings.NodeId));

    private static string? ResolveOptionalNodeId(
        WorkflowProjectStructureExecutorSettings settings,
        WorkflowNodeInput input)
    {
        if (!string.IsNullOrWhiteSpace(settings.NodeId))
        {
            return settings.NodeId.Trim();
        }

        if (!TryResolveInputJsonString(input, settings.NodeIdJsonPath, out var nodeId))
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(nodeId))
        {
            return nodeId.Trim();
        }

        throw new InvalidOperationException(
            $"Project-structure executor setting '{nameof(settings.NodeIdJsonPath)}' resolved an empty node id.");
    }

    private static string? ResolveWorkflowParentNodeId(WorkflowNodeInput input)
        => TryResolveInputJsonString(input, "$.runContext.workflowNodeId", out var workflowNodeId) &&
           !string.IsNullOrWhiteSpace(workflowNodeId)
            ? workflowNodeId.Trim()
            : null;

    private static string? ResolveInputJsonString(
        WorkflowNodeInput input,
        string jsonPath,
        string settingName)
    {
        if (string.IsNullOrWhiteSpace(jsonPath))
        {
            return null;
        }

        if (!WorkflowRoutingValidation.TryParseJsonPath(jsonPath.Trim(), out var path, out var pathError))
        {
            throw new InvalidOperationException($"Project-structure executor setting '{settingName}' has invalid JSON path: {pathError}.");
        }

        if (string.IsNullOrWhiteSpace(input.PayloadJson))
        {
            throw new InvalidOperationException($"Project-structure executor setting '{settingName}' requires a workflow JSON payload.");
        }

        using var document = JsonDocument.Parse(input.PayloadJson);
        if (!TryResolve(document.RootElement, path, out var value))
        {
            throw new InvalidOperationException($"Project-structure executor setting '{settingName}' path '{jsonPath}' was not found in the workflow payload.");
        }

        return value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : value.GetRawText();
    }

    private static bool TryResolveInputJsonString(
        WorkflowNodeInput input,
        string jsonPath,
        out string? resolvedValue)
    {
        resolvedValue = null;
        if (string.IsNullOrWhiteSpace(jsonPath))
        {
            return false;
        }

        if (!WorkflowRoutingValidation.TryParseJsonPath(jsonPath.Trim(), out var path, out var pathError))
        {
            throw new InvalidOperationException($"Project-structure executor has invalid JSON path '{jsonPath}': {pathError}.");
        }

        if (string.IsNullOrWhiteSpace(input.PayloadJson))
        {
            return false;
        }

        using var document = JsonDocument.Parse(input.PayloadJson);
        if (!TryResolve(document.RootElement, path, out var value))
        {
            return false;
        }

        resolvedValue = value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : value.GetRawText();
        return true;
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
