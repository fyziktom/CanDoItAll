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
        if (!request.Headers.Contains(SharedProviderHeaders.AccessContextReference) &&
            !request.Headers.Contains(SharedProviderHeaders.AccessContextReferenceType) &&
            httpContextAccessor.HttpContext?.RequestServices
                .GetService<IAccessContextReferenceAccessor>() is
                { Current: { } accessContextReference } accessor)
        {
            request.Headers.TryAddWithoutValidation(
                SharedProviderHeaders.AccessContextReference,
                accessContextReference.Value);
            if (accessor.CurrentType is { } type)
            {
                request.Headers.TryAddWithoutValidation(
                    SharedProviderHeaders.AccessContextReferenceType,
                    type.Value);
            }
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
