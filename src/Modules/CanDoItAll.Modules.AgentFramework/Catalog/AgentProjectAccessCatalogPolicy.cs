using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class AgentProjectAccessCatalogPolicy(ProjectWriteAdmissionService admissions, Guid databaseProfileId)
    : IAgentCatalogMutationPolicy {
    public async Task<SandboxWorkspaceCatalog> ApplyAsync(SandboxWorkspaceCatalog before, SandboxWorkspaceCatalog after,
        CancellationToken cancellationToken = default) {
        if (databaseProfileId == Guid.Empty || admissions.DatabaseProfileId != databaseProfileId) {
            throw new InvalidOperationException("The agent catalog and project binding policy belong to different database profiles.");
        }
        var previousById = before.Agents.ToDictionary(agent => agent.Id);
        var changes = after.Agents.Select(agent => new CatalogAgentScope(agent,
            previousById.TryGetValue(agent.Id, out var previous)
                ? AgentProjectStructureAccessMetadata.ReadBindingScopeForMutation(previous.ConfigurationJson)
                : new(false, [], []),
            AgentProjectStructureAccessMetadata.ReadBindingScopeForMutation(agent.ConfigurationJson))).ToArray();
        var projectIds = changes.Where(change => !change.After.AllowAllProjects)
            .SelectMany(change => change.After.ProjectIds).Distinct().ToArray();
        var facts = await admissions.ListAccessBindingFactsAsync(projectIds, cancellationToken);
        if (facts.DatabaseProfileId != databaseProfileId) {
            throw new InvalidOperationException("The project owner returned binding facts for another database profile.");
        }
        var projects = facts.Projects.ToDictionary(project => project.ProjectId);
        var reservationGrants = facts.Reservations.ToHashSet();
        var changed = false;
        var agents = new List<AgentDefinition>(changes.Length);
        foreach (var change in changes) {
            if (change.After.AllowAllProjects) {
                agents.Add(change.Agent);
                continue;
            }
            var bindings = change.After.Lifetimes.ToHashSet();
            foreach (var projectId in change.After.ProjectIds) {
                var previousBindings = change.Before.Lifetimes.Where(lifetime => lifetime.ProjectId == projectId).ToArray();
                var currentBindings = bindings.Where(lifetime => lifetime.ProjectId == projectId).ToArray();
                if (currentBindings.Length == 0 && previousBindings.Length > 0) {
                    bindings.UnionWith(previousBindings);
                    continue;
                }
                foreach (var lifetime in currentBindings.Except(previousBindings)) {
                    var live = lifetime.DatabaseProfileId == databaseProfileId &&
                        projects.TryGetValue(projectId, out var current) && current.LifetimeId == lifetime.LifetimeId;
                    var reserved = lifetime.DatabaseProfileId == databaseProfileId &&
                        reservationGrants.Contains(new(projectId, lifetime.LifetimeId, change.Agent.Id));
                    if (!live && !reserved) {
                        throw new InvalidOperationException($"Agent '{change.Agent.Id:D}' cannot bind inactive project lifetime '{lifetime.LifetimeId:D}' for project '{projectId:D}'.");
                    }
                }
                if (currentBindings.Length > 0) {
                    continue;
                }
                var existingUnbound = change.Before.ProjectIds.Contains(projectId);
                if (projects.TryGetValue(projectId, out var project) && (!existingUnbound || project.LegacyAgentAccessBindingEligible)) {
                    bindings.Add(new(databaseProfileId, projectId, project.LifetimeId));
                } else if (!existingUnbound) {
                    throw new InvalidOperationException($"Agent '{change.Agent.Id:D}' cannot grant access to missing project '{projectId:D}' without an exact creation reservation.");
                }
            }
            var configurationJson = AgentProjectStructureAccessMetadata.BindProjectLifetimes(change.Agent.ConfigurationJson, bindings);
            if (!string.Equals(configurationJson, change.Agent.ConfigurationJson, StringComparison.Ordinal)) {
                changed = true;
                agents.Add(change.Agent with { ConfigurationJson = configurationJson, UpdatedAtUtc = DateTimeOffset.UtcNow });
            } else {
                agents.Add(change.Agent);
            }
        }
        return changed ? after with { Agents = agents } : after;
    }

    private sealed record CatalogAgentScope(AgentDefinition Agent, AgentProjectStructureBindingScope Before,
        AgentProjectStructureBindingScope After);
}
