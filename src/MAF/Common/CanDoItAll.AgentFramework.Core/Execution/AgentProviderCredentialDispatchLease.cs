using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

internal sealed class AgentProviderCredentialDispatchScopeFactory(
    IAgentProviderCredentialResolver credentialResolver)
{
    public async ValueTask<AgentProviderCredentialDispatchLease> PrepareAsync(
        IReadOnlyList<ProviderProfile> providers,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(providers);
        if (providers.Count == 0)
        {
            throw new ArgumentException(
                "At least one provider is required for a credential dispatch scope.",
                nameof(providers));
        }

        cancellationToken.ThrowIfCancellationRequested();
        IAgentProviderCredentialDispatchScopePreparation preparation =
            credentialResolver is IAgentProviderCredentialDispatchScopeFactory factory
                ? await factory
                    .PrepareAsync(providers, cancellationToken)
                    .ConfigureAwait(false)
                : DirectCredentialDispatchScopePreparation.Create(
                    credentialResolver,
                    providers,
                    cancellationToken);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new AgentProviderCredentialDispatchLease(preparation);
        }
        catch
        {
            preparation.Dispose();
            throw;
        }
    }

    private readonly record struct DirectCredentialDispatchEntry(
        ProviderConfigurationFingerprint ConfigurationFingerprint,
        ProviderCredentialResolution Resolution);

    private sealed class DirectCredentialDispatchScopePreparation :
        IAgentProviderCredentialDispatchScopePreparation
    {
        private Dictionary<Guid, DirectCredentialDispatchEntry>? entries;

        private DirectCredentialDispatchScopePreparation(
            Dictionary<Guid, DirectCredentialDispatchEntry> entries)
        {
            this.entries = entries;
        }

        public static DirectCredentialDispatchScopePreparation Create(
            IAgentProviderCredentialResolver credentialResolver,
            IReadOnlyList<ProviderProfile> providers,
            CancellationToken cancellationToken)
        {
            var entries =
                new Dictionary<Guid, DirectCredentialDispatchEntry>();
            try
            {
                foreach (var provider in providers)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var fingerprint =
                        ProviderConfigurationFingerprintFactory.Create(
                            provider);
                    if (entries.TryGetValue(provider.Id, out var existing))
                    {
                        if (existing.ConfigurationFingerprint != fingerprint)
                        {
                            throw CreateFingerprintMismatchException(
                                provider.Id);
                        }

                        continue;
                    }

                    entries.Add(
                        provider.Id,
                        new DirectCredentialDispatchEntry(
                            fingerprint,
                            credentialResolver.Resolve(provider)));
                }

                return new DirectCredentialDispatchScopePreparation(entries);
            }
            catch
            {
                entries.Clear();
                throw;
            }
        }

        public IAgentProviderCredentialDispatchScope BeginScope()
        {
            var preparedEntries = Interlocked.Exchange(ref entries, null)
                ?? throw new ObjectDisposedException(
                    nameof(DirectCredentialDispatchScopePreparation));
            return new DirectCredentialDispatchScope(preparedEntries);
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref entries, null)?.Clear();
        }

        public override string ToString()
        {
            return nameof(DirectCredentialDispatchScopePreparation);
        }
    }

    private sealed class DirectCredentialDispatchScope(
        Dictionary<Guid, DirectCredentialDispatchEntry> entries) :
        IAgentProviderCredentialDispatchScope
    {
        private Dictionary<Guid, DirectCredentialDispatchEntry>? entries =
            entries;

        public ProviderCredentialResolution Resolve(
            ProviderProfile provider)
        {
            ArgumentNullException.ThrowIfNull(provider);
            var currentEntries = Volatile.Read(ref entries)
                ?? throw new ObjectDisposedException(
                    nameof(DirectCredentialDispatchScope));
            var fingerprint =
                ProviderConfigurationFingerprintFactory.Create(provider);
            if (!currentEntries.TryGetValue(provider.Id, out var entry))
            {
                throw new InvalidOperationException(
                    $"Provider '{provider.Id:N}' was not prepared for the active credential dispatch scope.");
            }

            if (entry.ConfigurationFingerprint != fingerprint)
            {
                throw CreateFingerprintMismatchException(provider.Id);
            }

            return entry.Resolution;
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref entries, null)?.Clear();
        }

        public override string ToString()
        {
            return nameof(DirectCredentialDispatchScope);
        }
    }

    private static InvalidOperationException
        CreateFingerprintMismatchException(Guid providerId)
    {
        return new(
            $"Provider '{providerId:N}' changed configuration within one credential dispatch.");
    }
}

internal sealed class AgentProviderCredentialDispatchLease(
    IAgentProviderCredentialDispatchScopePreparation preparation) :
    IDisposable
{
    private IAgentProviderCredentialDispatchScopePreparation? preparation =
        preparation;

    public IAgentProviderCredentialDispatchScope BeginScope()
    {
        var prepared = Interlocked.Exchange(ref preparation, null)
            ?? throw new ObjectDisposedException(
                nameof(AgentProviderCredentialDispatchLease));
        try
        {
            return prepared.BeginScope();
        }
        finally
        {
            prepared.Dispose();
        }
    }

    public void Dispose()
    {
        Interlocked.Exchange(ref preparation, null)?.Dispose();
    }

    public override string ToString()
    {
        return nameof(AgentProviderCredentialDispatchLease);
    }
}
