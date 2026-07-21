using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;

namespace CanDoItAll.Modules.AgentFramework;

public static class CapabilityCuratorAgentRuntimeAuthorizationPolicy
{
    public static bool CanAttach(AgentRuntimeToolProviderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Purpose == AgentRuntimeToolProviderPurpose.InteractiveChat &&
               IsManagedCuratorActor(context.Agent);
    }

    public static bool IsManagedCuratorActor(AgentDefinition? agent)
    {
        return CapabilityCuratorAgentIdentity.Matches(agent) &&
               agent!.Status == AgentLifecycleStatus.Active &&
               !agent.IsTemplate &&
               agent.Permissions.CanUseTools;
    }

    public static bool IsToolAuthorized(
        AgentDefinition agent,
        IReadOnlyList<CapabilityCatalogItem> capabilityCatalog,
        string toolName)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(capabilityCatalog);

        if (!CapabilityCuratorAgentCapabilityKeys.ToolNameToCapabilityKey.TryGetValue(toolName, out var capabilityKey))
        {
            throw new InvalidOperationException(
                $"Capability Curator runtime tool '{toolName}' does not have a capability key mapping.");
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

public sealed class CapabilityCuratorAgentRuntimeAuthorizationService(
    IAgentFrameworkWorkspaceService workspaceService)
{
    public async Task EnsureToolInvocationAuthorizedAsync(
        Guid actorAgentId,
        string toolName,
        CancellationToken cancellationToken)
    {
        if (actorAgentId != CapabilityCuratorAgentIdentity.AgentId)
        {
            throw CreateDeniedException(toolName);
        }

        var agents = await workspaceService.ListAgentsAsync(
            includeTemplates: true,
            cancellationToken);
        var actor = agents.FirstOrDefault(agent => agent.Id == actorAgentId);
        if (!CapabilityCuratorAgentRuntimeAuthorizationPolicy.IsManagedCuratorActor(actor))
        {
            throw CreateDeniedException(toolName);
        }

        var capabilityCatalog = await workspaceService.ListCapabilitiesAsync(cancellationToken);
        if (!CapabilityCuratorAgentRuntimeAuthorizationPolicy.IsToolAuthorized(
                actor!,
                capabilityCatalog,
                toolName))
        {
            throw CreateDeniedException(toolName);
        }
    }

    private static UnauthorizedAccessException CreateDeniedException(string toolName)
    {
        return new UnauthorizedAccessException(
            $"The current managed Capability Curator agent is not authorized to invoke '{toolName}'.");
    }
}
