using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.ProjectStructure;

public sealed class ProjectPartyAssignmentIntegrationTests
{
    [Fact]
    public async Task Bridge_persists_project_and_node_assignments_and_enriches_portfolio_context()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var bridge = scope.ServiceProvider.GetRequiredService<IProjectPartyIntegrationBridge>();

        var projectId = await CreateProjectAsync(projectsService, "B10 Integration Project");
        var customerId = await CreatePartyAsync(partyDirectoryService, PartyType.Organization, "Acme Customer");
        var deliveryUnitId = await CreatePartyAsync(partyDirectoryService, PartyType.OrganizationUnit, "Platform Guild");
        var ownerId = await CreatePartyAsync(partyDirectoryService, PartyType.Person, "Morgan Owner");

        Assert.True((await bridge.SaveAssignmentAsync(new ProjectPartyAssignmentUpsertRequest
        {
            ProjectId = projectId,
            PartyId = customerId,
            Role = ProjectPartyAssignmentRole.Customer,
            IsPrimary = true,
            Source = "integration-tests"
        })).IsSuccess);
        Assert.True((await bridge.SaveAssignmentAsync(new ProjectPartyAssignmentUpsertRequest
        {
            ProjectId = projectId,
            PartyId = deliveryUnitId,
            Role = ProjectPartyAssignmentRole.DeliveryUnit,
            IsPrimary = true,
            AllocationPercent = 70m,
            StartsOn = new DateOnly(2026, 7, 1),
            EndsOn = new DateOnly(2026, 7, 31),
            Source = "integration-tests"
        })).IsSuccess);
        Assert.True((await bridge.SaveAssignmentAsync(new ProjectPartyAssignmentUpsertRequest
        {
            ProjectId = projectId,
            PartyId = ownerId,
            Role = ProjectPartyAssignmentRole.Manager,
            IsPrimary = true,
            NodeKey = "work-item-alpha",
            Source = "integration-tests"
        })).IsSuccess);

        var quickCreate = await bridge.CreatePartyAsync(new ProjectPartyQuickCreateRequest
        {
            ProjectId = projectId,
            PartyKind = ProjectPartyQuickCreateKind.AiAgent,
            DisplayName = "Review Agent",
            Summary = "Assists with structured review."
        });

        Assert.True(quickCreate.IsSuccess);
        var createdParty = quickCreate.Value;
        Assert.NotNull(createdParty);

