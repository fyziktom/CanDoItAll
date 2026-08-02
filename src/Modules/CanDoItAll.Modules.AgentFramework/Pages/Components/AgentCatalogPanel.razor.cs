using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

public partial class AgentCatalogPanel
{
    private const string AllAgentsTreeNodeId = "agents:all";
    private const string TeamRootTreeNodeId = "agents:teams";
    private const string TeamTreeNodePrefix = "agents:team:";
    private const string AgentTreeNodePrefix = "agents:agent:";
    [Parameter]
    public Guid? RequestedAgentId { get; set; }

    [Parameter]
    public Guid? RequestedTeamId { get; set; }

    [Parameter]
    public IReadOnlyList<AgentDefinition>? InitialAgents { get; set; }

    [Parameter]
    public IReadOnlyList<ProviderProfile>? InitialProviders { get; set; }

    [Parameter]
    public IReadOnlyList<AgentTeamDefinition>? InitialTeams { get; set; }

    [Parameter]
    public bool SkipCatalogRepair { get; set; }

    [Parameter]
    public EventCallback<AgentDefinition?> SelectedAgentChanged { get; set; }

    [Parameter]
    public EventCallback<AgentTeamDefinition?> SelectedTeamChanged { get; set; }

    [Parameter]
    public EventCallback<AgentChatContextAccessState> ContextAccessStateChanged { get; set; }

    [Inject]
    public IAgentFrameworkWorkspaceService WorkspaceService { get; set; } = default!;

    [Inject]
    public IAgentFrameworkOrganizationCatalogRepairService OrganizationCatalogRepairService { get; set; } = default!;

    [Inject]
    public NotificationService NotificationService { get; set; } = default!;

    [Inject]
    public DialogService DialogService { get; set; } = default!;

    [Inject]
    public IAgentChatLauncher AgentChatLauncher { get; set; } = default!;

    private IReadOnlyList<AgentDefinition> agents = [];
    private IReadOnlyList<AgentTeamDefinition> teams = [];
    private IReadOnlyDictionary<Guid, bool> privateProviderById = new Dictionary<Guid, bool>();
    private readonly HashSet<string> expandedTreeNodeIds = [TeamRootTreeNodeId];
    private string agentSearch = string.Empty;
    private bool hasLoaded;
    private bool isLoading = true;
    private bool interactiveReloadAttempted;
    private Task? loadTask;
    private Guid? selectedAgentId;
    private Guid? selectedTeamId;
    private Guid? appliedRequestedTeamId;
    private Guid? openedRequestedAgentId;
    private Guid? openingManagedAgentChatId;
    private AgentChatContextAccessState? publishedAccessState;
    private Guid? contextRequestedAgentId;
    private Guid? contextRequestedTeamId;
    private bool contextRequestApplied;

    private IReadOnlyList<AgentDefinition> FilteredAgents => agents
        .Where(MatchesSelectedTeam)
        .Where(MatchesAgentSearch)
        .OrderBy(agent => agent.Name, StringComparer.OrdinalIgnoreCase)
        .ToList();

    private AgentTeamDefinition? SelectedTeam => selectedTeamId.HasValue
        ? teams.FirstOrDefault(item => item.Id == selectedTeamId.Value)
        : null;

    private string TeamPanelTitle => SelectedTeam is null ? "All agents" : SelectedTeam.Name;

