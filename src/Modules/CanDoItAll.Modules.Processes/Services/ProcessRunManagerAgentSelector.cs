using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Processes.Projections;

namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessRunManagerAgentSelector
{
    internal const string ProcessManagerTag = "process-manager";

    public AgentDefinition Select(
        IReadOnlyList<AgentDefinition> agents,
        IReadOnlyList<ProcessRunParticipantId> participants)
    {
        ArgumentNullException.ThrowIfNull(agents);
        ArgumentNullException.ThrowIfNull(participants);

        var participantAgentIds = participants
            .Select(participant => Guid.TryParse(participant.Value, out var agentId) ? agentId : Guid.Empty)
            .Where(agentId => agentId != Guid.Empty)
            .ToHashSet();
        return agents
            .Where(IsEligible)
            .Where(HasProcessManagerIdentity)
            .OrderByDescending(agent => participantAgentIds.Contains(agent.Id))
            .ThenBy(agent => agent.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault()
            ?? throw new InvalidOperationException(
                $"A process run narrative requires an active non-template agent tagged '{ProcessManagerTag}' " +
                $"or identified as the canonical '{DeliveryManagerAgentIdentity.TemplateKey}' agent, " +
                "authorized to observe other agents, and configured with a provider.");
    }

    private static bool IsEligible(AgentDefinition agent)
    {
        return !agent.IsTemplate &&
               agent.Status == AgentLifecycleStatus.Active &&
               agent.ProviderProfileId.HasValue &&
               agent.Permissions.CanObserveOtherAgents;
    }

    private static bool HasProcessManagerIdentity(AgentDefinition agent)
    {
        return DeliveryManagerAgentIdentity.Matches(agent) ||
               agent.Tags.Any(tag =>
                   string.Equals(tag?.Trim(), ProcessManagerTag, StringComparison.OrdinalIgnoreCase));
    }
}
