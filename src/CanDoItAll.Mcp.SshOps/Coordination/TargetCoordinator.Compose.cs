namespace CanDoItAll.Mcp.SshOps.Coordination;

public sealed partial class TargetCoordinator
{
    public async Task<SshOpsToolResult<DockerNetworkEnsureData>> DockerNetworkEnsureAsync(
        string correlationId,
        string targetName,
        string name,
        string driver,
        bool internalNetwork,
        CancellationToken cancellationToken)
    {
        var target = targetCatalog.GetRequired(targetName);
        await using var lease = await AcquireMutationLeaseAsync(target, "docker_network_ensure", cancellationToken);
        await EnsureNoRunningOperationsAsync(target, cancellationToken);

        EnsureDockerConfigured(target);
        var inspectResult = await transport.ExecuteAsync(target, ["docker", "network", "inspect", name], new RemoteExecutionOptions(), cancellationToken);
        if (inspectResult.ExitCode == 0)
        {
            return Result(
                new DockerNetworkEnsureData(name, Created: false, Exists: true),
                target: target.Name,
                status: "success",
                summary: $"Docker network '{name}' already exists.");
        }

        var createArgs = new List<string> { "docker", "network", "create", "--driver", driver };
        if (internalNetwork)
        {
            createArgs.Add("--internal");
        }

        createArgs.Add(name);
        var createResult = await transport.ExecuteAsync(target, createArgs, new RemoteExecutionOptions(), cancellationToken);
        EnsureSuccess(createResult, "ValidationFailed", $"Could not create docker network '{name}'.");

        return Result(
            new DockerNetworkEnsureData(name, Created: true, Exists: true),
            target: target.Name,
            status: "success",
            summary: $"Docker network '{name}' was created.");
    }

    public async Task<SshOpsToolResult<DockerVolumeEnsureData>> DockerVolumeEnsureAsync(
        string correlationId,
        string targetName,
        string name,
        CancellationToken cancellationToken)
    {
        var target = targetCatalog.GetRequired(targetName);
        await using var lease = await AcquireMutationLeaseAsync(target, "docker_volume_ensure", cancellationToken);
        await EnsureNoRunningOperationsAsync(target, cancellationToken);

        EnsureDockerConfigured(target);
        var inspectResult = await transport.ExecuteAsync(target, ["docker", "volume", "inspect", name], new RemoteExecutionOptions(), cancellationToken);
        if (inspectResult.ExitCode == 0)
        {
            return Result(
                new DockerVolumeEnsureData(name, Created: false),
                target: target.Name,
                status: "success",
                summary: $"Docker volume '{name}' already exists.");
        }

        var createResult = await transport.ExecuteAsync(target, ["docker", "volume", "create", name], new RemoteExecutionOptions(), cancellationToken);
        EnsureSuccess(createResult, "ValidationFailed", $"Could not create docker volume '{name}'.");

        return Result(
            new DockerVolumeEnsureData(name, Created: true),
            target: target.Name,
            status: "success",
            summary: $"Docker volume '{name}' was created.");
    }

    public async Task<SshOpsToolResult<ComposeValidateData>> ComposeValidateAsync(
        string correlationId,
        string targetName,
        string composeFile,
        string projectName,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        var target = targetCatalog.GetRequired(targetName);
        EnsureDockerConfigured(target);
        var validatedComposeFile = pathGuard.ResolveInsideStacksRoot(target, composeFile);
        var validatedWorkingDirectory = pathGuard.ResolveInsideStacksRoot(target, workingDirectory);
        var result = await transport.ExecuteAsync(
            target,
            BuildComposeCommand(target, validatedComposeFile, projectName, ["config"]),
            new RemoteExecutionOptions(WorkingDirectory: validatedWorkingDirectory, Timeout: runtimeConfiguration.CommandTimeout),
            cancellationToken);

        var valid = result.ExitCode == 0;
        var warnings = string.IsNullOrWhiteSpace(result.StandardError)
            ? Array.Empty<string>()
            : SplitLines(result.StandardError);

        return Result(
            new ComposeValidateData(valid, Truncate(result.StandardOutput, 8_192), warnings),
            target: target.Name,
            status: valid ? "validated" : "invalid",
            summary: valid
                ? "Docker compose configuration is valid."
                : "Docker compose configuration is invalid.",
            diagnostics: valid ? null : warnings,
            nextSuggestedTools: valid ? ["docker_network_ensure", "compose_apply"] : null);
    }

