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

var tracker = new ValidationTracker();
const string targetName = "rpi3-test";
const string projectName = "qa-validation";
const string scratchRoot = "/home/pi/candoitall/stacks/qa-validation";
const string composeFilePath = "/home/pi/candoitall/stacks/qa-validation/docker-compose.yml";
const string envFilePath = "/home/pi/candoitall/stacks/qa-validation/compose.env";
const string repoIpfsRoot = "/home/pi/candoitall/stacks/candoitall/ipfs";
const string networkName = "qa_validation_net";
const string volumeName = "qa_validation_pgdata";

try
{
    using var host = BuildHost();
    var tools = host.Services.GetRequiredService<SshOpsTools>();
    var targetCatalog = host.Services.GetRequiredService<TargetCatalog>();
    var remoteJobRunner = host.Services.GetRequiredService<RemoteJobRunner>();
    var target = targetCatalog.GetRequired(targetName);

    await CleanupScratchAsync(tools);

    var targets = await tools.TargetsListAsync();
    tracker.Expect(targets.Ok && targets.Data?.Targets.Any(static item => string.Equals(item.Name, targetName, StringComparison.OrdinalIgnoreCase)) == true, "targets_list", targets.Summary);

    var targetTest = await tools.TargetTestAsync(targetName);
    tracker.Expect(targetTest.Ok && targetTest.Data?.Verified == true, "target_test", targetTest.Summary);

    var targetAudit = await tools.TargetAuditAsync(targetName, includePorts: true, includeDocker: true, includeDisk: true);
    tracker.Expect(targetAudit.Ok && !string.IsNullOrWhiteSpace(targetAudit.Data?.Docker.ComposeVersion), "target_audit", targetAudit.Summary);

    var bundle = await tools.FsApplyBundleAsync(
        targetName,
        [
            new("/home/pi/candoitall/stacks/qa-validation/docker-compose.yml", "utf8", ComposeFile(volumeName, networkName), BackupBeforeWrite: false),
            new("/home/pi/candoitall/stacks/qa-validation/compose.env", "utf8", "TEST=1\n", BackupBeforeWrite: false)
        ]);
    tracker.Expect(bundle.Ok && bundle.Data?.Written == 2, "fs_apply_bundle", bundle.Summary);

    var readText = await tools.FsReadTextAsync(targetName, composeFilePath);
    tracker.Expect(readText.Ok && readText.Data?.Content.Contains("qa_validation_pgdata", StringComparison.Ordinal) == true, "fs_read_text", readText.Summary);

    var backup = await tools.FsBackupPathAsync(targetName, envFilePath, "runner-backup");
    tracker.Expect(backup.Ok && !string.IsNullOrWhiteSpace(backup.Data?.BackupId), "fs_backup_path", backup.Summary);

    var changedEnv = await tools.FsApplyBundleAsync(
        targetName,
        [new(envFilePath, "utf8", "TEST=2\n", BackupBeforeWrite: false)]);
    tracker.Expect(changedEnv.Ok, "fs_apply_bundle update", changedEnv.Summary);

    var restore = await tools.FsRestoreBackupAsync(targetName, backup.Data!.BackupId);
    tracker.Expect(restore.Ok, "fs_restore_backup", restore.Summary);

    var network = await tools.DockerNetworkEnsureAsync(targetName, networkName);
    tracker.Expect(network.Ok && network.Data?.Exists == true, "docker_network_ensure", network.Summary);

    var volume = await tools.DockerVolumeEnsureAsync(targetName, volumeName);
    tracker.Expect(volume.Ok, "docker_volume_ensure", volume.Summary);

    var composeValidate = await tools.ComposeValidateAsync(targetName, composeFilePath, projectName, scratchRoot);
    tracker.Expect(composeValidate.Ok && composeValidate.Data?.Valid == true, "compose_validate", composeValidate.Summary);

    var composeApply = await tools.ComposeApplyAsync(
        targetName,
        stackName: projectName,
        composeFile: composeFilePath,
        projectName: projectName,
        workingDirectory: scratchRoot,
        pull: false,
        build: false,
        removeOrphans: true,
        executionMode: "sync",
        postWaitPolicy: new ComposeWaitPolicy(["db"], 90));
    tracker.Expect(composeApply.Ok, "compose_apply", DescribeEnvelope(composeApply));

    var postgresReady = await tools.PostgresReadyAsync(targetName, composeFilePath, projectName, scratchRoot, "db", "postgres", "postgres", timeoutSeconds: 180);
    tracker.Expect(postgresReady.Ok && postgresReady.Data?.Ready == true, "postgres_ready", postgresReady.Summary);

    var scratchHttp = await tools.HttpWaitAsync(targetName, "remote", "http://127.0.0.1:18080", [200], timeoutSeconds: 60, pollIntervalSeconds: 2);
    tracker.Expect(scratchHttp.Ok && scratchHttp.Data?.Ready == true, "http_wait scratch", scratchHttp.Summary);

    var composePs = await tools.ComposePsAsync(targetName, composeFilePath, projectName, scratchRoot);
    tracker.Expect(composePs.Ok && composePs.Status != "degraded", "compose_ps", composePs.Summary);

    var composeLogs = await tools.ComposeLogsAsync(targetName, composeFilePath, projectName, scratchRoot, service: "hello", tail: 50, sinceSeconds: 300);
    tracker.Expect(composeLogs.Ok, "compose_logs", DescribeEnvelope(composeLogs));

    var composeExecAllowed = await tools.ComposeExecAsync(targetName, composeFilePath, projectName, scratchRoot, "db", ["pg_isready", "-U", "postgres", "-d", "postgres"], timeoutSeconds: 30);
    tracker.Expect(composeExecAllowed.Ok && composeExecAllowed.Data?.ExitCode == 0, "compose_exec allowed", composeExecAllowed.Summary);

    var composeExecBlocked = await tools.ComposeExecAsync(targetName, composeFilePath, projectName, scratchRoot, "db", ["bash", "-lc", "echo blocked"], timeoutSeconds: 30);
    tracker.Expect(!composeExecBlocked.Ok && string.Equals(composeExecBlocked.Status, "policy_blocked", StringComparison.OrdinalIgnoreCase), "compose_exec blocked", composeExecBlocked.Summary);

    var badCompose = await tools.FsApplyBundleAsync(
        targetName,
        [new(composeFilePath, "utf8", BrokenComposeFile(volumeName, networkName), BackupBeforeWrite: false)]);
    tracker.Expect(badCompose.Ok, "fs_apply_bundle broken compose", badCompose.Summary);

    var brokenApply = await tools.ComposeApplyAsync(
        targetName,
        stackName: projectName,
        composeFile: composeFilePath,
        projectName: projectName,
        workingDirectory: scratchRoot,
        pull: true,
        build: false,
        removeOrphans: true,
        executionMode: "sync");
    tracker.Expect(!brokenApply.Ok, "compose_apply broken", DescribeEnvelope(brokenApply));

    var rollback = await tools.StackRollbackAsync(targetName, projectName, strategy: "last-known-good", executionMode: "sync");
    tracker.Expect(rollback.Ok, "stack_rollback", DescribeEnvelope(rollback));

    var postgresReadyAfterRollback = await tools.PostgresReadyAsync(targetName, composeFilePath, projectName, scratchRoot, "db", "postgres", "postgres", timeoutSeconds: 180);
    tracker.Expect(postgresReadyAfterRollback.Ok && postgresReadyAfterRollback.Data?.Ready == true, "postgres_ready after rollback", postgresReadyAfterRollback.Summary);

    var bootstrap = await tools.HostBootstrapPrepareAsync(
        targetName,
        mode: "layout-only",
        installDockerFromOfficialRepo: false,
        createBaseDirectories: false,
        createProxyNetwork: false,
        enableDockerOnBoot: false,
        executionMode: "detached");
    tracker.Expect(bootstrap.Ok && !string.IsNullOrWhiteSpace(bootstrap.OperationId), "host_bootstrap_prepare", bootstrap.Summary);

    var bootstrapWait = await tools.OperationWaitAsync(targetName, bootstrap.OperationId!, timeoutSeconds: 60, pollIntervalSeconds: 2);
    tracker.Expect(bootstrapWait.Ok && bootstrapWait.Data?.Completed == true, "operation_wait bootstrap", bootstrapWait.Summary);

    var customOperation = await remoteJobRunner.StartAsync(
        target,
        new RemoteJobStartRequest(
            CorrelationId: "corr_remote_validation_runner",
            Kind: "validation_custom_job",
            InitialSummary: "Validation custom job queued.",
            SuccessSummary: "Validation custom job completed.",
            FailureSummary: "Validation custom job failed.",
            CancelSummary: "Validation custom job cancelled.",
            Command: ["sleep", "120"],
            WorkingDirectory: "/home/pi"),
        CancellationToken.None);

    await Task.Delay(TimeSpan.FromSeconds(2));

    var operationStatus = await tools.OperationStatusAsync(targetName, customOperation.OperationId);
    tracker.Expect(operationStatus.Ok && string.Equals(operationStatus.Data?.State, "running", StringComparison.OrdinalIgnoreCase), "operation_status custom", DescribeEnvelope(operationStatus));

    var operationLogs = await tools.OperationLogsAsync(targetName, customOperation.OperationId, stream: "stdout", cursor: 0, maxBytes: 4096);
    tracker.Expect(operationLogs.Ok, "operation_logs custom", operationLogs.Summary);

    var operationCancel = await tools.OperationCancelAsync(targetName, customOperation.OperationId, graceSeconds: 1);
    tracker.Expect(operationCancel.Ok, "operation_cancel custom", DescribeEnvelope(operationCancel));

    var operationWait = await tools.OperationWaitAsync(targetName, customOperation.OperationId, timeoutSeconds: 60, pollIntervalSeconds: 2);
    tracker.Expect(operationWait.Ok && operationWait.Data?.Completed == true && string.Equals(operationWait.Data.State, "cancelled", StringComparison.OrdinalIgnoreCase), "operation_wait custom", DescribeEnvelope(operationWait));

    var localProbe = await tools.HttpProbeAsync(targetName, "local", "http://10.190.32.143", [200, 301, 302], timeoutSeconds: 30);
    tracker.Expect(localProbe.Ok && localProbe.Data?.Success == true, "http_probe local", localProbe.Summary);

    var remoteProbe = await tools.HttpProbeAsync(targetName, "remote", "http://127.0.0.1", [301], timeoutSeconds: 30);
    tracker.Expect(remoteProbe.Ok && remoteProbe.Data is { Success: true, DurationMs: > 0 }, "http_probe remote", remoteProbe.Summary);

    var certCheck = await tools.CertCheckAsync(targetName, "10.190.32.143", "local");
    tracker.Expect(certCheck.Ok && certCheck.Data?.CertificateReady == true, "cert_check", certCheck.Summary);

    var ipfsStatus = await tools.IpfsStatusAsync(targetName, apiUrl: null);
    tracker.Expect(ipfsStatus.Ok && ipfsStatus.Data?.DaemonReady == true, "ipfs_status", ipfsStatus.Summary);

    var ipfsPrivate = await tools.IpfsPrivateValidateAsync(targetName, expectedBootstrapPeers: [], minimumPeerCount: 0, apiUrl: null, repoRoot: repoIpfsRoot);
    tracker.Expect(ipfsPrivate.Ok && ipfsPrivate.Data?.PrivateMode == true, "ipfs_private_validate", ipfsPrivate.Summary);

    var dangerousRawExec = await tools.DangerousRawExecAsync(targetName, ["bash", "-lc", "docker-compose version"], timeoutSeconds: 30);
    tracker.Expect(dangerousRawExec.Ok && dangerousRawExec.Data?.ExitCode == 0, "dangerous_raw_exec", dangerousRawExec.Summary);

    var composeDown = await tools.ComposeDownAsync(targetName, composeFilePath, projectName, scratchRoot, removeOrphans: true);
    tracker.Expect(composeDown.Ok, "compose_down", composeDown.Summary);

    await CleanupScratchAsync(tools);

    tracker.Print();
    return tracker.HasFailures ? 1 : 0;
}
catch (Exception ex)
{
    tracker.RecordFailure("runner_exception", ex.ToString());
    tracker.Print();
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

static string ComposeFile(string volumeName, string networkName)
{
    return $$"""
        version: '3.8'
        services:
          hello:
            image: nginx:alpine
            ports:
              - "18080:80"
            networks:
              - {{networkName}}
          db:
            image: postgres:16-alpine
            environment:
              POSTGRES_PASSWORD: postgres
              POSTGRES_DB: postgres
              POSTGRES_USER: postgres
            healthcheck:
              test: ["CMD-SHELL", "pg_isready -U postgres -d postgres"]
              interval: 5s
              timeout: 3s
              retries: 20
            volumes:
              - {{volumeName}}:/var/lib/postgresql/data
            networks:
              - {{networkName}}
        volumes:
          {{volumeName}}:
            external: true
        networks:
          {{networkName}}:
            external: true
        """;
}

static string BrokenComposeFile(string volumeName, string networkName)
{
    return ComposeFile(volumeName, networkName).Replace("nginx:alpine", "this-image-should-not-exist:latest", StringComparison.Ordinal);
}

async Task CleanupScratchAsync(SshOpsTools tools)
{
    await tools.DangerousRawExecAsync(
        targetName,
        [
            "bash",
            "-lc",
            $"""
            docker-compose -p {projectName} -f {composeFilePath} down --remove-orphans >/dev/null 2>&1 || true
            docker rm -f {projectName}-hello-1 {projectName}-db-1 >/dev/null 2>&1 || true
            rm -rf {scratchRoot}
            """
        ],
        timeoutSeconds: 60);
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

sealed class ValidationTracker
{
    private readonly List<(bool Passed, string Name, string Detail)> _results = [];

    public bool HasFailures => _results.Any(static result => !result.Passed);

    public void Expect(bool condition, string name, string? detail)
    {
        var normalizedDetail = detail ?? string.Empty;
        _results.Add((condition, name, normalizedDetail));
        if (!condition)
        {
            throw new InvalidOperationException($"{name} failed: {normalizedDetail}");
        }
    }

    public void RecordFailure(string name, string detail)
    {
        _results.Add((false, name, detail));
    }

    public void Print()
    {
        foreach (var result in _results)
        {
            Console.WriteLine($"{(result.Passed ? "PASS" : "FAIL")} | {result.Name} | {result.Detail}");
        }
    }
}
