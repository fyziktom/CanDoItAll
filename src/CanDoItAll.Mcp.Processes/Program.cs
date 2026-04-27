using CanDoItAll.Composition;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.DependencyInjection;
using CanDoItAll.Mcp.Core.Hosting;
using CanDoItAll.Mcp.Core.Identity;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;

namespace CanDoItAll.Mcp.Processes;

internal static class Program
{
    public static async Task Main(string[] args)
    {
        var settingsPath = ResolveSettingsPath(args);

        var builder = Host.CreateEmptyApplicationBuilder(settings: null);

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [LocalRuntimeHostedWorkerPolicy.LaneKindConfigurationKey] = LocalRuntimeHostedWorkerPolicy.McpToolHostLaneKind
        });
        builder.Configuration.AddCanDoItAllMcpSettings(settingsPath);

        builder.Logging.ConfigureCanDoItAllMcpStdioLogging();

        builder.Services
            .AddValidatedCanDoItAllMcpOptions<McpServerOptions, McpServerOptionsValidator>(builder.Configuration);

        builder.Services.AddSingleton<ServerInstanceIdentity>();
        builder.Services.AddSingleton<RuntimeConfiguration>();
        builder.Services.AddCanDoItAllInfrastructure(builder.Configuration, builder.Environment, ModuleAssemblies.All);
        builder.Services.AddCanDoItAllRuntimeDatabaseSwitching();
        builder.Services.AddCanDoItAllRuntimeModules(builder.Configuration);
        builder.Services.AddSingleton<IProcessesCoordinator, ProcessesCoordinator>();

        builder.Services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithTools<ProcessesTools>();

        using var host = builder.Build();

        var runtimeConfiguration = host.Services.GetRequiredService<RuntimeConfiguration>();
        if (runtimeConfiguration.EnsureCurrentProfileReadyOnStartup)
        {
            await host.Services.GetRequiredService<IAppDatabaseBootstrapper>().EnsureCurrentProfileReadyAsync();
        }

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

        return "CanDoItAll.Mcp.Processes.settings.json";
    }
}
