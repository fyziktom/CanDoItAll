using Bunit;
using CanDoItAll.AgentFramework.Components;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.CrmHr.Pages;
using CanDoItAll.Modules.Projects;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class CrmHrSecondaryPageFreshnessTests
{
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
        var items = workspaces.Values.Select(CreateAgentItem).ToArray();
        var cut = harness.Context.RenderComponent<RacingCrmHrAgentsPage>(parameters => parameters
            .Add(page => page.TestItems, items)
            .Add(page => page.TestWorkspaces, workspaces));
        cut.WaitForAssertion(() => AssertAgentSurface(cut, firstPartyId, "First agent"));
        cut.Instance.Delay(firstPartyId, secondPartyId);

        cut.Instance.PartyIdQuery = firstPartyId;
        var staleLoad = cut.InvokeAsync(() => InvokeAgentLoadAsync(cut.Instance, firstPartyId));
        await cut.Instance.WaitForRequestAsync(firstPartyId);
        Assert.Equal(AgentChatContextAccessState.Loading, ReadAgentAccessState(cut.Instance));

        cut.Instance.PartyIdQuery = secondPartyId;
        var currentLoad = cut.InvokeAsync(() => InvokeAgentLoadAsync(cut.Instance, secondPartyId));
        await cut.Instance.WaitForRequestAsync(secondPartyId);
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

        AssertAgentSurface(cut, secondPartyId, "Second agent");
        Assert.Equal(AgentChatContextAccessState.Ready, ReadAgentAccessState(cut.Instance));
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
        var cut = harness.Context.RenderComponent<RacingCrmHrAgentsPage>(parameters => parameters
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
        });
        Assert.Equal([missingPartyId], cut.Instance.WorkspaceRequests);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Assignments_page_ignores_late_previous_project_result(bool staleRequestFails)
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var firstProject = CreateProject("First project");
        var secondProject = CreateProject("Second project");
        var cut = harness.Context.RenderComponent<RacingCrmHrAssignmentsPage>(parameters => parameters
            .Add(page => page.TestProjects, [firstProject, secondProject])
            .Add(page => page.ProjectMarkers, new Dictionary<Guid, int>
            {
                [firstProject.Id] = 1,
                [secondProject.Id] = 2
            }));
        cut.WaitForAssertion(() => AssertAssignmentSurface(cut, firstProject));
        cut.Instance.Delay(firstProject.Id, secondProject.Id);

        var staleLoad = cut.InvokeAsync(() => InvokeProjectSelectionAsync(cut.Instance, firstProject.Id));
        await cut.Instance.WaitForRequestAsync(firstProject.Id);
        Assert.Equal(AgentChatContextAccessState.Loading, ReadAssignmentAccessState(cut.Instance));

        var currentLoad = cut.InvokeAsync(() => InvokeProjectSelectionAsync(cut.Instance, secondProject.Id));
        await cut.Instance.WaitForRequestAsync(secondProject.Id);
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
        Assert.Equal(2, ReadStaffingDashboard(cut.Instance).OpenRequestCount);
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
        var cut = harness.Context.RenderComponent<RacingCrmHrAssignmentsPage>(parameters => parameters
            .Add(page => page.TestProjects, [existingProject])
            .Add(page => page.ProjectMarkers, new Dictionary<Guid, int>
            {
                [existingProject.Id] = 1
            }));

        cut.WaitForAssertion(() =>
        {
            var provider = cut.FindComponent<AgentChatContextSurfaceProvider>();
            Assert.Equal(AgentChatContextAccessState.Failed, provider.Instance.ContextAccessState);
            Assert.Null(provider.Instance.Surface.Position.PrimarySelection);
        });
        Assert.Empty(cut.Instance.SelectionRequests);
        Assert.Equal(missingProjectId, ReadSelectedProjectId(cut.Instance));
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

    private static Guid? ReadSelectedProjectId(CrmHrAssignmentsPage page)
        => ReadField<Guid?>(page, "selectedProjectId");

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

    private static AiAgentListItemModel CreateAgentItem(AiAgentWorkspaceModel workspace)
        => new(
            workspace.PartyId,
            workspace.DisplayName,
            workspace.Summary,
            workspace.LifecycleStatus,
            workspace.TechnicalAgentId,
            workspace.BindingStatus,
            workspace.BindingSummary,
            workspace.Profile.ExecutionMode,
            workspace.Profile.ValidationStatus,
            workspace.ProviderName,
            workspace.Profile.DefaultModel,
            workspace.OwnerName,
            workspace.Profile.Capabilities.Count,
            true,
            workspace.AgentsRoute,
            DateTimeOffset.UtcNow);

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
            new AiAgentProfileEditorModel
            {
                PartyId = partyId,
                DefaultModel = "gpt-5.4-mini",
                ExecutionMode = AiExecutionMode.Remote,
                ValidationStatus = AiValidationStatus.Approved
            },
            [],
            []);

    private sealed class RacingCrmHrAgentsPage : CrmHrAgentsPage
    {
        private readonly Dictionary<Guid, PendingRequest> pendingRequests = [];

        [Microsoft.AspNetCore.Components.Parameter]
        public IReadOnlyList<AiAgentListItemModel> TestItems { get; set; } = [];

        [Microsoft.AspNetCore.Components.Parameter]
        public IReadOnlyDictionary<Guid, AiAgentWorkspaceModel> TestWorkspaces { get; set; }
            = new Dictionary<Guid, AiAgentWorkspaceModel>();

        public List<Guid> WorkspaceRequests { get; } = [];

        public void Delay(params Guid[] partyIds)
        {
            foreach (var partyId in partyIds)
            {
                pendingRequests[partyId] = new PendingRequest();
            }
        }

        public Task WaitForRequestAsync(Guid partyId)
            => GetPendingRequest(partyId).Started.Task.WaitAsync(TimeSpan.FromSeconds(10));

        public void Complete(Guid partyId)
            => GetPendingRequest(partyId).Completion.TrySetResult(null);

        public void Fail(Guid partyId)
            => GetPendingRequest(partyId).Completion.TrySetResult(
                new InvalidOperationException($"Delayed agent '{partyId:D}' failed."));

        public void RenderNow()
            => StateHasChanged();

        protected override Task<IReadOnlyList<AiAgentListItemModel>> ListAgentItemsAsync(
            CancellationToken cancellationToken)
            => Task.FromResult(TestItems);

        protected override async Task<AiAgentWorkspaceModel?> GetAgentWorkspaceAsync(
            Guid partyId,
            CancellationToken cancellationToken)
        {
            WorkspaceRequests.Add(partyId);
            if (pendingRequests.TryGetValue(partyId, out var pendingRequest))
            {
                pendingRequest.Started.TrySetResult();
                var exception = await pendingRequest.Completion.Task;
                if (exception is not null)
                {
                    throw exception;
                }
            }

            return TestWorkspaces.GetValueOrDefault(partyId);
        }

        private PendingRequest GetPendingRequest(Guid partyId)
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
        public IReadOnlyDictionary<Guid, int> ProjectMarkers { get; set; }
            = new Dictionary<Guid, int>();

        public List<Guid?> SelectionRequests { get; } = [];

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

        protected override Task<IReadOnlyList<ProjectSummary>> ListProjectsAsync(
            CancellationToken cancellationToken)
            => Task.FromResult(TestProjects);

        protected override async Task<AssignmentSelectionSnapshot> LoadSelectionSnapshotAsync(
            Guid? projectId,
            CandidateFilterSnapshot candidateFilter,
            CancellationToken cancellationToken)
        {
            SelectionRequests.Add(projectId);
            if (projectId is { } id && pendingRequests.TryGetValue(id, out var pendingRequest))
            {
                pendingRequest.Started.TrySetResult();
                var exception = await pendingRequest.Completion.Task;
                if (exception is not null)
                {
                    throw exception;
                }
            }

            var marker = projectId is { } projectKey
                ? ProjectMarkers.GetValueOrDefault(projectKey)
                : 0;
            return new AssignmentSelectionSnapshot(
                [],
                [],
                [],
                [],
                [],
                new StaffingDashboardModel(marker, marker, marker, marker));
        }

        private PendingRequest GetPendingRequest(Guid projectId)
            => pendingRequests.TryGetValue(projectId, out var pendingRequest)
                ? pendingRequest
                : throw new InvalidOperationException($"Project '{projectId:D}' is not delayed.");
    }

    private sealed class PendingRequest
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<Exception?> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
