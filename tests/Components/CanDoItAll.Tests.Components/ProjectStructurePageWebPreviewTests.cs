using System.Reflection;
using Bunit;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.ProjectStructure;

[Trait("Category", "HostPlatform")]
public sealed class ProjectStructurePageWebPreviewTests
{
    [Fact]
    public async Task Opening_a_web_link_renders_the_dialog_without_an_intermediate_overlay_change()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var projectId = await CreateProjectAsync(projectsService);
        var linkNode = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Link,
                "Google",
                "External search",
                string.Empty,
                $"project:{projectId}",
                420,
                240,
                MetadataJson: ProjectObjectMetadataSerializer.Serialize(new ProjectObjectMetadataEnvelope
                {
                    Link = new ProjectLinkMetadata
                    {
                        Url = "https://google.com/"
                    }
                })));

        var page = harness.Context.Render<ProjectStructurePage>(parameters => parameters
            .Add(component => component.ProjectId, projectId));
        var canvasWorkbench = WaitForCanvasWorkbench(page);

        await page.InvokeAsync(() => canvasWorkbench.Instance.NodeOpened.InvokeAsync(linkNode.Id));

        var dialog = page.WaitForElement("[data-testid='project-structure-web-preview-dialog']");
        Assert.Contains("Google", dialog.TextContent, StringComparison.Ordinal);
        Assert.Contains("cannot be embedded", dialog.TextContent, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(dialog.QuerySelectorAll("iframe"));

        var externalLink = Assert.IsAssignableFrom<AngleSharp.Dom.IElement>(
            dialog.QuerySelector("[data-testid='project-structure-web-preview-open-browser']"));
        Assert.Equal("a", externalLink.LocalName);
        Assert.Equal("https://google.com/", externalLink.GetAttribute("href"));
        Assert.Equal("_blank", externalLink.GetAttribute("target"));
    }

    [Fact]
    public async Task Open_web_preview_rerenders_when_only_the_source_label_changes()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var projectId = await CreateProjectAsync(projectsService);
        var linkNode = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Link,
                "Google",
                "External search",
                string.Empty,
                $"project:{projectId}",
                420,
                240,
                MetadataJson: ProjectObjectMetadataSerializer.Serialize(new ProjectObjectMetadataEnvelope
                {
                    Link = new ProjectLinkMetadata
                    {
                        Url = "https://google.com/"
                    }
                })));

        var page = harness.Context.Render<ProjectStructurePage>(parameters => parameters
            .Add(component => component.ProjectId, projectId));
        var canvasWorkbench = WaitForCanvasWorkbench(page);
        await page.InvokeAsync(() => canvasWorkbench.Instance.NodeOpened.InvokeAsync(linkNode.Id));

        var initialState = GetWebPreviewDialogState(page.Instance);
        var initialRenderKey = GetCanvasOverlayRenderKey(page.Instance);
        var updatedState = initialState with { SourceLabel = "Node route" };
        Assert.Equal(initialState, updatedState with { SourceLabel = initialState.SourceLabel });

        await page.InvokeAsync(() =>
        {
            SetWebPreviewDialogState(page.Instance, updatedState);
            RequestRender(page.Instance);
        });

        Assert.NotEqual(initialRenderKey, GetCanvasOverlayRenderKey(page.Instance));
        page.WaitForAssertion(() =>
        {
            var dialog = page.Find("[data-testid='project-structure-web-preview-dialog']");
            Assert.Contains("Node route", dialog.TextContent, StringComparison.Ordinal);
            Assert.DoesNotContain("Web link", dialog.TextContent, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task Opening_a_runtime_node_that_is_not_running_offers_its_actions_instead_of_an_unreachable_preview()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var projectId = await CreateProjectAsync(projectsService);
        var projectFolder = $"apps/RuntimePreview{Guid.NewGuid():N}";
        var workspaceRoot = harness.Context.Services.GetRequiredService<IWorkspacePathResolver>().ResolveWorkspaceRoot();
        Directory.CreateDirectory(Path.Combine(workspaceRoot, projectFolder));
        await File.WriteAllTextAsync(
            Path.Combine(workspaceRoot, projectFolder, "RuntimePreview.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk.Web\"></Project>");
        var runtimeNode = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Environment,
                "Run app locally",
                "Debug .NET runtime",
                string.Empty,
                $"project:{projectId}",
                420,
                240,
                ObjectSubtype: "dotnet-runtime",
                MetadataJson: ProjectObjectMetadataSerializer.Serialize(new ProjectObjectMetadataEnvelope
                {
                    Environment = new ProjectEnvironmentMetadata
                    {
                        EnvironmentKind = ProjectEnvironmentKind.DotNetRuntime,
                        ProjectPath = $"{projectFolder}/RuntimePreview.csproj",
                        LocalhostUrl = "http://localhost:5149"
                    }
                })));

        var page = harness.Context.Render<ProjectStructurePage>(parameters => parameters
            .Add(component => component.ProjectId, projectId));
        var canvasWorkbench = WaitForCanvasWorkbench(page);

        await page.InvokeAsync(() => canvasWorkbench.Instance.NodeOpened.InvokeAsync(runtimeNode.Id));

        var quickActions = page.WaitForElement("[data-testid='project-structure-node-quick-actions']");
        Assert.Contains("Run app locally", quickActions.TextContent, StringComparison.Ordinal);
        Assert.Contains("Run", quickActions.TextContent, StringComparison.Ordinal);
        Assert.Contains("Open preview", quickActions.TextContent, StringComparison.Ordinal);
        Assert.Empty(page.FindAll("[data-testid='project-structure-web-preview-dialog']"));
    }

    private static async Task<Guid> CreateProjectAsync(ProjectsService projectsService)
    {
        var result = await projectsService.SaveAsync(new ProjectEditorModel
        {
            Name = $"Web preview render regression {Guid.NewGuid():N}",
            Description = "Page-level embedded browser regression coverage.",
            Objective = "Render a web preview on the first open event.",
            CurrentPhase = "Validation"
        });
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static IRenderedComponent<CanvasWorkbench> WaitForCanvasWorkbench(IRenderedComponent<IComponent> page)
    {
        IRenderedComponent<CanvasWorkbench>? canvasWorkbench = null;
        page.WaitForAssertion(() => canvasWorkbench = page.FindComponent<CanvasWorkbench>());
        return canvasWorkbench ?? throw new InvalidOperationException("Canvas workbench did not render.");
    }

    private static ProjectStructureWebPreviewDialogState GetWebPreviewDialogState(ProjectStructurePage page)
        => (ProjectStructureWebPreviewDialogState?)GetWebPreviewDialogField().GetValue(page)
           ?? throw new InvalidOperationException("The web preview dialog was not open.");

    private static void SetWebPreviewDialogState(
        ProjectStructurePage page,
        ProjectStructureWebPreviewDialogState state)
        => GetWebPreviewDialogField().SetValue(page, state);

    private static FieldInfo GetWebPreviewDialogField()
        => typeof(ProjectStructurePage).GetField("webPreviewDialog", BindingFlags.Instance | BindingFlags.NonPublic)
           ?? throw new InvalidOperationException("The web preview dialog field was not found.");

    private static string GetCanvasOverlayRenderKey(ProjectStructurePage page)
        => (string?)typeof(ProjectStructurePage)
               .GetProperty("CanvasOverlayRenderKey", BindingFlags.Instance | BindingFlags.NonPublic)
               ?.GetValue(page)
           ?? throw new InvalidOperationException("The canvas overlay render key was not found.");

    private static void RequestRender(ProjectStructurePage page)
        => typeof(ComponentBase)
               .GetMethod("StateHasChanged", BindingFlags.Instance | BindingFlags.NonPublic)!
               .Invoke(page, []);
}
