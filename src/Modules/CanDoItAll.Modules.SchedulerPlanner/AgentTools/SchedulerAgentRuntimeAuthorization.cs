using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;

namespace CanDoItAll.Modules.SchedulerPlanner;

public static class SchedulerAgentRuntimeAuthorizationPolicy
{
    public static bool CanAttach(AgentRuntimeToolProviderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Purpose == AgentRuntimeToolProviderPurpose.InteractiveChat &&
               IsManagedSchedulerActor(context.Agent);
    }

    public static bool IsManagedSchedulerActor(AgentDefinition? agent)
    {
        return SchedulerAgentIdentity.Matches(agent) &&
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

        if (!SchedulerAgentCapabilityKeys.ToolNameToCapabilityKey.TryGetValue(toolName, out var capabilityKey))
        {
            throw new InvalidOperationException(
                $"Scheduler Agent runtime tool '{toolName}' does not have a capability key mapping.");
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

public sealed class SchedulerAgentRuntimeAuthorizationService(
    IAgentFrameworkWorkspaceService workspaceService)
{
    public async Task EnsureToolInvocationAuthorizedAsync(
        Guid actorAgentId,
        string toolName,
        CancellationToken cancellationToken)
    {
        if (actorAgentId != SchedulerAgentIdentity.AgentId)
        {
            throw CreateDeniedException(toolName);
        }

        var agents = await workspaceService.ListAgentsAsync(
            includeTemplates: true,
            cancellationToken);
        var actor = agents.FirstOrDefault(agent => agent.Id == actorAgentId);
        if (!SchedulerAgentRuntimeAuthorizationPolicy.IsManagedSchedulerActor(actor))
        {
            throw CreateDeniedException(toolName);
        }

        var capabilityCatalog = await workspaceService.ListCapabilitiesAsync(cancellationToken);
        if (!SchedulerAgentRuntimeAuthorizationPolicy.IsToolAuthorized(
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
            $"The current managed Scheduler Agent is not authorized to invoke '{toolName}'.");
    }
}
