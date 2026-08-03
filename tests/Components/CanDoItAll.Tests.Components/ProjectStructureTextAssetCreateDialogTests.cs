using System.Text;
using AngleSharp.Html.Dom;
using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class ProjectStructureTextAssetCreateDialogTests
{
    [Fact]
    public async Task Create_new_markdown_returns_stored_media_and_keeps_notes_descriptive()
    {
        using var context = CreateContext();
        var host = context.Render<DialogHost>();
        Task<object?> resultTask = OpenDialog(
            context,
            ProjectFileSubtype.Markdown,
            includePersistenceCallback: false);

        host.WaitForElement("[data-testid='project-structure-text-asset-title']");
        host.Find("[data-testid='project-structure-text-asset-title']").Input("Architecture notes");
        host.Find("[data-testid='project-structure-text-asset-file-name']").Input("architecture.md");
        host.Find("[data-testid='project-structure-text-asset-content']").Input("# Architecture\n\nBoundary notes.");
        host.Find("[data-testid='project-structure-text-asset-notes']").Input("Explains the storage boundary.");
        host.Find("[data-testid='project-structure-text-asset-submit']").Click();

        var result = Assert.IsType<ProjectStructureTextAssetDialogResult>(
            await resultTask.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.Equal("Architecture notes", result.CreateRequest.Title);
        Assert.Equal("Explains the storage boundary.", result.CreateRequest.Notes);
        Assert.Equal("architecture.md", result.Media.FileName);
        Assert.Equal("text/markdown", result.Media.ContentType);
        Assert.Equal(
            "# Architecture\n\nBoundary notes.",
            Encoding.UTF8.GetString(Convert.FromBase64String(result.Media.Base64Data)));
    }

    [Fact]
    public void Upload_existing_mode_replaces_the_content_editor_with_shared_file_upload()
    {
        using var context = CreateContext();
        var host = context.Render<DialogHost>();
        _ = OpenDialog(context, ProjectFileSubtype.Text);

        host.WaitForElement("[data-testid='project-structure-text-asset-source-upload']").Click();

        host.WaitForAssertion(() =>
        {
            Assert.NotNull(host.Find("[data-testid='project-structure-text-asset-upload']"));
            Assert.Empty(host.FindAll("[data-testid='project-structure-text-asset-content']"));
            Assert.Empty(host.FindAll("[data-testid='project-structure-text-asset-file-name']"));
        });
    }

    [Fact]
    public async Task Upload_existing_markdown_returns_canonical_media_after_persistence_succeeds()
    {
        using var context = CreateContext();
        var host = context.Render<DialogHost>();
        ProjectStructureTextAssetDialogResult? persisted = null;
        Task<object?> resultTask = OpenDialog(
            context,
            ProjectFileSubtype.Markdown,
            submission =>
            {
                persisted = submission;
                return Task.CompletedTask;
            });

        host.WaitForElement("[data-testid='project-structure-text-asset-source-upload']").Click();
        IRenderedComponent<InputFile> inputFile = host.FindComponent<InputFile>();
        inputFile.UploadFiles(InputFileContent.CreateFromText(
            "# Existing document",
            "README.markdown",
            contentType: "text/plain"));
        host.Find("[data-testid='project-structure-text-asset-submit']").Click();

        var result = Assert.IsType<ProjectStructureTextAssetDialogResult>(
            await resultTask.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.Same(result, persisted);
        Assert.Equal("README", result.CreateRequest.Title);
        Assert.Equal("README.md", result.Media.FileName);
        Assert.Equal("text/markdown", result.Media.ContentType);
        Assert.Equal(
            "# Existing document",
            Encoding.UTF8.GetString(Convert.FromBase64String(result.Media.Base64Data)));
    }

    [Fact]
    public void Invalid_json_is_rejected_without_closing_the_dialog()
    {
        using var context = CreateContext();
        var host = context.Render<DialogHost>();
        _ = OpenDialog(context, ProjectFileSubtype.Json);

        host.WaitForElement("[data-testid='project-structure-text-asset-title']").Input("Settings");
        host.Find("[data-testid='project-structure-text-asset-content']").Input("{ invalid }");
        host.Find("[data-testid='project-structure-text-asset-submit']").Click();

        host.WaitForAssertion(() =>
        {
            Assert.Contains("valid JSON", host.Find("[data-testid='project-structure-text-asset-validation-error']").TextContent);
            Assert.NotNull(host.Find("[data-testid='project-structure-text-asset-create-dialog']"));
        });
    }

    [Fact]
    public void Persistence_failure_keeps_the_dialog_open_with_the_pasted_content()
    {
        using var context = CreateContext();
        var host = context.Render<DialogHost>();
        _ = OpenDialog(
            context,
            ProjectFileSubtype.Text,
            _ => throw new ProjectStructureTextAssetSubmissionException(
                "Storage is temporarily unavailable.",
                new IOException("Test failure.")));

        host.WaitForElement("[data-testid='project-structure-text-asset-title']").Input("Runbook");
        host.Find("[data-testid='project-structure-text-asset-content']").Input("Keep this pasted content.");
        host.Find("[data-testid='project-structure-text-asset-submit']").Click();

        host.WaitForAssertion(() =>
        {
            Assert.Contains(
                "temporarily unavailable",
                host.Find("[data-testid='project-structure-text-asset-validation-error']").TextContent,
                StringComparison.Ordinal);
            var contentEditor = Assert.IsAssignableFrom<IHtmlTextAreaElement>(
                host.Find("[data-testid='project-structure-text-asset-content']"));
            Assert.Equal("Keep this pasted content.", contentEditor.Value);
            Assert.NotNull(host.Find("[data-testid='project-structure-text-asset-create-dialog']"));
        });
    }

    [Fact]
    public async Task Queued_double_submit_persists_the_asset_once()
    {
        using var context = CreateContext();
        var host = context.Render<DialogHost>();
        var persistenceStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releasePersistence = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var persistenceCount = 0;
        Task<object?> resultTask = OpenDialog(
            context,
            ProjectFileSubtype.Markdown,
            async _ =>
            {
                persistenceCount++;
                persistenceStarted.TrySetResult();
                await releasePersistence.Task;
            });

        host.WaitForElement("[data-testid='project-structure-text-asset-title']").Input("Architecture notes");
        host.Find("[data-testid='project-structure-text-asset-content']").Input("# Architecture");
        Task firstSubmit = host.InvokeAsync(
            () => host.Find("[data-testid='project-structure-text-asset-submit']").Click());
        await persistenceStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Task secondSubmit = host.InvokeAsync(() =>
            host.Find("[data-testid='project-structure-text-asset-submit']")
                .TriggerEvent("onclick", new MouseEventArgs()));
        releasePersistence.TrySetResult();

        await firstSubmit.WaitAsync(TimeSpan.FromSeconds(2));
        await secondSubmit.WaitAsync(TimeSpan.FromSeconds(2));
        _ = await resultTask.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(1, persistenceCount);
    }

    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        context.Services.AddSingleton<ProjectAssetCreationService>();
        return context;
    }

    private static Task<object?> OpenDialog(
        BunitContext context,
        ProjectFileSubtype subtype,
        Func<ProjectStructureTextAssetDialogResult, Task>? persistSubmissionAsync = null,
        bool includePersistenceCallback = true)
    {
        var request = new CanvasWorkbenchCreateActionRequest(
            $"add-file-{subtype.ToString().ToLowerInvariant()}",
            "parent-node",
            420,
            260,
            "parent-node",
            string.Empty,
            "docs",
            string.Empty,
            "child",
            "dialog",
            subtype.ToString().ToLowerInvariant(),
            null);
        var definition = new ProjectStructureTextAssetDialogDefinition(
            subtype,
            "File",
            "File title",
            "Runbook",
            "Folder",
            "docs",
            "Purpose",
            "Describe this file",
            ResolveFileName(subtype),
            "Enter content",
            ResolveAccept(subtype),
            "Drop a file here or choose one.",
            "Add file");
        var parameters = new Dictionary<string, object?>
        {
            [nameof(ProjectStructureTextAssetCreateDialog.CreateRequest)] = request,
            [nameof(ProjectStructureTextAssetCreateDialog.Definition)] = definition
        };
        if (includePersistenceCallback)
        {
            parameters[nameof(ProjectStructureTextAssetCreateDialog.PersistSubmissionAsync)] =
                persistSubmissionAsync ?? (_ => Task.CompletedTask);
        }

        return context.Services.GetRequiredService<DialogService>()
            .OpenAsync<ProjectStructureTextAssetCreateDialog>(
                "Add file",
                parameters,
                new DialogOptions
                {
                    TestId = "project-structure-text-asset-create-dialog"
                });
    }

    private static string ResolveFileName(ProjectFileSubtype subtype)
        => subtype switch
        {
            ProjectFileSubtype.Text => "notes.txt",
            ProjectFileSubtype.Json => "settings.json",
            ProjectFileSubtype.Markdown => "README.md",
            ProjectFileSubtype.Mermaid => "diagram.mmd",
            ProjectFileSubtype.Log => "build-output.log",
            _ => throw new ArgumentOutOfRangeException(nameof(subtype))
        };

    private static string ResolveAccept(ProjectFileSubtype subtype)
        => subtype switch
        {
            ProjectFileSubtype.Text => ".txt,text/plain",
            ProjectFileSubtype.Json => ".json,application/json",
            ProjectFileSubtype.Markdown => ".md,.markdown,text/markdown,text/plain",
            ProjectFileSubtype.Mermaid => ".mmd,.mermaid,text/vnd.mermaid,text/plain",
            ProjectFileSubtype.Log => ".log,.txt,text/plain",
            _ => throw new ArgumentOutOfRangeException(nameof(subtype))
        };
}
