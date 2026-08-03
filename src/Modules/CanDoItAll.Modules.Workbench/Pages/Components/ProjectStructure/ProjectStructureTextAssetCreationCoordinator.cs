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
    ProjectObjectMediaPayload media,
    CancellationToken cancellationToken);

internal sealed record ProjectStructureTextAssetCreationContext(
    Guid ProjectId,
    ProjectStructureTextAssetNodeCreator CreateNodeAsync);

internal sealed class ProjectStructureTextAssetSubmissionException(
    string message,
    Exception innerException)
    : Exception(message, innerException);

internal sealed class ProjectStructureTextAssetCreationCoordinator(
    DialogService dialogService,
    NotificationService notificationService,
    ProjectAssetCreationService assetCreationService,
    ILogger<ProjectStructureTextAssetCreationCoordinator> logger)
{
    public async Task CreateAsync(
        ProjectStructureTextAssetCreationContext context,
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
        try
        {
            if (createRequest.UploadedFile is { } uploadedFile)
            {
                ProjectObjectMediaPayload media = assetCreationService.AdaptEncodedTextUpload(
                    subtype,
                    uploadedFile.FileName,
                    uploadedFile.ContentType,
                    uploadedFile.Base64Data,
                    cancellationToken);
                string title = string.IsNullOrWhiteSpace(createRequest.Title)
                    ? Path.GetFileNameWithoutExtension(media.FileName)
                    : createRequest.Title;
                await CreateNodeAsync(
                    context,
                    definition,
                    createRequest with
                    {
                        Title = title,
                        UploadedFile = null
                    },
                    media,
                    cancellationToken);
                return;
            }

            await OpenDialogAsync(
                context,
                definition,
                createRequest,
                subtype,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (ProjectStructureNodeCreatedWithFollowUpFailureException exception)
        {
            ReportCommittedWithFollowUpFailure(context, definition, subtype, exception);
        }
        catch (Exception exception)
        {
            LogFailure(context, definition, subtype, exception);
            notificationService.Error(
                "Asset could not be created",
                ResolveUserMessage(exception));
        }
    }

    private async Task OpenDialogAsync(
        ProjectStructureTextAssetCreationContext context,
        ProjectStructureCreateLeafDefinition definition,
        CanvasWorkbenchCreateActionRequest createRequest,
        ProjectFileSubtype subtype,
        CancellationToken cancellationToken)
    {
        using var dialogCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        CancellationToken dialogCancellationToken = dialogCancellationSource.Token;
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
        Func<ProjectStructureTextAssetDialogResult, Task> persistSubmissionAsync = submission =>
            PersistDialogSubmissionAsync(
                context,
                definition,
                subtype,
                submission,
                dialogCancellationToken);

        try
        {
            await dialogService.OpenAsync<ProjectStructureTextAssetCreateDialog>(
                $"Add {definition.Label.ToLowerInvariant()}",
                new Dictionary<string, object?>
                {
                    [nameof(ProjectStructureTextAssetCreateDialog.CreateRequest)] = createRequest,
                    [nameof(ProjectStructureTextAssetCreateDialog.Definition)] = dialogDefinition,
                    [nameof(ProjectStructureTextAssetCreateDialog.PersistSubmissionAsync)] = persistSubmissionAsync,
                    [nameof(ProjectStructureTextAssetCreateDialog.CancellationToken)] = dialogCancellationToken
                },
                new DialogOptions
                {
                    Eyebrow = "Project structure asset",
                    Subtitle = "Create a stored file from new content or attach an existing file without leaving the canvas.",
                    Size = ModalSize.Wide,
                    DenseChrome = true,
                    TestId = "project-structure-text-asset-create-dialog",
                    AriaLabel = $"Add {definition.Label.ToLowerInvariant()} project asset",
                    CloseOnBackdrop = false,
                    ShowCloseButton = false,
                    ChromeCloseResult = null
                },
                dialogCancellationToken);
        }
        finally
        {
            await dialogCancellationSource.CancelAsync();
        }
    }

    private async Task PersistDialogSubmissionAsync(
        ProjectStructureTextAssetCreationContext context,
        ProjectStructureCreateLeafDefinition definition,
        ProjectFileSubtype subtype,
        ProjectStructureTextAssetDialogResult submission,
        CancellationToken cancellationToken)
    {
        try
        {
            await CreateNodeAsync(
                context,
                definition,
                submission.CreateRequest,
                submission.Media,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (ProjectStructureNodeCreatedWithFollowUpFailureException exception)
        {
            ReportCommittedWithFollowUpFailure(context, definition, subtype, exception);
        }
        catch (Exception exception)
        {
            LogFailure(context, definition, subtype, exception);
            throw new ProjectStructureTextAssetSubmissionException(
                ResolveUserMessage(exception),
                exception);
        }
    }

    private static async Task CreateNodeAsync(
        ProjectStructureTextAssetCreationContext context,
        ProjectStructureCreateLeafDefinition definition,
        CanvasWorkbenchCreateActionRequest createRequest,
        ProjectObjectMediaPayload media,
        CancellationToken cancellationToken)
    {
        ProjectStructureNode? created = await context.CreateNodeAsync(
            definition,
            createRequest,
            media,
            cancellationToken);
        if (created is null)
        {
            throw new InvalidOperationException("Text asset creation completed without a persisted node.");
        }
    }

    private void LogFailure(
        ProjectStructureTextAssetCreationContext context,
        ProjectStructureCreateLeafDefinition definition,
        ProjectFileSubtype subtype,
        Exception exception)
    {
        logger.LogError(
            exception,
            "Project text asset creation failed. ProjectId={ProjectId} ActionId={ActionId} Subtype={Subtype}.",
            context.ProjectId,
            definition.ActionId,
            subtype);
    }

    private void ReportCommittedWithFollowUpFailure(
        ProjectStructureTextAssetCreationContext context,
        ProjectStructureCreateLeafDefinition definition,
        ProjectFileSubtype subtype,
        ProjectStructureNodeCreatedWithFollowUpFailureException exception)
    {
        logger.LogError(
            exception.InnerException,
            "Project text asset was committed but follow-up project structure work failed. ProjectId={ProjectId} ActionId={ActionId} Subtype={Subtype} NodeId={NodeId}.",
            context.ProjectId,
            definition.ActionId,
            subtype,
            exception.CreatedNode.Id);
        notificationService.Warning(
            "Asset created; refresh required",
            $"{exception.CreatedNode.Title} was saved. Reload the project structure to see the latest state.");
    }

    private static string ResolveUserMessage(Exception exception)
        => exception is InvalidDataException or ProjectAssetCreationException
            ? exception.Message
            : "The file could not be saved. Check the application logs for details.";

    private static string ResolveFileNamePlaceholder(ProjectFileSubtype subtype)
        => subtype switch
        {
            ProjectFileSubtype.Text => "notes.txt",
            ProjectFileSubtype.Json => "settings.json",
            ProjectFileSubtype.Markdown => "README.md",
            ProjectFileSubtype.Mermaid => "diagram.mmd",
            ProjectFileSubtype.Log => "build-output.log",
            _ => throw new ArgumentOutOfRangeException(nameof(subtype))
        };

    private static string ResolveContentPlaceholder(ProjectFileSubtype subtype)
        => subtype switch
        {
            ProjectFileSubtype.Text => "Enter plain text content",
            ProjectFileSubtype.Json => "{\n  \"key\": \"value\"\n}",
            ProjectFileSubtype.Markdown => "# Document title",
            ProjectFileSubtype.Mermaid => "graph TD\nA[Start] --> B[Done]",
            ProjectFileSubtype.Log => "Paste log output",
            _ => throw new ArgumentOutOfRangeException(nameof(subtype))
        };
}
