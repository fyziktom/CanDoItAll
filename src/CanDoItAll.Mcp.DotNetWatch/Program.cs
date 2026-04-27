using CanDoItAll.Mcp.Core.Concurrency;
using CanDoItAll.Mcp.Core.Hosting;
using CanDoItAll.Mcp.Core.Identity;
using CanDoItAll.Mcp.Core.Net;
using CanDoItAll.Mcp.Core.Observability;
using CanDoItAll.Mcp.DotNetWatch.Backend;
using CanDoItAll.Mcp.DotNetWatch.Bridge;
using CanDoItAll.Mcp.DotNetWatch.Configuration;
using CanDoItAll.Mcp.DotNetWatch.Diagnostics;
using CanDoItAll.Mcp.DotNetWatch.Guidance;
using CanDoItAll.Mcp.DotNetWatch.Health;
using CanDoItAll.Mcp.DotNetWatch.Manager;
using CanDoItAll.Mcp.DotNetWatch.Operations;
using CanDoItAll.Mcp.DotNetWatch.Runtime;
using CanDoItAll.Mcp.DotNetWatch.Runtime.Atomic;
using CanDoItAll.Mcp.DotNetWatch.Runtime.Coordination;
using CanDoItAll.Mcp.DotNetWatch.Runtime.Events;
using CanDoItAll.Mcp.DotNetWatch.Security;
using CanDoItAll.Mcp.DotNetWatch.Tools;
using CanDoItAll.Mcp.LocalRuntime.Persistence;
using CanDoItAll.Mcp.LocalRuntime.Processes;
using Microsoft.AspNetCore.Hosting;
using ModelContextProtocol.Server;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using ServerConfigurationOptions = CanDoItAll.Mcp.DotNetWatch.Configuration.McpServerOptions;

namespace CanDoItAll.Mcp.DotNetWatch;

internal static partial class Program
{
    public static async Task Main(string[] args)
    {
        var launchContext = ResolveLaunchContext(args);
        if (launchContext.HostMode == DotNetWatchHostMode.Backend)
        {
            await RunBackendAsync(launchContext);
            return;
        }

        if (launchContext.HostMode == DotNetWatchHostMode.BackendLauncher)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                throw new PlatformNotSupportedException("Backend launcher mode is only supported on Windows.");
            }

