using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using AngleSharp.Dom;
using Bunit;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace CanDoItAll.Tests.Components;

public sealed class ProjectStructurePageSimpleMutationTests
{
    [Fact]
    public async Task Agent_completion_reload_preserves_immediate_canvas_selection_without_capturing_javascript_state()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var registry = harness.Context.Services.GetRequiredService<IAgentChatContextRegistry>();
        var notificationHub = harness.Context.Services.GetRequiredService<IAgentChatExecutionNotificationHub>();
        var projectId = await CreateProjectAsync(projectsService, "Agent completion selection refresh");
        var rootNodeId = $"project:{projectId}";
        var selectedNode = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Note,
                "Immediate selection",
                "Selection refresh",
                "Selection published before the debounced canvas state.",
                rootNodeId,
                420,
                240));
        await SaveSelectedNodeStateAsync(workbenchService, projectId, rootNodeId);

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));
        var canvasWorkbench = WaitForCanvasWorkbench(cut);

        await cut.InvokeAsync(() => canvasWorkbench.Instance.OnSelectionChanged(
            selectedNode.Id,
            JsonSerializer.Serialize(new[] { selectedNode.Id }),
            1));

        var refreshedNode = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Note,
                "Created by agent",
                "Completion refresh",
                "Authoritative data loaded after agent completion.",
                rootNodeId,
                660,
                240));
        var contextSnapshot = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
        var publishTask = notificationHub.PublishAsync(new AgentChatExecutionCompleted(
            contextSnapshot.Scope.Id,
            contextSnapshot.Scope.Source,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTimeOffset.UtcNow));

        await publishTask.WaitAsync(TimeSpan.FromSeconds(10));

        cut.WaitForAssertion(() =>
        {
            var reloadedWorkbench = cut.FindComponent<CanvasWorkbench>();
            Assert.Contains(reloadedWorkbench.Instance.Surface.Nodes, node => node.Id == refreshedNode.Id);
            Assert.Equal(new[] { selectedNode.Id }, reloadedWorkbench.Instance.Surface.UiState.SelectedNodeIds);
        });
        Assert.DoesNotContain(
            harness.Context.JSInterop.Invocations,
            invocation => string.Equals(
                invocation.Identifier,
                "CanDoItAll.canvasWorkbench.getState",
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task Inline_note_edit_uses_first_non_empty_line_as_title_and_patches_surface_without_structure_reload()
    {
        await using var harness = await ComponentTestHarness.CreateAsync(WrapDbContextFactoryWithCreateCounter);
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var createCounter = harness.Context.Services.GetRequiredService<DbContextCreateCounter>();

        var projectId = await CreateProjectAsync(projectsService, "Inline note patch");
        var noteNode = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Note,
                "Original note",
                "Inline edit",
                "Original note body",
                $"project:{projectId}",
                420,
                240));

        await SaveSelectedNodeStateAsync(workbenchService, projectId, noteNode.Id);

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));
        var canvasWorkbench = WaitForCanvasWorkbench(cut);

        cut.WaitForAssertion(() =>
        {
            var renderedNode = Assert.Single(canvasWorkbench.Instance.Surface.Nodes, node => string.Equals(node.Id, noteNode.Id, StringComparison.Ordinal));
            Assert.Equal("Original note body", renderedNode.InlineText);
        });
        createCounter.Reset();

        const string updatedNoteBody = "Updated heading\r\nSecond line of note";
        await cut.InvokeAsync(() => canvasWorkbench.Instance.OnNodeEdited(JsonSerializer.Serialize(
            new CanvasWorkbenchNodeEditRequest(noteNode.Id, noteNode.Title, updatedNoteBody))));

        cut.WaitForAssertion(() =>
        {
            var updatedNode = Assert.Single(canvasWorkbench.Instance.Surface.Nodes, node => string.Equals(node.Id, noteNode.Id, StringComparison.Ordinal));
            Assert.Equal("Updated heading", updatedNode.Title);
            Assert.Equal(updatedNoteBody, updatedNode.InlineText);
        });

        Assert.Equal(1, createCounter.CreateCount);

        var persistedSurface = await workbenchService.GetStructureAsync(projectId);
        var persistedNode = Assert.Single(persistedSurface.Nodes, node => string.Equals(node.Id, noteNode.Id, StringComparison.Ordinal));
        Assert.Equal("Updated heading", persistedNode.Title);
        Assert.Equal(updatedNoteBody, persistedNode.Notes);
    }

    [Fact]
    public async Task Quick_note_create_uses_derived_title_and_persists_full_long_body()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();

        var projectId = await CreateProjectAsync(projectsService, "Long quick note body");
        await SaveSelectedNodeStateAsync(workbenchService, projectId, $"project:{projectId}");

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));
        var canvasWorkbench = WaitForCanvasWorkbench(cut);

        string projectRootId = string.Empty;
        cut.WaitForAssertion(() =>
        {
            projectRootId = Assert.Single(
                canvasWorkbench.Instance.Surface.Nodes,
                node => node.Id.StartsWith("project:", StringComparison.Ordinal)).Id;
        });

        const string longNoteBody =
            "Long simple note first line that deliberately exceeds the short title limit and should be derived.\r\n" +
            "Second line keeps important context that must stay in the note body.\r\n" +
            "Final line includes symbols #release @owner and punctuation.";
        const string expectedTitle = "Long simple note first line that deliberately exceeds the sho...";

        await cut.InvokeAsync(() => canvasWorkbench.Instance.OnCreateAction(JsonSerializer.Serialize(
            new CanvasWorkbenchCreateActionRequest(
                "add-note",
                projectRootId,
                420,
                260,
                projectRootId,
                longNoteBody,
                string.Empty,
                longNoteBody,
                "child",
                "quick-note",
                string.Empty,
                null))));

        cut.WaitForAssertion(() =>
        {
            var createdNode = Assert.Single(
                canvasWorkbench.Instance.Surface.Nodes,
                node => string.Equals(node.Title, expectedTitle, StringComparison.Ordinal));

            Assert.Equal(longNoteBody, createdNode.InlineText);
        });

        var persistedSurface = await workbenchService.GetStructureAsync(projectId);
        var persistedNode = Assert.Single(
            persistedSurface.Nodes,
            node => string.Equals(node.Title, expectedTitle, StringComparison.Ordinal));
        Assert.Equal(longNoteBody, persistedNode.Notes);
    }

    [Fact]
    public async Task Generated_image_asset_create_uses_selected_provider_and_persists_image_asset()
    {
        var imageService = new RecordingAgentImageGenerationService(waitForRelease: true);
        await using var harness = await ComponentTestHarness.CreateAsync(services =>
        {
            services.RemoveAll<IAgentImageGenerationService>();
            services.AddSingleton<IAgentImageGenerationService>(imageService);
        });
        await using var deferredWorker = await StartedDeferredCompletionWorker.StartAsync(harness);
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var agentWorkspaceService = harness.Context.Services.GetRequiredService<IAgentFrameworkWorkspaceService>();

        var providerId = await SaveImageProviderAsync(agentWorkspaceService);

        var projectId = await CreateProjectAsync(projectsService, "Generated image asset");
        var parentNodeId = $"project:{projectId}";
        await SaveSelectedNodeStateAsync(workbenchService, projectId, parentNodeId);

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));
        var canvasWorkbench = WaitForCanvasWorkbench(cut);

        cut.WaitForAssertion(() =>
        {
            Assert.Contains(
                canvasWorkbench.Instance.Surface.Chrome.QuickCreateActions,
                action => action.ActionId == "group-assets" &&
                          action.Children.Any(child => child.ActionId == "generate-image-asset"));

            var rootNode = Assert.Single(
                canvasWorkbench.Instance.Surface.Nodes,
                node => string.Equals(node.Id, parentNodeId, StringComparison.Ordinal));
            var contextGenerateAction = FindCreateAction(rootNode.ContextActions, "generate-image-asset");
            var contextProviderField = Assert.Single(
                contextGenerateAction.InputFields,
                field => field.Key == "imageProviderProfileId");
            Assert.Contains(
                contextProviderField.Options,
                option => option.Value == providerId.ToString("D"));
        });

        const string prompt = "Create a crisp dashboard thumbnail with teal, white, and charcoal UI panels.";
        var createdNodeId = await InvokeCreateActionAsync(
            cut,
            canvasWorkbench,
            "generate-image-asset",
            parentNodeId,
            parentNodeId,
            "Generated dashboard concept",
            "Dashboard hero",
            prompt,
            [
                new CanvasWorkbenchInputValue { Key = "imageProviderProfileId", Value = providerId.ToString("D") },
                new CanvasWorkbenchInputValue { Key = "imageModel", Value = "gpt-image-1-mini" },
                new CanvasWorkbenchInputValue { Key = "imageSize", Value = "1536x1024" },
                new CanvasWorkbenchInputValue { Key = "imageQuality", Value = "medium" },
                new CanvasWorkbenchInputValue { Key = "imageOutputFormat", Value = "png" }
            ]);

        var waitingSurface = await workbenchService.GetStructureAsync(projectId);
        var waitingNode = Assert.Single(waitingSurface.Nodes, node => string.Equals(node.Id, createdNodeId, StringComparison.Ordinal));
        Assert.Equal(ProjectObjectType.ImageAsset, waitingNode.ObjectType);
        Assert.Equal("generated", waitingNode.ObjectSubtype);
        Assert.Equal("image/svg+xml", waitingNode.MediaContentType);
        Assert.Equal("waiting-for-image-creation-by-ai.svg", waitingNode.MediaOriginalFileName);
        Assert.Equal(prompt, waitingNode.Notes);

        var request = await imageService.WaitForFirstRequestAsync();
        Assert.Equal(providerId, request.Provider.Id);
        Assert.Equal("gpt-image-1-mini", request.Model);
        Assert.Equal(prompt, request.Prompt);
        Assert.Equal("1536x1024", request.Size);
        Assert.Equal("medium", request.Quality);
        Assert.Equal(AgentGeneratedImageFormat.Png, request.Format);

        imageService.CompleteGeneration();
        cut.WaitForAssertion(() =>
        {
            var updatedNode = Assert.Single(
                canvasWorkbench.Instance.Surface.Nodes,
                node => string.Equals(node.Id, createdNodeId, StringComparison.Ordinal));
            Assert.Equal("image/png", updatedNode.MediaContentType);
            Assert.Equal("generated-dashboard-concept.png", updatedNode.MediaFileName);
            Assert.Equal("Generated image ready", updatedNode.Status);
        });

        var persistedSurface = await workbenchService.GetStructureAsync(projectId);
        var persistedNode = Assert.Single(persistedSurface.Nodes, node => string.Equals(node.Id, createdNodeId, StringComparison.Ordinal));
        Assert.Equal(ProjectObjectType.ImageAsset, persistedNode.ObjectType);
        Assert.Equal("generated", persistedNode.ObjectSubtype);
        Assert.Equal("image/png", persistedNode.MediaContentType);
        Assert.Equal("generated-dashboard-concept.png", persistedNode.MediaOriginalFileName);
        Assert.Equal(prompt, persistedNode.Notes);
        var metadata = ProjectObjectMetadataSerializer.Parse(persistedNode.MetadataJson);
        Assert.Equal(ProjectStructureDeferredNodeCompletionState.Completed, metadata.DeferredCompletion?.State);
    }

    [Fact]
    public async Task Generated_image_asset_create_lists_legacy_openai_image_provider_without_purpose_metadata()
    {
        var imageService = new RecordingAgentImageGenerationService();
        await using var harness = await ComponentTestHarness.CreateAsync(services =>
        {
            services.RemoveAll<IAgentImageGenerationService>();
            services.AddSingleton<IAgentImageGenerationService>(imageService);
        });
        await using var deferredWorker = await StartedDeferredCompletionWorker.StartAsync(harness);
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var dbContextFactory = harness.Context.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var providerId = Guid.NewGuid();

        await using (var dbContext = await dbContextFactory.CreateDbContextAsync())
        {
            dbContext.Set<CanDoItAll.Modules.Workspace.ProviderProfile>().Add(new CanDoItAll.Modules.Workspace.ProviderProfile
            {
                Id = providerId,
                Name = "OpenAI image generation",
                ProviderKind = CanDoItAll.Modules.Workspace.ProviderKind.OpenAi,
                ConnectorPluginKey = CanDoItAll.Modules.Workspace.OpenAiProviderAdapter.PluginKey,
                ConfigSchemaVersion = "1.0",
                BaseUrl = "https://api.openai.com/v1",
                DefaultModel = "gpt-image-1-mini",
                TimeoutSeconds = 45,
                IsEnabled = true,
                SupportsStreaming = false,
                SupportsToolCalling = false,
                SupportsStructuredOutput = false,
                SupportsVision = false,
                ExtraSettingsJson = JsonSerializer.Serialize(new
                {
                    connectorPluginKey = CanDoItAll.Modules.Workspace.OpenAiProviderAdapter.PluginKey,
                    configSchemaVersion = "1.0",
                    timeoutSeconds = 45,
                    providerTransport = ProviderTransportKind.Responses.ToString(),
                    tags = new[] { "openai", "image-generation" }
                })
            });
            await dbContext.SaveChangesAsync();
        }

        var projectId = await CreateProjectAsync(projectsService, "Legacy generated image provider");
        var parentNodeId = $"project:{projectId}";
        await SaveSelectedNodeStateAsync(workbenchService, projectId, parentNodeId);

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));
        var canvasWorkbench = WaitForCanvasWorkbench(cut);

        cut.WaitForAssertion(() =>
        {
            var generateAction = FindCreateAction(
                canvasWorkbench.Instance.Surface.Chrome.QuickCreateActions,
                "generate-image-asset");
            var providerField = Assert.Single(
                generateAction.InputFields,
                field => field.Key == "imageProviderProfileId");
            var providerOption = Assert.Single(
                providerField.Options,
                option => option.Value == providerId.ToString("D"));

            Assert.Equal("OpenAI image generation (gpt-image-1-mini)", providerOption.Label);
        });

        const string prompt = "Create a crisp settings panel thumbnail with teal controls.";
        await InvokeCreateActionAsync(
            cut,
            canvasWorkbench,
            "generate-image-asset",
            parentNodeId,
            parentNodeId,
            "Legacy provider generated dashboard",
            "Dashboard hero",
            prompt,
            [
                new CanvasWorkbenchInputValue { Key = "imageProviderProfileId", Value = providerId.ToString("D") },
                new CanvasWorkbenchInputValue { Key = "imageSize", Value = "1024x1024" },
                new CanvasWorkbenchInputValue { Key = "imageQuality", Value = "low" },
                new CanvasWorkbenchInputValue { Key = "imageOutputFormat", Value = "png" }
            ]);

        var request = await imageService.WaitForFirstRequestAsync();
        Assert.Equal(providerId, request.Provider.Id);
        Assert.Equal(ProviderProfilePurpose.ImageGeneration, request.Provider.Purpose);
        Assert.Equal("gpt-image-1-mini", request.Model);
    }

    [Fact]
    public async Task Generated_image_asset_failure_marks_existing_waiting_node_without_recreating_it()
    {
        var imageService = new RecordingAgentImageGenerationService(
            failure: new InvalidOperationException("provider unavailable"));
        await using var harness = await ComponentTestHarness.CreateAsync(services =>
        {
            services.RemoveAll<IAgentImageGenerationService>();
            services.AddSingleton<IAgentImageGenerationService>(imageService);
        });
        await using var deferredWorker = await StartedDeferredCompletionWorker.StartAsync(harness);
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var agentWorkspaceService = harness.Context.Services.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var providerId = await SaveImageProviderAsync(agentWorkspaceService, "Failing image provider");

        var projectId = await CreateProjectAsync(projectsService, "Generated image failure");
        var parentNodeId = $"project:{projectId}";
        await SaveSelectedNodeStateAsync(workbenchService, projectId, parentNodeId);

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));
        var canvasWorkbench = WaitForCanvasWorkbench(cut);

        const string prompt = "Create a product calculator image with visible calculator controls.";
        var createdNodeId = await InvokeCreateActionAsync(
            cut,
            canvasWorkbench,
            "generate-image-asset",
            parentNodeId,
            parentNodeId,
            "Failing generated image",
            "Calculator",
            prompt,
            [
                new CanvasWorkbenchInputValue { Key = "imageProviderProfileId", Value = providerId.ToString("D") },
                new CanvasWorkbenchInputValue { Key = "imageSize", Value = "1024x1024" },
                new CanvasWorkbenchInputValue { Key = "imageQuality", Value = "low" },
                new CanvasWorkbenchInputValue { Key = "imageOutputFormat", Value = "png" }
            ]);

        var request = await imageService.WaitForFirstRequestAsync();
        Assert.Equal(prompt, request.Prompt);

        cut.WaitForAssertion(() =>
        {
            var updatedNode = Assert.Single(
                canvasWorkbench.Instance.Surface.Nodes,
                node => string.Equals(node.Id, createdNodeId, StringComparison.Ordinal));
            Assert.Equal("Image generation failed", updatedNode.Status);
            Assert.Equal("waiting-for-image-creation-by-ai.svg", updatedNode.MediaFileName);
        });

        var persistedSurface = await workbenchService.GetStructureAsync(projectId);
        var persistedNode = Assert.Single(persistedSurface.Nodes, node => string.Equals(node.Id, createdNodeId, StringComparison.Ordinal));
        Assert.Equal("Image generation failed", persistedNode.Status);
        Assert.Equal("waiting-for-image-creation-by-ai.svg", persistedNode.MediaOriginalFileName);
        var metadata = ProjectObjectMetadataSerializer.Parse(persistedNode.MetadataJson);
        Assert.Equal(ProjectStructureDeferredNodeCompletionState.Failed, metadata.DeferredCompletion?.State);
        Assert.Contains("provider unavailable", metadata.DeferredCompletion?.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Quick_sibling_note_insertion_persists_downward_stack_shift()
    {
        await using var harness = await ComponentTestHarness.CreateAsync(WrapDbContextFactoryWithCreateCounter);
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var createCounter = harness.Context.Services.GetRequiredService<DbContextCreateCounter>();

        var projectId = await CreateProjectAsync(projectsService, "Quick note stack shift");
        var parentNodeId = $"project:{projectId}";
        var sourceNode = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Note,
                "Source note",
                string.Empty,
                "Source note",
                parentNodeId,
                420,
                240));
        var lowerNode = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Note,
                "Lower note",
                string.Empty,
                "Lower note",
                parentNodeId,
                420,
                344));

        await SaveSelectedNodeStateAsync(workbenchService, projectId, sourceNode.Id);

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));
        var canvasWorkbench = WaitForCanvasWorkbench(cut);
        createCounter.Reset();

        const string insertedNote = "Inserted quick note\r\nwith enough text\r\nto require vertical room";
        await cut.InvokeAsync(() => canvasWorkbench.Instance.OnCreateAction(JsonSerializer.Serialize(
            new CanvasWorkbenchCreateActionRequest(
                "add-note",
                sourceNode.Id,
                sourceNode.X,
                sourceNode.Y,
                sourceNode.ParentId,
                "Inserted quick note",
                string.Empty,
                insertedNote,
                "sibling",
                "quick-note",
                string.Empty,
                null))));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains(
                canvasWorkbench.Instance.Surface.Nodes,
                node => string.Equals(node.Title, "Inserted quick note", StringComparison.Ordinal));
        });
        Assert.Equal(2, createCounter.CreateCount);

        var persistedSurface = await workbenchService.GetStructureAsync(projectId);
        var persistedLowerNode = Assert.Single(persistedSurface.Nodes, node => string.Equals(node.Id, lowerNode.Id, StringComparison.Ordinal));
        Assert.True(persistedLowerNode.Y > lowerNode.Y);
    }

    [Fact]
    public async Task Change_block_type_patches_surface_without_structure_reload()
    {
        await using var harness = await ComponentTestHarness.CreateAsync(WrapDbContextFactoryWithCreateCounter);
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var createCounter = harness.Context.Services.GetRequiredService<DbContextCreateCounter>();

        var projectId = await CreateProjectAsync(projectsService, "Block type patch");
        var blockNode = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                "Edge gateway",
                "Lab rack",
                "Original common block.",
                $"project:{projectId}",
                560,
                300,
                null,
                null,
                "computer"));

        await SaveSelectedNodeStateAsync(workbenchService, projectId, blockNode.Id);

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));
        var canvasWorkbench = WaitForCanvasWorkbench(cut);

        cut.WaitForAssertion(() => Assert.Contains("Change block", cut.Markup));
        createCounter.Reset();

        FindButtonByLabel(cut, "Change block", "[data-testid='project-structure-node-actions'] button").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("project-structure-block-mutation-dialog", cut.Markup);
        });

        cut.Find("[data-testid='project-structure-block-mutation-select']").Change("add-block-router");
        cut.Find("[data-testid='project-structure-block-mutation-submit']").Click();

        cut.WaitForAssertion(() =>
        {
            var updatedNode = Assert.Single(canvasWorkbench.Instance.Surface.Nodes, node => string.Equals(node.Id, blockNode.Id, StringComparison.Ordinal));
            Assert.Equal("Edge gateway", updatedNode.Title);
            Assert.Equal("Router block", updatedNode.Kind);
            Assert.Equal(ProjectObjectPaletteKeys.Info, updatedNode.PaletteKey);
            Assert.DoesNotContain("project-structure-block-mutation-dialog", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("was changed to router block.", cut.Markup);
        });

        Assert.Equal(1, createCounter.CreateCount);

        var persistedSurface = await workbenchService.GetStructureAsync(projectId);
        var persistedNode = Assert.Single(persistedSurface.Nodes, node => string.Equals(node.Id, blockNode.Id, StringComparison.Ordinal));
        Assert.Equal(ProjectObjectType.ProjectBlock, persistedNode.ObjectType);
        Assert.Equal("router", persistedNode.ObjectSubtype);
        Assert.Equal("Edge gateway", persistedNode.Title);
    }

    [Fact]
    public async Task Edit_dialog_updates_runtime_node_without_structure_reload()
    {
        await using var harness = await ComponentTestHarness.CreateAsync(WrapDbContextFactoryWithCreateCounter);
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var createCounter = harness.Context.Services.GetRequiredService<DbContextCreateCounter>();

        var projectId = await CreateProjectAsync(projectsService, "Runtime edit patch");
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
        var canvasWorkbench = WaitForCanvasWorkbench(cut);

        cut.WaitForAssertion(() => Assert.Contains("API runtime", cut.Markup));
        createCounter.Reset();

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
            var updatedNode = Assert.Single(canvasWorkbench.Instance.Surface.Nodes, node => string.Equals(node.Id, runtimeNode.Id, StringComparison.Ordinal));
            Assert.Equal("API runtime updated", updatedNode.Title);
            Assert.Equal("Release host", updatedNode.Subtitle);
            Assert.Contains("API runtime updated was updated.", cut.Markup);
        });

        Assert.Equal(1, createCounter.CreateCount);
    }

    [Fact]
    public async Task Artifact_create_sequence_persists_mermaid_file_after_prior_artifact_actions()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();

        var projectId = await CreateProjectAsync(projectsService, "Artifact create sequence");
        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));
        var canvasWorkbench = WaitForCanvasWorkbench(cut);

        string projectRootId = string.Empty;
        cut.WaitForAssertion(() =>
        {
            projectRootId = Assert.Single(
                canvasWorkbench.Instance.Surface.Nodes,
                node => !string.IsNullOrWhiteSpace(node.Id) &&
                    node.Id.StartsWith("project:", StringComparison.Ordinal)).Id;
        });

        var recordingId = await InvokeCreateActionAsync(
            cut,
            canvasWorkbench,
            "add-recording",
            projectRootId,
            projectRootId,
            "Kickoff recording",
            "Discovery sync",
            "Recording captured for transcript and LLM validation.",
            [
                new CanvasWorkbenchInputValue { Key = "recordingSource", Value = "Teams recording" },
                new CanvasWorkbenchInputValue { Key = "storageReference", Value = "workspace://meetings/kickoff.mp4" },
                new CanvasWorkbenchInputValue { Key = "durationMinutes", Value = "52" }
            ]);

        _ = await InvokeCreateActionAsync(
            cut,
            canvasWorkbench,
            "add-transcript",
            recordingId,
            recordingId,
            "Kickoff transcript",
            "Discovery sync transcript",
            string.Empty,
            [
                new CanvasWorkbenchInputValue { Key = "recordingRef", Value = recordingId },
                new CanvasWorkbenchInputValue
                {
                    Key = "transcriptText",
                    Value = "Alice: We need the toolbox redesign validated in the browser.\n" +
                        "Bob: Export progress workbook and Gantt evidence from the same summary source.\n" +
                        "Chris: Keep provider confirmation explicit before any transcript action."
                }
            ]);

        var featureId = await InvokeCreateActionAsync(
            cut,
            canvasWorkbench,
            "add-block-feature",
            projectRootId,
            projectRootId,
            "Canvas editor rollout",
            "Validation track",
            "Use this branch for reconnect, summary, export, and delete confirmation evidence.");

        _ = await InvokeCreateActionAsync(
            cut,
            canvasWorkbench,
            "add-work-task",
            featureId,
            featureId,
            "Capture screenshot evidence",
            "QA stream",
            "Capture the required evidence for the bundle.",
            [
                new CanvasWorkbenchInputValue { Key = "dueUtc", Value = "2026-04-10T15:00:00+00:00" }
            ]);

        _ = await InvokeCreateActionAsync(
            cut,
            canvasWorkbench,
            "add-work-task",
            featureId,
            featureId,
            "Export workbook and Gantt",
            "Reporting",
            "Use the progress summary modal exports for proof.",
            [
                new CanvasWorkbenchInputValue { Key = "dueUtc", Value = "2026-04-11T16:30:00+00:00" }
            ]);

        _ = await InvokeCreateActionAsync(
            cut,
            canvasWorkbench,
            "add-work-task",
            projectRootId,
            projectRootId,
            "Reconnect detached follow-up",
            "Backlog",
            "This task will be reparented into the feature branch.",
            [
                new CanvasWorkbenchInputValue { Key = "dueUtc", Value = "2026-04-12T12:00:00+00:00" }
            ]);

        _ = await InvokeCreateActionAsync(
            cut,
            canvasWorkbench,
            "add-file-pdf",
            projectRootId,
            projectRootId,
            "Architecture evidence PDF",
            "docs/architecture",
            "Typed PDF validation node.",
            uploadedFile: BuildUploadedFile(
                "architecture-evidence.pdf",
                "application/pdf",
                "%PDF-1.4\n1 0 obj\n<< /Type /Catalog >>\nendobj\ntrailer\n<< /Root 1 0 R >>\n%%EOF"));

        _ = await InvokeCreateActionAsync(
            cut,
            canvasWorkbench,
            "add-file-excel",
            projectRootId,
            projectRootId,
            "Validation workbook",
            "reports",
            "Typed spreadsheet validation node.",
            uploadedFile: BuildUploadedFile(
                "validation-workbook.csv",
                "text/csv",
                "name,status\nexports,ready\nsummary,ready"));

        _ = await InvokeCreateActionAsync(
            cut,
            canvasWorkbench,
            "add-file-docx",
            projectRootId,
            projectRootId,
            "Project brief docx",
            "docs/briefs",
            "Typed docx validation node.",
            uploadedFile: BuildUploadedFile(
                "project-brief.docx",
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                "Fake docx payload for UI evidence only."));

        _ = await InvokeCreateActionAsync(
            cut,
            canvasWorkbench,
            "add-file-text",
            projectRootId,
            projectRootId,
            "Runbook text",
            "docs/runbooks",
            "Operator checklist and rollout notes.");

        _ = await InvokeCreateActionAsync(
            cut,
            canvasWorkbench,
            "add-file-json",
            projectRootId,
            projectRootId,
            "Settings JSON",
            "config",
            "{\n  \"toolbox\": true,\n  \"validation\": \"strict\"\n}");

        _ = await InvokeCreateActionAsync(
            cut,
            canvasWorkbench,
            "add-file-markdown",
            projectRootId,
            projectRootId,
            "Evidence README",
            "docs",
            "# Validation evidence\n\nCapture screenshots and exports.");

        const string mermaidTitle = "Validation flow diagram";
        const string mermaidNotes = "gantt\n" +
            "    title Bundle validation timeline\n" +
            "    dateFormat YYYY-MM-DD\n" +
            "    section Evidence\n" +
            "    Capture screenshots :done, a1, 2026-04-08, 2d\n" +
            "    Export workbook :active, a2, 2026-04-10, 2d";
        var mermaidId = await InvokeCreateActionAsync(
            cut,
            canvasWorkbench,
            "add-file-mermaid",
            projectRootId,
            projectRootId,
            mermaidTitle,
            "docs/diagrams",
            string.Empty,
            [
                new CanvasWorkbenchInputValue { Key = "mermaidText", Value = mermaidNotes }
            ]);

        cut.WaitForAssertion(() =>
        {
            var runtimeNode = Assert.Single(
                canvasWorkbench.Instance.Surface.Nodes,
                node => string.Equals(node.Id, mermaidId, StringComparison.Ordinal));
            Assert.Equal(mermaidTitle, runtimeNode.Title);
            Assert.Equal("Mermaid", runtimeNode.Kind);
            Assert.Equal(mermaidNotes, runtimeNode.InlineText);
        });

        var persistedSurface = await workbenchService.GetStructureAsync(projectId);
        var persistedMermaidNode = Assert.Single(
            persistedSurface.Nodes,
            node => string.Equals(node.Id, mermaidId, StringComparison.Ordinal));
        Assert.Equal(ProjectObjectType.File, persistedMermaidNode.ObjectType);
        Assert.Equal("mermaid", persistedMermaidNode.ObjectSubtype);
        Assert.Equal(mermaidTitle, persistedMermaidNode.Title);
        Assert.Equal(mermaidNotes, persistedMermaidNode.Notes);
    }

    [Fact]
    public async Task Toolbar_tools_switch_surface_modes_and_preserve_frozen_dependency_source()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();

        var projectId = await CreateProjectAsync(projectsService, "Toolbar mode project");
        var noteNode = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Note,
                "Selected note",
                "Toolbar source",
                "Use this note as the dependency source.",
                $"project:{projectId}",
                420,
                240));

        await SaveSelectedNodeStateAsync(workbenchService, projectId, noteNode.Id);

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));
        var canvasWorkbench = WaitForCanvasWorkbench(cut);

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Selected note", cut.Markup);
            Assert.NotNull(cut.Find("[data-testid='project-structure-toolbar-tool-select']"));
            Assert.NotNull(cut.Find("[data-testid='project-structure-toolbar-tool-dependency']"));
            Assert.NotNull(cut.Find("[data-testid='project-structure-toolbar-tool-delete']"));
            Assert.Equal("authoring", canvasWorkbench.Instance.Surface.Mode);
            Assert.True(string.IsNullOrWhiteSpace(canvasWorkbench.Instance.Surface.DependencySourceId));
        });

        cut.Find("[data-testid='project-structure-toolbar-tool-dependency']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("dependency", canvasWorkbench.Instance.Surface.Mode);
            Assert.Equal(noteNode.Id, canvasWorkbench.Instance.Surface.DependencySourceId);
            Assert.Contains("Dependency tool:", cut.Markup);
        });

        cut.Find("[data-testid='project-structure-toolbar-tool-delete']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("delete", canvasWorkbench.Instance.Surface.Mode);
            Assert.True(string.IsNullOrWhiteSpace(canvasWorkbench.Instance.Surface.DependencySourceId));
            Assert.Contains("Delete tool:", cut.Markup);
        });

        cut.Find("[data-testid='project-structure-toolbar-tool-select']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("authoring", canvasWorkbench.Instance.Surface.Mode);
            Assert.True(string.IsNullOrWhiteSpace(canvasWorkbench.Instance.Surface.DependencySourceId));
        });
    }

    [Fact]
    public async Task Canvas_tool_mode_callback_returns_dependency_and_delete_modes_to_select()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();

        var projectId = await CreateProjectAsync(projectsService, "Escape tool reset project");
        var noteNode = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Note,
                "Reset source",
                "Escape test",
                "Selected note for Escape tool reset coverage.",
                $"project:{projectId}",
                420,
                240));

        await SaveSelectedNodeStateAsync(workbenchService, projectId, noteNode.Id);

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));
        var canvasWorkbench = WaitForCanvasWorkbench(cut);

        cut.WaitForAssertion(() => Assert.Contains("Reset source", cut.Markup));

        cut.Find("[data-testid='project-structure-toolbar-tool-dependency']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("dependency", canvasWorkbench.Instance.Surface.Mode);
            Assert.Equal(noteNode.Id, canvasWorkbench.Instance.Surface.DependencySourceId);
        });

        await cut.InvokeAsync(() => canvasWorkbench.Instance.OnContextActionRequest(JsonSerializer.Serialize(
            new CanvasWorkbenchContextActionRequest(null, "tool-mode:select", 0, 0, "canvas"))));

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("authoring", canvasWorkbench.Instance.Surface.Mode);
            Assert.True(string.IsNullOrWhiteSpace(canvasWorkbench.Instance.Surface.DependencySourceId));
            Assert.Contains("Click to select", cut.Markup);
        });

        cut.Find("[data-testid='project-structure-toolbar-tool-delete']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("delete", canvasWorkbench.Instance.Surface.Mode);
            Assert.Contains("Delete tool:", cut.Markup);
        });

        await cut.InvokeAsync(() => canvasWorkbench.Instance.OnContextActionRequest(JsonSerializer.Serialize(
            new CanvasWorkbenchContextActionRequest(null, "tool-mode:select", 0, 0, "canvas"))));

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("authoring", canvasWorkbench.Instance.Surface.Mode);
            Assert.Contains("Click to select", cut.Markup);
        });
    }

    [Fact]
    public async Task Dependency_context_requests_create_and_delete_persisted_links_for_note_nodes()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();

        var projectId = await CreateProjectAsync(projectsService, "Dependency mutation project");
        var dependentNote = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Note,
                "Dependent note",
                "Simple note",
                "This note should wait on another node.",
                $"project:{projectId}",
                420,
                240));
        var prerequisiteTask = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.WorkItem,
                "Prerequisite task",
                "Foundation",
                "Finish this before the note can proceed.",
                $"project:{projectId}",
                690,
                260,
                null,
                null,
                "task"));

        await SaveSelectedNodeStateAsync(workbenchService, projectId, dependentNote.Id);

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));
        var canvasWorkbench = WaitForCanvasWorkbench(cut);

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Dependent note", cut.Markup);
            Assert.Contains("Prerequisite task", cut.Markup);
        });

        await cut.InvokeAsync(() => canvasWorkbench.Instance.OnContextActionRequest(JsonSerializer.Serialize(
            new CanvasWorkbenchContextActionRequest(
                prerequisiteTask.Id,
                "dependency:create",
                0,
                0,
                "node",
                dependentNote.Id,
                prerequisiteTask.Id,
                ProjectObjectLinkKind.DependsOn.ToString()))));

        cut.WaitForAssertion(() => Assert.Contains("The dependency link was added.", cut.Markup));

        var surfaceWithDependency = await workbenchService.GetStructureAsync(projectId);
        Assert.Contains(surfaceWithDependency.Links, link =>
            string.Equals(link.SourceId, dependentNote.Id, StringComparison.Ordinal) &&
            string.Equals(link.TargetId, prerequisiteTask.Id, StringComparison.Ordinal) &&
            link.Kind == ProjectObjectLinkKind.DependsOn);

        await cut.InvokeAsync(() => canvasWorkbench.Instance.OnContextActionRequest(JsonSerializer.Serialize(
            new CanvasWorkbenchContextActionRequest(
                prerequisiteTask.Id,
                "delete-link",
                0,
                0,
                "link",
                dependentNote.Id,
                prerequisiteTask.Id,
                ProjectObjectLinkKind.DependsOn.ToString()))));

        cut.WaitForAssertion(() => Assert.Contains("The dependency link was deleted.", cut.Markup));

        var surfaceWithoutDependency = await workbenchService.GetStructureAsync(projectId);
        Assert.DoesNotContain(surfaceWithoutDependency.Links, link =>
            string.Equals(link.SourceId, dependentNote.Id, StringComparison.Ordinal) &&
            string.Equals(link.TargetId, prerequisiteTask.Id, StringComparison.Ordinal) &&
            link.Kind == ProjectObjectLinkKind.DependsOn);
    }

    [Fact]
    public async Task Delete_prompt_mentions_connected_nodes_when_multiple_dependency_links_touch_the_target()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();

        var projectId = await CreateProjectAsync(projectsService, "Delete prompt project");
        var centralNote = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Note,
                "Central note",
                "Dependency hub",
                "This node is connected to multiple prerequisites.",
                $"project:{projectId}",
                460,
                240));
        var prerequisiteOne = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Note,
                "Prerequisite one",
                "Connected node",
                "First visible prerequisite.",
                $"project:{projectId}",
                720,
                180));
        var prerequisiteTwo = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Note,
                "Prerequisite two",
                "Connected node",
                "Second visible prerequisite.",
                $"project:{projectId}",
                720,
                320));

        await workbenchService.LinkObjectsAsync(projectId, centralNote.Id, prerequisiteOne.Id, ProjectObjectLinkKind.DependsOn);
        await workbenchService.LinkObjectsAsync(projectId, centralNote.Id, prerequisiteTwo.Id, ProjectObjectLinkKind.DependsOn);
        await SaveSelectedNodeStateAsync(workbenchService, projectId, centralNote.Id);

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));
        var canvasWorkbench = WaitForCanvasWorkbench(cut);

        cut.WaitForAssertion(() => Assert.Contains("Central note", cut.Markup));

        await cut.InvokeAsync(() => canvasWorkbench.Instance.OnContextAction(centralNote.Id, "delete", 0, 0));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Delete node", cut.Markup);
        });

        var persistedSurface = await workbenchService.GetStructureAsync(projectId);
        Assert.Contains(persistedSurface.Nodes, node => string.Equals(node.Id, centralNote.Id, StringComparison.Ordinal));
        Assert.Equal(2, persistedSurface.Links.Count(link =>
            string.Equals(link.SourceId, centralNote.Id, StringComparison.Ordinal) &&
            link.Kind == ProjectObjectLinkKind.DependsOn));
    }

    [Fact]
    public async Task Canvas_context_delete_targets_selected_nodes_when_clicked_node_is_selected()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();

        var projectId = await CreateProjectAsync(projectsService, "Canvas multi-delete project");
        var firstNode = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Note,
                "Delete target one",
                string.Empty,
                "First selected node.",
                $"project:{projectId}",
                420,
                240));
        var secondNode = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Note,
                "Delete target two",
                string.Empty,
                "Second selected node.",
                $"project:{projectId}",
                700,
                240));

        await SaveSelectedNodeStateAsync(workbenchService, projectId, firstNode.Id, secondNode.Id);

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));
        var canvasWorkbench = WaitForCanvasWorkbench(cut);

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("2 nodes selected", cut.Markup, StringComparison.Ordinal);
            var firstCanvasNode = Assert.Single(
                canvasWorkbench.Instance.Surface.Nodes,
                node => string.Equals(node.Id, firstNode.Id, StringComparison.Ordinal));
            Assert.Contains(firstCanvasNode.ContextActions, action =>
                action.ActionId == "delete" &&
                action.Label == "Delete selected");
        });

        await cut.InvokeAsync(() => canvasWorkbench.Instance.OnContextAction(firstNode.Id, "delete", 0, 0));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Delete selection", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("This will delete 2 selected nodes.", cut.Markup, StringComparison.Ordinal);
        });

        FindButtonByLabel(cut, "Delete selected").Click();

        cut.WaitForAssertion(() => Assert.Contains("2 selected branches were deleted.", cut.Markup, StringComparison.Ordinal));
        var persistedSurface = await workbenchService.GetStructureAsync(projectId);
        Assert.DoesNotContain(persistedSurface.Nodes, node => string.Equals(node.Id, firstNode.Id, StringComparison.Ordinal));
        Assert.DoesNotContain(persistedSurface.Nodes, node => string.Equals(node.Id, secondNode.Id, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Canvas_group_context_delete_targets_selected_nodes()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();

        var projectId = await CreateProjectAsync(projectsService, "Canvas group multi-delete project");
        var firstNode = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Note,
                "Group delete target one",
                string.Empty,
                "First selected node.",
                $"project:{projectId}",
                420,
                240));
        var secondNode = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Note,
                "Group delete target two",
                string.Empty,
                "Second selected node.",
                $"project:{projectId}",
                700,
                240));

        await SaveSelectedNodeStateAsync(workbenchService, projectId, firstNode.Id, secondNode.Id);

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));
        var canvasWorkbench = WaitForCanvasWorkbench(cut);

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("2 nodes selected", cut.Markup, StringComparison.Ordinal);
            Assert.Contains(canvasWorkbench.Instance.Surface.Chrome.GroupContextActions, action =>
                action.ActionId == "delete" &&
                action.Label == "Delete selected");
        });

        await cut.InvokeAsync(() => canvasWorkbench.Instance.OnContextActionRequest(JsonSerializer.Serialize(
            new CanvasWorkbenchContextActionRequest(null, "delete", 0, 0, "canvas"))));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Delete selection", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("This will delete 2 selected nodes.", cut.Markup, StringComparison.Ordinal);
        });

        FindButtonByLabel(cut, "Delete selected").Click();

        cut.WaitForAssertion(() => Assert.Contains("2 selected branches were deleted.", cut.Markup, StringComparison.Ordinal));
        var persistedSurface = await workbenchService.GetStructureAsync(projectId);
        Assert.DoesNotContain(persistedSurface.Nodes, node => string.Equals(node.Id, firstNode.Id, StringComparison.Ordinal));
        Assert.DoesNotContain(persistedSurface.Nodes, node => string.Equals(node.Id, secondNode.Id, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Status_marker_priority_and_progress_mutations_patch_surface_without_structure_reload()
    {
        await using var harness = await ComponentTestHarness.CreateAsync(WrapDbContextFactoryWithCreateCounter);
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var createCounter = harness.Context.Services.GetRequiredService<DbContextCreateCounter>();

        var projectId = await CreateProjectAsync(projectsService, "Selection mutation patch");
        var firstWorkItemNode = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.WorkItem,
                "Selection target one",
                "Mutation coverage",
                "Apply selection-window and context mutations without a full reload.",
                $"project:{projectId}",
                540,
                320,
                null,
                null,
                "task"));
        var secondWorkItemNode = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.WorkItem,
                "Selection target two",
                "Mutation coverage",
                "Apply selection-window and context mutations without a full reload.",
                $"project:{projectId}",
                700,
                360,
                null,
                null,
                "task"));

        await SaveSelectedNodeStateAsync(workbenchService, projectId, firstWorkItemNode.Id, secondWorkItemNode.Id);

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));
        var canvasWorkbench = WaitForCanvasWorkbench(cut);

        cut.WaitForAssertion(() => Assert.Contains("2 nodes selected", cut.Markup));

        createCounter.Reset();
        cut.FindAll("button")
            .First(button => string.Equals(button.TextContent.Trim(), "In progress", StringComparison.Ordinal))
            .Click();
        cut.WaitForAssertion(() =>
        {
            var updatedNodes = canvasWorkbench.Instance.Surface.Nodes
                .Where(node => node.Id is not null &&
                    (string.Equals(node.Id, firstWorkItemNode.Id, StringComparison.Ordinal) ||
                     string.Equals(node.Id, secondWorkItemNode.Id, StringComparison.Ordinal)))
                .ToList();
            Assert.Equal(2, updatedNodes.Count);
            Assert.All(updatedNodes, node =>
            {
                Assert.Equal("In progress", node.Status);
                Assert.Equal(62, node.ProgressPercent);
            });
        });
        Assert.InRange(createCounter.CreateCount, 1, 2);

        createCounter.Reset();
        cut.FindAll("button")
            .First(button => string.Equals(button.TextContent.Trim(), "Risk", StringComparison.Ordinal))
            .Click();
        cut.WaitForAssertion(() =>
        {
            var updatedNodes = canvasWorkbench.Instance.Surface.Nodes
                .Where(node => node.Id is not null &&
                    (string.Equals(node.Id, firstWorkItemNode.Id, StringComparison.Ordinal) ||
                     string.Equals(node.Id, secondWorkItemNode.Id, StringComparison.Ordinal)))
                .ToList();
            Assert.Equal(2, updatedNodes.Count);
            Assert.All(updatedNodes, node =>
            {
                Assert.Equal("risk", node.MarkerIcon);
                Assert.Equal("Risk", node.MarkerLabel);
            });
        });
        Assert.InRange(createCounter.CreateCount, 1, 2);

        createCounter.Reset();
        cut.FindAll("button")
            .First(button => string.Equals(button.TextContent.Trim(), "P2", StringComparison.Ordinal))
            .Click();
        cut.WaitForAssertion(() =>
        {
            var updatedNodes = canvasWorkbench.Instance.Surface.Nodes
                .Where(node => node.Id is not null &&
                    (string.Equals(node.Id, firstWorkItemNode.Id, StringComparison.Ordinal) ||
                     string.Equals(node.Id, secondWorkItemNode.Id, StringComparison.Ordinal)))
                .ToList();
            Assert.Equal(2, updatedNodes.Count);
            Assert.All(updatedNodes, node => Assert.Equal(2, node.Priority));
        });
        Assert.InRange(createCounter.CreateCount, 1, 2);

        createCounter.Reset();
        cut.FindAll("button")
            .First(button => string.Equals(button.TextContent.Trim(), "100%", StringComparison.Ordinal))
            .Click();
        cut.WaitForAssertion(() =>
        {
            var updatedNodes = canvasWorkbench.Instance.Surface.Nodes
                .Where(node => node.Id is not null &&
                    (string.Equals(node.Id, firstWorkItemNode.Id, StringComparison.Ordinal) ||
                     string.Equals(node.Id, secondWorkItemNode.Id, StringComparison.Ordinal)))
                .ToList();
            Assert.Equal(2, updatedNodes.Count);
            Assert.All(updatedNodes, node =>
            {
                Assert.Equal("progress", node.ProgressMode);
                Assert.Equal(100, node.ProgressPercent);
            });
        });
        Assert.InRange(createCounter.CreateCount, 1, 2);
    }

    [Fact]
    public async Task Signals_window_can_stack_multiple_markers_on_the_selected_node()
    {
        await using var harness = await ComponentTestHarness.CreateAsync(WrapDbContextFactoryWithCreateCounter);
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var createCounter = harness.Context.Services.GetRequiredService<DbContextCreateCounter>();

        var projectId = await CreateProjectAsync(projectsService, "Signals marker stack");
        var node = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.WorkItem,
                "Signals target",
                "Marker stacking",
                "Verify that the floating signals window can add more than one marker.",
                $"project:{projectId}",
                540,
                320,
                null,
                null,
                "task"));

        await SaveSelectedNodeStateAsync(workbenchService, projectId, node.Id);

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));
        var canvasWorkbench = WaitForCanvasWorkbench(cut);

        cut.WaitForAssertion(() => Assert.Contains("Signals", cut.Markup));
        cut.Find("[data-testid='project-structure-signals-toggle']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("project-structure-signals-window", cut.Markup);
            Assert.Contains("project-structure-signals-action-marker-question", cut.Markup);
            Assert.Contains("project-structure-signals-action-marker-risk", cut.Markup);
        });

        createCounter.Reset();
        cut.Find("[data-testid='project-structure-signals-action-marker-question']").Click();

        cut.WaitForAssertion(() =>
        {
            var updatedNode = Assert.Single(canvasWorkbench.Instance.Surface.Nodes, item => string.Equals(item.Id, node.Id, StringComparison.Ordinal));
            Assert.Equal("question", updatedNode.MarkerIcon);
            Assert.Single(updatedNode.Markers);
            Assert.Equal("question", updatedNode.Markers[0].Icon);
        });
        Assert.InRange(createCounter.CreateCount, 1, 2);

        createCounter.Reset();
        cut.Find("[data-testid='project-structure-signals-action-marker-risk']").Click();

        cut.WaitForAssertion(() =>
        {
            var updatedNode = Assert.Single(canvasWorkbench.Instance.Surface.Nodes, item => string.Equals(item.Id, node.Id, StringComparison.Ordinal));
            Assert.Equal("risk", updatedNode.MarkerIcon);
            Assert.Equal(2, updatedNode.Markers.Count);
            Assert.Contains(updatedNode.Markers, marker => string.Equals(marker.Icon, "question", StringComparison.Ordinal));
            Assert.Contains(updatedNode.Markers, marker => string.Equals(marker.Icon, "risk", StringComparison.Ordinal));
        });
        Assert.InRange(createCounter.CreateCount, 1, 2);

        var persistedSurface = await workbenchService.GetStructureAsync(projectId);
        var persistedNode = Assert.Single(persistedSurface.Nodes, item => string.Equals(item.Id, node.Id, StringComparison.Ordinal));
        Assert.Equal("risk", persistedNode.MarkerIcon);
        Assert.Equal(2, persistedNode.Markers.Count);
        Assert.Contains(persistedNode.Markers, marker => string.Equals(marker.Icon, "question", StringComparison.Ordinal));
        Assert.Contains(persistedNode.Markers, marker => string.Equals(marker.Icon, "risk", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Clipboard_cut_and_paste_moves_selected_subtree_without_structure_reload()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();

        var projectId = await CreateProjectAsync(projectsService, "Clipboard subtree patch");
        var sourceNode = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                "Network orchestration",
                "Delivery flow",
                "Parent subtree node for cut and paste.",
                $"project:{projectId}",
                620,
                80,
                null,
                null,
                "task-flow"));
        var taskNode = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.WorkItem,
                "Inventory network dependencies",
                "Networking",
                "Task child that should move with the subtree.",
                sourceNode.Id,
                900,
                280,
                null,
                null,
                "task"));
        var evidenceNode = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.TestEvidence,
                "Store rack photo",
                "Validation",
                "Grandchild evidence node that should follow the subtree.",
                taskNode.Id,
                1180,
                420));

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));
        var canvasWorkbench = WaitForCanvasWorkbench(cut);

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Network orchestration", cut.Markup);
            Assert.Equal(4, canvasWorkbench.Instance.Surface.Nodes.Count);
        });

        var cutPayload = JsonSerializer.Serialize(new
        {
            operation = "cut",
            surfaceId = canvasWorkbench.Instance.Surface.SurfaceId,
            selectedNodeIds = new[] { sourceNode.Id }
        });
        var pasteEnvelope = JsonSerializer.Serialize(new
        {
            payloadJson = cutPayload,
            anchorWorld = new
            {
                x = 2000,
                y = 1200
            },
            surfaceId = canvasWorkbench.Instance.Surface.SurfaceId
        });

        await cut.InvokeAsync(() => canvasWorkbench.Instance.OnClipboardAction("cut", cutPayload));
        await cut.InvokeAsync(() => canvasWorkbench.Instance.OnClipboardAction("paste", pasteEnvelope));

        var persistedSurface = await workbenchService.GetStructureAsync(projectId);
        var persistedSource = Assert.Single(persistedSurface.Nodes, node => string.Equals(node.Id, sourceNode.Id, StringComparison.Ordinal));
        var persistedTask = Assert.Single(persistedSurface.Nodes, node => string.Equals(node.Id, taskNode.Id, StringComparison.Ordinal));
        var persistedEvidence = Assert.Single(persistedSurface.Nodes, node => string.Equals(node.Id, evidenceNode.Id, StringComparison.Ordinal));

        var persistedDeltaX = persistedSource.X - sourceNode.X;
        var persistedDeltaY = persistedSource.Y - sourceNode.Y;
        Assert.True(
            Math.Abs(persistedDeltaX) > 40 || Math.Abs(persistedDeltaY) > 40,
            $"Expected persisted cut/paste movement, but source remained at ({persistedSource.X}, {persistedSource.Y}). Markup: {cut.Markup}");
        Assert.InRange(Math.Abs((persistedTask.X - taskNode.X) - persistedDeltaX), 0, 6);
        Assert.InRange(Math.Abs((persistedTask.Y - taskNode.Y) - persistedDeltaY), 0, 6);
        Assert.InRange(Math.Abs((persistedEvidence.X - evidenceNode.X) - persistedDeltaX), 0, 6);
        Assert.InRange(Math.Abs((persistedEvidence.Y - evidenceNode.Y) - persistedDeltaY), 0, 6);

        cut.WaitForAssertion(() =>
        {
            var updatedSource = Assert.Single(canvasWorkbench.Instance.Surface.Nodes, node => string.Equals(node.Id, sourceNode.Id, StringComparison.Ordinal));
            var updatedTask = Assert.Single(canvasWorkbench.Instance.Surface.Nodes, node => string.Equals(node.Id, taskNode.Id, StringComparison.Ordinal));
            var updatedEvidence = Assert.Single(canvasWorkbench.Instance.Surface.Nodes, node => string.Equals(node.Id, evidenceNode.Id, StringComparison.Ordinal));

            var deltaX = updatedSource.X - sourceNode.X;
            var deltaY = updatedSource.Y - sourceNode.Y;
            Assert.True(Math.Abs(deltaX) > 40 || Math.Abs(deltaY) > 40);
            Assert.InRange(Math.Abs((updatedTask.X - taskNode.X) - deltaX), 0, 6);
            Assert.InRange(Math.Abs((updatedTask.Y - taskNode.Y) - deltaY), 0, 6);
            Assert.InRange(Math.Abs((updatedEvidence.X - evidenceNode.X) - deltaX), 0, 6);
            Assert.InRange(Math.Abs((updatedEvidence.Y - evidenceNode.Y) - deltaY), 0, 6);
            Assert.Contains("cut selection was pasted", cut.Markup, StringComparison.OrdinalIgnoreCase);
        });
    }

    private static async Task<Guid> CreateProjectAsync(ProjectsService projectsService, string name)
    {
        var project = await projectsService.GetAsync(null);
        project.Name = name;
        project.Description = $"{name} description";
        project.Objective = $"{name} objective";
        project.CurrentPhase = "Execution";

        var saveResult = await projectsService.SaveAsync(project);
        Assert.True(saveResult.IsSuccess);
        return saveResult.Value;
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

    private static Task<Guid> SaveImageProviderAsync(
        IAgentFrameworkWorkspaceService agentWorkspaceService,
        string name = "Component image provider")
        => agentWorkspaceService.SaveProviderAsync(new ProviderProfileEditorModel
        {
            Name = name,
            Kind = ProviderKind.OpenAi,
            BaseUrl = "https://api.openai.com/v1",
            ApiKeyEnvironmentVariable = "OPENAI_API_KEY",
            DefaultModel = "gpt-image-1-mini",
            Transport = ProviderTransportKind.Responses,
            Purpose = ProviderProfilePurpose.ImageGeneration,
            IsEnabled = true,
            SupportsStreaming = false,
            SupportsTools = false,
            SuggestedModels = ["gpt-image-1-mini"]
        });

    private static async Task<string> InvokeCreateActionAsync(
        IRenderedComponent<ProjectStructurePage> cut,
        IRenderedComponent<CanvasWorkbench> canvasWorkbench,
        string actionId,
        string sourceNodeId,
        string parentNodeId,
        string title,
        string subtitle,
        string notes,
        IReadOnlyList<CanvasWorkbenchInputValue>? inputValues = null,
        CanvasWorkbenchUploadedFile? uploadedFile = null)
    {
        var sourceNode = Assert.Single(
            canvasWorkbench.Instance.Surface.Nodes,
            node => string.Equals(node.Id, sourceNodeId, StringComparison.Ordinal));
        var request = new CanvasWorkbenchCreateActionRequest(
            actionId,
            sourceNodeId,
            sourceNode.X,
            sourceNode.Y,
            parentNodeId,
            title,
            subtitle,
            notes,
            "child",
            uploadedFile is not null || inputValues?.Count > 0 ? "dialog" : "create",
            string.Empty,
            uploadedFile,
            inputValues);

        await cut.InvokeAsync(() => canvasWorkbench.Instance.OnCreateAction(JsonSerializer.Serialize(request)));

        string createdNodeId = string.Empty;
        cut.WaitForAssertion(() =>
        {
            var matchingNodes = canvasWorkbench.Instance.Surface.Nodes
                .Where(node => string.Equals(node.Title, title, StringComparison.Ordinal))
                .ToList();
            Assert.True(
                matchingNodes.Count == 1,
                $"Expected one node titled '{title}', found {matchingNodes.Count}. Markup: {cut.Markup}");
            createdNodeId = Assert.Single(
                canvasWorkbench.Instance.Surface.Nodes,
                node => string.Equals(node.Title, title, StringComparison.Ordinal)).Id;
        });

        return createdNodeId;
    }

    private static IElement FindButtonByLabel(
        IRenderedFragment cut,
        string label,
        string selector = "button")
        => cut.FindAll(selector)
            .First(button => button.TextContent.Contains(label, StringComparison.Ordinal));

    private static CanvasWorkbenchAction FindCreateAction(
        IReadOnlyList<CanvasWorkbenchAction> actions,
        string actionId)
        => TryFindCreateAction(actions, actionId)
           ?? throw new InvalidOperationException($"Create action '{actionId}' was not found.");

    private static CanvasWorkbenchAction? TryFindCreateAction(
        IReadOnlyList<CanvasWorkbenchAction> actions,
        string actionId)
    {
        foreach (var action in actions)
        {
            if (string.Equals(action.ActionId, actionId, StringComparison.Ordinal))
            {
                return action;
            }

            if (action.Children.Count == 0)
            {
                continue;
            }

            var childAction = TryFindCreateAction(action.Children, actionId);
            if (childAction is not null)
            {
                return childAction;
            }
        }

        return null;
    }

    private static IRenderedComponent<CanvasWorkbench> WaitForCanvasWorkbench(IRenderedFragment cut)
    {
        IRenderedComponent<CanvasWorkbench>? canvasWorkbench = null;
        cut.WaitForAssertion(() => canvasWorkbench = cut.FindComponent<CanvasWorkbench>());
        return canvasWorkbench ?? throw new InvalidOperationException("Canvas workbench did not render.");
    }

    private static CanvasWorkbenchUploadedFile BuildUploadedFile(string fileName, string contentType, string content)
        => new()
        {
            FileName = fileName,
            ContentType = contentType,
            Base64Data = Convert.ToBase64String(Encoding.UTF8.GetBytes(content))
        };

    private sealed class RecordingAgentImageGenerationService : IAgentImageGenerationService
    {
        private static readonly byte[] PngBytes = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=");

        private readonly TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<AgentImageGenerationRequest> firstRequest = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly Exception? failure;

        public RecordingAgentImageGenerationService(
            bool waitForRelease = false,
            Exception? failure = null)
        {
            this.failure = failure;
            if (!waitForRelease)
            {
                release.TrySetResult();
            }
        }

        public ConcurrentQueue<AgentImageGenerationRequest> Requests { get; } = new();

        public async Task<AgentImageGenerationRequest> WaitForFirstRequestAsync()
            => await firstRequest.Task.WaitAsync(TimeSpan.FromSeconds(10));

        public void CompleteGeneration()
            => release.TrySetResult();

        public async Task<AgentImageGenerationResult> GenerateAsync(
            AgentImageGenerationRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Enqueue(request);
            firstRequest.TrySetResult(request);
            await release.Task.WaitAsync(cancellationToken);
            if (failure is not null)
            {
                throw failure;
            }

            return new AgentImageGenerationResult(
                request.Model,
                request.Format,
                [new AgentGeneratedImage("image/png", PngBytes, request.Prompt)]);
        }
    }

    private sealed class StartedDeferredCompletionWorker(ProjectStructureDeferredNodeCompletionWorker worker) : IAsyncDisposable
    {
        public static async Task<StartedDeferredCompletionWorker> StartAsync(ComponentTestHarness harness)
        {
            var worker = harness.Context.Services.GetRequiredService<ProjectStructureDeferredNodeCompletionWorker>();
            await worker.StartAsync(CancellationToken.None);
            return new StartedDeferredCompletionWorker(worker);
        }

        public async ValueTask DisposeAsync()
        {
            await worker.StopAsync(CancellationToken.None);
        }
    }

    private static void WrapDbContextFactoryWithCreateCounter(IServiceCollection services)
    {
        services.AddSingleton<DbContextCreateCounter>();

        var factoryDescriptor = services.Last(descriptor => descriptor.ServiceType == typeof(IDbContextFactory<AppDbContext>));
        services.Remove(factoryDescriptor);
        services.Add(new ServiceDescriptor(
            typeof(IDbContextFactory<AppDbContext>),
            serviceProvider =>
            {
                var innerFactory = (IDbContextFactory<AppDbContext>)CreateService(serviceProvider, factoryDescriptor);
                var counter = serviceProvider.GetRequiredService<DbContextCreateCounter>();
                return new CountingDbContextFactory(innerFactory, counter);
            },
            factoryDescriptor.Lifetime));
    }

    private static object CreateService(IServiceProvider serviceProvider, ServiceDescriptor descriptor)
    {
        if (descriptor.ImplementationInstance is not null)
        {
            return descriptor.ImplementationInstance;
        }

        if (descriptor.ImplementationFactory is not null)
        {
            return descriptor.ImplementationFactory(serviceProvider);
        }

        if (descriptor.ImplementationType is not null)
        {
            return ActivatorUtilities.GetServiceOrCreateInstance(serviceProvider, descriptor.ImplementationType);
        }

        throw new InvalidOperationException($"Service descriptor for '{descriptor.ServiceType}' does not expose an implementation.");
    }

    private sealed class CountingDbContextFactory(
        IDbContextFactory<AppDbContext> innerFactory,
        DbContextCreateCounter counter) : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext()
        {
            counter.Increment();
            return innerFactory.CreateDbContext();
        }

        public async Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            counter.Increment();
            return await innerFactory.CreateDbContextAsync(cancellationToken);
        }
    }

    private sealed class DbContextCreateCounter
    {
        private int createCount;

        public int CreateCount => createCount;

        public void Increment()
        {
            Interlocked.Increment(ref createCount);
        }

        public void Reset()
        {
            Interlocked.Exchange(ref createCount, 0);
        }
    }
}
