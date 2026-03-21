using CanDoItAll.Mcp.Core.Concurrency;
using CanDoItAll.Mcp.Core.Identity;
using CanDoItAll.Mcp.Core.Net;
using CanDoItAll.Mcp.Core.Observability;
using CanDoItAll.Mcp.DotNetWatch.Configuration;
using CanDoItAll.Mcp.DotNetWatch.Diagnostics;
using CanDoItAll.Mcp.DotNetWatch.Health;
using CanDoItAll.Mcp.DotNetWatch.Operations;
using CanDoItAll.Mcp.DotNetWatch.Runtime;
using CanDoItAll.Mcp.DotNetWatch.Security;
using CanDoItAll.Mcp.DotNetWatch.Tools;
using CanDoItAll.Mcp.LocalRuntime.Persistence;
using CanDoItAll.Mcp.LocalRuntime.Processes;
using Microsoft.Extensions.Options;
using System.Runtime.InteropServices;

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
builder.Services.AddSingleton<ServerInstanceIdentity>();
builder.Services.AddSingleton(serviceProvider => serviceProvider.GetRequiredService<RuntimeConfiguration>().CreateLocalProcessRuntimeOptions());
builder.Services.AddSingleton(serviceProvider => new SecretRedactor(serviceProvider.GetRequiredService<RuntimeConfiguration>().CreateSecretRedactionOptions()));
builder.Services.AddSingleton(serviceProvider => new FileLogStore(serviceProvider.GetRequiredService<RuntimeConfiguration>().CreateFileLogStoreOptions()));
builder.Services.AddSingleton<PathGuard>();
builder.Services.AddSingleton<EnvironmentOverlayFilter>();
builder.Services.AddSingleton<StaleProcessRegistry>();
builder.Services.AddSingleton<ServerInstanceRegistry>();
builder.Services.AddSingleton<IProcessCommandRunner, ProcessCommandRunner>();
        builder.Services.AddSingleton<IPlatformProcessTreeTerminator>(static serviceProvider =>
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? serviceProvider.GetRequiredService<WindowsProcessTreeTerminator>()
                : serviceProvider.GetRequiredService<UnixProcessTreeTerminator>());
        builder.Services.AddSingleton<WindowsProcessTreeTerminator>();
builder.Services.AddSingleton<UnixProcessTreeTerminator>();
builder.Services.AddSingleton<IProcessTreeTerminator, ProcessTreeTerminator>();
builder.Services.AddSingleton<ProcessSupervisor>();
builder.Services.AddSingleton<HttpProbeService>();
builder.Services.AddSingleton<TlsCertificateInspector>();
builder.Services.AddSingleton<HttpHealthProbe>();
builder.Services.AddSingleton<AppRuntimeManager>();
builder.Services.AddSingleton<ResourceMutationGate>();
builder.Services.AddSingleton<WorkspaceExecutionLock>();
builder.Services.AddSingleton<OperationRegistry>();
builder.Services.AddSingleton<StartFailureDiagnoser>();
        builder.Services.AddSingleton<SessionCoordinator>();
        builder.Services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithTools<CanDoItAllTools>();

        using var host = builder.Build();
        await using var registration = await host.Services.GetRequiredService<ServerInstanceRegistry>().RegisterCurrentAsync(CancellationToken.None);
        await RunStartupCleanupAsync(host.Services);
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

    private static async Task RunStartupCleanupAsync(IServiceProvider services)
    {
        var configuration = services.GetRequiredService<RuntimeConfiguration>();
        if (!configuration.CleanupStaleManagedProcessesOnStartup)
        {
            return;
        }

        var staleProcessRegistry = services.GetRequiredService<StaleProcessRegistry>();
        var terminator = services.GetRequiredService<IProcessTreeTerminator>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("CanDoItAll.Mcp.DotNetWatch.StartupCleanup");

        CleanupStaleProcessesData? result = null;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            result = await staleProcessRegistry.CleanupAsync(terminator, dryRun: false, CancellationToken.None);
            if (result.Checked == 0 ||
                !result.Skipped.Any(skip => string.Equals(skip.Reason, "Process no longer exists", StringComparison.OrdinalIgnoreCase)))
            {
                break;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500));
        }

        if (result is null)
        {
            return;
        }

        if (result.Killed.Count > 0 || result.Skipped.Count > 0)
        {
            logger.LogInformation(
                "Startup stale process cleanup completed. Checked={Checked}, Killed={Killed}, Skipped={Skipped}",
                result.Checked,
                result.Killed.Count,
                result.Skipped.Count);
        }
    }
}
