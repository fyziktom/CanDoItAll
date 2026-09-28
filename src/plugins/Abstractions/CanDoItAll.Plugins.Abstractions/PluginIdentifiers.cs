using System.Text.Json.Serialization;

namespace CanDoItAll.Plugins.Abstractions;

/// <summary>
/// Identifier of a plugin, for example <c>gmail.mail</c>, <c>office365.mail</c> or <c>candoitall.docker</c>. On the
/// wire it is a JSON string of letters, digits, <c>.</c>, <c>-</c> and <c>_</c>; the server trims and lower-cases it.
/// Request bodies may also send the object form <c>{"value":"gmail.mail"}</c>; responses always use the string.
/// </summary>
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

/// <summary>
/// Identifier of a plugin package, for example <c>gmail.mail.bundled</c> or <c>candoitall.docker.package</c>. On the
/// wire it is a JSON string of letters, digits, <c>.</c>, <c>-</c> and <c>_</c>; the server trims and lower-cases it.
/// Request bodies may also send the object form <c>{"value":"candoitall.docker.package"}</c>; responses always use
/// the string.
/// </summary>
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

/// <summary>
/// Identifier of a saved plugin connection: on the wire a JSON string containing a non-empty GUID, for example
/// <c>"5b0f3c1e-7d2a-4c9b-8e61-2f4a9d0c7b13"</c>. Request bodies may also send the object form
/// <c>{"value":"GUID"}</c>; responses always use the string.
/// </summary>
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

/// <summary>
/// Key of a connection kind that a plugin declares, for example <c>gmail</c> or <c>office365</c>. On the wire it is a
/// JSON string of letters, digits, <c>.</c>, <c>-</c> and <c>_</c>; the server trims and lower-cases it. Request bodies
/// may also send the object form <c>{"value":"gmail"}</c>; responses always use the string.
/// </summary>
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

/// <summary>
/// Key of a settings renderer (a settings user interface component), for example <c>gmail.settings</c>. On the wire
/// it is a JSON string of letters, digits, <c>.</c>, <c>-</c> and <c>_</c>, lower-cased by the server.
/// </summary>
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
