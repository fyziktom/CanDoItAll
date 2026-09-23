namespace CanDoItAll.AgentFramework.Capabilities.Abstractions;

/// <summary>
/// Capability key in lower kebab case, for example <c>release-notes</c>. It is serialized as an object whose
/// <c>value</c> member holds the key.
/// </summary>
/// <param name="Value">The key: groups of lower-case ASCII letters and digits separated by single hyphens.</param>
public readonly record struct CapabilityKey(string Value)
{
    public static bool TryCreate(string? value, out CapabilityKey key)
        => CapabilityNameRules.TryCreateKebab(value, out key);

    public static CapabilityKey Create(string value)
        => TryCreate(value, out var key)
            ? key
            : throw new ArgumentException($"Capability key '{value}' must be lower kebab-case.", nameof(value));

    public override string ToString()
        => Value;
}

/// <summary>
/// Skill name in lower kebab case, serialized as an object whose <c>value</c> member holds the name.
/// </summary>
/// <param name="Value">The name: groups of lower-case ASCII letters and digits separated by single hyphens.</param>
public readonly record struct SkillName(string Value)
{
    public static bool TryCreate(string? value, out SkillName name)
    {
        if (CapabilityNameRules.TryCreateKebab(value, out CapabilityKey capabilityKey))
        {
            name = new SkillName(capabilityKey.Value);
            return true;
        }

        name = default;
        return false;
    }

    public static SkillName Create(string value)
        => TryCreate(value, out var name)
            ? name
            : throw new ArgumentException(
                $"Skill name '{value}' must use only lowercase ASCII letters, numbers, and hyphens, and must not start or end with a hyphen or contain consecutive hyphens.",
                nameof(value));

    public static SkillName Normalize(string value)
        => Create(CapabilityNameRules.NormalizeLowerKebab(value));

    public override string ToString()
        => Value;
}

/// <summary>
/// Name of a runtime tool as offered to the model, in lower snake case, for example <c>read_file</c>. It is
/// serialized as an object whose <c>value</c> member holds the name.
/// </summary>
/// <param name="Value">
/// The name: a lower-case ASCII letter followed by at most 127 lower-case letters, digits or underscores.
/// </param>
public readonly record struct RuntimeToolName(string Value)
{
    public static bool TryCreate(string? value, out RuntimeToolName name)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (CapabilityNameRules.LowerSnakeRegex.IsMatch(normalized))
        {
            name = new RuntimeToolName(normalized);
            return true;
        }

        name = default;
        return false;
    }

    public static RuntimeToolName Create(string value)
        => TryCreate(value, out var name)
            ? name
            : throw new ArgumentException($"Runtime tool name '{value}' must be lower snake_case.", nameof(value));

    public override string ToString()
        => Value;
}

/// <summary>
/// Key of an MCP server capability in lower kebab case, serialized as an object whose <c>value</c> member holds the
/// key.
/// </summary>
/// <param name="Value">The key: groups of lower-case ASCII letters and digits separated by single hyphens.</param>
public readonly record struct McpServerKey(string Value)
{
    public static bool TryCreate(string? value, out McpServerKey key)
    {
        if (CapabilityNameRules.TryCreateKebab(value, out CapabilityKey capabilityKey))
        {
            key = new McpServerKey(capabilityKey.Value);
            return true;
        }

        key = default;
        return false;
    }

    public static McpServerKey Create(string value)
        => TryCreate(value, out var key)
            ? key
            : throw new ArgumentException($"MCP server key '{value}' must be lower kebab-case.", nameof(value));

    public override string ToString()
        => Value;
}

/// <summary>
/// Name of a tool exposed by an MCP server, serialized as an object whose <c>value</c> member holds the name.
/// </summary>
/// <param name="Value">The name: 1 to 128 ASCII letters, digits, periods, underscores or hyphens.</param>
public readonly record struct McpToolName(string Value)
{
    public static bool TryCreate(string? value, out McpToolName name)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (CapabilityNameRules.McpToolRegex.IsMatch(normalized))
        {
            name = new McpToolName(normalized);
            return true;
        }

        name = default;
        return false;
    }

    public static McpToolName Create(string value)
        => TryCreate(value, out var name)
            ? name
            : throw new ArgumentException($"MCP tool name '{value}' must be 1-128 ASCII letters, digits, '.', '_' or '-'.", nameof(value));

    public override string ToString()
        => Value;
}