    private IReadOnlyList<TreeViewNode> AgentTeamTreeNodes
    {
        get
        {
            var agentsById = agents.ToDictionary(item => item.Id);
            var teamNodes = teams
                .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .Select(team =>
                {
                    var teamNodeId = BuildTeamTreeNodeId(team.Id);
                    return new TreeViewNode
                    {
                        Id = teamNodeId,
                        Text = team.Name,
                        Icon = AgentTeamIconCatalog.Normalize(team.Icon),
                        Tooltip = $"{team.Name}. {team.AgentIds.Count} agent(s).",
                        BadgeText = team.AgentIds.Count.ToString(),
                        IsExpanded = expandedTreeNodeIds.Contains(teamNodeId),
                        IsSelected = selectedTeamId == team.Id,
                        DataTestId = "agents-team-tree-team",
                        ChildrenDataTestId = "agents-team-tree-team-members",
                        Children = team.AgentIds
                            .Select(agentId => agentsById.TryGetValue(agentId, out var agent) ? agent : null)
                            .Where(agent => agent is not null)
                            .Cast<AgentDefinition>()
                            .OrderBy(agent => agent.Name, StringComparer.OrdinalIgnoreCase)
                            .Select(agent => new TreeViewNode
                            {
                                Id = BuildAgentTreeNodeId(agent.Id),
                                Text = agent.Name,
                                Icon = "person",
                                Tooltip = agent.RoleTitle,
                                IsSelected = selectedAgentId == agent.Id,
                                DataTestId = "agents-team-tree-agent"
                            })
                            .ToList()
                    };
                })
                .ToList();

            return
            [
                new TreeViewNode
                {
                    Id = AllAgentsTreeNodeId,
                    Text = "All agents",
                    Icon = "support_agent",
                    Tooltip = "Show every technical agent.",
                    BadgeText = agents.Count.ToString(),
                    IsSelected = selectedTeamId is null,
                    DataTestId = "agents-team-tree-all"
                },
                new TreeViewNode
                {
                    Id = TeamRootTreeNodeId,
                    Text = "Teams",
                    Icon = "account_tree",
                    Tooltip = "Agent teams.",
                    BadgeText = teams.Count.ToString(),
                    IsExpanded = expandedTreeNodeIds.Contains(TeamRootTreeNodeId),
                    IsSelectable = false,
                    DataTestId = "agents-team-tree-root",
                    ChildrenDataTestId = "agents-team-tree-root-children",
                    Children = teamNodes
                }
            ];
        }
    }

    protected override async Task OnInitializedAsync()
    {
        await EnsureLoadedAsync();
    }

    protected override async Task OnParametersSetAsync()
    {
        if (!contextRequestApplied ||
            contextRequestedAgentId != RequestedAgentId ||
            contextRequestedTeamId != RequestedTeamId)
        {
            contextRequestApplied = true;
            contextRequestedAgentId = RequestedAgentId;
            contextRequestedTeamId = RequestedTeamId;
            publishedAccessState = null;
            await PublishAccessStateAsync(AgentChatContextAccessState.Loading);
        }

        await EnsureLoadedAsync();
        ApplyRequestedTeam();
        await OpenRequestedAgentDialogIfNeededAsync();
        await PublishAccessStateAsync(
            HasValidRequestedSelection()
                ? AgentChatContextAccessState.Ready
                : AgentChatContextAccessState.Failed);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender ||
            interactiveReloadAttempted ||
            hasLoaded)
        {
            return;
        }

