using System.Text;
using System.Text.Json;
using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages;
using CanDoItAll.Processes.Application;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class ProjectStructurePageAssetAndActivationTests
{
    [Fact]
    public async Task Summary_gantt_export_persists_mermaid_source_as_file_content()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var agentService = harness.Context.Services.GetRequiredService<ProjectStructureAgentService>();
        Guid projectId = await CreateProjectAsync(projectsService, "Summary Gantt export");
        string rootNodeId = $"project:{projectId}";
        _ = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Note,
                "Delivery note",
                "Validation",
                "Included in the exported summary.",
                rootNodeId));
        var page = harness.Context.Render<ProjectStructurePage>(parameters => parameters
            .Add(component => component.ProjectId, projectId));
        IRenderedComponent<CanvasWorkbench> canvas = WaitForCanvasWorkbench(page);
        CanvasWorkbenchNode rootNode = Assert.Single(
            canvas.Instance.Surface.Nodes,
            node => string.Equals(node.Id, rootNodeId, StringComparison.Ordinal));

        await page.InvokeAsync(() => canvas.Instance.OnContextActionRequest(JsonSerializer.Serialize(
            new CanvasWorkbenchContextActionRequest(
                rootNode.Id,
                "summary",
                rootNode.X,
                rootNode.Y,
                "node"))));
        var exportButton = page.WaitForElement(".project-structure-summary-dialog")
            .QuerySelectorAll("button")
            .Single(button => button.TextContent.Contains("Export Gantt", StringComparison.Ordinal));
        await exportButton.ClickAsync(new MouseEventArgs());

        ProjectStructureNode persistedNode = Assert.Single(
            (await workbenchService.GetStructureAsync(projectId)).Nodes,
            node => string.Equals(node.Title, "Summary Gantt export gantt", StringComparison.Ordinal));
        Assert.Equal(ProjectObjectType.File, persistedNode.ObjectType);
        Assert.Equal("mermaid", persistedNode.ObjectSubtype);
        Assert.Equal("Generated from the structure progress summary modal.", persistedNode.Notes);
        Assert.Equal("summary-gantt-export-progress-summary.mmd", persistedNode.MediaOriginalFileName);
        Assert.Equal("text/vnd.mermaid", persistedNode.MediaContentType);
        ProjectStructureAssetContentDescriptor storedContent = await agentService.GetAssetContentAsync(
            projectId,
            persistedNode.Id);
        Assert.StartsWith(
            "gantt",
            Encoding.UTF8.GetString(Convert.FromBase64String(storedContent.Base64Data)),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Right_click_markdown_create_persists_pasted_content_before_the_dialog_closes()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var agentService = harness.Context.Services.GetRequiredService<ProjectStructureAgentService>();
        Guid projectId = await CreateProjectAsync(projectsService, "Markdown create dialog");
        string rootNodeId = $"project:{projectId}";
        var dialogHost = harness.Context.Render<DialogHost>();
        var page = harness.Context.Render<ProjectStructurePage>(parameters => parameters
            .Add(component => component.ProjectId, projectId));
        IRenderedComponent<CanvasWorkbench> canvas = WaitForCanvasWorkbench(page);
        CanvasWorkbenchNode rootNode = Assert.Single(
            canvas.Instance.Surface.Nodes,
            node => string.Equals(node.Id, rootNodeId, StringComparison.Ordinal));

        Task createTask = page.InvokeAsync(() => canvas.Instance.OnContextActionRequest(JsonSerializer.Serialize(
            new CanvasWorkbenchContextActionRequest(
                rootNode.Id,
                "add-file-markdown",
                rootNode.X,
                rootNode.Y,
                "node"))));

        dialogHost.WaitForElement("[data-testid='project-structure-text-asset-title']").Input("Architecture notes");
        dialogHost.Find("[data-testid='project-structure-text-asset-file-name']").Input("architecture-notes.markdown");
        dialogHost.Find("[data-testid='project-structure-text-asset-content']").Input("# Architecture\n\nTyped storage boundary.");
        dialogHost.Find("[data-testid='project-structure-text-asset-notes']").Input("Describes the architecture decision.");
        dialogHost.Find("[data-testid='project-structure-text-asset-submit']").Click();
        await createTask.WaitAsync(TimeSpan.FromSeconds(20));

        page.WaitForAssertion(() => Assert.Contains(
            canvas.Instance.Surface.Nodes,
            node => string.Equals(node.Title, "Architecture notes", StringComparison.Ordinal)));
        dialogHost.WaitForAssertion(() => Assert.Empty(
            dialogHost.FindAll("[data-testid='project-structure-text-asset-create-dialog']")));
        ProjectStructureNode persistedNode = Assert.Single(
            (await workbenchService.GetStructureAsync(projectId)).Nodes,
            node => string.Equals(node.Title, "Architecture notes", StringComparison.Ordinal));
        Assert.Equal(ProjectObjectType.File, persistedNode.ObjectType);
        Assert.Equal("markdown", persistedNode.ObjectSubtype);
        Assert.Equal("Describes the architecture decision.", persistedNode.Notes);
        Assert.Equal("architecture-notes.md", persistedNode.MediaOriginalFileName);
        Assert.Equal("text/markdown", persistedNode.MediaContentType);
        ProjectStructureAssetContentDescriptor storedContent = await agentService.GetAssetContentAsync(
            projectId,
            persistedNode.Id);
        Assert.Equal(
            "# Architecture\n\nTyped storage boundary.",
            Encoding.UTF8.GetString(Convert.FromBase64String(storedContent.Base64Data)));
    }

    [Fact]
    public async Task Direct_json_upload_uses_the_typed_text_asset_boundary_before_persistence()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        Guid projectId = await CreateProjectAsync(projectsService, "Direct JSON upload boundary");
        string rootNodeId = $"project:{projectId}";
        var page = harness.Context.Render<ProjectStructurePage>(parameters => parameters
            .Add(component => component.ProjectId, projectId));
        IRenderedComponent<CanvasWorkbench> canvas = WaitForCanvasWorkbench(page);
        var rootNode = Assert.Single(
            canvas.Instance.Surface.Nodes,
            node => string.Equals(node.Id, rootNodeId, StringComparison.Ordinal));
        byte[] content = Encoding.UTF8.GetBytes("{\"enabled\":true}");
        var upload = new CanvasWorkbenchUploadedFile
        {
            FileName = "SETTINGS.JSON",
            ContentType = "application/octet-stream",
            Base64Data = Convert.ToBase64String(content)
        };
        var request = new CanvasWorkbenchCreateActionRequest(
            "add-file-json",
            rootNode.Id,
            rootNode.X,
            rootNode.Y,
            rootNode.Id,
            "Settings",
            "config",
            "Runtime settings for the project.",
            "child",
            "dialog",
            string.Empty,
            upload);

        await page.InvokeAsync(() => canvas.Instance.OnCreateAction(JsonSerializer.Serialize(request)));

        page.WaitForAssertion(() => Assert.Contains(
            canvas.Instance.Surface.Nodes,
            node => string.Equals(node.Title, "Settings", StringComparison.Ordinal)));
        ProjectStructureNode persistedNode = Assert.Single(
            (await workbenchService.GetStructureAsync(projectId)).Nodes,
            node => string.Equals(node.Title, "Settings", StringComparison.Ordinal));
        Assert.Equal("SETTINGS.json", persistedNode.MediaOriginalFileName);
        Assert.Equal("application/json", persistedNode.MediaContentType);

        var invalidRequest = request with
        {
            Title = "Invalid settings",
            UploadedFile = new CanvasWorkbenchUploadedFile
            {
                FileName = "invalid.json",
                ContentType = "application/json",
                Base64Data = Convert.ToBase64String(Encoding.UTF8.GetBytes("{ invalid }"))
            }
        };

        await page.InvokeAsync(() => canvas.Instance.OnCreateAction(JsonSerializer.Serialize(invalidRequest)));

        Assert.DoesNotContain(
            canvas.Instance.Surface.Nodes,
            node => string.Equals(node.Title, "Invalid settings", StringComparison.Ordinal));
        Assert.DoesNotContain(
            (await workbenchService.GetStructureAsync(projectId)).Nodes,
            node => string.Equals(node.Title, "Invalid settings", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Stored_mermaid_source_defines_diagram_kind_instead_of_descriptive_notes()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var assetCreationService = harness.Context.Services.GetRequiredService<ProjectAssetCreationService>();
        Guid projectId = await CreateProjectAsync(projectsService, "Mermaid metadata source");
        ProjectObjectMediaPayload media = await assetCreationService.CreateTextAsync(
            ProjectFileSubtype.Mermaid,
            "interaction.mmd",
            "sequenceDiagram\n    Alice->>Bob: Hello");

        ProjectStructureNode node = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.File,
                "Interaction diagram",
                "Architecture",
                "gantt purpose notes must not define the source kind",
                $"project:{projectId}",
                420,
                260,
                ObjectSubtype: "mermaid",
                Media: media));

        ProjectObjectMetadataEnvelope metadata = ProjectObjectMetadataSerializer.Parse(node.MetadataJson);
        Assert.Equal(MermaidDiagramKind.SequenceDiagram, metadata.File?.MermaidDiagramKind);
        Assert.Contains(
            ProjectStructureNodeDescriptor.BuildFacts(node),
            fact => fact.Label == "Diagram" && fact.Value == nameof(MermaidDiagramKind.SequenceDiagram));
    }

    [Fact]
    public async Task Opening_a_projected_subproject_keeps_related_structure_precedence_over_file_browsing()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        Guid projectId = await CreateProjectAsync(projectsService, "Parent activation");
        Guid subprojectId = await CreateProjectAsync(projectsService, "Child activation");
        Assert.True((await projectsService.AddSubprojectAsync(projectId, subprojectId)).IsSuccess);
        string subprojectNodeId = $"project-child:{subprojectId}";
        var page = harness.Context.Render<ProjectStructurePage>(parameters => parameters
            .Add(component => component.ProjectId, projectId));
        IRenderedComponent<CanvasWorkbench> canvas = WaitForCanvasWorkbench(page);
        page.WaitForAssertion(() => Assert.Contains(
            canvas.Instance.Surface.Nodes,
            node => string.Equals(node.Id, subprojectNodeId, StringComparison.Ordinal)));

        await page.InvokeAsync(() => canvas.Instance.NodeOpened.InvokeAsync(subprojectNodeId));

        page.WaitForAssertion(() =>
        {
            var invocation = Assert.Single(
                harness.Context.JSInterop.Invocations,
                item => string.Equals(item.Identifier, "open", StringComparison.Ordinal));
            Assert.Contains(
                $"/projects/{subprojectId}/structure",
                Assert.IsType<string>(invocation.Arguments[0]),
                StringComparison.Ordinal);
            Assert.Empty(page.FindComponents<ProjectStructureFileBrowserWindow>());
        });
    }

    [Fact]
    public async Task Opening_a_projected_process_run_folder_shows_the_file_browser_window()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var launchService = harness.Context.Services.GetRequiredService<ProcessLaunchApplicationService>();
        Guid projectId = await CreateProjectAsync(projectsService, "Run folder activation");
        ProjectStructureNode deliveryNode = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                "Delivery target",
                "Run projection",
                "Hosts the projected process run.",
                $"project:{projectId}",
                420,
                260,
                ObjectSubtype: "delivery"));
        var launchResult = await launchService.LaunchAsync(
            new ProcessLaunchRequest(
                DefinitionKey: "software-delivery",
                ProcessDefinitionId: null,
                LiveRunProfileKey: null,
                projectId,
                ProjectNodeId: deliveryNode.Id,
                RequestedBy: "component-test",
                Variables: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["ProductRoot"] = harness.ActiveProfile.WorkspaceRootPath,
                    ["OutputRoot"] = harness.ActiveProfile.WorkspaceRootPath
                },
                RunReadiness: false,
                Execute: false));
        Assert.True(launchResult.RunId.HasValue);
        var runId = launchResult.RunId.Value;
        string managedArtifactRoot = ProcessLaunchApplicationService.BuildManagedProcessArtifactRoot(runId);
        string outputNodeId = ProjectStructureProcessNodeKeys.BuildProcessRunOutputNodeKey(
            runId.Value,
            managedArtifactRoot);
        var page = harness.Context.Render<ProjectStructurePage>(parameters => parameters
            .Add(component => component.ProjectId, projectId));
        IRenderedComponent<CanvasWorkbench> canvas = WaitForCanvasWorkbench(page);
        page.WaitForAssertion(() => Assert.Contains(
            canvas.Instance.Surface.Nodes,
            node => string.Equals(node.Id, outputNodeId, StringComparison.Ordinal)));

        await page.InvokeAsync(() => canvas.Instance.NodeOpened.InvokeAsync(outputNodeId));

        page.WaitForAssertion(() =>
        {
            IRenderedComponent<ProjectStructureFileBrowserWindow> fileBrowser = Assert.Single(
                page.FindComponents<ProjectStructureFileBrowserWindow>());
            var request = Assert.IsType<ProjectStructureNodeFileCollectionRequest>(fileBrowser.Instance.Request);
            Assert.Equal(outputNodeId, request.NodeId);
        });
    }

    private static async Task<Guid> CreateProjectAsync(ProjectsService projectsService, string name)
    {
        ProjectEditorModel project = await projectsService.GetAsync(null);
        project.Name = name;
        project.Description = $"{name} description";
        project.Objective = $"{name} objective";
        project.CurrentPhase = "Validation";

        var result = await projectsService.SaveAsync(project);
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static IRenderedComponent<CanvasWorkbench> WaitForCanvasWorkbench(
        IRenderedComponent<IComponent> page)
    {
        IRenderedComponent<CanvasWorkbench>? canvas = null;
        page.WaitForAssertion(() => canvas = page.FindComponent<CanvasWorkbench>());
        return canvas ?? throw new InvalidOperationException("Canvas workbench did not render.");
    }
}
