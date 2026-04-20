using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

public partial class AgentCapabilitiesPanel
{
    [Parameter]
    public Guid? PreferredAgentId { get; set; }

    [Inject]
    public IAgentFrameworkWorkspaceService WorkspaceService { get; set; } = default!;

    private IReadOnlyList<AgentDefinition> agents = [];
    private IReadOnlyList<CapabilityCatalogItem> capabilities = [];
    private AgentDefinition? selectedAgent;
    private AgentEditorModel? selectedAgentEditor;
    private Guid? selectedAgentId;
    private bool isBusy;
    private string message = string.Empty;
    private string messageTone = "info";
    private string messageLabel = "Info";

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
    }

    protected override async Task OnParametersSetAsync()
    {
        if (PreferredAgentId.HasValue &&
            PreferredAgentId != selectedAgentId &&
            agents.Any(item => item.Id == PreferredAgentId.Value))
        {
            await SelectAgentAsync(PreferredAgentId.Value);
        }
    }

    private async Task LoadAsync()
    {
        agents = await WorkspaceService.ListAgentsAsync(includeTemplates: false);
        capabilities = await WorkspaceService.ListCapabilitiesAsync();
        if (agents.Count == 0)
        {
            selectedAgent = null;
            selectedAgentEditor = null;
            selectedAgentId = null;
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

    private async Task SelectAgentAsync(Guid agentId)
    {
        selectedAgentId = agentId;
        selectedAgent = agents.FirstOrDefault(item => item.Id == agentId);
        selectedAgentEditor = await WorkspaceService.GetAgentEditorAsync(agentId);
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

    private void SetMessage(string label, string tone, string value)
    {
        messageLabel = label;
        messageTone = tone;
        message = value;
    }

    private static string ResolveAgentMeta(AgentDefinition agent)
    {
        return string.IsNullOrWhiteSpace(agent.Model)
            ? "No model configured"
            : agent.Model;
    }

    private static string ResolveAgentTone(AgentLifecycleStatus status)
    {
        return status switch
        {
            AgentLifecycleStatus.Active => "success",
            AgentLifecycleStatus.Suspended => "warning",
            AgentLifecycleStatus.Archived => "neutral",
            _ => "info"
        };
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

    private static string ResolveProofTone(CapabilityProofStatus status)
    {
        return status switch
        {
            CapabilityProofStatus.Verified => "success",
            CapabilityProofStatus.PendingReview => "warning",
            CapabilityProofStatus.Failed => "danger",
            _ => "neutral"
        };
    }

    private static string ResolveProofLabel(CapabilityProofStatus status)
    {
        return status switch
        {
            CapabilityProofStatus.Verified => "Verified",
            CapabilityProofStatus.PendingReview => "Pending review",
            CapabilityProofStatus.Failed => "Failed",
            _ => "Not run"
        };
    }

    private static string ResolveEndpointSummary(CapabilityCatalogItem capability)
    {
        return string.IsNullOrWhiteSpace(capability.EndpointOrPath)
            ? "No endpoint or path is stored for this capability."
            : capability.EndpointOrPath;
    }
}
