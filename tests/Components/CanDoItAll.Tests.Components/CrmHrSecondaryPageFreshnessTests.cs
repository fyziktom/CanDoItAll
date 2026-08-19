using Bunit;
using CanDoItAll.AgentFramework.Components;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.CrmHr.Pages;
using CanDoItAll.Modules.Projects;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.CrmHr;

public sealed class CrmHrSecondaryPageFreshnessTests
{
    [Fact]
    public async Task Agents_page_requests_bounded_server_pages()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var workspaces = Enumerable.Range(0, 20)
            .Select(index => CreateAgentWorkspace(Guid.NewGuid(), $"Agent {index:D2}"))
            .ToDictionary(workspace => workspace.PartyId);
        var cut = harness.Context.Render<RacingCrmHrAgentsPage>(parameters => parameters
            .Add(page => page.TestItems, workspaces.Values.Select(workspace => CreateAgentItem(workspace)).ToArray())
            .Add(page => page.TestWorkspaces, workspaces));

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(12, cut.FindAll("[data-testid='crmhr-agent-item']").Count);
            Assert.Equal(
                new AiAgentDirectoryQuery(
                    PageSize: AiAgentDirectoryQueryLimits.DefaultPageSize),
                Assert.Single(cut.Instance.DirectoryQueries));
        });
        Assert.Equal(0, cut.Instance.DirectoryProjectionRefreshCount);
        Assert.Empty(cut.Instance.WorkspaceRequests);
        Assert.Empty(cut.Instance.DirectoryItemRequests);
        Assert.Empty(cut.FindAll("[data-testid='crmhr-agent-record-dialog']"));
        var provider = cut.FindComponent<AgentChatContextSurfaceProvider>();
        Assert.Equal(AgentChatContextAccessState.Ready, provider.Instance.ContextAccessState);
        Assert.Null(provider.Instance.Surface.Position.PrimarySelection);

        cut.Find("[data-testid='crmhr-agent-next']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(8, cut.FindAll("[data-testid='crmhr-agent-item']").Count);
            Assert.Equal(2, cut.Instance.DirectoryQueries.Count);
            Assert.Equal(1, cut.Instance.DirectoryQueries[1].PageIndex);
            Assert.Equal(
                AiAgentDirectoryQueryLimits.DefaultPageSize,
                cut.Instance.DirectoryQueries[1].PageSize);
        });
        Assert.Empty(cut.Instance.WorkspaceRequests);
        Assert.Empty(cut.Instance.DirectoryItemRequests);
    }

    [Fact]
    public async Task Agents_page_opens_record_dialog_for_explicit_party()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var partyId = Guid.NewGuid();
        var workspace = CreateAgentWorkspace(partyId, "Projected agent");
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo($"/crm-hr/agents?partyId={partyId:D}");
        var cut = harness.Context.Render<RacingCrmHrAgentsPage>(parameters => parameters
            .Add(page => page.TestItems, [CreateAgentItem(workspace)])
            .Add(page => page.TestWorkspaces, new Dictionary<Guid, AiAgentWorkspaceModel>
            {
                [partyId] = workspace
            }));

        cut.WaitForAssertion(() => AssertAgentDialogSurface(cut, partyId, "Projected agent"));
        Assert.Equal([partyId], cut.Instance.WorkspaceRequests);
        Assert.Equal([partyId], cut.Instance.DirectoryItemRequests);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Agents_page_ignores_late_previous_workspace_result(bool staleRequestFails)
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var firstPartyId = Guid.NewGuid();
        var secondPartyId = Guid.NewGuid();
        var workspaces = new Dictionary<Guid, AiAgentWorkspaceModel>
        {
            [firstPartyId] = CreateAgentWorkspace(firstPartyId, "First agent"),
            [secondPartyId] = CreateAgentWorkspace(secondPartyId, "Second agent")
        };
        var items = workspaces.Values.Select(workspace => CreateAgentItem(workspace)).ToArray();
        var cut = harness.Context.Render<RacingCrmHrAgentsPage>(parameters => parameters
            .Add(page => page.TestItems, items)
            .Add(page => page.TestWorkspaces, workspaces));
        cut.WaitForAssertion(() =>
        {
            Assert.Equal(2, cut.FindAll("[data-testid='crmhr-agent-item']").Count);
            Assert.Empty(cut.FindAll("[data-testid='crmhr-agent-record-dialog']"));
        });
        Assert.Empty(cut.Instance.WorkspaceRequests);
        Assert.Empty(cut.Instance.DirectoryItemRequests);
        cut.Instance.Delay(firstPartyId, secondPartyId);

        cut.Instance.PartyIdQuery = firstPartyId;
        var staleLoad = cut.InvokeAsync(() => InvokeAgentLoadAsync(cut.Instance, firstPartyId));
        await cut.Instance.WaitForRequestsAsync(firstPartyId);
        Assert.Equal(AgentChatContextAccessState.Loading, ReadAgentAccessState(cut.Instance));

        cut.Instance.PartyIdQuery = secondPartyId;
        var currentLoad = cut.InvokeAsync(() => InvokeAgentLoadAsync(cut.Instance, secondPartyId));
        await cut.Instance.WaitForRequestsAsync(secondPartyId);
        cut.Instance.Complete(secondPartyId);
        await currentLoad;

        if (staleRequestFails)
        {
            cut.Instance.Fail(firstPartyId);
        }
        else
        {
            cut.Instance.Complete(firstPartyId);
        }

        await staleLoad;
        await cut.InvokeAsync(cut.Instance.RenderNow);

        AssertAgentDialogSurface(cut, secondPartyId, "Second agent");
        Assert.Equal(AgentChatContextAccessState.Ready, ReadAgentAccessState(cut.Instance));
        Assert.Equal(
            [firstPartyId, secondPartyId],
            cut.Instance.WorkspaceRequests);
        Assert.Equal(
            [firstPartyId, secondPartyId],
            cut.Instance.DirectoryItemRequests);
    }

    [Fact]
    public async Task Agents_page_does_not_fallback_from_explicit_missing_party()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var existingPartyId = Guid.NewGuid();
        var missingPartyId = Guid.NewGuid();
        var workspace = CreateAgentWorkspace(existingPartyId, "Existing agent");
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo($"/crm-hr/agents?partyId={missingPartyId:D}");
        var cut = harness.Context.Render<RacingCrmHrAgentsPage>(parameters => parameters
            .Add(page => page.TestItems, [CreateAgentItem(workspace)])
            .Add(page => page.TestWorkspaces, new Dictionary<Guid, AiAgentWorkspaceModel>
            {
                [existingPartyId] = workspace
            }));

        cut.WaitForAssertion(() =>
        {
            var provider = cut.FindComponent<AgentChatContextSurfaceProvider>();
            Assert.Equal(AgentChatContextAccessState.Failed, provider.Instance.ContextAccessState);
            Assert.Null(provider.Instance.Surface.Position.PrimarySelection);
            Assert.Empty(cut.FindAll("[data-testid='crmhr-agent-record-dialog']"));
        });
        Assert.Equal([missingPartyId], cut.Instance.WorkspaceRequests);
        Assert.Equal([missingPartyId], cut.Instance.DirectoryItemRequests);
    }

    [Fact]
    public async Task Agents_page_rejects_conflicting_technical_projection_identity()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var partyId = Guid.NewGuid();
        var workspace = CreateAgentWorkspace(partyId, "Conflicting agent");
        var projectedItem = CreateAgentItem(workspace);
        var directoryItem = projectedItem with
        {
            Agent = projectedItem.Agent with
            {
                Id = Guid.NewGuid()
            }
        };
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo($"/crm-hr/agents?partyId={partyId:D}");

        var exception = Assert.Throws<InvalidOperationException>(
            () => harness.Context.Render<RacingCrmHrAgentsPage>(parameters => parameters
                .Add(page => page.TestItems, [directoryItem])
                .Add(page => page.TestWorkspaces, new Dictionary<Guid, AiAgentWorkspaceModel>
                {
                    [partyId] = workspace
                })));

        Assert.Contains("resolved conflicting technical agents", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Assignments_page_ignores_late_previous_project_result(bool staleRequestFails)
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var firstProject = CreateProject("First project");
        var secondProject = CreateProject("Second project");
        var firstAssignment = CreateAssignment(firstProject.Id, "First assignment");
        var secondAssignment = CreateAssignment(secondProject.Id, "Second assignment");
        var cut = harness.Context.Render<RacingCrmHrAssignmentsPage>(parameters => parameters
            .Add(page => page.TestProjects, [firstProject, secondProject])
            .Add(page => page.TestAssignments, new Dictionary<Guid, IReadOnlyList<ProjectPartyAssignmentDetail>>
            {
                [firstProject.Id] = [firstAssignment],
                [secondProject.Id] = [secondAssignment]
            }));
        cut.WaitForAssertion(() => AssertAssignmentSurface(cut, firstProject));
        AssertSelectionRequests(
            cut.Instance,
            new RecordedAssignmentSelectionRequest(
                firstProject.Id,
                RecordedAssignmentSelectionData.Assignments));
        Assert.Equal(1, cut.Instance.StaffingDashboardRequestCount);
        cut.Instance.Delay(firstProject.Id, secondProject.Id);

        var staleLoad = cut.InvokeAsync(() => InvokeProjectSelectionAsync(cut.Instance, firstProject.Id));
        await cut.Instance.WaitForRequestAsync(firstProject.Id);
        Assert.Equal(AgentChatContextAccessState.Loading, ReadAssignmentAccessState(cut.Instance));

        var currentLoad = cut.InvokeAsync(() => InvokeProjectSelectionAsync(cut.Instance, secondProject.Id));
        await cut.Instance.WaitForRequestAsync(secondProject.Id);
        Assert.Empty(ReadAssignments(cut.Instance));
        cut.Instance.Complete(secondProject.Id);
        await currentLoad;

        if (staleRequestFails)
        {
            cut.Instance.Fail(firstProject.Id);
        }
        else
        {
            cut.Instance.Complete(firstProject.Id);
        }

        await staleLoad;
        await cut.InvokeAsync(cut.Instance.RenderNow);

        AssertAssignmentSurface(cut, secondProject);
        var loadedAssignment = Assert.Single(ReadAssignments(cut.Instance));
        Assert.Equal(secondAssignment, loadedAssignment);
        Assert.Equal(cut.Instance.TestStaffingDashboard, ReadStaffingDashboard(cut.Instance));
        Assert.Equal(1, cut.Instance.StaffingDashboardRequestCount);
        AssertSelectionRequests(
            cut.Instance,
            new(firstProject.Id, RecordedAssignmentSelectionData.Assignments),
            new(firstProject.Id, RecordedAssignmentSelectionData.Assignments),
            new(secondProject.Id, RecordedAssignmentSelectionData.Assignments));
        Assert.Equal(AgentChatContextAccessState.Ready, ReadAssignmentAccessState(cut.Instance));
    }

    [Fact]
    public async Task Assignments_page_does_not_fallback_from_explicit_missing_project()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var existingProject = CreateProject("Existing project");
        var missingProjectId = Guid.NewGuid();
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo($"/crm-hr/assignments?projectId={missingProjectId:D}");
        var cut = harness.Context.Render<RacingCrmHrAssignmentsPage>(parameters => parameters
            .Add(page => page.TestProjects, [existingProject]));

        cut.WaitForAssertion(() =>
        {
            var provider = cut.FindComponent<AgentChatContextSurfaceProvider>();
            Assert.Equal(AgentChatContextAccessState.Failed, provider.Instance.ContextAccessState);
            Assert.Null(provider.Instance.Surface.Position.PrimarySelection);
        });
        Assert.Empty(cut.Instance.SelectionRequests);
        Assert.Equal(1, cut.Instance.StaffingDashboardRequestCount);
        Assert.Equal(missingProjectId, ReadSelectedProjectId(cut.Instance));
    }

    [Fact]
    public async Task Assignments_page_does_not_cancel_route_bootstrap_when_a_tab_is_selected_early()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var project = CreateProject("Bootstrap project");
        var dashboardCompletion = new TaskCompletionSource<StaffingDashboardModel>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo($"/crm-hr/assignments?projectId={project.Id:D}");
        var cut = harness.Context.Render<RacingCrmHrAssignmentsPage>(parameters => parameters
            .Add(page => page.TestProjects, [project])
            .Add(page => page.DashboardCompletion, dashboardCompletion));
        await cut.Instance.DashboardStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));

        cut.Find("[data-testid='crmhr-assignments-tab-relationships']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.True(cut.Find("[data-testid='crmhr-assignment-create-button']").HasAttribute("disabled"));
            Assert.Empty(cut.FindAll("[data-testid='crmhr-assignment-create-dialog']"));
        });
        Assert.Empty(cut.Instance.SelectionRequests);

        dashboardCompletion.TrySetResult(cut.Instance.TestStaffingDashboard);

        cut.WaitForAssertion(() =>
        {
            AssertAssignmentSurface(cut, project);
            Assert.False(cut.Find("[data-testid='crmhr-assignment-create-button']").HasAttribute("disabled"));
        });
        AssertSelectionRequests(
            cut.Instance,
            new RecordedAssignmentSelectionRequest(
                project.Id,
                RecordedAssignmentSelectionData.AssignmentCounts |
                RecordedAssignmentSelectionData.RelationshipAssignments));
    }

    [Fact]
    public async Task Assignments_page_selects_projects_through_the_server_paged_catalog()
    {
        var firstProject = CreateProject("First project");
        var secondProject = CreateProject("Second project");
        var queryService = new RecordingProjectRecordQueryService(
            [ToQueryItem(firstProject), ToQueryItem(secondProject)]);
        await using var harness = await ComponentTestHarness.CreateAsync(
            services => services.AddSingleton<IProjectRecordQueryService>(queryService));
        var cut = harness.Context.Render<RacingCrmHrAssignmentsPage>(parameters => parameters
            .Add(page => page.TestProjects, [firstProject, secondProject]));
        cut.WaitForAssertion(() => AssertAssignmentSurface(cut, firstProject));

        Assert.Equal(2, ReadProjectCount(cut.Instance));
        cut.Find("[data-testid='crmhr-assignment-project']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(cut.Find("[data-testid='crmhr-assignment-project-picker']"));
            Assert.NotNull(cut.Find($"[data-testid='crmhr-project-option-{secondProject.Id:N}']"));
        });
        var query = Assert.Single(queryService.Queries);
        Assert.Equal(ProjectRecordScope.All, query.Scope);
        Assert.Equal(ProjectRecordQueryLimits.DefaultPageSize, query.PageSize);

        cut.Find($"[data-testid='crmhr-project-option-{secondProject.Id:N}']").Click();
        cut.Find("[data-testid='crmhr-assignment-project-picker-confirm']").Click();

        cut.WaitForAssertion(() => AssertAssignmentSurface(cut, secondProject));
        Assert.Equal(secondProject.Id, ReadSelectedProjectId(cut.Instance));
    }

    [Fact]
    public async Task Assignments_page_mounts_one_workflow_tab_and_loads_each_dataset_lazily()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var project = CreateProject("Tabbed project");
        var cut = harness.Context.Render<RacingCrmHrAssignmentsPage>(parameters => parameters
            .Add(page => page.TestProjects, [project]));

        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(cut.Find("[data-testid='crmhr-assignment-resource-schedule']"));
            Assert.Empty(cut.FindAll("[data-testid='crmhr-assignment-page']"));
            Assert.Empty(cut.FindAll("[data-testid='crmhr-staffing-request-create-button']"));
            Assert.Empty(cut.FindAll("[data-testid='crmhr-allocations-surface']"));
            Assert.Empty(cut.FindAll("[data-testid='crmhr-assignment-create-dialog']"));
            Assert.Empty(cut.FindAll("[data-testid='crmhr-staffing-request-title']"));
            Assert.Empty(cut.FindAll("[data-testid='crmhr-allocation-percent']"));
        });
        AssertSelectionRequests(
            cut.Instance,
            new RecordedAssignmentSelectionRequest(
                project.Id,
                RecordedAssignmentSelectionData.Assignments));
        Assert.Equal(1, cut.Instance.StaffingDashboardRequestCount);

        cut.Find("[data-testid='crmhr-assignments-tab-relationships']").Click();
        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(cut.Find("[data-testid='crmhr-assignment-page']"));
            Assert.Empty(cut.FindAll("[data-testid='crmhr-assignment-resource-schedule']"));
            Assert.Empty(cut.FindAll("[data-testid='crmhr-assignment-role']"));
        });
        AssertSelectionRequests(
            cut.Instance,
            new RecordedAssignmentSelectionRequest(
                project.Id,
                RecordedAssignmentSelectionData.Assignments),
            new RecordedAssignmentSelectionRequest(
                project.Id,
                RecordedAssignmentSelectionData.RelationshipAssignments));

        cut.Find("[data-testid='crmhr-assignment-create-button']").Click();
        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(cut.Find("[data-testid='crmhr-assignment-create-dialog']"));
            Assert.NotNull(cut.Find("[data-testid='crmhr-assignment-role']"));
        });
        AssertSelectionRequests(
            cut.Instance,
            new RecordedAssignmentSelectionRequest(
                project.Id,
                RecordedAssignmentSelectionData.Assignments),
            new RecordedAssignmentSelectionRequest(
                project.Id,
                RecordedAssignmentSelectionData.RelationshipAssignments));

        cut.Find("[data-testid='crmhr-assignments-tab-staffing']").Click();
        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(cut.Find("[data-testid='crmhr-staffing-request-create-button']"));
            Assert.Empty(cut.FindAll("[data-testid='crmhr-assignment-role']"));
            Assert.Empty(cut.FindAll("[data-testid='crmhr-staffing-request-title']"));
        });
        AssertSelectionRequests(
            cut.Instance,
            new(project.Id, RecordedAssignmentSelectionData.Assignments),
            new(project.Id, RecordedAssignmentSelectionData.RelationshipAssignments),
            new(project.Id, RecordedAssignmentSelectionData.StaffingRequests));

        cut.Find("[data-testid='crmhr-staffing-request-create-button']").Click();
        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(cut.Find("[data-testid='crmhr-staffing-request-dialog']"));
            Assert.NotNull(cut.Find("[data-testid='crmhr-staffing-request-title']"));
        });
        AssertSelectionRequests(
            cut.Instance,
            new(project.Id, RecordedAssignmentSelectionData.Assignments),
            new(project.Id, RecordedAssignmentSelectionData.RelationshipAssignments),
            new(project.Id, RecordedAssignmentSelectionData.StaffingRequests),
            new(project.Id, RecordedAssignmentSelectionData.SkillCatalog));

        cut.Find("[data-testid='crmhr-assignments-tab-allocations']").Click();
        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(cut.Find("[data-testid='crmhr-allocations-surface']"));
            Assert.Empty(cut.FindAll("[data-testid='crmhr-staffing-request-title']"));
            Assert.Empty(cut.FindAll("[data-testid='crmhr-allocation-percent']"));
        });
        AssertSelectionRequests(
            cut.Instance,
            new(project.Id, RecordedAssignmentSelectionData.Assignments),
            new(project.Id, RecordedAssignmentSelectionData.RelationshipAssignments),
            new(project.Id, RecordedAssignmentSelectionData.StaffingRequests),
            new(project.Id, RecordedAssignmentSelectionData.SkillCatalog),
            new(project.Id, RecordedAssignmentSelectionData.AllocationAssignments));

        cut.Find("[data-testid='crmhr-allocation-create-button']").Click();
        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(cut.Find("[data-testid='crmhr-allocation-dialog']"));
            Assert.NotNull(cut.Find("[data-testid='crmhr-allocation-candidates-surface']"));
            Assert.NotNull(cut.Find("[data-testid='crmhr-allocation-percent']"));
        });
        AssertSelectionRequests(
            cut.Instance,
            new(project.Id, RecordedAssignmentSelectionData.Assignments),
            new(project.Id, RecordedAssignmentSelectionData.RelationshipAssignments),
            new(project.Id, RecordedAssignmentSelectionData.StaffingRequests),
            new(project.Id, RecordedAssignmentSelectionData.SkillCatalog),
            new(project.Id, RecordedAssignmentSelectionData.AllocationAssignments),
            new(project.Id, RecordedAssignmentSelectionData.StaffingCandidates));
        Assert.Equal(1, cut.Instance.StaffingDashboardRequestCount);
    }

    private static Task InvokeAgentLoadAsync(CrmHrAgentsPage page, Guid? partyId)
    {
        var method = typeof(CrmHrAgentsPage).GetMethod(
            "LoadAsync",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        return Assert.IsAssignableFrom<Task>(method?.Invoke(page, [partyId]));
    }

    private static Task InvokeProjectSelectionAsync(CrmHrAssignmentsPage page, Guid? projectId)
    {
        var method = typeof(CrmHrAssignmentsPage).GetMethod(
            "HandleProjectChanged",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        return Assert.IsAssignableFrom<Task>(method?.Invoke(page, [projectId]));
    }

    private static AgentChatContextAccessState ReadAgentAccessState(CrmHrAgentsPage page)
        => ReadField<AgentChatContextAccessState>(page, "agentChatContextAccessState");

    private static AgentChatContextAccessState ReadAssignmentAccessState(CrmHrAssignmentsPage page)
        => ReadField<AgentChatContextAccessState>(page, "agentChatContextAccessState");

    private static StaffingDashboardModel ReadStaffingDashboard(CrmHrAssignmentsPage page)
        => ReadField<StaffingDashboardModel>(page, "staffingDashboard");

    private static IReadOnlyList<ProjectPartyAssignmentDetail> ReadAssignments(CrmHrAssignmentsPage page)
        => ReadField<IReadOnlyList<ProjectPartyAssignmentDetail>>(page, "scheduleAssignments");

    private static Guid? ReadSelectedProjectId(CrmHrAssignmentsPage page)
        => ReadField<Guid?>(page, "selectedProjectId");

    private static int ReadProjectCount(CrmHrAssignmentsPage page)
        => ReadField<int>(page, "projectCount");

    private static T ReadField<T>(object instance, string name)
    {
        var field = instance.GetType().BaseType?.GetField(
            name,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        return Assert.IsAssignableFrom<T>(field?.GetValue(instance));
    }

    private static void AssertAgentSurface(
        IRenderedComponent<RacingCrmHrAgentsPage> cut,
        Guid partyId,
        string displayName)
    {
        var provider = cut.FindComponent<AgentChatContextSurfaceProvider>();
        Assert.Equal(AgentChatContextAccessState.Ready, provider.Instance.ContextAccessState);
        var selection = Assert.IsType<AgentChatContextEntityReference>(
            provider.Instance.Surface.Position.PrimarySelection);
        Assert.Equal(partyId.ToString("D"), selection.Id);
        Assert.Equal(displayName, selection.DisplayName);
    }

    private static void AssertAgentDialogSurface(
        IRenderedComponent<RacingCrmHrAgentsPage> cut,
        Guid partyId,
        string displayName)
    {
        AssertAgentSurface(cut, partyId, displayName);
        var dialog = cut.Find("[data-testid='crmhr-agent-record-dialog']");
        Assert.Contains(displayName, dialog.TextContent, StringComparison.Ordinal);
    }

    private static void AssertAssignmentSurface(
        IRenderedComponent<RacingCrmHrAssignmentsPage> cut,
        ProjectSummary project)
    {
        var provider = cut.FindComponent<AgentChatContextSurfaceProvider>();
        Assert.Equal(AgentChatContextAccessState.Ready, provider.Instance.ContextAccessState);
        var selection = Assert.IsType<AgentChatContextEntityReference>(
            provider.Instance.Surface.Position.PrimarySelection);
        Assert.Equal(project.Id.ToString("D"), selection.Id);
        Assert.Equal(project.Name, selection.DisplayName);
    }

    private static ProjectSummary CreateProject(string name)
        => new(
            Guid.NewGuid(),
            name,
            ProjectStatus.Active,
            "Delivery",
            1,
            0,
            0,
            DateTimeOffset.UtcNow);

    private static ProjectRecordQueryItem ToQueryItem(ProjectSummary project)
        => new(
            project.Id,
            project.Name,
            project.Status,
            project.CurrentPhase,
            string.Empty,
            project.UpdatedAtUtc);

    private static ProjectPartyAssignmentDetail CreateAssignment(Guid projectId, string displayName)
        => new(
            Guid.NewGuid(),
            projectId,
            Guid.NewGuid(),
            ProjectPartyAssignmentRole.TeamMember,
            displayName,
            "Person",
            ProjectPartyType.Person,
            string.Empty,
            false,
            100m,
            null,
            null,
            "component-test",
            string.Empty);

    private static void AssertSelectionRequests(
        RacingCrmHrAssignmentsPage page,
        params RecordedAssignmentSelectionRequest[] expected)
        => Assert.Equal(expected, page.SelectionRequests);

    private static AiAgentDirectoryItemModel CreateAgentItem(
        AiAgentWorkspaceModel workspace,
        bool includeProvider = true)
    {
        var now = DateTimeOffset.UtcNow;
        var providerId = includeProvider ? Guid.NewGuid() : (Guid?)null;
        var agent = new AgentDefinition(
            Id: workspace.TechnicalAgentId
                ?? throw new InvalidOperationException("The test workspace must reference a technical agent."),
            Name: workspace.DisplayName,
            RoleTitle: "CRM-HR projection specialist",
            Summary: workspace.Summary,
            Instructions: "Keep projected work scoped and explicit.",
            Status: AgentLifecycleStatus.Active,
            ProviderProfileId: providerId,
            Model: workspace.Profile.DefaultModel,
            Workload: AgentWorkloadKind.General,
            ChatHistoryMode: AgentChatHistoryMode.FrameworkManaged,
            Temperature: 0.2,
            RequirePerServiceCallChatHistoryPersistence: true,
            EnableBackgroundResponses: false,
            ConfigurationJson: "{}",
            IsTemplate: false,
            TemplateKey: string.Empty,
            Permissions: AgentPermissionsPolicy.Default,
            Capabilities: [],
            Tags: ["crm-hr", "projection"],
            CreatedAtUtc: now,
            UpdatedAtUtc: now);
        var governance = new AiAgentDirectoryGovernanceModel(
            workspace.LifecycleStatus,
            IsSensitive: false,
            workspace.BindingStatus,
            workspace.BindingSummary,
            workspace.Profile.ExecutionMode,
            HasProfile: true,
            workspace.Profile.ValidationStatus,
            workspace.OwnerName,
            now);
        var provider = providerId is Guid id
            ? new ProviderProfile(
                Id: id,
                Name: workspace.ProviderName,
                Kind: ProviderKind.OpenAi,
                BaseUrl: "https://api.example.test/v1",
                ApiKeyEnvironmentVariable: "TEST_API_KEY",
                DefaultModel: workspace.Profile.DefaultModel,
                Transport: ProviderTransportKind.Responses,
                IsEnabled: true,
                SupportsStreaming: true,
                SupportsTools: true,
                PreferFrameworkManagedChatHistory: true,
                SupportsBackgroundResponses: false,
                ConfigurationJson: "{}",
                Notes: string.Empty,
                HealthStatus: "Healthy",
                LastCheckedAtUtc: now,
                SuggestedModels: [workspace.Profile.DefaultModel])
            : null;

        return new AiAgentDirectoryItemModel(
            workspace.PartyId,
            agent,
            governance,
            provider,
            now);
    }

    private static AiAgentWorkspaceModel CreateAgentWorkspace(Guid partyId, string displayName)
        => new(
            partyId,
            displayName,
            $"{displayName} summary",
            PartyLifecycleStatus.Active,
            string.Empty,
            string.Empty,
            Guid.NewGuid(),
            AiResourceBindingStatus.Bound,
            "Linked",
            $"/agents?partyId={partyId:D}",
            "OpenAI",
            "Owner",
            0,
            new AiAgentProfileEditorModel
            {
                PartyId = partyId,
                DefaultModel = "gpt-5.4-mini",
                ExecutionMode = AiExecutionMode.Remote,
                ValidationStatus = AiValidationStatus.Approved
            });

    private sealed class RacingCrmHrAgentsPage : CrmHrAgentsPage
    {
        private readonly Dictionary<Guid, PendingAgentRequest> pendingRequests = [];

        [Microsoft.AspNetCore.Components.Parameter]
        public IReadOnlyList<AiAgentDirectoryItemModel> TestItems { get; set; } = [];

        [Microsoft.AspNetCore.Components.Parameter]
        public IReadOnlyDictionary<Guid, AiAgentWorkspaceModel> TestWorkspaces { get; set; }
            = new Dictionary<Guid, AiAgentWorkspaceModel>();

        public List<Guid> WorkspaceRequests { get; } = [];

        public List<Guid> DirectoryItemRequests { get; } = [];

        public List<AiAgentDirectoryQuery> DirectoryQueries { get; } = [];

        public int DirectoryProjectionRefreshCount { get; private set; }

        public void Delay(params Guid[] partyIds)
        {
            foreach (var partyId in partyIds)
            {
                pendingRequests[partyId] = new PendingAgentRequest();
            }
        }

        public Task WaitForRequestsAsync(Guid partyId)
        {
            var request = GetPendingRequest(partyId);
            return Task.WhenAll(
                    request.WorkspaceStarted.Task,
                    request.DirectoryItemStarted.Task)
                .WaitAsync(TimeSpan.FromSeconds(10));
        }

        public void Complete(Guid partyId)
        {
            var request = GetPendingRequest(partyId);
            request.WorkspaceCompletion.TrySetResult(null);
            request.DirectoryItemCompletion.TrySetResult(null);
        }

        public void Fail(Guid partyId)
        {
            var request = GetPendingRequest(partyId);
            request.WorkspaceCompletion.TrySetResult(null);
            request.DirectoryItemCompletion.TrySetResult(
                new InvalidOperationException($"Delayed agent '{partyId:D}' failed."));
        }

        public void RenderNow()
            => StateHasChanged();

        protected override Task RefreshAgentDirectoryProjectionAsync(
            CancellationToken cancellationToken)
        {
            DirectoryProjectionRefreshCount++;
            return Task.CompletedTask;
        }

        protected override Task<AiAgentDirectoryPage> QueryAgentDirectoryAsync(
            AiAgentDirectoryQuery query,
            CancellationToken cancellationToken)
        {
            DirectoryQueries.Add(query);
            var items = TestItems
                .Where(item =>
                    !query.ValidationStatus.HasValue ||
                    item.ValidationStatus == query.ValidationStatus)
                .Where(item =>
                    string.IsNullOrWhiteSpace(query.SearchText) ||
                    item.Agent.Name.Contains(query.SearchText, StringComparison.OrdinalIgnoreCase) ||
                    item.Agent.RoleTitle.Contains(query.SearchText, StringComparison.OrdinalIgnoreCase) ||
                    item.Governance.OwnerName.Contains(query.SearchText, StringComparison.OrdinalIgnoreCase) ||
                    item.Agent.Summary.Contains(query.SearchText, StringComparison.OrdinalIgnoreCase))
                .OrderBy(item => item.Agent.Name)
                .ToArray();
            return Task.FromResult(new AiAgentDirectoryPage(
                items
                    .Skip(query.PageIndex * query.PageSize)
                    .Take(query.PageSize)
                    .ToArray(),
                query.PageIndex,
                query.PageSize,
                items.Length));
        }

        protected override async Task<AiAgentDirectoryItemModel?> GetAgentDirectoryItemAsync(
            Guid partyId,
            CancellationToken cancellationToken)
        {
            DirectoryItemRequests.Add(partyId);
            if (pendingRequests.TryGetValue(partyId, out var pendingRequest))
            {
                pendingRequest.DirectoryItemStarted.TrySetResult();
                var exception = await pendingRequest.DirectoryItemCompletion.Task;
                if (exception is not null)
                {
                    throw exception;
                }
            }

            return TestItems.FirstOrDefault(item => item.PartyId == partyId);
        }

        protected override async Task<AiAgentWorkspaceModel?> GetAgentWorkspaceAsync(
            Guid partyId,
            CancellationToken cancellationToken)
        {
            WorkspaceRequests.Add(partyId);
            if (pendingRequests.TryGetValue(partyId, out var pendingRequest))
            {
                pendingRequest.WorkspaceStarted.TrySetResult();
                var exception = await pendingRequest.WorkspaceCompletion.Task;
                if (exception is not null)
                {
                    throw exception;
                }
            }

            return TestWorkspaces.GetValueOrDefault(partyId);
        }

        private PendingAgentRequest GetPendingRequest(Guid partyId)
            => pendingRequests.TryGetValue(partyId, out var pendingRequest)
                ? pendingRequest
                : throw new InvalidOperationException($"Agent '{partyId:D}' is not delayed.");
    }

    private sealed class RacingCrmHrAssignmentsPage : CrmHrAssignmentsPage
    {
        private readonly Dictionary<Guid, PendingRequest> pendingRequests = [];

        [Microsoft.AspNetCore.Components.Parameter]
        public IReadOnlyList<ProjectSummary> TestProjects { get; set; } = [];

        [Microsoft.AspNetCore.Components.Parameter]
        public IReadOnlyDictionary<Guid, IReadOnlyList<ProjectPartyAssignmentDetail>> TestAssignments { get; set; }
            = new Dictionary<Guid, IReadOnlyList<ProjectPartyAssignmentDetail>>();

        public StaffingDashboardModel TestStaffingDashboard { get; } = new(7, 8m, 9, 10);

        public List<RecordedAssignmentSelectionRequest> SelectionRequests { get; } = [];

        public int StaffingDashboardRequestCount { get; private set; }

        [Microsoft.AspNetCore.Components.Parameter]
        public TaskCompletionSource<StaffingDashboardModel>? DashboardCompletion { get; set; }

        public TaskCompletionSource DashboardStarted { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public void Delay(params Guid[] projectIds)
        {
            foreach (var projectId in projectIds)
            {
                pendingRequests[projectId] = new PendingRequest();
            }
        }

        public Task WaitForRequestAsync(Guid projectId)
            => GetPendingRequest(projectId).Started.Task.WaitAsync(TimeSpan.FromSeconds(10));

        public void Complete(Guid projectId)
            => GetPendingRequest(projectId).Completion.TrySetResult(null);

        public void Fail(Guid projectId)
            => GetPendingRequest(projectId).Completion.TrySetResult(
                new InvalidOperationException($"Delayed project '{projectId:D}' failed."));

        public void RenderNow()
            => StateHasChanged();

        protected override Task<AssignmentProjectCatalog> LoadProjectCatalogAsync(
            Guid? preferredProjectId,
            bool allowFallback,
            CancellationToken cancellationToken)
        {
            var selectedProject = preferredProjectId.HasValue
                ? TestProjects.FirstOrDefault(project => project.Id == preferredProjectId.Value)
                : null;
            selectedProject ??= allowFallback ? TestProjects.FirstOrDefault() : null;
            return Task.FromResult(new AssignmentProjectCatalog(
                selectedProject is null ? null : CrmHrSecondaryPageFreshnessTests.ToQueryItem(selectedProject),
                TestProjects.Count));
        }

        protected override Task<ProjectRecordQueryItem?> GetProjectAsync(
            Guid projectId,
            CancellationToken cancellationToken)
        {
            var project = TestProjects.FirstOrDefault(item => item.Id == projectId);
            return Task.FromResult(
                project is null ? null : CrmHrSecondaryPageFreshnessTests.ToQueryItem(project));
        }

        protected override async Task<AssignmentSelectionSnapshot> LoadSelectionSnapshotAsync(
            AssignmentSelectionLoadRequest request,
            CancellationToken cancellationToken)
        {
            SelectionRequests.Add(new(
                request.ProjectId,
                (RecordedAssignmentSelectionData)(int)request.RequestedData));
            if (request.ProjectId is { } id && pendingRequests.TryGetValue(id, out var pendingRequest))
            {
                pendingRequest.Started.TrySetResult();
                var exception = await pendingRequest.Completion.Task;
                if (exception is not null)
                {
                    throw exception;
                }
            }

            var assignments = (request.RequestedData.HasFlag(AssignmentSelectionData.ScheduleAssignments) ||
                               request.RequestedData.HasFlag(AssignmentSelectionData.RelationshipAssignments) ||
                               request.RequestedData.HasFlag(AssignmentSelectionData.AllocationAssignments))
                && request.ProjectId is { } projectId
                    ? TestAssignments.GetValueOrDefault(projectId) ?? []
                    : [];
            return new AssignmentSelectionSnapshot(
                request.RequestedData,
                assignments,
                [],
                [],
                []);
        }

        protected override async Task<StaffingDashboardModel> GetStaffingDashboardAsync(
            CancellationToken cancellationToken)
        {
            StaffingDashboardRequestCount++;
            DashboardStarted.TrySetResult();
            return DashboardCompletion is null
                ? TestStaffingDashboard
                : await DashboardCompletion.Task.WaitAsync(cancellationToken);
        }

        private PendingRequest GetPendingRequest(Guid projectId)
            => pendingRequests.TryGetValue(projectId, out var pendingRequest)
                ? pendingRequest
                : throw new InvalidOperationException($"Project '{projectId:D}' is not delayed.");

    }

    private sealed class RecordingProjectRecordQueryService(
        IReadOnlyList<ProjectRecordQueryItem> projects) : IProjectRecordQueryService
    {
        public List<ProjectRecordQuery> Queries { get; } = [];

        public Task<ProjectRecordQueryItem?> GetAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(projects.FirstOrDefault(project => project.Id == projectId));

        public Task<IReadOnlyList<ProjectRecordQueryItem>> GetManyAsync(
            IReadOnlyCollection<Guid> projectIds,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProjectRecordQueryItem>>(
                projects.Where(project => projectIds.Contains(project.Id)).ToList());

        public Task<ProjectRecordPage> SearchAsync(
            ProjectRecordQuery query,
            CancellationToken cancellationToken = default)
        {
            Queries.Add(query);
            var items = projects
                .Skip(query.PageIndex * query.PageSize)
                .Take(query.PageSize)
                .ToList();
            return Task.FromResult(new ProjectRecordPage(
                items,
                query.PageIndex,
                query.PageSize,
                projects.Count));
        }
    }

    [Flags]
    private enum RecordedAssignmentSelectionData
    {
        None = 0,
        AssignmentCounts = 1 << 0,
        ScheduleAssignments = 1 << 1,
        Assignments = AssignmentCounts | ScheduleAssignments,
        RelationshipAssignments = 1 << 2,
        AllocationAssignments = 1 << 3,
        SkillCatalog = 1 << 4,
        StaffingRequests = 1 << 5,
        StaffingCandidates = 1 << 6
    }

    private readonly record struct RecordedAssignmentSelectionRequest(
        Guid? ProjectId,
        RecordedAssignmentSelectionData RequestedData);

    private sealed class PendingAgentRequest
    {
        public TaskCompletionSource WorkspaceStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource DirectoryItemStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<Exception?> WorkspaceCompletion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<Exception?> DirectoryItemCompletion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class PendingRequest
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<Exception?> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
