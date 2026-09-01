using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.AgentFramework.Providers;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.AgentFramework.Maf;

internal sealed class ProviderHistorySecrets(IServiceScopeFactory scopeFactory) : IProviderHistorySecrets {
    public async Task<IReadOnlyList<string>> GetKnownSecretsAsync(ProviderIdentity providerId, CancellationToken cancellationToken) {
        await using var scope = scopeFactory.CreateAsyncScope();
        var providers = scope.ServiceProvider.GetRequiredService<IProviderRuntimeProfileSource>();
        var credentials = scope.ServiceProvider.GetRequiredService<IAgentProviderCredentialResolver>();
        var provider = await providers.GetProviderAsync(providerId.Value, cancellationToken)
            ?? throw new ProviderHistoryException(HistoryFailure.Unavailable, "The provider credential context is unavailable.");
        var credential = credentials.Resolve(provider);
        if (credential.IsResolved) {
            return [credential.ApiKey];
        }
        if (provider.CredentialBinding is not null || !string.IsNullOrWhiteSpace(provider.ApiKeyEnvironmentVariable)) {
            throw new ProviderHistoryException(HistoryFailure.Unavailable, "The provider secret could not be resolved for redaction.");
        }
        return [];
    }
}