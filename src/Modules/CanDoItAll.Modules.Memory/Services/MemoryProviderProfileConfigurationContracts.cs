using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.Modules.Memory.Services;

public interface IMemoryProviderProfileConfigurationService
{
    Task<IReadOnlyList<MemoryProviderProfileConfigurationSnapshot>> ListAsync(
        CancellationToken cancellationToken = default);

    Task<MemoryProviderProfileConfigurationSnapshot?> GetAsync(
        MemoryProviderInstanceId providerId,
        CancellationToken cancellationToken = default);

    Task<MemoryProviderProfileConfigurationSnapshot> SaveAsync(
        MemoryProviderInstanceId providerId,
        MemoryProviderProfileConfiguration configuration,
        CancellationToken cancellationToken = default);
}

public sealed record MemoryProviderProfileConfigurationSnapshot(
    MemoryProviderProfile Profile,
    MemoryProviderProfileConfiguration Configuration);

public sealed record MemoryProviderProfileConfiguration(
    string DisplayName,
    MemoryProviderDriverKind DriverKind,
    bool IsEnabled,
    MemoryProviderFallbackBehavior FallbackBehavior,
    string ProviderKind,
    IReadOnlyList<string> SelectionTags,
    MemoryProviderProfileCapabilityConfiguration Capabilities,
    MemoryProviderHttpTransportConfiguration? Http,
    MemoryProviderMcpTransportConfiguration? Mcp);

public sealed record MemoryProviderProfileCapabilityConfiguration(
    bool SupportsSynchronousQueries,
    bool SupportsAsynchronousQueries,
    bool SupportsOperationStatus);

public sealed record MemoryProviderHttpTransportConfiguration(
    string BaseUrl,
    string QueryPath,
    string HealthPath,
    string ApiKeyEnvironmentVariable,
    string AuthHeaderName,
    string AuthScheme,
    int TimeoutMilliseconds,
    int MaxRetryAttempts);

public sealed record MemoryProviderMcpTransportConfiguration(
    string DescriptorKind,
    string ServerKey,
    string DisplayName,
    string Description,
    string RemoteEndpoint,
    string AuthHeaderName,
    string AuthHeaderEnvironmentVariable,
    string ContextQueryTool,
    string OperationStatusTool);

public sealed class MemoryProviderProfileConfigurationException : Exception
{
    public MemoryProviderProfileConfigurationException(string message)
        : base(message)
    {
    }

    public MemoryProviderProfileConfigurationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
