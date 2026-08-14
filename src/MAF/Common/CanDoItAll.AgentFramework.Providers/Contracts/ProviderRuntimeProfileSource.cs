using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Providers;

public interface IProviderRuntimeProfileSource
{
    Task<IReadOnlyList<ProviderProfile>> ListProvidersAsync(
        CancellationToken cancellationToken = default);

    Task<ProviderProfile?> GetProviderAsync(
        Guid providerId,
        CancellationToken cancellationToken = default);
}
