using CanDoItAll.Mcp.Core.Concurrency;
using CanDoItAll.Mcp.Core.Contracts;
using CanDoItAll.Mcp.Core.Identity;
using CanDoItAll.Mcp.Core.Net;
using CanDoItAll.Mcp.Core.Observability;
using System.Text.Json;
using CanDoItAll.Mcp.SshOps.Configuration;
using CanDoItAll.Mcp.SshOps.Coordination;
using CanDoItAll.Mcp.SshOps.Operations;
using CanDoItAll.Mcp.SshOps.Security;
using CanDoItAll.Mcp.SshOps.Tools;
using CanDoItAll.Mcp.SshOps.Transport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

const string targetName = "rpi3-test";
const string traefikProjectName = "infra-traefik";
const string traefikStackName = "infra-traefik";
const string traefikRoot = "/home/pi/candoitall/stacks/infra-traefik";
const string traefikComposeFile = "/home/pi/candoitall/stacks/infra-traefik/docker-compose.yml";

const string appProjectName = "candoitall-compose";
const string appStackName = "candoitall-compose";
const string appRoot = "/home/pi/candoitall/stacks/candoitall-compose";
const string appComposeFile = "/home/pi/candoitall/stacks/candoitall-compose/docker-compose.yml";

var stoppedServices = new List<string>();

try
{
    using var host = BuildHost();
    var tools = host.Services.GetRequiredService<SshOpsTools>();

    var targetTest = await tools.TargetTestAsync(targetName);
    EnsureOk(targetTest, "target_test");

    var systemdStatus = await tools.DangerousRawExecAsync(
        targetName,
        [
            "bash",
            "-lc",
            """
            for service in candoitall-web candoitall-traefik; do
              printf "%s=" "$service"
              systemctl is-active "$service" 2>/dev/null || true
            done
            """
        ],
        timeoutSeconds: 30);
    EnsureOk(systemdStatus, "systemd status");

    foreach (var line in SplitLines(systemdStatus.Data?.StandardOutput))
    {
        var parts = line.Split('=', 2, StringSplitOptions.TrimEntries);
        if (parts.Length == 2 && string.Equals(parts[1], "active", StringComparison.OrdinalIgnoreCase))
        {
            stoppedServices.Add(parts[0]);
        }
    }

    if (stoppedServices.Count > 0)
    {
        var stopServices = await tools.DangerousRawExecAsync(
            targetName,
            [
                "bash",
                "-lc",
                $"sudo -n systemctl stop {string.Join(' ', stoppedServices)}"
            ],
            timeoutSeconds: 60);
        EnsureOk(stopServices, "stop legacy services");
    }

    var bundle = await tools.FsApplyBundleAsync(
        targetName,
        [
            new(traefikComposeFile, "utf8", TraefikComposeFile(), BackupBeforeWrite: true),
            new(appComposeFile, "utf8", OfficialRuntimeAppComposeFile(), BackupBeforeWrite: true)
        ]);
    EnsureOk(bundle, "fs_apply_bundle");

    var validateTraefik = await tools.ComposeValidateAsync(targetName, traefikComposeFile, traefikProjectName, traefikRoot);
    EnsureOk(validateTraefik, "compose_validate traefik");

    var validateApp = await tools.ComposeValidateAsync(targetName, appComposeFile, appProjectName, appRoot);
    EnsureOk(validateApp, "compose_validate app official");

    var applyTraefik = await tools.ComposeApplyAsync(
        targetName,
        stackName: traefikStackName,
        composeFile: traefikComposeFile,
        projectName: traefikProjectName,
        workingDirectory: traefikRoot,
        pull: true,
        build: false,
        removeOrphans: true,
        executionMode: "sync");
    EnsureOk(applyTraefik, "compose_apply traefik");

    var usingFallbackRuntime = false;
    var applyApp = await tools.ComposeApplyAsync(
        targetName,
        stackName: appStackName,
        composeFile: appComposeFile,
        projectName: appProjectName,
        workingDirectory: appRoot,
        pull: true,
        build: false,
        removeOrphans: true,
        executionMode: "sync");

    if (!applyApp.Ok || await ComposeProjectNeedsAttentionAsync(tools, appComposeFile, appProjectName, appRoot))
    {
        Console.WriteLine("official_runtime_attempt_failed_or_unstable");
        Console.WriteLine(DescribeEnvelope(applyApp));
        usingFallbackRuntime = true;

        await tools.ComposeDownAsync(targetName, appComposeFile, appProjectName, appRoot, removeOrphans: true);

        var fallbackBundle = await tools.FsApplyBundleAsync(
            targetName,
            [new(appComposeFile, "utf8", HostRuntimeAppComposeFile(), BackupBeforeWrite: true)]);
        EnsureOk(fallbackBundle, "fs_apply_bundle app fallback");

        var validateFallback = await tools.ComposeValidateAsync(targetName, appComposeFile, appProjectName, appRoot);
        EnsureOk(validateFallback, "compose_validate app fallback");

        applyApp = await tools.ComposeApplyAsync(
            targetName,
            stackName: appStackName,
            composeFile: appComposeFile,
            projectName: appProjectName,
            workingDirectory: appRoot,
            pull: true,
            build: false,
            removeOrphans: true,
            executionMode: "sync");
    }

    EnsureOk(applyApp, "compose_apply app");

    await Task.Delay(TimeSpan.FromSeconds(20));

    var directHealth = await tools.HttpWaitAsync(
        targetName,
        origin: "remote",
        url: "http://127.0.0.1:5100/health",
        expectedStatuses: [200],
        timeoutSeconds: 180,
        pollIntervalSeconds: 5);
    EnsureOk(directHealth, "http_wait direct");

    var traefikRedirect = await tools.HttpWaitAsync(
        targetName,
        origin: "local",
        url: "http://10.190.32.143",
        expectedStatuses: [301, 302],
        timeoutSeconds: 120,
        pollIntervalSeconds: 5);
    EnsureOk(traefikRedirect, "http_wait redirect");

    var httpsHealth = await tools.HttpWaitAsync(
        targetName,
        origin: "local",
        url: "https://10.190.32.143/health",
        expectedStatuses: [200],
        timeoutSeconds: 180,
        pollIntervalSeconds: 5,
        allowInsecureTls: true);
    EnsureOk(httpsHealth, "http_wait https health");

    var certCheck = await tools.CertCheckAsync(targetName, "10.190.32.143", "local");
    EnsureOk(certCheck, "cert_check");

    var traefikPs = await tools.ComposePsAsync(targetName, traefikComposeFile, traefikProjectName, traefikRoot);
    EnsureHealthyComposeProject(traefikPs, "compose_ps traefik");

    var appPs = await tools.ComposePsAsync(targetName, appComposeFile, appProjectName, appRoot);
    EnsureHealthyComposeProject(appPs, "compose_ps app");

    Console.WriteLine("deployment=success");
    Console.WriteLine($"runtime_mode={(usingFallbackRuntime ? "host-runtime" : "official-runtime")}");
    Console.WriteLine($"stopped_services={(stoppedServices.Count == 0 ? "none" : string.Join(',', stoppedServices))}");
    Console.WriteLine($"traefik_ps={JsonSerializer.Serialize(traefikPs.Data)}");
    Console.WriteLine($"app_ps={JsonSerializer.Serialize(appPs.Data)}");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex);

    if (stoppedServices.Count > 0)
    {
        try
        {
            using var host = BuildHost();
            var tools = host.Services.GetRequiredService<SshOpsTools>();
            await tools.DangerousRawExecAsync(
                targetName,
                [
                    "bash",
                    "-lc",
                    $"sudo -n systemctl start {string.Join(' ', stoppedServices)}"
                ],
                timeoutSeconds: 60);
        }
        catch
        {
            // Best-effort recovery only.
        }
    }

    return 1;
}

