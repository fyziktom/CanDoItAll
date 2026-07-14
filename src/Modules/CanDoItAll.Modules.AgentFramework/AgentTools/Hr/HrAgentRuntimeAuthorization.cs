using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.Modules.AgentFramework;

public static class HrAgentRuntimeAuthorizationPolicy
{
    public static bool CanAttach(AgentRuntimeToolProviderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Purpose == AgentRuntimeToolProviderPurpose.InteractiveChat &&
               IsManagedHrActor(context.Agent);
    }

    public static bool IsManagedHrActor(AgentDefinition? agent)
    {
        return HrAgentIdentity.Matches(agent) &&
               agent!.Status == AgentLifecycleStatus.Active &&
               !agent.IsTemplate &&
               agent.Permissions.CanUseTools;
    }

    public static bool IsToolAuthorized(
        AgentDefinition agent,
        IReadOnlyList<CapabilityCatalogItem> capabilityCatalog,
        string toolName,
        bool requiresCrmScope)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(capabilityCatalog);

        if (!HrAgentCapabilityKeys.ToolNameToCapabilityKey.TryGetValue(toolName, out var capabilityKey))
        {
            throw new InvalidOperationException($"HR runtime tool '{toolName}' does not have a capability key mapping.");
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
        if (catalogMatches.Length != 1)
        {
            return false;
        }

        if (!requiresCrmScope)
        {
            return true;
        }

        var memoryAccess = AgentMemoryAccessMetadata.Read(agent.ConfigurationJson);
        return memoryAccess.AllowedSourceScopes.Contains(MemorySourceScope.Crm);
    }
}

public sealed class HrAgentRuntimeAuthorizationService(
    IAgentFrameworkWorkspaceService workspaceService)
{
    public async Task EnsureToolInvocationAuthorizedAsync(
        Guid actorAgentId,
        string toolName,
        bool requiresCrmScope,
        CancellationToken cancellationToken)
    {
        if (actorAgentId != HrAgentIdentity.AgentId)
        {
            throw CreateDeniedException(toolName);
        }

        var agents = await workspaceService.ListAgentsAsync(
            includeTemplates: true,
            cancellationToken);
        var actor = agents.FirstOrDefault(agent => agent.Id == actorAgentId);
        if (!HrAgentRuntimeAuthorizationPolicy.IsManagedHrActor(actor))
        {
            throw CreateDeniedException(toolName);
        }

        var capabilityCatalog = await workspaceService.ListCapabilitiesAsync(cancellationToken);
        if (!HrAgentRuntimeAuthorizationPolicy.IsToolAuthorized(
                actor!,
                capabilityCatalog,
                toolName,
                requiresCrmScope))
        {
            throw CreateDeniedException(toolName);
        }
    }

    private static UnauthorizedAccessException CreateDeniedException(string toolName)
    {
        return new UnauthorizedAccessException(
            $"The current managed HR agent is not authorized to invoke '{toolName}'.");
    }
}
