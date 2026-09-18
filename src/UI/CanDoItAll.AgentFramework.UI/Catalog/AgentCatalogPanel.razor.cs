using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.AgentFramework.UI.Catalog;

public partial class AgentCatalogPanel
{
    private const string AllAgentsTreeNodeId = "agents:all";
    private const string TeamRootTreeNodeId = "agents:teams";
    private const string TeamTreeNodePrefix = "agents:team:";
    private const string AgentTreeNodePrefix = "agents:agent:";
    [Parameter, EditorRequired]
    public AgentCatalogSnapshot Snapshot { get; set; } = AgentCatalogSnapshot.Empty;

    [Parameter, EditorRequired]
    public AgentCatalogSelection Selection { get; set; } = new(null, null);

    [Parameter]
    public bool IsLoading { get; set; }

    [Parameter]
    public Guid? OpeningManagedAgentChatId { get; set; }

    [Parameter]
    public EventCallback<AgentCatalogIntent> Intent { get; set; }

    private IReadOnlyList<AgentDefinition> agents => Snapshot.Agents;
    private IReadOnlyList<AgentTeamDefinition> teams => Snapshot.Teams;
    private Guid? selectedAgentId => Selection.AgentId;
    private Guid? selectedTeamId => Selection.TeamId;
    private bool isLoading => IsLoading;
    private Guid? openingManagedAgentChatId => OpeningManagedAgentChatId;
    private readonly HashSet<string> expandedTreeNodeIds = [TeamRootTreeNodeId];
    private string agentSearch = string.Empty;
    private AgentCatalogSnapshot? previousSnapshot;
    private Guid? previousSelectedTeamId;

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

    protected override void OnParametersSet() {
        if (!ReferenceEquals(previousSnapshot, Snapshot)) {
            previousSnapshot = Snapshot;
            ExpandTeamNodes();
        }
        if (selectedTeamId != previousSelectedTeamId && selectedTeamId is { } teamId) {
            expandedTreeNodeIds.Add(BuildTeamTreeNodeId(teamId));
        }
        previousSelectedTeamId = selectedTeamId;
    }

    private Task SelectAgentAsync(Guid agentId)
        => Intent.InvokeAsync(new AgentCatalogIntent.SelectAgent(agentId));

    private Task OpenManagedAgentChatAsync(AgentDefinition agent)
        => Intent.InvokeAsync(new AgentCatalogIntent.OpenChat(agent.Id));

    private async Task HandleAgentTeamTreeSelectAsync(string nodeId)
    {
        if (string.Equals(nodeId, AllAgentsTreeNodeId, StringComparison.Ordinal))
        {
            await Intent.InvokeAsync(new AgentCatalogIntent.SelectTeam(null));
            return;
        }

        if (TryParseTeamTreeNodeId(nodeId, out var teamId) &&
            teams.Any(item => item.Id == teamId))
        {
            expandedTreeNodeIds.Add(nodeId);
            await Intent.InvokeAsync(new AgentCatalogIntent.SelectTeam(teamId));
            return;
        }

        if (TryParseAgentTreeNodeId(nodeId, out var agentId) &&
            agents.Any(item => item.Id == agentId))
        {
            await Intent.InvokeAsync(new AgentCatalogIntent.SelectTeamMember(agentId));
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
        => Intent.InvokeAsync(new AgentCatalogIntent.OpenAgent(null));

    private Task OpenNewTeamDialogAsync()
        => Intent.InvokeAsync(new AgentCatalogIntent.OpenTeam(null));

    private Task OpenSelectedTeamDialogAsync()
        => SelectedTeam is null ? Task.CompletedTask : Intent.InvokeAsync(new AgentCatalogIntent.OpenTeam(SelectedTeam.Id));

    private Task OpenSelectedTeamMembersDialogAsync()
        => SelectedTeam is null ? Task.CompletedTask : Intent.InvokeAsync(new AgentCatalogIntent.EditMembers(SelectedTeam.Id));

    private Task QueueAgentDetailsDialogAsync(Guid? agentId)
        => Intent.InvokeAsync(new AgentCatalogIntent.OpenAgent(agentId));

    private Task DeleteSelectedTeamAsync()
        => SelectedTeam is null ? Task.CompletedTask : Intent.InvokeAsync(new AgentCatalogIntent.DeleteTeam(SelectedTeam.Id));

    private void ResetAgentSearch()
    {
        agentSearch = string.Empty;
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

    private bool UsesPrivateProvider(AgentDefinition agent) => Snapshot.UsesPrivateProvider(agent);

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
