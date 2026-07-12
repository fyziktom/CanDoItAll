using CanDoItAll.Memory.Application;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Memory.Http;

public static class HttpMemoryServiceCollectionExtensions
{
    public static IServiceCollection AddHttpMemoryProviderDriver(
        this IServiceCollection services,
        Action<HttpMemoryProviderOptions>? configure = null)
    {
        var options = new HttpMemoryProviderOptions();
        configure?.Invoke(options);
        options.Validate();

        services.AddSingleton(options);
        services.AddHttpClient(options.ClientName, client =>
        {
            client.Timeout = Timeout.InfiniteTimeSpan;
        });
        services.TryAddSingleton<HttpMemoryProviderDriver>();
        services.AddSingleton<IMemoryProviderDriver>(provider =>
            provider.GetRequiredService<HttpMemoryProviderDriver>());
        services.AddSingleton<IMemoryProviderHealthDriver>(provider =>
            provider.GetRequiredService<HttpMemoryProviderDriver>());

        return services;
    }

    public static IServiceCollection AddNativeRemoteMemoryProviderDriver(
        this IServiceCollection services,
        Action<NativeRemoteMemoryProviderOptions>? configure = null)
    {
        var options = new NativeRemoteMemoryProviderOptions();
        configure?.Invoke(options);
        options.Validate();

        services.AddSingleton(options);
        services.AddHttpClient(options.ClientName, client =>
        {
            client.Timeout = Timeout.InfiniteTimeSpan;
        });
        services.TryAddSingleton<NativeRemoteMemoryProviderDriver>();
        services.AddSingleton<IMemoryProviderDriver>(provider =>
            provider.GetRequiredService<NativeRemoteMemoryProviderDriver>());
        services.AddSingleton<IMemoryProviderHealthDriver>(provider =>
            provider.GetRequiredService<NativeRemoteMemoryProviderDriver>());

        return services;
    }
}
