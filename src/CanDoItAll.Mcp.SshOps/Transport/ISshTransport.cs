using CanDoItAll.Mcp.SshOps.Configuration;

namespace CanDoItAll.Mcp.SshOps.Transport;

public sealed record RemoteExecutionOptions(
    string? WorkingDirectory = null,
    bool UseSudo = false,
    TimeSpan? Timeout = null);

public sealed record RemoteCommandResult(
    int ExitCode,
    string StandardOutput,
    string StandardError,
    string CommandText);

public sealed record RemoteFileStat(
    bool Exists,
    bool IsDirectory,
    long Size,
    DateTimeOffset? LastWriteUtc);

public interface ISshTransport
{
    Task<string> GetHostFingerprintAsync(ResolvedTargetConfiguration target, CancellationToken cancellationToken);

    Task<RemoteCommandResult> ExecuteAsync(
        ResolvedTargetConfiguration target,
        IReadOnlyList<string> command,
        RemoteExecutionOptions options,
        CancellationToken cancellationToken);

    Task EnsureDirectoryAsync(ResolvedTargetConfiguration target, string remotePath, bool useSudo, CancellationToken cancellationToken);

    Task UploadBytesAsync(
        ResolvedTargetConfiguration target,
        string remotePath,
        byte[] content,
        bool ensureParentDirectory,
        CancellationToken cancellationToken);

    Task<string> ReadTextAsync(ResolvedTargetConfiguration target, string remotePath, int maxBytes, CancellationToken cancellationToken);

    Task<byte[]> ReadBytesAsync(ResolvedTargetConfiguration target, string remotePath, long offset, int maxBytes, CancellationToken cancellationToken);

    Task<RemoteFileStat> StatAsync(ResolvedTargetConfiguration target, string remotePath, CancellationToken cancellationToken);

    Task DeleteAsync(ResolvedTargetConfiguration target, string remotePath, bool recursive, bool useSudo, CancellationToken cancellationToken);
}
