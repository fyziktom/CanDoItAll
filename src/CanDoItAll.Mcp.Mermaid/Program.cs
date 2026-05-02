using CanDoItAll.Mcp.Core.Hosting;
using CanDoItAll.Mcp.Core.Identity;
using CanDoItAll.Mcp.Mermaid.Catalog;
using CanDoItAll.Mcp.Mermaid.Configuration;
using CanDoItAll.Mcp.Mermaid.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;
using MermaidMcpServerOptions = CanDoItAll.Mcp.Mermaid.Configuration.McpServerOptions;

namespace CanDoItAll.Mcp.Mermaid;

internal static class Program
{
    public static async Task Main(string[] args)
    {
        var settingsPath = ResolveSettingsPath(args);

        var builder = Host.CreateEmptyApplicationBuilder(settings: null);

        builder.Configuration.AddCanDoItAllMcpSettings(settingsPath);
        builder.Logging.ConfigureCanDoItAllMcpStdioLogging();

        builder.Services
            .AddValidatedCanDoItAllMcpOptions<MermaidMcpServerOptions, McpServerOptionsValidator>(builder.Configuration);

        builder.Services.AddSingleton<ServerInstanceIdentity>();
        builder.Services.AddSingleton<MermaidSyntaxCatalogService>();

        builder.Services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithTools<MermaidTools>();

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

        return "CanDoItAll.Mcp.Mermaid.settings.json";
    }
}
