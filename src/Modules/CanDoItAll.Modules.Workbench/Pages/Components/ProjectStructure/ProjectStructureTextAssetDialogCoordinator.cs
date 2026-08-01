using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Workbench.Pages;

public sealed record ProjectStructureTextAssetDialogDefinition(
    ProjectFileSubtype FileSubtype,
    string Label,
    string TitleLabel,
    string TitlePlaceholder,
    string SubtitleLabel,
    string SubtitlePlaceholder,
    string NotesLabel,
    string NotesPlaceholder,
    string FileNamePlaceholder,
    string ContentPlaceholder,
    string AcceptedFileTypes,
    string FilePrompt,
    string SubmitLabel);

public sealed record ProjectStructureTextAssetDialogResult(
    CanvasWorkbenchCreateActionRequest CreateRequest,
    ProjectObjectMediaPayload Media);

internal delegate Task<ProjectStructureNode?> ProjectStructureTextAssetNodeCreator(
    ProjectStructureCreateLeafDefinition definition,
    CanvasWorkbenchCreateActionRequest request,
    ProjectObjectMediaPayload media);

internal sealed record ProjectStructureTextAssetDialogContext(
    Guid ProjectId,
    ProjectStructureTextAssetNodeCreator CreateNodeAsync);

internal sealed class ProjectStructureTextAssetDialogCoordinator(
    DialogService dialogService,
    NotificationService notificationService,
    ILogger<ProjectStructureTextAssetDialogCoordinator> logger)
{
    public async Task OpenCreateAsync(
        ProjectStructureTextAssetDialogContext context,
        ProjectStructureCreateLeafDefinition definition,
        CanvasWorkbenchCreateActionRequest createRequest,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(createRequest);
        if (!ProjectStructureCanvasCatalog.IsTextAssetAuthoringDefinition(definition))
        {
            throw new ArgumentException("A text-based file definition is required.", nameof(definition));
        }

        ProjectFileSubtype subtype = ProjectNodeKindRegistry.ResolveFileSubtype(
            definition.ObjectType,
            definition.ObjectSubtype);
        var dialogDefinition = new ProjectStructureTextAssetDialogDefinition(
            subtype,
            definition.Label,
            definition.TitleLabel,
            definition.TitlePlaceholder,
            definition.SubtitleLabel,
            definition.SubtitlePlaceholder,
            definition.NotesLabel,
            definition.NotesPlaceholder,
            ResolveFileNamePlaceholder(subtype),
            ResolveContentPlaceholder(subtype),
            definition.AcceptedFileTypes,
            definition.FilePrompt,
            definition.SubmitLabel);
        object? result = await dialogService.OpenAsync<ProjectStructureTextAssetCreateDialog>(
            $"Add {definition.Label.ToLowerInvariant()}",
            new Dictionary<string, object?>
            {
                [nameof(ProjectStructureTextAssetCreateDialog.CreateRequest)] = createRequest,
                [nameof(ProjectStructureTextAssetCreateDialog.Definition)] = dialogDefinition
            },
            new DialogOptions
            {
                Eyebrow = "Project structure asset",
                Subtitle = "Create a stored file from new content or attach an existing file without leaving the canvas.",
                Size = ModalSize.Wide,
                DenseChrome = true,
                TestId = "project-structure-text-asset-create-dialog",
                AriaLabel = $"Add {definition.Label.ToLowerInvariant()} project asset",
                ChromeCloseResult = null
            },
            cancellationToken);

        if (result is not ProjectStructureTextAssetDialogResult submission)
        {
            return;
        }

        try
        {
            await context.CreateNodeAsync(definition, submission.CreateRequest, submission.Media);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Project text asset creation failed. ProjectId={ProjectId} ActionId={ActionId} Subtype={Subtype}.",
                context.ProjectId,
                definition.ActionId,
                subtype);
            notificationService.Error(
                "Asset could not be created",
                exception is InvalidDataException
                    ? exception.Message
                    : "The file could not be saved. Check the application logs for details.");
        }
    }

    private static string ResolveFileNamePlaceholder(ProjectFileSubtype subtype)
        => subtype switch
        {
            ProjectFileSubtype.Text => "notes.txt",
            ProjectFileSubtype.Json => "settings.json",
            ProjectFileSubtype.Markdown => "README.md",
            ProjectFileSubtype.Mermaid => "diagram.mmd",
            _ => throw new ArgumentOutOfRangeException(nameof(subtype))
        };

    private static string ResolveContentPlaceholder(ProjectFileSubtype subtype)
        => subtype switch
        {
            ProjectFileSubtype.Text => "Enter plain text content",
            ProjectFileSubtype.Json => "{\n  \"key\": \"value\"\n}",
            ProjectFileSubtype.Markdown => "# Document title",
            ProjectFileSubtype.Mermaid => "graph TD\nA[Start] --> B[Done]",
            _ => throw new ArgumentOutOfRangeException(nameof(subtype))
        };
}
