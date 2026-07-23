using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class ProjectPartyAssignmentRevisionPricingTests
{
    [Fact]
    public async Task Generic_save_delete_and_replace_advance_revision_and_invalidate_stale_pricing()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var services = harness.Context.Services;
        var projectsService = services.GetRequiredService<ProjectsService>();
        var partyDirectoryService =
            services.GetRequiredService<PartyDirectoryService>();
        var workbenchService =
            services.GetRequiredService<ProjectWorkbenchService>();
        var bridge =
            services.GetRequiredService<IProjectPartyIntegrationBridge>();
        var projectId = await CreateProjectAsync(projectsService);
        var personA = await CreatePersonAsync(
            partyDirectoryService,
            "Generic assignment A");
        var personB = await CreatePersonAsync(
            partyDirectoryService,
            "Generic assignment B");

        var saveTask = await CreateTaskAsync(
            workbenchService,
            projectId,
            "Generic save task",
            manualCostAmount: 999m);
        var saveResult = await bridge.SaveAssignmentAsync(
            CreateAssignment(projectId, saveTask.Id, personA));
        Assert.True(saveResult.IsSuccess);
        AssertClearedWithRevision(
            await ReadStateAsync(
                workbenchService,
                projectId,
                saveTask.Id),
            expectedRevision: 1);

        var deleteTask = await CreateTaskAsync(
            workbenchService,
            projectId,
            "Generic delete task");
        var deleteSaveResult = await bridge.SaveAssignmentAsync(
            CreateAssignment(projectId, deleteTask.Id, personA));
        Assert.True(deleteSaveResult.IsSuccess);
        await SetAuthoritativePersonPricingAsync(
            workbenchService,
            projectId,
            deleteTask.Id,
            personA);
        await bridge.DeleteAssignmentAsync(deleteSaveResult.Value);
        AssertClearedWithRevision(
            await ReadStateAsync(
                workbenchService,
                projectId,
                deleteTask.Id),
            expectedRevision: 2);
        Assert.Empty(await ReadAssignmentsAsync(
            bridge,
            projectId,
            deleteTask.Id));

        var replaceTask = await CreateTaskAsync(
            workbenchService,
            projectId,
            "Generic replace task");
        var replaceSaveResult = await bridge.SaveAssignmentAsync(
            CreateAssignment(projectId, replaceTask.Id, personA));
        Assert.True(replaceSaveResult.IsSuccess);
        await SetAuthoritativePersonPricingAsync(
            workbenchService,
            projectId,
            replaceTask.Id,
            personA);
        var replaceResult = await bridge.ReplaceNodeAssignmentsAsync(
            projectId,
            new ProjectNodeReference(replaceTask.Id),
            [
                CreateAssignment(
                    projectId,
                    replaceTask.Id,
                    personB)
            ],
            [ProjectPartyAssignmentRole.WorkItemAssignee]);
        Assert.True(replaceResult.IsSuccess);
        AssertClearedWithRevision(
            await ReadStateAsync(
                workbenchService,
                projectId,
                replaceTask.Id),
            expectedRevision: 2);
        Assert.Equal(
            personB,
            Assert.Single(await ReadAssignmentsAsync(
                bridge,
                projectId,
                replaceTask.Id)).PartyId);
    }

    private static async Task<Guid> CreateProjectAsync(
        ProjectsService projectsService)
    {
        var result = await projectsService.SaveAsync(new ProjectEditorModel
        {
            Name = $"Assignment revision {Guid.NewGuid():N}",
            Description = "Generic CRM assignment mutation proof.",
            Objective =
                "Invalidate stale task pricing whenever direct assignment truth changes.",
            CurrentPhase = "Delivery"
        });
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static async Task<Guid> CreatePersonAsync(
        PartyDirectoryService partyDirectoryService,
        string displayName)
    {
        var result = await partyDirectoryService.SavePartyAsync(
            new PartyEditorModel
            {
                PartyType = PartyType.Person,
                LifecycleStatus = PartyLifecycleStatus.Active,
                DisplayName = displayName,
                Summary = $"{displayName} assignment-revision test.",
                LastChangedBy = "component-tests"
            });
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static Task<ProjectStructureNode> CreateTaskAsync(
        ProjectWorkbenchService workbenchService,
        Guid projectId,
        string title,
        decimal? manualCostAmount = null)
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
                ObjectSubtype: "task",
                MetadataJson: ProjectObjectMetadataSerializer.Serialize(
                    new ProjectObjectMetadataEnvelope
                    {
                        WorkItem = new ProjectWorkItemMetadata
                        {
                            WorkItemKind = ProjectWorkItemKind.Task,
                            ExecutionState =
                                ProjectTaskExecutionState.NotStarted,
                            ExpectedEffortHours = 8m,
                            ExpectedEffortUnit =
                                ProjectWorkItemEffortUnit.Hours,
                            ExpectedCostAmount = manualCostAmount,
                            ExpectedCostCurrencyCode =
                                manualCostAmount.HasValue
                                    ? "EUR"
                                    : string.Empty
                        }
                    })));
    }

    private static ProjectPartyAssignmentUpsertRequest CreateAssignment(
        Guid projectId,
        string taskNodeId,
        Guid partyId)
    {
        return new ProjectPartyAssignmentUpsertRequest
        {
            ProjectId = projectId,
            PartyId = partyId,
            Role = ProjectPartyAssignmentRole.WorkItemAssignee,
            NodeKey = taskNodeId,
            IsPrimary = true,
            Source = "generic-assignment-test"
        };
    }

    private static Task<ProjectStructureNode?>
        SetAuthoritativePersonPricingAsync(
            ProjectWorkbenchService workbenchService,
            Guid projectId,
            string taskNodeId,
            Guid personId)
    {
        return workbenchService.MutateObjectMetadataSerializableAsync(
            projectId,
            taskNodeId,
            metadata =>
            {
                Assert.NotNull(metadata.WorkItem);
                metadata.WorkItem!.ExpectedCostAmount = 100m;
                metadata.WorkItem.ExpectedCostCurrencyCode = "USD";
                metadata.WorkItem.ExpectedCostBasis =
                    new ProjectTaskExpectedCostBasis
                    {
                        ResourceKind =
                            ProjectStructureTaskResourceKind.Person,
                        ResourceId = personId,
                        Source = ProjectStructureTaskResourceCostSource
                            .CrmWorkforceRate,
                        CalculatedAtUtc =
                            DateTimeOffset.Parse(
                                "2026-07-23T19:00:00Z")
                    };
            });
    }

    private static async Task<ProjectStructureTaskEditState> ReadStateAsync(
        ProjectWorkbenchService workbenchService,
        Guid projectId,
        string taskNodeId)
    {
        var task = (await workbenchService.GetStructureAsync(projectId))
            .Nodes
            .Single(node => node.Id == taskNodeId);
        return ProjectStructureTaskEditStatePolicy.Read(task);
    }

    private static async Task<IReadOnlyList<ProjectPartyAssignmentDetail>>
        ReadAssignmentsAsync(
            IProjectPartyIntegrationBridge bridge,
            Guid projectId,
            string taskNodeId)
    {
        return (await bridge.ListAssignmentsDetailedAsync(
                projectId,
                [ProjectPartyAssignmentRole.WorkItemAssignee]))
            .Where(assignment =>
                string.Equals(
                    assignment.NodeKey,
                    taskNodeId,
                    StringComparison.Ordinal))
            .ToArray();
    }

    private static void AssertClearedWithRevision(
        ProjectStructureTaskEditState state,
        long expectedRevision)
    {
        Assert.Equal(expectedRevision, state.DirectAssignmentRevision);
        Assert.Null(state.Estimate.ExpectedCostAmount);
        Assert.Empty(state.Estimate.ExpectedCostCurrencyCode);
        Assert.Null(state.CostBasis);
    }
}
