using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Capabilities.Templates;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

public partial class AgentCapabilitiesPanel
{
    private const string AgentRootTreeNodeId = "capabilities:agents";
    private const string AgentTreeNodePrefix = "capabilities:agent:";

    [Parameter]
    public Guid? PreferredAgentId { get; set; }

    [Parameter]
    public EventCallback<AgentDefinition?> SelectedAgentChanged { get; set; }

    [Parameter]
    public EventCallback<AgentChatContextAccessState> ContextAccessStateChanged { get; set; }

    [Inject]
    public IAgentFrameworkWorkspaceService WorkspaceService { get; set; } = default!;

    [Inject]
    public IAgentCapabilitySetupFlowService CapabilitySetupFlowService { get; set; } = default!;

    [Inject]
    public NotificationService NotificationService { get; set; } = default!;

    [Inject]
    public DialogService DialogService { get; set; } = default!;

    private readonly HashSet<string> expandedAgentTreeNodeIds = [AgentRootTreeNodeId];
    private IReadOnlyList<AgentDefinition> agents = [];
    private IReadOnlyList<CapabilityCatalogItem> capabilities = [];
    private AgentDefinition? selectedAgent;
    private AgentEditorModel? selectedAgentEditor;
    private Guid? selectedAgentId;
    private IReadOnlyList<string> capabilityTagFilters = [];
    private string capabilitySearch = string.Empty;
    private CapabilityAssignmentFilter assignmentFilter = CapabilityAssignmentFilter.All;
    private CapabilityTypeFilter typeFilter = CapabilityTypeFilter.All;
    private string accessRuleEffect = "deny";
    private string accessRuleScope = "uiPreview";
    private string accessRuleSelectorKind = "operationClassification";
    private string accessRuleSelectorValue = "externalAction";
    private string accessRuleSelectorServerKey = string.Empty;
    private string accessRuleReason = "UI preview denies matching capabilities.";
    private CapabilityAccessPreviewResult? accessPreviewResult;
    private bool isBusy;
    private bool isAccessPreviewBusy;
    private long selectionGeneration;
    private AgentChatContextAccessState? publishedAccessState;
    private Guid? appliedPreferredAgentId;
    private bool preferredAgentApplied;

