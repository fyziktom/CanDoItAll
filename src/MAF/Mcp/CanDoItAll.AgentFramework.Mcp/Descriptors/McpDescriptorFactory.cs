using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Mcp.Abstractions;

namespace CanDoItAll.AgentFramework.Mcp;

public static class McpDescriptorFactory
{
    public static InternalHostedMcpServerDescriptor InternalHosted(
        CapabilityKey key,
        McpServerKey serverKey,
        string displayName,
        string description,
        ImplementationKey implementationKey,
        IEnumerable<McpToolName> allowedTools,
        McpApprovalMode approvalMode,
        TimeSpan timeout,
        IEnumerable<CapabilityTag>? tags = null,
        IEnumerable<CapabilityOperationClassification>? operationClassifications = null)
    {
        return new InternalHostedMcpServerDescriptor(
            Identity(key),
            serverKey,
            RequireText(displayName, nameof(displayName)),
            RequireText(description, nameof(description)),
            NormalizeTags([CapabilityTag.Create("mcp"), CapabilityTag.Create("internal")], tags),
            NormalizeClassifications(operationClassifications ?? [CapabilityOperationClassification.McpTool]),
            new CapabilitySideEffectProfile(CapabilitySideEffectKind.McpTool, approvalMode == McpApprovalMode.AlwaysRequire, false),
            CapabilityAvailabilityState.Available,
            NormalizeTools(allowedTools),
            approvalMode,
            timeout,
            implementationKey);
    }

    public static LocalStdioMcpServerDescriptor LocalStdio(
        CapabilityKey key,
        McpServerKey serverKey,
        string displayName,
        string description,
        string command,
        IEnumerable<string> arguments,
        string workingDirectory,
        IEnumerable<string> allowedWorkingDirectories,
        IEnumerable<McpToolName> allowedTools,
        IReadOnlyDictionary<string, string> environmentVariableBindings,
        IReadOnlyDictionary<string, string> rawEnvironmentVariables,
        McpApprovalMode approvalMode,
        TimeSpan timeout,
        IEnumerable<CapabilityTag>? tags = null,
        IEnumerable<CapabilityOperationClassification>? operationClassifications = null,
        McpStdioMessageFraming messageFraming = McpStdioMessageFraming.ContentLength)
    {
        return new LocalStdioMcpServerDescriptor(
            Identity(key),
            serverKey,
            RequireText(displayName, nameof(displayName)),
            RequireText(description, nameof(description)),
            NormalizeTags([CapabilityTag.Create("mcp"), CapabilityTag.Create("local"), CapabilityTag.Create("external")], tags),
            NormalizeClassifications(operationClassifications ?? [CapabilityOperationClassification.McpTool, CapabilityOperationClassification.ExternalAction]),
            new CapabilitySideEffectProfile(CapabilitySideEffectKind.LocalProcessExecution, approvalMode == McpApprovalMode.AlwaysRequire, true),
            CapabilityAvailabilityState.Available,
            NormalizeTools(allowedTools),
            approvalMode,
            timeout,
            RequireText(command, nameof(command)),
            NormalizeStrings(arguments),
            RequireText(workingDirectory, nameof(workingDirectory)),
            messageFraming,
            NormalizeStringSet(allowedWorkingDirectories),
            NormalizeDictionary(environmentVariableBindings),
            NormalizeDictionary(rawEnvironmentVariables));
    }

    public static RemoteHttpMcpServerDescriptor RemoteHttp(
        CapabilityKey key,
        McpServerKey serverKey,
        string displayName,
        string description,
        Uri endpoint,
        IEnumerable<McpToolName> allowedTools,
        IReadOnlyDictionary<string, string> headerBindings,
        IReadOnlyDictionary<string, string> rawHeaders,
        McpApprovalMode approvalMode,
        TimeSpan timeout,
        IEnumerable<CapabilityTag>? tags = null,
        IEnumerable<CapabilityOperationClassification>? operationClassifications = null)
    {
        return new RemoteHttpMcpServerDescriptor(
            Identity(key),
            serverKey,
            RequireText(displayName, nameof(displayName)),
            RequireText(description, nameof(description)),
            NormalizeTags([CapabilityTag.Create("mcp"), CapabilityTag.Create("remote"), CapabilityTag.Create("external")], tags),
            NormalizeClassifications(operationClassifications ?? [CapabilityOperationClassification.McpTool, CapabilityOperationClassification.ExternalAction]),
            new CapabilitySideEffectProfile(CapabilitySideEffectKind.ExternalAction, approvalMode == McpApprovalMode.AlwaysRequire, true),
            CapabilityAvailabilityState.Available,
            NormalizeTools(allowedTools),
            approvalMode,
            timeout,
            endpoint,
            NormalizeDictionary(headerBindings),
            NormalizeDictionary(rawHeaders));
    }

    private static CapabilityIdentity Identity(CapabilityKey key)
        => new(CapabilityKind.McpServer, key);

    private static IReadOnlySet<CapabilityTag> NormalizeTags(
        IEnumerable<CapabilityTag> requiredTags,
        IEnumerable<CapabilityTag>? providedTags)
    {
        var tags = requiredTags.ToHashSet();
        foreach (var tag in providedTags ?? [])
        {
            tags.Add(tag);
        }

        return tags;
    }

    private static IReadOnlySet<CapabilityOperationClassification> NormalizeClassifications(
        IEnumerable<CapabilityOperationClassification> classifications)
        => classifications.ToHashSet();

    private static IReadOnlySet<McpToolName> NormalizeTools(IEnumerable<McpToolName> toolNames)
        => toolNames.ToHashSet();

    private static IReadOnlyList<string> NormalizeStrings(IEnumerable<string> values)
        => values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .ToArray();

    private static IReadOnlySet<string> NormalizeStringSet(IEnumerable<string> values)
        => values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static IReadOnlyDictionary<string, string> NormalizeDictionary(IReadOnlyDictionary<string, string> values)
        => values
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && !string.IsNullOrWhiteSpace(pair.Value))
            .ToDictionary(pair => pair.Key.Trim(), pair => pair.Value.Trim(), StringComparer.OrdinalIgnoreCase);

    private static string RequireText(string value, string parameterName)
        => string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Value is required.", parameterName)
            : value.Trim();
}
