using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AngleSharp.Dom;
using Bunit;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Conversations.Shell;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Security;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace CanDoItAll.Tests.Components.ProjectStructure;

public sealed class ProjectStructurePageSimpleMutationTests
{
    [Fact]
    public async Task Agents_toggle_tracks_the_visible_conversation_catalog_after_external_close() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var shell = harness.Context.Services.GetRequiredService<IConversationShellCoordinator>();
        var projectId = await CreateProjectAsync(projectsService, "Project agents catalogue");
        var cut = harness.Context.Render<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));
        WaitForCanvasWorkbench(cut);

        cut.Find("[data-testid='project-structure-agents-toggle']").Click();

        Assert.True(shell.Snapshot().IsCatalogVisible);
        Assert.Equal(ConversationCatalogKindFilter.Agents, shell.Snapshot().KindFilter);
        await cut.InvokeAsync(shell.HideCatalog);

        cut.Find("[data-testid='project-structure-agents-toggle']").Click();

        Assert.True(shell.Snapshot().IsCatalogVisible);
        cut.Find("[data-testid='project-structure-agents-toggle']").Click();
        Assert.False(shell.Snapshot().IsCatalogVisible);
    }

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

        var cut = harness.Context.Render<ProjectStructurePage>(
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
    public async Task Text_asset_actions_bypass_the_generic_composer_and_preserve_drag_drop()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        Guid projectId = await CreateProjectAsync(projectsService, "Text asset action routing");
        string rootNodeId = $"project:{projectId}";
        var cut = harness.Context.Render<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));
        IRenderedComponent<CanvasWorkbench> canvasWorkbench = WaitForCanvasWorkbench(cut);
        string[] actionIds =
        [
            "add-file-text",
            "add-file-json",
            "add-file-markdown",
            "add-file-mermaid",
            "add-file-log"
        ];

        cut.WaitForAssertion(() =>
        {
            CanvasWorkbenchNode rootNode = Assert.Single(
                canvasWorkbench.Instance.Surface.Nodes,
                node => string.Equals(node.Id, rootNodeId, StringComparison.Ordinal));
            foreach (string actionId in actionIds)
            {
                AssertDedicatedTextAssetAction(
                    FindCreateAction(
                        canvasWorkbench.Instance.Surface.Chrome.QuickCreateActions,
                        actionId));
                AssertDedicatedTextAssetAction(
                    FindCreateAction(rootNode.ContextActions, actionId));
            }
        });
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

        var cut = harness.Context.Render<ProjectStructurePage>(
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

        var cut = harness.Context.Render<ProjectStructurePage>(
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
    public async Task Image_asset_create_persists_payload_larger_than_default_signalr_message_limit()
    {
        await using var harness = await ComponentTestHarness.CreateAsync(services =>
            services.Replace(ServiceDescriptor.Singleton<ISecretVault>(new InMemorySecretVault())));
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var agentService = harness.Context.Services.GetRequiredService<ProjectStructureAgentService>();
        var projectId = await CreateProjectAsync(projectsService, "Large image asset");
        var parentNodeId = $"project:{projectId}";
        await SaveSelectedNodeStateAsync(workbenchService, projectId, parentNodeId);

        var imageBytes = new byte[48 * 1024];
        for (var index = 0; index < imageBytes.Length; index++)
        {
            imageBytes[index] = (byte)(index % 251);
        }

        new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }.CopyTo(imageBytes, 0);
        var uploadedFile = BuildUploadedFile("large-image.png", "image/png", imageBytes);
        Assert.True(uploadedFile.Base64Data.Length > 32 * 1024);

        var cut = harness.Context.Render<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));
        var canvasWorkbench = WaitForCanvasWorkbench(cut);
        var createdNodeId = await InvokeCreateActionAsync(
            cut,
            canvasWorkbench,
            "add-image-asset",
            parentNodeId,
            parentNodeId,
            "Large uploaded image",
            "SignalR regression",
            "Image content larger than the default inbound hub message limit.",
            uploadedFile: uploadedFile);

        var persistedSurface = await workbenchService.GetStructureAsync(projectId);
        var persistedNode = Assert.Single(
            persistedSurface.Nodes,
            node => string.Equals(node.Id, createdNodeId, StringComparison.Ordinal));
        Assert.Equal(ProjectObjectType.ImageAsset, persistedNode.ObjectType);
        Assert.Equal("image/png", persistedNode.MediaContentType);
        Assert.Equal("large-image.png", persistedNode.MediaOriginalFileName);
        Assert.False(string.IsNullOrWhiteSpace(persistedNode.MediaRelativePath));

        var persistedContent = await agentService.GetAssetContentAsync(projectId, createdNodeId);
        Assert.Equal(imageBytes.LongLength, persistedContent.ContentLength);
        Assert.Equal(imageBytes, Convert.FromBase64String(persistedContent.Base64Data));
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
        var secretService = harness.Context.Services.GetRequiredService<SecretService>();

        var providerId = await SaveImageProviderAsync(secretService, agentWorkspaceService);

        var projectId = await CreateProjectAsync(projectsService, "Generated image asset");
        var parentNodeId = $"project:{projectId}";
        await SaveSelectedNodeStateAsync(workbenchService, projectId, parentNodeId);

        var cut = harness.Context.Render<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));
        var canvasWorkbench = WaitForCanvasWorkbench(cut);

        cut.WaitForAssertion(() =>
        {
            Assert.Contains(
                canvasWorkbench.Instance.Surface.Chrome.QuickCreateActions,
                action => action.ActionId == "group-assets" &&
                          action.Children.Any(child => child.ActionId == "generate-image-asset"));
        });
        var generatedImageAction = await RefreshGeneratedImageCreateActionAsync(
            cut,
            canvasWorkbench);
        var providerField = Assert.Single(
            generatedImageAction.InputFields,
            field => string.Equals(field.Key, "imageProviderProfileId", StringComparison.Ordinal));
        Assert.Contains(
            providerField.Options,
            option => string.Equals(option.Value, providerId.ToString("D"), StringComparison.Ordinal));

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
            dbContext.Set<CanDoItAll.Modules.AgentFramework.ProviderManagement.ProviderProfile>().Add(new CanDoItAll.Modules.AgentFramework.ProviderManagement.ProviderProfile
            {
                Id = providerId,
                Name = "OpenAI image generation",
                ProviderKind = CanDoItAll.Modules.AgentFramework.ProviderManagement.ProviderKind.OpenAi,
                ConnectorPluginKey = CanDoItAll.Modules.AgentFramework.ProviderManagement.ProviderConnectorKeys.OpenAi,
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
                    connectorPluginKey = CanDoItAll.Modules.AgentFramework.ProviderManagement.ProviderConnectorKeys.OpenAi,
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

        var cut = harness.Context.Render<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));
        var canvasWorkbench = WaitForCanvasWorkbench(cut);

        var generatedImageAction = await RefreshGeneratedImageCreateActionAsync(
            cut,
            canvasWorkbench);
        var providerField = Assert.Single(
            generatedImageAction.InputFields,
            field => string.Equals(field.Key, "imageProviderProfileId", StringComparison.Ordinal));
        Assert.Contains(
            providerField.Options,
            option => string.Equals(
                option.Label,
                "OpenAI image generation (gpt-image-1-mini)",
                StringComparison.Ordinal));

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
        var secretService = harness.Context.Services.GetRequiredService<SecretService>();
        var providerId = await SaveImageProviderAsync(secretService, agentWorkspaceService, "Failing image provider");

        var projectId = await CreateProjectAsync(projectsService, "Generated image failure");
        var parentNodeId = $"project:{projectId}";
        await SaveSelectedNodeStateAsync(workbenchService, projectId, parentNodeId);

        var cut = harness.Context.Render<ProjectStructurePage>(
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

        var cut = harness.Context.Render<ProjectStructurePage>(
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

        var cut = harness.Context.Render<ProjectStructurePage>(
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
        var runtimeProjectPath = ResolveComponentTestProjectPath();
        Assert.True(File.Exists(runtimeProjectPath), $"Runtime fixture project was not found at '{runtimeProjectPath}'.");

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
                        ProjectPath = runtimeProjectPath,
                        LaunchProfileName = "https",
                        RuntimeProtocol = ProjectRuntimeProtocol.Https,
                        LocalhostUrl = "https://localhost:7143"
                    }
                })));

        await SaveSelectedNodeStateAsync(workbenchService, projectId, runtimeNode.Id);

        var cut = harness.Context.Render<ProjectStructurePage>(
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
                    new CanvasWorkbenchInputValue { Key = "projectPath", Value = runtimeProjectPath },
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
        var dialogHost = harness.Context.Render<DialogHost>();
        var cut = harness.Context.Render<ProjectStructurePage>(
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

        var captureEvidenceTask = InvokeCreateActionAsync(
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
        dialogHost.WaitForElement("[data-testid='project-structure-task-create-submit']").Click();
        _ = await captureEvidenceTask.WaitAsync(TimeSpan.FromSeconds(20));

        var exportEvidenceTask = InvokeCreateActionAsync(
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
        dialogHost.WaitForElement("[data-testid='project-structure-task-create-submit']").Click();
        _ = await exportEvidenceTask.WaitAsync(TimeSpan.FromSeconds(20));

        var reconnectFollowUpTask = InvokeCreateActionAsync(
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
        dialogHost.WaitForElement("[data-testid='project-structure-task-create-submit']").Click();
        _ = await reconnectFollowUpTask.WaitAsync(TimeSpan.FromSeconds(20));

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
            "Operator checklist and rollout notes.",
            uploadedFile: BuildUploadedFile(
                "runbook.txt",
                "text/plain",
                "Validate the release, capture evidence, and record the rollout result."));

        _ = await InvokeCreateActionAsync(
            cut,
            canvasWorkbench,
            "add-file-json",
            projectRootId,
            projectRootId,
            "Settings JSON",
            "config",
            "Runtime settings for strict toolbox validation.",
            uploadedFile: BuildUploadedFile(
                "settings.json",
                "application/json",
                "{\n  \"toolbox\": true,\n  \"validation\": \"strict\"\n}"));

        _ = await InvokeCreateActionAsync(
            cut,
            canvasWorkbench,
            "add-file-markdown",
            projectRootId,
            projectRootId,
            "Evidence README",
            "docs",
            "Validation evidence index.",
            uploadedFile: BuildUploadedFile(
                "README.md",
                "text/markdown",
                "# Validation evidence\n\nCapture screenshots and exports."));

        const string mermaidTitle = "Validation flow diagram";
        const string mermaidPurpose = "Visualizes the bundle validation timeline.";
        const string mermaidSource = "gantt\n" +
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
            mermaidPurpose,
            uploadedFile: BuildUploadedFile(
                "validation-flow.mmd",
                "text/vnd.mermaid",
                mermaidSource));

        cut.WaitForAssertion(() =>
        {
            var runtimeNode = Assert.Single(
                canvasWorkbench.Instance.Surface.Nodes,
                node => string.Equals(node.Id, mermaidId, StringComparison.Ordinal));
            Assert.Equal(mermaidTitle, runtimeNode.Title);
            Assert.Equal("Mermaid", runtimeNode.Kind);
            Assert.Equal(mermaidPurpose, runtimeNode.InlineText);
        });

        var persistedSurface = await workbenchService.GetStructureAsync(projectId);
        var persistedMermaidNode = Assert.Single(
            persistedSurface.Nodes,
            node => string.Equals(node.Id, mermaidId, StringComparison.Ordinal));
        Assert.Equal(ProjectObjectType.File, persistedMermaidNode.ObjectType);
        Assert.Equal("mermaid", persistedMermaidNode.ObjectSubtype);
        Assert.Equal(mermaidTitle, persistedMermaidNode.Title);
        Assert.Equal(mermaidPurpose, persistedMermaidNode.Notes);
        Assert.Equal("validation-flow.mmd", persistedMermaidNode.MediaOriginalFileName);
        Assert.Equal("text/vnd.mermaid", persistedMermaidNode.MediaContentType);
        Assert.Equal(
            MermaidDiagramKind.Gantt,
            ProjectObjectMetadataSerializer.Parse(persistedMermaidNode.MetadataJson).File?.MermaidDiagramKind);
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

        var cut = harness.Context.Render<ProjectStructurePage>(
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

        var cut = harness.Context.Render<ProjectStructurePage>(
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

        var cut = harness.Context.Render<ProjectStructurePage>(
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

        var cut = harness.Context.Render<ProjectStructurePage>(
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
    public async Task Canvas_context_delete_of_managed_asset_warns_about_file_and_deletes_it_after_confirmation()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var assetCreationService = harness.Context.Services.GetRequiredService<ProjectAssetCreationService>();

        var projectId = await CreateProjectAsync(projectsService, "Managed asset delete project");
        var media = await assetCreationService.CreateTextAsync(
            ProjectFileSubtype.Markdown,
            "delete-me.md",
            "# Delete me");
        var asset = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.File,
                "Delete me",
                string.Empty,
                "Managed Markdown asset.",
                $"project:{projectId}",
                420,
                240,
                ObjectSubtype: "markdown",
                Media: media));
        var physicalPath = Path.Combine(
            harness.ActiveProfile.WorkspaceRootPath,
            asset.MediaRelativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(physicalPath));
        await SaveSelectedNodeStateAsync(workbenchService, projectId, asset.Id);

        var cut = harness.Context.Render<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));
        var canvasWorkbench = WaitForCanvasWorkbench(cut);

        cut.WaitForAssertion(() =>
        {
            var canvasAsset = Assert.Single(
                canvasWorkbench.Instance.Surface.Nodes,
                node => string.Equals(node.Id, asset.Id, StringComparison.Ordinal));
            Assert.Single(canvasAsset.ContextActions, action => action.ActionId == "delete");
        });

        await cut.InvokeAsync(() => canvasWorkbench.Instance.OnContextAction(asset.Id, "delete", 0, 0));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Delete node", cut.Markup, StringComparison.Ordinal);
            Assert.Contains(
                "Choose whether to preserve its stored file or request deletion",
                cut.Markup,
                StringComparison.Ordinal);
            Assert.Contains("Delete node only", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("Delete node and file", cut.Markup, StringComparison.Ordinal);
        });
        var surfaceBeforeConfirmation = await workbenchService.GetStructureAsync(projectId);
        Assert.Contains(surfaceBeforeConfirmation.Nodes, node => string.Equals(node.Id, asset.Id, StringComparison.Ordinal));
        Assert.True(File.Exists(physicalPath));

        FindButtonByLabel(cut, "Delete node and file", "[role='dialog'] button").Click();

        cut.WaitForAssertion(() => Assert.Contains("eligible managed files were deleted", cut.Markup, StringComparison.Ordinal));
        var surfaceAfterConfirmation = await workbenchService.GetStructureAsync(projectId);
        Assert.DoesNotContain(surfaceAfterConfirmation.Nodes, node => string.Equals(node.Id, asset.Id, StringComparison.Ordinal));
        Assert.False(File.Exists(physicalPath));
    }

    [Fact]
    public async Task Canvas_context_delete_of_managed_asset_can_preserve_the_backing_file()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var assetCreationService = harness.Context.Services.GetRequiredService<ProjectAssetCreationService>();

        var projectId = await CreateProjectAsync(projectsService, "Managed asset retention project");
        var media = await assetCreationService.CreateTextAsync(
            ProjectFileSubtype.Markdown,
            "preserve-me.md",
            "# Preserve me");
        var asset = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.File,
                "Preserve file",
                string.Empty,
                "Managed file retained after node deletion.",
                $"project:{projectId}",
                420,
                240,
                ObjectSubtype: "markdown",
                Media: media));
        var physicalPath = Path.Combine(
            harness.ActiveProfile.WorkspaceRootPath,
            asset.MediaRelativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(physicalPath));
        await SaveSelectedNodeStateAsync(workbenchService, projectId, asset.Id);

        var cut = harness.Context.Render<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));
        var canvasWorkbench = WaitForCanvasWorkbench(cut);
        cut.WaitForAssertion(() => Assert.Contains(
            canvasWorkbench.Instance.Surface.Nodes,
            node => string.Equals(node.Id, asset.Id, StringComparison.Ordinal)));

        await cut.InvokeAsync(() =>
            canvasWorkbench.Instance.OnContextAction(asset.Id, "delete", 0, 0));
        cut.WaitForAssertion(() => Assert.Contains(
            "Delete node only",
            cut.Markup,
            StringComparison.Ordinal));

        FindButtonByLabel(cut, "Delete node only", "[role='dialog'] button").Click();

        cut.WaitForAssertion(() => Assert.Contains(
            "managed files were preserved",
            cut.Markup,
            StringComparison.Ordinal));
        var surfaceAfterConfirmation = await workbenchService.GetStructureAsync(projectId);
        Assert.DoesNotContain(
            surfaceAfterConfirmation.Nodes,
            node => string.Equals(node.Id, asset.Id, StringComparison.Ordinal));
        Assert.True(File.Exists(physicalPath));
    }

    [Fact]
    public async Task Canvas_multi_delete_reports_completed_asset_when_another_storage_binding_is_invalid()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var assetCreationService = harness.Context.Services.GetRequiredService<ProjectAssetCreationService>();
        var dbContextFactory = harness.Context.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var projectId = await CreateProjectAsync(projectsService, "Managed asset partial batch project");
        var validNode = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Note,
                "Valid batch note",
                string.Empty,
                "This node can be deleted independently.",
                $"project:{projectId}",
                420,
                240));
        var invalidMedia = await assetCreationService.CreateTextAsync(
            ProjectFileSubtype.Markdown,
            "invalid-batch-delete.md",
            "# Invalid batch delete");
        var invalidAsset = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.File,
                "Invalid batch asset",
                string.Empty,
                "This asset has stale storage provenance.",
                $"project:{projectId}",
                700,
                240,
                ObjectSubtype: "markdown",
                Media: invalidMedia));
        var invalidPhysicalPath = Path.Combine(
            harness.ActiveProfile.WorkspaceRootPath,
            invalidAsset.MediaRelativePath.Replace('/', Path.DirectorySeparatorChar));
        await using (var dbContext = await dbContextFactory.CreateDbContextAsync())
        {
            var invalidRecordId = await dbContext.Set<ProjectObjectRecord>()
                .Where(record => record.ProjectId == projectId && record.NodeKey == invalidAsset.Id)
                .Select(record => record.Id)
                .SingleAsync();
            var binding = await dbContext.Set<ProjectNodeBindingRecord>()
                .SingleAsync(record => record.ProjectObjectId == invalidRecordId);
            var referenceJson = JsonNode.Parse(binding.StorageObjectReferenceJson)?.AsObject()
                ?? throw new InvalidOperationException("The test asset has no storage reference.");
            var provenanceJson = referenceJson["metadataJson"]?.GetValue<string>()
                ?? throw new InvalidOperationException("The test asset has no managed provenance.");
            var provenance = JsonNode.Parse(provenanceJson)?.AsObject()
                ?? throw new InvalidOperationException("The test asset managed provenance is invalid.");
            var currentFingerprint = provenance["physicalObjectFingerprint"]?.GetValue<string>()
                ?? throw new InvalidOperationException("The test asset has no physical fingerprint.");
            var staleFingerprint = currentFingerprint[0] == '0'
                ? $"1{currentFingerprint[1..]}"
                : $"0{currentFingerprint[1..]}";
            provenance["physicalObjectFingerprint"] = staleFingerprint;
            referenceJson["metadataJson"] = provenance.ToJsonString();
            binding.StorageObjectReferenceJson = referenceJson.ToJsonString();
            await dbContext.SaveChangesAsync();
            dbContext.ChangeTracker.Clear();
            var persistedReferenceJson = await dbContext.Set<ProjectNodeBindingRecord>()
                .Where(record => record.ProjectObjectId == invalidRecordId)
                .Select(record => record.StorageObjectReferenceJson)
                .SingleAsync();
            Assert.Contains(staleFingerprint, persistedReferenceJson, StringComparison.Ordinal);
        }

        await SaveSelectedNodeStateAsync(
            workbenchService,
            projectId,
            validNode.Id,
            invalidAsset.Id);
        var cut = harness.Context.Render<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));
        var canvasWorkbench = WaitForCanvasWorkbench(cut);
        cut.WaitForAssertion(() => Assert.Contains("2 nodes selected", cut.Markup, StringComparison.Ordinal));

        await cut.InvokeAsync(() =>
            canvasWorkbench.Instance.OnContextAction(validNode.Id, "delete", 0, 0));
        cut.WaitForAssertion(() => Assert.Contains("Delete nodes and files", cut.Markup, StringComparison.Ordinal));
        FindButtonByLabel(cut, "Delete nodes and files", "[role='dialog'] button").Click();

        cut.WaitForAssertion(() => Assert.Contains(
            "1 node was confirmed deleted. 1 selected branch requires separate follow-up.",
            cut.Markup,
            StringComparison.Ordinal));
        var persistedSurface = await workbenchService.GetStructureAsync(projectId);
        Assert.DoesNotContain(persistedSurface.Nodes, node => node.Id == validNode.Id);
        Assert.Contains(persistedSurface.Nodes, node => node.Id == invalidAsset.Id);
        Assert.True(File.Exists(invalidPhysicalPath));
    }

    [Fact]
    public async Task Canvas_context_delete_of_parent_warns_about_descendant_file_and_deletes_entire_branch_after_confirmation()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var assetCreationService = harness.Context.Services.GetRequiredService<ProjectAssetCreationService>();

        var projectId = await CreateProjectAsync(projectsService, "Asset branch delete project");
        var parent = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Note,
                "Asset-bearing parent",
                string.Empty,
                "Parent containing a managed asset.",
                $"project:{projectId}",
                420,
                240));
        var media = await assetCreationService.CreateTextAsync(
            ProjectFileSubtype.Markdown,
            "delete-with-parent.md",
            "# Delete with parent");
        var asset = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.File,
                "Child asset",
                string.Empty,
                "Managed child asset.",
                parent.Id,
                680,
                240,
                ObjectSubtype: "markdown",
                Media: media));
        var secondMedia = await assetCreationService.CreateTextAsync(
            ProjectFileSubtype.Markdown,
            "delete-second-with-parent.md",
            "# Delete second with parent");
        var secondAsset = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.File,
                "Second child asset",
                string.Empty,
                "Second managed child asset.",
                parent.Id,
                680,
                420,
                ObjectSubtype: "markdown",
                Media: secondMedia));
        var physicalPath = Path.Combine(
            harness.ActiveProfile.WorkspaceRootPath,
            asset.MediaRelativePath.Replace('/', Path.DirectorySeparatorChar));
        var secondPhysicalPath = Path.Combine(
            harness.ActiveProfile.WorkspaceRootPath,
            secondAsset.MediaRelativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(physicalPath));
        Assert.True(File.Exists(secondPhysicalPath));
        await SaveSelectedNodeStateAsync(workbenchService, projectId, parent.Id);

        var cut = harness.Context.Render<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));
        var canvasWorkbench = WaitForCanvasWorkbench(cut);

        cut.WaitForAssertion(() =>
        {
            var canvasParent = Assert.Single(
                canvasWorkbench.Instance.Surface.Nodes,
                node => string.Equals(node.Id, parent.Id, StringComparison.Ordinal));
            Assert.Single(canvasParent.ContextActions, action => action.ActionId == "delete");
        });

        await cut.InvokeAsync(() => canvasWorkbench.Instance.OnContextAction(parent.Id, "delete", 0, 0));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("This will delete 3 nodes including child items.", cut.Markup, StringComparison.Ordinal);
            Assert.Contains(
                "This branch has 2 managed file attachments.",
                cut.Markup,
                StringComparison.Ordinal);
            Assert.Contains(
                "Choose whether to preserve their stored files",
                cut.Markup,
                StringComparison.Ordinal);
        });
        var surfaceBeforeConfirmation = await workbenchService.GetStructureAsync(projectId);
        Assert.Contains(surfaceBeforeConfirmation.Nodes, node => string.Equals(node.Id, parent.Id, StringComparison.Ordinal));
        Assert.Contains(surfaceBeforeConfirmation.Nodes, node => string.Equals(node.Id, asset.Id, StringComparison.Ordinal));
        Assert.Contains(surfaceBeforeConfirmation.Nodes, node => string.Equals(node.Id, secondAsset.Id, StringComparison.Ordinal));
        Assert.True(File.Exists(physicalPath));
        Assert.True(File.Exists(secondPhysicalPath));

        FindButtonByLabel(cut, "Delete nodes and files", "[role='dialog'] button").Click();

        cut.WaitForAssertion(() => Assert.Contains("eligible managed files were deleted", cut.Markup, StringComparison.Ordinal));
        var surfaceAfterConfirmation = await workbenchService.GetStructureAsync(projectId);
        Assert.DoesNotContain(surfaceAfterConfirmation.Nodes, node => string.Equals(node.Id, parent.Id, StringComparison.Ordinal));
        Assert.DoesNotContain(surfaceAfterConfirmation.Nodes, node => string.Equals(node.Id, asset.Id, StringComparison.Ordinal));
        Assert.DoesNotContain(surfaceAfterConfirmation.Nodes, node => string.Equals(node.Id, secondAsset.Id, StringComparison.Ordinal));
        Assert.False(File.Exists(physicalPath));
        Assert.False(File.Exists(secondPhysicalPath));
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

        var cut = harness.Context.Render<ProjectStructurePage>(
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

        var cut = harness.Context.Render<ProjectStructurePage>(
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

        var cut = harness.Context.Render<ProjectStructurePage>(
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

        var cut = harness.Context.Render<ProjectStructurePage>(
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
        await cut.InvokeAsync(() =>
            cut.Find("[data-testid='project-structure-signals-action-marker-question']").Click());

        cut.WaitForAssertion(() =>
        {
            var updatedNode = Assert.Single(canvasWorkbench.Instance.Surface.Nodes, item => string.Equals(item.Id, node.Id, StringComparison.Ordinal));
            Assert.Equal("question", updatedNode.MarkerIcon);
            Assert.Single(updatedNode.Markers);
            Assert.Equal("question", updatedNode.Markers[0].Icon);
        });
        Assert.InRange(createCounter.CreateCount, 1, 2);

        createCounter.Reset();
        await cut.InvokeAsync(() =>
            cut.Find("[data-testid='project-structure-signals-action-marker-risk']").Click());

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
    public async Task Clipboard_copy_and_paste_duplicates_normalized_forest_under_selected_destination_and_remains_reusable()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();

        var projectId = await CreateProjectAsync(projectsService, "Clipboard copy forest");
        var projectRootNodeId = $"project:{projectId}";
        var sourceNode = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                "Network orchestration",
                "Delivery flow",
                "Parent subtree node for copy and paste.",
                projectRootNodeId,
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
                "Task child that should be duplicated with the subtree.",
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
        var secondSourceNode = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Note,
                "Field constraints",
                "Deployment notes",
                "A second selected root copied in the same forest.",
                projectRootNodeId,
                720,
                560));
        var destinationNode = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                "Execution package",
                "Paste destination",
                "Copied roots become direct children of this node.",
                projectRootNodeId,
                1540,
                560));

        var cut = harness.Context.Render<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));
        var canvasWorkbench = WaitForCanvasWorkbench(cut);

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Network orchestration", cut.Markup);
            Assert.Equal(6, canvasWorkbench.Instance.Surface.Nodes.Count);
        });

        var surfaceId = canvasWorkbench.Instance.Surface.SurfaceId;
        await InvokeClipboardActionAsync(
            cut,
            canvasWorkbench,
            new CanvasWorkbenchClipboardRequest(
                CanvasWorkbenchClipboardAction.Copy,
                surfaceId,
                sourceNode.Id,
                [sourceNode.Id, taskNode.Id, secondSourceNode.Id]));
        await InvokeClipboardActionAsync(
            cut,
            canvasWorkbench,
            new CanvasWorkbenchClipboardRequest(
                CanvasWorkbenchClipboardAction.Paste,
                surfaceId,
                destinationNode.Id,
                [destinationNode.Id],
                new CanvasWorkbenchPoint { X = 8000, Y = 6000 }));

        IReadOnlyList<string> firstCopiedRootNodeIds = [];
        cut.WaitForAssertion(() =>
        {
            firstCopiedRootNodeIds = canvasWorkbench.Instance.Surface.UiState.SelectedNodeIds.ToList();
            Assert.Equal(2, firstCopiedRootNodeIds.Count);
            Assert.DoesNotContain(sourceNode.Id, firstCopiedRootNodeIds);
            Assert.DoesNotContain(secondSourceNode.Id, firstCopiedRootNodeIds);
            Assert.Contains("Copied 2 branches", cut.Markup, StringComparison.OrdinalIgnoreCase);
        });

        var persistedSurface = await workbenchService.GetStructureAsync(projectId);
        var firstCopiedSource = Assert.Single(
            persistedSurface.Nodes,
            node => firstCopiedRootNodeIds.Contains(node.Id, StringComparer.Ordinal) &&
                    string.Equals(node.Title, sourceNode.Title, StringComparison.Ordinal));
        var firstCopiedSecondSource = Assert.Single(
            persistedSurface.Nodes,
            node => firstCopiedRootNodeIds.Contains(node.Id, StringComparer.Ordinal) &&
                    string.Equals(node.Title, secondSourceNode.Title, StringComparison.Ordinal));
        var firstCopiedTask = Assert.Single(
            persistedSurface.Nodes,
            node => string.Equals(node.ParentId, firstCopiedSource.Id, StringComparison.Ordinal) &&
                    string.Equals(node.Title, taskNode.Title, StringComparison.Ordinal));
        var firstCopiedEvidence = Assert.Single(
            persistedSurface.Nodes,
            node => string.Equals(node.ParentId, firstCopiedTask.Id, StringComparison.Ordinal) &&
                    string.Equals(node.Title, evidenceNode.Title, StringComparison.Ordinal));

        Assert.Equal(destinationNode.Id, firstCopiedSource.ParentId);
        Assert.Equal(destinationNode.Id, firstCopiedSecondSource.ParentId);
        Assert.NotEqual(taskNode.Id, firstCopiedTask.Id);
        Assert.NotEqual(evidenceNode.Id, firstCopiedEvidence.Id);
        Assert.Equal(projectRootNodeId, Assert.Single(persistedSurface.Nodes, node => node.Id == sourceNode.Id).ParentId);
        Assert.Equal(sourceNode.Id, Assert.Single(persistedSurface.Nodes, node => node.Id == taskNode.Id).ParentId);
        Assert.Equal(projectRootNodeId, Assert.Single(persistedSurface.Nodes, node => node.Id == secondSourceNode.Id).ParentId);

        await InvokeClipboardActionAsync(
            cut,
            canvasWorkbench,
            new CanvasWorkbenchClipboardRequest(
                CanvasWorkbenchClipboardAction.Paste,
                surfaceId,
                destinationNode.Id,
                [destinationNode.Id]));

        cut.WaitForAssertion(() =>
        {
            var secondCopiedRootNodeIds = canvasWorkbench.Instance.Surface.UiState.SelectedNodeIds;
            Assert.Equal(2, secondCopiedRootNodeIds.Count);
            Assert.DoesNotContain(secondCopiedRootNodeIds, nodeId => firstCopiedRootNodeIds.Contains(nodeId, StringComparer.Ordinal));
            Assert.Equal(14, canvasWorkbench.Instance.Surface.Nodes.Count);
        });
    }

    [Fact]
    public async Task Clipboard_cut_and_paste_reparents_normalized_forest_and_consumes_cut_buffer()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();

        var projectId = await CreateProjectAsync(projectsService, "Clipboard cut forest");
        var projectRootNodeId = $"project:{projectId}";
        var sourceNode = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                "Source branch",
                "Cut source",
                "The first selected subtree root.",
                projectRootNodeId,
                520,
                140));
        var sourceChildNode = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.WorkItem,
                "Source child",
                "Nested node",
                "Selecting this child with its parent must not create a second moved root.",
                sourceNode.Id,
                760,
                300));
        var secondSourceNode = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Note,
                "Second source branch",
                "Cut source",
                "The second selected subtree root.",
                projectRootNodeId,
                680,
                520));
        var destinationNode = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                "Destination branch",
                "Paste target",
                "Both cut roots become children of this node.",
                projectRootNodeId,
                1320,
                220));
        var alternateDestinationNode = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                "Alternate destination",
                "Second paste target",
                "A consumed cut buffer must not move the roots here.",
                projectRootNodeId,
                1480,
                520));

        var cut = harness.Context.Render<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));
        var canvasWorkbench = WaitForCanvasWorkbench(cut);
        var surfaceId = canvasWorkbench.Instance.Surface.SurfaceId;

        await InvokeClipboardActionAsync(
            cut,
            canvasWorkbench,
            new CanvasWorkbenchClipboardRequest(
                CanvasWorkbenchClipboardAction.Cut,
                surfaceId,
                sourceNode.Id,
                [sourceChildNode.Id, sourceNode.Id, secondSourceNode.Id]));
        await InvokeClipboardActionAsync(
            cut,
            canvasWorkbench,
            new CanvasWorkbenchClipboardRequest(
                CanvasWorkbenchClipboardAction.Paste,
                surfaceId,
                destinationNode.Id,
                [destinationNode.Id],
                new CanvasWorkbenchPoint { X = 9000, Y = 7000 }));

        var persistedSurface = await workbenchService.GetStructureAsync(projectId);
        Assert.Equal(destinationNode.Id, Assert.Single(persistedSurface.Nodes, node => node.Id == sourceNode.Id).ParentId);
        Assert.Equal(sourceNode.Id, Assert.Single(persistedSurface.Nodes, node => node.Id == sourceChildNode.Id).ParentId);
        Assert.Equal(destinationNode.Id, Assert.Single(persistedSurface.Nodes, node => node.Id == secondSourceNode.Id).ParentId);

        cut.WaitForAssertion(() =>
        {
            Assert.True(
                canvasWorkbench.Instance.Surface.UiState.SelectedNodeIds
                    .ToHashSet(StringComparer.Ordinal)
                    .SetEquals([sourceNode.Id, secondSourceNode.Id]));
            Assert.Contains("Moved 2 branches", cut.Markup, StringComparison.OrdinalIgnoreCase);
        });

        await InvokeClipboardActionAsync(
            cut,
            canvasWorkbench,
            new CanvasWorkbenchClipboardRequest(
                CanvasWorkbenchClipboardAction.Paste,
                surfaceId,
                alternateDestinationNode.Id,
                [alternateDestinationNode.Id]));

        cut.WaitForAssertion(() =>
            Assert.Contains("clipboard is empty", cut.Markup, StringComparison.OrdinalIgnoreCase));
        persistedSurface = await workbenchService.GetStructureAsync(projectId);
        Assert.Equal(destinationNode.Id, Assert.Single(persistedSurface.Nodes, node => node.Id == sourceNode.Id).ParentId);
        Assert.Equal(destinationNode.Id, Assert.Single(persistedSurface.Nodes, node => node.Id == secondSourceNode.Id).ParentId);
    }

    [Fact]
    public async Task Clipboard_rejected_capture_clears_armed_cut_and_shows_feedback_when_selection_window_is_hidden()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();

        var projectId = await CreateProjectAsync(projectsService, "Clipboard rejected capture");
        var projectRootNodeId = $"project:{projectId}";
        var sourceNode = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                "Armed cut source",
                "Cut source",
                "A rejected replacement capture must disarm this cut buffer.",
                projectRootNodeId,
                520,
                180));
        var destinationNode = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                "Rejected capture destination",
                "Paste destination",
                "The stale cut buffer must not move a node here.",
                projectRootNodeId,
                1180,
                360));
        await workbenchService.SaveViewStateAsync(
            projectId,
            "structure",
            new CanvasWorkbenchUiState
            {
                WindowStates = new Dictionary<string, CanvasWorkbenchWindowState>(StringComparer.Ordinal)
                {
                    ["project-structure.selection"] = new CanvasWorkbenchWindowState { IsVisible = false }
                }
            }.ToJson());

        var cut = harness.Context.Render<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));
        var canvasWorkbench = WaitForCanvasWorkbench(cut);
        var surfaceId = canvasWorkbench.Instance.Surface.SurfaceId;

        await InvokeClipboardActionAsync(
            cut,
            canvasWorkbench,
            new CanvasWorkbenchClipboardRequest(
                CanvasWorkbenchClipboardAction.Cut,
                surfaceId,
                sourceNode.Id,
                [sourceNode.Id]));
        await InvokeClipboardActionAsync(
            cut,
            canvasWorkbench,
            new CanvasWorkbenchClipboardRequest(
                CanvasWorkbenchClipboardAction.Copy,
                surfaceId,
                projectRootNodeId,
                [projectRootNodeId]));
        await InvokeClipboardActionAsync(
            cut,
            canvasWorkbench,
            new CanvasWorkbenchClipboardRequest(
                CanvasWorkbenchClipboardAction.Paste,
                surfaceId,
                destinationNode.Id,
                [destinationNode.Id]));

        cut.WaitForAssertion(() =>
        {
            var feedback = cut.Find("[data-testid='project-structure-clipboard-feedback']");
            Assert.Contains("clipboard is empty", feedback.TextContent, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("project-structure-selection-window", cut.Markup, StringComparison.Ordinal);
        });

        var persistedSurface = await workbenchService.GetStructureAsync(projectId);
        Assert.Equal(projectRootNodeId, Assert.Single(persistedSurface.Nodes, node => node.Id == sourceNode.Id).ParentId);
    }

    [Fact]
    public async Task Clipboard_paste_rejects_projected_subproject_destination_before_mutation()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();

        var projectId = await CreateProjectAsync(projectsService, "Clipboard projected destination");
        var subprojectId = await CreateProjectAsync(projectsService, "Clipboard projected subproject");
        Assert.True((await projectsService.AddSubprojectAsync(projectId, subprojectId)).IsSuccess);
        var projectRootNodeId = $"project:{projectId}";
        var projectedSubprojectNodeId = $"project-child:{subprojectId}";
        var sourceNode = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Note,
                "Projection-safe source",
                "Copy source",
                "This node must remain in the active project.",
                projectRootNodeId,
                560,
                220));

        var cut = harness.Context.Render<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));
        var canvasWorkbench = WaitForCanvasWorkbench(cut);
        cut.WaitForAssertion(() =>
            Assert.Contains(
                canvasWorkbench.Instance.Surface.Nodes,
                node => string.Equals(node.Id, projectedSubprojectNodeId, StringComparison.Ordinal)));
        var surfaceId = canvasWorkbench.Instance.Surface.SurfaceId;

        await InvokeClipboardActionAsync(
            cut,
            canvasWorkbench,
            new CanvasWorkbenchClipboardRequest(
                CanvasWorkbenchClipboardAction.Copy,
                surfaceId,
                sourceNode.Id,
                [sourceNode.Id]));
        await InvokeClipboardActionAsync(
            cut,
            canvasWorkbench,
            new CanvasWorkbenchClipboardRequest(
                CanvasWorkbenchClipboardAction.Paste,
                surfaceId,
                projectedSubprojectNodeId,
                [projectedSubprojectNodeId]));

        cut.WaitForAssertion(() =>
            Assert.Contains("project transfer action", cut.Markup, StringComparison.OrdinalIgnoreCase));
        var persistedSurface = await workbenchService.GetStructureAsync(projectId);
        Assert.Single(
            persistedSurface.Nodes,
            node => string.Equals(node.Title, sourceNode.Title, StringComparison.Ordinal));
        Assert.Equal(projectRootNodeId, Assert.Single(persistedSurface.Nodes, node => node.Id == sourceNode.Id).ParentId);
    }

    private static Task InvokeClipboardActionAsync(
        IRenderedComponent<ProjectStructurePage> cut,
        IRenderedComponent<CanvasWorkbench> canvasWorkbench,
        CanvasWorkbenchClipboardRequest request)
        => cut.InvokeAsync(() => canvasWorkbench.Instance.OnClipboardAction(JsonSerializer.Serialize(request)));

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

    private static async Task<Guid> SaveImageProviderAsync(
        SecretService secretService,
        IAgentFrameworkWorkspaceService agentWorkspaceService,
        string name = "Component image provider")
    {
        var secretResult = await secretService.SaveAsync(new SecretEditorModel
        {
            Name = $"{name} key",
            Kind = SecretKind.ApiKey,
            SecretValue = "component-image-provider-key",
            Scope = "workspace"
        });
        Assert.True(secretResult.IsSuccess);

        return await agentWorkspaceService.SaveProviderAsync(new ProviderProfileEditorModel
        {
            Name = name,
            Kind = ProviderKind.OpenAi,
            BaseUrl = "https://api.openai.com/v1",
            ApiKeyEnvironmentVariable = $"secret:{secretResult.Value:D}",
            DefaultModel = "gpt-image-1-mini",
            Transport = ProviderTransportKind.Responses,
            Purpose = ProviderProfilePurpose.ImageGeneration,
            IsEnabled = true,
            SupportsStreaming = false,
            SupportsTools = false,
            SuggestedModels = ["gpt-image-1-mini"]
        });
    }

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

    private static async Task<CanvasWorkbenchAction> RefreshGeneratedImageCreateActionAsync(
        IRenderedComponent<ProjectStructurePage> cut,
        IRenderedComponent<CanvasWorkbench> canvasWorkbench)
    {
        var assetGroup = Assert.Single(
            canvasWorkbench.Instance.Surface.Chrome.QuickCreateActions,
            action => string.Equals(action.ActionId, "group-assets", StringComparison.Ordinal));
        var generatedImageAction = Assert.Single(
            assetGroup.Children,
            action => string.Equals(action.ActionId, "generate-image-asset", StringComparison.Ordinal));
        var refreshMethod = typeof(ProjectStructurePage).GetMethod(
            "RefreshGeneratedImageCreateActionAsync",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(refreshMethod);
        var refreshTask = Assert.IsAssignableFrom<Task<CanvasWorkbenchAction>>(
            refreshMethod!.Invoke(cut.Instance, [generatedImageAction]));
        return await refreshTask;
    }

    private static string ResolveComponentTestProjectPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CanDoItAll.slnx")))
            {
                return Path.Combine(
                    directory.FullName,
                    "tests",
                    "Components",
                    "CanDoItAll.Tests.Components",
                    "CanDoItAll.Tests.Components.csproj");
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root from the component test output directory.");
    }

    private static IElement FindButtonByLabel(
        IRenderedComponent<IComponent> cut,
        string label,
        string selector = "button")
        => cut.FindAll(selector)
            .First(button => button.TextContent.Contains(label, StringComparison.Ordinal));

    private static CanvasWorkbenchAction FindCreateAction(
        IReadOnlyList<CanvasWorkbenchAction> actions,
        string actionId)
        => TryFindCreateAction(actions, actionId)
           ?? throw new InvalidOperationException($"Create action '{actionId}' was not found.");

    private static void AssertDedicatedTextAssetAction(CanvasWorkbenchAction action)
    {
        Assert.False(action.RequiresInput);
        Assert.NotEqual("command", action.CreateMode);
        Assert.True(action.SupportsDragDrop);
    }

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

    private static IRenderedComponent<CanvasWorkbench> WaitForCanvasWorkbench(IRenderedComponent<IComponent> cut)
    {
        IRenderedComponent<CanvasWorkbench>? canvasWorkbench = null;
        cut.WaitForAssertion(() => canvasWorkbench = cut.FindComponent<CanvasWorkbench>());
        return canvasWorkbench ?? throw new InvalidOperationException("Canvas workbench did not render.");
    }

    private static CanvasWorkbenchUploadedFile BuildUploadedFile(string fileName, string contentType, string content)
        => BuildUploadedFile(fileName, contentType, Encoding.UTF8.GetBytes(content));

    private static CanvasWorkbenchUploadedFile BuildUploadedFile(string fileName, string contentType, byte[] content)
        => new()
        {
            FileName = fileName,
            ContentType = contentType,
            Base64Data = Convert.ToBase64String(content)
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
