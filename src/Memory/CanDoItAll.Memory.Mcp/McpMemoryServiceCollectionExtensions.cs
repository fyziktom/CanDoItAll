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
        services.TryAddScoped<McpMemoryProviderDriver>();
        services.AddScoped<IMemoryProviderDriver>(provider =>
            provider.GetRequiredService<McpMemoryProviderDriver>());
        services.AddScoped<IMemoryProviderOperationStatusDriver>(provider =>
            provider.GetRequiredService<McpMemoryProviderDriver>());

        return services;
    }
}
