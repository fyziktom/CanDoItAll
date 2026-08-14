using System.Buffers;

namespace CanDoItAll.Manager;

internal interface ILinuxProcfsReader
{
    Task<string> ReadTextAsync(string relativePath, int maximumBytes, CancellationToken cancellationToken);

    Task<byte[]> ReadBytesAsync(string relativePath, int maximumBytes, CancellationToken cancellationToken);

    string ResolveLink(string relativePath);
}

internal sealed class LinuxProcfsReader(string procRoot = "/proc") : ILinuxProcfsReader
{
    private readonly string procRoot = Path.GetFullPath(procRoot);

    public async Task<string> ReadTextAsync(
        string relativePath,
        int maximumBytes,
        CancellationToken cancellationToken)
        => System.Text.Encoding.UTF8.GetString(
            await ReadBytesAsync(relativePath, maximumBytes, cancellationToken).ConfigureAwait(false));

    public async Task<byte[]> ReadBytesAsync(
        string relativePath,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        var path = ResolveContained(relativePath);
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var buffer = ArrayPool<byte>.Shared.Rent(maximumBytes + 1);
        try
        {
            var total = 0;
            while (total <= maximumBytes)
            {
                var read = await stream.ReadAsync(
                    buffer.AsMemory(total, maximumBytes + 1 - total),
                    cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    return buffer.AsSpan(0, total).ToArray();
                }

                total += read;
            }

            throw new InvalidDataException("The procfs process field exceeds the bounded read limit.");
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public string ResolveLink(string relativePath)
    {
        var path = ResolveContained(relativePath);
        return File.ResolveLinkTarget(path, returnFinalTarget: true)?.FullName
               ?? throw new IOException("The procfs executable link could not be resolved.");
    }

    private string ResolveContained(string relativePath)
    {
        var fullPath = Path.GetFullPath(relativePath, procRoot);
        var rootPrefix = procRoot.EndsWith(Path.DirectorySeparatorChar)
            ? procRoot
            : procRoot + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(rootPrefix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The procfs path escaped the configured root.");
        }

        return fullPath;
    }
}

internal sealed class LinuxManagerProcessDiscovery(ILinuxProcfsReader reader) : IManagerProcessDiscovery
{
    private const int StatLimit = 16 * 1024;
    private const int StatusLimit = 64 * 1024;
    private const int CommandLimit = 256 * 1024;

    public LinuxManagerProcessDiscovery()
        : this(new LinuxProcfsReader())
    {
    }

    public async Task<ManagerProcessDiscoveryResult> ProbeAsync(
        int processId,
        CancellationToken cancellationToken = default)
    {
        if (processId <= 0)
        {
            return ManagerProcessDiscoveryResult.Unavailable(
                ManagerProcessDiscoveryStatus.Incomplete,
                "invalid-pid");
        }

        try
        {
            var prefix = processId.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var stat = await reader.ReadTextAsync($"{prefix}/stat", StatLimit, cancellationToken).ConfigureAwait(false);
            var status = await reader.ReadTextAsync($"{prefix}/status", StatusLimit, cancellationToken).ConfigureAwait(false);
            var command = await reader.ReadBytesAsync($"{prefix}/cmdline", CommandLimit, cancellationToken).ConfigureAwait(false);
            var executablePath = reader.ResolveLink($"{prefix}/exe");
            return TryParse(processId, stat, status, command, executablePath, out var evidence)
                ? ManagerProcessDiscoveryResult.Available(evidence!)
                : ManagerProcessDiscoveryResult.Unavailable(
                    ManagerProcessDiscoveryStatus.Incomplete,
                    "linux-proc-evidence-incomplete");
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return ManagerProcessDiscoveryResult.Unavailable(
                ManagerProcessDiscoveryStatus.Exited,
                "process-exited-during-probe");
        }
        catch (UnauthorizedAccessException)
        {
            return ManagerProcessDiscoveryResult.Unavailable(
                ManagerProcessDiscoveryStatus.PermissionDenied,
                "linux-proc-permission-denied");
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or InvalidOperationException)
        {
            return ManagerProcessDiscoveryResult.Unavailable(
                ManagerProcessDiscoveryStatus.Incomplete,
                "linux-proc-read-incomplete");
        }
    }

    internal static bool TryParse(
        int processId,
        string stat,
        string status,
        byte[] commandLine,
        string executablePath,
        out ManagerProcessEvidence? evidence)
    {
        evidence = null;
        var closeParenthesis = stat.LastIndexOf(')');
        if (closeParenthesis < 0 || closeParenthesis + 2 >= stat.Length)
        {
            return false;
        }

        var fields = stat[(closeParenthesis + 2)..]
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (fields.Length < 20 ||
            !int.TryParse(fields[1], out var parentProcessId) ||
            !ulong.TryParse(fields[19], out var startTicks) ||
            startTicks == 0)
        {
            return false;
        }

        var uidLine = status
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(line => line.StartsWith("Uid:", StringComparison.Ordinal));
        var uid = uidLine?
            .Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Skip(1)
            .FirstOrDefault();
        if (string.IsNullOrWhiteSpace(uid) ||
            commandLine.Length == 0 ||
            commandLine.All(value => value == 0) ||
            string.IsNullOrWhiteSpace(executablePath) ||
            !Path.IsPathRooted(executablePath))
        {
            return false;
        }

        evidence = new ManagerProcessEvidence(
            processId,
            $"linux-proc-start:{startTicks}",
            Path.GetFullPath(executablePath),
            ManagerProcessFingerprint.ComputeObservedCommand(commandLine),
            $"uid:{uid}",
            parentProcessId);
        return true;
    }
}