    public async Task<SshOpsToolResult<ComposeApplyData>> ComposeApplyAsync(
        string correlationId,
        string targetName,
        string stackName,
        string composeFile,
        string projectName,
        string workingDirectory,
        bool pull,
        bool build,
        bool removeOrphans,
        string executionMode,
        ComposeWaitPolicy? postWaitPolicy,
        CancellationToken cancellationToken)
    {
        var target = targetCatalog.GetRequired(targetName);
        await using var lease = await AcquireMutationLeaseAsync(target, "compose_apply", cancellationToken);
        await EnsureNoRunningOperationsAsync(target, cancellationToken);
        EnsureDockerConfigured(target);

        var validatedComposeFile = pathGuard.ResolveInsideStacksRoot(target, composeFile);
        var validatedWorkingDirectory = pathGuard.ResolveInsideStacksRoot(target, workingDirectory);
        var backupRevisionId = runtimeConfiguration.Options.Revisions.Enabled
            ? await BackupStackDirectoryAsync(target, stackName, validatedWorkingDirectory, cancellationToken)
            : null;

        var composeArgs = new List<string> { "up", "-d" };
        if (pull)
        {
            composeArgs.Add("--pull");
            composeArgs.Add("always");
        }

        if (build)
        {
            composeArgs.Add("--build");
        }

        if (removeOrphans)
        {
            composeArgs.Add("--remove-orphans");
        }

        var resolvedExecutionMode = ResolveExecutionMode(executionMode);
        var data = new ComposeApplyData(stackName, resolvedExecutionMode, backupRevisionId);

        if (resolvedExecutionMode == "detached")
        {
            var operation = await remoteJobRunner.StartAsync(
                target,
                new RemoteJobStartRequest(
                    correlationId,
                    Kind: "compose_apply",
                    InitialSummary: $"Stack '{stackName}' apply queued.",
                    SuccessSummary: $"Stack '{stackName}' apply completed.",
                    FailureSummary: $"Stack '{stackName}' apply failed.",
                    CancelSummary: $"Stack '{stackName}' apply was cancelled.",
                    Command: BuildComposeCommand(target, validatedComposeFile, projectName, composeArgs),
                    WorkingDirectory: validatedWorkingDirectory),
                cancellationToken);

            SaveOperationMetadata(new OperationTrackingMetadata(operation.OperationId, target.Name, "target", "compose_apply", DateTimeOffset.UtcNow));
            return Result(
                data,
                target: target.Name,
                operationId: operation.OperationId,
                status: "accepted",
                summary: "Compose apply started in detached mode.",
                nextSuggestedTools: ["operation_wait", "compose_ps", "http_wait"]);
        }

        var result = await transport.ExecuteAsync(
            target,
            BuildComposeCommand(target, validatedComposeFile, projectName, composeArgs),
            new RemoteExecutionOptions(WorkingDirectory: validatedWorkingDirectory, Timeout: runtimeConfiguration.DefaultComposeApplyTimeout),
            cancellationToken);
        EnsureSuccess(result, "ValidationFailed", $"Compose apply failed for stack '{stackName}'.");

        return Result(
            data,
            target: target.Name,
            status: "success",
            summary: $"Compose apply completed for stack '{stackName}'.",
            warnings: postWaitPolicy is null ? null : ["Post-apply wait policy is advisory and should be enforced by calling compose_ps/http_wait explicitly."]);
    }

    public async Task<SshOpsToolResult<ComposePsData>> ComposePsAsync(
        string correlationId,
        string targetName,
        string composeFile,
        string projectName,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        var target = targetCatalog.GetRequired(targetName);
        EnsureDockerConfigured(target);

        var result = await transport.ExecuteAsync(
            target,
            BuildComposeCommand(target, pathGuard.ResolveInsideStacksRoot(target, composeFile), projectName, ["ps", "--format", "json"]),
            new RemoteExecutionOptions(WorkingDirectory: pathGuard.ResolveInsideStacksRoot(target, workingDirectory)),
            cancellationToken);
        EnsureSuccess(result, "ValidationFailed", "Could not read docker compose service states.");

        var services = ParseComposePs(result.StandardOutput);
        return Result(
            new ComposePsData(services),
            target: target.Name,
            status: "success",
            summary: $"Read {services.Count} service state(s).");
    }

