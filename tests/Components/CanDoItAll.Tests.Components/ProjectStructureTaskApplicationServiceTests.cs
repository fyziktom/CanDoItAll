using CanDoItAll.Components.Gantt;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class ProjectStructureTaskApplicationServiceTests
{
    [Fact]
    public async Task Create_applies_the_CRM_quote_and_returns_the_committed_assignment_revision()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var services = harness.Context.Services;
        var projectsService = services.GetRequiredService<ProjectsService>();
        var partyDirectoryService =
            services.GetRequiredService<PartyDirectoryService>();
        var hrService = services.GetRequiredService<HrService>();
        var workbenchService =
            services.GetRequiredService<ProjectWorkbenchService>();
        var applicationService =
            services.GetRequiredService<
                ProjectStructureTaskApplicationService>();
        var bridge =
            services.GetRequiredService<IProjectPartyIntegrationBridge>();
        var projectId = await CreateProjectAsync(projectsService);
        var personId = await CreateRatedPartyAsync(
            partyDirectoryService,
            hrService,
            "CRM-priced creator",
            PartyType.Person,
            25m);
        var estimate = new ProjectTaskEstimate(
            8m,
            ProjectWorkItemEffortUnit.Hours,
            999m,
            "EUR");
        var assignee = new ProjectStructureTaskResourceSelection(
            ProjectStructureTaskResourceKind.Person,
            personId);

        var result = await applicationService.CreateAsync(
            new ProjectStructureTaskCreateApplicationRequest(
                projectId,
                estimate,
                assignee,
                "application-service-tests"),
            (pricing, cancellationToken) => CreateTaskAsync(
                workbenchService,
                projectId,
                "CRM-priced task",
                pricing,
                cancellationToken));

        Assert.Equal(
            ProjectStructureTaskEstimateRefreshStatus.Refreshed,
            result.Pricing.Status);
        Assert.Equal(200m, result.Pricing.Estimate.ExpectedCostAmount);
        Assert.Equal("USD", result.Pricing.Estimate.ExpectedCostCurrencyCode);
        var state = ProjectStructureTaskEditStatePolicy.Read(result.Task);
        Assert.Equal(1, state.DirectAssignmentRevision);
        Assert.Equal(200m, state.Estimate.ExpectedCostAmount);
        Assert.Equal(personId, state.CostBasis?.ResourceId);
        Assert.Equal(
            ProjectStructureTaskResourceCostSource.CrmWorkforceRate,
            state.CostBasis?.Source);
        var assignment = Assert.Single(
            await ReadDirectAssignmentsAsync(
                bridge,
                projectId,
                result.Task.Id));
        Assert.Equal(personId, assignment.PartyId);
        Assert.True(assignment.IsPrimary);
    }

    [Fact]
    public async Task Edit_replaces_A_with_B_and_persists_from_the_post_replacement_cleared_snapshot()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var services = harness.Context.Services;
        var projectsService = services.GetRequiredService<ProjectsService>();
        var partyDirectoryService =
            services.GetRequiredService<PartyDirectoryService>();
        var hrService = services.GetRequiredService<HrService>();
        var workbenchService =
            services.GetRequiredService<ProjectWorkbenchService>();
        var mutationService =
            services.GetRequiredService<ProjectStructureGanttMutationService>();
        var applicationService =
            services.GetRequiredService<
                ProjectStructureTaskApplicationService>();
        var bridge =
            services.GetRequiredService<IProjectPartyIntegrationBridge>();
        var projectId = await CreateProjectAsync(projectsService);
        var personA = await CreateRatedPartyAsync(
            partyDirectoryService,
            hrService,
            "Edit person A",
            PartyType.Person,
            10m);
        var personB = await CreateRatedPartyAsync(
            partyDirectoryService,
            hrService,
            "Edit person B",
            PartyType.Person,
            30m);
        var created = await CreateAssignedTaskAsync(
            applicationService,
            workbenchService,
            projectId,
            "A to B task",
            personA,
            8m);
        var expectedState =
            ProjectStructureTaskEditStatePolicy.Read(created.Task);
        ProjectStructureTaskEditCommitContext? observedCommit = null;

        await applicationService.EditAsync(
            new ProjectStructureTaskEditApplicationRequest(
                projectId,
                created.Task.Id,
                expectedState,
                new ProjectTaskEstimate(
                    4m,
                    ProjectWorkItemEffortUnit.Hours,
                    1m,
                    "JPY"),
                ProjectTaskExecutionSnapshot.NotStarted,
                AssigneeChanged: true,
                ProposedAssignee:
                    new ProjectStructureTaskResourceSelection(
                        ProjectStructureTaskResourceKind.Person,
                        personB),
                "application-service-tests"),
            async (commit, cancellationToken) =>
            {
                observedCommit = commit;
                return await PersistEditAsync(
                    mutationService,
                    projectId,
                    commit,
                    "A to B task updated",
                    cancellationToken);
            });

        Assert.NotNull(observedCommit);
        Assert.Equal(
            expectedState.DirectAssignmentRevision + 1,
            observedCommit!.CurrentState.DirectAssignmentRevision);
        Assert.Null(
            observedCommit.CurrentState.Estimate.ExpectedCostAmount);
        Assert.Empty(
            observedCommit.CurrentState.Estimate
                .ExpectedCostCurrencyCode);
        Assert.Null(observedCommit.CurrentState.CostBasis);
        var committedAssignment =
            Assert.Single(observedCommit.DirectAssignments);
        Assert.Equal(personB, committedAssignment.PartyId);
        Assert.Equal(120m, observedCommit.ProposedEstimate.ExpectedCostAmount);
        Assert.Equal(personB, observedCommit.ProposedCostBasis?.ResourceId);

        var persistedTask = await ReadTaskAsync(
            workbenchService,
            projectId,
            created.Task.Id);
        var persistedState =
            ProjectStructureTaskEditStatePolicy.Read(persistedTask);
        Assert.Equal(
            expectedState.DirectAssignmentRevision + 1,
            persistedState.DirectAssignmentRevision);
        Assert.Equal(120m, persistedState.Estimate.ExpectedCostAmount);
        Assert.Equal("USD", persistedState.Estimate.ExpectedCostCurrencyCode);
        Assert.Equal(personB, persistedState.CostBasis?.ResourceId);
        Assert.Equal(
            personB,
            Assert.Single(await ReadDirectAssignmentsAsync(
                bridge,
                projectId,
                created.Task.Id)).PartyId);
    }

    [Fact]
    public async Task Callback_failure_restores_the_exact_A_assignment_and_pricing_while_revision_advances_twice()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var services = harness.Context.Services;
        var projectsService = services.GetRequiredService<ProjectsService>();
        var partyDirectoryService =
            services.GetRequiredService<PartyDirectoryService>();
        var hrService = services.GetRequiredService<HrService>();
        var workbenchService =
            services.GetRequiredService<ProjectWorkbenchService>();
        var applicationService =
            services.GetRequiredService<
                ProjectStructureTaskApplicationService>();
        var bridge =
            services.GetRequiredService<IProjectPartyIntegrationBridge>();
        var projectId = await CreateProjectAsync(projectsService);
        var personA = await CreateRatedPartyAsync(
            partyDirectoryService,
            hrService,
            "Compensation person A",
            PartyType.Person,
            15m);
        var personB = await CreateRatedPartyAsync(
            partyDirectoryService,
            hrService,
            "Compensation person B",
            PartyType.Person,
            40m);
        var created = await CreateAssignedTaskAsync(
            applicationService,
            workbenchService,
            projectId,
            "Compensation task",
            personA,
            8m);
        var startsOn = new DateOnly(2026, 7, 20);
        var endsOn = new DateOnly(2026, 7, 31);
        var exactAssignmentResult =
            await bridge.ReplaceNodeAssignmentsAsync(
                projectId,
                new ProjectNodeReference(created.Task.Id),
                [
                    new ProjectPartyAssignmentUpsertRequest
                    {
                        ProjectId = projectId,
                        PartyId = personA,
                        Role =
                            ProjectPartyAssignmentRole.WorkItemAssignee,
                        NodeKey = created.Task.Id,
                        IsPrimary = true,
                        AllocationPercent = 65m,
                        StartsOn = startsOn,
                        EndsOn = endsOn,
                        Source = "negotiated-capacity",
                        Notes = "Restore this exact assignment."
                    }
                ],
                [ProjectPartyAssignmentRole.WorkItemAssignee]);
        Assert.True(exactAssignmentResult.IsSuccess);
        var originalAssignment = Assert.Single(
            await ReadDirectAssignmentsAsync(
                bridge,
                projectId,
                created.Task.Id));
        var originalTask = await ReadTaskAsync(
            workbenchService,
            projectId,
            created.Task.Id);
        var originalState =
            ProjectStructureTaskEditStatePolicy.Read(originalTask);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => applicationService.EditAsync<bool>(
                new ProjectStructureTaskEditApplicationRequest(
                    projectId,
                    created.Task.Id,
                    originalState,
                    new ProjectTaskEstimate(
                        4m,
                        ProjectWorkItemEffortUnit.Hours,
                        null,
                        string.Empty),
                    ProjectTaskExecutionSnapshot.NotStarted,
                    AssigneeChanged: true,
                    ProposedAssignee:
                        new ProjectStructureTaskResourceSelection(
                            ProjectStructureTaskResourceKind.Person,
                            personB),
                    "application-service-tests"),
                (_, _) => throw new InvalidOperationException(
                    "Injected persistence failure.")));

        Assert.Contains(
            "Injected persistence failure",
            exception.Message,
            StringComparison.Ordinal);
        var restoredAssignment = Assert.Single(
            await ReadDirectAssignmentsAsync(
                bridge,
                projectId,
                created.Task.Id));
        AssertExactAssignment(originalAssignment, restoredAssignment);
        var restoredTask = await ReadTaskAsync(
            workbenchService,
            projectId,
            created.Task.Id);
        var restoredState =
            ProjectStructureTaskEditStatePolicy.Read(restoredTask);
        Assert.Equal(
            originalState.DirectAssignmentRevision + 2,
            restoredState.DirectAssignmentRevision);
        Assert.Equal(originalState.Estimate, restoredState.Estimate);
        Assert.Equal(originalState.Execution, restoredState.Execution);
        Assert.Equal(originalState.CostBasis, restoredState.CostBasis);
        Assert.Equal(startsOn, ToDateOnly(restoredAssignment.StartsAtUtc));
        Assert.Equal(endsOn, ToDateOnly(restoredAssignment.EndsAtUtc));
    }

    [Fact]
    public async Task Competing_assignment_inside_the_callback_causes_safe_conflict_and_compensation_failure()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var services = harness.Context.Services;
        var projectsService = services.GetRequiredService<ProjectsService>();
        var partyDirectoryService =
            services.GetRequiredService<PartyDirectoryService>();
        var hrService = services.GetRequiredService<HrService>();
        var workbenchService =
            services.GetRequiredService<ProjectWorkbenchService>();
        var mutationService =
            services.GetRequiredService<ProjectStructureGanttMutationService>();
        var applicationService =
            services.GetRequiredService<
                ProjectStructureTaskApplicationService>();
        var bridge =
            services.GetRequiredService<IProjectPartyIntegrationBridge>();
        var projectId = await CreateProjectAsync(projectsService);
        var personA = await CreateRatedPartyAsync(
            partyDirectoryService,
            hrService,
            "Race person A",
            PartyType.Person,
            10m);
        var personB = await CreateRatedPartyAsync(
            partyDirectoryService,
            hrService,
            "Race person B",
            PartyType.Person,
            20m);
        var competingAgent = await CreateRatedPartyAsync(
            partyDirectoryService,
            hrService,
            "Race agent C",
            PartyType.AiAgent,
            5m);
        var created = await CreateAssignedTaskAsync(
            applicationService,
            workbenchService,
            projectId,
            "Competing callback task",
            personA,
            8m);
        var expectedState =
            ProjectStructureTaskEditStatePolicy.Read(created.Task);

        var exception =
            await Assert.ThrowsAsync<
                ProjectStructureTaskApplicationException>(
                () => applicationService.EditAsync(
                    new ProjectStructureTaskEditApplicationRequest(
                        projectId,
                        created.Task.Id,
                        expectedState,
                        new ProjectTaskEstimate(
                            4m,
                            ProjectWorkItemEffortUnit.Hours,
                            null,
                            string.Empty),
                        ProjectTaskExecutionSnapshot.NotStarted,
                        AssigneeChanged: true,
                        ProposedAssignee:
                            new ProjectStructureTaskResourceSelection(
                                ProjectStructureTaskResourceKind.Person,
                                personB),
                        "application-service-tests"),
                    async (commit, cancellationToken) =>
                    {
                        var saveResult = await bridge.SaveAssignmentAsync(
                            new ProjectPartyAssignmentUpsertRequest
                            {
                                ProjectId = projectId,
                                PartyId = competingAgent,
                                Role = ProjectPartyAssignmentRole
                                    .WorkItemAssignee,
                                NodeKey = created.Task.Id,
                                IsPrimary = false,
                                Source = "competing-callback"
                            },
                            cancellationToken);
                        Assert.True(saveResult.IsSuccess);
                        return await PersistEditAsync(
                            mutationService,
                            projectId,
                            commit,
                            "Must not persist",
                            cancellationToken);
                    }));

        Assert.Equal(
            ProjectStructureTaskApplicationErrorCode.CompensationFailed,
            exception.Code);
        Assert.IsType<AggregateException>(exception.InnerException);
        var assignments = await ReadDirectAssignmentsAsync(
            bridge,
            projectId,
            created.Task.Id);
        Assert.Equal(2, assignments.Count);
        Assert.Contains(
            assignments,
            assignment => assignment.PartyId == personB);
        Assert.Contains(
            assignments,
            assignment => assignment.PartyId == competingAgent);
        Assert.DoesNotContain(
            assignments,
            assignment => assignment.PartyId == personA);
        var persistedTask = await ReadTaskAsync(
            workbenchService,
            projectId,
            created.Task.Id);
        var persistedState =
            ProjectStructureTaskEditStatePolicy.Read(persistedTask);
        Assert.Equal(
            expectedState.DirectAssignmentRevision + 2,
            persistedState.DirectAssignmentRevision);
        Assert.Null(persistedState.Estimate.ExpectedCostAmount);
        Assert.Empty(persistedState.Estimate.ExpectedCostCurrencyCode);
        Assert.Null(persistedState.CostBasis);
        Assert.Equal("Competing callback task", persistedTask.Title);
    }

    [Fact]
    public async Task Mixed_person_and_agent_allows_scalar_edit_but_rejects_direct_assignment_change()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var services = harness.Context.Services;
        var projectsService = services.GetRequiredService<ProjectsService>();
        var partyDirectoryService =
            services.GetRequiredService<PartyDirectoryService>();
        var hrService = services.GetRequiredService<HrService>();
        var workbenchService =
            services.GetRequiredService<ProjectWorkbenchService>();
        var mutationService =
            services.GetRequiredService<ProjectStructureGanttMutationService>();
        var applicationService =
            services.GetRequiredService<
                ProjectStructureTaskApplicationService>();
        var bridge =
            services.GetRequiredService<IProjectPartyIntegrationBridge>();
        var projectId = await CreateProjectAsync(projectsService);
        var personA = await CreateRatedPartyAsync(
            partyDirectoryService,
            hrService,
            "Mixed person A",
            PartyType.Person,
            12m);
        var personB = await CreateRatedPartyAsync(
            partyDirectoryService,
            hrService,
            "Mixed person B",
            PartyType.Person,
            18m);
        var supportingAgent = await CreateRatedPartyAsync(
            partyDirectoryService,
            hrService,
            "Mixed agent",
            PartyType.AiAgent,
            5m);
        var created = await CreateAssignedTaskAsync(
            applicationService,
            workbenchService,
            projectId,
            "Mixed assignment task",
            personA,
            8m);
        var addAgentResult = await bridge.SaveAssignmentAsync(
            new ProjectPartyAssignmentUpsertRequest
            {
                ProjectId = projectId,
                PartyId = supportingAgent,
                Role = ProjectPartyAssignmentRole.WorkItemAssignee,
                NodeKey = created.Task.Id,
                IsPrimary = false,
                Source = "supporting-agent"
            });
        Assert.True(addAgentResult.IsSuccess);
        var assignmentsBefore = (await ReadDirectAssignmentsAsync(
                bridge,
                projectId,
                created.Task.Id))
            .OrderBy(static assignment => assignment.Id)
            .ToArray();
        var currentTask = await ReadTaskAsync(
            workbenchService,
            projectId,
            created.Task.Id);
        var currentState =
            ProjectStructureTaskEditStatePolicy.Read(currentTask);

        await applicationService.EditAsync(
            new ProjectStructureTaskEditApplicationRequest(
                projectId,
                created.Task.Id,
                currentState,
                new ProjectTaskEstimate(
                    4m,
                    ProjectWorkItemEffortUnit.Hours,
                    null,
                    string.Empty),
                ProjectTaskExecutionSnapshot.NotStarted,
                AssigneeChanged: false,
                ProposedAssignee: null,
                "application-service-tests"),
            (commit, cancellationToken) => PersistEditAsync(
                mutationService,
                projectId,
                commit,
                "Mixed assignment scalar edit",
                cancellationToken));

        var assignmentsAfterScalar = (await ReadDirectAssignmentsAsync(
                bridge,
                projectId,
                created.Task.Id))
            .OrderBy(static assignment => assignment.Id)
            .ToArray();
        Assert.Equal(assignmentsBefore, assignmentsAfterScalar);
        var scalarTask = await ReadTaskAsync(
            workbenchService,
            projectId,
            created.Task.Id);
        var scalarState =
            ProjectStructureTaskEditStatePolicy.Read(scalarTask);
        Assert.Equal(
            currentState.DirectAssignmentRevision,
            scalarState.DirectAssignmentRevision);
        Assert.Equal(48m, scalarState.Estimate.ExpectedCostAmount);
        Assert.Equal(personA, scalarState.CostBasis?.ResourceId);
        var callbackInvoked = false;

        var exception =
            await Assert.ThrowsAsync<
                ProjectStructureTaskApplicationException>(
                () => applicationService.EditAsync(
                    new ProjectStructureTaskEditApplicationRequest(
                        projectId,
                        created.Task.Id,
                        scalarState,
                        scalarState.Estimate,
                        scalarState.Execution,
                        AssigneeChanged: true,
                        ProposedAssignee:
                            new ProjectStructureTaskResourceSelection(
                                ProjectStructureTaskResourceKind.Person,
                                personB),
                        "application-service-tests"),
                    (_, _) =>
                    {
                        callbackInvoked = true;
                        return Task.FromResult(true);
                    }));

        Assert.Equal(
            ProjectStructureTaskApplicationErrorCode.AssignmentConflict,
            exception.Code);
        Assert.False(callbackInvoked);
        Assert.Equal(
            assignmentsAfterScalar,
            (await ReadDirectAssignmentsAsync(
                    bridge,
                    projectId,
                    created.Task.Id))
                .OrderBy(static assignment => assignment.Id)
                .ToArray());
        Assert.Equal(
            scalarState,
            ProjectStructureTaskEditStatePolicy.Read(
                await ReadTaskAsync(
                    workbenchService,
                    projectId,
                    created.Task.Id)));
    }

    private static async Task<Guid> CreateProjectAsync(
        ProjectsService projectsService)
    {
        var result = await projectsService.SaveAsync(new ProjectEditorModel
        {
            Name = $"Task application {Guid.NewGuid():N}",
            Description = "Task application service orchestration proof.",
            Objective =
                "Keep assignments, revisions, and authoritative pricing consistent.",
            CurrentPhase = "Delivery"
        });
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static async Task<Guid> CreateRatedPartyAsync(
        PartyDirectoryService partyDirectoryService,
        HrService hrService,
        string displayName,
        PartyType partyType,
        decimal hourlyRate)
    {
        var partyResult = await partyDirectoryService.SavePartyAsync(
            new PartyEditorModel
            {
                PartyType = partyType,
                LifecycleStatus = PartyLifecycleStatus.Active,
                DisplayName = displayName,
                Summary = $"{displayName} application-service test.",
                LastChangedBy = "component-tests"
            });
        Assert.True(partyResult.IsSuccess);
        var profileResult = await hrService.SaveWorkforceProfileAsync(
            new WorkforceProfileEditorModel
            {
                PartyId = partyResult.Value,
                WorkforceKind = partyType == PartyType.AiAgent
                    ? WorkforceKind.Contractor
                    : WorkforceKind.Employee,
                InternalCostRate = hourlyRate,
                RateUnit = ProjectResourceRateUnit.Hour,
                RateCurrencyCode = "USD",
                Status = "Active",
                LastChangedBy = "component-tests"
            });
        Assert.True(profileResult.IsSuccess);
        return partyResult.Value;
    }

    private static Task<ProjectStructureTaskCreateApplicationResult>
        CreateAssignedTaskAsync(
            ProjectStructureTaskApplicationService applicationService,
            ProjectWorkbenchService workbenchService,
            Guid projectId,
            string title,
            Guid personId,
            decimal effortHours)
    {
        return applicationService.CreateAsync(
            new ProjectStructureTaskCreateApplicationRequest(
                projectId,
                new ProjectTaskEstimate(
                    effortHours,
                    ProjectWorkItemEffortUnit.Hours,
                    null,
                    string.Empty),
                new ProjectStructureTaskResourceSelection(
                    ProjectStructureTaskResourceKind.Person,
                    personId),
                "application-service-tests"),
            (pricing, cancellationToken) => CreateTaskAsync(
                workbenchService,
                projectId,
                title,
                pricing,
                cancellationToken));
    }

    private static async Task<ProjectStructureNode?> CreateTaskAsync(
        ProjectWorkbenchService workbenchService,
        Guid projectId,
        string title,
        ProjectStructureTaskEstimateRefreshResult pricing,
        CancellationToken cancellationToken)
    {
        var estimate = pricing.Estimate;
        return await workbenchService.CreateObjectAsync(
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
                            ExpectedEffortHours =
                                estimate.ExpectedEffortHours,
                            ExpectedEffortUnit =
                                estimate.ExpectedEffortUnit,
                            ExpectedCostAmount =
                                estimate.ExpectedCostAmount,
                            ExpectedCostCurrencyCode =
                                estimate.ExpectedCostCurrencyCode,
                            ExpectedCostBasis =
                                pricing.CalculatedCostBasis
                        }
                    }),
                TaskPricingInitialization:
                    ProjectObjectTaskPricingInitialization
                        .PreserveValidatedAuthoritativePricing),
            cancellationToken);
    }

    private static Task<ProjectStructureGanttMutationResult>
        PersistEditAsync(
            ProjectStructureGanttMutationService mutationService,
            Guid projectId,
            ProjectStructureTaskEditCommitContext commit,
            string proposedTitle,
            CancellationToken cancellationToken)
    {
        var currentProgress = Math.Clamp(
            commit.CurrentTask.ProgressPercent,
            0,
            100);
        return mutationService.ApplyTaskDetailsAsync(
            projectId,
            new ProjectStructureTaskDetailsMutationRequest(
                new GanttTaskId(commit.CurrentTask.Id),
                commit.CurrentTask.Title,
                proposedTitle,
                currentProgress,
                currentProgress,
                commit.CurrentState.Estimate,
                commit.ProposedEstimate,
                ScheduleChange: null,
                commit.CurrentState.Execution,
                commit.ProposedExecution,
                commit.CurrentState.CostBasis,
                commit.ProposedCostBasis,
                commit.ProposedCostBasis !=
                    commit.CurrentState.CostBasis,
                commit.CurrentState.DirectAssignmentRevision),
            cancellationToken);
    }

    private static async Task<ProjectStructureNode> ReadTaskAsync(
        ProjectWorkbenchService workbenchService,
        Guid projectId,
        string taskNodeId)
    {
        return (await workbenchService.GetStructureAsync(projectId))
            .Nodes
            .Single(node => node.Id == taskNodeId);
    }

    private static async Task<IReadOnlyList<ProjectPartyAssignmentDetail>>
        ReadDirectAssignmentsAsync(
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

    private static void AssertExactAssignment(
        ProjectPartyAssignmentDetail expected,
        ProjectPartyAssignmentDetail actual)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.ProjectId, actual.ProjectId);
        Assert.Equal(expected.PartyId, actual.PartyId);
        Assert.Equal(expected.PartyType, actual.PartyType);
        Assert.Equal(expected.Role, actual.Role);
        Assert.Equal(expected.NodeKey, actual.NodeKey);
        Assert.Equal(expected.IsPrimary, actual.IsPrimary);
        Assert.Equal(
            expected.AllocationPercent,
            actual.AllocationPercent);
        Assert.Equal(
            ToDateOnly(expected.StartsAtUtc),
            ToDateOnly(actual.StartsAtUtc));
        Assert.Equal(
            ToDateOnly(expected.EndsAtUtc),
            ToDateOnly(actual.EndsAtUtc));
        Assert.Equal(expected.Source, actual.Source);
        Assert.Equal(expected.Notes, actual.Notes);
    }

    private static DateOnly? ToDateOnly(DateTimeOffset? value)
        => value.HasValue
            ? DateOnly.FromDateTime(value.Value.UtcDateTime)
            : null;
}
