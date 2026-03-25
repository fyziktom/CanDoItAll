using CanDoItAll.Mcp.Core.Concurrency;
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
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using ServerConfigurationOptions = CanDoItAll.Mcp.DotNetWatch.Configuration.McpServerOptions;

namespace CanDoItAll.Mcp.DotNetWatch;

internal static class Program
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
        ConfigureStdioLogging(builder.Logging);
        ConfigureCommonServices(builder.Services, launchContext);

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
        ConfigureBackendLogging(builder.Logging);
        ConfigureCommonServices(builder.Services, launchContext);
        builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        builder.Services.AddSingleton<IDotNetWatchToolInvoker, LocalToolInvoker>();

        var backendOptions = builder.Configuration.GetSection("Backend").Get<BackendOptions>() ?? new BackendOptions();
        builder.WebHost.UseUrls($"http://{backendOptions.BindHost}:0");

        var app = builder.Build();
        var registrationStore = app.Services.GetRequiredService<BackendRegistrationStore>();
        var globalCatalogStore = app.Services.GetRequiredService<GlobalBackendCatalogStore>();
        var identityProvider = app.Services.GetRequiredService<BackendIdentityProvider>();
        var managerService = app.Services.GetRequiredService<BackendManagerService>();
        var invoker = app.Services.GetRequiredService<IDotNetWatchToolInvoker>();
        var bootstrapDiagnostics = app.Services.GetRequiredService<BootstrapDiagnosticsWriter>();

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
        configuration.AddJsonFile(Path.GetFullPath(launchContext.SettingsPath), optional: false, reloadOnChange: false);
        configuration.AddEnvironmentVariables(prefix: "CanDoItAllMcp_");
    }

    private static void ConfigureStdioLogging(ILoggingBuilder logging)
    {
        logging.ClearProviders();
        logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
        logging.SetMinimumLevel(LogLevel.Information);
    }

    private static void ConfigureBackendLogging(ILoggingBuilder logging)
    {
        logging.ClearProviders();
        logging.SetMinimumLevel(LogLevel.Information);
    }

    private static void ConfigureCommonServices(IServiceCollection services, LaunchContext launchContext)
    {
        services.AddSingleton(launchContext);
        services
            .AddOptions<ServerConfigurationOptions>()
            .BindConfiguration(string.Empty)
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<ServerConfigurationOptions>, McpServerOptionsValidator>();

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

    private static void MapToolRoutes(WebApplication app, IDotNetWatchToolInvoker invoker)
    {
        app.MapPost("/api/tools/workspace-info", (HttpContext httpContext, WorkspaceInfoRequest request, BackendRequestReplayStore replayStore, CancellationToken cancellationToken) =>
            ExecuteToolRouteAsync(httpContext, replayStore, "workspace-info", request, token => invoker.WorkspaceInfoAsync(request.IncludeHistory, request.IncludeConfigSnapshot, token), cancellationToken));
        app.MapPost("/api/tools/app-start", (HttpContext httpContext, AppStartRequest request, BackendRequestReplayStore replayStore, CancellationToken cancellationToken) =>
            ExecuteToolRouteAsync(httpContext, replayStore, "app-start", request, token => invoker.AppStartAsync(request.LogicalAppId, request.ProjectPath, request.Mode, request.LaunchType, request.PreferredLane, request.EntryPath, request.ConfigurationName, request.Framework, request.LaunchProfile, request.WorkingDirectory, request.Arguments, request.EnvironmentOverlay, request.Urls, request.ReuseIfCompatible, request.ConflictPolicy, request.WaitFor, token), cancellationToken));
        app.MapPost("/api/tools/app-stop", (HttpContext httpContext, AppStopRequest request, BackendRequestReplayStore replayStore, CancellationToken cancellationToken) =>
            ExecuteToolRouteAsync(httpContext, replayStore, "app-stop", request, token => invoker.AppStopAsync(request.SessionId, request.Reason, request.Force, token), cancellationToken));
        app.MapPost("/api/tools/app-status", (HttpContext httpContext, AppStatusRequest request, BackendRequestReplayStore replayStore, CancellationToken cancellationToken) =>
            ExecuteToolRouteAsync(httpContext, replayStore, "app-status", request, token => invoker.AppStatusAsync(request.SessionId, token), cancellationToken));
        app.MapPost("/api/tools/app-wait", (HttpContext httpContext, AppWaitRequest request, BackendRequestReplayStore replayStore, CancellationToken cancellationToken) =>
            ExecuteToolRouteAsync(httpContext, replayStore, "app-wait", request, token => invoker.AppWaitAsync(request.SessionId, request.Condition, request.TimeoutMs, request.PollIntervalMs, request.Cursor, request.QuietPeriodMs, request.LogPattern, request.CaseInsensitive, token), cancellationToken));
        app.MapPost("/api/tools/app-logs", (HttpContext httpContext, AppLogsRequest request, BackendRequestReplayStore replayStore, CancellationToken cancellationToken) =>
            ExecuteToolRouteAsync(httpContext, replayStore, "app-logs", request, token => invoker.AppLogsAsync(request.SessionId, request.Cursor, request.Limit, request.IncludeStdOut, request.IncludeStdErr, request.IncludeSystemEvents, request.View, token), cancellationToken));
        app.MapPost("/api/tools/app-events", (HttpContext httpContext, AppEventsRequest request, BackendRequestReplayStore replayStore, CancellationToken cancellationToken) =>
            ExecuteToolRouteAsync(httpContext, replayStore, "app-events", request, token => invoker.AppEventsAsync(request.LogicalAppId, request.SessionId, request.Cursor, request.Limit, token), cancellationToken));
        app.MapPost("/api/tools/app-update-atomic", (HttpContext httpContext, AtomicUpdateRequest request, BackendRequestReplayStore replayStore, CancellationToken cancellationToken) =>
            ExecuteToolRouteAsync(httpContext, replayStore, "app-update-atomic", request, token => invoker.AppUpdateAtomicAsync(request.LogicalAppId, request.ProjectPath, request.ConfigurationName, request.Framework, request.Arguments, request.EnvironmentOverlay, request.ActivateOnSuccess, request.KeepPreviousRuntimeWarm, request.AllowRollback, request.TimeoutMs, token), cancellationToken));
        app.MapPost("/api/tools/app-rollback", (HttpContext httpContext, AtomicRollbackRequest request, BackendRequestReplayStore replayStore, CancellationToken cancellationToken) =>
            ExecuteToolRouteAsync(httpContext, replayStore, "app-rollback", request, token => invoker.AppRollbackAsync(request.LogicalAppId, request.TransactionId, token), cancellationToken));
        app.MapPost("/api/tools/solution-build", (HttpContext httpContext, SolutionBuildRequest request, BackendRequestReplayStore replayStore, CancellationToken cancellationToken) =>
            ExecuteToolRouteAsync(httpContext, replayStore, "solution-build", request, token => invoker.SolutionBuildAsync(request.TargetPath, request.ConfigurationName, request.Framework, request.Arguments, request.EnvironmentOverlay, request.WhenAppRunning, request.WaitForCompletion, request.TimeoutMs, token), cancellationToken));
        app.MapPost("/api/tools/tests-run", (HttpContext httpContext, TestsRunRequest request, BackendRequestReplayStore replayStore, CancellationToken cancellationToken) =>
            ExecuteToolRouteAsync(httpContext, replayStore, "tests-run", request, token => invoker.TestsRunAsync(request.TargetPath, request.ConfigurationName, request.Framework, request.Filter, request.Arguments, request.EnvironmentOverlay, request.CollectCoverage, request.WhenAppRunning, request.RunnerPreference, request.WaitForCompletion, request.TimeoutMs, token), cancellationToken));
        app.MapPost("/api/tools/operation-status", (HttpContext httpContext, OperationStatusRequest request, BackendRequestReplayStore replayStore, CancellationToken cancellationToken) =>
            ExecuteToolRouteAsync(httpContext, replayStore, "operation-status", request, token => invoker.OperationStatusAsync(request.OperationId, token), cancellationToken));
        app.MapPost("/api/tools/operation-wait", (HttpContext httpContext, OperationWaitRequest request, BackendRequestReplayStore replayStore, CancellationToken cancellationToken) =>
            ExecuteToolRouteAsync(httpContext, replayStore, "operation-wait", request, token => invoker.OperationWaitAsync(request.OperationId, request.TimeoutMs, request.PollIntervalMs, token), cancellationToken));
        app.MapPost("/api/tools/operation-logs", (HttpContext httpContext, OperationLogsRequest request, BackendRequestReplayStore replayStore, CancellationToken cancellationToken) =>
            ExecuteToolRouteAsync(httpContext, replayStore, "operation-logs", request, token => invoker.OperationLogsAsync(request.OperationId, request.Cursor, request.Limit, request.View, token), cancellationToken));
        app.MapPost("/api/tools/cleanup-stale-processes", (HttpContext httpContext, CleanupStaleProcessesRequest request, BackendRequestReplayStore replayStore, CancellationToken cancellationToken) =>
            ExecuteToolRouteAsync(httpContext, replayStore, "cleanup-stale-processes", request, token => invoker.CleanupStaleProcessesAsync(request.DryRun, token), cancellationToken));
        app.MapPost("/api/tools/diagnose-start-failure", (HttpContext httpContext, DiagnoseStartFailureRequest request, BackendRequestReplayStore replayStore, CancellationToken cancellationToken) =>
            ExecuteToolRouteAsync(httpContext, replayStore, "diagnose-start-failure", request, token => invoker.DiagnoseStartFailureAsync(request.SessionId, request.OperationId, request.MaxLogEntries, token), cancellationToken));
    }

    private static async Task<IResult> ExecuteToolRouteAsync<TRequest, TResponse>(
        HttpContext httpContext,
        BackendRequestReplayStore replayStore,
        string route,
        TRequest request,
        Func<CancellationToken, Task<TResponse>> callback,
        CancellationToken cancellationToken)
    {
        var requestId = httpContext.Request.Headers["X-CanDoItAll-RequestId"].FirstOrDefault();
        var json = await replayStore.ExecuteJsonAsync(route, requestId, request, callback, cancellationToken);
        return Results.Text(json, "application/json; charset=utf-8");
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