    public async Task<SshOpsToolResult<ComposeLogsData>> ComposeLogsAsync(
        string correlationId,
        string targetName,
        string composeFile,
        string projectName,
        string workingDirectory,
        string? service,
        int tail,
        int sinceSeconds,
        CancellationToken cancellationToken)
    {
        var target = targetCatalog.GetRequired(targetName);
        EnsureDockerConfigured(target);

        var args = new List<string> { "logs", "--tail", Math.Max(1, tail).ToString() };
        if (sinceSeconds > 0)
        {
            args.Add("--since");
            args.Add($"{sinceSeconds}s");
        }

        if (!string.IsNullOrWhiteSpace(service))
        {
            args.Add(service);
        }

        var result = await transport.ExecuteAsync(
            target,
            BuildComposeCommand(target, pathGuard.ResolveInsideStacksRoot(target, composeFile), projectName, args),
            new RemoteExecutionOptions(WorkingDirectory: pathGuard.ResolveInsideStacksRoot(target, workingDirectory), Timeout: runtimeConfiguration.CommandTimeout),
            cancellationToken);
        EnsureSuccess(result, "ValidationFailed", "Could not read docker compose logs.");

        return Result(
            new ComposeLogsData(service, SplitLines(result.StandardOutput), Redacted: true),
            target: target.Name,
            status: "success",
            summary: "Read docker compose logs.");
    }

    public async Task<SshOpsToolResult<ComposeExecData>> ComposeExecAsync(
        string correlationId,
        string targetName,
        string composeFile,
        string projectName,
        string workingDirectory,
        string service,
        string[] command,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        if (command.Length == 0)
        {
            throw new ToolInvocationException("ValidationFailed", "Compose exec requires a command.");
        }

        var target = targetCatalog.GetRequired(targetName);
        if (!target.Guards.AllowComposeExec)
        {
            throw new ToolInvocationException("ValidationFailed", $"Target '{target.Name}' does not allow docker compose exec.");
        }

        EnsureDockerConfigured(target);
        var args = new List<string> { "exec", "-T", service };
        args.AddRange(command);
        var result = await transport.ExecuteAsync(
            target,
            BuildComposeCommand(target, pathGuard.ResolveInsideStacksRoot(target, composeFile), projectName, args),
            new RemoteExecutionOptions(WorkingDirectory: pathGuard.ResolveInsideStacksRoot(target, workingDirectory), Timeout: TimeSpan.FromSeconds(Math.Max(1, timeoutSeconds))),
            cancellationToken);

        return Result(
            new ComposeExecData(result.ExitCode, result.StandardOutput, result.StandardError),
            target: target.Name,
            status: result.ExitCode == 0 ? "success" : "failed",
            summary: result.ExitCode == 0
                ? $"Compose exec completed for service '{service}'."
                : $"Compose exec failed for service '{service}'.",
            diagnostics: result.ExitCode == 0 ? null : SplitLines(result.StandardError));
    }

    public async Task<SshOpsToolResult<ComposeDownData>> ComposeDownAsync(
        string correlationId,
        string targetName,
        string composeFile,
        string projectName,
        string workingDirectory,
        bool removeOrphans,
        CancellationToken cancellationToken)
    {
        var target = targetCatalog.GetRequired(targetName);
        await using var lease = await AcquireMutationLeaseAsync(target, "compose_down", cancellationToken);
        await EnsureNoRunningOperationsAsync(target, cancellationToken);
        EnsureDockerConfigured(target);

        var args = new List<string> { "down" };
        if (removeOrphans)
        {
            args.Add("--remove-orphans");
        }

        var result = await transport.ExecuteAsync(
            target,
            BuildComposeCommand(target, pathGuard.ResolveInsideStacksRoot(target, composeFile), projectName, args),
            new RemoteExecutionOptions(WorkingDirectory: pathGuard.ResolveInsideStacksRoot(target, workingDirectory), Timeout: runtimeConfiguration.DefaultComposeApplyTimeout),
            cancellationToken);
        EnsureSuccess(result, "ValidationFailed", $"Could not stop compose project '{projectName}'.");

        return Result(
            new ComposeDownData(projectName, Stopped: true),
            target: target.Name,
            status: "success",
            summary: $"Compose project '{projectName}' was stopped.");
    }

