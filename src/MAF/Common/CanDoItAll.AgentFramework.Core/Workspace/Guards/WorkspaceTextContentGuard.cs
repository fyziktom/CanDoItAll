namespace CanDoItAll.AgentFramework.Core;

internal enum WorkspaceTextGuardFailure
{
    None,
    TooLarge,
    Binary,
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

    public WorkspaceTextLoadResult LoadForRead(string fullPath, string relativePath, int maxCharacters)
    {
        var loaded = TryLoadText(fullPath, relativePath, MaxReadableFileBytes, "read");
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
        => TryLoadText(fullPath, relativePath, MaxDiffFileBytes, "diff");

    public WorkspaceTextGuardFailure TryLoadForSearch(string fullPath, string relativePath, out string text)
    {
        var loaded = TryLoadText(fullPath, relativePath, MaxSearchableFileBytes, "search");
        text = loaded.Content;
        return loaded.Failure;
    }

    private static WorkspaceTextLoadResult TryLoadText(string fullPath, string relativePath, long maxBytes, string operationName)
    {
        try
        {
            var fileInfo = new FileInfo(fullPath);
            if (fileInfo.Length > maxBytes)
            {
                return new WorkspaceTextLoadResult(
                    Succeeded: false,
                    Content: string.Empty,
                    TotalCharacters: 0,
                    IsTruncated: false,
                    Failure: WorkspaceTextGuardFailure.TooLarge,
                    Message: $"File '{relativePath}' exceeds the safe {operationName} limit of {maxBytes} bytes.");
            }

            if (LooksBinary(fullPath))
            {
                return new WorkspaceTextLoadResult(
                    Succeeded: false,
                    Content: string.Empty,
                    TotalCharacters: 0,
                    IsTruncated: false,
                    Failure: WorkspaceTextGuardFailure.Binary,
                    Message: $"File '{relativePath}' appears to be binary and cannot be {operationName}ed as text.");
            }

            var content = File.ReadAllText(fullPath);
            return new WorkspaceTextLoadResult(
                Succeeded: true,
                Content: content,
                TotalCharacters: content.Length,
                IsTruncated: false,
                Failure: WorkspaceTextGuardFailure.None,
                Message: string.Empty);
        }
        catch (Exception exception)
        {
            return new WorkspaceTextLoadResult(
                Succeeded: false,
                Content: string.Empty,
                TotalCharacters: 0,
                IsTruncated: false,
                Failure: WorkspaceTextGuardFailure.ReadFailed,
                Message: $"Failed to read '{relativePath}': {exception.Message}");
        }
    }

    private static bool LooksBinary(string fullPath)
    {
        using var stream = File.OpenRead(fullPath);
        var buffer = new byte[BinaryProbeBytes];
        var read = stream.Read(buffer, 0, buffer.Length);
        if (read == 0)
        {
            return false;
        }

        var suspicious = 0;
        for (var index = 0; index < read; index++)
        {
            var value = buffer[index];
            if (value == 0)
            {
                return true;
            }

            if (value < 0x09 || (value > 0x0D && value < 0x20))
            {
                suspicious++;
            }
        }

        return suspicious > Math.Max(3, read / 10);
    }
}
