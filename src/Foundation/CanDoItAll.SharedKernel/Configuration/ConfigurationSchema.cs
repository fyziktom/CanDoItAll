using System.Globalization;
using System.Text.Json;

namespace CanDoItAll.SharedKernel.Configuration;

/// <summary>
/// Value type of a settings field, as a JSON integer: 0 Text, 1 Url (absolute http or https URL), 2 Number (see
/// <c>numberKind</c>), 3 Boolean (<c>true</c> or <c>false</c>), 4 Json (JSON text), 5 SecretReference (identifier of a
/// stored secret, a non-empty GUID), 6 Select (one of the field's options), 7 MultilineText, 8 Guid (a non-empty GUID).
/// </summary>
public enum ConfigurationFieldType
{
    Text,
    Url,
    Number,
    Boolean,
    Json,
    SecretReference,
    Select,
    MultilineText,
    Guid
}

/// <summary>
/// Number format of a Number settings field, as a JSON integer: 0 Int32, 1 Int64, 2 Decimal, 3 Double (finite values
/// only).
/// </summary>
public enum ConfigurationNumberKind
{
    Int32,
    Int64,
    Decimal,
    Double
}

/// <summary>
/// One allowed value of a Select settings field.
/// </summary>
/// <param name="Value">The value to store.</param>
/// <param name="Label">Display label of the value.</param>
public sealed record ConfigurationFieldOption(string Value, string Label)
{
    /// <summary>
    /// Other values accepted as equivalent to <c>value</c> (compared case-insensitively); empty when there are none.
    /// </summary>
    public IReadOnlyList<string> AcceptedValues { get; init; } = [];
}

/// <summary>
/// One field of a settings form.
/// </summary>
/// <param name="Key">
/// Key of the field in the settings JSON object, for example <c>label</c>; settings keys are compared
/// case-insensitively.
/// </param>
/// <param name="Label">Display label of the field.</param>
/// <param name="FieldType">
/// Value type, as a JSON integer: 0 Text, 1 Url, 2 Number, 3 Boolean, 4 Json, 5 SecretReference, 6 Select,
/// 7 MultilineText, 8 Guid.
/// </param>
/// <param name="IsRequired">True when the field must have a non-blank value.</param>
/// <param name="HelpText">Help text shown with the field.</param>
public record ConfigurationFieldDescriptor(
    string Key,
    string Label,
    ConfigurationFieldType FieldType,
    bool IsRequired,
    string HelpText)
{
    /// <summary>Allowed values of a Select field; empty for other field types.</summary>
    public IReadOnlyList<ConfigurationFieldOption> Options { get; init; } = [];

    /// <summary>
    /// Number format of a Number field, as a JSON integer: 0 Int32, 1 Int64, 2 Decimal, 3 Double. 0 by default; ignored
    /// for other field types.
    /// </summary>
    public ConfigurationNumberKind NumberKind { get; init; } = ConfigurationNumberKind.Int32;
}

/// <summary>
/// Description of a settings form: the fields of a flat JSON settings object, with their value types and whether
/// they are required. Plugins use it for connection, plugin and workflow executor settings. It is not a JSON Schema
/// document.
/// </summary>
/// <param name="Version">Version label of the form, for example <c>1.0</c>.</param>
/// <param name="Fields">The fields, in display order; empty when there are no settings.</param>
public record ConfigurationSchema(
    string Version,
    IReadOnlyList<ConfigurationFieldDescriptor> Fields)
{
    public static ConfigurationSchema Empty(string version = "1.0") => new(version, []);
}

