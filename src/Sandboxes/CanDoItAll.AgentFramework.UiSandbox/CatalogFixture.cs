using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Text.Json;
using CanDoItAll.AgentFramework.UI.Catalog;

namespace CanDoItAll.AgentFramework.UiSandbox;

internal static class CatalogFixture {
    private const string ResourceName = "CatalogFixture.json";

    public static AgentCatalogSnapshot Load() {
        using var stream = typeof(CatalogFixture).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException("The catalog rendering fixture is missing.");
        var snapshot = JsonSerializer.Deserialize<AgentCatalogSnapshot>(
            stream, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException("The catalog rendering fixture is empty.");
        return new(
            snapshot.Agents.Select(agent => agent with {
                Tags = agent.Tags.ToImmutableArray(),
                Capabilities = agent.Capabilities.ToImmutableArray()
            }).ToImmutableArray(),
            snapshot.Teams.Select(team => team with {
                AgentIds = team.AgentIds.ToImmutableArray()
            }).ToImmutableArray(),
            snapshot.PrivateProviderById.ToFrozenDictionary());
    }
}
