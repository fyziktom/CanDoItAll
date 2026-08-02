using System.Collections.Frozen;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;

namespace CanDoItAll.Modules.AgentFramework;

public static class WorkflowAgentRuntimeAuthorizationPolicy
{
    public static IReadOnlySet<AgentRuntimeToolProviderPurpose> SupportedPurposes { get; } = new[]
    {
        AgentRuntimeToolProviderPurpose.InteractiveChat,
        AgentRuntimeToolProviderPurpose.GovernedProcessAutomation,
        AgentRuntimeToolProviderPurpose.AutoApprovedNonInteractive,
        AgentRuntimeToolProviderPurpose.A2AEndpoint
    }.ToFrozenSet();

    public static bool CanAttach(AgentRuntimeToolProviderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return SupportedPurposes.Contains(context.Purpose) &&
               IsEligibleActor(context.Agent);
    }

    public static bool IsEligibleActor(AgentDefinition? agent)
    {
        return agent is
        {
            Id: var id,
            Status: AgentLifecycleStatus.Active,
            IsTemplate: false,
            Permissions.CanUseTools: true
        } && id != Guid.Empty;
    }

    public static bool IsToolAuthorized(
        AgentDefinition agent,
        IReadOnlyList<CapabilityCatalogItem> capabilityCatalog,
        string toolName)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(capabilityCatalog);

        if (!WorkflowAgentCapabilityKeys.ToolNameToCapabilityKey.TryGetValue(toolName, out var capabilityKey))
        {
            throw new InvalidOperationException(
                $"Workflow runtime tool '{toolName}' does not have a capability key mapping.");
        }

        var assignments = agent.Capabilities
            .Where(item => string.Equals(item.CapabilityKey, capabilityKey, StringComparison.Ordinal))
            .ToArray();
        if (assignments.Length != 1 || assignments[0].Kind != CapabilityKind.Tool)
        {
            return false;
        }

        var assignment = assignments[0];
        var catalogMatches = capabilityCatalog
            .Where(item => item.Id == assignment.CapabilityId)
            .Where(item => item.Kind == CapabilityKind.Tool)
            .Where(item => string.Equals(item.Key, capabilityKey, StringComparison.Ordinal))
            .ToArray();
        return catalogMatches.Length == 1;
    }
}

public sealed class WorkflowAgentRuntimeAuthorizationService(
    IAgentFrameworkWorkspaceService workspaceService)
{
    public async Task EnsureToolInvocationAuthorizedAsync(
        Guid actorAgentId,
        string toolName,
        CancellationToken cancellationToken)
    {
        if (actorAgentId == Guid.Empty)
        {
            throw CreateDeniedException(toolName);
        }

        var agents = await workspaceService.ListAgentsAsync(
            includeTemplates: true,
            cancellationToken);
        var actors = agents
            .Where(agent => agent.Id == actorAgentId)
            .ToArray();
        if (actors.Length != 1 ||
            !WorkflowAgentRuntimeAuthorizationPolicy.IsEligibleActor(actors[0]))
        {
            throw CreateDeniedException(toolName);
        }

        var capabilityCatalog = await workspaceService.ListCapabilitiesAsync(cancellationToken);
        if (!WorkflowAgentRuntimeAuthorizationPolicy.IsToolAuthorized(
                actors[0],
                capabilityCatalog,
                toolName))
        {
            throw CreateDeniedException(toolName);
        }
    }

    private static UnauthorizedAccessException CreateDeniedException(string toolName)
    {
        return new UnauthorizedAccessException(
            $"The current agent is not authorized to invoke workflow runtime tool '{toolName}'.");
    }
}
