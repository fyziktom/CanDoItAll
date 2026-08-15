using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Components.ProjectStructure;

public sealed class ProjectStructureTaskResourceAttachmentServiceTests
{
    [Fact]
    public async Task Pricing_conflict_rolls_back_attachment_and_preserves_concurrent_task_change()
    {
        var strategy = new MutatingProcessQuoteStrategy();
        await using var harness = await ComponentTestHarness.CreateAsync(services =>
        {
            services.RemoveAll<IProjectStructureTaskResourceCostStrategy>();
            services.AddSingleton<IProjectStructureTaskResourceCostStrategy>(strategy);
        });
        var services = harness.Context.Services;
        var projectsService = services.GetRequiredService<ProjectsService>();
        var workbenchService = services.GetRequiredService<ProjectWorkbenchService>();
        var resourceService = services.GetRequiredService<ProjectStructureTaskResourceService>();
        var attachmentService = services.GetRequiredService<ProjectStructureTaskResourceAttachmentService>();
        var projectId = await CreateProjectAsync(projectsService);
        var processes = (await resourceService.ListOptionsAsync(projectId))
            .Where(option => option.Kind == ProjectStructureTaskResourceKind.Process)
            .Take(2)
            .ToArray();
        Assert.Equal(2, processes.Length);
        var existingProcess = processes[0];
        var process = processes[1];
        var initialEstimate = new ProjectTaskEstimate(
            8m,
            ProjectWorkItemEffortUnit.Hours,
            999m,
            "EUR");
        var task = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.WorkItem,
                "Attachment rollback task",
                string.Empty,
                string.Empty,
                $"project:{projectId}",
                420,
                260,
                ObjectSubtype: "task",
                MetadataJson: ProjectObjectMetadataSerializer.Serialize(
                    new ProjectObjectMetadataEnvelope
                    {
                        WorkItem = new ProjectWorkItemMetadata
                        {
                            WorkItemKind = ProjectWorkItemKind.Task,
                            ExecutionState = ProjectTaskExecutionState.NotStarted,
                            ExpectedEffortHours = initialEstimate.ExpectedEffortHours,
                            ExpectedEffortUnit = initialEstimate.ExpectedEffortUnit,
                            ExpectedCostAmount = initialEstimate.ExpectedCostAmount,
                            ExpectedCostCurrencyCode = initialEstimate.ExpectedCostCurrencyCode
                        }
                    })));
        var existingSelection = new ProjectStructureTaskResourceSelection(
            ProjectStructureTaskResourceKind.Process,
            existingProcess.ResourceId);
        var existingAttachment = await resourceService.AttachAsync(
            projectId,
            task.Id,
            existingSelection,
            CreateAgent(projectId));
        var existingCostBasis = new ProjectTaskExpectedCostBasis
        {
            ResourceKind = ProjectStructureTaskResourceKind.Process,
            ResourceId = existingProcess.ResourceId,
            Source = ProjectStructureTaskResourceCostSource.ProcessRunHistory,
            CalculatedAtUtc = DateTimeOffset.Parse("2026-07-23T17:00:00Z")
        };
        await workbenchService.MutateObjectMetadataSerializableAsync(
            projectId,
            task.Id,
            metadata =>
            {
                Assert.NotNull(metadata.WorkItem);
                metadata.WorkItem!.ExpectedCostBasis = existingCostBasis;
            });
        strategy.BeforeQuoteReturnedAsync = () => workbenchService.MutateObjectMetadataSerializableAsync(
            projectId,
            task.Id,
            metadata =>
            {
                Assert.NotNull(metadata.WorkItem);
                metadata.WorkItem!.ExpectedEffortHours = 4m;
            });
        var selection = new ProjectStructureTaskResourceSelection(
            ProjectStructureTaskResourceKind.Process,
            process.ResourceId);

        var exception = await Assert.ThrowsAsync<ProjectStructureAgentException>(() =>
            attachmentService.AttachAsync(
                projectId,
                task.Id,
                new ProjectStructureTaskResourceAttachRequest(
                    selection,
                    ProjectTaskExecutionSnapshot.NotStarted),
                CreateAgent(projectId)));

        Assert.Equal(409, exception.StatusCode);
        Assert.Equal("TaskResourceAttachmentPricingConflict", exception.ErrorCode);
        Assert.Equal(1, strategy.CallCount);
        await resourceService.DetachAsync(
            projectId,
            task.Id,
            new ProjectStructureTaskResourceAttachment(
                ProjectStructureTaskResourceKind.Process,
                CreatedNodeId: null,
                $"process-definition:{process.ResourceId:D}"),
            CreateAgent(projectId));
        var surface = await workbenchService.GetStructureAsync(projectId);
        var resourceLink = Assert.Single(surface.Links, link =>
            link.SourceId == task.Id &&
            link.Kind == ProjectObjectLinkKind.Uses);
        Assert.Equal(existingAttachment.LinkTargetNodeId, resourceLink.TargetId);
        var persistedTask = surface.Nodes.Single(node => node.Id == task.Id);
        var metadata = ProjectObjectMetadataSerializer.Parse(persistedTask.MetadataJson).WorkItem;
        Assert.NotNull(metadata);
        Assert.Equal(4m, metadata!.ExpectedEffortHours);
        Assert.Equal(999m, metadata.ExpectedCostAmount);
        Assert.Equal("EUR", metadata.ExpectedCostCurrencyCode);
        Assert.Equal(existingCostBasis, metadata.ExpectedCostBasis);
    }

    [Fact]
    public async Task Pricing_commit_rejects_a_resource_that_is_no_longer_attached()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var services = harness.Context.Services;
        var projectsService = services.GetRequiredService<ProjectsService>();
        var workbenchService = services.GetRequiredService<ProjectWorkbenchService>();
        var pricingCommitService = services.GetRequiredService<ProjectStructureTaskPricingCommitService>();
        var projectId = await CreateProjectAsync(projectsService);
        var task = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.WorkItem,
                "Detached pricing task",
                string.Empty,
                string.Empty,
                $"project:{projectId}",
                420,
                260,
                ObjectSubtype: "task"));
        var resource = new ProjectStructureTaskResourceSelection(
            ProjectStructureTaskResourceKind.Process,
            Guid.Parse("70000000-0000-0000-0000-000000000017"));
        var pricing = new ProjectStructureTaskEstimateRefreshResult(
            ProjectTaskEstimate.Empty(),
            ProjectStructureTaskEstimateRefreshStatus.Preserved,
            ProjectStructureTaskEstimateRefreshReason.ExecutionStateDoesNotAllowRefresh,
            resource,
            Quote: null,
            CalculatedCostBasis: null,
            ReplacesCostBasis: false);
        var plan = new ProjectStructureTaskPricingCommitPlan(
            projectId,
            task.Id,
            resource,
            ProjectTaskExecutionSnapshot.NotStarted,
            ProjectTaskEstimate.Empty(),
            ExpectedCostBasis: null,
            pricing);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            pricingCommitService.CommitAsync(plan));

        Assert.Contains("no longer attached", exception.Message, StringComparison.OrdinalIgnoreCase);
        var persistedTask = (await workbenchService.GetStructureAsync(projectId))
            .Nodes
            .Single(node => node.Id == task.Id);
        var metadata = ProjectObjectMetadataSerializer.Parse(persistedTask.MetadataJson).WorkItem;
        Assert.NotNull(metadata);
        Assert.Equal(ProjectTaskEstimate.Empty(), new ProjectTaskEstimate(
            metadata!.ExpectedEffortHours,
            metadata.ExpectedEffortUnit,
            metadata.ExpectedCostAmount,
            metadata.ExpectedCostCurrencyCode));
        Assert.Null(metadata.ExpectedCostBasis);
    }

    private static async Task<Guid> CreateProjectAsync(ProjectsService projectsService)
    {
        var result = await projectsService.SaveAsync(new ProjectEditorModel
        {
            Name = $"Task resource attachment {Guid.NewGuid():N}",
            Description = "Task resource attachment orchestration proof.",
            Objective = "Rollback resource links when pricing loses its concurrency race.",
            CurrentPhase = "Delivery"
        });
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static ProjectStructureAgentContext CreateAgent(Guid projectId)
        => new(
            "component-tests-resource-attachment",
            "Component tests",
            Environment.MachineName,
            AppContext.BaseDirectory,
            string.Empty,
            $"{projectId:D}-resource-attachment");

    private sealed class MutatingProcessQuoteStrategy : IProjectStructureTaskResourceCostStrategy
    {
        public ProjectStructureTaskResourceKind Kind => ProjectStructureTaskResourceKind.Process;

        public int CallCount { get; private set; }

        public Func<Task>? BeforeQuoteReturnedAsync { get; set; }

        public async Task<ProjectStructureTaskResourceCostQuote> GetQuoteAsync(
            ProjectStructureTaskResourceCostRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            if (BeforeQuoteReturnedAsync is not null)
            {
                await BeforeQuoteReturnedAsync();
            }

            return new ProjectStructureTaskResourceCostQuote(
                ProjectStructureTaskResourceCostQuoteStatus.Available,
                50m,
                "USD",
                "Process run history",
                "Calculated from process run history.",
                DateTimeOffset.Parse("2026-07-23T17:30:00Z"),
                ProjectStructureTaskResourceCostSource.ProcessRunHistory);
        }
    }
}
