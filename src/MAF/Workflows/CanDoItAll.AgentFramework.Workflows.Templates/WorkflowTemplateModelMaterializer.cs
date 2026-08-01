using System.Collections;
using System.Globalization;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Workflows.Templates;

internal static class WorkflowTemplateModelMaterializer
{
    public static WorkflowValueShape CreateJsonShape(WorkflowTemplatePackManifest manifest)
        => new(
            ParseEnum<WorkflowValueShapeKind>(
                manifest.JsonShape.Kind,
                "value shape kind",
                new WorkflowTemplateContext(
                    WorkflowTemplatePackOptions.ManifestFileName,
                    string.Empty,
                    "jsonShape.kind"),
                WorkflowTemplateFailureKind.GraphMaterializationFailed),
            manifest.JsonShape.SchemaJson,
            WorkflowTemplateDiagnostics.Require(
                manifest.JsonShape.Description,
                "value shape description",
                new WorkflowTemplateContext(
                    WorkflowTemplatePackOptions.ManifestFileName,
                    string.Empty,
                    "jsonShape.description"),
                "Set jsonShape.description so generated workflow ports and components have a meaningful shape description."));

    public static WorkflowRuntimePolicy CreateRuntimePolicy(WorkflowTemplatePackManifest manifest)
        => new(
            ParseEnum<WorkflowRuntimeBackendKind>(
                manifest.RuntimePolicy.PreferredBackend,
                "runtime preferredBackend",
                new WorkflowTemplateContext(
                    WorkflowTemplatePackOptions.ManifestFileName,
                    string.Empty,
                    "runtimePolicy.preferredBackend"),
                WorkflowTemplateFailureKind.GraphMaterializationFailed),
            manifest.RuntimePolicy.AllowInProcessPreviewRuns,
            manifest.RuntimePolicy.RequireDurableProductionRuns,
            manifest.RuntimePolicy.ExposeAzureFunctionsStatusEndpoint,
            manifest.RuntimePolicy.ExposeAzureFunctionsMcpTool);

    public static WorkflowModelSettings CreateModelSettings(WorkflowTemplatePackManifest manifest)
        => new(
            manifest.Component.ModelSettings.Temperature,
            manifest.Component.ModelSettings.MaxOutputTokens,
            manifest.Component.ModelSettings.RequireJsonOutput,
            manifest.Component.ModelSettings.ResponseFormatJsonSchema);

    public static WorkflowExecutorExecutionPolicy CreateExecutionPolicy(
        WorkflowTemplateExecutionPolicy templatePolicy,
        WorkflowTemplateContext context)
    {
        var policy = new WorkflowExecutorExecutionPolicy(
            templatePolicy.TimeoutSeconds,
            templatePolicy.MaxRetryAttempts,
            templatePolicy.RetryDelayMilliseconds,
            templatePolicy.CaptureOutputArtifact);
        if (!WorkflowExecutorPolicyLimits.IsValid(policy))
        {
            throw WorkflowTemplateDiagnostics.CreateException(
                WorkflowTemplateFailureKind.GraphMaterializationFailed,
                "Workflow template defines an invalid executor policy.",
                context,
                "Set timeoutSeconds, maxRetryAttempts, and retryDelayMilliseconds within workflow executor policy limits.");
        }

        return policy;
    }

    public static string CreateComponentInstructions(
        WorkflowTemplatePackManifest manifest,
        WorkflowTemplateDefinition template)
    {
        var context = WorkflowTemplatePack.CreateContext(template).WithYamlPath("component.instructionsTemplate");
        var templateText = WorkflowTemplateDiagnostics.Require(
            manifest.Component.InstructionsTemplate,
            "component.instructionsTemplate",
            context,
            "Set component.instructionsTemplate and include the {name} and {routingInstructions} placeholders as needed.");
        return templateText
            .Replace("{name}", template.Name, StringComparison.Ordinal)
            .Replace("{routingInstructions}", template.RoutingInstructions, StringComparison.Ordinal);
    }

    public static string SerializeSettings(IDictionary<string, object?> settings)
        => settings.Count == 0
            ? string.Empty
            : JsonSerializer.Serialize(
                settings.ToDictionary(
                    item => item.Key,
                    item => NormalizeSettingValue(item.Value),
                    StringComparer.OrdinalIgnoreCase),
                WorkflowTemplateJson.Options);

    public static TEnum ParseEnum<TEnum>(
        string value,
        string fieldName,
        WorkflowTemplateContext context,
        WorkflowTemplateFailureKind failureKind)
        where TEnum : struct, Enum
        => Enum.TryParse<TEnum>(value, ignoreCase: true, out var result) && Enum.IsDefined(result)
            ? result
            : throw WorkflowTemplateDiagnostics.CreateException(
                failureKind,
                $"Workflow template has invalid {fieldName} '{value}'.",
                context,
                $"Use a valid {typeof(TEnum).Name} value.");

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

        if (value is IDictionary dictionary)
        {
            var normalized = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (DictionaryEntry entry in dictionary)
            {
                normalized[Convert.ToString(entry.Key, CultureInfo.InvariantCulture) ?? string.Empty] =
                    NormalizeSettingValue(entry.Value);
            }

            return normalized;
        }

        if (value is IEnumerable enumerable && value is not string)
        {
            return enumerable.Cast<object?>().Select(NormalizeSettingValue).ToArray();
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
}
