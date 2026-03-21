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
        var result = await ExecuteComposeCommandAsync(
            target,
            validatedComposeFile,
            projectName,
            ["config"],
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
        var composeCommand = await ResolveComposeCommandAsync(target, cancellationToken);
        var requiresLegacyPullStep = pull && IsLegacyComposeCommand(composeCommand);
        var backupRevisionId = runtimeConfiguration.Options.Revisions.Enabled
            ? await SnapshotStackDirectoryAsync(target, stackName, validatedWorkingDirectory, purpose: "pre_apply", isKnownGood: false, cancellationToken)
            : null;

        var composeArgs = new List<string> { "up", "-d" };
        if (pull && !requiresLegacyPullStep)
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

        var normalizedPostWaitPolicy = NormalizeComposeWaitPolicy(postWaitPolicy);
        var resolvedExecutionMode = ResolveExecutionMode(executionMode);
        var data = new ComposeApplyData(stackName, resolvedExecutionMode, backupRevisionId);

        if (resolvedExecutionMode == "detached")
        {
            var detachedCommand = normalizedPostWaitPolicy is null && !requiresLegacyPullStep
                ? BuildComposeCommand(composeCommand, target.Name, validatedComposeFile, projectName, composeArgs)
                : ["bash", "-lc", BuildComposeApplyScript(composeCommand, target.Name, validatedComposeFile, projectName, composeArgs, normalizedPostWaitPolicy, requiresLegacyPullStep)];
            var operation = await remoteJobRunner.StartAsync(
                target,
                new RemoteJobStartRequest(
                    correlationId,
                    Kind: "compose_apply",
                    InitialSummary: $"Stack '{stackName}' apply queued.",
                    SuccessSummary: $"Stack '{stackName}' apply completed.",
                    FailureSummary: $"Stack '{stackName}' apply failed.",
                    CancelSummary: $"Stack '{stackName}' apply was cancelled.",
                    Command: detachedCommand,
                    WorkingDirectory: validatedWorkingDirectory,
                    TimeoutSummary: $"Stack '{stackName}' apply timed out while waiting for the post-apply policy."),
                cancellationToken);

            SaveOperationMetadata(new OperationTrackingMetadata(operation.OperationId, target.Name, "target", "compose_apply", DateTimeOffset.UtcNow));
            return Result(
                data,
                target: target.Name,
                operationId: operation.OperationId,
                status: "accepted",
                summary: "Compose apply started in detached mode.",
                nextSuggestedTools: normalizedPostWaitPolicy is null ? ["operation_wait", "compose_ps", "http_wait"] : ["operation_wait"]);
        }

        RemoteCommandResult result;
        if (normalizedPostWaitPolicy is null && !requiresLegacyPullStep)
        {
            result = await ExecuteComposeCommandAsync(
                target,
                validatedComposeFile,
                projectName,
                composeArgs,
                new RemoteExecutionOptions(WorkingDirectory: validatedWorkingDirectory, Timeout: runtimeConfiguration.DefaultComposeApplyTimeout),
                cancellationToken);
        }
        else if (normalizedPostWaitPolicy is null)
        {
            var pullResult = await ExecuteComposeCommandAsync(
                target,
                validatedComposeFile,
                projectName,
                ["pull"],
                new RemoteExecutionOptions(WorkingDirectory: validatedWorkingDirectory, Timeout: runtimeConfiguration.DefaultComposeApplyTimeout),
                cancellationToken);
            EnsureSuccess(pullResult, "ValidationFailed", $"Compose image pull failed for stack '{stackName}'.");

            result = await ExecuteComposeCommandAsync(
                target,
                validatedComposeFile,
                projectName,
                composeArgs,
                new RemoteExecutionOptions(WorkingDirectory: validatedWorkingDirectory, Timeout: runtimeConfiguration.DefaultComposeApplyTimeout),
                cancellationToken);
        }
        else
        {
            result = await transport.ExecuteAsync(
                target,
                ["bash", "-lc", BuildComposeApplyScript(composeCommand, target.Name, validatedComposeFile, projectName, composeArgs, normalizedPostWaitPolicy, requiresLegacyPullStep)],
                new RemoteExecutionOptions(WorkingDirectory: validatedWorkingDirectory, Timeout: runtimeConfiguration.DefaultComposeApplyTimeout + TimeSpan.FromSeconds(normalizedPostWaitPolicy.TimeoutSeconds)),
                cancellationToken);
        }

        EnsureComposeApplyResult(result, stackName, normalizedPostWaitPolicy);

        if (runtimeConfiguration.Options.Revisions.Enabled)
        {
            await SnapshotStackDirectoryAsync(target, stackName, validatedWorkingDirectory, purpose: "post_apply_known_good", isKnownGood: true, cancellationToken);
        }

        return Result(
            data,
            target: target.Name,
            status: "success",
            summary: normalizedPostWaitPolicy is null
                ? $"Compose apply completed for stack '{stackName}'."
                : $"Compose apply completed for stack '{stackName}' and satisfied the post-apply wait policy.");
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

        var composeFilePath = pathGuard.ResolveInsideStacksRoot(target, composeFile);
        var workingDirectoryPath = pathGuard.ResolveInsideStacksRoot(target, workingDirectory);
        var composeCommand = await ResolveComposeCommandAsync(target, cancellationToken);

        var result = await ExecuteComposeCommandAsync(
            target,
            composeFilePath,
            projectName,
            ["ps", "--format", "json"],
            new RemoteExecutionOptions(WorkingDirectory: workingDirectoryPath),
            cancellationToken);

        IReadOnlyList<ComposeServiceState> services;
        if (IsLegacyComposeCommand(composeCommand) || (result.ExitCode != 0 && LooksLikeLegacyComposePs(result)))
        {
            services = await ReadLegacyComposePsAsync(target, projectName, cancellationToken);
        }
        else
        {
            EnsureSuccess(result, "ValidationFailed", "Could not read docker compose service states.");
            services = ParseComposePs(result.StandardOutput);
        }

        var degradedServices = services.Where(IsComposeServiceDegraded).ToArray();
        return Result(
            new ComposePsData(services),
            target: target.Name,
            status: degradedServices.Length > 0 ? "degraded" : "success",
            summary: degradedServices.Length > 0
                ? $"Read {services.Count} service state(s); {degradedServices.Length} service(s) are degraded."
                : $"Read {services.Count} service state(s).",
            warnings: degradedServices.Length > 0
                ? degradedServices.Select(service => $"Service '{service.Name}' is in state '{service.State}' with health '{service.Health ?? "n/a"}'.").ToArray()
                : null);
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
        var composeCommand = await ResolveComposeCommandAsync(target, cancellationToken);
        var isLegacyCompose = IsLegacyComposeCommand(composeCommand);
        var warnings = new List<string>();

        var args = new List<string> { "logs" };
        var normalizedTail = Math.Max(1, tail).ToString();
        if (isLegacyCompose)
        {
            args.Add($"--tail={normalizedTail}");
        }
        else
        {
            args.Add("--tail");
            args.Add(normalizedTail);
        }

        if (sinceSeconds > 0)
        {
            if (isLegacyCompose)
            {
                warnings.Add($"Target '{target.Name}' uses legacy docker-compose; the '--since' filter is unsupported and was ignored.");
            }
            else
            {
                args.Add("--since");
                args.Add($"{sinceSeconds}s");
            }
        }

        if (!string.IsNullOrWhiteSpace(service))
        {
            args.Add(service);
        }

        var result = await ExecuteComposeCommandAsync(
            target,
            pathGuard.ResolveInsideStacksRoot(target, composeFile),
            projectName,
            args,
            new RemoteExecutionOptions(WorkingDirectory: pathGuard.ResolveInsideStacksRoot(target, workingDirectory), Timeout: runtimeConfiguration.CommandTimeout),
            cancellationToken);
        EnsureSuccess(result, "ValidationFailed", "Could not read docker compose logs.");

        return Result(
            new ComposeLogsData(service, SplitLines(result.StandardOutput), Redacted: true),
            target: target.Name,
            status: "success",
            summary: "Read docker compose logs.",
            warnings: warnings.Count > 0 ? warnings : null);
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
            throw new ToolInvocationException("PolicyBlocked", $"Target '{target.Name}' does not allow docker compose exec.");
        }

        EnsureDockerConfigured(target);
        EnsureComposeExecCommandAllowed(command);
        var args = new List<string> { "exec", "-T", service };
        args.AddRange(command);
        var result = await ExecuteComposeCommandAsync(
            target,
            pathGuard.ResolveInsideStacksRoot(target, composeFile),
            projectName,
            args,
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

        var result = await ExecuteComposeCommandAsync(
            target,
            pathGuard.ResolveInsideStacksRoot(target, composeFile),
            projectName,
            args,
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
        var composeCommand = await ResolveComposeCommandAsync(target, cancellationToken);

        var manifest = LoadRollbackRevisionManifest(target.Name, stackName, strategy)
            ?? throw new ToolInvocationException(
                "RollbackRevisionNotFound",
                $"No revision manifest matching strategy '{strategy}' was found for stack '{stackName}' on target '{target.Name}'.");

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

        restoreScript.AppendLine(string.Join(' ', BuildComposeCommand(composeCommand, target.Name, composeFilePath, stackName, ["up", "-d", "--remove-orphans"]).Select(QuoteShell)));
        var restoreScriptText = restoreScript.ToString().ReplaceLineEndings("\n");
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
                    Command: ["bash", "-lc", restoreScriptText],
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
            ["bash", "-lc", restoreScriptText],
            new RemoteExecutionOptions(WorkingDirectory: workingDirectory, Timeout: runtimeConfiguration.DefaultComposeApplyTimeout),
            cancellationToken);
        EnsureSuccess(result, "ValidationFailed", $"Rollback for stack '{stackName}' failed.");

        if (runtimeConfiguration.Options.Revisions.Enabled)
        {
            await SnapshotStackDirectoryAsync(target, stackName, workingDirectory, purpose: "post_rollback_known_good", isKnownGood: true, cancellationToken);
        }

        return Result(
            data,
            target: target.Name,
            status: "success",
            summary: $"Rollback for stack '{stackName}' completed.");
    }

    private async Task<string> SnapshotStackDirectoryAsync(
        ResolvedTargetConfiguration target,
        string stackName,
        string workingDirectory,
        string purpose,
        bool isKnownGood,
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
            [new RevisionEntryMetadata(workingDirectory, backupPath)],
            Purpose: purpose,
            IsKnownGood: isKnownGood);
        SaveRevisionManifest(manifest);
        await UploadJsonAsync(target, pathGuard.ResolveInsideStateRoot(target, $"revisions/{revisionId}/manifest.json"), manifest, cancellationToken);
        return revisionId;
    }

    private static ComposeWaitPolicy? NormalizeComposeWaitPolicy(ComposeWaitPolicy? policy)
    {
        if (policy is null)
        {
            return null;
        }

        var services = (policy.WaitForHealthyServices ?? [])
            .Select(static service => service?.Trim())
            .Where(static service => !string.IsNullOrWhiteSpace(service))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Cast<string>()
            .ToArray();

        if (services.Length == 0)
        {
            return null;
        }

        if (policy.TimeoutSeconds <= 0)
        {
            throw new ToolInvocationException("ValidationFailed", "Compose postWaitPolicy.TimeoutSeconds must be greater than zero.");
        }

        if (services.Any(static service => service.Any(static ch => !(char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.'))))
        {
            throw new ToolInvocationException("ValidationFailed", "Compose postWaitPolicy.WaitForHealthyServices contains an invalid service name.");
        }

        return new ComposeWaitPolicy(services, policy.TimeoutSeconds);
    }

    private static void EnsureComposeApplyResult(RemoteCommandResult result, string stackName, ComposeWaitPolicy? postWaitPolicy)
    {
        if (result.ExitCode == 0)
        {
            return;
        }

        if (postWaitPolicy is not null && result.ExitCode == 124)
        {
            throw new ToolInvocationException(
                "Timeout",
                $"Compose apply timed out while waiting for the post-apply policy on stack '{stackName}'.",
                new
                {
                    exitCode = result.ExitCode,
                    stdout = result.StandardOutput,
                    stderr = result.StandardError,
                    command = result.CommandText,
                    waitForHealthyServices = postWaitPolicy.WaitForHealthyServices,
                    timeoutSeconds = postWaitPolicy.TimeoutSeconds
                });
        }

        EnsureSuccess(result, "ValidationFailed", $"Compose apply failed for stack '{stackName}'.");
    }

    private static string BuildComposeApplyScript(
        string composeCommand,
        string targetName,
        string composeFile,
        string projectName,
        IReadOnlyList<string> composeArgs,
        ComposeWaitPolicy? postWaitPolicy,
        bool requiresLegacyPullStep)
    {
        var script = new StringBuilder();
        script.AppendLine("set -euo pipefail");
        if (requiresLegacyPullStep)
        {
            script.AppendLine(string.Join(' ', BuildComposeCommand(composeCommand, targetName, composeFile, projectName, ["pull"]).Select(QuoteShell)));
        }

        script.AppendLine(string.Join(' ', BuildComposeCommand(composeCommand, targetName, composeFile, projectName, composeArgs).Select(QuoteShell)));
        if (postWaitPolicy is not null)
        {
            script.AppendLine(BuildComposeServiceWaitScript(projectName, postWaitPolicy));
        }

        return script.ToString().ReplaceLineEndings("\n");
    }

    private static string BuildComposeServiceWaitScript(string projectName, ComposeWaitPolicy postWaitPolicy)
    {
        var serviceLines = string.Join("\n", postWaitPolicy.WaitForHealthyServices);
        const string stateFormat = "{{.State.Status}}";
        const string healthFormat = "{{if .State.Health}}{{.State.Health.Status}}{{else}}none{{end}}";

        return $$"""
            PROJECT_NAME={{QuoteShell(projectName)}}
            WAIT_TIMEOUT_SECONDS={{postWaitPolicy.TimeoutSeconds}}
            WAIT_DEADLINE=$(( $(date +%s) + WAIT_TIMEOUT_SECONDS ))

            while [ "$(date +%s)" -le "$WAIT_DEADLINE" ]; do
              WAIT_PENDING=0
              while IFS= read -r SERVICE_NAME; do
                [ -z "$SERVICE_NAME" ] && continue

                CONTAINER_ID="$(docker ps -aq --filter "label=com.docker.compose.project=$PROJECT_NAME" --filter "label=com.docker.compose.service=$SERVICE_NAME" | head -n 1)"
                if [ -z "$CONTAINER_ID" ]; then
                  WAIT_PENDING=1
                  break
                fi

                SERVICE_STATE="$(docker inspect --format '{{stateFormat}}' "$CONTAINER_ID" 2>/dev/null || true)"
                SERVICE_HEALTH="$(docker inspect --format '{{healthFormat}}' "$CONTAINER_ID" 2>/dev/null || true)"
                if [ "$SERVICE_STATE" != "running" ]; then
                  WAIT_PENDING=1
                  break
                fi

                if [ "$SERVICE_HEALTH" != "none" ] && [ "$SERVICE_HEALTH" != "healthy" ]; then
                  WAIT_PENDING=1
                  break
                fi
              done <<'EOF_COMPOSE_WAIT_SERVICES'
            {{serviceLines}}
            EOF_COMPOSE_WAIT_SERVICES

              if [ "$WAIT_PENDING" -eq 0 ]; then
                break
              fi

              sleep 2
            done

            if [ "$WAIT_PENDING" -ne 0 ]; then
              echo "Timed out waiting for compose services to satisfy the post-apply policy." >&2
              while IFS= read -r SERVICE_NAME; do
                [ -z "$SERVICE_NAME" ] && continue
                CONTAINER_ID="$(docker ps -aq --filter "label=com.docker.compose.project=$PROJECT_NAME" --filter "label=com.docker.compose.service=$SERVICE_NAME" | head -n 1)"
                if [ -z "$CONTAINER_ID" ]; then
                  echo "Service '$SERVICE_NAME' has no discovered container for project '$PROJECT_NAME'." >&2
                  continue
                fi

                SERVICE_STATE="$(docker inspect --format '{{stateFormat}}' "$CONTAINER_ID" 2>/dev/null || true)"
                SERVICE_HEALTH="$(docker inspect --format '{{healthFormat}}' "$CONTAINER_ID" 2>/dev/null || true)"
                echo "Service '$SERVICE_NAME' container '$CONTAINER_ID' state='$SERVICE_STATE' health='$SERVICE_HEALTH'." >&2
              done <<'EOF_COMPOSE_WAIT_SERVICES'
            {{serviceLines}}
            EOF_COMPOSE_WAIT_SERVICES

              exit 124
            fi
            """;
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

    private async Task<IReadOnlyList<ComposeServiceState>> ReadLegacyComposePsAsync(
        ResolvedTargetConfiguration target,
        string projectName,
        CancellationToken cancellationToken)
    {
        var projectFilter = QuoteShell($"label=com.docker.compose.project={projectName}");
        const string inspectFormat = "{{index .Config.Labels \"com.docker.compose.service\"}}|{{.State.Status}}|{{if .State.Health}}{{.State.Health.Status}}{{end}}";
        var result = await RunRemoteShellAsync(
            target,
            $@"IDS=$(docker ps -a -q --filter {projectFilter})
if [ -n ""$IDS"" ]; then
  docker inspect --format '{inspectFormat}' $IDS
fi",
            timeout: runtimeConfiguration.CommandTimeout,
            cancellationToken: cancellationToken);
        EnsureSuccess(result, "ValidationFailed", "Could not read docker container state for the compose project.");

        return SplitLines(result.StandardOutput)
            .Select(ParseLegacyComposePsLine)
            .Where(static service => !string.IsNullOrWhiteSpace(service.Name))
            .GroupBy(static service => service.Name, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .ToArray();
    }

    private static ComposeServiceState ParseLegacyComposePsLine(string line)
    {
        var separatorIndex = line.IndexOf('|', StringComparison.Ordinal);
        if (separatorIndex <= 0)
        {
            return new ComposeServiceState(line, "unknown", null);
        }

        var serviceName = line[..separatorIndex].Trim();
        var remaining = line[(separatorIndex + 1)..];
        var secondSeparatorIndex = remaining.IndexOf('|', StringComparison.Ordinal);
        var state = secondSeparatorIndex >= 0
            ? remaining[..secondSeparatorIndex].Trim()
            : remaining.Trim();
        var health = secondSeparatorIndex >= 0
            ? remaining[(secondSeparatorIndex + 1)..].Trim()
            : string.Empty;

        return new ComposeServiceState(
            serviceName,
            string.IsNullOrWhiteSpace(state) ? "unknown" : state,
            string.IsNullOrWhiteSpace(health) ? null : health);
    }

    private static bool LooksLikeLegacyComposePs(RemoteCommandResult result)
    {
        if (result.ExitCode == 0)
        {
            return false;
        }

        var combined = $"{result.StandardError}\n{result.StandardOutput}";
        return combined.Contains("No such option: --format", StringComparison.OrdinalIgnoreCase) ||
               combined.Contains("unknown flag: --format", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLegacyComposeCommand(string composeCommand)
    {
        return string.Equals(
            NormalizeComposeCommand(composeCommand),
            "docker-compose",
            StringComparison.OrdinalIgnoreCase);
    }
}
