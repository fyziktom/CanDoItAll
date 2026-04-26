using System.Text.Json;

namespace CanDoItAll.Modules.Workspace;

public sealed class ConnectorConfigState
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly Dictionary<string, string> values;

    public ConnectorConfigState()
        : this(null)
    {
    }

    public ConnectorConfigState(IDictionary<string, string>? values)
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

    public ConnectorConfigState Clone()
    {
        return new ConnectorConfigState(values);
    }

    public string ToJson()
    {
        return JsonSerializer.Serialize(values, SerializerOptions);
    }

    public static ConnectorConfigState FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new ConnectorConfigState();
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(json, SerializerOptions);
            return new ConnectorConfigState(parsed);
        }
        catch (JsonException)
        {
            return new ConnectorConfigState();
        }
    }
}
