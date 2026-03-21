namespace CanDoItAll.Mcp.SshOps.Coordination;

public sealed partial class TargetCoordinator
{
    public Task<SshOpsToolResult<TargetsListData>> TargetsListAsync(string correlationId, CancellationToken cancellationToken)
    {
        var targets = targetCatalog.GetAll()
            .Select(target => new TargetDescriptor(
                target.Name,
                target.Host,
                !string.Equals(target.Sudo.Mode, "none", StringComparison.OrdinalIgnoreCase),
                target.AllowedRoots,
                new TargetCapabilitySummary(
                    target.Guards.AllowBootstrap,
                    true,
                    runtimeConfiguration.Options.Revisions.Enabled,
                    target.Guards.AllowRawExec && runtimeConfiguration.Options.Server.AllowDangerousRawExec)))
            .ToArray();

        return Task.FromResult(Result(
            new TargetsListData(targets),
            status: "success",
            summary: $"{targets.Length} configured SSH target(s) available."));
    }

    public async Task<SshOpsToolResult<TargetTestData>> TargetTestAsync(
        string correlationId,
        string targetName,
        CancellationToken cancellationToken)
    {
        var target = targetCatalog.GetRequired(targetName);
        var fingerprint = await transport.GetHostFingerprintAsync(target, cancellationToken);
        var userResult = await transport.ExecuteAsync(target, ["id", "-un"], new RemoteExecutionOptions(), cancellationToken);
        EnsureSuccess(userResult, "AuthenticationFailed", "Could not read the remote user identity.");
        var bannerResult = await transport.ExecuteAsync(target, ["uname", "-srmo"], new RemoteExecutionOptions(), cancellationToken);
        EnsureSuccess(bannerResult, "ValidationFailed", "Could not read the remote host banner.");

        var data = new TargetTestData(
            Verified: true,
            RemoteUser: userResult.StandardOutput.Trim(),
            FingerprintSha256: fingerprint,
            AuthenticationMethod: ResolveAuthenticationMethod(target),
            Banner: bannerResult.StandardOutput.Trim());

        return Result(
            data,
            target: target.Name,
            status: "success",
            summary: "SSH connectivity and host verification passed.",
            nextSuggestedTools: ["target_audit"]);
    }

