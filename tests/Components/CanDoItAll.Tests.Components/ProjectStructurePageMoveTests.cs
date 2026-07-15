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

public sealed class ProjectStructurePageMoveTests
{
    [Fact]
    public async Task Move_objects_async_batches_multi_node_persistence_into_one_save_transaction()
    {
        await using var harness = await ComponentTestHarness.CreateAsync(WrapDbContextFactoryWithSaveCounter);
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var saveCounter = harness.Context.Services.GetRequiredService<SaveChangesCounter>();

        var projectId = await CreateProjectAsync(projectsService, "Batched move persistence");
        var firstNode = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Note,
                "First move target",
                "Batch move",
                "Move these nodes together.",
                $"project:{projectId}",
                220,
                180));
        var secondNode = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Note,
                "Second move target",
                "Batch move",
                "Move these nodes together.",
                $"project:{projectId}",
                340,
                260));

        saveCounter.Reset();

        var updatedNodeIds = await workbenchService.MoveObjectsAsync(
            projectId,
            [
                new ProjectNodeMoveRequest(firstNode.Id, 720, 280),
                new ProjectNodeMoveRequest(secondNode.Id, 860, 360)
            ]);

        Assert.Equal(1, saveCounter.SaveChangesCount);
        Assert.Equal(2, updatedNodeIds.Count);
        Assert.Contains(firstNode.Id, updatedNodeIds);
        Assert.Contains(secondNode.Id, updatedNodeIds);

        var surface = await workbenchService.GetStructureAsync(projectId);
        var reloadedFirstNode = Assert.Single(surface.Nodes, node => string.Equals(node.Id, firstNode.Id, StringComparison.Ordinal));
        var reloadedSecondNode = Assert.Single(surface.Nodes, node => string.Equals(node.Id, secondNode.Id, StringComparison.Ordinal));

        Assert.Equal(720d, reloadedFirstNode.X);
        Assert.Equal(280d, reloadedFirstNode.Y);
        Assert.Equal(860d, reloadedSecondNode.X);
        Assert.Equal(360d, reloadedSecondNode.Y);
    }

    [Fact]
    public async Task Nodes_moved_callback_keeps_multi_selection_and_adopts_nodes_into_existing_border()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();

        var projectId = await CreateProjectAsync(projectsService, "Selection and border move");
        var leftAnchor = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                "Delivery anchor",
                "Border anchor",
                "Keeps the frame bounds stable.",
                $"project:{projectId}",
                420,
                220,
                null,
                null,
                "feature"));
        var rightAnchor = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                "Evidence anchor",
                "Border anchor",
                "Keeps the frame bounds stable.",
                $"project:{projectId}",
                760,
                220,
                null,
                null,
                "support"));
        var movedTask = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.WorkItem,
                "Capture screenshots",
                "Selected node",
                "Should stay selected after drop.",
                $"project:{projectId}",
                120,
                120,
                null,
                null,
                "task"));
        var movedEvidence = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.TestEvidence,
                "Store proof",
                "Selected node",
                "Should join the border after drop.",
                $"project:{projectId}",
                160,
                360));

        await workbenchService.SaveViewStateAsync(
            projectId,
            "structure",
            new CanvasWorkbenchUiState
            {
                SelectedNodeIds = [movedTask.Id, movedEvidence.Id],
                GroupFrames =
                [
                    new CanvasWorkbenchGroupFrame
                    {
                        Id = "delivery-swimlane",
                        Label = "Delivery swimlane",
                        Tone = "accent",
                        AnchorNodeIds = [leftAnchor.Id, rightAnchor.Id]
                    }
                ],
                WindowStates = new Dictionary<string, CanvasWorkbenchWindowState>(StringComparer.Ordinal)
                {
                    ["project-structure.selection"] = new CanvasWorkbenchWindowState { IsVisible = true }
                }
            }.ToJson());

        var cut = harness.Context.RenderComponent<ProjectStructurePage>(
            parameters => parameters.Add(page => page.ProjectId, projectId));

        cut.WaitForAssertion(() =>
        {
            var workbench = cut.FindComponent<CanvasWorkbench>();
            Assert.Equal(2, workbench.Instance.Surface.UiState.SelectedNodeIds.Count);
            Assert.Contains(movedTask.Id, workbench.Instance.Surface.UiState.SelectedNodeIds);
            Assert.Contains(movedEvidence.Id, workbench.Instance.Surface.UiState.SelectedNodeIds);
            Assert.Contains("2 nodes selected", cut.Markup);
            Assert.False(workbench.Instance.Surface.Chrome.TransformHandles.IsEnabled);
        });

        await cut.InvokeAsync(() => cut.FindComponent<CanvasWorkbench>().Instance.OnNodesMoved(JsonSerializer.Serialize<IReadOnlyList<CanvasWorkbenchNodePositionChange>>(
        [
            new CanvasWorkbenchNodePositionChange(movedTask.Id, 580, 220),
            new CanvasWorkbenchNodePositionChange(movedEvidence.Id, 620, 260)
        ])));

        cut.WaitForAssertion(() =>
        {
            var workbench = cut.FindComponent<CanvasWorkbench>();
            Assert.Equal(2, workbench.Instance.Surface.UiState.SelectedNodeIds.Count);
            Assert.Contains(movedTask.Id, workbench.Instance.Surface.UiState.SelectedNodeIds);
            Assert.Contains(movedEvidence.Id, workbench.Instance.Surface.UiState.SelectedNodeIds);
            Assert.Contains("2 nodes selected", cut.Markup);
            Assert.False(workbench.Instance.Surface.Chrome.TransformHandles.IsEnabled);
        });

        var reloadedSurface = await workbenchService.GetStructureAsync(projectId);
        var movedTaskNode = Assert.Single(reloadedSurface.Nodes, node => string.Equals(node.Id, movedTask.Id, StringComparison.Ordinal));
        var movedEvidenceNode = Assert.Single(reloadedSurface.Nodes, node => string.Equals(node.Id, movedEvidence.Id, StringComparison.Ordinal));
        var persistedUiState = CanvasWorkbenchUiState.Parse(reloadedSurface.ViewStateJson);
        var frame = Assert.Single(persistedUiState.GroupFrames);

        Assert.Equal(580d, movedTaskNode.X);
        Assert.Equal(220d, movedTaskNode.Y);
        Assert.Equal(620d, movedEvidenceNode.X);
        Assert.Equal(260d, movedEvidenceNode.Y);
        Assert.Contains(movedTask.Id, frame.AnchorNodeIds);
        Assert.Contains(movedEvidence.Id, frame.AnchorNodeIds);

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

    private static void WrapDbContextFactoryWithSaveCounter(IServiceCollection services)
    {
        services.AddSingleton<SaveChangesCounter>();

        var factoryDescriptor = services.Last(descriptor => descriptor.ServiceType == typeof(IDbContextFactory<AppDbContext>));
        services.Remove(factoryDescriptor);
        services.Add(new ServiceDescriptor(
            typeof(IDbContextFactory<AppDbContext>),
            serviceProvider =>
            {
                var innerFactory = (IDbContextFactory<AppDbContext>)CreateService(serviceProvider, factoryDescriptor);
                var counter = serviceProvider.GetRequiredService<SaveChangesCounter>();
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
        SaveChangesCounter counter) : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext()
        {
            var dbContext = innerFactory.CreateDbContext();
            AttachSaveCounter(dbContext);
            return dbContext;
        }

        public async Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            var dbContext = await innerFactory.CreateDbContextAsync(cancellationToken);
            AttachSaveCounter(dbContext);
            return dbContext;
        }

        private void AttachSaveCounter(AppDbContext dbContext)
        {
            dbContext.SavedChanges += (_, _) => counter.Increment();
        }
    }

    private sealed class SaveChangesCounter
    {
        private int saveChangesCount;

        public int SaveChangesCount => saveChangesCount;

        public void Increment()
        {
            Interlocked.Increment(ref saveChangesCount);
        }

        public void Reset()
        {
            Interlocked.Exchange(ref saveChangesCount, 0);
        }
    }
}
