using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessManagerAgentResolver
{
    public static Guid? ResolveConfiguredTechnicalAgentId(
        Guid? managerAgentId,
        string? managerAgentName,
        IReadOnlyList<ProcessManagerAgentOption> managerOptions,
        IReadOnlyList<AgentDefinition> agents)
    {
        var runManagerOption = ResolveManagerOptionByIdentifier(managerAgentId, managerOptions);
        if (runManagerOption?.TechnicalAgentId is Guid runManagerTechnicalAgentId)
        {
            return runManagerTechnicalAgentId;
        }

        if (managerAgentId.HasValue && agents.Any(item => item.Id == managerAgentId.Value))
        {
            return managerAgentId.Value;
        }

        var namedRunManagerOption = ResolveManagerOptionByName(managerAgentName, managerOptions);
        if (namedRunManagerOption?.TechnicalAgentId is Guid namedRunManagerTechnicalAgentId)
        {
            return namedRunManagerTechnicalAgentId;
        }

        var namedAgent = ResolveAgentByName(managerAgentName, agents);
        return namedAgent?.Id;
    }

    public static Guid? ResolveAssignedTechnicalAgentId(
        IReadOnlyList<ProcessRunAssignmentViewModel> assignments,
        IReadOnlyList<ProcessManagerAgentOption> managerOptions,
        IReadOnlyList<AgentDefinition> agents)
    {
        var candidates = assignments
            .Where(item => !item.IsCapabilityGap)
            .Select(item => new
            {
                Assignment = item,
                TechnicalAgentId = ResolveManagerOptionByIdentifier(item.PartyId, managerOptions)?.TechnicalAgentId ??
                                   ResolveAgentByIdentifier(item.PartyId, agents)?.Id,
                Score = ResolveManagerAssignmentScore(item)
            })
            .Where(item => item.TechnicalAgentId.HasValue && item.Score > 0)
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Assignment.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (candidates.Count == 0)
        {
            return null;
        }

        var topScore = candidates[0].Score;
        return candidates.Count(item => item.Score == topScore) == 1
            ? candidates[0].TechnicalAgentId
            : null;
    }

    public static Guid? ResolveFallbackTechnicalAgentId(
        IReadOnlyList<ProcessManagerAgentOption> managerOptions,
        IReadOnlyList<AgentDefinition> agents)
    {
        var fallbackManagerOptions = managerOptions
            .Where(option => option.TechnicalAgentId.HasValue)
            .Where(IsManagerLikeOption)
            .ToList();
        if (fallbackManagerOptions.Count == 1)
        {
            return fallbackManagerOptions[0].TechnicalAgentId;
        }

        var fallbackAgents = agents
            .Select(agent => new
            {
                Agent = agent,
                Score = ResolveAgentManagerScore(agent)
            })
            .Where(item => item.Score > 0)
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.Agent.UpdatedAtUtc)
            .ToList();
        if (fallbackAgents.Count == 0)
        {
            return null;
        }

        var topScore = fallbackAgents[0].Score;
        return fallbackAgents.Count(item => item.Score == topScore) == 1
            ? fallbackAgents[0].Agent.Id
            : null;
    }

    private static ProcessManagerAgentOption? ResolveManagerOptionByIdentifier(
        Guid? managerId,
        IReadOnlyList<ProcessManagerAgentOption> managerOptions)
    {
        if (!managerId.HasValue)
        {
            return null;
        }

        return managerOptions.FirstOrDefault(option =>
            option.PartyId == managerId.Value ||
            option.TechnicalAgentId == managerId.Value);
    }

    private static AgentDefinition? ResolveAgentByIdentifier(
        Guid? managerId,
        IReadOnlyList<AgentDefinition> agents)
    {
        return managerId.HasValue
            ? agents.FirstOrDefault(agent => agent.Id == managerId.Value)
            : null;
    }

    private static ProcessManagerAgentOption? ResolveManagerOptionByName(
        string? managerName,
        IReadOnlyList<ProcessManagerAgentOption> managerOptions)
    {
        if (string.IsNullOrWhiteSpace(managerName))
        {
            return null;
        }

        var normalizedName = managerName.Trim();
        return managerOptions.FirstOrDefault(option =>
            string.Equals(option.DisplayName, normalizedName, StringComparison.OrdinalIgnoreCase));
    }

    private static AgentDefinition? ResolveAgentByName(
        string? managerName,
        IReadOnlyList<AgentDefinition> agents)
    {
        if (string.IsNullOrWhiteSpace(managerName))
        {
            return null;
        }

        var normalizedName = managerName.Trim();
        return agents.FirstOrDefault(agent =>
            string.Equals(agent.Name, normalizedName, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsManagerLikeOption(ProcessManagerAgentOption option)
    {
        return ContainsManagerToken(option.DisplayName) ||
               ContainsManagerToken(option.BindingSummary);
    }

    private static int ResolveManagerAssignmentScore(ProcessRunAssignmentViewModel assignment)
    {
        var score = 0;
        score = Math.Max(score, ResolveManagerTextScore(assignment.RoleDisplayName));
        score = Math.Max(score, ResolveManagerTextScore(assignment.DisplayName));
        score = Math.Max(score, ResolveManagerTextScore(assignment.BindingReason));
        return assignment.AllowsDirectMessaging
            ? score + 5
            : score;
    }

    private static int ResolveAgentManagerScore(AgentDefinition agent)
    {
        var score = 0;
        score = Math.Max(score, ResolveManagerTextScore(agent.Name));
        score = Math.Max(score, ResolveManagerTextScore(agent.RoleTitle));
        score = Math.Max(score, agent.Tags.Any(ContainsManagerToken) ? 20 : 0);
        return score;
    }

    private static int ResolveManagerTextScore(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0;
        }

        if (value.Contains("process manager", StringComparison.OrdinalIgnoreCase))
        {
            return 100;
        }

        if (value.Contains("delivery manager", StringComparison.OrdinalIgnoreCase))
        {
            return 90;
        }

        if (value.Contains("manager", StringComparison.OrdinalIgnoreCase))
        {
            return 70;
        }

        if (value.Contains("orchestrator", StringComparison.OrdinalIgnoreCase))
        {
            return 60;
        }

        return value.Contains("lead", StringComparison.OrdinalIgnoreCase)
            ? 50
            : 0;
    }

    private static bool ContainsManagerToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var tokens = value.Split(
            [' ', '-', '_', '/', '\\', '.', ':', ';', ',', '(', ')', '[', ']'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return tokens.Any(token =>
            string.Equals(token, "manager", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(token, "lead", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(token, "orchestrator", StringComparison.OrdinalIgnoreCase));
    }
}
