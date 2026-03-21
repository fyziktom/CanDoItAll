using System.ComponentModel;
using CanDoItAll.Mcp.SshOps.Coordination;
using ModelContextProtocol.Server;

namespace CanDoItAll.Mcp.SshOps.Tools;

[McpServerToolType]
public sealed class SshOpsTools(TargetCoordinator coordinator, ILogger<SshOpsTools> logger)
{
    [McpServerTool(Name = "targets_list", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Lists configured SSH deployment targets and their capability summary.")]
    public Task<McpToolEnvelope<TargetsListData>> TargetsListAsync(CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("targets_list", correlationId => coordinator.TargetsListAsync(correlationId, cancellationToken));
    }

    [McpServerTool(Name = "target_test", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Validates SSH connectivity, host identity, and remote user resolution without mutating the host.")]
    public Task<McpToolEnvelope<TargetTestData>> TargetTestAsync(string target, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("target_test", correlationId => coordinator.TargetTestAsync(correlationId, target, cancellationToken), target);
    }

    [McpServerTool(Name = "target_audit", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Audits target readiness including OS, sudo, docker, ports, disk, directories, and base tool availability.")]
    public Task<McpToolEnvelope<TargetAuditData>> TargetAuditAsync(
        string target,
        bool includePorts = true,
        bool includeDocker = true,
        bool includeDisk = true,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("target_audit", correlationId => coordinator.TargetAuditAsync(correlationId, target, includePorts, includeDocker, includeDisk, cancellationToken), target);
    }

    [McpServerTool(Name = "host_bootstrap_prepare", ReadOnly = false, Idempotent = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Prepares a Linux target for deployment by creating directories and optionally installing Docker or the proxy network.")]
    public Task<McpToolEnvelope<HostBootstrapData>> HostBootstrapPrepareAsync(
        string target,
        string mode = "layout-only",
        bool installDockerFromOfficialRepo = false,
        bool createBaseDirectories = true,
        bool createProxyNetwork = false,
        bool enableDockerOnBoot = false,
        string executionMode = "auto",
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("host_bootstrap_prepare", correlationId => coordinator.HostBootstrapPrepareAsync(correlationId, target, mode, installDockerFromOfficialRepo, createBaseDirectories, createProxyNetwork, enableDockerOnBoot, executionMode, cancellationToken), target);
    }

    [McpServerTool(Name = "fs_apply_bundle", ReadOnly = false, Idempotent = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Writes a validated bundle of files to allowed remote paths and optionally creates revision backups before overwriting.")]
    public Task<McpToolEnvelope<FsApplyBundleData>> FsApplyBundleAsync(
        string target,
        RemoteFileBundleEntry[] bundle,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("fs_apply_bundle", correlationId => coordinator.FsApplyBundleAsync(correlationId, target, bundle, cancellationToken), target);
    }

    [McpServerTool(Name = "fs_read_text", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Reads a text file from an allowed remote path.")]
    public Task<McpToolEnvelope<FsReadTextData>> FsReadTextAsync(
        string target,
        string path,
        int maxBytes = 65536,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("fs_read_text", correlationId => coordinator.FsReadTextAsync(correlationId, target, path, maxBytes, cancellationToken), target);
    }

    [McpServerTool(Name = "fs_backup_path", ReadOnly = false, Idempotent = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Creates an explicit backup of an allowed remote file or directory.")]
    public Task<McpToolEnvelope<FsBackupPathData>> FsBackupPathAsync(
        string target,
        string path,
        string? label = null,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("fs_backup_path", correlationId => coordinator.FsBackupPathAsync(correlationId, target, path, label, cancellationToken), target);
    }

    [McpServerTool(Name = "fs_restore_backup", ReadOnly = false, Idempotent = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Restores a previously created backup.")]
    public Task<McpToolEnvelope<FsRestoreBackupData>> FsRestoreBackupAsync(
        string target,
        string backupId,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("fs_restore_backup", correlationId => coordinator.FsRestoreBackupAsync(correlationId, target, backupId, cancellationToken), target);
    }

    [McpServerTool(Name = "docker_network_ensure", ReadOnly = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Ensures that a Docker network exists on the remote host.")]
    public Task<McpToolEnvelope<DockerNetworkEnsureData>> DockerNetworkEnsureAsync(
        string target,
        string name,
        string driver = "bridge",
        bool internalNetwork = false,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("docker_network_ensure", correlationId => coordinator.DockerNetworkEnsureAsync(correlationId, target, name, driver, internalNetwork, cancellationToken), target);
    }

    [McpServerTool(Name = "docker_volume_ensure", ReadOnly = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Ensures that a Docker volume exists on the remote host.")]
    public Task<McpToolEnvelope<DockerVolumeEnsureData>> DockerVolumeEnsureAsync(
        string target,
        string name,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("docker_volume_ensure", correlationId => coordinator.DockerVolumeEnsureAsync(correlationId, target, name, cancellationToken), target);
    }

    [McpServerTool(Name = "compose_validate", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Runs docker compose config and returns validation status with a normalized preview.")]
    public Task<McpToolEnvelope<ComposeValidateData>> ComposeValidateAsync(
        string target,
        string composeFile,
        string projectName,
        string workingDirectory,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("compose_validate", correlationId => coordinator.ComposeValidateAsync(correlationId, target, composeFile, projectName, workingDirectory, cancellationToken), target);
    }

    [McpServerTool(Name = "compose_apply", ReadOnly = false, Idempotent = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Applies a docker compose stack, typically as a detached remote operation.")]
    public Task<McpToolEnvelope<ComposeApplyData>> ComposeApplyAsync(
        string target,
        string stackName,
        string composeFile,
        string projectName,
        string workingDirectory,
        bool pull = true,
        bool build = false,
        bool removeOrphans = true,
        string executionMode = "auto",
        ComposeWaitPolicy? postWaitPolicy = null,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("compose_apply", correlationId => coordinator.ComposeApplyAsync(correlationId, target, stackName, composeFile, projectName, workingDirectory, pull, build, removeOrphans, executionMode, postWaitPolicy, cancellationToken), target);
    }

    [McpServerTool(Name = "compose_ps", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Reads the runtime state of services in a docker compose project.")]
    public Task<McpToolEnvelope<ComposePsData>> ComposePsAsync(
        string target,
        string composeFile,
        string projectName,
        string workingDirectory,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("compose_ps", correlationId => coordinator.ComposePsAsync(correlationId, target, composeFile, projectName, workingDirectory, cancellationToken), target);
    }

    [McpServerTool(Name = "compose_logs", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Reads recent logs from a docker compose project or service.")]
    public Task<McpToolEnvelope<ComposeLogsData>> ComposeLogsAsync(
        string target,
        string composeFile,
        string projectName,
        string workingDirectory,
        string? service = null,
        int tail = 200,
        int sinceSeconds = 600,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("compose_logs", correlationId => coordinator.ComposeLogsAsync(correlationId, target, composeFile, projectName, workingDirectory, service, tail, sinceSeconds, cancellationToken), target);
    }

    [McpServerTool(Name = "compose_exec", ReadOnly = false, Idempotent = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Executes a command inside a docker compose service.")]
    public Task<McpToolEnvelope<ComposeExecData>> ComposeExecAsync(
        string target,
        string composeFile,
        string projectName,
        string workingDirectory,
        string service,
        string[] command,
        int timeoutSeconds = 30,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("compose_exec", correlationId => coordinator.ComposeExecAsync(correlationId, target, composeFile, projectName, workingDirectory, service, command, timeoutSeconds, cancellationToken), target);
    }

    [McpServerTool(Name = "compose_down", ReadOnly = false, Idempotent = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Stops and removes a docker compose stack.")]
    public Task<McpToolEnvelope<ComposeDownData>> ComposeDownAsync(
        string target,
        string composeFile,
        string projectName,
        string workingDirectory,
        bool removeOrphans = true,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("compose_down", correlationId => coordinator.ComposeDownAsync(correlationId, target, composeFile, projectName, workingDirectory, removeOrphans, cancellationToken), target);
    }

    [McpServerTool(Name = "stack_rollback", ReadOnly = false, Idempotent = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Restores the latest known good revision of a stack and restarts it.")]
    public Task<McpToolEnvelope<StackRollbackData>> StackRollbackAsync(
        string target,
        string stackName,
        string strategy = "last-known-good",
        string executionMode = "auto",
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("stack_rollback", correlationId => coordinator.StackRollbackAsync(correlationId, target, stackName, strategy, executionMode, cancellationToken), target);
    }

    [McpServerTool(Name = "http_probe", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Probes an HTTP or HTTPS endpoint either locally or from the remote host.")]
    public Task<McpToolEnvelope<HttpProbeData>> HttpProbeAsync(
        string target,
        string origin,
        string url,
        int[]? expectedStatuses = null,
        int timeoutSeconds = 20,
        bool allowInsecureTls = false,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("http_probe", correlationId => coordinator.HttpProbeAsync(correlationId, target, origin, url, expectedStatuses, timeoutSeconds, allowInsecureTls, cancellationToken), target);
    }

    [McpServerTool(Name = "http_wait", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Waits for an HTTP or HTTPS endpoint to satisfy the expected status conditions.")]
    public Task<McpToolEnvelope<HttpWaitData>> HttpWaitAsync(
        string target,
        string origin,
        string url,
        int[]? expectedStatuses = null,
        int timeoutSeconds = 180,
        int pollIntervalSeconds = 5,
        bool allowInsecureTls = false,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("http_wait", correlationId => coordinator.HttpWaitAsync(correlationId, target, origin, url, expectedStatuses, timeoutSeconds, pollIntervalSeconds, allowInsecureTls, cancellationToken), target);
    }

    [McpServerTool(Name = "cert_check", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Inspects the current TLS certificate served by a host or URL.")]
    public Task<McpToolEnvelope<CertCheckData>> CertCheckAsync(
        string target,
        string domain,
        string origin = "local",
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("cert_check", correlationId => coordinator.CertCheckAsync(correlationId, target, domain, origin, cancellationToken), target);
    }

    [McpServerTool(Name = "postgres_ready", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Waits until PostgreSQL reports ready through docker compose exec and pg_isready.")]
    public Task<McpToolEnvelope<PostgresReadyData>> PostgresReadyAsync(
        string target,
        string composeFile,
        string projectName,
        string workingDirectory,
        string service,
        string database,
        string user,
        int timeoutSeconds = 120,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("postgres_ready", correlationId => coordinator.PostgresReadyAsync(correlationId, target, composeFile, projectName, workingDirectory, service, database, user, timeoutSeconds, cancellationToken), target);
    }

    [McpServerTool(Name = "ipfs_status", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Checks the IPFS daemon/API status and returns peer identity information.")]
    public Task<McpToolEnvelope<IpfsStatusData>> IpfsStatusAsync(
        string target,
        string? apiUrl = null,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("ipfs_status", correlationId => coordinator.IpfsStatusAsync(correlationId, target, apiUrl, cancellationToken), target);
    }

    [McpServerTool(Name = "ipfs_private_validate", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Validates that IPFS is configured as a private swarm without public bootstrap peers.")]
    public Task<McpToolEnvelope<IpfsPrivateValidateData>> IpfsPrivateValidateAsync(
        string target,
        string[]? expectedBootstrapPeers = null,
        int minimumPeerCount = 0,
        string? apiUrl = null,
        string? repoRoot = null,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("ipfs_private_validate", correlationId => coordinator.IpfsPrivateValidateAsync(correlationId, target, expectedBootstrapPeers, minimumPeerCount, apiUrl, repoRoot, cancellationToken), target);
    }

    [McpServerTool(Name = "operation_status", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Reads the current snapshot of a detached remote operation.")]
    public Task<McpToolEnvelope<OperationStatusData>> OperationStatusAsync(
        string target,
        string operationId,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("operation_status", correlationId => coordinator.OperationStatusAsync(correlationId, target, operationId, cancellationToken), target);
    }

    [McpServerTool(Name = "operation_wait", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Waits for a detached remote operation to finish.")]
    public Task<McpToolEnvelope<OperationWaitData>> OperationWaitAsync(
        string target,
        string operationId,
        int timeoutSeconds = 900,
        int pollIntervalSeconds = 5,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("operation_wait", correlationId => coordinator.OperationWaitAsync(correlationId, target, operationId, timeoutSeconds, pollIntervalSeconds, cancellationToken), target);
    }

    [McpServerTool(Name = "operation_logs", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Reads incremental logs from a detached remote operation.")]
    public Task<McpToolEnvelope<OperationLogsData>> OperationLogsAsync(
        string target,
        string operationId,
        string stream = "stdout",
        long cursor = 0,
        int maxBytes = 32768,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("operation_logs", correlationId => coordinator.OperationLogsAsync(correlationId, target, operationId, stream, cursor, maxBytes, cancellationToken), target);
    }

    [McpServerTool(Name = "operation_cancel", ReadOnly = false, Idempotent = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Requests cancellation of a detached remote operation.")]
    public Task<McpToolEnvelope<OperationCancelData>> OperationCancelAsync(
        string target,
        string operationId,
        int graceSeconds = 10,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("operation_cancel", correlationId => coordinator.OperationCancelAsync(correlationId, target, operationId, graceSeconds, cancellationToken), target);
    }

    [McpServerTool(Name = "dangerous_raw_exec", ReadOnly = false, Idempotent = false, OpenWorld = true, UseStructuredContent = true)]
    [Description("Executes a break-glass raw command on the remote host when explicitly enabled.")]
    public Task<McpToolEnvelope<DangerousRawExecData>> DangerousRawExecAsync(
        string target,
        string[] command,
        int timeoutSeconds = 30,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("dangerous_raw_exec", correlationId => coordinator.DangerousRawExecAsync(correlationId, target, command, timeoutSeconds, cancellationToken), target);
    }

    private async Task<McpToolEnvelope<T>> ExecuteAsync<T>(
        string toolName,
        Func<string, Task<SshOpsToolResult<T>>> callback,
        string? target = null)
    {
        var correlationId = CorrelationIdFactory.Create();

        try
        {
            var result = await callback(correlationId);
            return McpToolEnvelope<T>.Success(
                tool: toolName,
                correlationId: correlationId,
                data: result.Data,
                warnings: result.Warnings,
                target: result.Target ?? target,
                operationId: result.OperationId,
                status: result.Status,
                summary: result.Summary,
                diagnostics: result.Diagnostics,
                nextSuggestedTools: result.NextSuggestedTools);
        }
        catch (ToolInvocationException ex)
        {
            logger.LogWarning(ex, "{ToolName} failed with a deterministic tool error {Code}.", toolName, ex.Code);
            return McpToolEnvelope<T>.Failure(
                tool: toolName,
                correlationId: correlationId,
                error: new ToolError(ex.Code, ex.Message, ex.Details),
                target: target,
                status: MapFailureStatus(ex.Code),
                summary: ex.Message);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{ToolName} failed unexpectedly.", toolName);
            return McpToolEnvelope<T>.Failure(
                tool: toolName,
                correlationId: correlationId,
                error: new ToolError("InternalError", ex.Message),
                target: target,
                status: "failed",
                summary: "The tool failed unexpectedly.");
        }
    }

    private static string MapFailureStatus(string code)
    {
        return code switch
        {
            "AuthenticationFailed" => "auth_failed",
            "CertificateNotReady" => "certificate_not_ready",
            "ComposePluginMissing" => "compose_unavailable",
            "HostKeyMismatch" => "host_key_mismatch",
            "OperationBusy" => "target_locked",
            "OperationNotFound" => "not_found",
            "PathNotAllowed" => "path_not_allowed",
            "PolicyBlocked" => "policy_blocked",
            "RateLimitLikely" => "rate_limit_likely",
            "RemotePathMissing" => "not_found",
            "RollbackRevisionNotFound" => "not_found",
            "SudoRequired" => "sudo_required",
            "TargetNotConfigured" => "validation_error",
            "TargetNotFound" => "target_not_found",
            "Timeout" => "timeout",
            "ValidationFailed" => "validation_error",
            _ => "failed"
        };
    }
}
