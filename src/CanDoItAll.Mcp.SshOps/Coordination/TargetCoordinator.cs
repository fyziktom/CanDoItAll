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
    SecretRedactor secretRedactor,
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

    private static IReadOnlyList<string> BuildComposeCommand(
        ResolvedTargetConfiguration target,
        string? composeFile,
        string? projectName,
        IReadOnlyList<string> args)
    {
        var command = target.Docker.ComposeCommand.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        if (command.Count == 0)
        {
            throw new ToolInvocationException("ComposePluginMissing", $"Target '{target.Name}' does not define a docker compose command.");
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
