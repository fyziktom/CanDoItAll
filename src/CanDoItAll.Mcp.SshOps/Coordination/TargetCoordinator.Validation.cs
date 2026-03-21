namespace CanDoItAll.Mcp.SshOps.Coordination;

public sealed partial class TargetCoordinator
{
    public async Task<SshOpsToolResult<HttpProbeData>> HttpProbeAsync(
        string correlationId,
        string targetName,
        string origin,
        string url,
        int[]? expectedStatuses,
        int timeoutSeconds,
        bool allowInsecureTls,
        CancellationToken cancellationToken)
    {
        var target = targetCatalog.GetRequired(targetName);
        var data = string.Equals(origin, "remote", StringComparison.OrdinalIgnoreCase)
            ? await ProbeRemoteAsync(target, url, expectedStatuses, timeoutSeconds, allowInsecureTls, captureBody: false, cancellationToken)
            : await ProbeLocalAsync(url, expectedStatuses, timeoutSeconds, allowInsecureTls, captureBody: false, cancellationToken);

        return Result(
            data,
            target: target.Name,
            status: data.Success ? "success" : "failed",
            summary: data.Summary ?? "HTTP probe completed.");
    }

    public async Task<SshOpsToolResult<HttpWaitData>> HttpWaitAsync(
        string correlationId,
        string targetName,
        string origin,
        string url,
        int[]? expectedStatuses,
        int timeoutSeconds,
        int pollIntervalSeconds,
        bool allowInsecureTls,
        CancellationToken cancellationToken)
    {
        var target = targetCatalog.GetRequired(targetName);
        var waitOutcome = await _waitEngine.WaitAsync(
            ct => string.Equals(origin, "remote", StringComparison.OrdinalIgnoreCase)
                ? ProbeRemoteAsync(target, url, expectedStatuses, timeoutSeconds, allowInsecureTls, captureBody: false, ct)
                : ProbeLocalAsync(url, expectedStatuses, timeoutSeconds, allowInsecureTls, captureBody: false, ct),
            probe => probe.Success,
            TimeSpan.FromSeconds(Math.Max(1, timeoutSeconds)),
            TimeSpan.FromSeconds(Math.Max(1, pollIntervalSeconds)),
            cancellationToken);

        return Result(
            new HttpWaitData(origin, url, waitOutcome.Completed, waitOutcome.TimedOut, waitOutcome.Snapshot, waitOutcome.ElapsedMs),
            target: target.Name,
            status: waitOutcome.Completed ? "ready" : "timeout",
            summary: waitOutcome.Completed
                ? "HTTP endpoint reached the expected ready state."
                : "Timed out while waiting for the HTTP endpoint to become ready.");
    }

