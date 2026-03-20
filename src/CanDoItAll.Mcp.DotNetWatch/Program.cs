using CanDoItAll.Mcp.DotNetWatch.Configuration;
using CanDoItAll.Mcp.DotNetWatch.Diagnostics;
using CanDoItAll.Mcp.DotNetWatch.Health;
using CanDoItAll.Mcp.DotNetWatch.Logging;
using CanDoItAll.Mcp.DotNetWatch.Operations;
using CanDoItAll.Mcp.DotNetWatch.Persistence;
using CanDoItAll.Mcp.DotNetWatch.Processes;
using CanDoItAll.Mcp.DotNetWatch.Runtime;
using CanDoItAll.Mcp.DotNetWatch.Security;
using CanDoItAll.Mcp.DotNetWatch.Tools;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Mcp.DotNetWatch;

internal static class Program
{
    public static async Task Main(string[] args)
    {
        var settingsPath = ResolveSettingsPath(args);

        var builder = Host.CreateEmptyApplicationBuilder(settings: null);

        builder.Configuration.AddJsonFile(settingsPath, optional: false, reloadOnChange: false);
        builder.Configuration.AddEnvironmentVariables(prefix: "CanDoItAllMcp_");

        builder.Logging.ClearProviders();
        builder.Logging.AddConsole(options =>
        {
            options.LogToStandardErrorThreshold = LogLevel.Trace;
        });
        builder.Logging.SetMinimumLevel(LogLevel.Information);

        builder.Services
            .AddOptions<McpServerOptions>()
            .Bind(builder.Configuration)
            .ValidateOnStart();
        builder.Services.AddSingleton<IValidateOptions<McpServerOptions>, McpServerOptionsValidator>();

        builder.Services.AddSingleton<RuntimeConfiguration>();
        builder.Services.AddSingleton<LogRedactor>();
        builder.Services.AddSingleton<FileLogStore>();
        builder.Services.AddSingleton<PathGuard>();
        builder.Services.AddSingleton<EnvironmentOverlayFilter>();
        builder.Services.AddSingleton<StaleProcessRegistry>();
        builder.Services.AddSingleton<IProcessTreeTerminator, ProcessTreeTerminator>();
        builder.Services.AddSingleton<ProcessSupervisor>();
        builder.Services.AddSingleton<HttpHealthProbe>();
        builder.Services.AddSingleton<AppRuntimeManager>();
        builder.Services.AddSingleton<WorkspaceExecutionLock>();
        builder.Services.AddSingleton<OperationRegistry>();
        builder.Services.AddSingleton<StartFailureDiagnoser>();
        builder.Services.AddSingleton<SessionCoordinator>();
        builder.Services.AddHostedService<StartupCleanupHostedService>();

        builder.Services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithTools<CanDoItAllTools>();

        using var host = builder.Build();
        await host.RunAsync();
    }

    private static string ResolveSettingsPath(string[] args)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--settings", StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return "CanDoItAll.Mcp.DotNetWatch.settings.json";
    }
}
