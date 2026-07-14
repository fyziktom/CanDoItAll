using CanDoItAll.FileTools.FileBrowser;
using CanDoItAll.FileTools.FileInteraction;

namespace CanDoItAll.FileTools.Integration;

public static class FileToolsExternalOpenPolicy
{
    private static readonly HashSet<string> SystemAssociatedDocumentExtensions = new(
        StringComparer.OrdinalIgnoreCase)
    {
        ".bmp",
        ".csv",
        ".doc",
        ".docx",
        ".dot",
        ".dotx",
        ".epub",
        ".gif",
        ".jpeg",
        ".jpg",
        ".key",
        ".m4a",
        ".md",
        ".mkv",
        ".mov",
        ".mp3",
        ".mp4",
        ".numbers",
        ".odp",
        ".ods",
        ".odt",
        ".ogg",
        ".pages",
        ".pdf",
        ".png",
        ".pot",
        ".potx",
        ".pps",
        ".ppsx",
        ".ppt",
        ".pptx",
        ".rtf",
        ".svg",
        ".tif",
        ".tiff",
        ".tsv",
        ".txt",
        ".wav",
        ".webm",
        ".webp",
        ".xls",
        ".xlsx",
        ".xlt",
        ".xltx"
    };

    public static bool IsAllowedSystemAssociatedFile(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        return SystemAssociatedDocumentExtensions.Contains(Path.GetExtension(fileName));
    }
}

public enum FileToolsLocalFileAction
{
    OpenInPreferredApplication,
    OpenContainingFolder
}

public static class FileToolsBrowseHostActionIds
{
    public const string OpenContainingFolder = "host:open-containing-folder";
}

public enum FileToolsBrowseItemActionFailureCode
{
    TargetUnavailable,
    PreferredApplicationUnavailable,
    LaunchFailed
}

public readonly record struct FileToolsBrowseSourceActionAvailability(
    bool SupportsLocalOpen,
    bool SupportsDownload);

public interface IFileToolsBrowseSourceActionCapabilities
{
    FileToolsBrowseSourceActionAvailability ActionAvailability { get; }
}

public sealed record FileToolsBrowseItemActionResult
{
    private FileToolsBrowseItemActionResult(
        string message,
        FileToolsBrowseItemActionFailureCode? failureCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        Message = message.Trim();
        FailureCode = failureCode;
    }

    public bool IsSuccess => FailureCode is null;

    public string Message { get; }

    public FileToolsBrowseItemActionFailureCode? FailureCode { get; }

    public static FileToolsBrowseItemActionResult Success(string message)
        => new(message, null);

    public static FileToolsBrowseItemActionResult Failure(
        FileToolsBrowseItemActionFailureCode failureCode,
        string message)
        => new(message, failureCode);
}

public interface IFileToolsDownloadLease : IAsyncDisposable
{
    string FileName { get; }

    ValueTask<FileContentLease> OpenReadAsync(CancellationToken cancellationToken = default);
}

public interface IFileToolsBrowseItemActionService
{
    bool IsLocalLaunchAvailable { get; }

    ValueTask<FileToolsBrowseItemActionResult> LaunchAsync(
        FileToolsSemanticScope scope,
        FileBrowserItemKey itemKey,
        FileToolsLocalFileAction action,
        CancellationToken cancellationToken = default);

    ValueTask<IFileToolsDownloadLease> AuthorizeDownloadAsync(
        FileToolsSemanticScope scope,
        FileBrowserItemKey itemKey,
        CancellationToken cancellationToken = default);
}

public interface IFileToolsKnownFileActionService
{
    bool IsLocalLaunchAvailable { get; }

    ValueTask<FileToolsBrowseItemActionResult> LaunchAsync(
        FileToolsSemanticScope scope,
        FileToolsKnownFileOccurrence occurrence,
        FileToolsLocalFileAction action,
        CancellationToken cancellationToken = default);
}
