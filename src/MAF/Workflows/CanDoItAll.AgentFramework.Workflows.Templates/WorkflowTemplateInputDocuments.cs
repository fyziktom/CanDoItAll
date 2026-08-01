using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Builder;

namespace CanDoItAll.AgentFramework.Workflows.Templates;

public sealed class WorkflowTemplateInputParameter
{
    public string Key { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string Kind { get; set; } = string.Empty;

    public bool Required { get; set; }

    public string Description { get; set; } = string.Empty;

    public string JsonPath { get; set; } = string.Empty;

    public string DefaultValue { get; set; } = string.Empty;

    public WorkflowTemplateInputParameterOptionSource? OptionSource { get; set; }

    public int? MinimumValue { get; set; }

    public int? MaximumValue { get; set; }

    public string Placeholder { get; set; } = string.Empty;
}

public sealed class WorkflowTemplateInputParameterOptionSource
{
    public string Kind { get; set; } = string.Empty;

    public string DependsOnParameterKey { get; set; } = string.Empty;

    public List<WorkflowTemplateInputParameterOption> StaticOptions { get; set; } = [];
}

public sealed class WorkflowTemplateInputParameterOption
{
    public string Value { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;
}

internal static class WorkflowTemplateInputParameterMaterializer
{
    public static IReadOnlyList<WorkflowInputParameterDescriptor> CreateInputParameters(
        WorkflowTemplateDefinition template)
    {
        ArgumentNullException.ThrowIfNull(template);

        var context = WorkflowTemplatePack.CreateContext(template);
        var descriptors = template.InputParameters
            .Select((parameter, index) => CreateInputParameter(parameter, context.WithYamlPath($"workflows[].inputParameters[{index}]")))
            .ToArray();
        var duplicateKeys = descriptors
            .GroupBy(parameter => parameter.Key, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (duplicateKeys.Length > 0)
        {
            throw WorkflowTemplateDiagnostics.CreateException(
                WorkflowTemplateFailureKind.InputParameterInvalid,
                $"Workflow template contains duplicate input parameter key(s): {string.Join(", ", duplicateKeys)}.",
                context.WithYamlPath("workflows[].inputParameters"),
                "Use one unique input parameter key per workflow template.");
        }

        return descriptors;
    }

    private static WorkflowInputParameterDescriptor CreateInputParameter(
        WorkflowTemplateInputParameter parameter,
        WorkflowTemplateContext context)
    {
        var key = WorkflowTemplateDiagnostics.Require(
            parameter.Key,
            "input parameter key",
            context,
            "Set inputParameters[].key to a stable non-empty identifier.");
        var jsonPath = string.IsNullOrWhiteSpace(parameter.JsonPath) ? $"$.{key}" : parameter.JsonPath.Trim();
        if (parameter.MinimumValue.HasValue &&
            parameter.MaximumValue.HasValue &&
            parameter.MinimumValue.Value > parameter.MaximumValue.Value)
        {
            throw WorkflowTemplateDiagnostics.CreateException(
                WorkflowTemplateFailureKind.InputParameterInvalid,
                $"Workflow template input parameter '{key}' has minimumValue greater than maximumValue.",
                context.WithYamlPath($"{context.YamlPath}.minimumValue"),
                "Set minimumValue less than or equal to maximumValue.");
        }

        return WorkflowInputParameterBuilder
            .Create(key)
            .WithLabel(string.IsNullOrWhiteSpace(parameter.Label) ? key : parameter.Label.Trim())
            .WithKind(ParseEnum<WorkflowInputParameterKind>(
                parameter.Kind,
                $"input parameter '{key}' kind",
                context.WithYamlPath($"{context.YamlPath}.kind")))
            .WithDescription(parameter.Description.Trim())
            .WithJsonPath(jsonPath)
            .WithDefaultValue(parameter.DefaultValue.Trim())
            .WithOptionSource(CreateOptionSource(parameter.OptionSource, context, key))
            .WithRange(parameter.MinimumValue, parameter.MaximumValue)
            .WithPlaceholder(parameter.Placeholder.Trim())
            .Build()
            with
            {
                IsRequired = parameter.Required
            };
    }

    private static WorkflowInputParameterOptionSource CreateOptionSource(
        WorkflowTemplateInputParameterOptionSource? optionSource,
        WorkflowTemplateContext context,
        string parameterKey)
    {
        if (optionSource is null)
        {
            return WorkflowInputParameterOptionSource.None;
        }

        var kind = string.IsNullOrWhiteSpace(optionSource.Kind)
            ? WorkflowInputParameterOptionSourceKind.None
            : ParseEnum<WorkflowInputParameterOptionSourceKind>(
                optionSource.Kind,
                $"input parameter '{parameterKey}' optionSource.kind",
                context.WithYamlPath($"{context.YamlPath}.optionSource.kind"));

        return new WorkflowInputParameterOptionSource(
            kind,
            optionSource.DependsOnParameterKey.Trim(),
            optionSource.StaticOptions
                .Select((option, index) => CreateOption(option, context.WithYamlPath($"{context.YamlPath}.optionSource.staticOptions[{index}]"), parameterKey))
                .ToArray());
    }

    private static WorkflowInputParameterOption CreateOption(
        WorkflowTemplateInputParameterOption option,
        WorkflowTemplateContext context,
        string parameterKey)
    {
        var value = WorkflowTemplateDiagnostics.Require(
            option.Value,
            $"input parameter '{parameterKey}' option value",
            context,
            "Set staticOptions[].value to the persisted option value.");
        return new WorkflowInputParameterOption(
            value,
            string.IsNullOrWhiteSpace(option.Label) ? value : option.Label.Trim(),
            option.Description.Trim());
    }

    private static TEnum ParseEnum<TEnum>(
        string value,
        string fieldName,
        WorkflowTemplateContext context)
        where TEnum : struct, Enum
        => Enum.TryParse<TEnum>(value, ignoreCase: true, out var result) && Enum.IsDefined(result)
            ? result
            : throw WorkflowTemplateDiagnostics.CreateException(
                WorkflowTemplateFailureKind.InputParameterInvalid,
                $"Workflow template has invalid {fieldName} '{value}'.",
                context,
                $"Use a valid {typeof(TEnum).Name} value.");
}
