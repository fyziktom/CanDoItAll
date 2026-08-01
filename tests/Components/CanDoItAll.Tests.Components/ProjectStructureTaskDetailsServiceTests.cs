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
        var currentTask = (await workbenchService.GetStructureAsync(
                projectId))
            .Nodes
            .Single(node => node.Id == task.Id);
        var currentState =
            ProjectStructureTaskEditStatePolicy.Read(currentTask);
        var request = new ProjectStructureTaskDetailsUpdateRequest(
            new GanttTaskId(task.Id),
            "Stale title",
            "Proposed title",
            Math.Clamp(task.ProgressPercent, 0, 100),
            60,
            currentState.Estimate,
            currentState.Estimate,
            ScheduleChange: null,
            AssigneeChanged: true,
            ProposedAssignee: new ProjectStructureTaskResourceSelection(
                ProjectStructureTaskResourceKind.Person,
                janeId),
            CurrentExecution: currentState.Execution,
            ProposedExecution: currentState.Execution,
            CurrentCostBasis: currentState.CostBasis,
            CurrentDirectAssignmentRevision:
                currentState.DirectAssignmentRevision);

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
        var detailsService = CreateDetailsService(
            harness.Context.Services,
            failingBridge);
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
        var directAssignmentRevision =
            await ReadDirectAssignmentRevisionAsync(
                workbenchService,
                projectId,
                task.Id);
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
                janeId),
            CurrentExecution: ProjectTaskExecutionSnapshot.NotStarted,
            ProposedExecution: ProjectTaskExecutionSnapshot.NotStarted,
            CurrentCostBasis: null,
            CurrentDirectAssignmentRevision:
                directAssignmentRevision);
        failingBridge.FailReplacement = true;

        var exception = await Assert.ThrowsAsync<ProjectStructureTaskDetailsException>(() =>
            detailsService.UpdateAsync(projectId, request));

        Assert.Equal(ProjectStructureTaskDetailsErrorCode.AssignmentCompensationFailed, exception.Code);
        var applicationException =
            Assert.IsType<ProjectStructureTaskApplicationException>(
                exception.InnerException);
        Assert.IsType<AggregateException>(
            applicationException.InnerException);
        var assignment = Assert.Single(await innerBridge.ListAssignmentsDetailedAsync(projectId), item =>
            item.NodeKey == task.Id && item.Role == ProjectPartyAssignmentRole.WorkItemAssignee);
        Assert.Equal(janeId, assignment.PartyId);
        var refreshed = (await workbenchService.GetStructureAsync(projectId)).Nodes.Single(node => node.Id == task.Id);
        Assert.Equal("Jane Doe", ProjectObjectMetadataSerializer.Parse(refreshed.MetadataJson).WorkItem!.AssigneePartyDisplayName);
        Assert.Equal("Authoritative title", refreshed.Title);
    }

    [Fact]
    public async Task Assignment_change_rejects_stale_snapshot_without_erasing_concurrently_added_assignee()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var services = harness.Context.Services;
        var projectsService = services.GetRequiredService<ProjectsService>();
        var partyDirectoryService = services.GetRequiredService<PartyDirectoryService>();
        var workbenchService = services.GetRequiredService<ProjectWorkbenchService>();
        var innerBridge = services.GetRequiredService<IProjectPartyIntegrationBridge>();
        var projectId = await CreateProjectAsync(projectsService);
        var currentPersonId = await CreatePartyAsync(partyDirectoryService, "Current owner", PartyType.Person);
        var proposedPersonId = await CreatePartyAsync(partyDirectoryService, "Proposed owner", PartyType.Person);
        var concurrentAgentId = await CreatePartyAsync(partyDirectoryService, "Concurrent agent", PartyType.AiAgent);
        var task = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.WorkItem,
                "Concurrent assignment task",
                string.Empty,
                string.Empty,
                $"project:{projectId}",
                420,
                260,
                ObjectSubtype: "task"));
        var initialAssigneeService = services.GetRequiredService<ProjectStructureWorkItemAssigneeService>();
        await initialAssigneeService.ReplaceAsync(
            projectId,
            task.Id,
            new ProjectStructureTaskResourceSelection(
                ProjectStructureTaskResourceKind.Person,
                currentPersonId),
            "task-details-tests");
        var directAssignmentRevision =
            await ReadDirectAssignmentRevisionAsync(
                workbenchService,
                projectId,
                task.Id);
        var guardedBridge = new ConcurrentAssignmentBridge(
            innerBridge,
            new ProjectPartyAssignmentUpsertRequest
            {
                ProjectId = projectId,
                PartyId = concurrentAgentId,
                Role = ProjectPartyAssignmentRole.WorkItemAssignee,
                NodeKey = task.Id,
                IsPrimary = false,
                Source = "concurrent-agent-assignment"
            });
        var detailsService = CreateDetailsService(
            services,
            guardedBridge);
        var estimate = ProjectTaskEstimate.Empty();
        var execution = ProjectTaskExecutionSnapshot.NotStarted;
        var request = new ProjectStructureTaskDetailsUpdateRequest(
            new GanttTaskId(task.Id),
            task.Title,
            task.Title,
            Math.Clamp(task.ProgressPercent, 0, 100),
            Math.Clamp(task.ProgressPercent, 0, 100),
            estimate,
            estimate,
            ScheduleChange: null,
            AssigneeChanged: true,
            ProposedAssignee: new ProjectStructureTaskResourceSelection(
                ProjectStructureTaskResourceKind.Person,
                proposedPersonId),
            CurrentExecution: execution,
            ProposedExecution: execution,
            CurrentCostBasis: null,
            CurrentDirectAssignmentRevision:
                directAssignmentRevision);

        var exception = await Assert.ThrowsAsync<ProjectStructureTaskDetailsException>(() =>
            detailsService.UpdateAsync(projectId, request));

        Assert.Equal(
            ProjectStructureTaskDetailsErrorCode.AssignmentConflict,
            exception.Code);
        var assignments = (await innerBridge.ListAssignmentsDetailedAsync(
                projectId,
                [ProjectPartyAssignmentRole.WorkItemAssignee]))
            .Where(assignment => assignment.NodeKey == task.Id)
            .ToArray();
        Assert.Equal(2, assignments.Length);
        Assert.Contains(assignments, assignment => assignment.PartyId == currentPersonId);
        Assert.Contains(assignments, assignment => assignment.PartyId == concurrentAgentId);
        Assert.DoesNotContain(assignments, assignment => assignment.PartyId == proposedPersonId);
    }

    [Fact]
    public async Task Stale_direct_assignment_revision_is_a_safe_concurrency_conflict()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var services = harness.Context.Services;
        var projectsService = services.GetRequiredService<ProjectsService>();
        var partyDirectoryService = services.GetRequiredService<PartyDirectoryService>();
        var workbenchService = services.GetRequiredService<ProjectWorkbenchService>();
        var bridge = services.GetRequiredService<IProjectPartyIntegrationBridge>();
        var detailsService =
            services.GetRequiredService<ProjectStructureTaskDetailsService>();
        var projectId = await CreateProjectAsync(projectsService);
        var currentPersonId = await CreatePartyAsync(
            partyDirectoryService,
            "Commit owner",
            PartyType.Person);
        var task = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.WorkItem,
                "Assignment snapshot title",
                string.Empty,
                string.Empty,
                $"project:{projectId}",
                420,
                260,
                ObjectSubtype: "task"));
        var initialAssigneeService = services.GetRequiredService<ProjectStructureWorkItemAssigneeService>();
        await initialAssigneeService.ReplaceAsync(
            projectId,
            task.Id,
            new ProjectStructureTaskResourceSelection(
                ProjectStructureTaskResourceKind.Person,
                currentPersonId),
            "task-details-tests");
        var currentTask = (await workbenchService.GetStructureAsync(
                projectId))
            .Nodes
            .Single(node => node.Id == task.Id);
        var currentState =
            ProjectStructureTaskEditStatePolicy.Read(currentTask);
        Assert.True(currentState.DirectAssignmentRevision > 0);

        var exception = await Assert.ThrowsAsync<ProjectStructureTaskDetailsException>(() =>
            detailsService.UpdateAsync(
                projectId,
                new ProjectStructureTaskDetailsUpdateRequest(
                    new GanttTaskId(task.Id),
                    task.Title,
                    "Title that must not be committed",
                    Math.Clamp(task.ProgressPercent, 0, 100),
                    Math.Clamp(task.ProgressPercent, 0, 100),
                    currentState.Estimate,
                    currentState.Estimate,
                    ScheduleChange: null,
                    AssigneeChanged: false,
                    ProposedAssignee: null,
                    CurrentExecution: currentState.Execution,
                    ProposedExecution: currentState.Execution,
                    CurrentCostBasis: currentState.CostBasis,
                    CurrentDirectAssignmentRevision:
                        currentState.DirectAssignmentRevision - 1)));

        Assert.Equal(
            ProjectStructureTaskDetailsErrorCode.ConcurrencyConflict,
            exception.Code);
        Assert.Equal(
            currentPersonId,
            Assert.Single(await bridge.ListAssignmentsDetailedAsync(
                    projectId,
                    [ProjectPartyAssignmentRole.WorkItemAssignee]),
                assignment => assignment.NodeKey == task.Id).PartyId);
        var persistedTask = (await workbenchService.GetStructureAsync(projectId))
            .Nodes
            .Single(node => node.Id == task.Id);
        Assert.Equal(task.Title, persistedTask.Title);
    }

    [Fact]
    public async Task Stale_estimate_execution_and_cost_basis_are_concurrency_conflicts()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var services = harness.Context.Services;
        var projectsService = services.GetRequiredService<ProjectsService>();
        var workbenchService = services.GetRequiredService<ProjectWorkbenchService>();
        var detailsService = services.GetRequiredService<ProjectStructureTaskDetailsService>();
        var projectId = await CreateProjectAsync(projectsService);
        var currentEstimate = new ProjectTaskEstimate(
            8m,
            ProjectWorkItemEffortUnit.Hours,
            100m,
            "USD");
        var task = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.WorkItem,
                "Concurrency snapshot task",
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
                            ExecutionState = ProjectTaskExecutionState.Unknown,
                            ExpectedEffortHours = currentEstimate.ExpectedEffortHours,
                            ExpectedEffortUnit = currentEstimate.ExpectedEffortUnit,
                            ExpectedCostAmount = currentEstimate.ExpectedCostAmount,
                            ExpectedCostCurrencyCode = currentEstimate.ExpectedCostCurrencyCode
                        }
                    })));
        var baseRequest = new ProjectStructureTaskDetailsUpdateRequest(
            new GanttTaskId(task.Id),
            task.Title,
            task.Title,
            Math.Clamp(task.ProgressPercent, 0, 100),
            Math.Clamp(task.ProgressPercent, 0, 100),
            currentEstimate,
            currentEstimate,
            ScheduleChange: null,
            AssigneeChanged: false,
            ProposedAssignee: null,
            CurrentExecution: ProjectTaskExecutionSnapshot.NotStarted,
            ProposedExecution: ProjectTaskExecutionSnapshot.NotStarted,
            CurrentCostBasis: null,
            CurrentDirectAssignmentRevision: 0);
        var staleRequests = new[]
        {
            baseRequest with
            {
                CurrentEstimate = currentEstimate with
                {
                    ExpectedEffortHours = 4m
                }
            },
            baseRequest with
            {
                CurrentExecution = ProjectTaskExecutionSnapshot.Unknown,
                ProposedExecution = ProjectTaskExecutionSnapshot.Unknown
            },
            baseRequest with
            {
                CurrentCostBasis = new ProjectTaskExpectedCostBasis
                {
                    ResourceKind =
                        ProjectStructureTaskResourceKind.Person,
                    ResourceId =
                        Guid.Parse(
                            "70000000-0000-0000-0000-000000000007"),
                    Source = ProjectStructureTaskResourceCostSource
                        .CrmWorkforceRate,
                    CalculatedAtUtc =
                        DateTimeOffset.Parse(
                            "2026-07-23T17:00:00Z")
                }
            }
        };

        foreach (var staleRequest in staleRequests)
        {
            var exception = await Assert.ThrowsAsync<ProjectStructureTaskDetailsException>(() =>
                detailsService.UpdateAsync(projectId, staleRequest));

            Assert.Equal(ProjectStructureTaskDetailsErrorCode.ConcurrencyConflict, exception.Code);
            Assert.Contains(
                "pricing, execution state, or direct assignments",
                exception.Message,
                StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Not_started_update_refreshes_cost_and_preserves_mixed_assignments()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var services = harness.Context.Services;
        var projectsService = services.GetRequiredService<ProjectsService>();
        var partyDirectoryService = services.GetRequiredService<PartyDirectoryService>();
        var workbenchService = services.GetRequiredService<ProjectWorkbenchService>();
        var bridge = services.GetRequiredService<IProjectPartyIntegrationBridge>();
        var pricingStrategy = new FixedQuoteStrategy(
            new ProjectStructureTaskResourceCostQuote(
                ProjectStructureTaskResourceCostQuoteStatus.Available,
                80m,
                "USD",
                "CRM workforce rate",
                "Fresh CRM rate for the updated effort.",
                DateTimeOffset.Parse("2026-07-23T16:00:00Z"),
                ProjectStructureTaskResourceCostSource.CrmWorkforceRate));
        var detailsService = CreateDetailsService(
            services,
            bridge,
            new ProjectStructureTaskEstimateRefreshService(
                new ProjectStructureTaskResourceCostService([pricingStrategy])));
        var projectId = await CreateProjectAsync(projectsService);
        var personId = await CreatePartyAsync(partyDirectoryService, "Primary owner", PartyType.Person);
        var agentId = await CreatePartyAsync(partyDirectoryService, "Supporting agent", PartyType.AiAgent);
        var startUtc = new DateTimeOffset(2026, 7, 15, 8, 0, 0, TimeSpan.Zero);
        var staleEstimate = new ProjectTaskEstimate(
            8m,
            ProjectWorkItemEffortUnit.Hours,
            999m,
            "EUR");
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
                        ExecutionState = ProjectTaskExecutionState.NotStarted,
                        ExpectedEffortHours = staleEstimate.ExpectedEffortHours,
                        ExpectedEffortUnit = staleEstimate.ExpectedEffortUnit,
                        ExpectedCostAmount = staleEstimate.ExpectedCostAmount,
                        ExpectedCostCurrencyCode = staleEstimate.ExpectedCostCurrencyCode
                    }
                })));
        var assignmentResult = await bridge.ReplaceNodeAssignmentsAsync(
            projectId,
            new ProjectNodeReference(task.Id),
            [
                new ProjectPartyAssignmentUpsertRequest
                {
                    ProjectId = projectId,
                    PartyId = personId,
                    Role = ProjectPartyAssignmentRole.WorkItemAssignee,
                    NodeKey = task.Id,
                    IsPrimary = true,
                    Source = "mixed-assignment-test"
                },
                new ProjectPartyAssignmentUpsertRequest
                {
                    ProjectId = projectId,
                    PartyId = agentId,
                    Role = ProjectPartyAssignmentRole.WorkItemAssignee,
                    NodeKey = task.Id,
                    IsPrimary = false,
                    Source = "mixed-assignment-test"
                }
            ],
            [ProjectPartyAssignmentRole.WorkItemAssignee]);
        Assert.True(assignmentResult.IsSuccess);
        var currentTask = (await workbenchService.GetStructureAsync(
                projectId))
            .Nodes
            .Single(node => node.Id == task.Id);
        var currentState =
            ProjectStructureTaskEditStatePolicy.Read(currentTask);
        var assignmentsBefore = (await bridge.ListAssignmentsDetailedAsync(projectId))
            .Where(assignment =>
                assignment.NodeKey == task.Id &&
                assignment.Role == ProjectPartyAssignmentRole.WorkItemAssignee)
            .OrderBy(static assignment => assignment.Id)
            .ToArray();
        var execution = ProjectTaskExecutionSnapshot.NotStarted;

        var update = await detailsService.UpdateWithPricingAsync(
            projectId,
            new ProjectStructureTaskDetailsUpdateRequest(
                new GanttTaskId(task.Id),
                task.Title,
                "Re-estimated title",
                Math.Clamp(task.ProgressPercent, 0, 100),
                Math.Clamp(task.ProgressPercent, 0, 100),
                currentState.Estimate,
                currentState.Estimate with
                {
                    ExpectedEffortHours = 4m
                },
                ScheduleChange: null,
                AssigneeChanged: false,
                ProposedAssignee: null,
                CurrentExecution: execution,
                ProposedExecution: execution,
                CurrentCostBasis: currentState.CostBasis,
                CurrentDirectAssignmentRevision:
                    currentState.DirectAssignmentRevision));

        var refreshedTask = (await workbenchService.GetStructureAsync(projectId))
            .Nodes
            .Single(node => node.Id == task.Id);
        var metadata = ProjectObjectMetadataSerializer.Parse(refreshedTask.MetadataJson).WorkItem;
        var assignmentsAfter = (await bridge.ListAssignmentsDetailedAsync(projectId))
            .Where(assignment =>
                assignment.NodeKey == task.Id &&
                assignment.Role == ProjectPartyAssignmentRole.WorkItemAssignee)
            .OrderBy(static assignment => assignment.Id)
            .ToArray();

        Assert.Equal(ProjectStructureTaskEstimateRefreshStatus.Refreshed, update.Pricing.Status);
        Assert.Equal(4m, pricingStrategy.LastRequest?.Estimate.ExpectedEffortHours);
        Assert.Equal(
            new ProjectStructureTaskResourceSelection(
                ProjectStructureTaskResourceKind.Person,
                personId),
            pricingStrategy.LastRequest?.Resource);
        Assert.NotNull(metadata);
        Assert.Equal(4m, metadata!.ExpectedEffortHours);
        Assert.Equal(80m, metadata.ExpectedCostAmount);
        Assert.Equal("USD", metadata.ExpectedCostCurrencyCode);
        Assert.Equal(personId, metadata.ExpectedCostBasis?.ResourceId);
        Assert.Equal(assignmentsBefore, assignmentsAfter);
        Assert.Equal(2, assignmentsAfter.Length);
    }

    [Fact]
    public async Task Not_started_update_clears_authoritative_person_price_when_the_resource_is_missing()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var services = harness.Context.Services;
        var projectsService = services.GetRequiredService<ProjectsService>();
        var workbenchService = services.GetRequiredService<ProjectWorkbenchService>();
        var detailsService = services.GetRequiredService<ProjectStructureTaskDetailsService>();
        var projectId = await CreateProjectAsync(projectsService);
        var estimate = new ProjectTaskEstimate(
            8m,
            ProjectWorkItemEffortUnit.Hours,
            999m,
            "EUR");
        var costBasis = new ProjectTaskExpectedCostBasis
        {
            ResourceKind = ProjectStructureTaskResourceKind.Person,
            ResourceId = Guid.Parse("80000000-0000-0000-0000-000000000018"),
            Source = ProjectStructureTaskResourceCostSource.CrmWorkforceRate,
            CalculatedAtUtc = DateTimeOffset.Parse("2026-07-23T18:00:00Z")
        };
        var task = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.WorkItem,
                "Missing pricing resource task",
                string.Empty,
                string.Empty,
                $"project:{projectId}",
                420,
                260,
                ObjectSubtype: "task"));
        await workbenchService.MutateObjectMetadataSerializableAsync(
            projectId,
            task.Id,
            metadata =>
            {
                metadata.WorkItem ??= new ProjectWorkItemMetadata
                {
                    WorkItemKind = ProjectWorkItemKind.Task
                };
                metadata.WorkItem.ExecutionState = ProjectTaskExecutionState.NotStarted;
                metadata.WorkItem.ExpectedEffortHours = estimate.ExpectedEffortHours;
                metadata.WorkItem.ExpectedEffortUnit = estimate.ExpectedEffortUnit;
                metadata.WorkItem.ExpectedCostAmount = estimate.ExpectedCostAmount;
                metadata.WorkItem.ExpectedCostCurrencyCode = estimate.ExpectedCostCurrencyCode;
                metadata.WorkItem.ExpectedCostBasis = costBasis;
            });

        var update = await detailsService.UpdateWithPricingAsync(
            projectId,
            new ProjectStructureTaskDetailsUpdateRequest(
                new GanttTaskId(task.Id),
                task.Title,
                task.Title,
                Math.Clamp(task.ProgressPercent, 0, 100),
                Math.Clamp(task.ProgressPercent, 0, 100),
                estimate,
                estimate,
                ScheduleChange: null,
                AssigneeChanged: false,
                ProposedAssignee: null,
                CurrentExecution: ProjectTaskExecutionSnapshot.NotStarted,
                ProposedExecution: ProjectTaskExecutionSnapshot.NotStarted,
                CurrentCostBasis: costBasis,
                CurrentDirectAssignmentRevision: 0));

        Assert.Equal(ProjectStructureTaskEstimateRefreshStatus.Cleared, update.Pricing.Status);
        Assert.Equal(
            ProjectStructureTaskEstimateRefreshReason.AuthoritativeResourceRemoved,
            update.Pricing.Reason);
        var refreshedTask = (await workbenchService.GetStructureAsync(projectId))
            .Nodes
            .Single(node => node.Id == task.Id);
        var metadata = ProjectObjectMetadataSerializer.Parse(refreshedTask.MetadataJson).WorkItem;
        Assert.NotNull(metadata);
        Assert.Equal(8m, metadata!.ExpectedEffortHours);
        Assert.Null(metadata.ExpectedCostAmount);
        Assert.Empty(metadata.ExpectedCostCurrencyCode);
        Assert.Null(metadata.ExpectedCostBasis);
    }

    [Fact]
    public async Task Started_and_completed_updates_preserve_cost_and_currency_while_effort_changes()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var services = harness.Context.Services;
        var projectsService = services.GetRequiredService<ProjectsService>();
        var workbenchService = services.GetRequiredService<ProjectWorkbenchService>();
        var detailsService = services.GetRequiredService<ProjectStructureTaskDetailsService>();
        var projectId = await CreateProjectAsync(projectsService);
        var startedAtUtc = new DateTimeOffset(2026, 7, 15, 8, 0, 0, TimeSpan.Zero);
        var snapshots = new[]
        {
            new ProjectTaskExecutionSnapshot(
                ProjectTaskExecutionState.Started,
                startedAtUtc,
                null),
            new ProjectTaskExecutionSnapshot(
                ProjectTaskExecutionState.Completed,
                startedAtUtc,
                startedAtUtc.AddHours(2))
        };
        var currentEstimate = new ProjectTaskEstimate(
            8m,
            ProjectWorkItemEffortUnit.Hours,
            999m,
            "EUR");

        foreach (var snapshot in snapshots)
        {
            var task = await workbenchService.CreateObjectAsync(
                projectId,
                new ProjectObjectCreateRequest(
                    ProjectObjectType.WorkItem,
                    $"{snapshot.State} task",
                    string.Empty,
                    string.Empty,
                    $"project:{projectId}",
                    420,
                    260,
                    StartUtc: startedAtUtc,
                    EndUtc: startedAtUtc.AddDays(1),
                    ObjectSubtype: "task",
                    MetadataJson: ProjectObjectMetadataSerializer.Serialize(new ProjectObjectMetadataEnvelope
                    {
                        WorkItem = new ProjectWorkItemMetadata
                        {
                            WorkItemKind = ProjectWorkItemKind.Task,
                            ExecutionState = snapshot.State,
                            ActualStartedAtUtc = snapshot.ActualStartedAtUtc,
                            ActualEndedAtUtc = snapshot.ActualEndedAtUtc,
                            ExpectedEffortHours = currentEstimate.ExpectedEffortHours,
                            ExpectedEffortUnit = currentEstimate.ExpectedEffortUnit,
                            ExpectedCostAmount = currentEstimate.ExpectedCostAmount,
                            ExpectedCostCurrencyCode = currentEstimate.ExpectedCostCurrencyCode
                        }
                    })));
            await workbenchService.MutateObjectMetadataSerializableAsync(
                projectId,
                task.Id,
                metadata =>
                {
                    metadata.WorkItem ??= new ProjectWorkItemMetadata
                    {
                        WorkItemKind = ProjectWorkItemKind.Task
                    };
                    metadata.WorkItem.ExecutionState = snapshot.State;
                    metadata.WorkItem.ActualStartedAtUtc = snapshot.ActualStartedAtUtc;
                    metadata.WorkItem.ActualEndedAtUtc = snapshot.ActualEndedAtUtc;
                    metadata.WorkItem.ExpectedEffortHours = currentEstimate.ExpectedEffortHours;
                    metadata.WorkItem.ExpectedEffortUnit = currentEstimate.ExpectedEffortUnit;
                    metadata.WorkItem.ExpectedCostAmount = currentEstimate.ExpectedCostAmount;
                    metadata.WorkItem.ExpectedCostCurrencyCode = currentEstimate.ExpectedCostCurrencyCode;
                });

            await detailsService.UpdateWithPricingAsync(
                projectId,
                new ProjectStructureTaskDetailsUpdateRequest(
                    new GanttTaskId(task.Id),
                    task.Title,
                    task.Title,
                    Math.Clamp(task.ProgressPercent, 0, 100),
                    Math.Clamp(task.ProgressPercent, 0, 100),
                    currentEstimate,
                    new ProjectTaskEstimate(
                        4m,
                        ProjectWorkItemEffortUnit.Hours,
                        1m,
                        "JPY"),
                    ScheduleChange: null,
                    AssigneeChanged: false,
                    ProposedAssignee: null,
                    CurrentExecution: snapshot,
                    ProposedExecution: snapshot,
                    CurrentCostBasis: null,
                    CurrentDirectAssignmentRevision: 0));

            var refreshedTask = (await workbenchService.GetStructureAsync(projectId))
                .Nodes
                .Single(node => node.Id == task.Id);
            var metadata = ProjectObjectMetadataSerializer.Parse(refreshedTask.MetadataJson).WorkItem;

            Assert.NotNull(metadata);
            Assert.Equal(4m, metadata!.ExpectedEffortHours);
            Assert.Equal(999m, metadata.ExpectedCostAmount);
            Assert.Equal("EUR", metadata.ExpectedCostCurrencyCode);
        }
    }

    private static ProjectStructureTaskDetailsService CreateDetailsService(
        IServiceProvider services,
        IProjectPartyIntegrationBridge bridge,
        ProjectStructureTaskEstimateRefreshService? estimateRefreshService = null)
    {
        var workbenchService =
            services.GetRequiredService<ProjectWorkbenchService>();
        var taskApplicationService =
            new ProjectStructureTaskApplicationService(
                new ProjectStructureWorkItemAssigneeService(
                    bridge,
                    workbenchService),
                estimateRefreshService ??
                    services.GetRequiredService<
                        ProjectStructureTaskEstimateRefreshService>(),
                services.GetRequiredService<
                    ProjectStructureTaskEditCompensationService>(),
                workbenchService,
                NullLogger<ProjectStructureTaskApplicationService>.Instance);
        return new ProjectStructureTaskDetailsService(
            services.GetRequiredService<
                ProjectStructureGanttMutationService>(),
            taskApplicationService);
    }

    private static async Task<long> ReadDirectAssignmentRevisionAsync(
        ProjectWorkbenchService workbenchService,
        Guid projectId,
        string taskNodeId)
    {
        var task = (await workbenchService.GetStructureAsync(projectId))
            .Nodes
            .Single(node => node.Id == taskNodeId);
        return ProjectObjectMetadataSerializer.Parse(task.MetadataJson)
            .WorkItem?
            .DirectAssignmentRevision ?? 0;
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

    private static Task<Guid> CreatePersonAsync(
        PartyDirectoryService partyDirectoryService,
        string displayName)
        => CreatePartyAsync(partyDirectoryService, displayName, PartyType.Person);

    private static async Task<Guid> CreatePartyAsync(
        PartyDirectoryService partyDirectoryService,
        string displayName,
        PartyType partyType)
    {
        var result = await partyDirectoryService.SavePartyAsync(new PartyEditorModel
        {
            PartyType = partyType,
            LifecycleStatus = PartyLifecycleStatus.Active,
            DisplayName = displayName,
            Summary = $"{displayName} task-details test record.",
            LastChangedBy = "component-tests"
        });
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private sealed class FixedQuoteStrategy(
        ProjectStructureTaskResourceCostQuote quote)
        : IProjectStructureTaskResourceCostStrategy
    {
        public ProjectStructureTaskResourceKind Kind => ProjectStructureTaskResourceKind.Person;

        public ProjectStructureTaskResourceCostRequest? LastRequest { get; private set; }

        public Task<ProjectStructureTaskResourceCostQuote> GetQuoteAsync(
            ProjectStructureTaskResourceCostRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(quote);
        }
    }

    private static DateOnly? ToDateOnly(DateTimeOffset? value)
        => value.HasValue ? DateOnly.FromDateTime(value.Value.UtcDateTime) : null;

    private sealed class CompensationFailingBridge(IProjectPartyIntegrationBridge inner)
        : DelegatingProjectPartyIntegrationBridge(inner)
    {
        private int conditionalReplacementCount;

        public bool FailReplacement { get; set; }

        public override Task<Result> ReplaceNodeAssignmentsAsync(
            Guid projectId,
            ProjectNodeReference nodeReference,
            IReadOnlyList<ProjectPartyAssignmentUpsertRequest> desiredAssignments,
            IReadOnlyList<ProjectPartyAssignmentRole> targetRoles,
            CancellationToken cancellationToken = default)
            => base.ReplaceNodeAssignmentsAsync(
                projectId,
                nodeReference,
                desiredAssignments,
                targetRoles,
                cancellationToken);

        public override Task<Result> ReplaceNodeAssignmentsIfCurrentAsync(
            Guid projectId,
            ProjectNodeReference nodeReference,
            IReadOnlyList<ProjectPartyAssignmentUpsertRequest> desiredAssignments,
            IReadOnlyList<ProjectPartyAssignmentRole> targetRoles,
            IReadOnlyCollection<ProjectPartyAssignmentConcurrencySnapshot> expectedAssignments,
            ProjectWorkItemDirectAssignmentRevision? expectedDirectAssignmentRevision,
            CancellationToken cancellationToken = default)
        {
            conditionalReplacementCount++;
            if (FailReplacement &&
                conditionalReplacementCount > 1)
            {
                return Task.FromResult(Result.Failure(Error.Failure(
                    "Injected assignment compensation failure.",
                    "test.assignment-compensation-failure")));
            }

            return base.ReplaceNodeAssignmentsIfCurrentAsync(
                projectId,
                nodeReference,
                desiredAssignments,
                targetRoles,
                expectedAssignments,
                expectedDirectAssignmentRevision,
                cancellationToken);
        }
    }

    private sealed class ConcurrentAssignmentBridge(
        IProjectPartyIntegrationBridge inner,
        ProjectPartyAssignmentUpsertRequest concurrentAssignment)
        : DelegatingProjectPartyIntegrationBridge(inner)
    {
        private bool assignmentInjected;

        public override async Task<Result> ReplaceNodeAssignmentsIfCurrentAsync(
            Guid projectId,
            ProjectNodeReference nodeReference,
            IReadOnlyList<ProjectPartyAssignmentUpsertRequest> desiredAssignments,
            IReadOnlyList<ProjectPartyAssignmentRole> targetRoles,
            IReadOnlyCollection<ProjectPartyAssignmentConcurrencySnapshot> expectedAssignments,
            ProjectWorkItemDirectAssignmentRevision? expectedDirectAssignmentRevision,
            CancellationToken cancellationToken = default)
        {
            if (!assignmentInjected)
            {
                assignmentInjected = true;
                var saveResult = await Inner.SaveAssignmentAsync(concurrentAssignment, cancellationToken);
                if (saveResult.IsFailure)
                {
                    return Result.Failure(saveResult.Errors);
                }
            }

            return await base.ReplaceNodeAssignmentsIfCurrentAsync(
                projectId,
                nodeReference,
                desiredAssignments,
                targetRoles,
                expectedAssignments,
                expectedDirectAssignmentRevision,
                cancellationToken);
        }
    }

    private abstract class DelegatingProjectPartyIntegrationBridge(
        IProjectPartyIntegrationBridge inner)
        : IProjectPartyIntegrationBridge
    {
        protected IProjectPartyIntegrationBridge Inner { get; } = inner;

        public Task<IReadOnlyDictionary<Guid, ProjectPortfolioPartyContext>> GetPortfolioContextsAsync(
            IReadOnlyCollection<Guid> projectIds,
            CancellationToken cancellationToken = default)
            => Inner.GetPortfolioContextsAsync(projectIds, cancellationToken);

        public Task<IReadOnlyList<ProjectPartyOption>> ListPartyOptionsAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
            => Inner.ListPartyOptionsAsync(projectId, cancellationToken);

        public Task<ProjectPartyOption?> GetPartyOptionAsync(
            Guid partyId,
            CancellationToken cancellationToken = default)
            => Inner.GetPartyOptionAsync(partyId, cancellationToken);

        public Task<IReadOnlyList<ProjectPartyAssignmentDetail>> ListAssignmentsDetailedAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
            => Inner.ListAssignmentsDetailedAsync(projectId, cancellationToken);

        public virtual Task<IReadOnlyList<ProjectPartyAssignmentDetail>> ListAssignmentsDetailedAsync(
            Guid projectId,
            IReadOnlyCollection<ProjectPartyAssignmentRole> roles,
            CancellationToken cancellationToken = default)
            => Inner.ListAssignmentsDetailedAsync(projectId, roles, cancellationToken);

        public Task<IReadOnlyList<ProjectWorkItemAssigneeBinding>> ListWorkItemAssigneeBindingsAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
            => Inner.ListWorkItemAssigneeBindingsAsync(projectId, cancellationToken);

        public Task<Result<Guid>> SaveAssignmentAsync(
            ProjectPartyAssignmentUpsertRequest request,
            CancellationToken cancellationToken = default)
            => Inner.SaveAssignmentAsync(request, cancellationToken);

        public virtual Task<Result> ReplaceNodeAssignmentsAsync(
            Guid projectId,
            ProjectNodeReference nodeReference,
            IReadOnlyList<ProjectPartyAssignmentUpsertRequest> desiredAssignments,
            IReadOnlyList<ProjectPartyAssignmentRole> targetRoles,
            CancellationToken cancellationToken = default)
            => Inner.ReplaceNodeAssignmentsAsync(
                projectId,
                nodeReference,
                desiredAssignments,
                targetRoles,
                cancellationToken);

        public virtual Task<Result> ReplaceNodeAssignmentsIfCurrentAsync(
            Guid projectId,
            ProjectNodeReference nodeReference,
            IReadOnlyList<ProjectPartyAssignmentUpsertRequest> desiredAssignments,
            IReadOnlyList<ProjectPartyAssignmentRole> targetRoles,
            IReadOnlyCollection<ProjectPartyAssignmentConcurrencySnapshot> expectedAssignments,
            ProjectWorkItemDirectAssignmentRevision? expectedDirectAssignmentRevision,
            CancellationToken cancellationToken = default)
            => Inner.ReplaceNodeAssignmentsIfCurrentAsync(
                projectId,
                nodeReference,
                desiredAssignments,
                targetRoles,
                expectedAssignments,
                expectedDirectAssignmentRevision,
                cancellationToken);

        public Task DeleteAssignmentAsync(
            Guid assignmentId,
            CancellationToken cancellationToken = default)
            => Inner.DeleteAssignmentAsync(assignmentId, cancellationToken);

        public Task DeleteAssignmentsForNodesAsync(
            Guid projectId,
            IReadOnlyCollection<ProjectNodeReference> nodeReferences,
            CancellationToken cancellationToken = default)
            => Inner.DeleteAssignmentsForNodesAsync(projectId, nodeReferences, cancellationToken);

        public Task MoveAssignmentsToProjectAsync(
            Guid sourceProjectId,
            IReadOnlyCollection<ProjectNodeReference> nodeReferences,
            Guid targetProjectId,
            CancellationToken cancellationToken = default)
            => Inner.MoveAssignmentsToProjectAsync(
                sourceProjectId,
                nodeReferences,
                targetProjectId,
                cancellationToken);

        public Task<Result<ProjectPartyQuickCreateResult>> CreatePartyAsync(
            ProjectPartyQuickCreateRequest request,
            CancellationToken cancellationToken = default)
            => Inner.CreatePartyAsync(request, cancellationToken);
    }
}
