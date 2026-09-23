namespace CanDoItAll.Security.Abstractions;

public static class SecretRuntimePurposes
{
    public const string AgentProviderApiKey = "agent-provider-api-key";
    public const string AgentMcpEnvironmentVariable = "agent-mcp-environment-variable";
    public const string AgentMcpHeader = "agent-mcp-header";
    public const string StorageCredential = "storage-credential";
    public const string PluginConnectionSecret = "plugin-connection-secret";
    public const string PluginWorkflowExecutorSecret = "plugin-workflow-executor-secret";
    public const string PluginSettingsValidation = "plugin-settings-validation";
    public const string SharedProviderSourceToken = "shared-provider-source-token";
}

public static class SecretRuntimeConsumerTypes
{
    public const string AgentMcp = "agent-mcp";
    public const string ProviderProfile = "provider-profile";
    public const string StorageCredential = "storage-credential";
    public const string WorkflowExecutor = "workflow-executor";
    public const string WorkflowHttpExecutor = "workflow-http";
    public const string Plugin = "plugin";
    public const string PluginConnection = "plugin-connection";
    public const string PluginWorkflowExecutor = "plugin-workflow-executor";
    public const string SharedProviderSource = "shared-provider-source";
}

public static class SecretRuntimeConsumerIds
{
    public static string AgentMcp(Guid agentId, string capabilityName, string bindingName)
        => $"{RequireGuid(agentId, nameof(agentId))}/{RequireSegment(capabilityName, nameof(capabilityName))}/{RequireSegment(bindingName, nameof(bindingName))}";

    public static string ProviderProfile(Guid providerProfileId)
        => RequireGuid(providerProfileId, nameof(providerProfileId)).ToString("D");

    public static string StorageRuntime()
        => "storage-runtime";

    public static string StorageCatalog(Guid storageCatalogId)
        => RequireGuid(storageCatalogId, nameof(storageCatalogId)).ToString("D");

    public static string WorkflowNode(Guid workflowId, string nodeId)
        => $"{RequireGuid(workflowId, nameof(workflowId)):D}/{RequireSegment(nodeId, nameof(nodeId))}";

    public static string PluginConnection(string pluginId, string connectionId)
        => $"{RequireSegment(pluginId, nameof(pluginId))}/{RequireSegment(connectionId, nameof(connectionId))}";

    public static string SharedProviderSource(Guid sourceId)
        => RequireGuid(sourceId, nameof(sourceId)).ToString("D");

    private static Guid RequireGuid(Guid value, string parameterName)
        => value == Guid.Empty
            ? throw new ArgumentException("Identifier is required.", parameterName)
            : value;

    private static string RequireSegment(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Identifier segment is required.", parameterName);
        }

        var normalized = value.Trim();
        if (normalized.Contains('\r', StringComparison.Ordinal) ||
            normalized.Contains('\n', StringComparison.Ordinal) ||
            normalized.Contains('/', StringComparison.Ordinal))
        {
            throw new ArgumentException("Identifier segment cannot contain slashes or line breaks.", parameterName);
        }

        return normalized;
    }
}

public sealed record SecretRuntimeRequest(
    Guid SecretId,
    string Purpose,
    IReadOnlyCollection<Guid>? AllowedSecretIds = null,
    string? ConsumerType = null,
    string? ConsumerId = null);

public interface ISecretRuntimeResolver
{
    Task<string?> ResolveValueAsync(SecretRuntimeRequest request, CancellationToken cancellationToken = default);
}
