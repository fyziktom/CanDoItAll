using CanDoItAll.Mcp.Core.Hosting;
using CanDoItAll.Mcp.Core.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;

namespace CanDoItAll.Mcp.ProjectStructure;

internal static class Program
{
    public static async Task Main(string[] args)
    {
        var settingsPath = ResolveSettingsPath(args);

        var builder = Host.CreateEmptyApplicationBuilder(settings: null);

        builder.Configuration.AddCanDoItAllMcpSettings(settingsPath);

        builder.Logging.ConfigureCanDoItAllMcpStdioLogging();

        builder.Services
            .AddValidatedCanDoItAllMcpOptions<McpServerOptions, McpServerOptionsValidator>(builder.Configuration);

        builder.Services.AddHttpClient<ProjectStructureHttpClient>();
        builder.Services.AddSingleton<ServerInstanceIdentity>();
        builder.Services.AddSingleton<RuntimeConfiguration>();
        builder.Services.AddSingleton<IProjectStructureCoordinator, ProjectStructureCoordinator>();

        builder.Services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithTools<ProjectStructureTools>();

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

        return "CanDoItAll.Mcp.ProjectStructure.settings.local.json";
    }
}
