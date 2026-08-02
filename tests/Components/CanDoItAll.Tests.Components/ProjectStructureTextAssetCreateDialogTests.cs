using System.Text;
using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class ProjectStructureTextAssetCreateDialogTests
{
    [Fact]
    public async Task Create_new_markdown_returns_stored_media_and_keeps_notes_descriptive()
    {
        using var context = CreateContext();
        var host = context.Render<DialogHost>();
        Task<object?> resultTask = OpenDialog(context, ProjectFileSubtype.Markdown);

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

    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        context.Services.AddSingleton(new ProjectAssetContentGeneratorResolver(
            [new ProjectTextAssetContentGenerator()]));
        context.Services.AddSingleton<ProjectAssetCreationService>();
        return context;
    }

    private static Task<object?> OpenDialog(BunitContext context, ProjectFileSubtype subtype)
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
            subtype.ToString(),
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
        return context.Services.GetRequiredService<DialogService>()
            .OpenAsync<ProjectStructureTextAssetCreateDialog>(
                "Add file",
                new Dictionary<string, object?>
                {
                    [nameof(ProjectStructureTextAssetCreateDialog.CreateRequest)] = request,
                    [nameof(ProjectStructureTextAssetCreateDialog.Definition)] = definition
                },
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
            _ => throw new ArgumentOutOfRangeException(nameof(subtype))
        };

    private static string ResolveAccept(ProjectFileSubtype subtype)
        => subtype switch
        {
            ProjectFileSubtype.Text => ".txt,text/plain",
            ProjectFileSubtype.Json => ".json,application/json",
            ProjectFileSubtype.Markdown => ".md,.markdown,text/markdown,text/plain",
            ProjectFileSubtype.Mermaid => ".mmd,.mermaid,text/vnd.mermaid,text/plain",
            _ => throw new ArgumentOutOfRangeException(nameof(subtype))
        };
}
