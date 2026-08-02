using System.Text;

namespace CanDoItAll.AgentFramework.Core;

internal enum WorkspaceTextGuardFailure
{
    None,
    TooLarge,
    Binary,
    Inaccessible,
    ReadFailed
}

internal readonly record struct WorkspaceTextLoadResult(
    bool Succeeded,
    string Content,
    int TotalCharacters,
    bool IsTruncated,
    WorkspaceTextGuardFailure Failure,
    string Message);

internal sealed class WorkspaceTextContentGuard
{
    private const long MaxReadableFileBytes = 256 * 1024;
    private const long MaxSearchableFileBytes = 256 * 1024;
    private const long MaxDiffFileBytes = 256 * 1024;
    private const int BinaryProbeBytes = 4096;

    private readonly Func<string, Stream> openRead;

    public WorkspaceTextContentGuard()
        : this(File.OpenRead)
    {
    }

    internal WorkspaceTextContentGuard(Func<string, Stream> openRead)
    {
        this.openRead = openRead ?? throw new ArgumentNullException(nameof(openRead));
    }

    public WorkspaceTextLoadResult LoadForRead(string fullPath, string relativePath, int maxCharacters)
    {
        var loaded = TryLoadText(
            fullPath,
            relativePath,
            MaxReadableFileBytes,
            "read",
            propagateUnauthorizedAccess: true);
        if (!loaded.Succeeded)
        {
            return loaded;
        }

        var safeMaxCharacters = Math.Clamp(
            maxCharacters,
            1,
            WorkspaceFileLimits.MaxTextReadCharacters);
        var isTruncated = loaded.Content.Length > safeMaxCharacters;
        var preview = isTruncated ? loaded.Content[..safeMaxCharacters] : loaded.Content;
        return new WorkspaceTextLoadResult(
            Succeeded: true,
            Content: preview,
            TotalCharacters: loaded.Content.Length,
            IsTruncated: isTruncated,
            Failure: WorkspaceTextGuardFailure.None,
            Message: string.Empty);
    }

    public WorkspaceTextLoadResult LoadForDiff(string fullPath, string relativePath)
        => TryLoadText(
            fullPath,
            relativePath,
            MaxDiffFileBytes,
            "diff",
            propagateUnauthorizedAccess: true);

    public WorkspaceTextGuardFailure TryLoadForSearch(string fullPath, string relativePath, out string text)
    {
        var loaded = TryLoadText(
            fullPath,
            relativePath,
            MaxSearchableFileBytes,
            "search",
            propagateUnauthorizedAccess: false,
            classifyIoAsInaccessible: true);
        text = loaded.Content;
        return loaded.Failure;
    }

    private WorkspaceTextLoadResult TryLoadText(
        string fullPath,
        string relativePath,
        long maxBytes,
        string operationName,
        bool propagateUnauthorizedAccess = false,
        bool classifyIoAsInaccessible = false)
    {
        try
        {
            using var stream = openRead(fullPath);
            var bytes = ReadBounded(stream, maxBytes, out var exceedsLimit);
            if (exceedsLimit)
            {
                return new WorkspaceTextLoadResult(
                    Succeeded: false,
                    Content: string.Empty,
                    TotalCharacters: 0,
                    IsTruncated: false,
                    Failure: WorkspaceTextGuardFailure.TooLarge,
                    Message: $"File '{relativePath}' exceeds the safe {operationName} limit of {maxBytes} bytes.");
            }

            if (LooksBinary(bytes))
            {
                return new WorkspaceTextLoadResult(
                    Succeeded: false,
                    Content: string.Empty,
                    TotalCharacters: 0,
                    IsTruncated: false,
                    Failure: WorkspaceTextGuardFailure.Binary,
                    Message: $"File '{relativePath}' appears to be binary and cannot be {operationName}ed as text.");
            }

            var content = Encoding.UTF8.GetString(bytes);
            if (content.Length > 0 && content[0] == '\uFEFF')
            {
                content = content[1..];
            }

            return new WorkspaceTextLoadResult(
                Succeeded: true,
                Content: content,
                TotalCharacters: content.Length,
                IsTruncated: false,
                Failure: WorkspaceTextGuardFailure.None,
                Message: string.Empty);
        }
        catch (UnauthorizedAccessException) when (propagateUnauthorizedAccess)
        {
            throw;
        }
        catch (UnauthorizedAccessException)
        {
            return CreateReadFailure(
                relativePath,
                operationName,
                classifyIoAsInaccessible
                    ? WorkspaceTextGuardFailure.Inaccessible
                    : WorkspaceTextGuardFailure.ReadFailed);
        }
        catch (IOException)
        {
            return CreateReadFailure(
                relativePath,
                operationName,
                classifyIoAsInaccessible
                    ? WorkspaceTextGuardFailure.Inaccessible
                    : WorkspaceTextGuardFailure.ReadFailed);
        }
        catch (Exception)
        {
            return CreateReadFailure(
                relativePath,
                operationName,
                WorkspaceTextGuardFailure.ReadFailed);
        }
    }

    private static WorkspaceTextLoadResult CreateReadFailure(
        string relativePath,
        string operationName,
        WorkspaceTextGuardFailure failure)
        => new(
            Succeeded: false,
            Content: string.Empty,
            TotalCharacters: 0,
            IsTruncated: false,
            Failure: failure,
            Message: $"Failed to read '{relativePath}' for {operationName}.");

    private static byte[] ReadBounded(Stream stream, long maxBytes, out bool exceedsLimit)
    {
        var buffer = new byte[checked((int)maxBytes + 1)];
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var read = stream.Read(buffer, totalRead, buffer.Length - totalRead);
            if (read == 0)
            {
                break;
            }

            totalRead += read;
        }

        exceedsLimit = totalRead > maxBytes;
        return exceedsLimit
            ? []
            : buffer[..totalRead];
    }

    private static bool LooksBinary(ReadOnlySpan<byte> content)
    {
        var probe = content[..Math.Min(content.Length, BinaryProbeBytes)];
        var suspicious = 0;
        foreach (var value in probe)
        {
            if (value == 0)
            {
                return true;
            }

            if (value < 0x09 || (value > 0x0D && value < 0x20))
            {
                suspicious++;
            }
        }

        return suspicious > Math.Max(3, probe.Length / 10);
    }
}