    public async Task<SshOpsToolResult<CertCheckData>> CertCheckAsync(
        string correlationId,
        string targetName,
        string domain,
        string origin,
        CancellationToken cancellationToken)
    {
        var target = targetCatalog.GetRequired(targetName);
        var uri = domain.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? new Uri(domain)
            : new Uri($"https://{domain}");

        var certificate = await tlsCertificateInspector.InspectAsync(uri, cancellationToken);
        var warnings = new List<string>();
        if (certificate is not null && string.Equals(certificate.Subject, certificate.Issuer, StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add("The certificate appears to be self-signed.");
        }

        return Result(
            new CertCheckData(
                uri.Host,
                certificate is not null,
                certificate?.Issuer,
                certificate?.Subject,
                certificate?.NotAfter,
                warnings),
            target: target.Name,
            status: certificate is not null ? "success" : "failed",
            summary: certificate is not null
                ? "TLS certificate inspection completed."
                : "TLS certificate is not ready or could not be read.",
            warnings: warnings);
    }

    public async Task<SshOpsToolResult<PostgresReadyData>> PostgresReadyAsync(
        string correlationId,
        string targetName,
        string composeFile,
        string projectName,
        string workingDirectory,
        string service,
        string database,
        string user,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        var target = targetCatalog.GetRequired(targetName);
        EnsureDockerConfigured(target);
        var waitOutcome = await _waitEngine.WaitAsync(
            async ct =>
            {
                var execResult = await ExecuteComposeCommandAsync(
                    target,
                    pathGuard.ResolveInsideStacksRoot(target, composeFile),
                    projectName,
                    ["exec", "-T", service, "pg_isready", "-U", user, "-d", database],
                    new RemoteExecutionOptions(WorkingDirectory: pathGuard.ResolveInsideStacksRoot(target, workingDirectory), Timeout: TimeSpan.FromSeconds(30)),
                    ct);
                return execResult.ExitCode == 0;
            },
            ready => ready,
            TimeSpan.FromSeconds(Math.Max(1, timeoutSeconds)),
            runtimeConfiguration.DefaultOperationPollInterval,
            cancellationToken);

        return Result(
            new PostgresReadyData(waitOutcome.Completed, service, waitOutcome.Completed ? "PostgreSQL is ready." : "PostgreSQL readiness timed out."),
            target: target.Name,
            status: waitOutcome.Completed ? "success" : "timeout",
            summary: waitOutcome.Completed ? "PostgreSQL is ready." : "Timed out while waiting for PostgreSQL readiness.");
    }

    public async Task<SshOpsToolResult<IpfsStatusData>> IpfsStatusAsync(
        string correlationId,
        string targetName,
        string? apiUrl,
        CancellationToken cancellationToken)
    {
        var target = targetCatalog.GetRequired(targetName);
        var effectiveApiUrl = apiUrl ?? "http://127.0.0.1:5001";

        var idDocument = await InvokeJsonApiAsync(target, effectiveApiUrl, "id", cancellationToken);
        var peersDocument = await InvokeJsonApiAsync(target, effectiveApiUrl, "swarm/peers", cancellationToken);
        var gatewayStatus = await ProbeRemoteGatewayAsync(target, "http://127.0.0.1:8080", cancellationToken);

        var peerId = idDocument?.RootElement.TryGetProperty("ID", out var idElement) == true
            ? idElement.GetString()
            : null;
        var peerCount = peersDocument?.RootElement.TryGetProperty("Peers", out var peersElement) == true && peersElement.ValueKind == JsonValueKind.Array
            ? peersElement.GetArrayLength()
            : 0;

        return Result(
            new IpfsStatusData(
                DaemonReady: peerId is not null,
                PeerId: peerId,
                ApiReachable: idDocument is not null,
                GatewayReachable: gatewayStatus,
                SwarmPeerCount: peerCount),
            target: target.Name,
            status: peerId is not null ? "success" : "failed",
            summary: peerId is not null
                ? "IPFS API is reachable."
                : "IPFS API is not reachable.");
    }

    public async Task<SshOpsToolResult<IpfsPrivateValidateData>> IpfsPrivateValidateAsync(
        string correlationId,
        string targetName,
        string[]? expectedBootstrapPeers,
        int minimumPeerCount,
        string? apiUrl,
        string? repoRoot,
        CancellationToken cancellationToken)
    {
        var target = targetCatalog.GetRequired(targetName);
        var effectiveApiUrl = apiUrl ?? "http://127.0.0.1:5001";
        var effectiveRepoRoot = repoRoot is null
            ? pathGuard.ResolveInsideStateRoot(target, "../ipfs")
            : pathGuard.EnsureAllowedPath(target, repoRoot);

        var warnings = new List<string>();
        var swarmKeyPath = $"{effectiveRepoRoot.TrimEnd('/')}/swarm.key";
        var swarmKeyPresent = (await transport.StatAsync(target, swarmKeyPath, cancellationToken)).Exists;
        if (!swarmKeyPresent)
        {
            warnings.Add("IPFS swarm.key is missing.");
        }

        var bootstrapDocument = await InvokeJsonApiAsync(target, effectiveApiUrl, "bootstrap/list", cancellationToken);
        var bootstrapPeers = bootstrapDocument?.RootElement.TryGetProperty("Peers", out var peersElement) == true && peersElement.ValueKind == JsonValueKind.Array
            ? peersElement.EnumerateArray().Select(static element => element.GetString() ?? string.Empty).Where(static value => !string.IsNullOrWhiteSpace(value)).ToArray()
            : Array.Empty<string>();

        var publicBootstrapDetected = bootstrapPeers.Any(IsPublicBootstrapPeer);
        if (publicBootstrapDetected)
        {
            warnings.Add("Public IPFS bootstrap peers were detected.");
        }

        if (expectedBootstrapPeers is { Length: > 0 } && !expectedBootstrapPeers.SequenceEqual(bootstrapPeers, StringComparer.OrdinalIgnoreCase))
        {
            warnings.Add("Configured bootstrap peers do not match the expected private bootstrap list.");
        }

        var status = await IpfsStatusAsync(correlationId, targetName, effectiveApiUrl, cancellationToken);
        if (status.Data.SwarmPeerCount < minimumPeerCount)
        {
            warnings.Add($"IPFS swarm peer count {status.Data.SwarmPeerCount} is below the expected minimum of {minimumPeerCount}.");
        }

        var apiBindingResult = await RunRemoteShellAsync(
            target,
            "ss -ltnH '( sport = :5001 )' || true",
            timeout: TimeSpan.FromSeconds(10),
            cancellationToken: cancellationToken);
        if (apiBindingResult.StandardOutput.Contains("0.0.0.0:5001", StringComparison.OrdinalIgnoreCase) ||
            apiBindingResult.StandardOutput.Contains("[::]:5001", StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add("IPFS API appears to be listening on a public interface.");
        }

        var privateMode = swarmKeyPresent && !publicBootstrapDetected && warnings.All(static warning => !warning.Contains("bootstrap", StringComparison.OrdinalIgnoreCase));

        return Result(
            new IpfsPrivateValidateData(privateMode, swarmKeyPresent, publicBootstrapDetected, bootstrapPeers, warnings),
            target: target.Name,
            status: privateMode ? "success" : "failed",
            summary: privateMode
                ? "IPFS private swarm validation passed."
                : "IPFS private swarm validation found issues.",
            warnings: warnings);
    }

    public async Task<SshOpsToolResult<DangerousRawExecData>> DangerousRawExecAsync(
        string correlationId,
        string targetName,
        string[] command,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        if (command.Length == 0)
        {
            throw new ToolInvocationException("ValidationFailed", "dangerous_raw_exec requires at least one command segment.");
        }

        var target = targetCatalog.GetRequired(targetName);
        if (!runtimeConfiguration.Options.Server.AllowDangerousRawExec || !target.Guards.AllowRawExec)
        {
            throw new ToolInvocationException("PolicyBlocked", $"dangerous_raw_exec is disabled for target '{target.Name}'.");
        }

        await using var lease = await AcquireMutationLeaseAsync(target, "dangerous_raw_exec", cancellationToken);
        await EnsureNoRunningOperationsAsync(target, cancellationToken);

        var result = await transport.ExecuteAsync(
            target,
            command,
            new RemoteExecutionOptions(Timeout: TimeSpan.FromSeconds(Math.Max(1, timeoutSeconds))),
            cancellationToken);

        return Result(
            new DangerousRawExecData(
                result.ExitCode,
                result.StandardOutput,
                result.StandardError,
                "Break-glass command executed. Review carefully."),
            target: target.Name,
            status: result.ExitCode == 0 ? "success" : "failed",
            summary: result.ExitCode == 0
                ? "Break-glass command completed."
                : "Break-glass command failed.",
            warnings: ["dangerous_raw_exec bypasses the safer task-specific tool surface."]);
    }

    private async Task<HttpProbeData> ProbeLocalAsync(
        string url,
        int[]? expectedStatuses,
        int timeoutSeconds,
        bool allowInsecureTls,
        bool captureBody,
        CancellationToken cancellationToken)
    {
        var result = await httpProbeService.ProbeAsync(
            new HttpProbeRequest(
                new Uri(url),
                expectedStatuses,
                TimeSpan.FromSeconds(Math.Max(1, timeoutSeconds)),
                AllowInsecureTls: allowInsecureTls,
                CaptureTls: true,
                CaptureBody: captureBody),
            cancellationToken);

        return new HttpProbeData(
            "local",
            result.Url.ToString(),
            result.StatusCode,
            result.DurationMs,
            result.Success,
            result.Summary,
            result.Tls,
            result.Body);
    }

    private async Task<HttpProbeData> ProbeRemoteAsync(
        ResolvedTargetConfiguration target,
        string url,
        int[]? expectedStatuses,
        int timeoutSeconds,
        bool allowInsecureTls,
        bool captureBody,
        CancellationToken cancellationToken)
    {
        var statusLineMarker = "__STATUS__:";
        var bodyMarker = "__BODY__:";
        var tempFile = $"/tmp/http-probe-{Guid.NewGuid():N}.txt";
        var insecureFlag = allowInsecureTls ? "-k" : string.Empty;
        var startedAt = DateTimeOffset.UtcNow;
        var script =
            "HTTP_STATUS=$(curl -sS " + insecureFlag + " -o " + QuoteShell(tempFile) + " -w '%{http_code}' --max-time " + Math.Max(1, timeoutSeconds) + " " + QuoteShell(url) + " 2>/dev/null || echo '000')\n" +
            "printf '" + statusLineMarker + "%s\\n' \"$HTTP_STATUS\"\n" +
            "if [ -f " + QuoteShell(tempFile) + " ]; then\n" +
            "  printf '" + bodyMarker + "'\n" +
            "  cat " + QuoteShell(tempFile) + "\n" +
            "  rm -f " + QuoteShell(tempFile) + "\n" +
            "fi\n";

        var result = await RunRemoteShellAsync(target, script, timeout: TimeSpan.FromSeconds(Math.Max(1, timeoutSeconds) + 5), cancellationToken: cancellationToken);
        var statusCode = ParseRemoteStatusCode(result.StandardOutput, statusLineMarker);
        var body = captureBody ? ParseRemoteBody(result.StandardOutput, bodyMarker) : null;
        var statuses = expectedStatuses is { Length: > 0 } ? expectedStatuses : [200];
        var success = statusCode is not null && statuses.Contains(statusCode.Value);
        var durationMs = (long)(DateTimeOffset.UtcNow - startedAt).TotalMilliseconds;

        return new HttpProbeData(
            "remote",
            url,
            statusCode,
            durationMs,
            success,
            success ? "HTTP probe succeeded." : result.ExitCode == 0 ? $"HTTP probe returned {statusCode}." : "Remote HTTP probe failed.",
            null,
            body);
    }

    private async Task<bool> ProbeRemoteGatewayAsync(ResolvedTargetConfiguration target, string gatewayBaseUrl, CancellationToken cancellationToken)
    {
        var result = await RunRemoteShellAsync(
            target,
            $"curl -sS -o /dev/null -w '%{{http_code}}' --max-time 10 {QuoteShell(gatewayBaseUrl)} || true",
            timeout: TimeSpan.FromSeconds(15),
            cancellationToken: cancellationToken);

        return int.TryParse(result.StandardOutput.Trim(), out var statusCode) &&
               statusCode is >= 200 and < 400;
    }

    private async Task<JsonDocument?> InvokeJsonApiAsync(
        ResolvedTargetConfiguration target,
        string apiBaseUrl,
        string endpoint,
        CancellationToken cancellationToken)
    {
        var url = $"{apiBaseUrl.TrimEnd('/')}/api/v0/{endpoint}";
        var result = await RunRemoteShellAsync(
            target,
            $"curl -sS -X POST --max-time 15 {QuoteShell(url)}",
            timeout: TimeSpan.FromSeconds(20),
            cancellationToken: cancellationToken);

        if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.StandardOutput))
        {
            return null;
        }

        try
        {
            return JsonDocument.Parse(result.StandardOutput);
        }
        catch (JsonException ex)
        {
            logger.LogDebug(ex, "Could not parse JSON from IPFS API endpoint {Endpoint}", endpoint);
            return null;
        }
    }

    private static int? ParseRemoteStatusCode(string stdout, string marker)
    {
        var line = SplitLines(stdout).FirstOrDefault(candidate => candidate.StartsWith(marker, StringComparison.Ordinal));
        if (line is null)
        {
            return null;
        }

        return int.TryParse(line[marker.Length..], out var statusCode) ? statusCode : null;
    }

    private static string? ParseRemoteBody(string stdout, string marker)
    {
        var index = stdout.IndexOf(marker, StringComparison.Ordinal);
        return index >= 0 ? stdout[(index + marker.Length)..] : null;
    }
}
