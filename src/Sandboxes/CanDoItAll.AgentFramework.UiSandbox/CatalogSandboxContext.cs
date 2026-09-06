using CanDoItAll.AgentFramework.UI.Catalog;

namespace CanDoItAll.AgentFramework.UiSandbox;

public enum CatalogSandboxScenario {
    Normal,
    Loading,
    Empty,
    CardStates,
    AvatarFallback
}

public enum CatalogSandboxLayout {
    Matched,
    Flexible
}

public sealed record CatalogSandboxContext(
    CatalogSandboxScenario Scenario = CatalogSandboxScenario.Normal,
    CatalogSandboxLayout Layout = CatalogSandboxLayout.Matched,
    Guid? AgentId = null,
    Guid? TeamId = null) {
    public AgentCatalogSelection Selection => new(AgentId, TeamId);

    public static CatalogSandboxContext Parse(
        string? scenario, string? layout, string? agentId, string? teamId, AgentCatalogSnapshot fixture) {
        var selectedScenario = scenario?.Trim().ToLowerInvariant() switch {
            "loading" => CatalogSandboxScenario.Loading,
            "empty" => CatalogSandboxScenario.Empty,
            "card-states" => CatalogSandboxScenario.CardStates,
            "avatar-fallback" => CatalogSandboxScenario.AvatarFallback,
            _ => CatalogSandboxScenario.Normal
        };
        var selectedLayout = string.Equals(layout?.Trim(), "flexible", StringComparison.OrdinalIgnoreCase)
            ? CatalogSandboxLayout.Flexible : CatalogSandboxLayout.Matched;
        return new CatalogSandboxContext(selectedScenario, selectedLayout,
            Guid.TryParse(agentId, out var agent) ? agent : null,
            Guid.TryParse(teamId, out var team) ? team : null).Normalize(fixture);
    }

    public CatalogSandboxContext Normalize(AgentCatalogSnapshot fixture) => this with {
        AgentId = fixture.Agents.Any(agent => agent.Id == AgentId) ? AgentId : null,
        TeamId = fixture.Teams.Any(team => team.Id == TeamId) ? TeamId : null
    };

    public IReadOnlyDictionary<string, object?> ToQuery() => new Dictionary<string, object?> {
        ["scenario"] = Scenario switch {
            CatalogSandboxScenario.Normal => "normal",
            CatalogSandboxScenario.Loading => "loading",
            CatalogSandboxScenario.Empty => "empty",
            CatalogSandboxScenario.CardStates => "card-states",
            CatalogSandboxScenario.AvatarFallback => "avatar-fallback",
            _ => throw new ArgumentOutOfRangeException(nameof(Scenario))
        },
        ["layout"] = Layout switch {
            CatalogSandboxLayout.Matched => "matched",
            CatalogSandboxLayout.Flexible => "flexible",
            _ => throw new ArgumentOutOfRangeException(nameof(Layout))
        },
        ["agentId"] = AgentId,
        ["teamId"] = TeamId
    };
}
