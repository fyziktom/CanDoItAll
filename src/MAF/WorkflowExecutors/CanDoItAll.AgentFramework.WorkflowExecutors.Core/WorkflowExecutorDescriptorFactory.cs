using System.Globalization;
using System.Reflection;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedKernel.Configuration;

namespace CanDoItAll.AgentFramework.Core;

public static class WorkflowExecutorDescriptorFactory
{
    public const string SettingsSchemaVersion = "1.0";
    public const string DefaultObjectJsonSchema = "{\"type\":\"object\"}";

    public static WorkflowValueShape JsonShape { get; } = new(
        WorkflowValueShapeKind.Json,
        "{}",
        "JSON payload");

    public static WorkflowExecutorDescriptor CreateImplemented<TSettings>(
        WorkflowExecutorId id,
        string name,
        string description,
        WorkflowExecutorCategoryKind category,
        string iconName,
        string setupRendererKey,
        TSettings defaultSettings,
        WorkflowExecutorSourceDescriptor source,
        WorkflowValueShape? inputShape = null,
        WorkflowValueShape? resultShape = null,
        string schemaJson = DefaultObjectJsonSchema,
        WorkflowExecutorExecutionPolicy? defaultPolicy = null,
        WorkflowExecutorPermissionPolicy? permissionPolicy = null,
        WorkflowExecutorDeterministicTestModeDescriptor? deterministicTestMode = null,
        JsonSerializerOptions? settingsJsonOptions = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaJson);

        var configurationSchema = CreateSettingsConfigurationSchema<TSettings>();
        return new WorkflowExecutorDescriptor(
            id,
            name,
            description,
            category,
            iconName,
            setupRendererKey,
            inputShape ?? WorkflowValueShape.Text,
            resultShape ?? JsonShape,
            schemaJson,
            SerializeDefaultSettings(defaultSettings, settingsJsonOptions),
            defaultPolicy ?? WorkflowExecutorExecutionPolicy.Default,
            IsImplemented: true)
        {
            Source = source,
            Availability = WorkflowExecutorAvailabilityDescriptor.Available(),
            SettingsSchema = WorkflowExecutorSettingsSchemaDescriptor.JsonSchema(SettingsSchemaVersion, schemaJson),
            ConfigurationSchema = configurationSchema,
            PermissionPolicy = permissionPolicy ?? WorkflowExecutorPermissionPolicy.None,
            DeterministicTestMode = deterministicTestMode ?? WorkflowExecutorDeterministicTestModeDescriptor.None
        };
    }

    public static WorkflowExecutorDescriptor CreatePlanned(
        WorkflowExecutorId id,
        string name,
        string description,
        WorkflowExecutorCategoryKind category,
        string iconName,
        string setupRendererKey,
        WorkflowExecutorSourceDescriptor source,
        WorkflowValueShape? inputShape = null,
        WorkflowValueShape? resultShape = null,
        string schemaJson = DefaultObjectJsonSchema)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaJson);

        return new WorkflowExecutorDescriptor(
            id,
            name,
            description,
            category,
            iconName,
            setupRendererKey,
            inputShape ?? WorkflowValueShape.Text,
            resultShape ?? JsonShape,
            schemaJson,
            "{}",
            WorkflowExecutorExecutionPolicy.Default,
            IsImplemented: false)
        {
            Source = source,
            Availability = WorkflowExecutorAvailabilityDescriptor.Planned("Executor is listed for roadmap visibility but is not implemented in this host."),
            SettingsSchema = WorkflowExecutorSettingsSchemaDescriptor.JsonSchema(SettingsSchemaVersion, schemaJson),
            ConfigurationSchema = ConfigurationSchema.Empty(SettingsSchemaVersion)
        };
    }

    public static ConfigurationSchema CreateSettingsConfigurationSchema<TSettings>()
    {
        var fields = typeof(TSettings)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => property.GetMethod is not null)
            .Select(property => new ConfigurationFieldDescriptor(
                JsonNamingPolicy.CamelCase.ConvertName(property.Name),
                property.Name,
                ResolveFieldType(property.PropertyType),
                IsRequired: false,
                HelpText: string.Empty)
            {
                Options = ResolveOptions(property.PropertyType)
            })
            .ToArray();

        return new ConfigurationSchema(SettingsSchemaVersion, fields);
    }

    private static string SerializeDefaultSettings<TSettings>(
        TSettings defaultSettings,
        JsonSerializerOptions? settingsJsonOptions)
        => settingsJsonOptions is null
            ? WorkflowExecutorJson.Serialize(defaultSettings)
            : JsonSerializer.Serialize(defaultSettings, settingsJsonOptions);

    private static ConfigurationFieldType ResolveFieldType(Type propertyType)
    {
        var type = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
        if (type == typeof(bool))
        {
            return ConfigurationFieldType.Boolean;
        }

        if (type == typeof(int) ||
            type == typeof(long) ||
            type == typeof(decimal) ||
            type == typeof(double) ||
            type == typeof(float))
        {
            return ConfigurationFieldType.Number;
        }

        if (type.IsEnum)
        {
            return ConfigurationFieldType.Select;
        }

        if (type == typeof(string) || type == typeof(Guid))
        {
            return ConfigurationFieldType.Text;
        }

        return ConfigurationFieldType.Json;
    }

    private static IReadOnlyList<ConfigurationFieldOption> ResolveOptions(Type propertyType)
    {
        var type = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
        if (!type.IsEnum)
        {
            return [];
        }

        return Enum.GetValues(type)
            .Cast<object>()
            .Select(value =>
            {
                var name = Enum.GetName(type, value) ?? value.ToString() ?? string.Empty;
                return new ConfigurationFieldOption(name, name)
                {
                    AcceptedValues = [Convert.ToInt64(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture)]
                };
            })
            .ToArray();
    }
}
