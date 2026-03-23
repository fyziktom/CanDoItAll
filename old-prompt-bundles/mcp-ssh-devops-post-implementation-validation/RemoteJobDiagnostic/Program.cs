using CanDoItAll.Mcp.Core.Concurrency;
using CanDoItAll.Mcp.Core.Contracts;
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
var targetCatalog = host.Services.GetRequiredService<TargetCatalog>();
var target = targetCatalog.GetRequired(targetName);
var remoteJobRunner = host.Services.GetRequiredService<RemoteJobRunner>();
var transport = host.Services.GetRequiredService<ISshTransport>();
var tools = host.Services.GetRequiredService<SshOpsTools>();

var started = await remoteJobRunner.StartAsync(
    target,
    new RemoteJobStartRequest(
        CorrelationId: "corr_remote_job_diagnostic",
        Kind: "diagnostic_sleep_job",
        InitialSummary: "Diagnostic job queued.",
        SuccessSummary: "Diagnostic job completed.",
        FailureSummary: "Diagnostic job failed.",
        CancelSummary: "Diagnostic job cancelled.",
        Command: ["sleep", "120"],
        WorkingDirectory: "/home/pi"),
    CancellationToken.None);

Console.WriteLine($"operationId={started.OperationId}");
Console.WriteLine($"jobDirectory={started.JobDirectory}");

await Task.Delay(TimeSpan.FromSeconds(2));

var snapshot = await remoteJobRunner.GetSnapshotAsync(target, started.OperationId, CancellationToken.None);
PrintSnapshot("after-2s", snapshot);

await PrintRemoteTextAsync(transport, target, $"{started.JobDirectory}/run.sh", "run.sh", 12_000);
await PrintRemoteTextAsync(transport, target, $"{started.JobDirectory}/state", "state", 256);
await PrintRemoteTextAsync(transport, target, $"{started.JobDirectory}/summary", "summary", 1_024);
await PrintRemoteTextAsync(transport, target, $"{started.JobDirectory}/pid", "pid", 256);
await PrintRemoteTextAsync(transport, target, $"{started.JobDirectory}/exitCode", "exitCode", 256);
await PrintRemoteTextAsync(transport, target, $"{started.JobDirectory}/stdout.log", "stdout.log", 4_096);
await PrintRemoteTextAsync(transport, target, $"{started.JobDirectory}/stderr.log", "stderr.log", 4_096);

var directSleep = await tools.DangerousRawExecAsync(
    targetName,
    ["bash", "-lc", "SECONDS=0; sleep 5; echo elapsed:$SECONDS"],
    timeoutSeconds: 20);
Console.WriteLine($"directSleep.ok={directSleep.Ok}");
Console.WriteLine($"directSleep.stdout={directSleep.Data?.StandardOutput}");
Console.WriteLine($"directSleep.stderr={directSleep.Data?.StandardError}");

if (snapshot.ProcessId is not null)
{
    var processProbe = await tools.DangerousRawExecAsync(
        targetName,
        ["bash", "-lc", $"ps -p {snapshot.ProcessId.Value} -o pid=,ppid=,stat=,cmd= || true"],
        timeoutSeconds: 30);
    Console.WriteLine($"ps.ok={processProbe.Ok}");
    Console.WriteLine($"ps.stdout={processProbe.Data?.StandardOutput}");
    Console.WriteLine($"ps.stderr={processProbe.Data?.StandardError}");
}

var cancelled = await remoteJobRunner.CancelAsync(target, started.OperationId, TimeSpan.FromSeconds(1), CancellationToken.None);
PrintSnapshot("after-cancel", cancelled);

var waited = await remoteJobRunner.WaitAsync(target, started.OperationId, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(2), CancellationToken.None);
Console.WriteLine($"wait.completed={waited.Completed}");
PrintSnapshot("after-wait", waited.Snapshot);

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

static void PrintSnapshot(string label, RemoteOperationSnapshot snapshot)
{
    Console.WriteLine($"[{label}] state={snapshot.State} summary={snapshot.Summary} exitCode={snapshot.ExitCode?.ToString() ?? "null"} pid={snapshot.ProcessId?.ToString() ?? "null"}");
}

static async Task PrintRemoteTextAsync(ISshTransport transport, ResolvedTargetConfiguration target, string path, string label, int maxBytes)
{
    var stat = await transport.StatAsync(target, path, CancellationToken.None);
    if (!stat.Exists || stat.IsDirectory)
    {
        Console.WriteLine($"{label}=<missing>");
        return;
    }

    var content = await transport.ReadTextAsync(target, path, maxBytes, CancellationToken.None);
    Console.WriteLine($"{label}<<EOF");
    Console.WriteLine(content);
    Console.WriteLine("EOF");
}