    public async Task<SshOpsToolResult<TargetAuditData>> TargetAuditAsync(
        string correlationId,
        string targetName,
        bool includePorts,
        bool includeDocker,
        bool includeDisk,
        CancellationToken cancellationToken)
    {
        var target = targetCatalog.GetRequired(targetName);
        var warnings = new List<string>();
        var blockers = new List<string>();

        var osResult = await RunRemoteShellAsync(
            target,
            """
            source /etc/os-release >/dev/null 2>&1 || true
            printf '%s|%s|' "${NAME:-unknown}" "${VERSION_ID:-unknown}"
            uname -r
            """,
            timeout: TimeSpan.FromSeconds(20),
            cancellationToken: cancellationToken);
        EnsureSuccess(osResult, "ValidationFailed", "Could not read the remote operating system information.");
        var osParts = osResult.StandardOutput.Trim().Split('|', 3, StringSplitOptions.TrimEntries);
        var osData = new AuditOsData(
            Distribution: osParts.ElementAtOrDefault(0) ?? "unknown",
            Version: osParts.ElementAtOrDefault(1) ?? "unknown",
            Kernel: osParts.ElementAtOrDefault(2) ?? "unknown");

        var sudoAvailable = await CheckSudoAvailabilityAsync(target, cancellationToken);
        if (!sudoAvailable && !string.Equals(target.Sudo.Mode, "none", StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add("Configured sudo mode is not available non-interactively on the target.");
        }

        AuditDockerData dockerData;
        if (includeDocker)
        {
            dockerData = await GetDockerAuditAsync(target, cancellationToken);
            if (!dockerData.Installed)
            {
                warnings.Add("Docker is not installed or not reachable on the target.");
            }
            else
            {
                if (string.IsNullOrWhiteSpace(dockerData.ComposeVersion))
                {
                    warnings.Add("A usable Docker Compose command was not detected on the target.");
                }

                foreach (var networkName in await GetMissingRequiredNetworksAsync(target, cancellationToken))
                {
                    warnings.Add($"Required Docker network '{networkName}' is missing.");
                }
            }
        }
        else
        {
            dockerData = new AuditDockerData(false, null, null);
        }

        var portData = includePorts
            ? await Task.WhenAll(new[] { 22, 80, 443, 4001, 5001 }.Select(port => GetPortDataAsync(target, port, cancellationToken)))
            : Array.Empty<AuditPortData>();

        var diskData = includeDisk
            ? await GetDiskDataAsync(target, cancellationToken)
            : null;

        var directories = await Task.WhenAll(
            new[]
            {
                target.RemoteStateRoot,
                target.StacksRoot,
                target.SecretsRoot
            }.Select(path => GetDirectoryAuditAsync(target, path, cancellationToken)));

        var toolAvailability = await Task.WhenAll(
            new[] { "bash", "curl", "tar", "ss" }
                .Select(tool => GetToolAvailabilityAsync(target, tool, cancellationToken)));

        if (toolAvailability.Any(tool => !tool.Available))
        {
            blockers.Add("One or more required base utilities are missing from the target.");
        }

        var status = blockers.Count > 0
            ? "blocked"
            : warnings.Count > 0
                ? "degraded"
                : "ready";

        return Result(
            new TargetAuditData(
                osData,
                new AuditSudoData(sudoAvailable, target.Sudo.Command),
                dockerData,
                portData,
                diskData,
                directories,
                toolAvailability,
                warnings,
                blockers),
            target: target.Name,
            status: status,
            summary: blockers.Count > 0
                ? "Target audit found blockers."
                : warnings.Count > 0
                    ? "Target audit completed with warnings."
                    : "Target audit completed without blockers.",
            nextSuggestedTools: blockers.Count == 0 ? ["host_bootstrap_prepare", "fs_apply_bundle"] : null,
            warnings: warnings);
    }

    public async Task<SshOpsToolResult<HostBootstrapData>> HostBootstrapPrepareAsync(
        string correlationId,
        string targetName,
        string mode,
        bool installDockerFromOfficialRepo,
        bool createBaseDirectories,
        bool createProxyNetwork,
        bool enableDockerOnBoot,
        string executionMode,
        CancellationToken cancellationToken)
    {
        var target = targetCatalog.GetRequired(targetName);
        if (!target.Guards.AllowBootstrap)
        {
            throw new ToolInvocationException("PolicyBlocked", $"Target '{target.Name}' does not allow host bootstrap actions.");
        }

        await using var lease = await AcquireMutationLeaseAsync(target, "host_bootstrap_prepare", cancellationToken);
        await EnsureNoRunningOperationsAsync(target, cancellationToken);

        var useSudoForBootstrap = !string.Equals(target.Sudo.Mode, "none", StringComparison.OrdinalIgnoreCase) &&
                                  (installDockerFromOfficialRepo || createBaseDirectories || createProxyNetwork || enableDockerOnBoot);
        var requiresSudo = installDockerFromOfficialRepo || enableDockerOnBoot || useSudoForBootstrap;
        if (requiresSudo && string.Equals(target.Sudo.Mode, "none", StringComparison.OrdinalIgnoreCase))
        {
            throw new ToolInvocationException("SudoRequired", $"Target '{target.Name}' requires non-interactive sudo for the requested bootstrap mode.");
        }

        var scriptLines = new List<string>();
        if (createBaseDirectories)
        {
            scriptLines.Add($"mkdir -p {QuoteShell(target.RemoteStateRoot)} {QuoteShell(target.StacksRoot)} {QuoteShell(target.SecretsRoot)} {QuoteShell(target.RemoteStateRoot + "/jobs")} {QuoteShell(target.RemoteStateRoot + "/backups")} {QuoteShell(target.RemoteStateRoot + "/revisions")}");
            if (useSudoForBootstrap)
            {
                scriptLines.Add($"chown {QuoteShell($"{target.User}:{target.User}")} {QuoteShell(target.RemoteStateRoot)} {QuoteShell(target.StacksRoot)} {QuoteShell(target.SecretsRoot)} {QuoteShell(target.RemoteStateRoot + "/jobs")} {QuoteShell(target.RemoteStateRoot + "/backups")} {QuoteShell(target.RemoteStateRoot + "/revisions")}");
            }
        }

        if (installDockerFromOfficialRepo)
        {
            scriptLines.Add("command -v docker >/dev/null 2>&1 || curl -fsSL https://get.docker.com | sh");
        }

        if (enableDockerOnBoot)
        {
            scriptLines.Add("systemctl enable docker");
        }

        if (createProxyNetwork)
        {
            foreach (var networkName in target.Docker.RequiredNetworks.DefaultIfEmpty("proxy"))
            {
                scriptLines.Add($"docker network inspect {QuoteShell(networkName)} >/dev/null 2>&1 || docker network create {QuoteShell(networkName)}");
            }
        }

        if (scriptLines.Count == 0)
        {
            scriptLines.Add("true");
        }

        var script = string.Join(" && ", scriptLines);
        var resolvedExecutionMode = ResolveExecutionMode(executionMode);
        var data = new HostBootstrapData(mode, createBaseDirectories, createProxyNetwork);

        if (resolvedExecutionMode == "detached")
        {
            var operation = await remoteJobRunner.StartAsync(
                target,
                new RemoteJobStartRequest(
                    correlationId,
                    Kind: "host_bootstrap_prepare",
                    InitialSummary: "Bootstrap preparation queued.",
                    SuccessSummary: "Bootstrap preparation completed.",
                    FailureSummary: "Bootstrap preparation failed.",
                    CancelSummary: "Bootstrap preparation was cancelled.",
                    Command: ["bash", "-lc", script],
                    UseSudo: useSudoForBootstrap),
                cancellationToken);

            SaveOperationMetadata(new OperationTrackingMetadata(operation.OperationId, target.Name, "target", "host_bootstrap_prepare", DateTimeOffset.UtcNow));
            return Result(
                data,
                target: target.Name,
                operationId: operation.OperationId,
                status: "accepted",
                summary: "Bootstrap preparation started as a detached operation.",
                nextSuggestedTools: ["operation_wait", "target_audit"]);
        }

        var result = await transport.ExecuteAsync(
            target,
            ["bash", "-lc", script],
            new RemoteExecutionOptions(UseSudo: useSudoForBootstrap, Timeout: runtimeConfiguration.DefaultComposeApplyTimeout),
            cancellationToken);
        EnsureSuccess(result, "ValidationFailed", "Bootstrap preparation failed.");

        return Result(
            data,
            target: target.Name,
            status: "success",
            summary: "Bootstrap preparation completed.",
            nextSuggestedTools: ["target_audit"]);
    }

    private async Task<AuditDockerData> GetDockerAuditAsync(ResolvedTargetConfiguration target, CancellationToken cancellationToken)
    {
        var dockerVersion = await transport.ExecuteAsync(target, ["docker", "--version"], new RemoteExecutionOptions(), cancellationToken);
        if (dockerVersion.ExitCode != 0)
        {
            return new AuditDockerData(false, null, null);
        }

        var composeVersion = await transport.ExecuteAsync(target, BuildComposeCommand(target, null, null, ["version"]), new RemoteExecutionOptions(), cancellationToken);
        if (composeVersion.ExitCode != 0 && LooksLikeComposeCommandUnavailable(composeVersion))
        {
            composeVersion = await ExecuteComposeCommandAsync(target, null, null, ["version"], new RemoteExecutionOptions(), cancellationToken, throwIfUnavailable: false);
        }

        return new AuditDockerData(
            Installed: true,
            Version: dockerVersion.StandardOutput.Trim(),
            ComposeVersion: composeVersion.ExitCode == 0 ? composeVersion.StandardOutput.Trim() : null);
    }

    private async Task<IReadOnlyList<string>> GetMissingRequiredNetworksAsync(ResolvedTargetConfiguration target, CancellationToken cancellationToken)
    {
        var missingNetworks = new List<string>();
        foreach (var networkName in target.Docker.RequiredNetworks.Where(static value => !string.IsNullOrWhiteSpace(value)))
        {
            var inspectResult = await transport.ExecuteAsync(target, ["docker", "network", "inspect", networkName], new RemoteExecutionOptions(), cancellationToken);
            if (inspectResult.ExitCode != 0)
            {
                missingNetworks.Add(networkName);
            }
        }

        return missingNetworks;
    }

    private async Task<AuditPortData> GetPortDataAsync(ResolvedTargetConfiguration target, int port, CancellationToken cancellationToken)
    {
        var result = await RunRemoteShellAsync(
            target,
            $"ss -ltnH '( sport = :{port} )' | grep -q .",
            timeout: TimeSpan.FromSeconds(10),
            cancellationToken: cancellationToken);

        return new AuditPortData(port, result.ExitCode == 0);
    }

    private async Task<AuditDiskData?> GetDiskDataAsync(ResolvedTargetConfiguration target, CancellationToken cancellationToken)
    {
        var result = await RunRemoteShellAsync(
            target,
            "df -Pk / | tail -n 1 | awk '{print $2\"|\"$4\"|\"$6}'",
            timeout: TimeSpan.FromSeconds(10),
            cancellationToken: cancellationToken);
        if (result.ExitCode != 0)
        {
            return null;
        }

        var parts = result.StandardOutput.Trim().Split('|', 3, StringSplitOptions.TrimEntries);
        if (parts.Length != 3 ||
            !long.TryParse(parts[0], out var totalKb) ||
            !long.TryParse(parts[1], out var availableKb))
        {
            return null;
        }

        return new AuditDiskData(availableKb / 1024, totalKb / 1024, parts[2]);
    }

    private async Task<AuditDirectoryData> GetDirectoryAuditAsync(ResolvedTargetConfiguration target, string path, CancellationToken cancellationToken)
    {
        var stat = await transport.StatAsync(target, path, cancellationToken);
        return new AuditDirectoryData(path, stat.Exists && stat.IsDirectory);
    }

    private async Task<AuditToolData> GetToolAvailabilityAsync(ResolvedTargetConfiguration target, string name, CancellationToken cancellationToken)
    {
        var result = await RunRemoteShellAsync(
            target,
            $"command -v {QuoteShell(name)} >/dev/null 2>&1",
            timeout: TimeSpan.FromSeconds(10),
            cancellationToken: cancellationToken);
        return new AuditToolData(name, result.ExitCode == 0);
    }

    private async Task<bool> CheckSudoAvailabilityAsync(ResolvedTargetConfiguration target, CancellationToken cancellationToken)
    {
        if (string.Equals(target.Sudo.Mode, "none", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var result = await transport.ExecuteAsync(
            target,
            ["true"],
            new RemoteExecutionOptions(UseSudo: true, Timeout: TimeSpan.FromSeconds(10)),
            cancellationToken);
        return result.ExitCode == 0;
    }
}
