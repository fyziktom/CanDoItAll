using CanDoItAll.SharedProviders.Abstractions;
using CanDoItAll.AgentFramework.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.SharedProviders.Http;

public static class SharedProviderHttpServiceCollectionExtensions
{
    public static IServiceCollection AddSharedProviderHttpDescriptors(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<ISharedProviderSourceUriPolicy, SharedProviderSourceUriPolicy>();
        services.TryAddSingleton<ISharedProviderHostAddressResolver, SystemSharedProviderHostAddressResolver>();
        services.TryAddSingleton<ISharedProviderSocketConnector, SystemSharedProviderSocketConnector>();
        AddCatalogClient(
            services,
            SharedProviderCatalogClient.PublicClientName,
            SharedProviderDestinationAccess.PublicOnly);
        AddCatalogClient(
            services,
            SharedProviderCatalogClient.TrustedNetworkClientName,
            SharedProviderDestinationAccess.TrustedNetwork);
        AddCatalogClient(
            services,
            SharedProviderCatalogClient.PrivateHttpClientName,
            SharedProviderDestinationAccess.ApprovedPrivateOnly);
        services.TryAddScoped<ISharedProviderCatalogClient, SharedProviderCatalogClient>();
        services.TryAddSingleton<ISharedProviderRelaySupportCatalog, SharedProviderRelaySupportCatalog>();
        services.TryAddSingleton<ISharedProviderRelayRequestPolicy, SharedProviderRelayRequestPolicy>();
        services.AddHttpClient(SharedProviderHttpRelayClient.ClientName, client =>
            {
                client.Timeout = Timeout.InfiniteTimeSpan;
            })
            .RemoveAllLoggers()
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                UseCookies = false,
                ConnectTimeout = SharedProviderRelayTimeouts.Connect
            });
        services.TryAddScoped<SharedProviderHttpRelayClient>();
        services.TryAddSingleton<
            IProviderInferenceRelayTransport,
            SharedProviderInferenceRelayTransport>();
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<ISharedProviderRelayAdapter, SharedProviderOpenAiRelayAdapter>());
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<ISharedProviderRelayAdapter, SharedProviderOllamaRelayAdapter>());
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<ISharedProviderRelayAdapter, SharedProviderComfyUiRelayAdapter>());
        services.TryAddScoped<SharedProviderRelayAdapterRegistry>();
        services.TryAddScoped<ISharedProviderRelayDispatcher, SharedProviderRelayDispatcher>();
        return services;
    }

    private static void AddCatalogClient(
        IServiceCollection services,
        string clientName,
        SharedProviderDestinationAccess access)
        => services.AddHttpClient(clientName, client =>
            {
                client.Timeout = Timeout.InfiniteTimeSpan;
            })
            .RemoveAllLoggers()
            .ConfigurePrimaryHttpMessageHandler(serviceProvider =>
                SharedProviderSourceHttpHandlerFactory.Create(
                    access,
                    serviceProvider.GetRequiredService<ISharedProviderHostAddressResolver>(),
                    serviceProvider.GetRequiredService<ISharedProviderSocketConnector>()));
}

internal static class SharedProviderRelayTimeouts
{
    public static TimeSpan Connect { get; } = TimeSpan.FromSeconds(10);

    public static TimeSpan StreamingIdle { get; } = TimeSpan.FromSeconds(30);
}
