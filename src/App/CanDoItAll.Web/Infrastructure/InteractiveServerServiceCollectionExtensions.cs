using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Web.Infrastructure;

public static class InteractiveServerServiceCollectionExtensions
{
    public const long MaximumReceiveMessageBytes = 40L * 1024L * 1024L;

    public static IServiceCollection AddCanDoItAllInteractiveServer(
        this IServiceCollection services,
        bool detailedErrors)
    {
        services.AddRazorComponents()
            .AddInteractiveServerComponents(options => options.DetailedErrors = detailedErrors)
            .AddHubOptions(options => options.MaximumReceiveMessageSize = MaximumReceiveMessageBytes);

        return services;
    }

    public static IServiceCollection AddCanDoItAllLocalOperatorUiAuthentication(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddHttpContextAccessor();
        services.TryAddScoped<LocalOperatorAuthenticationStateProvider>();
        services.Replace(ServiceDescriptor.Scoped<AuthenticationStateProvider>(
            serviceProvider => serviceProvider
                .GetRequiredService<LocalOperatorAuthenticationStateProvider>()));
        services.Replace(ServiceDescriptor.Scoped<IHostEnvironmentAuthenticationStateProvider>(
            serviceProvider => serviceProvider
                .GetRequiredService<LocalOperatorAuthenticationStateProvider>()));
        services.Replace(ServiceDescriptor.Scoped<IInteractiveAccessPrincipalProvider>(
            serviceProvider => serviceProvider
                .GetRequiredService<LocalOperatorAuthenticationStateProvider>()));
        return services;
    }
}
