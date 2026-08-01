using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Components;

public sealed class ProjectStructureTaskResourceGraphPersistenceTests
{
    private static readonly DateTimeOffset CalculatedAtUtc =
        DateTimeOffset.Parse("2026-07-23T18:30:00Z");

    [Fact]
    public async Task Unlinking_the_matching_process_resource_clears_not_started_authoritative_pricing()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var services = harness.Context.Services;
        var projectsService = services.GetRequiredService<ProjectsService>();
        var workbenchService = services.GetRequiredService<ProjectWorkbenchService>();
        var resourceService = services.GetRequiredService<ProjectStructureTaskResourceService>();
        var relationService = services.GetRequiredService<ProjectWorkbenchRelationService>();
        var projectId = await CreateProjectAsync(projectsService);
        var task = await CreateTaskAsync(workbenchService, projectId, "Process unlink task");
        var process = (await resourceService.ListOptionsAsync(projectId))
            .First(option => option.Kind == ProjectStructureTaskResourceKind.Process);
        var resource = new ProjectStructureTaskResourceSelection(
            process.Kind,
            process.ResourceId);
        var attachment = await resourceService.AttachAsync(
            projectId,
            task.Id,
            resource,
            CreateAgent(projectId));
        Assert.False(string.IsNullOrWhiteSpace(attachment.LinkTargetNodeId));
        await SetAuthoritativePricingAsync(workbenchService, projectId, task.Id, resource);

        var unlinked = await relationService.UnlinkObjectsAsync(
            projectId,
            task.Id,
            attachment.LinkTargetNodeId!,
            ProjectObjectLinkKind.Uses);

