using CanDoItAll.Memory.Application;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Memory.Mcp;

public static class McpMemoryServiceCollectionExtensions
{
    public static IServiceCollection AddMcpMemoryProviderDriver(
        this IServiceCollection services,
        Action<McpMemoryProviderOptions>? configure = null)
    {
        var options = new McpMemoryProviderOptions();
        configure?.Invoke(options);
        options.Validate();

        services.AddSingleton(options);
        services.TryAddSingleton<McpMemoryProviderDriver>();
        services.AddSingleton<IMemoryProviderDriver>(provider =>
            provider.GetRequiredService<McpMemoryProviderDriver>());
        services.AddSingleton<IMcpMemoryProviderAdapter>(provider =>
            provider.GetRequiredService<McpMemoryProviderDriver>());
        services.AddSingleton<IMemoryProviderOperationStatusDriver>(provider =>
            provider.GetRequiredService<McpMemoryProviderDriver>());
        services.AddSingleton<IMemoryProviderEventPollDriver>(provider =>
            provider.GetRequiredService<McpMemoryProviderDriver>());

        return services;
    }
}
