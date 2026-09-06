using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using Microsoft.AspNetCore.Components;
using AccessEffect = CanDoItAll.AgentFramework.Capabilities.Abstractions.CapabilityAccessEffect;
using AccessScope = CanDoItAll.AgentFramework.Capabilities.Abstractions.CapabilityAccessScope;
using SelectorKind = CanDoItAll.AgentFramework.Capabilities.Abstractions.CapabilitySelectorKind;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

public partial class AgentCapabilitiesSurface {
    private const string AgentRootTreeNodeId = "capabilities:agents";
    private const string AgentTreeNodePrefix = "capabilities:agent:";

    [Parameter]
    public AgentCapabilitiesSnapshot Snapshot { get; set; } = AgentCapabilitiesSnapshot.Empty;

    [Parameter]
    public AgentCapabilitiesSelection Selection { get; set; } = new(null);

    [Parameter]
    public AgentCapabilitiesLoadState LoadState { get; set; }

    [Parameter]
    public EventCallback<AgentCapabilitiesIntent> Intent { get; set; }

    private readonly HashSet<string> expandedAgentTreeNodeIds = [AgentRootTreeNodeId];
    private IReadOnlyList<string> capabilityTagFilters = [];
    private string capabilitySearch = string.Empty;
    private CapabilityAssignmentFilter assignmentFilter;
    private CapabilityTypeFilter typeFilter;
    private AccessEffect accessRuleEffect = AccessEffect.Deny;
    private AccessScope accessRuleScope = AccessScope.UiPreview;
    private SelectorKind accessRuleSelectorKind = SelectorKind.OperationClassification;
    private string accessRuleSelectorValue = "externalAction";
    private string accessRuleSelectorServerKey = string.Empty;
    private string accessRuleReason = "UI preview denies matching capabilities.";

    private IReadOnlyList<AgentCapabilitiesAgent> agents => Snapshot.Agents;
    private IReadOnlyList<CapabilityCatalogItem> capabilities => Snapshot.Capabilities;
    private Guid? selectedAgentId => Selection.AgentId;
    private AgentCapabilitiesAgent? selectedAgent => agents.FirstOrDefault(agent => agent.Id == selectedAgentId);
    private bool hasSelection => LoadState == AgentCapabilitiesLoadState.Ready && selectedAgent is not null;
    private bool isBusy => Snapshot.IsBusy;
    private bool isAccessPreviewBusy => Snapshot.IsAccessPreviewBusy;
    private bool isOpeningCapabilityWizard => Snapshot.IsOpeningWizard;
    private AgentCapabilityPreview? accessPreviewResult => Snapshot.Preview;
    private string CapabilityCuratorDisplayName => Snapshot.Curator.Name;
    private string CapabilityCuratorAvatarImageUrl => Snapshot.Curator.AvatarImageUrl;
    private bool CanOpenCapabilityCurator => Snapshot.Curator.CanLaunch && !Snapshot.IsBusy && !Snapshot.IsOpeningCurator;

    private Task Emit(AgentCapabilitiesIntent intent) => Intent.InvokeAsync(intent);
    private Task ToggleCapabilityAsync(Guid id) => Emit(new AgentCapabilitiesIntent.ToggleAssignment(id));
    private Task VerifyCapabilityAsync(Guid id) => Emit(new AgentCapabilitiesIntent.VerifyCapability(id));
    private Task OpenCapabilityDetailsDialogAsync(Guid id) => Emit(new AgentCapabilitiesIntent.OpenDetails(id));
    private Task OpenCapabilityWizardAsync(CapabilityKind kind) => Emit(new AgentCapabilitiesIntent.CreateCapability(kind));
    private Task OpenCapabilityCuratorAsync() => CanOpenCapabilityCurator
        ? Emit(new AgentCapabilitiesIntent.OpenCurator()) : Task.CompletedTask;
    private Task PreviewAccessAsync() => Emit(new AgentCapabilitiesIntent.PreviewAccess(new(
        accessRuleEffect, accessRuleScope, accessRuleSelectorKind,
        accessRuleSelectorValue, accessRuleSelectorServerKey, accessRuleReason)));

    private IReadOnlyList<TreeViewNode> AgentTreeNodes {
        get {
            var selectedId = selectedAgentId;
            return
            [
                new TreeViewNode {
                    Id = AgentRootTreeNodeId,
                    Text = "Agents",
                    Icon = "support_agent",
                    BadgeText = agents.Count.ToString(),
                    IsExpanded = expandedAgentTreeNodeIds.Contains(AgentRootTreeNodeId),
                    IsSelectable = false,
                    DataTestId = "agents-capability-tree-root",
                    ChildrenDataTestId = "agents-capability-tree-root-children",
                    Children = agents
                        .OrderBy(agent => agent.Name, StringComparer.OrdinalIgnoreCase)
                        .Select(agent => new TreeViewNode {
                            Id = BuildAgentTreeNodeId(agent.Id),
                            Text = agent.Name,
                            Icon = "person",
                            Tooltip = $"{agent.RoleTitle}. {ResolveAgentMeta(agent)}.",
                            BadgeText = ResolveAssignedCount(agent.Id).ToString(),
                            IsSelected = selectedId == agent.Id,
                            DataTestId = "agents-capability-tree-agent"
                        })
                        .ToArray()
                }
            ];
        }
    }

