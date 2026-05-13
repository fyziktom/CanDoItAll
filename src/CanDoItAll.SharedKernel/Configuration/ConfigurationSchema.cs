using System.Text.Json;

namespace CanDoItAll.SharedKernel.Configuration;

public enum ConfigurationFieldType
{
    Text,
    Url,
    Number,
    Boolean,
    Json,
    SecretReference,
    Select,
    MultilineText
}

public sealed record ConfigurationFieldOption(string Value, string Label)
{
    public IReadOnlyList<string> AcceptedValues { get; init; } = [];
}

public record ConfigurationFieldDescriptor(
    string Key,
    string Label,
    ConfigurationFieldType FieldType,
    bool IsRequired,
    string HelpText)
{
    public IReadOnlyList<ConfigurationFieldOption> Options { get; init; } = [];
}

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
        SetText(key, value?.ToString());
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
            var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(json, SerializerOptions);
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
                if (!int.TryParse(value, out _))
                {
                    issues.Add(new ConfigurationValidationIssue(key, $"Configuration field '{key}' must be a number."));
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
}
