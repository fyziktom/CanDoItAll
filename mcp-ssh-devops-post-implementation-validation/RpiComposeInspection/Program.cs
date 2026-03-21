using CanDoItAll.Mcp.Core.Concurrency;
using CanDoItAll.Mcp.Core.Identity;
using CanDoItAll.Mcp.Core.Net;
using CanDoItAll.Mcp.Core.Observability;
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

using var host = BuildHost();
var tools = host.Services.GetRequiredService<SshOpsTools>();

var command = new[]
{
    "bash",
    "-lc",
    """
    set -eu
    echo "== uname =="
    uname -a
    echo
    echo "== arch =="
    uname -m
    getconf LONG_BIT || true
    echo
    echo "== docker version =="
    docker --version
    echo
    echo "== docker compose version =="
    (docker compose version || docker-compose version)
    echo
    echo "== running containers =="
    docker ps -a --format '{{.Names}}|{{.Image}}|{{.Status}}|{{.Ports}}'
    echo
    echo "== docker networks =="
    docker network ls
    echo
    echo "== candoitall root =="
    ls -la /home/pi/candoitall
    echo
    echo "== stacks tree =="
    find /home/pi/candoitall/stacks -maxdepth 4 -print | sort
    echo
    echo "== traefik tree =="
    find /home/pi/candoitall/traefik -maxdepth 4 -print | sort
    echo
    echo "== current app dir =="
    if [ -d /home/pi/candoitall/stacks/candoitall/app/current ]; then
      ls -la /home/pi/candoitall/stacks/candoitall/app/current
    else
      echo "missing"
    fi
    echo
    echo "== current web systemd unit =="
    if [ -f /etc/systemd/system/candoitall-web.service ]; then
      cat /etc/systemd/system/candoitall-web.service
    else
      echo "missing"
    fi
    echo
    echo "== docker logs candoitall-web =="
    docker logs --tail 200 candoitall-web 2>&1 || true
    echo
    echo "== docker logs candoitall-traefik =="
    docker logs --tail 200 candoitall-traefik 2>&1 || true
    """
};

var result = await tools.DangerousRawExecAsync(targetName, command, timeoutSeconds: 90);

Console.WriteLine($"ok={result.Ok}");
Console.WriteLine($"status={result.Status}");
Console.WriteLine($"summary={result.Summary}");
Console.WriteLine("stdout<<EOF");
Console.WriteLine(result.Data?.StandardOutput);
Console.WriteLine("EOF");
Console.WriteLine("stderr<<EOF");
Console.WriteLine(result.Data?.StandardError);
Console.WriteLine("EOF");

return result.Ok ? 0 : 1;

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