/// <summary>
/// Key of the implementation behind a capability, for example <c>tool.release-notes</c>, serialized as an object
/// whose <c>value</c> member holds the key.
/// </summary>
/// <param name="Value">
/// The key: groups of lower-case ASCII letters and digits separated by single periods, underscores or hyphens.
/// </param>
public readonly record struct ImplementationKey(string Value)
{
    public static bool TryCreate(string? value, out ImplementationKey key)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (CapabilityNameRules.ImplementationKeyRegex.IsMatch(normalized))
        {
            key = new ImplementationKey(normalized);
            return true;
        }

        key = default;
        return false;
    }

    public static ImplementationKey Create(string value)
        => TryCreate(value, out var key)
            ? key
            : throw new ArgumentException($"Implementation key '{value}' must use ASCII lower-case segments separated by '.', '_' or '-'.", nameof(value));

    public override string ToString()
        => Value;
}

/// <summary>
/// Capability tag in lower kebab case, serialized as an object whose <c>value</c> member holds the tag.
/// </summary>
/// <param name="Value">The tag: groups of lower-case ASCII letters and digits separated by single hyphens.</param>
public readonly record struct CapabilityTag(string Value)
{
    public static bool TryCreate(string? value, out CapabilityTag tag)
    {
        if (CapabilityNameRules.TryCreateKebab(value, out CapabilityKey capabilityKey))
        {
            tag = new CapabilityTag(capabilityKey.Value);
            return true;
        }

        tag = default;
        return false;
    }

    public static CapabilityTag Create(string value)
        => TryCreate(value, out var tag)
            ? tag
            : throw new ArgumentException($"Capability tag '{value}' must be lower kebab-case.", nameof(value));

    public override string ToString()
        => Value;
}

/// <summary>
/// Name of a process operation contract in PascalCase, serialized as an object whose <c>value</c> member holds the
/// name.
/// </summary>
/// <param name="Value">The name: an upper-case ASCII letter followed by at most 127 ASCII letters or digits.</param>
public readonly record struct ProcessOperationKey(string Value)
{
    public static bool TryCreate(string? value, out ProcessOperationKey key)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (CapabilityNameRules.PascalIdentifierRegex.IsMatch(normalized))
        {
            key = new ProcessOperationKey(normalized);
            return true;
        }

        key = default;
        return false;
    }

    public static ProcessOperationKey Create(string value)
        => TryCreate(value, out var key)
            ? key
            : throw new ArgumentException($"Process operation key '{value}' must be a PascalCase operation contract name.", nameof(value));

    public override string ToString()
        => Value;
}

/// <summary>
/// Identifier of a capability access rule in lower kebab case, serialized as an object whose <c>value</c> member holds
/// the identifier.
/// </summary>
/// <param name="Value">
/// The identifier: groups of lower-case ASCII letters and digits separated by single hyphens.
/// </param>
public readonly record struct CapabilityRuleId(string Value)
{
    public static bool TryCreate(string? value, out CapabilityRuleId id)
    {
        if (CapabilityNameRules.TryCreateKebab(value, out CapabilityKey capabilityKey))
        {
            id = new CapabilityRuleId(capabilityKey.Value);
            return true;
        }

        id = default;
        return false;
    }

    public static CapabilityRuleId Create(string value)
        => TryCreate(value, out var id)
            ? id
            : throw new ArgumentException($"Capability access rule id '{value}' must be lower kebab-case.", nameof(value));

    public override string ToString()
        => Value;
}

/// <summary>
/// Stable identifier of a capability definition, serialized as an object whose <c>value</c> member holds the
/// identifier.
/// </summary>
/// <param name="Value">The identifier: 1 to 128 ASCII letters, digits, underscores, periods, colons or hyphens.</param>
public readonly record struct CapabilityStableId(string Value)
{
    public static bool TryCreate(string? value, out CapabilityStableId id)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (CapabilityNameRules.StableIdRegex.IsMatch(normalized))
        {
            id = new CapabilityStableId(normalized);
            return true;
        }

        id = default;
        return false;
    }

    public static CapabilityStableId Create(string value)
        => TryCreate(value, out var id)
            ? id
            : throw new ArgumentException($"Stable id '{value}' must use ASCII identifier characters.", nameof(value));

    public override string ToString()
        => Value;
}

/// <summary>
/// Path of the template or definition a capability or diagnostic comes from, with forward slashes; serialized as an
/// object whose <c>value</c> member holds the path.
/// </summary>
/// <param name="Value">The path, trimmed and with forward slashes.</param>
public readonly record struct TemplatePath(string Value)
{
    public static TemplatePath Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Template path is required.", nameof(value));
        }

        return new TemplatePath(value.Trim().Replace('\\', '/'));
    }

    public override string ToString()
        => Value;
}