    public async Task<SshOpsToolResult<StackRollbackData>> StackRollbackAsync(
        string correlationId,
        string targetName,
        string stackName,
        string strategy,
        string executionMode,
        CancellationToken cancellationToken)
    {
        var target = targetCatalog.GetRequired(targetName);
        await using var lease = await AcquireMutationLeaseAsync(target, "stack_rollback", cancellationToken);
        await EnsureNoRunningOperationsAsync(target, cancellationToken);
        EnsureDockerConfigured(target);

        var manifest = LoadLatestRevisionManifest(target.Name, stackName)
            ?? throw new ToolInvocationException("RollbackRevisionNotFound", $"No revision manifest was found for stack '{stackName}' on target '{target.Name}'.");

        if (manifest.Entries.Count == 0)
        {
            throw new ToolInvocationException("RollbackRevisionNotFound", $"Revision '{manifest.RevisionId}' does not contain any restorable entries.");
        }

        var workingDirectory = pathGuard.ResolveInsideStacksRoot(target, stackName);
        var composeFilePath = $"{workingDirectory}/docker-compose.yml";
        var restoreScript = new StringBuilder();
        foreach (var entry in manifest.Entries)
        {
            var parent = GetParentPosixPath(entry.Path) ?? "/";
            restoreScript.AppendLine($"mkdir -p {QuoteShell(parent)}");
            restoreScript.AppendLine($"rm -rf {QuoteShell(entry.Path)}");
            restoreScript.AppendLine($"cp -a -- {QuoteShell(entry.BackupPath)} {QuoteShell(entry.Path)}");
        }

        restoreScript.AppendLine(string.Join(' ', BuildComposeCommand(target, composeFilePath, stackName, ["up", "-d", "--remove-orphans"]).Select(QuoteShell)));
        var data = new StackRollbackData(manifest.RevisionId);
        var resolvedExecutionMode = ResolveExecutionMode(executionMode);

        if (resolvedExecutionMode == "detached")
        {
            var operation = await remoteJobRunner.StartAsync(
                target,
                new RemoteJobStartRequest(
                    correlationId,
                    Kind: "stack_rollback",
                    InitialSummary: $"Rollback for stack '{stackName}' queued.",
                    SuccessSummary: $"Rollback for stack '{stackName}' completed.",
                    FailureSummary: $"Rollback for stack '{stackName}' failed.",
                    CancelSummary: $"Rollback for stack '{stackName}' was cancelled.",
                    Command: ["bash", "-lc", restoreScript.ToString()],
                    WorkingDirectory: workingDirectory),
                cancellationToken);

            SaveOperationMetadata(new OperationTrackingMetadata(operation.OperationId, target.Name, "target", "stack_rollback", DateTimeOffset.UtcNow));
            return Result(
                data,
                target: target.Name,
                operationId: operation.OperationId,
                status: "accepted",
                summary: $"Rollback for stack '{stackName}' started as a detached operation.",
                nextSuggestedTools: ["operation_wait", "compose_ps", "http_wait"]);
        }

        var result = await transport.ExecuteAsync(
            target,
            ["bash", "-lc", restoreScript.ToString()],
            new RemoteExecutionOptions(WorkingDirectory: workingDirectory, Timeout: runtimeConfiguration.DefaultComposeApplyTimeout),
            cancellationToken);
        EnsureSuccess(result, "ValidationFailed", $"Rollback for stack '{stackName}' failed.");

        return Result(
            data,
            target: target.Name,
            status: "success",
            summary: $"Rollback for stack '{stackName}' completed.");
    }

    private async Task<string> BackupStackDirectoryAsync(
        ResolvedTargetConfiguration target,
        string stackName,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        var stat = await transport.StatAsync(target, workingDirectory, cancellationToken);
        var revisionId = CorrelationIdFactory.Create("rev");
        if (!stat.Exists)
        {
            return revisionId;
        }

        var backupPath = pathGuard.ResolveInsideStateRoot(target, $"revisions/{revisionId}/{Guid.NewGuid():N}");
        await transport.EnsureDirectoryAsync(target, GetParentPosixPath(backupPath)!, useSudo: false, cancellationToken);
        await CopyRemotePathAsync(target, workingDirectory, backupPath, useSudo: false, cancellationToken);

        var manifest = new RevisionManifestMetadata(
            revisionId,
            target.Name,
            stackName,
            DateTimeOffset.UtcNow,
            [new RevisionEntryMetadata(workingDirectory, backupPath)]);
        SaveRevisionManifest(manifest);
        await UploadJsonAsync(target, pathGuard.ResolveInsideStateRoot(target, $"revisions/{revisionId}/manifest.json"), manifest, cancellationToken);
        return revisionId;
    }

    private static IReadOnlyList<ComposeServiceState> ParseComposePs(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(payload);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            return document.RootElement.EnumerateArray()
                .Select(element => new ComposeServiceState(
                    element.TryGetProperty("Service", out var serviceElement) ? serviceElement.GetString() ?? "unknown" : "unknown",
                    element.TryGetProperty("State", out var stateElement) ? stateElement.GetString() ?? "unknown" : "unknown",
                    element.TryGetProperty("Health", out var healthElement) ? healthElement.GetString() : null))
                .ToArray();
        }
        catch (JsonException)
        {
            return SplitLines(payload)
                .Select(line => new ComposeServiceState(line, "unknown", null))
                .ToArray();
        }
    }
}
