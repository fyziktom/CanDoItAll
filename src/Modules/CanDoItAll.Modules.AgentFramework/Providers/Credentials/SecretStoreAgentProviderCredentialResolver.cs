using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Security.Abstractions;
using Microsoft.Extensions.Configuration;

namespace CanDoItAll.Modules.AgentFramework;

using AgentFrameworkProviderProfile = CanDoItAll.AgentFramework.Models.ProviderProfile;

internal sealed class SecretStoreAgentProviderCredentialResolver(
    ISecretRuntimeResolver secretResolver,
    IConfiguration configuration) :
    IAgentProviderCredentialResolver,
    IAgentProviderCredentialDispatchScopeFactory
{
    private readonly AsyncLocal<CredentialDispatchScopeState?> currentScope =
        new();

    public ProviderCredentialResolution Resolve(
        AgentFrameworkProviderProfile provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        var scope = currentScope.Value;
        return scope is null
            ? ResolveUnscoped(provider)
            : scope.Resolve(provider);
    }

    public async ValueTask<IAgentProviderCredentialDispatchScopePreparation>
        PrepareAsync(
        IReadOnlyList<AgentFrameworkProviderProfile> providers,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(providers);
        if (providers.Count == 0)
        {
            throw new ArgumentException(
                "At least one provider is required for a credential dispatch scope.",
                nameof(providers));
        }

        cancellationToken.ThrowIfCancellationRequested();
        var entries = new Dictionary<Guid, CredentialDispatchEntry>();
        try
        {
            foreach (var provider in providers)
            {
                ArgumentNullException.ThrowIfNull(provider);
                cancellationToken.ThrowIfCancellationRequested();
                var fingerprint =
                    ProviderConfigurationFingerprintFactory.Create(provider);
                if (entries.TryGetValue(provider.Id, out var existing))
                {
                    if (existing.ConfigurationFingerprint != fingerprint)
                    {
                        throw CreateFingerprintMismatchException(provider.Id);
                    }

                    continue;
                }

                var resolution = await ResolveCoreAsync(
                        provider,
                        cancellationToken)
                    .ConfigureAwait(false);
                entries.Add(
                    provider.Id,
                    new CredentialDispatchEntry(
                        fingerprint,
                        resolution));
            }

            cancellationToken.ThrowIfCancellationRequested();
            return new CredentialDispatchScopePreparation(this, entries);
        }
        catch
        {
            entries.Clear();
            throw;
        }
    }

    private ProviderCredentialResolution ResolveUnscoped(
        AgentFrameworkProviderProfile provider)
    {
        return Task.Run(
                async () => await ResolveCoreAsync(
                        provider,
                        CancellationToken.None)
                    .ConfigureAwait(false))
            .GetAwaiter()
            .GetResult();
    }

    private async ValueTask<ProviderCredentialResolution> ResolveCoreAsync(
        AgentFrameworkProviderProfile provider,
        CancellationToken cancellationToken)
    {
        if (provider.CredentialBinding is { } credentialBinding)
        {
            return await ResolveBoundCredentialAsync(
                    provider,
                    credentialBinding,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var secretRecordId =
            AgentFrameworkProviderMetadata.ResolveSecretRecordId(provider);
        if (secretRecordId.HasValue)
        {
            try
            {
                var secretValue = await secretResolver.ResolveValueAsync(
                        new SecretRuntimeRequest(
                            secretRecordId.Value,
                            SecretRuntimePurposes.AgentProviderApiKey),
                        cancellationToken)
                    .ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(secretValue))
                {
                    return new ProviderCredentialResolution(
                        string.Empty,
                        $"secret record '{secretRecordId.Value:D}'",
                        $"Secret record '{secretRecordId.Value:D}' was not found or did not contain a usable value.");
                }

                return new ProviderCredentialResolution(
                    secretValue,
                    $"secret record '{secretRecordId.Value:D}'",
                    string.Empty);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                return new ProviderCredentialResolution(
                    string.Empty,
                    $"secret record '{secretRecordId.Value:D}'",
                    $"Secret record '{secretRecordId.Value:D}' could not be resolved ({exception.GetType().Name}).");
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(provider.ApiKeyEnvironmentVariable))
        {
            return new ProviderCredentialResolution(
                string.Empty,
                "not configured",
                "No secret record or API key environment variable is configured for this provider.");
        }

        var variableName = provider.ApiKeyEnvironmentVariable.Trim();
        var value =
            AgentProviderEnvironmentCredential.Resolve(variableName);
        if (!string.IsNullOrWhiteSpace(value))
        {
            return new ProviderCredentialResolution(
                value,
                $"environment variable '{variableName}'",
                string.Empty);
        }

        var configuredValue = configuration[variableName];
        if (!string.IsNullOrWhiteSpace(configuredValue))
        {
            return new ProviderCredentialResolution(
                configuredValue.Trim(),
                $"application configuration key '{variableName}'",
                string.Empty);
        }

        return new ProviderCredentialResolution(
            string.Empty,
            $"environment variable '{variableName}'",
            $"Environment variable '{variableName}' is not set and application configuration key '{variableName}' is empty. {AgentProviderEnvironmentCredential.DescribePresence(variableName)}");
    }

    private async ValueTask<ProviderCredentialResolution>
        ResolveBoundCredentialAsync(
        AgentFrameworkProviderProfile provider,
        ProviderCredentialBinding binding,
        CancellationToken cancellationToken)
    {
        if (binding.SecretId == Guid.Empty ||
            binding.ConsumerId == Guid.Empty ||
            !Enum.IsDefined(binding.Purpose) ||
            !Enum.IsDefined(binding.ConsumerKind))
        {
            return new ProviderCredentialResolution(
                string.Empty,
                "invalid provider credential binding",
                "The provider credential binding is invalid.");
        }

        SecretRuntimeRequest request;
        try
        {
            request = CreateSecretRuntimeRequest(provider, binding);
        }
        catch (InvalidOperationException)
        {
            return new ProviderCredentialResolution(
                string.Empty,
                "invalid provider credential binding",
                "The provider credential binding is incompatible with the provider profile.");
        }

        var protectsSecretReference = binding.Purpose ==
            ProviderCredentialPurpose.SourceAccessToken;
        var resolutionSource = protectsSecretReference
            ? "source access credential"
            : $"secret record '{binding.SecretId:D}'";
        try
        {
            var secretValue = await secretResolver.ResolveValueAsync(
                    request,
                    cancellationToken)
                .ConfigureAwait(false);
            return string.IsNullOrWhiteSpace(secretValue)
                ? new ProviderCredentialResolution(
                    string.Empty,
                    resolutionSource,
                    protectsSecretReference
                        ? "The source access credential was unavailable."
                        : $"Secret record '{binding.SecretId:D}' was not found or did not contain a usable value.")
                : new ProviderCredentialResolution(
                    secretValue,
                    resolutionSource,
                    string.Empty);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return new ProviderCredentialResolution(
                string.Empty,
                resolutionSource,
                protectsSecretReference
                    ? "The source access credential could not be resolved."
                    : $"Secret record '{binding.SecretId:D}' could not be resolved ({exception.GetType().Name}).");
        }
    }

    private static SecretRuntimeRequest CreateSecretRuntimeRequest(
        AgentFrameworkProviderProfile provider,
        ProviderCredentialBinding binding)
    {
        return (binding.Purpose, binding.ConsumerKind) switch
        {
            (ProviderCredentialPurpose.ProviderApiKey,
                ProviderCredentialConsumerKind.ProviderProfile)
                when binding.ConsumerId == provider.Id =>
                new SecretRuntimeRequest(
                    binding.SecretId,
                    SecretRuntimePurposes.AgentProviderApiKey,
                    AllowedSecretIds: [binding.SecretId],
                    ConsumerType: SecretRuntimeConsumerTypes.ProviderProfile,
                    ConsumerId: SecretRuntimeConsumerIds.ProviderProfile(
                        binding.ConsumerId)),
            (ProviderCredentialPurpose.SourceAccessToken,
                ProviderCredentialConsumerKind.Source) =>
                new SecretRuntimeRequest(
                    binding.SecretId,
                    SecretRuntimePurposes.SharedProviderSourceToken,
                    AllowedSecretIds: [binding.SecretId],
                    ConsumerType: SecretRuntimeConsumerTypes.SharedProviderSource,
                    ConsumerId: SecretRuntimeConsumerIds.SharedProviderSource(
                        binding.ConsumerId)),
            _ => throw new InvalidOperationException(
                "The provider credential binding is incompatible with the provider profile.")
        };
    }

    private IAgentProviderCredentialDispatchScope BeginScope(
        Dictionary<Guid, CredentialDispatchEntry> entries)
    {
        var previous = currentScope.Value;
        var state = new CredentialDispatchScopeState(entries);
        currentScope.Value = state;
        return new CredentialDispatchScope(this, state, previous);
    }

    private void EndScope(
        CredentialDispatchScopeState state,
        CredentialDispatchScopeState? previous)
    {
        state.Dispose();
        if (ReferenceEquals(currentScope.Value, state))
        {
            currentScope.Value = previous;
        }
    }

    private readonly record struct CredentialDispatchEntry(
        ProviderConfigurationFingerprint ConfigurationFingerprint,
        ProviderCredentialResolution Resolution);

    private sealed class CredentialDispatchScopePreparation :
        IAgentProviderCredentialDispatchScopePreparation
    {
        private readonly object gate = new();
        private SecretStoreAgentProviderCredentialResolver? owner;
        private Dictionary<Guid, CredentialDispatchEntry>? entries;

        public CredentialDispatchScopePreparation(
            SecretStoreAgentProviderCredentialResolver owner,
            Dictionary<Guid, CredentialDispatchEntry> entries)
        {
            this.owner = owner;
            this.entries = entries;
        }

        public IAgentProviderCredentialDispatchScope BeginScope()
        {
            SecretStoreAgentProviderCredentialResolver preparedOwner;
            Dictionary<Guid, CredentialDispatchEntry> preparedEntries;
            lock (gate)
            {
                preparedOwner = owner
                    ?? throw new ObjectDisposedException(
                        nameof(CredentialDispatchScopePreparation));
                preparedEntries = entries
                    ?? throw new ObjectDisposedException(
                        nameof(CredentialDispatchScopePreparation));
                owner = null;
                entries = null;
            }

            return preparedOwner.BeginScope(preparedEntries);
        }

        public void Dispose()
        {
            lock (gate)
            {
                owner = null;
                entries?.Clear();
                entries = null;
            }
        }

        public override string ToString()
        {
            return nameof(CredentialDispatchScopePreparation);
        }
    }

    private sealed class CredentialDispatchScope(
        SecretStoreAgentProviderCredentialResolver owner,
        CredentialDispatchScopeState state,
        CredentialDispatchScopeState? previous) :
        IAgentProviderCredentialDispatchScope
    {
        private SecretStoreAgentProviderCredentialResolver? owner = owner;

        public ProviderCredentialResolution Resolve(
            AgentFrameworkProviderProfile provider)
        {
            return state.Resolve(provider);
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref owner, null)
                ?.EndScope(state, previous);
        }

        public override string ToString()
        {
            return nameof(CredentialDispatchScope);
        }
    }

    private sealed class CredentialDispatchScopeState(
        Dictionary<Guid, CredentialDispatchEntry> entries) :
        IDisposable
    {
        private Dictionary<Guid, CredentialDispatchEntry>? entries = entries;
        private int disposed;

        public ProviderCredentialResolution Resolve(
            AgentFrameworkProviderProfile provider)
        {
            ArgumentNullException.ThrowIfNull(provider);
            ThrowIfDisposed();
            var currentEntries = Volatile.Read(ref entries)
                ?? throw new ObjectDisposedException(
                    nameof(CredentialDispatchScopeState));
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

            ThrowIfDisposed();
            return entry.Resolution;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0)
            {
                return;
            }

            Interlocked.Exchange(ref entries, null)?.Clear();
        }

        private void ThrowIfDisposed()
        {
            if (Volatile.Read(ref disposed) != 0)
            {
                throw new ObjectDisposedException(
                    nameof(CredentialDispatchScopeState));
            }
        }

        public override string ToString()
        {
            return nameof(CredentialDispatchScopeState);
        }
    }

    private static InvalidOperationException
        CreateFingerprintMismatchException(Guid providerId)
    {
        return new(
            $"Provider '{providerId:N}' changed configuration within one credential dispatch.");
    }
}
