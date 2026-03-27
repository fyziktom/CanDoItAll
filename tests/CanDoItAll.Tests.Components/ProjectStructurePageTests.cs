using Bunit;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace CanDoItAll.Tests.Components;

public sealed class ProjectStructurePageTests
{
    [Fact]
    public async Task Renders_selection_and_health_as_floating_windows_without_stage_inspector_column()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();

        var project = await projectsService.GetAsync(null);
        project.Name = "Windowed Structure Project";
        project.Description = "Verify floating workbench windows.";
        project.Objective = "Keep inspector and health in the canvas.";
        project.CurrentPhase = "Validation";

        var saveResult = await projectsService.SaveAsync(project);
        Assert.True(saveResult.IsSuccess);
        var projectId = saveResult.Value;

        await workbenchService.SeedProjectObjectsAsync(
            projectId,
            [
                new ProjectObjectSeedRequest(
                    ProjectObjectType.Note,
                    "Floating window note",
                    "Window seed",
                    "Exercise the selection window.")
            ]);

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Inspector", cut.Markup);
            Assert.Contains("Health", cut.Markup);
            Assert.Contains("Blocks", cut.Markup);
            Assert.Contains("project-structure-selection-window", cut.Markup);
            Assert.Contains("project-structure-validation-window", cut.Markup);
            Assert.Contains("project-structure-toolbox-window", cut.Markup);
            Assert.Contains("project-structure-standard-blocks-toolbox", cut.Markup);
            Assert.DoesNotContain("Project structure toolbox", cut.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain("Search the shared block catalog", cut.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain("cw-inspector-column", cut.Markup, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task Renders_shared_structure_workbench_and_updates_inspector_from_outline_selection()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();

        var project = await projectsService.GetAsync(null);
        project.Name = "Structure Test Project";
        project.Description = "Project structure coverage";
        project.Objective = "Verify the shared structure canvas page";
        project.CurrentPhase = "Discovery";

        var saveResult = await projectsService.SaveAsync(project);
        Assert.True(saveResult.IsSuccess);
        var projectId = saveResult.Value;

        await workbenchService.SeedProjectObjectsAsync(
            projectId,
            [
                new ProjectObjectSeedRequest(
                    ProjectObjectType.Note,
                    "Architecture note",
                    "Tracks the first implementation idea",
                    "Shared canvas test note",
                    null,
                    null)
            ]);

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Structure canvas", cut.Markup);
            Assert.Contains("Project object index", cut.Markup);
            Assert.Contains("Graph health", cut.Markup);
            Assert.Contains("Architecture note", cut.Markup);
            Assert.Single(cut.FindAll("[data-testid='project-structure-action-catalog-adapter']"));
            Assert.Single(cut.FindAll("[data-testid='project-structure-placement-policy']"));
        });

        cut.FindAll("button")
            .First(button => button.TextContent.Contains("Architecture note", StringComparison.Ordinal))
            .Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Create next to source", cut.Markup);
            Assert.Contains("Open standard blocks", cut.Markup);
            Assert.Contains("Architecture note", cut.Markup);
            Assert.Contains("Tracks the first implementation idea", cut.Markup);
            Assert.Contains("project-structure-standard-blocks-toolbox", cut.Markup);
            Assert.Contains("project-structure-toolbox-group-capture", cut.Markup);
            Assert.Contains("project-structure-toolbox-group-work", cut.Markup);
            Assert.Contains("project-structure-toolbox-group-assets", cut.Markup);
            Assert.DoesNotContain("project-structure-toolbox-group-body-work", cut.Markup, StringComparison.Ordinal);
        });

        cut.Find("[data-testid='project-structure-toolbox-group-work']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("project-structure-toolbox-group-body-work", cut.Markup);
            Assert.Contains(">Task<", cut.Markup);
            Assert.Contains(">Issue<", cut.Markup);
            Assert.Contains("fa-list-check", cut.Markup);
            Assert.DoesNotContain("project-structure-toolbox-group-body-capture", cut.Markup, StringComparison.Ordinal);
        });

        cut.Find("input.project-structure-toolbox__search").Input("pdf");

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("project-structure-toolbox-group-body-assets", cut.Markup);
            Assert.Contains("project-structure-toolbox-add-file-pdf", cut.Markup);
            Assert.Contains("fa-file-pdf", cut.Markup);
            Assert.DoesNotContain("Unknown icon token", cut.Markup, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task Prompt_flow_nodes_expose_wizard_navigation_from_the_inspector()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();

        var project = await projectsService.GetAsync(null);
        project.Name = "Prompt Flow Structure";
        project.Description = "Prompt flow navigation coverage";
        project.Objective = "Open the prompt wizard from the structure page";
        project.CurrentPhase = "Discovery";

        var saveResult = await projectsService.SaveAsync(project);
        Assert.True(saveResult.IsSuccess);
        var projectId = saveResult.Value;

        var created = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.PromptFlow,
                "Feature wizard flow",
                "Feature discovery",
                "Start from the structure canvas.",
                $"project:{projectId}",
                420,
                260));

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));

        cut.WaitForAssertion(() => Assert.Contains("Feature wizard flow", cut.Markup));

        cut.FindAll("button")
            .First(button => button.TextContent.Contains("Feature wizard flow", StringComparison.Ordinal))
            .Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains(">Wizard<", cut.Markup);
        });

        cut.FindAll("button")
            .First(button => string.Equals(button.TextContent.Trim(), "Wizard", StringComparison.Ordinal))
            .Click();

        Assert.Contains("/prompt-factory?sessionId=", navigation.Uri, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(created.ArtifactId!.Value.ToString(), navigation.Uri, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Persisted_multi_select_state_renders_common_actions_in_selection_window()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();

        var project = await projectsService.GetAsync(null);
        project.Name = "Persisted Multi Select";
        project.Description = "Verify the shared multi-select action surface.";
        project.Objective = "Restore batch actions from saved workbench state.";
        project.CurrentPhase = "Validation";

        var saveResult = await projectsService.SaveAsync(project);
        Assert.True(saveResult.IsSuccess);
        var projectId = saveResult.Value;

        var feature = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                "Feature block",
                "Feature cluster",
                "Use for multi-select shared actions.",
                $"project:{projectId}",
                620,
                220,
                null,
                null,
                "feature"));

        var support = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                "Support block",
                "Support cluster",
                "Use for multi-select shared actions.",
                $"project:{projectId}",
                860,
                360,
                null,
                null,
                "support"));

        await workbenchService.SaveViewStateAsync(
            projectId,
            "structure",
            new CanvasWorkbenchUiState
            {
                SelectedNodeIds = [feature.Id, support.Id],
                WindowStates = new Dictionary<string, CanvasWorkbenchWindowState>(StringComparer.Ordinal)
                {
                    ["project-structure.selection"] = new CanvasWorkbenchWindowState { IsVisible = true },
                    ["project-structure.health"] = new CanvasWorkbenchWindowState { IsVisible = true }
                }
            }.ToJson());

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("2 nodes selected", cut.Markup);
            Assert.Contains("Grouping", cut.Markup);
            Assert.Contains(">P1<", cut.Markup);
            Assert.Contains(">50%<", cut.Markup);
            Assert.Contains(">Question<", cut.Markup);
            Assert.Contains(">Border<", cut.Markup);
        });
    }

    [Fact]
    public async Task Selected_nodes_with_children_open_summary_modal_and_show_export_actions()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();

        var project = await projectsService.GetAsync(null);
        project.Name = "Summary Modal Project";
        project.Description = "Verify the progress summary modal.";
        project.Objective = "Expose summary exports from the selection window.";
        project.CurrentPhase = "Execution";

        var saveResult = await projectsService.SaveAsync(project);
        Assert.True(saveResult.IsSuccess);
        var projectId = saveResult.Value;

        var feature = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                "Execution feature",
                "Delivery branch",
                "Use this node as the summary root.",
                $"project:{projectId}",
                520,
                240,
                null,
                null,
                "feature"));

        await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.WorkItem,
                "Ship checklist",
                "Ready for release",
                "Confirm the rollout tasks.",
                feature.Id,
                780,
                340,
                new DateTimeOffset(2026, 3, 28, 9, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 3, 29, 18, 0, 0, TimeSpan.Zero),
                "task"));

        await SaveSelectedNodeStateAsync(workbenchService, projectId, feature.Id);

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Execution feature", cut.Markup);
            Assert.Contains(">Summary<", cut.Markup);
        });

        cut.FindAll("button")
            .First(button => string.Equals(button.TextContent.Trim(), "Summary", StringComparison.Ordinal))
            .Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Export XLSX", cut.Markup);
            Assert.Contains("Export Gantt", cut.Markup);
            Assert.Contains("Ship checklist", cut.Markup);
        });
    }

    [Fact]
    public async Task Selected_mermaid_nodes_open_viewer_modal_with_detected_diagram_type()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();

        var project = await projectsService.GetAsync(null);
        project.Name = "Mermaid Viewer Project";
        project.Description = "Verify Mermaid viewing from project structure.";
        project.Objective = "Open Mermaid source in a typed viewer.";
        project.CurrentPhase = "Review";

        var saveResult = await projectsService.SaveAsync(project);
        Assert.True(saveResult.IsSuccess);
        var projectId = saveResult.Value;

        var mermaidMetadata = ProjectObjectMetadataSerializer.Serialize(new ProjectObjectMetadataEnvelope
        {
            File = new ProjectFileMetadata
            {
                FileSubtype = ProjectFileSubtype.Mermaid,
                MermaidDiagramKind = MermaidDiagramKind.Gantt
            }
        });

        var mermaidNode = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.File,
                "Release gantt",
                "Mermaid file",
                "gantt\n    title Release plan\n    section Build\n    Kickoff :done, task1, 2026-03-28, 1d",
                $"project:{projectId}",
                560,
                260,
                null,
                null,
                "mermaid",
                null,
                mermaidMetadata));

        await SaveSelectedNodeStateAsync(workbenchService, projectId, mermaidNode.Id);

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Mermaid viewer", cut.Markup);
            Assert.Contains("View Mermaid", cut.Markup);
        });

        cut.FindAll("button")
            .First(button => string.Equals(button.TextContent.Trim(), "View Mermaid", StringComparison.Ordinal))
            .Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Detected diagram type: Gantt", cut.Markup);
            Assert.Contains("Release plan", cut.Markup);
            Assert.Contains("Kickoff", cut.Markup);
        });
    }

    [Fact]
    public async Task Transcript_nodes_open_confirmation_dialog_with_provider_selection()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var workspaceService = harness.Context.Services.GetRequiredService<WorkspaceService>();

        var project = await projectsService.GetAsync(null);
        project.Name = "Transcript Workflow Project";
        project.Description = "Verify transcript confirmation and provider selection.";
        project.Objective = "Require confirmation before sending transcript actions.";
        project.CurrentPhase = "Review";

        var saveResult = await projectsService.SaveAsync(project);
        Assert.True(saveResult.IsSuccess);
        var projectId = saveResult.Value;

        var providerSave = await workspaceService.SaveProviderAsync(new ProviderProfileEditorModel
        {
            Name = "Local llama",
            ProviderKind = ProviderKind.OllamaLocal,
            BaseUrl = "http://localhost:11434",
            DefaultModel = "llama3.1",
            TimeoutSeconds = 30,
            IsEnabled = true
        });

        Assert.True(providerSave.IsSuccess);

        var transcriptMetadata = ProjectObjectMetadataSerializer.Serialize(new ProjectObjectMetadataEnvelope
        {
            Transcript = new ProjectTranscriptMetadata
            {
                TranscriptText = "Alice promised the rollout checklist and Bob owes the final screenshots.",
                LastProviderName = "Legacy reviewer"
            }
        });

        var transcriptNode = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Transcript,
                "Workshop transcript",
                "Client call",
                "Alice promised the rollout checklist and Bob owes the final screenshots.",
                $"project:{projectId}",
                540,
                280,
                null,
                null,
                null,
                null,
                transcriptMetadata));

        await SaveSelectedNodeStateAsync(workbenchService, projectId, transcriptNode.Id);

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains(">Summarize<", cut.Markup);
            Assert.Contains(">Find my tasks<", cut.Markup);
            Assert.Contains(">Find others delivery to me<", cut.Markup);
        });

        cut.FindAll("button")
            .First(button => string.Equals(button.TextContent.Trim(), "Find my tasks", StringComparison.Ordinal))
            .Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("This action will send transcript content to an external or local provider.", cut.Markup);
            Assert.Contains("Local llama", cut.Markup);
            Assert.Contains("Select a provider", cut.Markup);
            Assert.Contains("Last provider: Legacy reviewer", cut.Markup);
            Assert.Contains("Send request", cut.Markup);
        });
    }

    [Fact]
    public async Task Pdf_attachment_nodes_render_inline_preview_and_open_modal_without_navigation()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();

        var project = await projectsService.GetAsync(null);
        project.Name = "PDF Preview Project";
        project.Description = "Verify attachment previews in the inspector.";
        project.Objective = "Keep PDF viewing inside project structure.";
        project.CurrentPhase = "Review";

        var saveResult = await projectsService.SaveAsync(project);
        Assert.True(saveResult.IsSuccess);
        var projectId = saveResult.Value;

        await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.File,
                "Architecture spec",
                "Uploaded PDF",
                "Attachment preview coverage",
                $"project:{projectId}",
                540,
                260,
                null,
                null,
                string.Empty,
                new ProjectObjectMediaPayload(
                    "architecture-spec.pdf",
                    "application/pdf",
                    Convert.ToBase64String("%PDF-1.4 test payload"u8.ToArray()))));

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));

        cut.WaitForAssertion(() => Assert.Contains("Architecture spec", cut.Markup));

        cut.FindAll("button")
            .First(button => button.TextContent.Contains("Architecture spec", StringComparison.Ordinal))
            .Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Attachment preview", cut.Markup);
            Assert.Contains("application/pdf", cut.Markup);
            Assert.Contains("project-structure-document-preview", cut.Markup);
        });

        var uriBeforeOpen = navigation.Uri;

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Expand preview", cut.Markup);
            Assert.Contains("Open in new tab", cut.Markup);
        });

        cut.FindAll("button")
            .First(button => string.Equals(button.TextContent.Trim(), "Open", StringComparison.Ordinal))
            .Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("project-structure-preview-dialog", cut.Markup);
            Assert.Contains("dialog preview", cut.Markup);
            Assert.Single(cut.FindAll(".cw-stage-surface .project-structure-preview-backdrop--canvas"));
        });

        Assert.Equal(uriBeforeOpen, navigation.Uri);
    }

    [Fact]
    public async Task Audio_attachment_nodes_render_audio_preview_and_local_open_action_when_host_supports_it()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();

        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();

        var project = await projectsService.GetAsync(null);
        project.Name = "Audio Preview Project";
        project.Description = "Verify audio and local-open coverage.";
        project.Objective = "Keep audio attachment handling inside project structure.";
        project.CurrentPhase = "Review";

        var saveResult = await projectsService.SaveAsync(project);
        Assert.True(saveResult.IsSuccess);
        var projectId = saveResult.Value;

        var audioNode = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.File,
                "Interview clip",
                "Uploaded audio",
                "Audio preview coverage",
                $"project:{projectId}",
                540,
                260,
                null,
                null,
                string.Empty,
                new ProjectObjectMediaPayload(
                    "interview-clip.mp3",
                    "audio/mpeg",
                    Convert.ToBase64String(new byte[] { 0x49, 0x44, 0x33, 0x04, 0x00, 0x00, 0x00, 0x00, 0x00, 0x21 }))));

        await workbenchService.SaveViewStateAsync(
            projectId,
            "structure",
            new CanvasWorkbenchUiState
            {
                SelectedNodeIds = [audioNode.Id]
            }.ToJson());

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Interview clip", cut.Markup);
            Assert.Contains("project-structure-audio-preview", cut.Markup);
            Assert.Contains("audio/mpeg", cut.Markup);
            Assert.Contains("Open locally", cut.Markup);
            Assert.Contains("Expand preview", cut.Markup);
        });
    }

    [Fact]
    public async Task Selected_nodes_render_advanced_details_and_keep_delete_last_in_action_order()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();

        var project = await projectsService.GetAsync(null);
        project.Name = "Inspector Layout Project";
        project.Description = "Verify advanced details layout.";
        project.Objective = "Tighten the selection panel information architecture.";
        project.CurrentPhase = "Review";

        var saveResult = await projectsService.SaveAsync(project);
        Assert.True(saveResult.IsSuccess);
        var projectId = saveResult.Value;

        var block = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                "Main AMU server",
                "Server lane",
                "Operational server block.",
                $"project:{projectId}",
                620,
                280,
                null,
                null,
                "server"));

        await SaveSelectedNodeStateAsync(workbenchService, projectId, block.Id);

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Advanced details", cut.Markup);
            Assert.DoesNotContain("Typed details", cut.Markup, StringComparison.Ordinal);
        });

        var quickSignals = cut.Find("[data-testid='project-structure-quick-signals']");
        Assert.Contains("Progress", quickSignals.TextContent);
        Assert.Contains("Priority", quickSignals.TextContent);
        Assert.Contains("Marker", quickSignals.TextContent);

        var advancedDetails = cut.Find("[data-testid='project-structure-advanced-details']");
        Assert.False(advancedDetails.HasAttribute("open"));

        var actionLabels = cut.FindAll("[data-testid='project-structure-node-actions'] button")
            .Select(button => button.TextContent.Trim())
            .ToList();
        Assert.Contains("Edit", actionLabels);
        Assert.Equal("Delete", actionLabels.Last());
    }

    [Fact]
    public async Task Edit_actions_open_prefilled_canvas_composer_for_supported_nodes()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();

        var project = await projectsService.GetAsync(null);
        project.Name = "Edit Composer Project";
        project.Description = "Verify edit composer prefill.";
        project.Objective = "Open the shared composer with current node values.";
        project.CurrentPhase = "Execution";

        var saveResult = await projectsService.SaveAsync(project);
        Assert.True(saveResult.IsSuccess);
        var projectId = saveResult.Value;

        var runtimeNode = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Environment,
                "API runtime",
                "dotnet watch",
                "Launch the selected runtime from the inspector.",
                $"project:{projectId}",
                620,
                280,
                null,
                null,
                "dotnet-watch",
                null,
                ProjectObjectMetadataSerializer.Serialize(new ProjectObjectMetadataEnvelope
                {
                    Environment = new ProjectEnvironmentMetadata
                    {
                        EnvironmentKind = ProjectEnvironmentKind.DotNetWatch,
                        ProjectPath = @"C:\repos\api\Api.csproj",
                        LaunchProfileName = "https",
                        RuntimeProtocol = ProjectRuntimeProtocol.Https,
                        LocalhostUrl = "https://localhost:7143",
                        RepositoryResourceId = Guid.NewGuid()
                    }
                })));

        await SaveSelectedNodeStateAsync(workbenchService, projectId, runtimeNode.Id);

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Edit", cut.Markup);
        });

        cut.FindAll("[data-testid='project-structure-node-actions'] button")
            .First(button => string.Equals(button.TextContent.Trim(), "Edit", StringComparison.Ordinal))
            .Click();

        harness.Context.JSInterop.VerifyInvoke("CanDoItAll.canvasWorkbench.openCreateComposer");

        var invocation = harness.Context.JSInterop.Invocations
            .Last(candidate => string.Equals(candidate.Identifier, "CanDoItAll.canvasWorkbench.openCreateComposer", StringComparison.Ordinal));
        var action = Assert.IsType<CanvasWorkbenchAction>(invocation.Arguments[1]);
        var request = Assert.IsType<CanvasWorkbenchCreateActionRequest>(invocation.Arguments[2]);

        Assert.Equal("edit:add-environment-dotnet-watch", action.ActionId);
        Assert.Equal("Save changes", action.SubmitLabel);
        Assert.DoesNotContain(action.InputFields, field => string.Equals(field.Key, "repositoryRef", StringComparison.Ordinal));

        Assert.Equal("API runtime", request.Title);
        Assert.Equal("dotnet watch", request.Subtitle);
        Assert.Equal("Launch the selected runtime from the inspector.", request.Notes);
        Assert.Contains(request.InputValues!, value => value.Key == "environmentKind" && value.Value == "dotNetWatch");
        Assert.Contains(request.InputValues!, value => value.Key == "projectPath" && value.Value == @"C:\repos\api\Api.csproj");
        Assert.Contains(request.InputValues!, value => value.Key == "launchProfileName" && value.Value == "https");
        Assert.Contains(request.InputValues!, value => value.Key == "localhostUrl" && value.Value == "https://localhost:7143");
    }

    [Fact]
    public async Task Edit_create_actions_update_existing_nodes_and_refresh_selection_panel()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();

        var project = await projectsService.GetAsync(null);
        project.Name = "Edit Update Project";
        project.Description = "Verify edit submission updates existing nodes.";
        project.Objective = "Persist shared-composer edits against the selected node.";
        project.CurrentPhase = "Execution";

        var saveResult = await projectsService.SaveAsync(project);
        Assert.True(saveResult.IsSuccess);
        var projectId = saveResult.Value;

        var runtimeNode = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Environment,
                "API runtime",
                "dotnet watch",
                "Original runtime description.",
                $"project:{projectId}",
                620,
                280,
                null,
                null,
                "dotnet-watch",
                null,
                ProjectObjectMetadataSerializer.Serialize(new ProjectObjectMetadataEnvelope
                {
                    Environment = new ProjectEnvironmentMetadata
                    {
                        EnvironmentKind = ProjectEnvironmentKind.DotNetWatch,
                        ProjectPath = @"C:\repos\api\Api.csproj",
                        LaunchProfileName = "https",
                        RuntimeProtocol = ProjectRuntimeProtocol.Https,
                        LocalhostUrl = "https://localhost:7143"
                    }
                })));

        await SaveSelectedNodeStateAsync(workbenchService, projectId, runtimeNode.Id);

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));
        var canvasWorkbench = cut.FindComponent<CanvasWorkbench>();

        await cut.InvokeAsync(() => canvasWorkbench.Instance.OnCreateAction(JsonSerializer.Serialize(
            new CanvasWorkbenchCreateActionRequest(
                "edit:add-environment-dotnet-watch",
                runtimeNode.Id,
                runtimeNode.X,
                runtimeNode.Y,
                runtimeNode.ParentId,
                "API runtime updated",
                "Release host",
                "Edited runtime description.",
                "edit",
                "dialog",
                "dotnet-watch",
                null,
                [
                    new CanvasWorkbenchInputValue { Key = "environmentKind", Value = "dotNetWatch" },
                    new CanvasWorkbenchInputValue { Key = "projectPath", Value = @"C:\repos\api\Updated\Api.csproj" },
                    new CanvasWorkbenchInputValue { Key = "launchProfileName", Value = "staging" },
                    new CanvasWorkbenchInputValue { Key = "runtimeProtocol", Value = "http" },
                    new CanvasWorkbenchInputValue { Key = "localhostUrl", Value = "http://localhost:5099" }
                ]))));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("API runtime updated", cut.Markup);
            Assert.Contains("API runtime updated was updated.", cut.Markup);
        });

        var surface = await workbenchService.GetStructureAsync(projectId);
        var updatedNode = Assert.Single(surface.Nodes, node => node.Id == runtimeNode.Id);
        Assert.Equal("API runtime updated", updatedNode.Title);
        Assert.Equal("Release host", updatedNode.Subtitle);
        Assert.Equal("Edited runtime description.", updatedNode.Notes);

        var metadata = ProjectObjectMetadataSerializer.Parse(updatedNode.MetadataJson);
        Assert.NotNull(metadata.Environment);
        Assert.Equal(ProjectEnvironmentKind.DotNetWatch, metadata.Environment!.EnvironmentKind);
        Assert.Equal(@"C:\repos\api\Updated\Api.csproj", metadata.Environment.ProjectPath);
        Assert.Equal("staging", metadata.Environment.LaunchProfileName);
        Assert.Equal(ProjectRuntimeProtocol.Http, metadata.Environment.RuntimeProtocol);
        Assert.Equal("http://localhost:5099", metadata.Environment.LocalhostUrl);
    }

    [Fact]
    public async Task Launchable_runtime_nodes_render_powershell_actions_and_surface_launch_feedback()
    {
        var runtimeLauncher = new TestRuntimeLauncher();
        await using var harness = await ComponentTestHarness.CreateAsync(
            services => services.AddSingleton<IProjectStructureRuntimeLauncher>(runtimeLauncher));

        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();

        var project = await projectsService.GetAsync(null);
        project.Name = "Runtime Launch Project";
        project.Description = "Verify runtime launch actions.";
        project.Objective = "Launch runtime nodes from the selection panel.";
        project.CurrentPhase = "Execution";

        var saveResult = await projectsService.SaveAsync(project);
        Assert.True(saveResult.IsSuccess);
        var projectId = saveResult.Value;

        var runtimeNode = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Environment,
                "API runtime",
                "dotnet watch",
                "Launch the selected runtime from the inspector.",
                $"project:{projectId}",
                620,
                280,
                null,
                null,
                "dotnet-watch",
                null,
                ProjectObjectMetadataSerializer.Serialize(new ProjectObjectMetadataEnvelope
                {
                    Environment = new ProjectEnvironmentMetadata
                    {
                        EnvironmentKind = ProjectEnvironmentKind.DotNetWatch,
                        ProjectPath = @"C:\repos\api\Api.csproj",
                        LaunchProfileName = "https"
                    }
                })));

        await SaveSelectedNodeStateAsync(workbenchService, projectId, runtimeNode.Id);

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Open PowerShell", cut.Markup);
            Assert.Contains("Open PowerShell (Admin)", cut.Markup);
        });

        cut.FindAll("button")
            .First(button => string.Equals(button.TextContent.Trim(), "Open PowerShell (Admin)", StringComparison.Ordinal))
            .Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Opened elevated PowerShell and started dotnet watch.", cut.Markup);
        });

        Assert.Single(runtimeLauncher.Requests);
        Assert.Equal(runtimeNode.Id, runtimeLauncher.Requests[0].NodeId);
        Assert.True(runtimeLauncher.Requests[0].RunAsAdministrator);
    }

    [Fact]
    public async Task Non_launchable_nodes_do_not_render_runtime_launch_actions()
    {
        await using var harness = await ComponentTestHarness.CreateAsync(
            services => services.AddSingleton<IProjectStructureRuntimeLauncher>(new TestRuntimeLauncher()));

        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();

        var project = await projectsService.GetAsync(null);
        project.Name = "No Runtime Launch";
        project.Description = "Verify unsupported nodes stay clean.";
        project.Objective = "Do not show runtime launch buttons on non-runtime nodes.";
        project.CurrentPhase = "Review";

        var saveResult = await projectsService.SaveAsync(project);
        Assert.True(saveResult.IsSuccess);
        var projectId = saveResult.Value;

        var noteNode = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Note,
                "Design note",
                "Context",
                "Notes should not expose runtime launch actions.",
                $"project:{projectId}",
                500,
                240));

        await SaveSelectedNodeStateAsync(workbenchService, projectId, noteNode.Id);

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));

        cut.WaitForAssertion(() =>
        {
            Assert.DoesNotContain("Open PowerShell", cut.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain("Open PowerShell (Admin)", cut.Markup, StringComparison.Ordinal);
        });
    }

    private static Task SaveSelectedNodeStateAsync(ProjectWorkbenchService workbenchService, Guid projectId, params string[] selectedNodeIds)
        => workbenchService.SaveViewStateAsync(
            projectId,
            "structure",
            new CanvasWorkbenchUiState
            {
                SelectedNodeIds = selectedNodeIds.ToList(),
                WindowStates = new Dictionary<string, CanvasWorkbenchWindowState>(StringComparer.Ordinal)
                {
                    ["project-structure.selection"] = new CanvasWorkbenchWindowState { IsVisible = true }
                }
            }.ToJson());

    private sealed class TestRuntimeLauncher : IProjectStructureRuntimeLauncher
    {
        public bool IsAvailable => true;

        public List<(string NodeId, bool RunAsAdministrator)> Requests { get; } = [];

        public ProjectStructureRuntimeLaunchResolution Resolve(ProjectStructureNode? node)
            => node?.ObjectType is ProjectObjectType.Environment or ProjectObjectType.Script
                ? new(
                    new ProjectStructureRuntimeLaunchPlan(
                        @"C:\repos\api",
                        "Set-Location -LiteralPath 'C:\\repos\\api'",
                        "dotnet watch --project 'C:\\repos\\api\\Api.csproj' run --launch-profile 'https'",
                        "dotnet watch",
                        new ProjectStructureRuntimeLaunchTarget("project path", @"C:\repos\api\Api.csproj", false)),
                    "Launch plan resolved.")
                : new(null, "PowerShell launch is only available for runtime-capable nodes.");

        public Task<ProjectStructureRuntimeLaunchResult> LaunchAsync(ProjectStructureNode node, bool runAsAdministrator, CancellationToken cancellationToken = default)
        {
            Requests.Add((node.Id, runAsAdministrator));
            var message = runAsAdministrator
                ? "Opened elevated PowerShell and started dotnet watch."
                : "Opened PowerShell and started dotnet watch.";
            return Task.FromResult(new ProjectStructureRuntimeLaunchResult(true, message));
        }
    }
}


