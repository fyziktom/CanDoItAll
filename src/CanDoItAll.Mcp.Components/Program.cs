using CanDoItAll.Mcp.Components.Catalog;
using CanDoItAll.Mcp.Components.Configuration;
using CanDoItAll.Mcp.Components.Tools;
using CanDoItAll.Mcp.Core.Hosting;
using CanDoItAll.Mcp.Core.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;
using ComponentsMcpServerOptions = CanDoItAll.Mcp.Components.Configuration.McpServerOptions;

namespace CanDoItAll.Mcp.Components;

internal static class Program
{
    public static async Task Main(string[] args)
    {
        var settingsPath = ResolveSettingsPath(args);

        var builder = Host.CreateEmptyApplicationBuilder(settings: null);

        builder.Configuration.AddCanDoItAllMcpSettings(settingsPath);

        builder.Logging.ConfigureCanDoItAllMcpStdioLogging();

        builder.Services
            .AddValidatedCanDoItAllMcpOptions<ComponentsMcpServerOptions, McpServerOptionsValidator>(builder.Configuration);

        builder.Services.AddCanDoItAllMcpIdleShutdown<ComponentsMcpServerOptions>(options => options.Server.IdleShutdown);

        builder.Services.AddSingleton<ServerInstanceIdentity>();
        builder.Services.AddSingleton<ComponentCatalogService>();

        builder.Services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithTools<ComponentsTools>();

        using var host = builder.Build();
        await host.RunAsync();
    }

    private static string ResolveSettingsPath(string[] args)
    {
        for (var index = 0; index < args.Length - 1; index++)
        {
            if (string.Equals(args[index], "--settings", StringComparison.OrdinalIgnoreCase))
            {
                return args[index + 1];
            }
        }

        return "CanDoItAll.Mcp.Components.settings.json";
    }
}