        interactiveReloadAttempted = true;
        await EnsureLoadedAsync();
        StateHasChanged();
    }

    private Task EnsureLoadedAsync()
    {
        if (hasLoaded)
        {
            return Task.CompletedTask;
        }

        if (loadTask is not null)
        {
            return loadTask;
        }

        loadTask = LoadAsync();
        return loadTask;
    }

    private async Task LoadAsync()
    {
        isLoading = true;
        await PublishAccessStateAsync(AgentChatContextAccessState.Loading);

        try
        {
            if (!SkipCatalogRepair)
            {
                await OrganizationCatalogRepairService.EnsureCurrentOrganizationCatalogAsync();
            }

            var agentsTask = InitialAgents is null
                ? WorkspaceService.ListAgentsAsync(includeTemplates: false)
                : Task.FromResult(InitialAgents);
            var providersTask = InitialProviders is null
                ? WorkspaceService.ListProvidersAsync()
                : Task.FromResult(InitialProviders);
            var teamsTask = InitialTeams is null
                ? WorkspaceService.ListAgentTeamsAsync()
                : Task.FromResult(InitialTeams);

            agents = (await agentsTask).ToList();
            privateProviderById = BuildPrivateProviderMap(await providersTask);
            teams = (await teamsTask).ToList();
            ExpandTeamNodes();

            if (RequestedAgentId.HasValue &&
                agents.Any(item => item.Id == RequestedAgentId.Value))
            {
                selectedAgentId = RequestedAgentId.Value;
            }

            ApplyRequestedTeam(force: true);

            hasLoaded = true;
            await PublishAccessStateAsync(
                HasValidRequestedSelection()
                    ? AgentChatContextAccessState.Ready
                    : AgentChatContextAccessState.Failed);
        }
        catch
        {
            await PublishAccessStateAsync(AgentChatContextAccessState.Failed);
            throw;
        }
        finally
        {
            isLoading = false;
            loadTask = null;
        }
    }

    private async Task OpenRequestedAgentDialogIfNeededAsync()
    {
        if (!RequestedAgentId.HasValue ||
            openedRequestedAgentId == RequestedAgentId.Value ||
            !agents.Any(item => item.Id == RequestedAgentId.Value))
        {
            return;
        }

        openedRequestedAgentId = RequestedAgentId.Value;
        selectedAgentId = RequestedAgentId.Value;
        await SelectedAgentChanged.InvokeAsync(agents.First(item => item.Id == RequestedAgentId.Value));
        _ = OpenAgentDetailsDialogAsync(RequestedAgentId.Value);
    }

    private async Task SelectAgentAsync(Guid agentId)
    {
        selectedAgentId = agentId;
        await SelectedAgentChanged.InvokeAsync(agents.First(item => item.Id == agentId));
        await PublishAccessStateAsync(AgentChatContextAccessState.Ready);
    }

    private async Task OpenManagedAgentChatAsync(AgentDefinition agent)
    {
        if (openingManagedAgentChatId.HasValue)
        {
            return;
        }

        if (!IsManagedQuickChatAgent(agent))
        {
            throw new InvalidOperationException(
                "Only an exact configured managed-agent identity can open a quick chat window.");
        }

        selectedAgentId = agent.Id;
        await SelectedAgentChanged.InvokeAsync(agent);
        openingManagedAgentChatId = agent.Id;
        try
        {
            await AgentChatLauncher.StartNewChatAsync(agent.Id);
            NotificationService.Success("Chat ready", $"Opened a new chat with {agent.Name}.");
        }
        catch (Exception exception)
        {
            NotificationService.Error("Unable to open managed agent chat", exception.Message);
        }
        finally
        {
            openingManagedAgentChatId = null;
        }
    }

    private static bool IsManagedQuickChatAgent(AgentDefinition agent)
    {
        return HrAgentIdentity.Matches(agent) ||
               PromptsCuratorAgentIdentity.Matches(agent) ||
               WorkflowCuratorAgentIdentity.Matches(agent) ||
               SchedulerAgentIdentity.Matches(agent);
    }

    private async Task HandleAgentTeamTreeSelectAsync(string nodeId)
    {
        if (string.Equals(nodeId, AllAgentsTreeNodeId, StringComparison.Ordinal))
        {
            selectedTeamId = null;
            await SelectedTeamChanged.InvokeAsync(null);
            return;
        }

        if (TryParseTeamTreeNodeId(nodeId, out var teamId) &&
            teams.Any(item => item.Id == teamId))
        {
            selectedTeamId = teamId;
            expandedTreeNodeIds.Add(nodeId);
            await SelectedTeamChanged.InvokeAsync(teams.First(item => item.Id == teamId));
            return;
        }

        if (TryParseAgentTreeNodeId(nodeId, out var agentId) &&
            agents.Any(item => item.Id == agentId))
        {
            selectedAgentId = agentId;
            await SelectedAgentChanged.InvokeAsync(agents.First(item => item.Id == agentId));
        }
    }

    private Task HandleAgentTeamTreeToggleAsync(string nodeId)
    {
        if (!expandedTreeNodeIds.Add(nodeId))
        {
            expandedTreeNodeIds.Remove(nodeId);
        }

        return Task.CompletedTask;
    }

    private Task OpenNewAgentDialogAsync()
        => QueueAgentDetailsDialogAsync(agentId: null);

    private Task OpenNewTeamDialogAsync()
        => QueueTeamDetailsDialogAsync(teamId: null);

    private Task OpenSelectedTeamDialogAsync()
        => SelectedTeam is null
            ? Task.CompletedTask
            : QueueTeamDetailsDialogAsync(SelectedTeam.Id);

    private Task OpenSelectedTeamMembersDialogAsync()
    {
        _ = OpenTeamMembersDialogAsync();
        return Task.CompletedTask;
    }

    private Task QueueAgentDetailsDialogAsync(Guid? agentId)
    {
        _ = OpenAgentDetailsDialogAsync(agentId);
        return Task.CompletedTask;
    }

    private Task QueueTeamDetailsDialogAsync(Guid? teamId)
    {
        _ = OpenTeamDetailsDialogAsync(teamId);
        return Task.CompletedTask;
    }

    private async Task OpenAgentDetailsDialogAsync(Guid? agentId)
    {
        if (agentId.HasValue)
        {
            selectedAgentId = agentId.Value;
            await SelectedAgentChanged.InvokeAsync(agents.FirstOrDefault(item => item.Id == agentId.Value));
        }

        try
        {
            var title = agentId.HasValue
                ? ResolveDialogTitle(agentId.Value)
                : "New technical agent";

            var result = await DialogService.OpenAsync<AgentDetailsDialog>(
                title,
                new Dictionary<string, object?>
                {
                    [nameof(AgentDetailsDialog.AgentId)] = agentId,
                    [nameof(AgentDetailsDialog.InitialProviders)] = InitialProviders,
                    [nameof(AgentDetailsDialog.Saved)] =
                        EventCallback.Factory.Create<AgentDetailsDialogResult>(this, HandleAgentDialogSavedAsync)
                },
                new DialogOptions
                {
                    Eyebrow = "Technical editor",
                    Subtitle = "Edit identity, runtime, access policy, and capabilities for this technical agent.",
                    Size = ModalSize.Full,
                    DenseChrome = true,
                    AriaLabel = "Agent details editor",
                    TestId = "agents-details-dialog"
                });

            if (result is AgentDetailsDialogResult dialogResult)
            {
                await HandleAgentDialogSavedAsync(dialogResult);
            }
        }
        catch (Exception exception)
        {
            NotificationService.Error("Agent dialog failed", exception.Message);
        }
    }

    private string ResolveDialogTitle(Guid agentId)
    {
        var agent = agents.FirstOrDefault(item => item.Id == agentId);
        return string.IsNullOrWhiteSpace(agent?.Name)
            ? "Agent details"
            : agent.Name;
    }

    private async Task HandleAgentDialogSavedAsync(AgentDetailsDialogResult result)
    {
        await ReloadCatalogAsync();
        if (result.Deleted)
        {
            selectedAgentId = null;
            await SelectedAgentChanged.InvokeAsync(null);
            await InvokeAsync(StateHasChanged);
            return;
        }

        if (result.AgentId.HasValue)
        {
            selectedAgentId = result.AgentId.Value;
            await SelectedAgentChanged.InvokeAsync(agents.FirstOrDefault(item => item.Id == result.AgentId.Value));
        }

        await InvokeAsync(StateHasChanged);
    }

    private async Task OpenTeamDetailsDialogAsync(Guid? teamId)
    {
        try
        {
            var result = await DialogService.OpenAsync<AgentTeamDetailsDialog>(
                teamId.HasValue ? "Edit agent team" : "New agent team",
                new Dictionary<string, object?>
                {
                    [nameof(AgentTeamDetailsDialog.TeamId)] = teamId
                },
                new DialogOptions
                {
                    Eyebrow = "Technical team",
                    Subtitle = "Create or rename an AgentFramework team for filtering and process delivery.",
                    Size = ModalSize.Compact,
                    AriaLabel = "Agent team editor",
                    TestId = "agents-team-details-dialog"
                });

            if (result is AgentTeamDetailsDialogResult teamResult)
            {
                await ReloadCatalogAsync();
                selectedTeamId = teamResult.TeamId;
                expandedTreeNodeIds.Add(BuildTeamTreeNodeId(teamResult.TeamId));
                await SelectedTeamChanged.InvokeAsync(SelectedTeam);
                await InvokeAsync(StateHasChanged);
            }
        }
        catch (Exception exception)
        {
            NotificationService.Error("Team dialog failed", exception.Message);
        }
    }

    private async Task OpenTeamMembersDialogAsync()
    {
        var team = SelectedTeam;
        if (team is null)
        {
            return;
        }

        try
        {
            var result = await DialogService.OpenAsync<AgentTeamMembersDialog>(
                $"Add agents to {team.Name}",
                new Dictionary<string, object?>
                {
                    [nameof(AgentTeamMembersDialog.Team)] = team,
                    [nameof(AgentTeamMembersDialog.Agents)] = agents,
                    [nameof(AgentTeamMembersDialog.PrivateAgentIds)] = ResolvePrivateAgentIds()
                },
                new DialogOptions
                {
                    Eyebrow = "Team membership",
                    Subtitle = "Click agent cards to select multiple agents, then confirm the team membership.",
                    Size = ModalSize.Wide,
                    AriaLabel = "Add agents to team",
                    TestId = "agents-team-members-dialog-shell"
                });

            if (result is AgentTeamMembersDialogResult membersResult)
            {
                await WorkspaceService.UpdateAgentTeamMembersAsync(
                    membersResult.TeamId,
                    membersResult.AgentIds,
                    CancellationToken.None);
                await ReloadCatalogAsync();
                selectedTeamId = membersResult.TeamId;
                expandedTreeNodeIds.Add(BuildTeamTreeNodeId(membersResult.TeamId));
                await SelectedTeamChanged.InvokeAsync(SelectedTeam);
                NotificationService.Success("Team updated", "Agent team membership was saved.");
                await InvokeAsync(StateHasChanged);
            }
        }
        catch (Exception exception)
        {
            NotificationService.Error("Team membership failed", exception.Message);
        }
    }

    private async Task DeleteSelectedTeamAsync()
    {
        var team = SelectedTeam;
        if (team is null)
        {
            return;
        }

        try
        {
            await WorkspaceService.DeleteAgentTeamAsync(team.Id);
            selectedTeamId = null;
            await ReloadCatalogAsync();
            await SelectedTeamChanged.InvokeAsync(null);
            NotificationService.Success("Team deleted", $"Deleted {team.Name}.");
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception exception)
        {
            NotificationService.Error("Team delete failed", exception.Message);
        }
    }

    private void ResetAgentSearch()
    {
        agentSearch = string.Empty;
    }

    private async Task ReloadCatalogAsync()
    {
        var agentsTask = WorkspaceService.ListAgentsAsync(includeTemplates: false);
        var providersTask = WorkspaceService.ListProvidersAsync();
        var teamsTask = WorkspaceService.ListAgentTeamsAsync();
        agents = (await agentsTask).ToList();
        privateProviderById = BuildPrivateProviderMap(await providersTask);
        teams = (await teamsTask).ToList();
        if (selectedTeamId.HasValue &&
            teams.All(item => item.Id != selectedTeamId.Value))
        {
            selectedTeamId = null;
        }

        ExpandTeamNodes();
    }

    private void ExpandTeamNodes()
    {
        expandedTreeNodeIds.Add(TeamRootTreeNodeId);
        foreach (var team in teams)
        {
            expandedTreeNodeIds.Add(BuildTeamTreeNodeId(team.Id));
        }
    }

    private bool MatchesSelectedTeam(AgentDefinition agent)
    {
        var team = SelectedTeam;
        return team is null || team.AgentIds.Contains(agent.Id);
    }

    private void ApplyRequestedTeam(bool force = false)
    {
        if (!force &&
            appliedRequestedTeamId == RequestedTeamId)
        {
            return;
        }

        appliedRequestedTeamId = RequestedTeamId;
        if (!RequestedTeamId.HasValue)
        {
            selectedTeamId = null;
            return;
        }

        selectedTeamId = teams.Any(item => item.Id == RequestedTeamId.Value)
            ? RequestedTeamId.Value
            : null;

        if (selectedTeamId.HasValue)
        {
            expandedTreeNodeIds.Add(BuildTeamTreeNodeId(selectedTeamId.Value));
        }
    }

    private bool UsesPrivateProvider(AgentDefinition agent)
    {
        return agent.ProviderProfileId.HasValue &&
               privateProviderById.TryGetValue(agent.ProviderProfileId.Value, out var isPrivateProvider) &&
               isPrivateProvider;
    }

    private bool HasValidRequestedSelection()
        => (!RequestedAgentId.HasValue || agents.Any(item => item.Id == RequestedAgentId.Value)) &&
           (!RequestedTeamId.HasValue || teams.Any(item => item.Id == RequestedTeamId.Value));

    private async Task PublishAccessStateAsync(AgentChatContextAccessState state)
    {
        if (publishedAccessState == state)
        {
            return;
        }

        publishedAccessState = state;
        await ContextAccessStateChanged.InvokeAsync(state);
    }

    private IReadOnlyCollection<Guid> ResolvePrivateAgentIds()
    {
        return agents
            .Where(UsesPrivateProvider)
            .Select(agent => agent.Id)
            .ToHashSet();
    }

    private static IReadOnlyDictionary<Guid, bool> BuildPrivateProviderMap(IReadOnlyList<ProviderProfile> providers)
    {
        return providers.ToDictionary(
            provider => provider.Id,
            provider => provider.IsPrivateProvider);
    }

    private bool MatchesAgentSearch(AgentDefinition agent)
    {
        if (string.IsNullOrWhiteSpace(agentSearch))
        {
            return true;
        }

        return agent.Name.Contains(agentSearch, StringComparison.OrdinalIgnoreCase) ||
               agent.RoleTitle.Contains(agentSearch, StringComparison.OrdinalIgnoreCase) ||
               agent.Summary.Contains(agentSearch, StringComparison.OrdinalIgnoreCase) ||
               agent.Model.Contains(agentSearch, StringComparison.OrdinalIgnoreCase) ||
               agent.Tags.Any(tag => tag.Contains(agentSearch, StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildTeamTreeNodeId(Guid teamId)
        => $"{TeamTreeNodePrefix}{teamId:N}";

    private static string BuildAgentTreeNodeId(Guid agentId)
        => $"{AgentTreeNodePrefix}{agentId:N}";

    private static bool TryParseTeamTreeNodeId(string nodeId, out Guid teamId)
        => TryParsePrefixedGuid(nodeId, TeamTreeNodePrefix, out teamId);

    private static bool TryParseAgentTreeNodeId(string nodeId, out Guid agentId)
        => TryParsePrefixedGuid(nodeId, AgentTreeNodePrefix, out agentId);

    private static bool TryParsePrefixedGuid(string nodeId, string prefix, out Guid id)
    {
        id = Guid.Empty;
        return nodeId.StartsWith(prefix, StringComparison.Ordinal) &&
               Guid.TryParseExact(nodeId[prefix.Length..], "N", out id);
    }
}
