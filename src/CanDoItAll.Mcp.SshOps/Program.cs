using CanDoItAll.Mcp.Core.Hosting;
using CanDoItAll.Mcp.SshOps.Configuration;
using CanDoItAll.Mcp.SshOps.Coordination;
using CanDoItAll.Mcp.SshOps.Operations;
using CanDoItAll.Mcp.SshOps.Security;
using CanDoItAll.Mcp.SshOps.Tools;
using CanDoItAll.Mcp.SshOps.Transport;
using CanDoItAll.Mcp.Core.Concurrency;

namespace CanDoItAll.Mcp.SshOps;

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

        builder.Services.AddCanDoItAllMcpIdleShutdown<McpServerOptions>(options => options.Server.IdleShutdown);

        builder.Services.AddSingleton<RuntimeConfiguration>();
        builder.Services.AddSingleton<ServerInstanceIdentity>();
        builder.Services.AddSingleton<SecretResolver>();
        builder.Services.AddSingleton<HostKeyVerifier>();
        builder.Services.AddSingleton<RemotePathGuard>();
        builder.Services.AddSingleton<TargetCatalog>();
        builder.Services.AddSingleton<HttpProbeService>();
        builder.Services.AddSingleton<TlsCertificateInspector>();
        builder.Services.AddSingleton(serviceProvider => new SecretRedactor(serviceProvider.GetRequiredService<RuntimeConfiguration>().CreateSecretRedactionOptions()));
        builder.Services.AddSingleton(serviceProvider => new FileLogStore(serviceProvider.GetRequiredService<RuntimeConfiguration>().CreateFileLogStoreOptions()));
        builder.Services.AddSingleton<ResourceMutationGate>();
        builder.Services.AddSingleton<ISshTransport, SshNetTransport>();
        builder.Services.AddSingleton<RemoteJobRunner>();
        builder.Services.AddSingleton<TargetCoordinator>();

        builder.Services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithTools<SshOpsTools>();

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

        return "CanDoItAll.Mcp.SshOps.settings.json";
    }
}
