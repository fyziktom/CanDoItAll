using System.Text.Json;
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
    public async Task Inline_note_edit_patches_surface_without_structure_reload()
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

        await cut.InvokeAsync(() => canvasWorkbench.Instance.OnNodeEdited(JsonSerializer.Serialize(
            new CanvasWorkbenchNodeEditRequest(noteNode.Id, noteNode.Title, "Updated inline note body"))));

        cut.WaitForAssertion(() =>
        {
            var updatedNode = Assert.Single(canvasWorkbench.Instance.Surface.Nodes, node => string.Equals(node.Id, noteNode.Id, StringComparison.Ordinal));
            Assert.Equal("Updated inline note body", updatedNode.InlineText);
        });

        Assert.Equal(1, createCounter.CreateCount);

        var persistedSurface = await workbenchService.GetStructureAsync(projectId);
        var persistedNode = Assert.Single(persistedSurface.Nodes, node => string.Equals(node.Id, noteNode.Id, StringComparison.Ordinal));
        Assert.Equal("Updated inline note body", persistedNode.Notes);
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
