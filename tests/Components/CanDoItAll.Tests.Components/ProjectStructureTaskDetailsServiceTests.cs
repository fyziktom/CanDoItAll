using CanDoItAll.Components.Gantt;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Components;

public sealed class ProjectStructureTaskDetailsServiceTests
{
    [Fact]
    public async Task Stale_task_update_restores_previous_direct_assignee()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var partyDirectoryService = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var assigneeService = harness.Context.Services.GetRequiredService<ProjectStructureWorkItemAssigneeService>();
        var detailsService = harness.Context.Services.GetRequiredService<ProjectStructureTaskDetailsService>();
        var bridge = harness.Context.Services.GetRequiredService<IProjectPartyIntegrationBridge>();
        var projectId = await CreateProjectAsync(projectsService);
        var joeId = await CreatePersonAsync(partyDirectoryService, "Joe Doe");
        var janeId = await CreatePersonAsync(partyDirectoryService, "Jane Doe");
        var startUtc = new DateTimeOffset(2026, 7, 15, 8, 0, 0, TimeSpan.Zero);
        var task = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.WorkItem,
                "Authoritative title",
                string.Empty,
                string.Empty,
                $"project:{projectId}",
                420,
                260,
                StartUtc: startUtc,
                EndUtc: startUtc.AddDays(7),
                ObjectSubtype: "task",
                MetadataJson: ProjectObjectMetadataSerializer.Serialize(new ProjectObjectMetadataEnvelope
                {
                    WorkItem = new ProjectWorkItemMetadata
                    {
                        WorkItemKind = ProjectWorkItemKind.Task,
                        ExpectedEffortHours = 8m,
                        ExpectedEffortUnit = ProjectWorkItemEffortUnit.ManDays,
                        ExpectedCostAmount = 900m,
                        ExpectedCostCurrencyCode = "USD"
                    }
                })));
        await assigneeService.ReplaceAsync(
            projectId,
            task.Id,
            new ProjectStructureTaskResourceSelection(ProjectStructureTaskResourceKind.Person, joeId),
            "task-details-tests");
        var assignmentStartsOn = new DateOnly(2026, 7, 14);
        var assignmentEndsOn = new DateOnly(2026, 7, 25);
        var exactAssignmentResult = await bridge.ReplaceNodeAssignmentsAsync(
            projectId,
            new ProjectNodeReference(task.Id),
            [
                new ProjectPartyAssignmentUpsertRequest
                {
                    ProjectId = projectId,
                    PartyId = joeId,
                    Role = ProjectPartyAssignmentRole.WorkItemAssignee,
                    NodeKey = task.Id,
                    IsPrimary = true,
                    AllocationPercent = 65m,
                    StartsOn = assignmentStartsOn,
                    EndsOn = assignmentEndsOn,
                    Source = "negotiated-capacity",
                    Notes = "Joe is reserved at a negotiated allocation."
                }
            ],
            [ProjectPartyAssignmentRole.WorkItemAssignee]);
        Assert.True(exactAssignmentResult.IsSuccess);
        var estimate = new ProjectTaskEstimate(
            8m,
            ProjectWorkItemEffortUnit.ManDays,
            900m,
            "USD");
        var request = new ProjectStructureTaskDetailsUpdateRequest(
            new GanttTaskId(task.Id),
            "Stale title",
            "Proposed title",
            Math.Clamp(task.ProgressPercent, 0, 100),
            60,
            estimate,
            estimate,
            ScheduleChange: null,
            AssigneeChanged: true,
            ProposedAssignee: new ProjectStructureTaskResourceSelection(
                ProjectStructureTaskResourceKind.Person,
                janeId));

        var exception = await Assert.ThrowsAsync<ProjectStructureGanttMutationException>(() =>
            detailsService.UpdateAsync(projectId, request));

        Assert.Equal(ProjectStructureGanttMutationErrorCode.StaleTask, exception.Code);
        var assignment = Assert.Single(await bridge.ListAssignmentsDetailedAsync(projectId), item =>
            item.NodeKey == task.Id && item.Role == ProjectPartyAssignmentRole.WorkItemAssignee);
        Assert.Equal(joeId, assignment.PartyId);
        Assert.True(assignment.IsPrimary);
        Assert.Equal(65m, assignment.AllocationPercent);
        Assert.Equal(assignmentStartsOn, ToDateOnly(assignment.StartsAtUtc));
        Assert.Equal(assignmentEndsOn, ToDateOnly(assignment.EndsAtUtc));
        Assert.Equal("negotiated-capacity", assignment.Source);
        Assert.Equal("Joe is reserved at a negotiated allocation.", assignment.Notes);
        var refreshed = (await workbenchService.GetStructureAsync(projectId)).Nodes.Single(node => node.Id == task.Id);
        Assert.Equal(
            "Joe Doe",
            ProjectObjectMetadataSerializer.Parse(refreshed.MetadataJson).WorkItem!.AssigneePartyDisplayName);
        Assert.Equal("Authoritative title", refreshed.Title);
    }

    [Fact]
    public async Task Compensation_failure_is_explicit_and_does_not_claim_the_assignment_was_restored()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var partyDirectoryService = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var assigneeService = harness.Context.Services.GetRequiredService<ProjectStructureWorkItemAssigneeService>();
        var mutationService = harness.Context.Services.GetRequiredService<ProjectStructureGanttMutationService>();
        var innerBridge = harness.Context.Services.GetRequiredService<IProjectPartyIntegrationBridge>();
        var failingBridge = new CompensationFailingBridge(innerBridge);
        var detailsService = new ProjectStructureTaskDetailsService(
            mutationService,
            assigneeService,
            failingBridge,
            workbenchService,
            NullLogger<ProjectStructureTaskDetailsService>.Instance);
        var projectId = await CreateProjectAsync(projectsService);
        var joeId = await CreatePersonAsync(partyDirectoryService, "Joe Doe");
        var janeId = await CreatePersonAsync(partyDirectoryService, "Jane Doe");
        var startUtc = new DateTimeOffset(2026, 7, 15, 8, 0, 0, TimeSpan.Zero);
        var task = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.WorkItem,
                "Authoritative title",
                string.Empty,
                string.Empty,
                $"project:{projectId}",
                420,
                260,
                StartUtc: startUtc,
                EndUtc: startUtc.AddDays(7),
                ObjectSubtype: "task"));
        await assigneeService.ReplaceAsync(
            projectId,
            task.Id,
            new ProjectStructureTaskResourceSelection(ProjectStructureTaskResourceKind.Person, joeId),
            "task-details-tests");
        var estimate = ProjectTaskEstimate.Empty();
        var request = new ProjectStructureTaskDetailsUpdateRequest(
            new GanttTaskId(task.Id),
            "Stale title",
            "Proposed title",
            Math.Clamp(task.ProgressPercent, 0, 100),
            60,
            estimate,
            estimate,
            ScheduleChange: null,
            AssigneeChanged: true,
            ProposedAssignee: new ProjectStructureTaskResourceSelection(
                ProjectStructureTaskResourceKind.Person,
                janeId));
        failingBridge.FailReplacement = true;

        var exception = await Assert.ThrowsAsync<ProjectStructureTaskDetailsException>(() =>
            detailsService.UpdateAsync(projectId, request));

        Assert.Equal(ProjectStructureTaskDetailsErrorCode.AssignmentCompensationFailed, exception.Code);
        Assert.IsType<AggregateException>(exception.InnerException);
        var assignment = Assert.Single(await innerBridge.ListAssignmentsDetailedAsync(projectId), item =>
            item.NodeKey == task.Id && item.Role == ProjectPartyAssignmentRole.WorkItemAssignee);
        Assert.Equal(janeId, assignment.PartyId);
        var refreshed = (await workbenchService.GetStructureAsync(projectId)).Nodes.Single(node => node.Id == task.Id);
        Assert.Equal("Jane Doe", ProjectObjectMetadataSerializer.Parse(refreshed.MetadataJson).WorkItem!.AssigneePartyDisplayName);
        Assert.Equal("Authoritative title", refreshed.Title);
    }

    private static async Task<Guid> CreateProjectAsync(ProjectsService projectsService)
    {
        var result = await projectsService.SaveAsync(new ProjectEditorModel
        {
            Name = $"Task details compensation {Guid.NewGuid():N}",
            Description = "Task detail assignment compensation proof.",
            Objective = "Keep task state and direct assignment consistent.",
            CurrentPhase = "Delivery"
        });
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static async Task<Guid> CreatePersonAsync(
        PartyDirectoryService partyDirectoryService,
        string displayName)
    {
        var result = await partyDirectoryService.SavePartyAsync(new PartyEditorModel
        {
            PartyType = PartyType.Person,
            LifecycleStatus = PartyLifecycleStatus.Active,
            DisplayName = displayName,
            Summary = $"{displayName} task-details test record.",
            LastChangedBy = "component-tests"
        });
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static DateOnly? ToDateOnly(DateTimeOffset? value)
        => value.HasValue ? DateOnly.FromDateTime(value.Value.UtcDateTime) : null;

    private sealed class CompensationFailingBridge(IProjectPartyIntegrationBridge inner)
        : IProjectPartyIntegrationBridge
    {
        public bool FailReplacement { get; set; }

        public Task<IReadOnlyDictionary<Guid, ProjectPortfolioPartyContext>> GetPortfolioContextsAsync(
            IReadOnlyCollection<Guid> projectIds,
            CancellationToken cancellationToken = default)
            => inner.GetPortfolioContextsAsync(projectIds, cancellationToken);

        public Task<IReadOnlyList<ProjectPartyOption>> ListPartyOptionsAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
            => inner.ListPartyOptionsAsync(projectId, cancellationToken);

        public Task<ProjectPartyOption?> GetPartyOptionAsync(
            Guid partyId,
            CancellationToken cancellationToken = default)
            => inner.GetPartyOptionAsync(partyId, cancellationToken);

        public Task<IReadOnlyList<ProjectPartyAssignmentDetail>> ListAssignmentsDetailedAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
            => inner.ListAssignmentsDetailedAsync(projectId, cancellationToken);

        public Task<Result<Guid>> SaveAssignmentAsync(
            ProjectPartyAssignmentUpsertRequest request,
            CancellationToken cancellationToken = default)
            => inner.SaveAssignmentAsync(request, cancellationToken);

        public Task<Result> ReplaceNodeAssignmentsAsync(
            Guid projectId,
            ProjectNodeReference nodeReference,
            IReadOnlyList<ProjectPartyAssignmentUpsertRequest> desiredAssignments,
            IReadOnlyList<ProjectPartyAssignmentRole> targetRoles,
            CancellationToken cancellationToken = default)
            => FailReplacement
                ? Task.FromResult(Result.Failure(Error.Failure(
                    "Injected assignment compensation failure.",
                    "test.assignment-compensation-failure")))
                : inner.ReplaceNodeAssignmentsAsync(
                    projectId,
                    nodeReference,
                    desiredAssignments,
                    targetRoles,
                    cancellationToken);

        public Task DeleteAssignmentAsync(
            Guid assignmentId,
            CancellationToken cancellationToken = default)
            => inner.DeleteAssignmentAsync(assignmentId, cancellationToken);

        public Task DeleteAssignmentsForNodesAsync(
            Guid projectId,
            IReadOnlyCollection<ProjectNodeReference> nodeReferences,
            CancellationToken cancellationToken = default)
            => inner.DeleteAssignmentsForNodesAsync(projectId, nodeReferences, cancellationToken);

        public Task MoveAssignmentsToProjectAsync(
            Guid sourceProjectId,
            IReadOnlyCollection<ProjectNodeReference> nodeReferences,
            Guid targetProjectId,
            CancellationToken cancellationToken = default)
            => inner.MoveAssignmentsToProjectAsync(
                sourceProjectId,
                nodeReferences,
                targetProjectId,
                cancellationToken);

        public Task<Result<ProjectPartyQuickCreateResult>> CreatePartyAsync(
            ProjectPartyQuickCreateRequest request,
            CancellationToken cancellationToken = default)
            => inner.CreatePartyAsync(request, cancellationToken);
    }
}
