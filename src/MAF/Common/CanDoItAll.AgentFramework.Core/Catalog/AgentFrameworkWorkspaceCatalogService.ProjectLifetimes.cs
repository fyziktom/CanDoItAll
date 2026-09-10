using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

internal sealed partial class AgentFrameworkWorkspaceCatalogService {
    public Task GrantAgentProjectStructureLifetimeAsync(Guid agentId, AgentProjectStructureLifetime lifetime,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(lifetime);
        return UpdateCatalogAsync(catalog => {
            var agent = catalog.Agents.FirstOrDefault(item => item.Id == agentId)
                ?? throw new InvalidOperationException("Agent was not found.");
            var configuration = AgentProjectStructureAccessMetadata.GrantProjectLifetime(agent.ConfigurationJson, lifetime);
            if (string.Equals(configuration, agent.ConfigurationJson, StringComparison.Ordinal)) {
                return catalog;
            }
            return catalog with {
                Agents = catalog.Agents.Where(item => item.Id != agentId).Append(agent with {
                    ConfigurationJson = configuration,
                    UpdatedAtUtc = AgentConfigurationVersion.NextRevision(agent.UpdatedAtUtc, DateTimeOffset.UtcNow)
                }).OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToList()
            };
        }, cancellationToken);
    }

    public Task<int> RevokeProjectStructureLifetimeAccessFromAllAgentsAsync(AgentProjectStructureRevocationTarget target,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(target);
        return RevokeProjectStructureAccessCoreAsync(
            configuration => AgentProjectStructureAccessMetadata.RevokeProjectLifetime(configuration, target), cancellationToken);
    }

    private async Task<int> RevokeProjectStructureAccessCoreAsync(
        Func<string, AgentProjectStructureAccessRevocationResult> revoke, CancellationToken cancellationToken) {
        var changedAgentCount = 0;
        var now = DateTimeOffset.UtcNow;
        await UpdateCatalogAsync(catalog => {
            changedAgentCount = 0;
            var updatedAgents = new List<AgentDefinition>(catalog.Agents.Count);
            foreach (var agent in catalog.Agents) {
                var revocation = revoke(agent.ConfigurationJson);
                if (!revocation.Changed) {
                    updatedAgents.Add(agent);
                    continue;
                }
                changedAgentCount++;
                updatedAgents.Add(agent with {
                    ConfigurationJson = revocation.ConfigurationJson,
                    UpdatedAtUtc = AgentConfigurationVersion.NextRevision(agent.UpdatedAtUtc, now)
                });
            }
            return changedAgentCount == 0 ? catalog : catalog with {
                Agents = updatedAgents.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToList()
            };
        }, cancellationToken);
        return changedAgentCount;
    }
}