static IHost BuildHost()
{
    var builder = Host.CreateEmptyApplicationBuilder(settings: null);
    builder.Configuration.AddJsonFile(Path.GetFullPath("CanDoItAll.Mcp.SshOps.settings.json", Environment.CurrentDirectory), optional: false, reloadOnChange: false);
    builder.Configuration.AddEnvironmentVariables(prefix: "CanDoItAllMcp_");

    builder.Logging.ClearProviders();
    builder.Logging.AddConsole();
    builder.Logging.SetMinimumLevel(LogLevel.Warning);

    builder.Services
        .AddOptions<McpServerOptions>()
        .Bind(builder.Configuration)
        .ValidateDataAnnotations()
        .ValidateOnStart();
    builder.Services.AddSingleton<IValidateOptions<McpServerOptions>, McpServerOptionsValidator>();

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
    builder.Services.AddSingleton<SshOpsTools>();

    return builder.Build();
}

static void EnsureOk<T>(McpToolEnvelope<T> envelope, string name)
{
    if (envelope.Ok)
    {
        return;
    }

    throw new InvalidOperationException($"{name} failed: {DescribeEnvelope(envelope)}");
}

static void EnsureHealthyComposeProject(McpToolEnvelope<ComposePsData> envelope, string name)
{
    EnsureOk(envelope, name);

    if (!string.Equals(envelope.Status, "success", StringComparison.OrdinalIgnoreCase) ||
        envelope.Data?.Services.Any(static service => !string.Equals(service.State, "running", StringComparison.OrdinalIgnoreCase)) != false)
    {
        throw new InvalidOperationException($"{name} reported a degraded state: {DescribeEnvelope(envelope)}");
    }
}

