using System.Text.Json.Serialization;

namespace CanDoItAll.Plugins.Abstractions;

[JsonConverter(typeof(PluginIdJsonConverter))]
public readonly record struct PluginId
{
    public PluginId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Plugin id cannot be empty.", nameof(value));
        }

        Value = NormalizeIdentifier(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;

    internal static string NormalizeIdentifier(string value, string parameterName)
    {
        var normalized = value.Trim().ToLowerInvariant();
        if (normalized.Any(character => !char.IsLetterOrDigit(character) && character is not '.' and not '-' and not '_'))
        {
            throw new ArgumentException("Identifier can contain only letters, digits, '.', '-', and '_'.", parameterName);
        }

        return normalized;
    }
}

[JsonConverter(typeof(PluginPackageIdJsonConverter))]
public readonly record struct PluginPackageId
{
    public PluginPackageId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Plugin package id cannot be empty.", nameof(value));
        }

        Value = PluginId.NormalizeIdentifier(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;
}

[JsonConverter(typeof(PluginConnectionIdJsonConverter))]
public readonly record struct PluginConnectionId
{
    public PluginConnectionId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Plugin connection id cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }

    public override string ToString() => Value.ToString("D");
}

[JsonConverter(typeof(PluginConnectionKeyJsonConverter))]
public readonly record struct PluginConnectionKey
{
    public PluginConnectionKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Plugin connection key cannot be empty.", nameof(value));
        }

        Value = PluginId.NormalizeIdentifier(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;
}

[JsonConverter(typeof(PluginRendererKeyJsonConverter))]
public readonly record struct PluginRendererKey
{
    public PluginRendererKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Plugin renderer key cannot be empty.", nameof(value));
        }

        Value = PluginId.NormalizeIdentifier(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;
}
