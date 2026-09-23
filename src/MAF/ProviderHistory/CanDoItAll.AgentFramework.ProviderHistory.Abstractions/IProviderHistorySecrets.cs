namespace CanDoItAll.AgentFramework.ProviderHistory;

public interface IProviderHistorySecrets {
    Task<IReadOnlyList<string>> GetKnownSecretsAsync(ProviderIdentity provider, CancellationToken cancellationToken);
}
