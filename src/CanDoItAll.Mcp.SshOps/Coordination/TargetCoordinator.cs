using System.Text;
using System.Text.Json;
using CanDoItAll.Mcp.Core.Concurrency;
using CanDoItAll.Mcp.Core.Operations;
using CanDoItAll.Mcp.SshOps.Configuration;
using CanDoItAll.Mcp.SshOps.Operations;
using CanDoItAll.Mcp.SshOps.Security;
using CanDoItAll.Mcp.SshOps.Transport;

namespace CanDoItAll.Mcp.SshOps.Coordination;

public sealed partial class TargetCoordinator(
    RuntimeConfiguration runtimeConfiguration,
    TargetCatalog targetCatalog,
    ISshTransport transport,
    RemotePathGuard pathGuard,
    RemoteJobRunner remoteJobRunner,
    ResourceMutationGate mutationGate,
    HttpProbeService httpProbeService,
    TlsCertificateInspector tlsCertificateInspector,
    ILogger<TargetCoordinator> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly OperationWaitEngine _waitEngine = new();

    private SshOpsToolResult<T> Result<T>(
        T data,
        string status,
        string summary,
        string? target = null,
        string? operationId = null,
        IReadOnlyList<string>? diagnostics = null,
        IReadOnlyList<string>? nextSuggestedTools = null,
        IReadOnlyList<string>? warnings = null)
    {
        return new SshOpsToolResult<T>(data, status, summary, target, operationId, diagnostics, nextSuggestedTools, warnings);
    }

    private async Task<ResourceMutationGate.MutationLease> AcquireMutationLeaseAsync(
        ResolvedTargetConfiguration target,
        string reason,
        CancellationToken cancellationToken)
    {
        return await mutationGate.AcquireAsync($"target:{target.Name}", reason, cancellationToken, busyCode: "OperationBusy");
    }

    private async Task EnsureNoRunningOperationsAsync(ResolvedTargetConfiguration target, CancellationToken cancellationToken)
    {
        foreach (var metadata in LoadOperationMetadata(target.Name))
        {
            try
            {
                var snapshot = await remoteJobRunner.GetSnapshotAsync(target, metadata.OperationId, cancellationToken);
                if (!snapshot.IsTerminal)
                {
                    throw new ToolInvocationException(
                        "OperationBusy",
                        $"Target '{target.Name}' already has a running operation '{metadata.OperationId}' ({metadata.Kind}).",
                        new { metadata.OperationId, metadata.Kind });
                }
            }
            catch (ToolInvocationException ex) when (string.Equals(ex.Code, "OperationNotFound", StringComparison.Ordinal))
            {
                logger.LogDebug("Tracked operation {OperationId} for target {Target} is no longer present on the remote host.", metadata.OperationId, target.Name);
            }
        }
    }

    private async Task<RemoteCommandResult> RunRemoteShellAsync(
        ResolvedTargetConfiguration target,
        string script,
        bool useSudo = false,
        TimeSpan? timeout = null,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default)
    {
        return await transport.ExecuteAsync(
            target,
            ["bash", "-lc", script],
            new RemoteExecutionOptions(WorkingDirectory: workingDirectory, UseSudo: useSudo, Timeout: timeout),
            cancellationToken);
    }

    private static void EnsureSuccess(RemoteCommandResult result, string code, string message)
    {
        if (result.ExitCode == 0)
        {
            return;
        }

        throw new ToolInvocationException(
            code,
            message,
            new
            {
                exitCode = result.ExitCode,
                stdout = result.StandardOutput,
                stderr = result.StandardError,
                command = result.CommandText
            });
    }

    private static string[] SplitLines(string text)
    {
        return text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static string Truncate(string? value, int maxChars)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxChars)
        {
            return value ?? string.Empty;
        }

        return value[..maxChars];
    }

    private static byte[] GetContentBytes(RemoteFileBundleEntry entry)
    {
        return entry.Encoding.Trim().ToLowerInvariant() switch
        {
            "utf8" or "text" => Encoding.UTF8.GetBytes(entry.Content),
            "base64" => Convert.FromBase64String(entry.Content),
            _ => throw new ToolInvocationException("ValidationFailed", $"Encoding '{entry.Encoding}' is not supported.")
        };
    }

    private static string? GetParentPosixPath(string path)
    {
        var normalized = path.Replace('\\', '/').TrimEnd('/');
        var separatorIndex = normalized.LastIndexOf('/');
        return separatorIndex <= 0 ? null : normalized[..separatorIndex];
    }

    private static string ResolveStackName(ResolvedTargetConfiguration target, IReadOnlyList<string> paths)
    {
        foreach (var path in paths)
        {
            if (!path.StartsWith(target.StacksRoot + "/", StringComparison.Ordinal))
            {
                continue;
            }

            var relative = path[(target.StacksRoot.Length + 1)..];
            var segments = relative.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length > 0)
            {
                return segments[0];
            }
        }

        return "default";
    }

    private static bool IsPublicBootstrapPeer(string value)
    {
        return value.Contains("bootstrap.libp2p.io", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("ipfs.io", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("p2p-circuit", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("mars.", StringComparison.OrdinalIgnoreCase);
    }

    private static string QuoteShell(string value)
    {
        return $"'{value.Replace("'", "'\"'\"'")}'";
    }

    private static string ResolveAuthenticationMethod(ResolvedTargetConfiguration target)
    {
        if (!string.IsNullOrWhiteSpace(target.Auth.PrivateKeyEnv))
        {
            return "publickey";
        }

        if (!string.IsNullOrWhiteSpace(target.Auth.PasswordEnv))
        {
            return "password";
        }

        return "unknown";
    }

    private static string ResolveExecutionMode(string? executionMode)
    {
        return executionMode?.Trim().ToLowerInvariant() switch
        {
            "sync" or "inline" => "sync",
            "detached" => "detached",
            _ => "detached"
        };
    }

    private static void EnsureDockerConfigured(ResolvedTargetConfiguration target)
    {
        if (string.IsNullOrWhiteSpace(target.Docker.ComposeCommand))
        {
            throw new ToolInvocationException("ComposePluginMissing", $"Target '{target.Name}' does not define a docker compose command.");
        }
    }

    private async Task<RemoteCommandResult> ExecuteComposeCommandAsync(
        ResolvedTargetConfiguration target,
        string? composeFile,
        string? projectName,
        IReadOnlyList<string> args,
        RemoteExecutionOptions options,
        CancellationToken cancellationToken,
        bool throwIfUnavailable = true)
    {
        EnsureDockerConfigured(target);

        RemoteCommandResult? lastResult = null;
        foreach (var composeCommand in GetComposeCommandCandidates(target.Docker.ComposeCommand))
        {
            var command = BuildComposeCommand(composeCommand, target.Name, composeFile, projectName, args);
            var result = await transport.ExecuteAsync(target, command, options, cancellationToken);
            lastResult = result;

            if (result.ExitCode == 0 || !LooksLikeComposeCommandUnavailable(result))
            {
                return result;
            }
        }

        if (throwIfUnavailable && lastResult is not null)
        {
            throw new ToolInvocationException(
                "ComposePluginMissing",
                $"No usable Docker Compose command was found for target '{target.Name}'.",
                new
                {
                    configured = target.Docker.ComposeCommand,
                    stderr = lastResult.StandardError,
                    stdout = lastResult.StandardOutput
                });
        }

        return lastResult ?? throw new ToolInvocationException("ComposePluginMissing", $"No usable Docker Compose command was found for target '{target.Name}'.");
    }

    private async Task<string> ResolveComposeCommandAsync(
        ResolvedTargetConfiguration target,
        CancellationToken cancellationToken)
    {
        EnsureDockerConfigured(target);

        RemoteCommandResult? lastResult = null;
        foreach (var composeCommand in GetComposeCommandCandidates(target.Docker.ComposeCommand))
        {
            var command = BuildComposeCommand(
                composeCommand,
                target.Name,
                composeFile: null,
                projectName: null,
                args: ["version"]);
            var result = await transport.ExecuteAsync(
                target,
                command,
                new RemoteExecutionOptions(Timeout: runtimeConfiguration.CommandTimeout),
                cancellationToken);
            lastResult = result;

            if (result.ExitCode == 0 || !LooksLikeComposeCommandUnavailable(result))
            {
                return composeCommand;
            }
        }

        throw new ToolInvocationException(
            "ComposePluginMissing",
            $"No usable Docker Compose command was found for target '{target.Name}'.",
            new
            {
                configured = target.Docker.ComposeCommand,
                stderr = lastResult?.StandardError,
                stdout = lastResult?.StandardOutput
            });
    }

    private static IReadOnlyList<string> BuildComposeCommand(
        ResolvedTargetConfiguration target,
        string? composeFile,
        string? projectName,
        IReadOnlyList<string> args)
    {
        return BuildComposeCommand(target.Docker.ComposeCommand, target.Name, composeFile, projectName, args);
    }

    private static IReadOnlyList<string> BuildComposeCommand(
        string composeCommand,
        string targetName,
        string? composeFile,
        string? projectName,
        IReadOnlyList<string> args)
    {
        var command = composeCommand.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        if (command.Count == 0)
        {
            throw new ToolInvocationException("ComposePluginMissing", $"Target '{targetName}' does not define a docker compose command.");
        }

        if (!string.IsNullOrWhiteSpace(projectName))
        {
            command.Add("-p");
            command.Add(projectName);
        }

        if (!string.IsNullOrWhiteSpace(composeFile))
        {
            command.Add("-f");
            command.Add(composeFile);
        }

        command.AddRange(args);
        return command;
    }

    private static IReadOnlyList<string> GetComposeCommandCandidates(string configuredCommand)
    {
        var candidates = new List<string>();
        var primary = configuredCommand.Trim();
        if (!string.IsNullOrWhiteSpace(primary))
        {
            candidates.Add(primary);
        }

        if (TryGetAlternateComposeCommand(primary, out var alternate) &&
            !candidates.Contains(alternate, StringComparer.OrdinalIgnoreCase))
        {
            candidates.Add(alternate);
        }

        return candidates;
    }

    private static bool TryGetAlternateComposeCommand(string configuredCommand, out string alternate)
    {
        var normalized = NormalizeComposeCommand(configuredCommand);
        switch (normalized)
        {
            case "docker compose":
                alternate = "docker-compose";
                return true;
            case "docker-compose":
                alternate = "docker compose";
                return true;
            default:
                alternate = string.Empty;
                return false;
        }
    }

    private static string NormalizeComposeCommand(string value)
    {
        return string.Join(
            ' ',
            value
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(static segment => segment.Trim().Trim('"').Trim('\''))
                .Select(static segment => segment.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                    ? segment[..^4]
                    : segment)
                .Select(static segment => segment.ToLowerInvariant()));
    }

    private static bool LooksLikeComposeCommandUnavailable(RemoteCommandResult result)
    {
        if (result.ExitCode == 0)
        {
            return false;
        }

        var combined = $"{result.StandardError}\n{result.StandardOutput}";
        return combined.Contains("docker: 'compose' is not a docker command", StringComparison.OrdinalIgnoreCase) ||
               combined.Contains("unknown shorthand flag: 'p' in -p", StringComparison.OrdinalIgnoreCase) ||
               combined.Contains("unknown shorthand flag: 'f' in -f", StringComparison.OrdinalIgnoreCase) ||
               combined.Contains("docker-compose: command not found", StringComparison.OrdinalIgnoreCase) ||
               combined.Contains("'docker-compose' is not recognized", StringComparison.OrdinalIgnoreCase);
    }

    private static void EnsureComposeExecCommandAllowed(string[] command)
    {
        var executable = Path.GetFileName(command[0].Replace('\\', '/'));
        if (string.IsNullOrWhiteSpace(executable))
        {
            throw new ToolInvocationException("ValidationFailed", "Compose exec requires a command executable.");
        }

        if (BlockedComposeExecCommands.Contains(executable) ||
            command.Skip(1).Any(static arg => string.Equals(arg, "-c", StringComparison.OrdinalIgnoreCase) ||
                                             string.Equals(arg, "-lc", StringComparison.OrdinalIgnoreCase)))
        {
            throw new ToolInvocationException(
                "PolicyBlocked",
                $"compose_exec does not allow shell or interpreter style commands ('{executable}').",
                new { command });
        }

        if (!AllowedComposeExecCommands.Contains(executable))
        {
            throw new ToolInvocationException(
                "PolicyBlocked",
                $"compose_exec command '{executable}' is outside the allowed safe command list.",
                new { command, allowed = AllowedComposeExecCommands.OrderBy(static value => value).ToArray() });
        }
    }

    private static bool IsComposeServiceDegraded(ComposeServiceState service)
    {
        if (!string.IsNullOrWhiteSpace(service.Health) &&
            string.Equals(service.Health, "unhealthy", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var state = service.State.Trim().ToLowerInvariant();
        return state is "dead" or "exited" or "restarting" or "removing" or "paused" ||
               state.StartsWith("exit", StringComparison.Ordinal) ||
               state.Contains("unhealthy", StringComparison.OrdinalIgnoreCase);
    }

    private static readonly HashSet<string> AllowedComposeExecCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "cat",
        "curl",
        "env",
        "hostname",
        "ipfs",
        "ls",
        "mysqladmin",
        "pg_isready",
        "printenv",
        "redis-cli",
        "sha256sum",
        "shasum",
        "stat",
        "test",
        "wget"
    };

    private static readonly HashSet<string> BlockedComposeExecCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "ash",
        "bash",
        "chmod",
        "chown",
        "cmd",
        "cp",
        "dd",
        "dnf",
        "mkfs",
        "mv",
        "node",
        "perl",
        "powershell",
        "pwsh",
        "python",
        "python3",
        "rm",
        "ruby",
        "sh",
        "sudo",
        "su",
        "zsh"
    };

    private OperationStatusData ToStatusData(RemoteOperationSnapshot snapshot)
    {
        return new OperationStatusData(
            snapshot.OperationId,
            snapshot.State.ToString().ToLowerInvariant(),
            snapshot.StartedAtUtc,
            snapshot.FinishedAtUtc,
            snapshot.ExitCode,
            snapshot.Summary);
    }
}
