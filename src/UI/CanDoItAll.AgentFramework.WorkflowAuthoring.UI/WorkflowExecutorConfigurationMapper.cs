using System.Globalization;
using System.Text;
using System.Text.Json;
using CanDoItAll.SharedKernel.Configuration;

namespace CanDoItAll.AgentFramework.WorkflowAuthoring.UI;

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
                var field = schema.Fields.SingleOrDefault(item => string.Equals(item.Key, property.Name, StringComparison.OrdinalIgnoreCase));
                if (field is null) {
                    continue;
                }
                if (values.ContainsKey(field.Key)) {
                    throw new InvalidOperationException("Multiple settings properties map to the same schema field. Edit the original JSON to resolve the ambiguity.");
                }
                var value = property.Value.ValueKind switch
                {
                    JsonValueKind.String => property.Value.GetString() ?? string.Empty,
                    JsonValueKind.Null => string.Empty,
                    _ => property.Value.GetRawText()
                };
                values[field.Key] = NormalizeSelectValue(field.Key, value, schema);
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

    public static string MergeState(string originalJson, ConfigurationSchema schema, ConfigurationState state) {
        var original = string.IsNullOrWhiteSpace(originalJson) ? "{}" : originalJson;
        var before = ReadState(original, schema);
        _ = SerializeState(schema, state);
        var changed = schema.Fields.Where(field => !string.Equals(before.GetText(field.Key), state.GetText(field.Key), StringComparison.Ordinal))
            .ToDictionary(field => field.Key, StringComparer.OrdinalIgnoreCase);
        if (changed.Count == 0) {
            return originalJson;
        }
        using var source = JsonDocument.Parse(original);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) {
            writer.WriteStartObject();
            foreach (var property in source.RootElement.EnumerateObject()) {
                if (!changed.Remove(property.Name, out var field)) {
                    property.WriteTo(writer);
                    continue;
                }
                var value = state.GetText(field.Key);
                if (!string.IsNullOrWhiteSpace(value)) {
                    WriteValue(writer, property.Name, value, field);
                }
            }
            foreach (var field in changed.Values) {
                var value = state.GetText(field.Key);
                if (!string.IsNullOrWhiteSpace(value)) {
                    WriteValue(writer, field.Key, value, field);
                }
            }
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

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