            WindowsDetachedBackendBootstrap.Launch(launchContext);
            return;
        }

        await RunStdioAsync(launchContext);
    }

    private static async Task RunStdioAsync(LaunchContext launchContext)
    {
        var builder = Host.CreateEmptyApplicationBuilder(settings: null);
        ConfigureConfiguration(builder.Configuration, launchContext);
        builder.Logging.ConfigureCanDoItAllMcpStdioLogging();
        ConfigureCommonServices(builder.Services, builder.Configuration, launchContext);

        builder.Services.AddSingleton<BackendProcessLauncher>();
        builder.Services.AddSingleton<BackendConnectionManager>();
        builder.Services.AddSingleton<BridgeRepairCoordinator>();
        builder.Services.AddSingleton<BackendToolInvoker>();
        builder.Services.AddSingleton<IDotNetWatchToolInvoker>(serviceProvider =>
        {
            var configuration = serviceProvider.GetRequiredService<RuntimeConfiguration>();
            return configuration.BackendEnabled
                ? serviceProvider.GetRequiredService<BackendToolInvoker>()
                : serviceProvider.GetRequiredService<LocalToolInvoker>();
        });
        builder.Services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithTools<CanDoItAllTools>();

        using var host = builder.Build();
        var configuration = host.Services.GetRequiredService<RuntimeConfiguration>();
        var bootstrapDiagnostics = host.Services.GetRequiredService<BootstrapDiagnosticsWriter>();

        IAsyncDisposable? registration = null;
        try
        {
            if (configuration.BackendEnabled)
            {
                await host.Services.GetRequiredService<BackendConnectionManager>().EnsureReadyAsync(CancellationToken.None);
            }
            else
            {
                registration = await host.Services.GetRequiredService<ServerInstanceRegistry>().RegisterCurrentAsync(CancellationToken.None);
                await RunStartupCleanupAsync(host.Services);
            }

            await host.RunAsync();
        }
        catch (Exception ex)
        {
            await bootstrapDiagnostics.WriteFailureAsync(
                phase: "stdio-startup-or-run",
                exception: ex,
                context: new Dictionary<string, object?>
                {
                    ["settingsPath"] = launchContext.SettingsPath,
                    ["hostMode"] = launchContext.HostMode.ToString()
                },
                cancellationToken: CancellationToken.None);
            throw;
        }
        finally
        {
            if (registration is not null)
            {
                await registration.DisposeAsync();
            }
        }
    }

    private static async Task RunBackendAsync(LaunchContext launchContext)
    {
        if (string.IsNullOrWhiteSpace(launchContext.BackendToken))
        {
            throw new InvalidOperationException("Backend mode requires --backend-token.");
        }

        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            Args = Array.Empty<string>(),
            ContentRootPath = Environment.CurrentDirectory
        });

        ConfigureConfiguration(builder.Configuration, launchContext);
        builder.Logging.ConfigureCanDoItAllMcpBackendLogging();
        ConfigureCommonServices(builder.Services, builder.Configuration, launchContext);
        builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        builder.Services.AddSingleton<IDotNetWatchToolInvoker, LocalToolInvoker>();

        var backendOptions = builder.Configuration.GetSection("Backend").Get<BackendOptions>() ?? new BackendOptions();
        builder.WebHost.UseUrls($"http://{backendOptions.BindHost}:0");

        var app = builder.Build();
        var registrationStore = app.Services.GetRequiredService<BackendRegistrationStore>();
        var globalCatalogStore = app.Services.GetRequiredService<GlobalBackendCatalogStore>();
        var identityProvider = app.Services.GetRequiredService<BackendIdentityProvider>();
        var ownershipCoordinator = app.Services.GetRequiredService<BackendWorkspaceOwnershipCoordinator>();
        var managerService = app.Services.GetRequiredService<BackendManagerService>();
        var invoker = app.Services.GetRequiredService<IDotNetWatchToolInvoker>();
        var bootstrapDiagnostics = app.Services.GetRequiredService<BootstrapDiagnosticsWriter>();
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("CanDoItAll.Mcp.DotNetWatch.BackendHost");

        BackendRegistrationRecord? registrationRecord = null;
        app.Use(async (httpContext, next) =>
        {
            if (httpContext.Request.Path == "/favicon.ico")
            {
                httpContext.Response.StatusCode = StatusCodes.Status204NoContent;
                return;
            }

            if (!BackendAuth.IsAuthorized(httpContext, launchContext.BackendToken))
            {
                httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await httpContext.Response.WriteAsync("Unauthorized");
                return;
            }

            await next();
        });

        app.MapGet("/", async (CancellationToken cancellationToken) =>
        {
            var snapshot = await managerService.CreateAggregateStatusAsync(registrationRecord, cancellationToken);
            return Results.Content(BackendDashboardPage.Render(snapshot), "text/html; charset=utf-8");
        });

        app.MapGet("/api/backend/ping", () =>
        {
            var record = registrationRecord ?? throw new InvalidOperationException("Backend registration is not ready.");
            return Results.Ok(new BackendPingResponse(record.BackendId, record.ProcessId, record.ProcessStartedUtc, record.Identity));
        });

        app.MapGet("/api/backend/status", () => Results.Ok(managerService.CreateLocalRuntimeStatus(registrationRecord)));
        app.MapGet("/api/manager/status", async (CancellationToken cancellationToken) =>
            Results.Ok(await managerService.CreateAggregateStatusAsync(registrationRecord, cancellationToken)));
        app.MapPost("/api/backend/manager-action", async (BackendManagerActionRequest request, CancellationToken cancellationToken) =>
            Results.Ok(await managerService.ExecuteLocalActionAsync(request, proxied: false, cancellationToken)));
        app.MapPost("/api/manager/action", async (BackendManagerActionRequest request, CancellationToken cancellationToken) =>
            Results.Ok(await managerService.ExecuteManagerActionAsync(request, registrationRecord, cancellationToken)));

        MapToolRoutes(app, invoker);

        var ownership = await ownershipCoordinator.AcquireAsync(CancellationToken.None);
        if (!ownership.Acquired)
        {
            logger.LogInformation(
                "Backend startup exited because workspace ownership is held by {BackendId}.",
                ownership.ExistingOwner?.BackendId ?? "<pending>");
            return;
        }

        await using var ownershipLease = ownership.Lease!;
        await using var serverRegistration = await app.Services.GetRequiredService<ServerInstanceRegistry>().RegisterCurrentAsync(CancellationToken.None);
        await RunStartupCleanupAsync(app.Services);

        await app.StartAsync();
        var baseUrl = app.Urls.FirstOrDefault() ?? throw new InvalidOperationException("Backend did not publish a listening address.");
        var managerUrl = $"{baseUrl.TrimEnd('/')}/?{BackendAuth.QueryKey}={Uri.EscapeDataString(launchContext.BackendToken)}";

        registrationRecord = new BackendRegistrationRecord(
            CorrelationIdFactory.Create("backend"),
            Environment.ProcessId,
            Process.GetCurrentProcess().StartTime.ToUniversalTime(),
            DateTimeOffset.UtcNow,
            baseUrl,
            managerUrl,
            launchContext.BackendToken,
            identityProvider.Current);

        await registrationStore.WriteAsync(registrationRecord, CancellationToken.None);
        await globalCatalogStore.UpsertAsync(registrationRecord, CancellationToken.None);

        try
        {
            await app.WaitForShutdownAsync();
        }
        catch (Exception ex)
        {
            await bootstrapDiagnostics.WriteFailureAsync(
                phase: "backend-startup-or-run",
                exception: ex,
                context: new Dictionary<string, object?>
                {
                    ["settingsPath"] = launchContext.SettingsPath,
                    ["hostMode"] = launchContext.HostMode.ToString()
                },
                cancellationToken: CancellationToken.None);
            throw;
        }
        finally
        {
            registrationStore.Delete();
            await globalCatalogStore.DeleteAsync(registrationRecord?.BackendId, CancellationToken.None);
        }
    }

    private static void ConfigureConfiguration(ConfigurationManager configuration, LaunchContext launchContext)
    {
        configuration.Sources.Clear();
        configuration.AddCanDoItAllMcpSettings(launchContext.SettingsPath);
    }

    private static void ConfigureCommonServices(IServiceCollection services, IConfiguration configuration, LaunchContext launchContext)
    {
        services.AddSingleton(launchContext);
        services
            .AddValidatedCanDoItAllMcpOptions<ServerConfigurationOptions, McpServerOptionsValidator>(
                configuration,
                validateDataAnnotations: false);

        services.AddHttpClient();
        services.AddSingleton<RuntimeConfiguration>();
        services.AddSingleton<BackendIdentityProvider>();
        services.AddSingleton<ServerInstanceIdentity>();
        services.AddSingleton(serviceProvider => serviceProvider.GetRequiredService<RuntimeConfiguration>().CreateLocalProcessRuntimeOptions());
        services.AddSingleton(serviceProvider => new SecretRedactor(serviceProvider.GetRequiredService<RuntimeConfiguration>().CreateSecretRedactionOptions()));
        services.AddSingleton(serviceProvider => new FileLogStore(serviceProvider.GetRequiredService<RuntimeConfiguration>().CreateFileLogStoreOptions()));
        services.AddSingleton<PathGuard>();
        services.AddSingleton<EnvironmentOverlayFilter>();
        services.AddSingleton<StaleProcessRegistry>();
        services.AddSingleton<ServerInstanceRegistry>();
        services.AddSingleton<BackendRegistrationStore>();
        services.AddSingleton<GlobalBackendCatalogStore>();
        services.AddSingleton<BackendWorkspaceOwnershipCoordinator>();
        services.AddSingleton<BackendRequestReplayStore>();
        services.AddSingleton<IProcessCommandRunner, ProcessCommandRunner>();
        services.AddSingleton<IPlatformProcessTreeTerminator>(static serviceProvider =>
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? serviceProvider.GetRequiredService<WindowsProcessTreeTerminator>()
                : serviceProvider.GetRequiredService<UnixProcessTreeTerminator>());
        services.AddSingleton<WindowsProcessTreeTerminator>();
        services.AddSingleton<UnixProcessTreeTerminator>();
        services.AddSingleton<IProcessTreeTerminator, ProcessTreeTerminator>();
        services.AddSingleton<ProcessSupervisor>();
        services.AddSingleton<HttpProbeService>();
        services.AddSingleton<TlsCertificateInspector>();
        services.AddSingleton<HttpHealthProbe>();
        services.AddSingleton<AgentLogReducer>();
        services.AddSingleton<WorkflowGuidancePolicy>();
        services.AddSingleton<BootstrapDiagnosticsWriter>();
        services.AddSingleton<TailwindCompanionCoordinator>();
        services.AddSingleton<RuntimeEndpointAllocator>();
        services.AddSingleton<ResourceScopePlanner>();
        services.AddSingleton<RuntimeSlotRegistry>();
        services.AddSingleton<SessionEventJournal>();
        services.AddSingleton<AppRuntimeManager>();
        services.AddSingleton<ResourceMutationGate>();
        services.AddSingleton<WorkspaceExecutionLock>();
        services.AddSingleton<OperationRegistry>();
        services.AddSingleton<AtomicUpdateCoordinator>();
        services.AddSingleton<StartFailureDiagnoser>();
        services.AddSingleton<SessionCoordinator>();
        services.AddSingleton<LocalToolInvoker>();
        services.AddSingleton<IProjectPathPicker, WindowsProjectPathPicker>();
        services.AddSingleton<BackendManagerService>();
    }

    private static LaunchContext ResolveLaunchContext(string[] args)
    {
        var settingsPath = "CanDoItAll.Mcp.DotNetWatch.settings.json";
        var hostMode = DotNetWatchHostMode.StdioProxy;
        string? backendToken = null;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--settings" when i < args.Length - 1:
                    settingsPath = args[++i];
                    break;
                case "--backend":
                    hostMode = DotNetWatchHostMode.Backend;
                    break;
                case "--backend-launcher":
                    hostMode = DotNetWatchHostMode.BackendLauncher;
                    break;
                case "--backend-token" when i < args.Length - 1:
                    backendToken = args[++i];
                    break;
            }
        }

        return new LaunchContext(Path.GetFullPath(settingsPath), hostMode, backendToken);
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