public class ConfigurationState
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly Dictionary<string, string> values;

    public ConfigurationState()
        : this(null)
    {
    }

    public ConfigurationState(IReadOnlyDictionary<string, string>? values)
    {
        this.values = values is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(values, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyDictionary<string, string> Values => values;

    public string GetText(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return string.Empty;
        }

        return values.TryGetValue(key, out var value)
            ? value
            : string.Empty;
    }

    public void SetText(string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        var normalizedKey = key.Trim();
        var normalizedValue = value?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedValue))
        {
            values.Remove(normalizedKey);
            return;
        }

        values[normalizedKey] = normalizedValue;
    }

    public int? GetNumber(string key)
    {
        var rawValue = GetText(key);
        return int.TryParse(rawValue, out var parsed)
            ? parsed
            : null;
    }

    public void SetNumber(string key, int? value)
    {
        SetText(key, value?.ToString(CultureInfo.InvariantCulture));
    }

    public decimal? GetDecimalNumber(string key)
    {
        var rawValue = GetText(key);
        return decimal.TryParse(
            rawValue,
            NumberStyles.Number | NumberStyles.AllowExponent,
            CultureInfo.InvariantCulture,
            out var parsed)
                ? parsed
                : null;
    }

    public void SetDecimalNumber(string key, decimal? value)
    {
        SetText(key, value?.ToString(CultureInfo.InvariantCulture));
    }

    public bool GetBoolean(string key)
    {
        var rawValue = GetText(key);
        return bool.TryParse(rawValue, out var parsed) && parsed;
    }

    public void SetBoolean(string key, bool value)
    {
        SetText(key, value ? bool.TrueString : bool.FalseString);
    }

    public void KeepOnly(IEnumerable<string> allowedKeys)
    {
        ArgumentNullException.ThrowIfNull(allowedKeys);

        var allowed = allowedKeys
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Select(key => key.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var staleKeys = values.Keys
            .Where(key => !allowed.Contains(key))
            .ToList();
        foreach (var key in staleKeys)
        {
            values.Remove(key);
        }
    }

    public ConfigurationState Clone()
    {
        return new ConfigurationState(values);
    }

    public string ToJson()
    {
        return JsonSerializer.Serialize(values, SerializerOptions);
    }

    public static ConfigurationState FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new ConfigurationState();
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return new ConfigurationState();
            }

            var parsed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                var value = property.Value.ValueKind switch
                {
                    JsonValueKind.String => property.Value.GetString(),
                    JsonValueKind.Number => property.Value.GetRawText(),
                    JsonValueKind.True => bool.TrueString,
                    JsonValueKind.False => bool.FalseString,
                    _ => null
                };

                if (!string.IsNullOrWhiteSpace(value))
                {
                    parsed[property.Name] = value;
                }
            }

            return new ConfigurationState(parsed);
        }
        catch (JsonException)
        {
            return new ConfigurationState();
        }
    }
}

public sealed record ConfigurationValidationIssue(
    string FieldKey,
    string Message);

public sealed record ConfigurationValidationResult(IReadOnlyList<ConfigurationValidationIssue> Issues)
{
    public bool Succeeded => Issues.Count == 0;

    public static ConfigurationValidationResult Success { get; } = new([]);
}

public interface IConfigurationSchemaValidator
{
    ConfigurationValidationResult Validate(ConfigurationSchema schema, ConfigurationState state);
}

public sealed class ConfigurationSchemaValidator : IConfigurationSchemaValidator
{
    public ConfigurationValidationResult Validate(ConfigurationSchema schema, ConfigurationState state)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(state);

