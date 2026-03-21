using System.Text;
using CanDoItAll.Mcp.SshOps.Configuration;
using CanDoItAll.Mcp.SshOps.Security;
using Renci.SshNet;
using Renci.SshNet.Common;
using Renci.SshNet.Sftp;

namespace CanDoItAll.Mcp.SshOps.Transport;

public sealed class SshNetTransport(
    RuntimeConfiguration runtimeConfiguration,
    SecretResolver secretResolver,
    HostKeyVerifier hostKeyVerifier,
    SecretRedactor secretRedactor,
    ILogger<SshNetTransport> logger) : ISshTransport
{
    public async Task<string> GetHostFingerprintAsync(ResolvedTargetConfiguration target, CancellationToken cancellationToken)
    {
        string? fingerprint = null;
        await RunWithSshClientAsync(
            target,
            client =>
            {
                fingerprint = ConnectAndCaptureFingerprint(client, target);
                return Task.CompletedTask;
            },
            cancellationToken);

        return fingerprint ?? throw new ToolInvocationException("HostKeyMismatch", $"Could not capture a host key fingerprint for target '{target.Name}'.");
    }

    public Task<RemoteCommandResult> ExecuteAsync(
        ResolvedTargetConfiguration target,
        IReadOnlyList<string> command,
        RemoteExecutionOptions options,
        CancellationToken cancellationToken)
    {
        if (command.Count == 0)
        {
            throw new ToolInvocationException("ValidationFailed", "At least one command segment is required.");
        }

        return RunWithSshClientAsync(
            target,
            async client =>
            {
                ConnectAndCaptureFingerprint(client, target);
                var commandText = BuildCommandText(target, command, options);
                var result = await Task.Run(() =>
                {
                    using var sshCommand = client.CreateCommand(commandText);
                    sshCommand.CommandTimeout = options.Timeout ?? runtimeConfiguration.CommandTimeout;
                    var stdout = sshCommand.Execute() ?? string.Empty;
                    return new RemoteCommandResult(
                        sshCommand.ExitStatus ?? -1,
                        secretRedactor.Redact(stdout),
                        secretRedactor.Redact(sshCommand.Error ?? string.Empty),
                        commandText);
                }, cancellationToken);

                return result;
            },
            cancellationToken);
    }

    public async Task EnsureDirectoryAsync(ResolvedTargetConfiguration target, string remotePath, bool useSudo, CancellationToken cancellationToken)
    {
        var result = await ExecuteAsync(
            target,
            ["mkdir", "-p", remotePath],
            new RemoteExecutionOptions(UseSudo: useSudo),
            cancellationToken);
        if (result.ExitCode != 0 && !useSudo && CanUseSudo(target) && LooksLikePermissionDenied(result))
        {
            result = await ExecuteAsync(
                target,
                ["mkdir", "-p", remotePath],
                new RemoteExecutionOptions(UseSudo: true),
                cancellationToken);
        }

        EnsureCommandSucceeded(result, $"Could not ensure remote directory '{remotePath}'.");
    }

    public async Task UploadBytesAsync(
        ResolvedTargetConfiguration target,
        string remotePath,
        byte[] content,
        bool ensureParentDirectory,
        CancellationToken cancellationToken)
    {
        try
        {
            await RunWithSftpClientAsync(
                target,
                async client =>
                {
                    ConnectAndCaptureFingerprint(client, target);
                    if (ensureParentDirectory)
                    {
                        var parent = GetParentDirectory(remotePath);
                        if (!string.IsNullOrWhiteSpace(parent))
                        {
                            EnsureSftpDirectory(client, parent!);
                        }
                    }

                    await using var stream = new MemoryStream(content);
                    client.UploadFile(stream, remotePath, canOverride: true);
                },
                cancellationToken);
            return;
        }
        catch (Exception ex) when (CanUseSudo(target) && LooksLikePermissionDenied(ex))
        {
            logger.LogDebug(ex, "Retrying upload for {RemotePath} on target {Target} through a sudo move flow.", remotePath, target.Name);
        }

        await UploadWithSudoMoveAsync(target, remotePath, content, ensureParentDirectory, cancellationToken);
    }

    public Task<string> ReadTextAsync(ResolvedTargetConfiguration target, string remotePath, int maxBytes, CancellationToken cancellationToken)
    {
        return RunWithSftpClientAsync(
            target,
            async client =>
            {
                ConnectAndCaptureFingerprint(client, target);
                await using var stream = client.OpenRead(remotePath);
                using var memoryStream = new MemoryStream();
                var buffer = new byte[Math.Clamp(maxBytes, 1, 1024 * 1024)];
                var totalRead = 0;
                while (totalRead < maxBytes)
                {
                    var toRead = Math.Min(buffer.Length, maxBytes - totalRead);
                    var read = await stream.ReadAsync(buffer.AsMemory(0, toRead), cancellationToken);
                    if (read == 0)
                    {
                        break;
                    }

                    memoryStream.Write(buffer, 0, read);
                    totalRead += read;
                }

                return Encoding.UTF8.GetString(memoryStream.ToArray());
            },
            cancellationToken);
    }

    public Task<byte[]> ReadBytesAsync(ResolvedTargetConfiguration target, string remotePath, long offset, int maxBytes, CancellationToken cancellationToken)
    {
        return RunWithSftpClientAsync(
            target,
            async client =>
            {
                ConnectAndCaptureFingerprint(client, target);
                await using var stream = client.OpenRead(remotePath);
                stream.Seek(offset, SeekOrigin.Begin);
                var buffer = new byte[Math.Clamp(maxBytes, 1, 1024 * 1024)];
                var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
                return buffer[..read];
            },
            cancellationToken);
    }

    public Task<RemoteFileStat> StatAsync(ResolvedTargetConfiguration target, string remotePath, CancellationToken cancellationToken)
    {
        return RunWithSftpClientAsync(
            target,
            client =>
            {
                ConnectAndCaptureFingerprint(client, target);
                if (!client.Exists(remotePath))
                {
                    return Task.FromResult(new RemoteFileStat(false, false, 0, null));
                }

                var attributes = client.GetAttributes(remotePath);
                return Task.FromResult(new RemoteFileStat(true, attributes.IsDirectory, attributes.Size, attributes.LastWriteTimeUtc));
            },
            cancellationToken);
    }

    public async Task DeleteAsync(ResolvedTargetConfiguration target, string remotePath, bool recursive, bool useSudo, CancellationToken cancellationToken)
    {
        var flags = recursive ? new[] { "-rf", remotePath } : new[] { "-f", remotePath };
        var result = await ExecuteAsync(target, ["rm", .. flags], new RemoteExecutionOptions(UseSudo: useSudo), cancellationToken);
        if (result.ExitCode != 0 && !useSudo && CanUseSudo(target) && LooksLikePermissionDenied(result))
        {
            result = await ExecuteAsync(target, ["rm", .. flags], new RemoteExecutionOptions(UseSudo: true), cancellationToken);
        }

        EnsureCommandSucceeded(result, $"Could not delete remote path '{remotePath}'.");
    }

    private async Task<T> RunWithSshClientAsync<T>(
        ResolvedTargetConfiguration target,
        Func<SshClient, Task<T>> callback,
        CancellationToken cancellationToken)
    {
        using var client = CreateSshClient(target);
        return await callback(client);
    }

    private async Task RunWithSshClientAsync(
        ResolvedTargetConfiguration target,
        Func<SshClient, Task> callback,
        CancellationToken cancellationToken)
    {
        using var client = CreateSshClient(target);
        await callback(client);
    }

    private async Task<T> RunWithSftpClientAsync<T>(
        ResolvedTargetConfiguration target,
        Func<SftpClient, Task<T>> callback,
        CancellationToken cancellationToken)
    {
        using var client = CreateSftpClient(target);
        return await callback(client);
    }

    private async Task RunWithSftpClientAsync(
        ResolvedTargetConfiguration target,
        Func<SftpClient, Task> callback,
        CancellationToken cancellationToken)
    {
        using var client = CreateSftpClient(target);
        await callback(client);
    }

    private string ConnectAndCaptureFingerprint(BaseClient client, ResolvedTargetConfiguration target)
    {
        Exception? verificationFailure = null;
        string? fingerprint = null;

        client.HostKeyReceived += (_, eventArgs) =>
        {
            fingerprint = hostKeyVerifier.ComputeSha256Fingerprint(eventArgs.HostKey);
            try
            {
                hostKeyVerifier.EnsureTrusted(fingerprint, target.HostKeyVerification, target.Name);
                eventArgs.CanTrust = true;
            }
            catch (Exception ex)
            {
                verificationFailure = ex;
                eventArgs.CanTrust = false;
            }
        };

        try
        {
            client.ConnectionInfo.Timeout = runtimeConfiguration.ConnectTimeout;
            client.Connect();
        }
        catch (SshAuthenticationException ex)
        {
            throw new ToolInvocationException("AuthenticationFailed", $"Authentication failed for target '{target.Name}'.", new { target = target.Name, detail = ex.Message });
        }
        catch (SshConnectionException ex)
        {
            throw new ToolInvocationException("TargetNotConfigured", $"SSH connection failed for target '{target.Name}'.", new { target = target.Name, detail = ex.Message });
        }

        if (verificationFailure is not null)
        {
            client.Dispose();
            throw verificationFailure;
        }

        return fingerprint ?? throw new ToolInvocationException("HostKeyMismatch", $"No host key fingerprint was captured for target '{target.Name}'.");
    }

    private SshClient CreateSshClient(ResolvedTargetConfiguration target)
    {
        return new SshClient(CreateConnectionInfo(target));
    }

    private SftpClient CreateSftpClient(ResolvedTargetConfiguration target)
    {
        return new SftpClient(CreateConnectionInfo(target));
    }

    private ConnectionInfo CreateConnectionInfo(ResolvedTargetConfiguration target)
    {
        List<AuthenticationMethod> authenticationMethods = [];

        var privateKeyText = secretResolver.ResolvePrivateKey(target.Auth);
        if (!string.IsNullOrWhiteSpace(privateKeyText))
        {
            using var privateKeyStream = new MemoryStream(Encoding.UTF8.GetBytes(privateKeyText));
            var passphrase = secretResolver.ResolvePrivateKeyPassphrase(target.Auth);
            var keyFile = string.IsNullOrWhiteSpace(passphrase)
                ? new PrivateKeyFile(privateKeyStream)
                : new PrivateKeyFile(privateKeyStream, passphrase);
            authenticationMethods.Add(new PrivateKeyAuthenticationMethod(target.User, keyFile));
        }

        var password = secretResolver.ResolvePassword(target.Auth);
        if (!string.IsNullOrWhiteSpace(password))
        {
            authenticationMethods.Add(new PasswordAuthenticationMethod(target.User, password));
        }

        if (authenticationMethods.Count == 0)
        {
            throw new ToolInvocationException("TargetNotConfigured", $"Target '{target.Name}' does not have any usable authentication configured.");
        }

        return new ConnectionInfo(target.Host, target.Port, target.User, authenticationMethods.ToArray())
        {
            Timeout = runtimeConfiguration.ConnectTimeout
        };
    }

    private string BuildCommandText(ResolvedTargetConfiguration target, IReadOnlyList<string> command, RemoteExecutionOptions options)
    {
        var commandText = string.Join(' ', command.Select(EscapeShellArgument));
        var script = string.IsNullOrWhiteSpace(options.WorkingDirectory)
            ? commandText
            : $"cd {EscapeShellArgument(options.WorkingDirectory!)} && {commandText}";

        if (options.UseSudo && !string.Equals(target.Sudo.Mode, "none", StringComparison.OrdinalIgnoreCase))
        {
            return $"{target.Sudo.Command} bash -lc {EscapeShellArgument(script)}";
        }

        return $"bash -lc {EscapeShellArgument(script)}";
    }

    private static string EscapeShellArgument(string value)
    {
        return "'" + value.Replace("'", "'\"'\"'") + "'";
    }

    private async Task UploadWithSudoMoveAsync(
        ResolvedTargetConfiguration target,
        string remotePath,
        byte[] content,
        bool ensureParentDirectory,
        CancellationToken cancellationToken)
    {
        var tempPath = $"/tmp/candoitall-upload-{Guid.NewGuid():N}";
        try
        {
            await RunWithSftpClientAsync(
                target,
                async client =>
                {
                    ConnectAndCaptureFingerprint(client, target);
                    await using var stream = new MemoryStream(content);
                    client.UploadFile(stream, tempPath, canOverride: true);
                },
                cancellationToken);

            var parent = GetParentDirectory(remotePath);
            var script = new List<string>();
            if (ensureParentDirectory && !string.IsNullOrWhiteSpace(parent))
            {
                script.Add($"mkdir -p {EscapeShellArgument(parent!)}");
            }

            script.Add($"mv -f {EscapeShellArgument(tempPath)} {EscapeShellArgument(remotePath)}");
            var moveResult = await ExecuteAsync(
                target,
                ["bash", "-lc", string.Join(" && ", script)],
                new RemoteExecutionOptions(UseSudo: true, Timeout: runtimeConfiguration.UploadTimeout),
                cancellationToken);
            EnsureCommandSucceeded(moveResult, $"Could not move uploaded content into '{remotePath}'.");
        }
        catch
        {
            try
            {
                await ExecuteAsync(
                    target,
                    ["bash", "-lc", $"rm -f {EscapeShellArgument(tempPath)}"],
                    new RemoteExecutionOptions(Timeout: TimeSpan.FromSeconds(10)),
                    cancellationToken);
            }
            catch (Exception cleanupEx)
            {
                logger.LogDebug(cleanupEx, "Could not clean up temporary upload file {TempPath} on target {Target}.", tempPath, target.Name);
            }

            throw;
        }
    }

    private static bool CanUseSudo(ResolvedTargetConfiguration target)
    {
        return !string.Equals(target.Sudo.Mode, "none", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikePermissionDenied(RemoteCommandResult result)
    {
        var combined = $"{result.StandardError}\n{result.StandardOutput}";
        return combined.Contains("Permission denied", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikePermissionDenied(Exception exception)
    {
        return exception is SftpPermissionDeniedException ||
               exception.Message.Contains("Permission denied", StringComparison.OrdinalIgnoreCase);
    }

    private static void EnsureCommandSucceeded(RemoteCommandResult result, string message)
    {
        if (result.ExitCode == 0)
        {
            return;
        }

        throw new ToolInvocationException(
            "ValidationFailed",
            message,
            new
            {
                exitCode = result.ExitCode,
                stdout = result.StandardOutput,
                stderr = result.StandardError,
                command = result.CommandText
            });
    }

    private static void EnsureSftpDirectory(SftpClient client, string directoryPath)
    {
        if (client.Exists(directoryPath))
        {
            return;
        }

        var parent = GetParentDirectory(directoryPath);
        if (!string.IsNullOrWhiteSpace(parent) && !client.Exists(parent))
        {
            EnsureSftpDirectory(client, parent!);
        }

        client.CreateDirectory(directoryPath);
    }

    private static string? GetParentDirectory(string path)
    {
        var normalized = path.Replace('\\', '/');
        var separatorIndex = normalized.LastIndexOf('/');
        if (separatorIndex <= 0)
        {
            return null;
        }

        return normalized[..separatorIndex];
    }
}
