using System.Globalization;
using System.Text;
using System.Text.Json;
using CanDoItAll.SharedKernel.Configuration;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

public static class WorkflowExecutorConfigurationMapper
{
    private const string InputKeyPrefix = "executor-setting:";

    public static string BuildInputKey(string fieldKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldKey);
        return $"{InputKeyPrefix}{fieldKey.Trim()}";
    }

    public static ConfigurationState ReadState(
        string? settingsJson,
        ConfigurationSchema schema)
    {
        ArgumentNullException.ThrowIfNull(schema);
        if (string.IsNullOrWhiteSpace(settingsJson))
        {
            return new ConfigurationState();
        }

        try
        {
            using var document = JsonDocument.Parse(settingsJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException("Workflow executor settings must be a JSON object.");
            }

            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                var value = property.Value.ValueKind switch
                {
                    JsonValueKind.String => property.Value.GetString() ?? string.Empty,
                    JsonValueKind.Null => string.Empty,
                    _ => property.Value.GetRawText()
                };
                values[property.Name] = NormalizeSelectValue(property.Name, value, schema);
            }

            return new ConfigurationState(values);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException("Workflow executor settings contain invalid JSON.", exception);
        }
    }

    public static string SerializeState(
        ConfigurationSchema schema,
        ConfigurationState state)
        => SerializeState(schema, state, requireCompleteConfiguration: false);

    public static string SerializeCompleteState(
        ConfigurationSchema schema,
        ConfigurationState state)
        => SerializeState(schema, state, requireCompleteConfiguration: true);

    private static string SerializeState(
        ConfigurationSchema schema,
        ConfigurationState state,
        bool requireCompleteConfiguration)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(state);

        var validationSchema = requireCompleteConfiguration
            ? schema
            : schema with
            {
                Fields = schema.Fields
                    .Select(field => field with { IsRequired = false })
                    .ToArray()
            };
        var validation = new ConfigurationSchemaValidator().Validate(validationSchema, state);
        if (!validation.Succeeded)
        {
            throw new InvalidOperationException(string.Join(
                Environment.NewLine,
                validation.Issues.Select(issue => issue.Message)));
        }

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            var writtenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var field in schema.Fields)
            {
                if (!state.Values.TryGetValue(field.Key, out var value) ||
                    string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                WriteValue(writer, field.Key, value, field);
                writtenKeys.Add(field.Key);
            }

            foreach (var item in state.Values)
            {
                if (writtenKeys.Contains(item.Key) ||
                    string.IsNullOrWhiteSpace(item.Value))
                {
                    continue;
                }

                writer.WriteString(item.Key, item.Value);
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static string NormalizeSelectValue(
        string key,
        string value,
        ConfigurationSchema schema)
    {
        var field = schema.Fields.FirstOrDefault(candidate =>
            string.Equals(candidate.Key, key, StringComparison.OrdinalIgnoreCase));
        if (field?.FieldType != ConfigurationFieldType.Select)
        {
            return value;
        }

        var option = field.Options.FirstOrDefault(candidate =>
            string.Equals(candidate.Value, value, StringComparison.OrdinalIgnoreCase) ||
            candidate.AcceptedValues.Any(acceptedValue =>
                string.Equals(acceptedValue, value, StringComparison.OrdinalIgnoreCase)));
        return option?.Value ?? value;
    }

    private static void WriteValue(
        Utf8JsonWriter writer,
        string key,
        string value,
        ConfigurationFieldDescriptor field)
    {
        switch (field.FieldType)
        {
            case ConfigurationFieldType.Number:
                WriteNumber(writer, key, value, field.NumberKind);
                return;
            case ConfigurationFieldType.Boolean:
                writer.WriteBoolean(
                    key,
                    bool.Parse(value));
                return;
            case ConfigurationFieldType.Json:
                WriteJson(writer, key, value);
                return;
            default:
                writer.WriteString(key, value);
                return;
        }
    }

    private static void WriteNumber(
        Utf8JsonWriter writer,
        string key,
        string value,
        ConfigurationNumberKind numberKind)
    {
        switch (numberKind)
        {
            case ConfigurationNumberKind.Int32 when int.TryParse(
                value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var int32):
                writer.WriteNumber(key, int32);
                return;
            case ConfigurationNumberKind.Int64 when long.TryParse(
                value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var int64):
                writer.WriteNumber(key, int64);
                return;
            case ConfigurationNumberKind.Decimal when decimal.TryParse(
                value,
                NumberStyles.Number | NumberStyles.AllowExponent,
                CultureInfo.InvariantCulture,
                out var decimalNumber):
                writer.WriteNumber(key, decimalNumber);
                return;
            case ConfigurationNumberKind.Double when double.TryParse(
                value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var doubleNumber) && double.IsFinite(doubleNumber):
                writer.WriteNumber(key, doubleNumber);
                return;
            default:
                throw new InvalidOperationException(
                    $"Workflow executor setting '{key}' must be a valid {numberKind} number.");
        }
    }

    private static void WriteJson(
        Utf8JsonWriter writer,
        string key,
        string value)
    {
        try
        {
            using var document = JsonDocument.Parse(value);
            writer.WritePropertyName(key);
            document.RootElement.WriteTo(writer);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                $"Workflow executor setting '{key}' must contain valid JSON.",
                exception);
        }
    }
}
