using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.AgentFramework;

public enum AgentFrameworkAgentsChatView
{
    Overview,
    Agents,
    Providers,
    Voice,
    FloatingChat,
    Chat,
    Capabilities,
    Governance,
    Scenarios,
    Diagnostics
}

public static class AgentFrameworkAgentsChatContextBuilder
{
    public const string SourceKind = "agents";
    public const string Route = "/agents";
    public const string Module = "agent-framework";
    public const string Surface = "agents";

    public static AgentFrameworkAgentsChatView ResolveView(string view)
        => view switch
        {
            "overview" => AgentFrameworkAgentsChatView.Overview,
            "agents" => AgentFrameworkAgentsChatView.Agents,
            "providers" => AgentFrameworkAgentsChatView.Providers,
            "voice" => AgentFrameworkAgentsChatView.Voice,
            "floating-chat" => AgentFrameworkAgentsChatView.FloatingChat,
            "chat" => AgentFrameworkAgentsChatView.Chat,
            "capabilities" => AgentFrameworkAgentsChatView.Capabilities,
            "governance" => AgentFrameworkAgentsChatView.Governance,
            "scenarios" => AgentFrameworkAgentsChatView.Scenarios,
            "diagnostics" => AgentFrameworkAgentsChatView.Diagnostics,
            _ => throw new ArgumentOutOfRangeException(nameof(view), view, "The Agents view is not supported.")
        };

    public static AgentChatContextSurface Build(
        AgentFrameworkAgentsChatView view,
        Guid? requestedAgentId,
        Guid? requestedTeamId,
        int technicalAgentCount,
        int providerCount,
        int boundResourceCount,
        int capabilityCount,
        int activeRunCount,
        int failedRunCount,
        AgentDefinition? selectedAgent = null,
        AgentTeamDefinition? selectedTeam = null)
    {
        ValidateOptionalId(requestedAgentId, nameof(requestedAgentId));
        ValidateOptionalId(requestedTeamId, nameof(requestedTeamId));
        ValidateCount(technicalAgentCount, nameof(technicalAgentCount));
        ValidateCount(providerCount, nameof(providerCount));
        ValidateCount(boundResourceCount, nameof(boundResourceCount));
        ValidateCount(capabilityCount, nameof(capabilityCount));
        ValidateCount(activeRunCount, nameof(activeRunCount));
        ValidateCount(failedRunCount, nameof(failedRunCount));
        ValidateSelection(requestedAgentId, selectedAgent?.Id, nameof(selectedAgent));
        ValidateSelection(requestedTeamId, selectedTeam?.Id, nameof(selectedTeam));
        if (!Enum.IsDefined(view))
        {
            throw new ArgumentOutOfRangeException(nameof(view), view, "The Agents view is not supported.");
        }

        var viewToken = ResolveViewToken(view);
        var viewLabel = ResolveViewLabel(view);
        var agentId = SupportsAgentSelection(view)
            ? selectedAgent?.Id ?? requestedAgentId
            : null;
        var teamId = view == AgentFrameworkAgentsChatView.Agents
            ? selectedTeam?.Id ?? requestedTeamId
            : null;
        var primarySelection = BuildPrimarySelection(agentId, teamId, selectedAgent, selectedTeam);
        IReadOnlyList<AgentChatContextEntityReference> selectedEntities = agentId.HasValue && teamId.HasValue
            ? new[] { BuildTeamReference(teamId.Value, selectedTeam?.Name) }
            : [];

        return new AgentChatContextSurface(
            new AgentChatContextSource(
                new AgentChatContextSourceKind(SourceKind),
                new AgentChatContextSourceId(BuildSourceId(viewToken, agentId, teamId))),
            $"Agents · {viewLabel}",
            new AgentChatSurfacePosition(
                Module,
                Surface,
                viewToken,
                Route,
                primarySelection,
                selectedEntities,
                BuildFacts(
                    technicalAgentCount,
                    providerCount,
                    boundResourceCount,
                    capabilityCount,
                    activeRunCount,
                    failedRunCount)),
            accessMode: AgentChatContextScopeAccessMode.Unrestricted);
    }