static async Task<bool> ComposeProjectNeedsAttentionAsync(
    SshOpsTools tools,
    string composeFile,
    string projectName,
    string workingDirectory)
{
    await Task.Delay(TimeSpan.FromSeconds(20));
    var snapshot = await tools.ComposePsAsync(targetName, composeFile, projectName, workingDirectory);
    if (!snapshot.Ok)
    {
        return true;
    }

    if (!string.Equals(snapshot.Status, "success", StringComparison.OrdinalIgnoreCase))
    {
        return true;
    }

    return snapshot.Data?.Services.Any(static service => !string.Equals(service.State, "running", StringComparison.OrdinalIgnoreCase)) == true;
}

static IReadOnlyList<string> SplitLines(string? text)
{
    return string.IsNullOrWhiteSpace(text)
        ? []
        : text.Replace("\r", string.Empty, StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

static string DescribeEnvelope<T>(McpToolEnvelope<T> envelope)
{
    var parts = new List<string>();
    if (!string.IsNullOrWhiteSpace(envelope.Status))
    {
        parts.Add($"status={envelope.Status}");
    }

    if (!string.IsNullOrWhiteSpace(envelope.Summary))
    {
        parts.Add(envelope.Summary);
    }

    if (envelope.Error is not null)
    {
        parts.Add($"error={envelope.Error.Code}: {envelope.Error.Message}");
        if (envelope.Error.Details is not null)
        {
            parts.Add($"details={JsonSerializer.Serialize(envelope.Error.Details)}");
        }
    }

    if (envelope.Diagnostics is { Count: > 0 })
    {
        parts.Add($"diagnostics={string.Join(" | ", envelope.Diagnostics)}");
    }

    if (envelope.Warnings.Count > 0)
    {
        parts.Add($"warnings={string.Join(" | ", envelope.Warnings)}");
    }

    return string.Join(" || ", parts.Where(static part => !string.IsNullOrWhiteSpace(part)));
}

static string TraefikComposeFile()
{
    return """
        version: '3.8'
        services:
          traefik:
            image: traefik:v3.6
            container_name: candoitall-traefik
            restart: unless-stopped
            network_mode: host
            command:
              - --configFile=/etc/traefik/traefik.yml
            volumes:
              - /home/pi/candoitall/traefik/traefik.yml:/etc/traefik/traefik.yml:ro
              - /home/pi/candoitall/traefik/dynamic:/home/pi/candoitall/traefik/dynamic:ro
              - /home/pi/candoitall/traefik/certs:/home/pi/candoitall/traefik/certs:ro
        """;
}

static string OfficialRuntimeAppComposeFile()
{
    return """
        version: '3.8'
        services:
          candoitall-web:
            image: mcr.microsoft.com/dotnet/aspnet:10.0
            container_name: candoitall-web
            restart: unless-stopped
            working_dir: /app
            command: ["dotnet", "CanDoItAll.Web.dll"]
            environment:
              ASPNETCORE_ENVIRONMENT: Production
              ASPNETCORE_URLS: http://0.0.0.0:5100
              DOTNET_SYSTEM_GLOBALIZATION_INVARIANT: "1"
            volumes:
              - /home/pi/candoitall/stacks/candoitall/app/current:/app:ro
              - /home/pi/candoitall/stacks/candoitall/config/appsettings.Production.json:/app/appsettings.Production.json:ro
              - /home/pi/candoitall/data/web:/home/pi/candoitall/data/web
            ports:
              - "127.0.0.1:5100:5100"
        """;
}

static string HostRuntimeAppComposeFile()
{
    return """
        version: '3.8'
        services:
          candoitall-web:
            image: debian:12-slim
            container_name: candoitall-web
            restart: unless-stopped
            working_dir: /app
            command:
              - /home/pi/candoitall/dotnet/dotnet
              - /app/CanDoItAll.Web.dll
            environment:
              ASPNETCORE_ENVIRONMENT: Production
              ASPNETCORE_URLS: http://0.0.0.0:5100
              DOTNET_ROOT: /home/pi/candoitall/dotnet
              DOTNET_SYSTEM_GLOBALIZATION_INVARIANT: "1"
              LD_LIBRARY_PATH: /home/pi/candoitall/glibc-bookworm/lib/arm-linux-gnueabihf:/home/pi/candoitall/glibc-bookworm/usr/lib/arm-linux-gnueabihf:/home/pi/candoitall/dotnet
            volumes:
              - /home/pi/candoitall/stacks/candoitall/app/current:/app:ro
              - /home/pi/candoitall/stacks/candoitall/config/appsettings.Production.json:/app/appsettings.Production.json:ro
              - /home/pi/candoitall/data/web:/home/pi/candoitall/data/web
              - /home/pi/candoitall/dotnet:/home/pi/candoitall/dotnet:ro
              - /home/pi/candoitall/glibc-bookworm:/home/pi/candoitall/glibc-bookworm:ro
            ports:
              - "127.0.0.1:5100:5100"
        """;
}
