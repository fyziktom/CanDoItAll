using System.Diagnostics.CodeAnalysis;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using CanDoItAll.SharedProviders.Http;

namespace CanDoItAll.Composition;

using AgentFrameworkProviderProfile =
    CanDoItAll.AgentFramework.Models.ProviderProfile;
using AgentFrameworkProviderKind =
    CanDoItAll.AgentFramework.Models.ProviderKind;

internal sealed class SharedProviderRuntimeHttpClientSelector(
    IHttpClientFactory httpClientFactory) : IProviderHttpClientSelector,
    IDisposable
{
    private readonly Lazy<HttpClient> publicClient = new(
        () => httpClientFactory.CreateClient(
            SharedProviderCatalogClient.PublicClientName));
    private readonly Lazy<HttpClient> trustedNetworkClient = new(
        () => httpClientFactory.CreateClient(
            SharedProviderCatalogClient.TrustedNetworkClientName));
    private readonly Lazy<HttpClient> privateHttpClient = new(
        () => httpClientFactory.CreateClient(
            SharedProviderCatalogClient.PrivateHttpClientName));

    public bool TryGetClient(
        AgentFrameworkProviderProfile provider,
        [NotNullWhen(true)] out HttpClient? client)
    {
        ArgumentNullException.ThrowIfNull(provider);
        var hasSharedConnector = string.Equals(
                provider.ConnectorPluginKey,
                SharedProviderReconciliationCoordinator
                    .ImportedConnectorPluginKey,
                StringComparison.Ordinal);
        var hasSharedCredential = provider.CredentialBinding?.Purpose ==
            ProviderCredentialPurpose.SourceAccessToken;
        var hasSharedNetworkPolicy = provider.NetworkAccessPolicy !=
            ProviderNetworkAccessPolicy.Default;
        if (!hasSharedConnector &&
            !hasSharedCredential &&
            !hasSharedNetworkPolicy)
        {
            client = null;
            return false;
        }

        if (!hasSharedConnector ||
            !HasValidSharedRuntimeBinding(provider) ||
            !Uri.TryCreate(provider.BaseUrl, UriKind.Absolute, out var baseUri))
        {
            throw CreateSelectionException(provider);
        }

        client = baseUri.Scheme == Uri.UriSchemeHttp
            ? provider.NetworkAccessPolicy ==
                ProviderNetworkAccessPolicy.AllowPrivateNetwork
                ? privateHttpClient.Value
                : throw CreateSelectionException(provider)
            : baseUri.Scheme == Uri.UriSchemeHttps
                ? provider.NetworkAccessPolicy switch
                {
                    ProviderNetworkAccessPolicy.PublicOnly => publicClient.Value,
                    ProviderNetworkAccessPolicy.AllowPrivateNetwork =>
                        trustedNetworkClient.Value,
                    _ => throw CreateSelectionException(provider)
                }
                : throw CreateSelectionException(provider);
        return true;
    }

    public void Dispose()
    {
        DisposeIfCreated(publicClient);
        DisposeIfCreated(trustedNetworkClient);
        DisposeIfCreated(privateHttpClient);
    }

    private static bool HasValidSharedRuntimeBinding(
        AgentFrameworkProviderProfile provider)
    {
        return provider.IsEnabled &&
            provider.Kind == AgentFrameworkProviderKind.OpenAi &&
            provider.CredentialBinding is
            {
                Purpose: ProviderCredentialPurpose.SourceAccessToken,
                ConsumerKind: ProviderCredentialConsumerKind.Source
            } binding &&
            binding.SecretId != Guid.Empty &&
            binding.ConsumerId != Guid.Empty &&
            provider.ModelSelectionConstraint is { } modelConstraint &&
            modelConstraint.Allows(provider.DefaultModel) &&
            provider.NetworkAccessPolicy is
                ProviderNetworkAccessPolicy.PublicOnly or
                ProviderNetworkAccessPolicy.AllowPrivateNetwork;
    }

    private static ProviderHttpClientSelectionException
        CreateSelectionException(AgentFrameworkProviderProfile provider)
        => new(
            provider.Id,
            "The shared-provider runtime HTTP binding is invalid.");

    private static void DisposeIfCreated(Lazy<HttpClient> client)
    {
        if (client.IsValueCreated)
        {
            client.Value.Dispose();
        }
    }
}