        var issues = new List<ConfigurationValidationIssue>();
        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in schema.Fields)
        {
            if (string.IsNullOrWhiteSpace(field.Key))
            {
                issues.Add(new ConfigurationValidationIssue(string.Empty, "Configuration field key is required."));
                continue;
            }

            var key = field.Key.Trim();
            if (!seenKeys.Add(key))
            {
                issues.Add(new ConfigurationValidationIssue(key, $"Configuration field '{key}' is duplicated."));
                continue;
            }

            if (!Enum.IsDefined(field.FieldType))
            {
                issues.Add(new ConfigurationValidationIssue(
                    key,
                    $"Configuration field '{key}' has an undefined field type."));
                continue;
            }

            if (field.FieldType == ConfigurationFieldType.Number &&
                !Enum.IsDefined(field.NumberKind))
            {
                issues.Add(new ConfigurationValidationIssue(
                    key,
                    $"Configuration field '{key}' has an undefined number kind."));
                continue;
            }

            var value = state.GetText(key);
            if (field.IsRequired && string.IsNullOrWhiteSpace(value))
            {
                issues.Add(new ConfigurationValidationIssue(key, $"Configuration field '{key}' is required."));
                continue;
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            AddTypeIssues(field, key, value, issues);
        }

        return issues.Count == 0
            ? ConfigurationValidationResult.Success
            : new ConfigurationValidationResult(issues);
    }

    private static void AddTypeIssues(
        ConfigurationFieldDescriptor field,
        string key,
        string value,
        List<ConfigurationValidationIssue> issues)
    {
        switch (field.FieldType)
        {
            case ConfigurationFieldType.Url:
                if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
                    uri.Scheme is not ("http" or "https"))
                {
                    issues.Add(new ConfigurationValidationIssue(key, $"Configuration field '{key}' must be an absolute HTTP or HTTPS URL."));
                }

                return;
            case ConfigurationFieldType.Number:
                if (!IsValidNumber(field.NumberKind, value))
                {
                    issues.Add(new ConfigurationValidationIssue(
                        key,
                        $"Configuration field '{key}' must be a valid {field.NumberKind} number."));
                }

                return;
            case ConfigurationFieldType.Guid:
                if (!Guid.TryParse(value, out var guid) || guid == Guid.Empty)
                {
                    issues.Add(new ConfigurationValidationIssue(key, $"Configuration field '{key}' must be a non-empty GUID."));
                }

                return;
            case ConfigurationFieldType.Boolean:
                if (!bool.TryParse(value, out _))
                {
                    issues.Add(new ConfigurationValidationIssue(key, $"Configuration field '{key}' must be true or false."));
                }

                return;
            case ConfigurationFieldType.Json:
                try
                {
                    using var _ = JsonDocument.Parse(value);
                }
                catch (JsonException)
                {
                    issues.Add(new ConfigurationValidationIssue(key, $"Configuration field '{key}' must be valid JSON."));
                }

                return;
            case ConfigurationFieldType.SecretReference:
                if (!Guid.TryParse(value, out var secretId) || secretId == Guid.Empty)
                {
                    issues.Add(new ConfigurationValidationIssue(key, $"Configuration field '{key}' must reference a stored secret."));
                }

                return;
            case ConfigurationFieldType.Select:
                if (field.Options.Count > 0 &&
                    !field.Options.Any(option => IsConfiguredOptionValue(option, value)))
                {
                    issues.Add(new ConfigurationValidationIssue(key, $"Configuration field '{key}' must use one of the configured options."));
                }

                return;
            case ConfigurationFieldType.Text:
            case ConfigurationFieldType.MultilineText:
            default:
                return;
        }
    }

    private static bool IsConfiguredOptionValue(
        ConfigurationFieldOption option,
        string value)
    {
        return string.Equals(option.Value, value, StringComparison.OrdinalIgnoreCase) ||
               option.AcceptedValues.Any(acceptedValue => string.Equals(acceptedValue, value, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsValidNumber(
        ConfigurationNumberKind numberKind,
        string value)
        => numberKind switch
        {
            ConfigurationNumberKind.Int32 => int.TryParse(
                value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out _),
            ConfigurationNumberKind.Int64 => long.TryParse(
                value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out _),
            ConfigurationNumberKind.Decimal => decimal.TryParse(
                value,
                NumberStyles.Number | NumberStyles.AllowExponent,
                CultureInfo.InvariantCulture,
                out _),
            ConfigurationNumberKind.Double => double.TryParse(
                value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var number) && double.IsFinite(number),
            _ => false
        };
}