    private static IReadOnlyList<AgentChatContextPositionFact> BuildFacts(
        int technicalAgentCount,
        int providerCount,
        int boundResourceCount,
        int capabilityCount,
        int activeRunCount,
        int failedRunCount)
        =>
        [
            new("technical-agent-count", technicalAgentCount.ToString()),
            new("provider-count", providerCount.ToString()),
            new("bound-resource-count", boundResourceCount.ToString()),
            new("capability-count", capabilityCount.ToString()),
            new("active-run-count", activeRunCount.ToString()),
            new("failed-run-count", failedRunCount.ToString())
        ];

    private static AgentChatContextEntityReference? BuildPrimarySelection(
        Guid? agentId,
        Guid? teamId,
        AgentDefinition? selectedAgent,
        AgentTeamDefinition? selectedTeam)
    {
        if (agentId.HasValue)
        {
            return new AgentChatContextEntityReference(
                "technical-agent",
                agentId.Value.ToString("D"),
                selectedAgent?.Name ?? $"Agent {agentId.Value:D}");
        }

        return teamId.HasValue ? BuildTeamReference(teamId.Value, selectedTeam?.Name) : null;
    }

    private static AgentChatContextEntityReference BuildTeamReference(Guid teamId, string? teamName)
        => new(
            "agent-team",
            teamId.ToString("D"),
            string.IsNullOrWhiteSpace(teamName) ? $"Team {teamId:D}" : teamName.Trim());

    private static string BuildSourceId(
        string view,
        Guid? agentId,
        Guid? teamId)
    {
        if (agentId.HasValue)
        {
            return $"agent:{agentId.Value:D}";
        }

        return teamId.HasValue
            ? $"team:{teamId.Value:D}"
            : view;
    }

    private static bool SupportsAgentSelection(AgentFrameworkAgentsChatView view)
        => view is AgentFrameworkAgentsChatView.Agents or
            AgentFrameworkAgentsChatView.Chat or
            AgentFrameworkAgentsChatView.Capabilities or
            AgentFrameworkAgentsChatView.Governance;

    private static string ResolveViewToken(AgentFrameworkAgentsChatView view)
        => view switch
        {
            AgentFrameworkAgentsChatView.Overview => "overview",
            AgentFrameworkAgentsChatView.Agents => "agents",
            AgentFrameworkAgentsChatView.Providers => "providers",
            AgentFrameworkAgentsChatView.Voice => "voice",
            AgentFrameworkAgentsChatView.FloatingChat => "floating-chat",
            AgentFrameworkAgentsChatView.Chat => "chat",
            AgentFrameworkAgentsChatView.Capabilities => "capabilities",
            AgentFrameworkAgentsChatView.Governance => "governance",
            AgentFrameworkAgentsChatView.Scenarios => "scenarios",
            AgentFrameworkAgentsChatView.Diagnostics => "diagnostics",
            _ => throw new ArgumentOutOfRangeException(nameof(view), view, "The Agents view is not supported.")
        };

    private static string ResolveViewLabel(AgentFrameworkAgentsChatView view)
        => view switch
        {
            AgentFrameworkAgentsChatView.Overview => "Overview",
            AgentFrameworkAgentsChatView.Agents => "Agents",
            AgentFrameworkAgentsChatView.Providers => "Providers",
            AgentFrameworkAgentsChatView.Voice => "Voice",
            AgentFrameworkAgentsChatView.FloatingChat => "Floating chat",
            AgentFrameworkAgentsChatView.Chat => "Chat",
            AgentFrameworkAgentsChatView.Capabilities => "Capabilities",
            AgentFrameworkAgentsChatView.Governance => "Governance",
            AgentFrameworkAgentsChatView.Scenarios => "Scenarios",
            AgentFrameworkAgentsChatView.Diagnostics => "Diagnostics",
            _ => throw new ArgumentOutOfRangeException(nameof(view), view, "The Agents view is not supported.")
        };

    private static void ValidateOptionalId(Guid? value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("An optional Agents selection id cannot be empty.", parameterName);
        }
    }

    private static void ValidateSelection(
        Guid? requestedId,
        Guid? selectedId,
        string parameterName)
    {
        if (requestedId.HasValue &&
            selectedId.HasValue &&
            requestedId.Value != selectedId.Value)
        {
            throw new ArgumentException(
                "The selected entity must match the effective requested id.",
                parameterName);
        }
    }

    private static void ValidateCount(int value, string parameterName)
        => ArgumentOutOfRangeException.ThrowIfNegative(value, parameterName);
}