    private IReadOnlyList<CapabilityCatalogItem> FilteredCapabilities => capabilities
        .Where(MatchesCapabilitySearch)
        .Where(MatchesCapabilityTagFilters)
        .Where(MatchesAssignmentFilter)
        .Where(MatchesTypeFilter)
        .OrderByDescending(capability => Snapshot.SelectedCapabilityIds.Contains(capability.Id))
        .ThenBy(capability => capability.Kind)
        .ThenBy(capability => capability.Name, StringComparer.OrdinalIgnoreCase)
        .ToList();

    private IReadOnlyList<string> AvailableCapabilityTags => capabilities
        .SelectMany(capability => capability.Tags)
        .Where(tag => !string.IsNullOrWhiteSpace(tag))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
        .ToList();

    private Task HandleAgentTreeSelectAsync(string nodeId) {
        if (TryParseAgentTreeNodeId(nodeId, out var agentId) &&
            agents.Any(item => item.Id == agentId)) {
            return Emit(new AgentCapabilitiesIntent.SelectAgent(agentId));
        }

        return Task.CompletedTask;
    }

    private Task HandleAgentTreeToggleAsync(string nodeId) {
        if (!expandedAgentTreeNodeIds.Add(nodeId)) {
            expandedAgentTreeNodeIds.Remove(nodeId);
        }

        return Task.CompletedTask;
    }

    private Task HandleCapabilityTagFiltersChangedAsync(IReadOnlyList<string> value) {
        capabilityTagFilters = value;
        return Task.CompletedTask;
    }

    private void ResetCapabilityFilters() {
        capabilitySearch = string.Empty;
        capabilityTagFilters = [];
        assignmentFilter = CapabilityAssignmentFilter.All;
        typeFilter = CapabilityTypeFilter.All;
    }

    private bool MatchesCapabilitySearch(CapabilityCatalogItem capability) {
        if (string.IsNullOrWhiteSpace(capabilitySearch)) {
            return true;
        }

        return capability.Name.Contains(capabilitySearch, StringComparison.OrdinalIgnoreCase) ||
               capability.Key.Contains(capabilitySearch, StringComparison.OrdinalIgnoreCase) ||
               capability.Description.Contains(capabilitySearch, StringComparison.OrdinalIgnoreCase) ||
               capability.EndpointOrPath.Contains(capabilitySearch, StringComparison.OrdinalIgnoreCase) ||
               capability.Tags.Any(tag => tag.Contains(capabilitySearch, StringComparison.OrdinalIgnoreCase));
    }

    private bool MatchesCapabilityTagFilters(CapabilityCatalogItem capability) {
        if (capabilityTagFilters.Count == 0) {
            return true;
        }

        return capabilityTagFilters.All(filter =>
            capability.Tags.Any(tag => string.Equals(tag, filter, StringComparison.OrdinalIgnoreCase)));
    }

    private bool MatchesAssignmentFilter(CapabilityCatalogItem capability) {
        var isAssigned = Snapshot.SelectedCapabilityIds.Contains(capability.Id);
        return assignmentFilter switch {
            CapabilityAssignmentFilter.Assigned => isAssigned,
            CapabilityAssignmentFilter.NotAssigned => !isAssigned,
            _ => true
        };
    }

    private bool MatchesTypeFilter(CapabilityCatalogItem capability) {
        return typeFilter switch {
            CapabilityTypeFilter.Mcp => capability.Kind == CapabilityKind.McpServer,
            CapabilityTypeFilter.Skill => capability.Kind == CapabilityKind.Skill,
            CapabilityTypeFilter.Tool => capability.Kind == CapabilityKind.Tool,
            _ => true
        };
    }

    private int ResolveAssignedCount(Guid id) => id == selectedAgentId && hasSelection
        ? Snapshot.SelectedCapabilityIds.Length : agents.FirstOrDefault(agent => agent.Id == id)?.AssignedCount ?? 0;

    private static string ResolveAgentMeta(AgentCapabilitiesAgent agent)
        => string.IsNullOrWhiteSpace(agent.Model) ? "No model configured" : agent.Model;

    private static string BuildAgentTreeNodeId(Guid agentId)
        => $"{AgentTreeNodePrefix}{agentId:N}";

    private static bool TryParseAgentTreeNodeId(string nodeId, out Guid agentId) {
        agentId = Guid.Empty;
        return nodeId.StartsWith(AgentTreeNodePrefix, StringComparison.Ordinal) &&
               Guid.TryParseExact(nodeId[AgentTreeNodePrefix.Length..], "N", out agentId);
    }

    private enum CapabilityAssignmentFilter {
        All,
        Assigned,
        NotAssigned
    }

    private enum CapabilityTypeFilter {
        All,
        Mcp,
        Skill,
        Tool
    }
}