    private IReadOnlyList<TreeViewNode> AgentTreeNodes
    {
        get
        {
            var selectedId = selectedAgentId;
            return
            [
                new TreeViewNode
                {
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
                        .Select(agent => new TreeViewNode
                        {
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
        .OrderByDescending(capability => selectedAgentEditor?.SelectedCapabilityIds.Contains(capability.Id) == true)
        .ThenBy(capability => capability.Kind)
        .ThenBy(capability => capability.Name, StringComparer.OrdinalIgnoreCase)
        .ToList();

    private IReadOnlyList<string> AvailableCapabilityTags => capabilities
        .SelectMany(capability => capability.Tags)
        .Where(tag => !string.IsNullOrWhiteSpace(tag))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
        .ToList();

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
    }

    protected override async Task OnParametersSetAsync()
    {
        if (preferredAgentApplied && appliedPreferredAgentId == PreferredAgentId)
        {
            return;
        }

        preferredAgentApplied = true;
        appliedPreferredAgentId = PreferredAgentId;
        if (PreferredAgentId.HasValue &&
            agents.All(item => item.Id != PreferredAgentId.Value))
        {
            Interlocked.Increment(ref selectionGeneration);
            selectedAgentId = null;
            selectedAgent = null;
            selectedAgentEditor = null;
            await SelectedAgentChanged.InvokeAsync(null);
            await PublishAccessStateAsync(AgentChatContextAccessState.Failed);
            return;
        }

        var agentId = PreferredAgentId ??
                      (selectedAgentId.HasValue && agents.Any(item => item.Id == selectedAgentId.Value)
                          ? selectedAgentId.Value
                          : agents.FirstOrDefault()?.Id);
        if (agentId.HasValue)
        {
            await SelectAgentAsync(agentId.Value);
        }
    }

    private async Task LoadAsync()
    {
        await PublishAccessStateAsync(AgentChatContextAccessState.Loading);
        try
        {
            agents = await WorkspaceService.ListAgentsAsync(includeTemplates: false);
            capabilities = await WorkspaceService.ListCapabilitiesAsync();
            preferredAgentApplied = true;
            appliedPreferredAgentId = PreferredAgentId;
        }
        catch
        {
            await PublishAccessStateAsync(AgentChatContextAccessState.Failed);
            throw;
        }

        expandedAgentTreeNodeIds.Add(AgentRootTreeNodeId);
        if (agents.Count == 0)
        {
            selectedAgent = null;
            selectedAgentEditor = null;
            selectedAgentId = null;
            await SelectedAgentChanged.InvokeAsync(null);
            await PublishAccessStateAsync(
                PreferredAgentId.HasValue
                    ? AgentChatContextAccessState.Failed
                    : AgentChatContextAccessState.Ready);
            return;
        }

        if (PreferredAgentId.HasValue &&
            agents.All(item => item.Id != PreferredAgentId.Value))
        {
            Interlocked.Increment(ref selectionGeneration);
            selectedAgent = null;
            selectedAgentEditor = null;
            selectedAgentId = null;
            await SelectedAgentChanged.InvokeAsync(null);
            await PublishAccessStateAsync(AgentChatContextAccessState.Failed);
            return;
        }

        var initialAgentId = PreferredAgentId.HasValue &&
                             agents.Any(item => item.Id == PreferredAgentId.Value)
            ? PreferredAgentId.Value
            : selectedAgentId is { } currentAgentId &&
              agents.Any(item => item.Id == currentAgentId)
                ? currentAgentId
                : agents[0].Id;

        await SelectAgentAsync(initialAgentId);
    }

    private Task HandleAgentTreeSelectAsync(string nodeId)
    {
        if (TryParseAgentTreeNodeId(nodeId, out var agentId) &&
            agents.Any(item => item.Id == agentId))
        {
            return SelectAgentAsync(agentId);
        }

        return Task.CompletedTask;
    }

    private Task HandleAgentTreeToggleAsync(string nodeId)
    {
        if (!expandedAgentTreeNodeIds.Add(nodeId))
        {
            expandedAgentTreeNodeIds.Remove(nodeId);
        }

        return Task.CompletedTask;
    }

    private async Task SelectAgentAsync(Guid agentId)
    {
        var generation = Interlocked.Increment(ref selectionGeneration);
        await PublishAccessStateAsync(AgentChatContextAccessState.Loading);
        var agent = agents.FirstOrDefault(item => item.Id == agentId);
        AgentEditorModel editor;
        try
        {
            editor = await WorkspaceService.GetAgentEditorAsync(agentId);
        }
        catch
        {
            if (generation == Volatile.Read(ref selectionGeneration))
            {
                await PublishAccessStateAsync(AgentChatContextAccessState.Failed);
            }

            throw;
        }

        if (generation != Volatile.Read(ref selectionGeneration))
        {
            return;
        }

        selectedAgentId = agentId;
        selectedAgent = agent;
        selectedAgentEditor = editor;
        await SelectedAgentChanged.InvokeAsync(agent);
        await PublishAccessStateAsync(
            agent is null
                ? AgentChatContextAccessState.Failed
                : AgentChatContextAccessState.Ready);
    }

    private async Task PublishAccessStateAsync(AgentChatContextAccessState state)
    {
        if (publishedAccessState == state)
        {
            return;
        }

        publishedAccessState = state;
        await ContextAccessStateChanged.InvokeAsync(state);
    }

    private async Task ToggleCapabilityAsync(Guid capabilityId)
    {
        if (selectedAgentEditor is null || !selectedAgentId.HasValue)
        {
            return;
        }

        isBusy = true;
        try
        {
            var selectedCapabilityIds = selectedAgentEditor.SelectedCapabilityIds.ToList();
            if (selectedCapabilityIds.Contains(capabilityId))
            {
                selectedCapabilityIds.Remove(capabilityId);
            }
            else
            {
                selectedCapabilityIds.Add(capabilityId);
            }

            selectedAgentEditor.SelectedCapabilityIds = selectedCapabilityIds;
            await WorkspaceService.SaveAgentAsync(selectedAgentEditor);
            await LoadAsync();
            SetMessage("Ready", "success", "Capability assignment updated.");
        }
        catch (Exception exception)
        {
            SetMessage("Attention", "danger", exception.Message);
        }
        finally
        {
            isBusy = false;
        }
    }

    private async Task VerifyCapabilityAsync(Guid capabilityId)
    {
        if (!selectedAgentId.HasValue)
        {
            return;
        }

        isBusy = true;
        try
        {
            await WorkspaceService.VerifyCapabilityAsync(selectedAgentId.Value, capabilityId);
            capabilities = await WorkspaceService.ListCapabilitiesAsync();
            selectedAgentEditor = await WorkspaceService.GetAgentEditorAsync(selectedAgentId.Value);
            SetMessage("Ready", "success", "Capability verification completed.");
        }
        catch (Exception exception)
        {
            SetMessage("Attention", "danger", exception.Message);
        }
        finally
        {
            isBusy = false;
        }
    }

    private async Task OpenCapabilityDetailsDialogAsync(Guid capabilityId)
    {
        var capability = capabilities.FirstOrDefault(item => item.Id == capabilityId);
        try
        {
            accessPreviewResult = null;
            var result = await DialogService.OpenAsync<CapabilityDetailsDialog>(
                capability?.Name ?? "Capability details",
                new Dictionary<string, object?>
                {
                    [nameof(CapabilityDetailsDialog.CapabilityId)] = capabilityId,
                    [nameof(CapabilityDetailsDialog.TagSuggestions)] = AvailableCapabilityTags
                },
                new DialogOptions
                {
                    Eyebrow = "Capability metadata",
                    Subtitle = "Inspect and edit capability tags, identity, and type-specific configuration.",
                    Size = ModalSize.Wide,
                    DenseChrome = true,
                    AriaLabel = "Capability details",
                    TestId = "agents-capability-details-dialog"
                });

            if (result is CapabilityDetailsDialogResult)
            {
                await ReloadCapabilitiesAsync();
            }
        }
        catch (Exception exception)
        {
            NotificationService.Error("Capability dialog failed", exception.Message);
        }
    }

    private async Task OpenCapabilityWizardAsync(CapabilityKind initialKind)
    {
        try
        {
            var result = await DialogService.OpenAsync<CapabilitySetupWizardDialog>(
                ResolveCapabilityWizardTitle(initialKind),
                new Dictionary<string, object?>
                {
                    [nameof(CapabilitySetupWizardDialog.InitialKind)] = initialKind,
                    [nameof(CapabilitySetupWizardDialog.TagSuggestions)] = AvailableCapabilityTags
                },
                new DialogOptions
                {
                    Eyebrow = "Capability setup",
                    Subtitle = "Create a skill, tool, or MCP capability for assignment to technical agents.",
                    Size = ModalSize.Wide,
                    DenseChrome = true,
                    AriaLabel = "Capability setup wizard",
                    TestId = "agents-capability-setup-dialog"
                });

            if (result is CapabilityDetailsDialogResult)
            {
                await ReloadCapabilitiesAsync();
                SetMessage("Ready", "success", "Capability created.");
            }
        }
        catch (Exception exception)
        {
            NotificationService.Error("Capability wizard failed", exception.Message);
        }
    }

    private async Task ReloadCapabilitiesAsync()
    {
        capabilities = await WorkspaceService.ListCapabilitiesAsync();
        accessPreviewResult = null;
        if (selectedAgentId.HasValue)
        {
            selectedAgentEditor = await WorkspaceService.GetAgentEditorAsync(selectedAgentId.Value);
        }

        await InvokeAsync(StateHasChanged);
    }

    private Task HandleCapabilityTagFiltersChangedAsync(IReadOnlyList<string> value)
    {
        capabilityTagFilters = value;
        return Task.CompletedTask;
    }

    private void ResetCapabilityFilters()
    {
        capabilitySearch = string.Empty;
        capabilityTagFilters = [];
        assignmentFilter = CapabilityAssignmentFilter.All;
        typeFilter = CapabilityTypeFilter.All;
    }

    private async Task PreviewAccessAsync()
    {
        if (isAccessPreviewBusy)
        {
            return;
        }

        isAccessPreviewBusy = true;
        try
        {
            var policy = new CapabilityAccessPolicyTemplateDto
            {
                DefaultEffect = "inherit",
                Rules =
                [
                    new CapabilityAccessRuleTemplateDto
                    {
                        Id = "ui-preview-rule",
                        Effect = accessRuleEffect,
                        Scope = accessRuleScope,
                        Selector = new CapabilitySelectorTemplateDto
                        {
                            Kind = accessRuleSelectorKind,
                            Value = accessRuleSelectorValue,
                            ServerKey = accessRuleSelectorServerKey
                        },
                        Reason = accessRuleReason
                    }
                ]
            };

            accessPreviewResult = await CapabilitySetupFlowService.PreviewAccessAsync(new CapabilityAccessPreviewRequest
            {
                CapabilityIds = selectedAgentEditor?.SelectedCapabilityIds ?? [],
                Policy = policy
            });

            if (accessPreviewResult.ValidationResult.IsValid)
            {
                NotificationService.Success("Access preview ready", "Capability access policy preview completed.");
            }
            else
            {
                NotificationService.Warning("Access preview has validation issues", "Review the policy diagnostics.");
            }
        }
        catch (Exception exception)
        {
            NotificationService.Error("Access preview failed", exception.Message);
        }
        finally
        {
            isAccessPreviewBusy = false;
        }
    }

    private bool MatchesCapabilitySearch(CapabilityCatalogItem capability)
    {
        if (string.IsNullOrWhiteSpace(capabilitySearch))
        {
            return true;
        }

        return capability.Name.Contains(capabilitySearch, StringComparison.OrdinalIgnoreCase) ||
               capability.Key.Contains(capabilitySearch, StringComparison.OrdinalIgnoreCase) ||
               capability.Description.Contains(capabilitySearch, StringComparison.OrdinalIgnoreCase) ||
               capability.EndpointOrPath.Contains(capabilitySearch, StringComparison.OrdinalIgnoreCase) ||
               capability.Tags.Any(tag => tag.Contains(capabilitySearch, StringComparison.OrdinalIgnoreCase));
    }

    private bool MatchesCapabilityTagFilters(CapabilityCatalogItem capability)
    {
        if (capabilityTagFilters.Count == 0)
        {
            return true;
        }

        return capabilityTagFilters.All(filter =>
            capability.Tags.Any(tag => string.Equals(tag, filter, StringComparison.OrdinalIgnoreCase)));
    }

    private bool MatchesAssignmentFilter(CapabilityCatalogItem capability)
    {
        var isAssigned = selectedAgentEditor?.SelectedCapabilityIds.Contains(capability.Id) == true;
        return assignmentFilter switch
        {
            CapabilityAssignmentFilter.Assigned => isAssigned,
            CapabilityAssignmentFilter.NotAssigned => !isAssigned,
            _ => true
        };
    }

    private bool MatchesTypeFilter(CapabilityCatalogItem capability)
    {
        return typeFilter switch
        {
            CapabilityTypeFilter.Mcp => capability.Kind == CapabilityKind.McpServer,
            CapabilityTypeFilter.Skill => capability.Kind == CapabilityKind.Skill,
            CapabilityTypeFilter.Tool => capability.Kind == CapabilityKind.Tool,
            _ => true
        };
    }

    private int ResolveAssignedCount(Guid agentId)
    {
        if (selectedAgentId == agentId && selectedAgentEditor is not null)
        {
            return selectedAgentEditor.SelectedCapabilityIds.Count;
        }

        var agent = agents.FirstOrDefault(item => item.Id == agentId);
        return agent?.Capabilities.Count ?? 0;
    }

    private void SetMessage(string label, string tone, string value)
    {
        switch (tone)
        {
            case "success":
                NotificationService.Success(label, value);
                break;
            case "warning":
                NotificationService.Warning(label, value);
                break;
            case "danger":
                NotificationService.Error(label, value);
                break;
            default:
                NotificationService.Info(label, value);
                break;
        }
    }

    private static string ResolveAgentMeta(AgentDefinition agent)
    {
        return string.IsNullOrWhiteSpace(agent.Model)
            ? "No model configured"
            : agent.Model;
    }

    private static string ResolveCapabilityKindLabel(CapabilityKind kind)
    {
        return kind switch
        {
            CapabilityKind.McpServer => "MCP server",
            CapabilityKind.AiContext => "AI context",
            _ => kind.ToString()
        };
    }

    private static string ResolveCapabilityWizardTitle(CapabilityKind kind)
    {
        return kind switch
        {
            CapabilityKind.McpServer => "New MCP server",
            CapabilityKind.Tool => "New tool",
            _ => "New skill"
        };
    }

    private static string ResolveEndpointSummary(CapabilityCatalogItem capability)
    {
        return string.IsNullOrWhiteSpace(capability.EndpointOrPath)
            ? "No endpoint or path is stored for this capability."
            : capability.EndpointOrPath;
    }

    private static string BuildAgentTreeNodeId(Guid agentId)
        => $"{AgentTreeNodePrefix}{agentId:N}";

    private static bool TryParseAgentTreeNodeId(string nodeId, out Guid agentId)
    {
        agentId = Guid.Empty;
        return nodeId.StartsWith(AgentTreeNodePrefix, StringComparison.Ordinal) &&
               Guid.TryParseExact(nodeId[AgentTreeNodePrefix.Length..], "N", out agentId);
    }

    private enum CapabilityAssignmentFilter
    {
        All,
        Assigned,
        NotAssigned
    }

    private enum CapabilityTypeFilter
    {
        All,
        Mcp,
        Skill,
        Tool
    }
}
