using System.Globalization;
using System.Net.Mail;
using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.Modules.SchedulerPlanner;

public interface ISchedulerWorkflowInputSchemaService
{
    Task<SchedulerWorkflowInputSchema> ResolveSchemaAsync(
        WorkflowId workflowId,
        WorkflowVersionId? versionId = null,
        CancellationToken cancellationToken = default);

    Task<SchedulerWorkflowInputValidationResult> ValidateInputAsync(
        WorkflowId workflowId,
        WorkflowVersionId? versionId,
        string? inputJson,
        CancellationToken cancellationToken = default);
}

public sealed class SchedulerWorkflowInputSchemaService(
    IWorkflowCatalogService workflowCatalogService) : ISchedulerWorkflowInputSchemaService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<SchedulerWorkflowInputSchema> ResolveSchemaAsync(
        WorkflowId workflowId,
        WorkflowVersionId? versionId = null,
        CancellationToken cancellationToken = default)
    {
        var definition = await ResolveDefinitionAsync(workflowId, versionId, cancellationToken);
        return new SchedulerWorkflowInputSchema(
            definition.Id,
            definition.VersionId,
            definition.Name,
            SnapshotInputParameters(definition.InputParameters),
            UsesRawJsonFallback: definition.InputParameters.Count == 0);
    }

    public async Task<SchedulerWorkflowInputValidationResult> ValidateInputAsync(
        WorkflowId workflowId,
        WorkflowVersionId? versionId,
        string? inputJson,
        CancellationToken cancellationToken = default)
    {
        var definition = await ResolveDefinitionAsync(workflowId, versionId, cancellationToken);
        var normalizedInputJson = NormalizeRawInputJson(inputJson);
        JsonNode? parsed;
        try
        {
            parsed = JsonNode.Parse(normalizedInputJson);
        }
        catch (JsonException exception)
        {
            return InvalidJsonResult(normalizedInputJson, exception);
        }

        if (definition.InputParameters.Count == 0)
        {
            return new SchedulerWorkflowInputValidationResult(
                Succeeded: true,
                normalizedInputJson,
                []);
        }

        if (parsed is not JsonObject root)
        {
            return new SchedulerWorkflowInputValidationResult(
                Succeeded: false,
                normalizedInputJson,
                [new SchedulerWorkflowInputValidationIssue(string.Empty, "Workflow input must be a JSON object when typed parameters are defined.")]);
        }

        var issues = new List<SchedulerWorkflowInputValidationIssue>();
        foreach (var parameter in definition.InputParameters)
        {
            ValidateParameter(root, parameter, issues);
        }

        return new SchedulerWorkflowInputValidationResult(
            issues.Count == 0,
            issues.Count == 0 ? root.ToJsonString(JsonOptions) : normalizedInputJson,
            issues);
    }

    private async Task<WorkflowDefinition> ResolveDefinitionAsync(
        WorkflowId workflowId,
        WorkflowVersionId? versionId,
        CancellationToken cancellationToken)
    {
        var detail = await workflowCatalogService.GetDefinitionAsync(workflowId, versionId, cancellationToken);
        return detail?.Definition
               ?? throw new KeyNotFoundException($"Workflow definition '{workflowId}' was not found.");
    }

    private static void ValidateParameter(
        JsonObject root,
        WorkflowInputParameterDescriptor parameter,
        List<SchedulerWorkflowInputValidationIssue> issues)
    {
        if (!TryResolveRootPropertyName(parameter, out var propertyName, out var pathError))
        {
            issues.Add(new SchedulerWorkflowInputValidationIssue(parameter.Key, pathError));
            return;
        }

        root.TryGetPropertyValue(propertyName, out var value);
        if (IsMissingValue(value) && !string.IsNullOrWhiteSpace(parameter.DefaultValue))
        {
            value = CreateDefaultValue(parameter, issues);
            if (value is not null)
            {
                root[propertyName] = value;
            }
        }

        if (IsMissingValue(value))
        {
            if (parameter.IsRequired)
            {
                issues.Add(new SchedulerWorkflowInputValidationIssue(parameter.Key, $"{parameter.Label} is required."));
            }

            return;
        }

        switch (parameter.Kind)
        {
            case WorkflowInputParameterKind.EmailAddress:
            case WorkflowInputParameterKind.CrmContactEmail:
                ValidateEmail(parameter, value!, issues);
                break;
            case WorkflowInputParameterKind.ProjectId:
                ValidateProjectId(parameter, value!, issues);
                break;
            case WorkflowInputParameterKind.ProjectNodeId:
            case WorkflowInputParameterKind.Category:
            case WorkflowInputParameterKind.Text:
            case WorkflowInputParameterKind.ExternalConnectionId:
                ValidateString(parameter, value!, issues);
                break;
            case WorkflowInputParameterKind.Integer:
            case WorkflowInputParameterKind.DurationMinutes:
                ValidateInteger(root, propertyName, parameter, value!, issues);
                break;
            case WorkflowInputParameterKind.TimeZone:
                ValidateTimeZone(parameter, value!, issues);
                break;
            default:
                issues.Add(new SchedulerWorkflowInputValidationIssue(
                    parameter.Key,
                    $"Workflow input parameter kind '{parameter.Kind}' is not supported by Scheduler validation."));
                break;
        }
    }

    private static JsonNode? CreateDefaultValue(
        WorkflowInputParameterDescriptor parameter,
        List<SchedulerWorkflowInputValidationIssue> issues)
    {
        if (parameter.Kind is WorkflowInputParameterKind.Integer or WorkflowInputParameterKind.DurationMinutes)
        {
            if (int.TryParse(parameter.DefaultValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
            {
                return JsonValue.Create(integer);
            }

            issues.Add(new SchedulerWorkflowInputValidationIssue(
                parameter.Key,
                $"{parameter.Label} default value must be an integer."));
            return null;
        }

        return JsonValue.Create(parameter.DefaultValue);
    }

    private static void ValidateEmail(
        WorkflowInputParameterDescriptor parameter,
        JsonNode value,
        List<SchedulerWorkflowInputValidationIssue> issues)
    {
        if (!TryGetString(value, out var text))
        {
            issues.Add(new SchedulerWorkflowInputValidationIssue(parameter.Key, $"{parameter.Label} must be a text email address."));
            return;
        }

        try
        {
            var address = new MailAddress(text);
            if (!string.Equals(address.Address, text.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new SchedulerWorkflowInputValidationIssue(parameter.Key, $"{parameter.Label} must be a plain email address."));
            }
        }
        catch (FormatException)
        {
            issues.Add(new SchedulerWorkflowInputValidationIssue(parameter.Key, $"{parameter.Label} must be a valid email address."));
        }
    }

    private static void ValidateProjectId(
        WorkflowInputParameterDescriptor parameter,
        JsonNode value,
        List<SchedulerWorkflowInputValidationIssue> issues)
    {
        if (!TryGetString(value, out var text) || !Guid.TryParse(text, out var projectId) || projectId == Guid.Empty)
        {
            issues.Add(new SchedulerWorkflowInputValidationIssue(parameter.Key, $"{parameter.Label} must be a non-empty project id."));
        }
    }

    private static void ValidateString(
        WorkflowInputParameterDescriptor parameter,
        JsonNode value,
        List<SchedulerWorkflowInputValidationIssue> issues)
    {
        if (!TryGetString(value, out _))
        {
            issues.Add(new SchedulerWorkflowInputValidationIssue(parameter.Key, $"{parameter.Label} must be text."));
        }
    }

    private static void ValidateInteger(
        JsonObject root,
        string propertyName,
        WorkflowInputParameterDescriptor parameter,
        JsonNode value,
        List<SchedulerWorkflowInputValidationIssue> issues)
    {
        if (!TryGetInteger(value, out var integer))
        {
            issues.Add(new SchedulerWorkflowInputValidationIssue(parameter.Key, $"{parameter.Label} must be an integer."));
            return;
        }

        if (parameter.MinimumValue.HasValue && integer < parameter.MinimumValue.Value)
        {
            issues.Add(new SchedulerWorkflowInputValidationIssue(parameter.Key, $"{parameter.Label} must be at least {parameter.MinimumValue.Value}."));
        }

        if (parameter.MaximumValue.HasValue && integer > parameter.MaximumValue.Value)
        {
            issues.Add(new SchedulerWorkflowInputValidationIssue(parameter.Key, $"{parameter.Label} must be at most {parameter.MaximumValue.Value}."));
        }

        root[propertyName] = integer;
    }

    private static void ValidateTimeZone(
        WorkflowInputParameterDescriptor parameter,
        JsonNode value,
        List<SchedulerWorkflowInputValidationIssue> issues)
    {
        if (!TryGetString(value, out var text))
        {
            issues.Add(new SchedulerWorkflowInputValidationIssue(parameter.Key, $"{parameter.Label} must be a time zone id."));
            return;
        }

        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(text);
        }
        catch (TimeZoneNotFoundException)
        {
            issues.Add(new SchedulerWorkflowInputValidationIssue(parameter.Key, $"{parameter.Label} is not a known time zone id."));
        }
        catch (InvalidTimeZoneException)
        {
            issues.Add(new SchedulerWorkflowInputValidationIssue(parameter.Key, $"{parameter.Label} is not a valid time zone id."));
        }
    }

    private static bool TryResolveRootPropertyName(
        WorkflowInputParameterDescriptor parameter,
        out string propertyName,
        out string error)
    {
        var jsonPath = string.IsNullOrWhiteSpace(parameter.JsonPath)
            ? $"$.{parameter.Key}"
            : parameter.JsonPath.Trim();
        if (!jsonPath.StartsWith("$.", StringComparison.Ordinal) ||
            jsonPath.Length <= 2 ||
            jsonPath[2..].Contains('.', StringComparison.Ordinal) ||
            jsonPath.Contains('[', StringComparison.Ordinal))
        {
            propertyName = string.Empty;
            error = $"{parameter.Label} uses unsupported JSON path '{jsonPath}'. Scheduler typed input parameters must target a root property.";
            return false;
        }

        propertyName = jsonPath[2..];
        error = string.Empty;
        return true;
    }

    private static bool TryGetString(JsonNode value, out string text)
    {
        if (value is JsonValue jsonValue &&
            jsonValue.TryGetValue<string>(out var raw) &&
            !string.IsNullOrWhiteSpace(raw))
        {
            text = raw.Trim();
            return true;
        }

        text = string.Empty;
        return false;
    }

    private static bool TryGetInteger(JsonNode value, out int integer)
    {
        if (value is JsonValue jsonValue)
        {
            if (jsonValue.TryGetValue<int>(out integer))
            {
                return true;
            }

            if (jsonValue.TryGetValue<string>(out var text) &&
                int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out integer))
            {
                return true;
            }
        }

        integer = 0;
        return false;
    }

    private static bool IsMissingValue(JsonNode? value)
        => value is null ||
           (value is JsonValue jsonValue &&
            jsonValue.TryGetValue<string>(out var text) &&
            string.IsNullOrWhiteSpace(text));

    private static string NormalizeRawInputJson(string? inputJson)
        => string.IsNullOrWhiteSpace(inputJson) ? "{}" : inputJson.Trim();

    private static SchedulerWorkflowInputValidationResult InvalidJsonResult(
        string inputJson,
        JsonException exception)
        => new(
            Succeeded: false,
            inputJson,
            [new SchedulerWorkflowInputValidationIssue(string.Empty, $"Workflow input JSON is invalid: {exception.Message}")]);

    private static IReadOnlyList<WorkflowInputParameterDescriptor> SnapshotInputParameters(
        IReadOnlyList<WorkflowInputParameterDescriptor> inputParameters)
        => inputParameters
            .Select(parameter => parameter with
            {
                OptionSource = parameter.OptionSource with
                {
                    StaticOptions = parameter.OptionSource.StaticOptions.ToArray()
                }
            })
            .ToArray();
}
