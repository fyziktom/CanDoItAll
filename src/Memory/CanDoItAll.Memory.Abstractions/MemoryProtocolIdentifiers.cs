using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace CanDoItAll.Memory.Abstractions;

internal static partial class MemoryProtocolGuard
{
    public static string EnsureText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value must not be empty.", parameterName);
        }

        return value.Trim();
    }

    public static Guid EnsureNonEmpty(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Value must not be empty.", parameterName);
        }

        return value;
    }

    public static string EnsureCapabilityId(string value, string parameterName)
    {
        var normalized = EnsureText(value, parameterName);
        if (!CapabilityIdPattern().IsMatch(normalized))
        {
            throw new ArgumentException(
                "Capability ids must use dotted lowercase tokens such as 'context.query.sync'.",
                parameterName);
        }

        return normalized;
    }

    public static string EnsureExtensionKey(string value, string parameterName)
    {
        var normalized = EnsureText(value, parameterName);
        if (MemoryExtensionData.ReservedNamespaces.Any(prefix => normalized.StartsWith(prefix, StringComparison.Ordinal)))
        {
            return normalized;
        }

        throw new ArgumentException(
            "Extension keys must start with one of the reserved namespaces: host.candoitall.*, native.cognitiveMemory.*, or provider.vendor.*.",
            parameterName);
    }

    [GeneratedRegex("^[a-z][a-z0-9]*(?:[.-][a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex CapabilityIdPattern();
}

public readonly record struct MemoryProtocolVersion
{
    public const string CurrentValue = "memory-protocol.v1";

    public static readonly MemoryProtocolVersion Current = new(CurrentValue);

    [JsonConstructor]
    public MemoryProtocolVersion(string value)
    {
        Value = MemoryProtocolGuard.EnsureText(value, nameof(value));
        if (!string.Equals(Value, CurrentValue, StringComparison.Ordinal))
        {
            throw new NotSupportedException($"Unsupported memory protocol version '{Value}'.");
        }
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct MemoryProviderInstanceId
{
    [JsonConstructor]
    public MemoryProviderInstanceId(string value)
    {
        Value = MemoryProtocolGuard.EnsureText(value, nameof(value));
    }

    public string Value { get; }

    public static MemoryProviderInstanceId Parse(string value) => new(value);

    public override string ToString() => Value;
}

public readonly record struct MemoryProviderKind
{
    [JsonConstructor]
    public MemoryProviderKind(string value)
    {
        Value = MemoryProtocolGuard.EnsureCapabilityId(value, nameof(value));
    }

    public string Value { get; }

    public static MemoryProviderKind Parse(string value) => new(value);

    public override string ToString() => Value;
}

public readonly record struct MemoryCapabilityId
{
    [JsonConstructor]
    public MemoryCapabilityId(string value)
    {
        Value = MemoryProtocolGuard.EnsureCapabilityId(value, nameof(value));
    }

    public string Value { get; }

    public static MemoryCapabilityId Parse(string value) => new(value);

    public override string ToString() => Value;
}