        Assert.True(unlinked);
        var surface = await workbenchService.GetStructureAsync(projectId);
        Assert.DoesNotContain(surface.Links, link =>
            link.SourceId == task.Id &&
            link.TargetId == attachment.LinkTargetNodeId &&
            link.Kind == ProjectObjectLinkKind.Uses);
        AssertAuthoritativePricingCleared(ReadWorkItem(surface, task.Id));
    }

    [Fact]
    public async Task Unlinking_another_process_preserves_the_still_attached_authoritative_resource()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var services = harness.Context.Services;
        var projectsService = services.GetRequiredService<ProjectsService>();
        var workbenchService = services.GetRequiredService<ProjectWorkbenchService>();
        var resourceService = services.GetRequiredService<ProjectStructureTaskResourceService>();
        var relationService = services.GetRequiredService<ProjectWorkbenchRelationService>();
        var projectId = await CreateProjectAsync(projectsService);
        var task = await CreateTaskAsync(workbenchService, projectId, "Nonmatching process unlink task");
        var processes = (await resourceService.ListOptionsAsync(projectId))
            .Where(option => option.Kind == ProjectStructureTaskResourceKind.Process)
            .Take(2)
            .ToArray();
        Assert.Equal(2, processes.Length);
        var detachedResource = new ProjectStructureTaskResourceSelection(
            processes[0].Kind,
            processes[0].ResourceId);
        var authoritativeResource = new ProjectStructureTaskResourceSelection(
            processes[1].Kind,
            processes[1].ResourceId);
        var detachedAttachment = await resourceService.AttachAsync(
            projectId,
            task.Id,
            detachedResource,
            CreateAgent(projectId, "detached"));
        var authoritativeAttachment = await resourceService.AttachAsync(
            projectId,
            task.Id,
            authoritativeResource,
            CreateAgent(projectId, "authoritative"));
        Assert.False(string.IsNullOrWhiteSpace(detachedAttachment.LinkTargetNodeId));
        Assert.False(string.IsNullOrWhiteSpace(authoritativeAttachment.LinkTargetNodeId));
        await SetAuthoritativePricingAsync(
            workbenchService,
            projectId,
            task.Id,
            authoritativeResource);

        var unlinked = await relationService.UnlinkObjectsAsync(
            projectId,
            task.Id,
            detachedAttachment.LinkTargetNodeId!,
            ProjectObjectLinkKind.Uses);

        Assert.True(unlinked);
        var surface = await workbenchService.GetStructureAsync(projectId);
        Assert.Contains(surface.Links, link =>
            link.SourceId == task.Id &&
            link.TargetId == authoritativeAttachment.LinkTargetNodeId &&
            link.Kind == ProjectObjectLinkKind.Uses);
        AssertAuthoritativePricing(
            ReadWorkItem(surface, task.Id),
            authoritativeResource);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Reparenting_an_attached_workflow_clears_its_previous_task_pricing(
        bool moveSubtree)
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var services = harness.Context.Services;
        var projectsService = services.GetRequiredService<ProjectsService>();
        var workflowCatalogService = services.GetRequiredService<IWorkflowCatalogService>();
        var workbenchService = services.GetRequiredService<ProjectWorkbenchService>();
        var resourceService = services.GetRequiredService<ProjectStructureTaskResourceService>();
        var relationService = services.GetRequiredService<ProjectWorkbenchRelationService>();
        var projectId = await CreateProjectAsync(projectsService);
        var task = await CreateTaskAsync(workbenchService, projectId, "Workflow reparent task");
        var workflow = await CreateWorkflowAsync(
            workflowCatalogService,
            $"Reparent workflow {Guid.NewGuid():N}");
        var resource = new ProjectStructureTaskResourceSelection(
            ProjectStructureTaskResourceKind.Workflow,
            workflow.Id.Value,
            workflow.VersionId.Value);
        var attachment = await resourceService.AttachAsync(
            projectId,
            task.Id,
            resource,
            CreateAgent(projectId));
        Assert.False(string.IsNullOrWhiteSpace(attachment.CreatedNodeId));
        await SetAuthoritativePricingAsync(workbenchService, projectId, task.Id, resource);
        var projectRootNodeId = $"project:{projectId}";

        if (moveSubtree)
        {
            var moved = await relationService.ReparentSubtreesAsync(
                projectId,
                [attachment.CreatedNodeId!],
                projectRootNodeId);
            Assert.Single(moved);
        }
        else
        {
            var moved = await relationService.ReparentObjectAsync(
                projectId,
                attachment.CreatedNodeId!,
                projectRootNodeId);
            Assert.NotNull(moved);
        }

        var surface = await workbenchService.GetStructureAsync(projectId);
        var workflowNode = surface.Nodes.Single(node => node.Id == attachment.CreatedNodeId);
        Assert.Equal(projectRootNodeId, workflowNode.ParentId);
        AssertAuthoritativePricingCleared(ReadWorkItem(surface, task.Id));
    }

    [Fact]
    public async Task Deleting_an_attached_workflow_clears_its_previous_task_pricing()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var services = harness.Context.Services;
        var projectsService = services.GetRequiredService<ProjectsService>();
        var workflowCatalogService = services.GetRequiredService<IWorkflowCatalogService>();
        var workbenchService = services.GetRequiredService<ProjectWorkbenchService>();
        var resourceService = services.GetRequiredService<ProjectStructureTaskResourceService>();
        var mutationService = services.GetRequiredService<ProjectWorkbenchCrossModuleMutationService>();
        var projectId = await CreateProjectAsync(projectsService);
        var task = await CreateTaskAsync(workbenchService, projectId, "Workflow delete task");
        var workflow = await CreateWorkflowAsync(
            workflowCatalogService,
            $"Delete workflow {Guid.NewGuid():N}");
        var resource = new ProjectStructureTaskResourceSelection(
            ProjectStructureTaskResourceKind.Workflow,
            workflow.Id.Value,
            workflow.VersionId.Value);
        var attachment = await resourceService.AttachAsync(
            projectId,
            task.Id,
            resource,
            CreateAgent(projectId));
        Assert.False(string.IsNullOrWhiteSpace(attachment.CreatedNodeId));
        await SetAuthoritativePricingAsync(workbenchService, projectId, task.Id, resource);

        var deletedCount = await mutationService.DeleteObjectAsync(
            projectId,
            attachment.CreatedNodeId!);

        Assert.True(deletedCount > 0);
        var surface = await workbenchService.GetStructureAsync(projectId);
        Assert.DoesNotContain(surface.Nodes, node => node.Id == attachment.CreatedNodeId);
        AssertAuthoritativePricingCleared(ReadWorkItem(surface, task.Id));
    }

    [Fact]
    public async Task Hiding_an_attached_projected_process_clears_not_started_authoritative_pricing()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var services = harness.Context.Services;
        var projectsService = services.GetRequiredService<ProjectsService>();
        var workbenchService = services.GetRequiredService<ProjectWorkbenchService>();
        var resourceService = services.GetRequiredService<ProjectStructureTaskResourceService>();
        var mutationService = services.GetRequiredService<ProjectWorkbenchCrossModuleMutationService>();
        var projectId = await CreateProjectAsync(projectsService);
        var task = await CreateTaskAsync(workbenchService, projectId, "Projected process hide task");
        var process = (await resourceService.ListOptionsAsync(projectId))
            .First(option => option.Kind == ProjectStructureTaskResourceKind.Process);
        var resource = new ProjectStructureTaskResourceSelection(
            process.Kind,
            process.ResourceId);
        var attachment = await resourceService.AttachAsync(
            projectId,
            task.Id,
            resource,
            CreateAgent(projectId));
        Assert.False(string.IsNullOrWhiteSpace(attachment.LinkTargetNodeId));
        await SetAuthoritativePricingAsync(workbenchService, projectId, task.Id, resource);

        var hiddenCount = await mutationService.DeleteObjectAsync(
            projectId,
            attachment.LinkTargetNodeId!);

        Assert.True(hiddenCount > 0);
        var surface = await workbenchService.GetStructureAsync(projectId);
        Assert.DoesNotContain(surface.Links, link =>
            link.SourceId == task.Id &&
            link.TargetId == attachment.LinkTargetNodeId &&
            link.Kind == ProjectObjectLinkKind.Uses);
        AssertAuthoritativePricingCleared(ReadWorkItem(surface, task.Id));
    }

    [Fact]
    public async Task Concurrent_pricing_commit_and_unlink_leave_a_consistent_resource_price_pair()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var services = harness.Context.Services;
        var projectsService = services.GetRequiredService<ProjectsService>();
        var workbenchService = services.GetRequiredService<ProjectWorkbenchService>();
        var resourceService = services.GetRequiredService<ProjectStructureTaskResourceService>();
        var relationService = services.GetRequiredService<ProjectWorkbenchRelationService>();
        var projectId = await CreateProjectAsync(projectsService);
        var task = await CreateTaskAsync(workbenchService, projectId, "Concurrent pricing unlink task");
        var process = (await resourceService.ListOptionsAsync(projectId))
            .First(option => option.Kind == ProjectStructureTaskResourceKind.Process);
        var resource = new ProjectStructureTaskResourceSelection(
            process.Kind,
            process.ResourceId);
        var attachment = await resourceService.AttachAsync(
            projectId,
            task.Id,
            resource,
            CreateAgent(projectId));
        Assert.False(string.IsNullOrWhiteSpace(attachment.LinkTargetNodeId));
        var pricingCommitService = new ProjectStructureTaskPricingCommitService(
            workbenchService,
            new ProjectStructureTaskEstimateRefreshService(
                new ProjectStructureTaskResourceCostService(
                [
                    new FixedProcessQuoteStrategy()
                ])),
            services.GetRequiredService<ProjectStructureTaskPricingPersistenceService>(),
            NullLogger<ProjectStructureTaskPricingCommitService>.Instance);
        var plan = await pricingCommitService.PrepareAfterTransitionAsync(
            projectId,
            task.Id,
            resource,
            ProjectTaskExecutionSnapshot.NotStarted,
            ProjectTaskExecutionSnapshot.NotStarted);
        var start = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var commitTask = CaptureFailureAfterAsync(
            start.Task,
            async () => await pricingCommitService.CommitAsync(plan));
        var unlinkTask = CaptureFailureAfterAsync(
            start.Task,
            async () => await relationService.UnlinkObjectsAsync(
                projectId,
                task.Id,
                attachment.LinkTargetNodeId!,
                ProjectObjectLinkKind.Uses));

        start.SetResult();
        var failures = (await Task.WhenAll(commitTask, unlinkTask))
            .Where(static failure => failure is not null)
            .ToArray();

        Assert.True(failures.Length <= 1);
        var surface = await workbenchService.GetStructureAsync(projectId);
        var attached = surface.Links.Any(link =>
            link.SourceId == task.Id &&
            link.TargetId == attachment.LinkTargetNodeId &&
            link.Kind == ProjectObjectLinkKind.Uses);
        var metadata = ReadWorkItem(surface, task.Id);
        if (attached)
        {
            AssertAuthoritativePricing(metadata, resource, expectedAmount: 50m);
        }
        else
        {
            AssertAuthoritativePricingCleared(metadata);
        }
    }

    private static async Task<Guid> CreateProjectAsync(ProjectsService projectsService)
    {
        var result = await projectsService.SaveAsync(new ProjectEditorModel
        {
            Name = $"Task resource graph {Guid.NewGuid():N}",
            Description = "Task resource graph pricing invariant proof.",
            Objective = "Keep authoritative pricing symmetric with graph attachment state.",
            CurrentPhase = "Delivery"
        });
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static Task<ProjectStructureNode> CreateTaskAsync(
        ProjectWorkbenchService workbenchService,
        Guid projectId,
        string title)
    {
        return workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.WorkItem,
                title,
                string.Empty,
                string.Empty,
                $"project:{projectId}",
                420,
                260,
                ObjectSubtype: "task"));
    }

    private static Task<ProjectStructureNode?> SetAuthoritativePricingAsync(
        ProjectWorkbenchService workbenchService,
        Guid projectId,
        string taskNodeId,
        ProjectStructureTaskResourceSelection resource)
    {
        return workbenchService.MutateObjectMetadataSerializableAsync(
            projectId,
            taskNodeId,
            metadata =>
            {
                metadata.WorkItem ??= new ProjectWorkItemMetadata
                {
                    WorkItemKind = ProjectWorkItemKind.Task
                };
                metadata.WorkItem.ExecutionState = ProjectTaskExecutionState.NotStarted;
                metadata.WorkItem.ExpectedCostAmount = 125m;
                metadata.WorkItem.ExpectedCostCurrencyCode = "USD";
                metadata.WorkItem.ExpectedCostBasis = new ProjectTaskExpectedCostBasis
                {
                    ResourceKind = resource.Kind,
                    ResourceId = resource.ResourceId,
                    ResourceVersionId = resource.VersionId,
                    Source = ProjectStructureTaskResourceCostSourcePolicy.RequireFor(resource.Kind),
                    CalculatedAtUtc = CalculatedAtUtc
                };
            });
    }

    private static ProjectWorkItemMetadata ReadWorkItem(
        ProjectStructureSurface surface,
        string taskNodeId)
    {
        var task = surface.Nodes.Single(node => node.Id == taskNodeId);
        return Assert.IsType<ProjectWorkItemMetadata>(
            ProjectObjectMetadataSerializer.Parse(task.MetadataJson).WorkItem);
    }

    private static void AssertAuthoritativePricingCleared(ProjectWorkItemMetadata metadata)
    {
        Assert.Null(metadata.ExpectedCostAmount);
        Assert.Empty(metadata.ExpectedCostCurrencyCode);
        Assert.Null(metadata.ExpectedCostBasis);
    }

    private static void AssertAuthoritativePricing(
        ProjectWorkItemMetadata metadata,
        ProjectStructureTaskResourceSelection resource,
        decimal expectedAmount = 125m)
    {
        Assert.Equal(expectedAmount, metadata.ExpectedCostAmount);
        Assert.Equal("USD", metadata.ExpectedCostCurrencyCode);
        Assert.Equal(resource.Kind, metadata.ExpectedCostBasis?.ResourceKind);
        Assert.Equal(resource.ResourceId, metadata.ExpectedCostBasis?.ResourceId);
        Assert.Equal(resource.VersionId, metadata.ExpectedCostBasis?.ResourceVersionId);
    }

    private static Task<WorkflowDefinition> CreateWorkflowAsync(
        IWorkflowCatalogService workflowCatalogService,
        string name)
    {
        var start = new WorkflowNodeId("start");
        var end = new WorkflowNodeId("end");
        return workflowCatalogService.SaveDefinitionAsync(new WorkflowDefinitionSaveRequest(
            Id: null,
            ExpectedVersionId: null,
            name,
            $"{name} description",
            WorkflowLifecycleStatus.Active,
            new WorkflowGraph(
                start,
                [
                    CreateWorkflowNode(start, WorkflowNodeKind.Start),
                    CreateWorkflowNode(end, WorkflowNodeKind.End)
                ],
                [
                    new WorkflowEdge(
                        new WorkflowEdgeId("start-to-end"),
                        start,
                        SourcePortId: null,
                        end,
                        TargetPortId: null,
                        WorkflowEdgeKind.Direct,
                        ConditionExpression: string.Empty)
                ]),
            new WorkflowRuntimePolicy(
                WorkflowRuntimeBackendKind.InProcess,
                AllowInProcessPreviewRuns: true,
                RequireDurableProductionRuns: false,
                ExposeAzureFunctionsStatusEndpoint: false,
                ExposeAzureFunctionsMcpTool: false)));
    }

    private static WorkflowNode CreateWorkflowNode(
        WorkflowNodeId id,
        WorkflowNodeKind kind)
    {
        return new WorkflowNode(
            id,
            kind,
            id.Value,
            [],
            new WorkflowNodeSettings(
                ComponentId: null,
                AgentId: null,
                SubworkflowId: null,
                ExternalRequestKind: null,
                Instructions: string.Empty,
                InputShape: WorkflowValueShape.Text,
                ResultShape: WorkflowValueShape.Text));
    }

    private static ProjectStructureAgentContext CreateAgent(
        Guid projectId,
        string owner = "default")
    {
        return new ProjectStructureAgentContext(
            $"component-tests-resource-graph-{owner}",
            "Component tests",
            Environment.MachineName,
            AppContext.BaseDirectory,
            string.Empty,
            $"{projectId:D}-resource-graph-{owner}");
    }

    private static async Task<Exception?> CaptureFailureAfterAsync(
        Task start,
        Func<Task> operation)
    {
        await start;
        try
        {
            await operation();
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private sealed class FixedProcessQuoteStrategy : IProjectStructureTaskResourceCostStrategy
    {
        public ProjectStructureTaskResourceKind Kind =>
            ProjectStructureTaskResourceKind.Process;

        public Task<ProjectStructureTaskResourceCostQuote> GetQuoteAsync(
            ProjectStructureTaskResourceCostRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ProjectStructureTaskResourceCostQuote(
                ProjectStructureTaskResourceCostQuoteStatus.Available,
                50m,
                "USD",
                "Process run history",
                "Fixed process price for a serialization race.",
                CalculatedAtUtc,
                ProjectStructureTaskResourceCostSource.ProcessRunHistory));
        }
    }
}
