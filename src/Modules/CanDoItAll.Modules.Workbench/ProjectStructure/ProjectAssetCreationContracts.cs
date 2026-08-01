namespace CanDoItAll.Modules.Workbench;

public enum ProjectAssetSourceMode
{
    UploadExisting,
    CreateNew
}

public enum ProjectAssetGenerationKind
{
    Text,
    Image,
    Spreadsheet
}

public enum ProjectAssetCreationErrorCode
{
    UnsupportedGenerationKind,
    UnsupportedFileSubtype,
    InvalidGeneratorRequest,
    InvalidFileName,
    InvalidContentType,
    InvalidContent,
    InvalidJson,
    ContentTooLarge
}

public static class ProjectAssetCreationLimits
{
    public const int MaximumEditableTextBytes = 16 * 1024 * 1024;
}

public abstract record ProjectAssetContentGenerationRequest(ProjectAssetGenerationKind GenerationKind);

public sealed record ProjectTextAssetContentGenerationRequest(
    ProjectFileSubtype FileSubtype,
    string FileName,
    string Content)
    : ProjectAssetContentGenerationRequest(ProjectAssetGenerationKind.Text);

public sealed record ProjectAssetContent(
    string FileName,
    string ContentType,
    ReadOnlyMemory<byte> Content);

public interface IProjectAssetContentGenerator
{
    ProjectAssetGenerationKind GenerationKind { get; }

    ValueTask<ProjectAssetContent> GenerateAsync(
        ProjectAssetContentGenerationRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class ProjectAssetCreationException : Exception
{
    public ProjectAssetCreationException(ProjectAssetCreationErrorCode code, string message)
        : base(message)
    {
        Code = code;
    }

    public ProjectAssetCreationException(
        ProjectAssetCreationErrorCode code,
        string message,
        Exception innerException)
        : base(message, innerException)
    {
        Code = code;
    }

    public ProjectAssetCreationErrorCode Code { get; }
}
