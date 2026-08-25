using CanDoItAll.SharedProviders.Abstractions;
using CanDoItAll.SharedProviders.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Composition;

internal sealed class SharedProviderRuntimeAccessContextHandler(
    IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (!request.Headers.Contains(
                SharedProviderHeaders.AccessContextReference) &&
            httpContextAccessor.HttpContext?.RequestServices
                .GetService<IAccessContextReferenceAccessor>()?.Current is
                { } accessContextReference)
        {
            request.Headers.TryAddWithoutValidation(
                SharedProviderHeaders.AccessContextReference,
                accessContextReference.Value);
        }

        return base.SendAsync(request, cancellationToken);
    }
}

internal static class SharedProviderRuntimeAccessContextServiceCollectionExtensions
{
    public static IServiceCollection
        AddSharedProviderRuntimeAccessContextPropagation(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddHttpContextAccessor();
        services.TryAddTransient<SharedProviderRuntimeAccessContextHandler>();
        AddHandler(
            services,
            SharedProviderCatalogClient.PublicClientName);
        AddHandler(
            services,
            SharedProviderCatalogClient.TrustedNetworkClientName);
        AddHandler(
            services,
            SharedProviderCatalogClient.PrivateHttpClientName);
        return services;
    }

    private static void AddHandler(
        IServiceCollection services,
        string clientName)
        => services.AddHttpClient(clientName)
            .AddHttpMessageHandler<SharedProviderRuntimeAccessContextHandler>();
}
