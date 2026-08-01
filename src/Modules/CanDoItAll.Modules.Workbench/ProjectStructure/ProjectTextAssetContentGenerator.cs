namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectTextAssetContentGenerator : IProjectAssetContentGenerator
{
    public ProjectAssetGenerationKind GenerationKind => ProjectAssetGenerationKind.Text;

    public ValueTask<ProjectAssetContent> GenerateAsync(
        ProjectAssetContentGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (request is not ProjectTextAssetContentGenerationRequest textRequest)
        {
            throw new ProjectAssetCreationException(
                ProjectAssetCreationErrorCode.InvalidGeneratorRequest,
                "The text asset generator requires a text asset generation request.");
        }

        ProjectTextAssetFormat format = ProjectTextAssetFormatCatalog.Resolve(textRequest.FileSubtype);
        string fileName = ProjectTextAssetFormatCatalog.NormalizeGeneratedFileName(textRequest.FileName, format);
        byte[] content = ProjectTextAssetContentPolicy.Encode(
            textRequest.FileSubtype,
            textRequest.Content,
            cancellationToken);

        return ValueTask.FromResult(new ProjectAssetContent(fileName, format.CanonicalContentType, content));
    }
}