        var detailedAssignments = await bridge.ListAssignmentsDetailedAsync(projectId);
        Assert.Equal(3, detailedAssignments.Count);
        Assert.Contains(detailedAssignments, item => item.Role == ProjectPartyAssignmentRole.Customer && item.PartyDisplayName == "Acme Customer");
        Assert.Contains(detailedAssignments, item =>
            item.Role == ProjectPartyAssignmentRole.DeliveryUnit &&
            item.AllocationPercent == 70m &&
            item.StartsAtUtc == new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero) &&
            item.EndsAtUtc == new DateTimeOffset(2026, 7, 31, 0, 0, 0, TimeSpan.Zero));
        Assert.Contains(detailedAssignments, item => item.Role == ProjectPartyAssignmentRole.Manager && item.NodeKey == "work-item-alpha");

        var contexts = await bridge.GetPortfolioContextsAsync([projectId]);
        var context = Assert.Single(contexts).Value;
        Assert.Equal("Acme Customer", context.PrimaryCustomerName);
        Assert.Equal("Platform Guild", context.PrimaryDeliveryUnitName);
        Assert.Contains("Acme Customer", context.SearchText, StringComparison.Ordinal);

        var options = await bridge.ListPartyOptionsAsync(projectId);
        Assert.Contains(options, item =>
            item.PartyId == createdParty!.PartyId &&
            item.PartyType == ProjectPartyType.AiAgent &&
            item.PartyTypeLabel == "AI agent");
    }

    [Fact]
    public async Task Detailed_assignment_search_pages_the_server_result_and_rejects_unbounded_page_sizes()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var assignments = scope.ServiceProvider.GetRequiredService<ProjectPartyIntegrationService>();

        var projectId = await CreateProjectAsync(projectsService, "Bounded assignment search project");
        for (var index = 0; index < 13; index++)
        {
            var partyId = await CreatePartyAsync(
                partyDirectoryService,
                PartyType.Person,
                $"Paged resource {index:D2}");
            var result = await assignments.SaveAssignmentAsync(new ProjectPartyAssignmentUpsertRequest
            {
                ProjectId = projectId,
                PartyId = partyId,
                Role = ProjectPartyAssignmentRole.TeamMember,
                AllocationPercent = 50m,
                StartsOn = new DateOnly(2026, 7, 1),
                EndsOn = new DateOnly(2026, 7, 31),
                Source = "integration-tests"
            });

            Assert.True(result.IsSuccess);
        }

        var page = await assignments.SearchAssignmentsDetailedAsync(
            new ProjectPartyAssignmentQuery(
                projectId,
                [ProjectPartyAssignmentRole.TeamMember],
                "Paged resource",
                PageIndex: 1,
                PageSize: 5,
                WindowStartUtc: new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero),
                WindowEndUtc: new DateTimeOffset(2026, 7, 31, 23, 59, 59, TimeSpan.Zero),
                AllocationOnly: true));

        Assert.Equal(13, page.TotalCount);
        Assert.Equal(1, page.PageIndex);
        Assert.Equal(5, page.Items.Count);
        Assert.All(page.Items, item => Assert.Contains("Paged resource", item.PartyDisplayName));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            assignments.SearchAssignmentsDetailedAsync(
                new ProjectPartyAssignmentQuery(
                    projectId,
                    [ProjectPartyAssignmentRole.TeamMember],
                PageSize: ProjectPartyAssignmentQueryLimits.MaximumPageSize + 1)));
    }

    [Fact]
    public async Task Party_assignment_history_pages_the_server_result_and_rejects_unbounded_page_sizes()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var assignments = scope.ServiceProvider.GetRequiredService<IProjectPartyIntegrationBridge>();
        var partyId = await CreatePartyAsync(
            partyDirectoryService,
            PartyType.Person,
            "Paged assignment history party");

        for (var index = 0; index < 9; index++)
        {
            var projectId = await CreateProjectAsync(
                projectsService,
                $"Party assignment history project {index:D2}");
            var result = await assignments.SaveAssignmentAsync(new ProjectPartyAssignmentUpsertRequest
            {
                ProjectId = projectId,
                PartyId = partyId,
                Role = ProjectPartyAssignmentRole.TeamMember,
                Source = "integration-tests"
            });

            Assert.True(result.IsSuccess);
        }

        var page = await partyDirectoryService.SearchPartyProjectAssignmentsAsync(
            new PartyProjectAssignmentQuery(partyId, PageIndex: 1, PageSize: 4));

        Assert.Equal(9, page.TotalCount);
        Assert.Equal(1, page.PageIndex);
        Assert.Equal(4, page.Items.Count);
        Assert.Equal(
            [
                "Party assignment history project 04",
                "Party assignment history project 05",
                "Party assignment history project 06",
                "Party assignment history project 07"
            ],
            page.Items.Select(item => item.ProjectName));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            partyDirectoryService.SearchPartyProjectAssignmentsAsync(
                new PartyProjectAssignmentQuery(
                    partyId,
                    PageSize: PartyProjectAssignmentQueryLimits.MaximumPageSize + 1)));
    }

    [Fact]
    public async Task Node_details_bridge_reads_the_live_workbench_record()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var detailsBridge = scope.ServiceProvider.GetRequiredService<IProjectNodeDetailsBridge>();

        var projectId = await CreateProjectAsync(projectsService, "Linked detail project");
        var workItem = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.WorkItem,
                "Accessibility proof",
                "Release validation",
                "Owns export and accessibility proof.",
                null,
                420,
                240,
                null,
                null,
                "task"));

        var details = await detailsBridge.GetAsync(
            projectId,
            new ProjectNodeReference(workItem.Id));

        Assert.NotNull(details);
        Assert.Equal(projectId, details.ProjectId);
        Assert.Equal(workItem.Id, details.NodeKey);
        Assert.Equal(ProjectObjectType.WorkItem, details.ObjectType);
        Assert.Equal("task", details.ObjectSubtype);
        Assert.Equal("Accessibility proof", details.Title);
        Assert.Equal("Release validation", details.Subtitle);

        await workbench.UpdateObjectMetadataAsync(
            projectId,
            workItem.Id,
            "{}",
            status: "In progress");

        var refreshedDetails = await detailsBridge.GetAsync(
            projectId,
            new ProjectNodeReference(workItem.Id));

        Assert.NotNull(refreshedDetails);
        Assert.Equal("In progress", refreshedDetails.Status);
    }

    [Fact]
    public async Task Bridge_rejects_invalid_assignment_values_on_both_mutation_paths()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var bridge = scope.ServiceProvider.GetRequiredService<IProjectPartyIntegrationBridge>();

        var projectId = await CreateProjectAsync(projectsService, "Assignment invariant project");
        var partyId = await CreatePartyAsync(partyDirectoryService, PartyType.Person, "Invariant worker");

        var allocationResult = await bridge.SaveAssignmentAsync(new ProjectPartyAssignmentUpsertRequest
        {
            ProjectId = projectId,
            PartyId = partyId,
            Role = ProjectPartyAssignmentRole.TeamMember,
            AllocationPercent = 0m,
            Source = "integration-tests"
        });
        var dateResult = await bridge.SaveAssignmentAsync(new ProjectPartyAssignmentUpsertRequest
        {
            ProjectId = projectId,
            PartyId = partyId,
            Role = ProjectPartyAssignmentRole.TeamMember,
            AllocationPercent = 100m,
            StartsOn = new DateOnly(2026, 8, 2),
            EndsOn = new DateOnly(2026, 8, 1),
            Source = "integration-tests"
        });
        var replacementResult = await bridge.ReplaceNodeAssignmentsAsync(
            projectId,
            new ProjectNodeReference("work-item-invariant-check"),
            [new ProjectPartyAssignmentUpsertRequest
            {
                PartyId = partyId,
                Role = ProjectPartyAssignmentRole.WorkItemAssignee,
                AllocationPercent = 101m,
                Source = "integration-tests"
            }],
            [ProjectPartyAssignmentRole.WorkItemAssignee]);

        Assert.False(allocationResult.IsSuccess);
        Assert.Contains(allocationResult.Errors, error => error.Code == "crmhr.project-assignment.allocation-range");
        Assert.False(dateResult.IsSuccess);
        Assert.Contains(dateResult.Errors, error => error.Code == "crmhr.project-assignment.date-range-invalid");
        Assert.True(replacementResult.IsFailure);
        Assert.Contains(replacementResult.Errors, error => error.Code == "crmhr.project-assignment.allocation-range");
        Assert.Empty(await bridge.ListAssignmentsDetailedAsync(projectId));
    }

    [Fact]
    public async Task Bridge_validates_and_projects_the_selected_party_affiliation()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var partyDirectoryService = scope.ServiceProvider
            .GetRequiredService<PartyDirectoryService>();
        var affiliationService = scope.ServiceProvider
            .GetRequiredService<IPartyOrganizationAffiliationService>();
        var projectsService = scope.ServiceProvider
            .GetRequiredService<ProjectsService>();
        var bridge = scope.ServiceProvider
            .GetRequiredService<IProjectPartyIntegrationBridge>();

        var projectId = await CreateProjectAsync(
            projectsService,
            "Affiliation-aware assignment project");
        var organizationId = await CreatePartyAsync(
            partyDirectoryService,
            PartyType.Organization,
            "External Design Partners");
        var personId = await CreatePartyAsync(
            partyDirectoryService,
            PartyType.Person,
            "Alex External");
        var otherPersonId = await CreatePartyAsync(
            partyDirectoryService,
            PartyType.Person,
            "Sam Different");
        var affiliationResult = await affiliationService.UpsertAsync(
            new PartyOrganizationAffiliationEditorModel
            {
                PersonPartyId = personId,
                OrganizationPartyId = organizationId,
                AffiliationKind =
                    PartyOrganizationAffiliationKind.ExternalContact,
                IsPrimary = true,
                JobTitle = "Garden designer",
                ValidFrom = new DateOnly(2026, 1, 1),
                ValidTo = new DateOnly(2026, 12, 31)
            },
            "integration-tests");
        Assert.True(affiliationResult.IsSuccess);
        var affiliationId = affiliationResult.Value!.Id;

        var wrongPersonResult = await bridge.SaveAssignmentAsync(
            new ProjectPartyAssignmentUpsertRequest
            {
                ProjectId = projectId,
                PartyId = otherPersonId,
                PartyAffiliationId = affiliationId,
                Role = ProjectPartyAssignmentRole.TeamMember,
                Source = "integration-tests"
            });
        var outsideDateResult = await bridge.SaveAssignmentAsync(
            new ProjectPartyAssignmentUpsertRequest
            {
                ProjectId = projectId,
                PartyId = personId,
                PartyAffiliationId = affiliationId,
                Role = ProjectPartyAssignmentRole.TeamMember,
                StartsOn = new DateOnly(2027, 1, 1),
                EndsOn = new DateOnly(2027, 1, 31),
                Source = "integration-tests"
            });
        var partiallyCoveredDateResult = await bridge.SaveAssignmentAsync(
            new ProjectPartyAssignmentUpsertRequest
            {
                ProjectId = projectId,
                PartyId = personId,
                PartyAffiliationId = affiliationId,
                Role = ProjectPartyAssignmentRole.TeamMember,
                StartsOn = new DateOnly(2025, 12, 15),
                EndsOn = new DateOnly(2026, 1, 15),
                Source = "integration-tests"
            });
        var validResult = await bridge.SaveAssignmentAsync(
            new ProjectPartyAssignmentUpsertRequest
            {
                ProjectId = projectId,
                PartyId = personId,
                PartyAffiliationId = affiliationId,
                Role = ProjectPartyAssignmentRole.TeamMember,
                StartsOn = new DateOnly(2026, 8, 1),
                EndsOn = new DateOnly(2026, 8, 31),
                Source = "integration-tests"
            });

        Assert.False(wrongPersonResult.IsSuccess);
        Assert.Contains(
            wrongPersonResult.Errors,
            error =>
                error.Code ==
                ProjectPartyAffiliationErrorCodes.PartyMismatch);
        Assert.False(outsideDateResult.IsSuccess);
        Assert.Contains(
            outsideDateResult.Errors,
            error =>
                error.Code ==
                ProjectPartyAffiliationErrorCodes.DateMismatch);
        Assert.False(partiallyCoveredDateResult.IsSuccess);
        Assert.Contains(
            partiallyCoveredDateResult.Errors,
            error =>
                error.Code ==
                ProjectPartyAffiliationErrorCodes.DateMismatch);
        Assert.True(validResult.IsSuccess);

        var assignment = Assert.Single(
            await bridge.ListAssignmentsDetailedAsync(projectId));
        Assert.NotNull(assignment.Affiliation);
        Assert.Equal(affiliationId, assignment.PartyAffiliationId);
        Assert.Equal(affiliationId, assignment.Affiliation.AffiliationId);
        Assert.Equal(
            "External contact",
            assignment.Affiliation.AffiliationLabel);
        Assert.Equal(
            "External Design Partners",
            assignment.Affiliation.OrganizationName);
        Assert.Equal("Garden designer", assignment.Affiliation.RoleTitle);
    }

    [Fact]
    public async Task Bridge_rejects_missing_and_cross_project_canonical_node_assignments()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var bridge = scope.ServiceProvider.GetRequiredService<IProjectPartyIntegrationBridge>();

        var firstProjectId = await CreateProjectAsync(projectsService, "Canonical assignment A");
        var secondProjectId = await CreateProjectAsync(projectsService, "Canonical assignment B");
        var assigneeId = await CreatePartyAsync(partyDirectoryService, PartyType.Person, "Willa Worker");
        var foreignWorkItem = await workbench.CreateObjectAsync(
            secondProjectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.WorkItem,
                "Foreign work item",
                string.Empty,
                "Belongs to another project.",
                null,
                420,
                240,
                null,
                null,
                "task"));

        var missingNodeResult = await bridge.SaveAssignmentAsync(new ProjectPartyAssignmentUpsertRequest
        {
            ProjectId = firstProjectId,
            PartyId = assigneeId,
            Role = ProjectPartyAssignmentRole.WorkItemAssignee,
            NodeKey = "custom:missing-work-item",
            IsPrimary = true,
            Source = "integration-tests"
        });

        Assert.False(missingNodeResult.IsSuccess);
        Assert.Contains(missingNodeResult.Errors, error => error.Code == "crmhr.project-assignment.node-not-found");

        var foreignNodeResult = await bridge.SaveAssignmentAsync(new ProjectPartyAssignmentUpsertRequest
        {
            ProjectId = firstProjectId,
            PartyId = assigneeId,
            Role = ProjectPartyAssignmentRole.WorkItemAssignee,
            NodeKey = foreignWorkItem.Id,
            IsPrimary = true,
            Source = "integration-tests"
        });

        Assert.False(foreignNodeResult.IsSuccess);
        Assert.Contains(foreignNodeResult.Errors, error => error.Code == "crmhr.project-assignment.node-project-mismatch");
    }

    [Fact]
    public async Task Bridge_rejects_disallowed_canonical_node_role_combinations()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var bridge = scope.ServiceProvider.GetRequiredService<IProjectPartyIntegrationBridge>();

        var projectId = await CreateProjectAsync(projectsService, "Canonical role policy");
        var participantId = await CreatePartyAsync(partyDirectoryService, PartyType.Person, "Mina Meeting");
        var workItemAssigneeId = await CreatePartyAsync(partyDirectoryService, PartyType.Person, "Ari Assignee");
        var noteNode = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Note,
                "Loose note",
                string.Empty,
                "Not a meeting or work item.",
                null,
                420,
                260));
        var meetingNode = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Meeting,
                "Stand-up",
                string.Empty,
                "Meeting node.",
                null,
                680,
                260));

        var invalidMeetingRole = await bridge.SaveAssignmentAsync(new ProjectPartyAssignmentUpsertRequest
        {
            ProjectId = projectId,
            PartyId = participantId,
            Role = ProjectPartyAssignmentRole.MeetingParticipant,
            NodeKey = noteNode.Id,
            IsPrimary = true,
            Source = "integration-tests"
        });

        Assert.False(invalidMeetingRole.IsSuccess);
        Assert.Contains(invalidMeetingRole.Errors, error => error.Code == "crmhr.project-assignment.node-role-not-allowed");

        var validMeetingRole = await bridge.SaveAssignmentAsync(new ProjectPartyAssignmentUpsertRequest
        {
            ProjectId = projectId,
            PartyId = participantId,
            Role = ProjectPartyAssignmentRole.MeetingParticipant,
            NodeKey = meetingNode.Id,
            IsPrimary = true,
            Source = "integration-tests"
        });

        Assert.True(validMeetingRole.IsSuccess);

        var invalidWorkItemRole = await bridge.SaveAssignmentAsync(new ProjectPartyAssignmentUpsertRequest
        {
            ProjectId = projectId,
            PartyId = workItemAssigneeId,
            Role = ProjectPartyAssignmentRole.WorkItemAssignee,
            NodeKey = meetingNode.Id,
            IsPrimary = true,
            Source = "integration-tests"
        });

        Assert.False(invalidWorkItemRole.IsSuccess);
        Assert.Contains(invalidWorkItemRole.Errors, error => error.Code == "crmhr.project-assignment.node-role-not-allowed");
    }

    [Fact]
    public async Task Bridge_rejects_projection_only_node_targets_and_uses_participant_capabilities_for_optional_node_scope()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var bridge = scope.ServiceProvider.GetRequiredService<IProjectPartyIntegrationBridge>();

        var projectId = await CreateProjectAsync(projectsService, "Participant role policy");
        var teamMemberId = await CreatePartyAsync(partyDirectoryService, PartyType.Person, "Taylor Team Member");
        var partnerId = await CreatePartyAsync(partyDirectoryService, PartyType.Organization, "Partner Org");
        var participantNode = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Participant,
                "Freelancer node",
                string.Empty,
                "Participant node for optional node scope validation.",
                null,
                420,
                260,
                null,
                null,
                "freelancer"));

        var projectionOnlyResult = await bridge.SaveAssignmentAsync(new ProjectPartyAssignmentUpsertRequest
        {
            ProjectId = projectId,
            PartyId = teamMemberId,
            Role = ProjectPartyAssignmentRole.TeamMember,
            NodeKey = $"project:{projectId}",
            IsPrimary = true,
            Source = "integration-tests"
        });

        Assert.False(projectionOnlyResult.IsSuccess);
        Assert.Contains(projectionOnlyResult.Errors, error => error.Code == "crmhr.project-assignment.node-projection-not-allowed");

        var invalidParticipantRole = await bridge.SaveAssignmentAsync(new ProjectPartyAssignmentUpsertRequest
        {
            ProjectId = projectId,
            PartyId = partnerId,
            Role = ProjectPartyAssignmentRole.Partner,
            NodeKey = participantNode.Id,
            IsPrimary = true,
            Source = "integration-tests"
        });

        Assert.False(invalidParticipantRole.IsSuccess);
        Assert.Contains(invalidParticipantRole.Errors, error => error.Code == "crmhr.project-assignment.node-role-not-allowed");

        var validParticipantRole = await bridge.SaveAssignmentAsync(new ProjectPartyAssignmentUpsertRequest
        {
            ProjectId = projectId,
            PartyId = teamMemberId,
            Role = ProjectPartyAssignmentRole.TeamMember,
            NodeKey = participantNode.Id,
            IsPrimary = true,
            Source = "integration-tests"
        });

        Assert.True(validParticipantRole.IsSuccess);

        var projectLevelRole = await bridge.SaveAssignmentAsync(new ProjectPartyAssignmentUpsertRequest
        {
            ProjectId = projectId,
            PartyId = teamMemberId,
            Role = ProjectPartyAssignmentRole.TeamMember,
            IsPrimary = true,
            Source = "integration-tests"
        });

        Assert.True(projectLevelRole.IsSuccess);
    }

    private static async Task<Guid> CreateProjectAsync(ProjectsService projectsService, string name)
    {
        var result = await projectsService.SaveAsync(new ProjectEditorModel
        {
            Name = name,
            Description = $"{name} description",
            Objective = $"{name} objective",
            CurrentPhase = "Discovery"
        });

        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static async Task<Guid> CreatePartyAsync(PartyDirectoryService partyDirectoryService, PartyType partyType, string displayName)
    {
        var result = await partyDirectoryService.SavePartyAsync(new PartyEditorModel
        {
            PartyType = partyType,
            LifecycleStatus = PartyLifecycleStatus.Active,
            DisplayName = displayName,
            Summary = $"{displayName} summary",
            LastChangedBy = "integration-tests"
        });

        Assert.True(result.IsSuccess);
        return result.Value;
    }
}
