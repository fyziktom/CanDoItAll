using System.Text.Json;
using AngleSharp.Dom;
using Bunit;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class ProjectStructurePageSimpleMutationTests
{
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
        var canvasWorkbench = cut.FindComponent<CanvasWorkbench>();

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
    public async Task Convert_note_to_block_patches_surface_without_structure_reload()
    {
        await using var harness = await ComponentTestHarness.CreateAsync(WrapDbContextFactoryWithCreateCounter);
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var createCounter = harness.Context.Services.GetRequiredService<DbContextCreateCounter>();

        var projectId = await CreateProjectAsync(projectsService, "Note conversion patch");
        const string noteBody = "Deploy edge gateway\r\nRemember the router and WiFi blocks";
        var noteNode = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Note,
                "Original note",
                string.Empty,
                noteBody,
                $"project:{projectId}",
                420,
                240));

        await SaveSelectedNodeStateAsync(workbenchService, projectId, noteNode.Id);

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));
        var canvasWorkbench = cut.FindComponent<CanvasWorkbench>();

        cut.WaitForAssertion(() => Assert.Contains("Convert to block", cut.Markup));
        createCounter.Reset();

        FindButtonByLabel(cut, "Convert to block", "[data-testid='project-structure-node-actions'] button").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("project-structure-block-mutation-dialog", cut.Markup);
        });

        cut.Find("[data-testid='project-structure-block-mutation-select']").Change("add-block-deployment");
        cut.Find("[data-testid='project-structure-block-mutation-submit']").Click();

        cut.WaitForAssertion(() =>
        {
            var updatedNode = Assert.Single(canvasWorkbench.Instance.Surface.Nodes, node => string.Equals(node.Id, noteNode.Id, StringComparison.Ordinal));
            Assert.Equal("Deploy edge gateway", updatedNode.Title);
            Assert.Equal("Deployment block", updatedNode.Kind);
            Assert.Equal(ProjectObjectPaletteKeys.Info, updatedNode.PaletteKey);
            Assert.False(updatedNode.IsInlineTextNode);
            Assert.DoesNotContain("project-structure-block-mutation-dialog", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("was converted to deployment block.", cut.Markup);
        });

        Assert.Equal(1, createCounter.CreateCount);

        var persistedSurface = await workbenchService.GetStructureAsync(projectId);
        var persistedNode = Assert.Single(persistedSurface.Nodes, node => string.Equals(node.Id, noteNode.Id, StringComparison.Ordinal));
        Assert.Equal(ProjectObjectType.ProjectBlock, persistedNode.ObjectType);
        Assert.Equal("deployment", persistedNode.ObjectSubtype);
        Assert.Equal("Deploy edge gateway", persistedNode.Title);
        Assert.Equal(noteBody, persistedNode.Notes);
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
        var canvasWorkbench = cut.FindComponent<CanvasWorkbench>();

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
        var canvasWorkbench = cut.FindComponent<CanvasWorkbench>();

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
        var canvasWorkbench = cut.FindComponent<CanvasWorkbench>();

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
        Assert.Equal(1, createCounter.CreateCount);

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
        Assert.Equal(1, createCounter.CreateCount);

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
        Assert.Equal(1, createCounter.CreateCount);

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
        Assert.Equal(1, createCounter.CreateCount);
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
        var canvasWorkbench = cut.FindComponent<CanvasWorkbench>();

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
        Assert.Equal(1, createCounter.CreateCount);

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
        Assert.Equal(1, createCounter.CreateCount);

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
        var canvasWorkbench = cut.FindComponent<CanvasWorkbench>();

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

    private static IElement FindButtonByLabel(
        IRenderedFragment cut,
        string label,
        string selector = "button")
        => cut.FindAll(selector)
            .First(button => button.TextContent.Contains(label, StringComparison.Ordinal));

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
