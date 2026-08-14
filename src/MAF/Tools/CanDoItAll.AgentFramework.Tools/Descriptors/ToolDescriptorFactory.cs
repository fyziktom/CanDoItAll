using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Tools.Abstractions;

namespace CanDoItAll.AgentFramework.Tools;

public static class ToolDescriptorFactory
{
    public static InternalToolDescriptor Internal(
        CapabilityKey key,
        RuntimeToolName runtimeToolName,
        ImplementationKey implementationKey,
        IEnumerable<CapabilityTag> tags,
        IEnumerable<CapabilityOperationClassification> operationClassifications,
        CapabilitySideEffectProfile sideEffectProfile)
    {
        return new InternalToolDescriptor(
            Identity(key),
            runtimeToolName,
            implementationKey,
            NormalizeTags(tags),
            NormalizeClassifications(operationClassifications),
            sideEffectProfile);
    }

    public static ExternalProcessToolDescriptor ExternalProcess(
        CapabilityKey key,
        RuntimeToolName runtimeToolName,
        ImplementationKey implementationKey,
        string executablePath,
        IEnumerable<string> arguments,
        string workingDirectory,
        TimeSpan timeout,
        int maxOutputBytes,
        IEnumerable<string> allowedExecutableNames,
        IEnumerable<string> requiredOutputProperties)
    {
        return new ExternalProcessToolDescriptor(
            Identity(key),
            runtimeToolName,
            implementationKey,
            NormalizeTags([CapabilityTag.Create("external"), CapabilityTag.Create("process")]),
            NormalizeClassifications([CapabilityOperationClassification.ExternalAction]),
            new CapabilitySideEffectProfile(CapabilitySideEffectKind.ExternalAction, true, true),
            RequireText(executablePath, nameof(executablePath)),
            PreserveSequence(arguments),
            RequireDataValue(workingDirectory, nameof(workingDirectory)),
            timeout,
            Math.Max(64, maxOutputBytes),
            NormalizeAuthoritySet(allowedExecutableNames),
            NormalizeAuthoritySet(requiredOutputProperties));
    }

    public static ExternalHttpToolDescriptor ExternalHttp(
        CapabilityKey key,
        RuntimeToolName runtimeToolName,
        ImplementationKey implementationKey,
        HttpMethod method,
        Uri endpoint,
        IReadOnlyDictionary<string, string> headers,
        TimeSpan timeout,
        int maxResponseBytes,
        IEnumerable<string> requiredOutputProperties)
    {
        return new ExternalHttpToolDescriptor(
            Identity(key),
            runtimeToolName,
            implementationKey,
            NormalizeTags([CapabilityTag.Create("external"), CapabilityTag.Create("http")]),
            NormalizeClassifications([CapabilityOperationClassification.ExternalAction]),
            new CapabilitySideEffectProfile(CapabilitySideEffectKind.ExternalAction, true, true),
            method,
            endpoint,
            new Dictionary<string, string>(headers, StringComparer.OrdinalIgnoreCase),
            timeout,
            Math.Max(64, maxResponseBytes),
            NormalizeAuthoritySet(requiredOutputProperties));
    }

    public static ProviderNativeToolDescriptor ProviderNative(
        CapabilityKey key,
        RuntimeToolName runtimeToolName,
        ImplementationKey implementationKey,
        IEnumerable<CapabilityTag> tags,
        IEnumerable<CapabilityOperationClassification> operationClassifications,
        CapabilitySideEffectProfile sideEffectProfile)
    {
        return new ProviderNativeToolDescriptor(
            Identity(key),
            runtimeToolName,
            implementationKey,
            NormalizeTags(tags),
            NormalizeClassifications(operationClassifications),
            sideEffectProfile);
    }

    private static CapabilityIdentity Identity(CapabilityKey key)
        => new(CapabilityKind.Tool, key);

    private static IReadOnlySet<CapabilityTag> NormalizeTags(IEnumerable<CapabilityTag> tags)
        => tags.ToHashSet();

    private static IReadOnlySet<CapabilityOperationClassification> NormalizeClassifications(
        IEnumerable<CapabilityOperationClassification> classifications)
        => classifications.ToHashSet();

    private static IReadOnlyList<string> PreserveSequence(IEnumerable<string> values)
        => values.ToArray();

    private static IReadOnlySet<string> NormalizeAuthoritySet(IEnumerable<string> values)
        => values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.Ordinal);

    private static string RequireText(string value, string parameterName)
        => string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Value is required.", parameterName)
            : value.Trim();

    private static string RequireDataValue(string value, string parameterName)
        => string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Value is required.", parameterName)
            : value;
}
