using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

internal sealed partial class AgentFrameworkWorkspaceCatalogService
{
    public async Task<IReadOnlyList<AgentTeamDefinition>> ListAgentTeamsAsync(
        CancellationToken cancellationToken = default)
    {
        var catalog = await store.LoadCatalogAsync(cancellationToken);
        return catalog.AgentTeams
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<AgentTeamEditorModel> GetAgentTeamEditorAsync(
        Guid? teamId = null,
        CancellationToken cancellationToken = default)
    {
        if (!teamId.HasValue)
        {
            return new AgentTeamEditorModel();
        }

        var catalog = await store.LoadCatalogAsync(cancellationToken);
        var team = catalog.AgentTeams.FirstOrDefault(item => item.Id == teamId.Value)
            ?? throw new InvalidOperationException("Agent team was not found.");

        return AgentTeamEditorModel.FromDefinition(team);
    }

    public async Task<Guid> SaveAgentTeamAsync(
        AgentTeamEditorModel model,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        var now = DateTimeOffset.UtcNow;
        var id = model.Id ?? Guid.NewGuid();
        var name = NormalizeTeamName(model.Name);
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("Agent team name is required.");
        }

        await UpdateCatalogAsync(catalog =>
        {
            EnsureUniqueTeamName(catalog.AgentTeams, id, name);
            var existingTeam = catalog.AgentTeams.FirstOrDefault(item => item.Id == id);
            var team = new AgentTeamDefinition(
                Id: id,
                Name: name,
                Description: NormalizeTeamDescription(model.Description),
                AgentIds: NormalizeRequestedTeamAgentIds(model.AgentIds, catalog.Agents),
                CreatedAtUtc: existingTeam?.CreatedAtUtc ?? now,
                UpdatedAtUtc: now,
                Icon: AgentTeamIconCatalog.Normalize(model.Icon));

            return catalog with
            {
                AgentTeams = catalog.AgentTeams
                    .Where(item => item.Id != id)
                    .Append(team)
                    .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList()
            };
        }, cancellationToken);

        return id;
    }

    public async Task<AgentTeamDefinition> UpdateAgentTeamMembersAsync(
        Guid teamId,
        IReadOnlyList<Guid> agentIds,
        CancellationToken cancellationToken = default)
    {
        AgentTeamDefinition? updatedTeam = null;
        var now = DateTimeOffset.UtcNow;
        await UpdateCatalogAsync(catalog =>
        {
            var currentTeam = catalog.AgentTeams.FirstOrDefault(item => item.Id == teamId)
                ?? throw new InvalidOperationException("Agent team was not found.");
            updatedTeam = currentTeam with
            {
                AgentIds = NormalizeRequestedTeamAgentIds(agentIds, catalog.Agents),
                UpdatedAtUtc = now
            };

            return catalog with
            {
                AgentTeams = catalog.AgentTeams
                    .Where(item => item.Id != teamId)
                    .Append(updatedTeam)
                    .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList()
            };
        }, cancellationToken);

        return updatedTeam ?? throw new InvalidOperationException("Agent team could not be updated.");
    }

    public async Task DeleteAgentTeamAsync(Guid teamId, CancellationToken cancellationToken = default)
    {
        await UpdateCatalogAsync(catalog =>
        {
            if (!catalog.AgentTeams.Any(item => item.Id == teamId))
            {
                return catalog;
            }

            return catalog with
            {
                AgentTeams = catalog.AgentTeams
                    .Where(item => item.Id != teamId)
                    .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList()
            };
        }, cancellationToken);
    }

    private static IReadOnlyList<Guid> NormalizeRequestedTeamAgentIds(
        IReadOnlyList<Guid>? agentIds,
        IReadOnlyList<AgentDefinition> availableAgents)
    {
        var availableAgentIds = availableAgents
            .Select(item => item.Id)
            .ToHashSet();
        var requestedAgentIds = (agentIds ?? [])
            .Where(item => item != Guid.Empty)
            .Distinct()
            .ToList();
        var missingAgentId = requestedAgentIds.FirstOrDefault(item => !availableAgentIds.Contains(item));
        if (missingAgentId != Guid.Empty)
        {
            throw new InvalidOperationException($"Agent team references missing agent '{missingAgentId:N}'.");
        }

        var nameByAgentId = availableAgents
            .ToDictionary(item => item.Id, item => item.Name);

        return requestedAgentIds
            .OrderBy(item => nameByAgentId.TryGetValue(item, out var name) ? name : string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item)
            .ToList();
    }

    private static void EnsureUniqueTeamName(
        IReadOnlyList<AgentTeamDefinition> teams,
        Guid currentTeamId,
        string name)
    {
        var collision = teams
            .FirstOrDefault(item => item.Id != currentTeamId && string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
        if (collision is null)
        {
            return;
        }

        throw new InvalidOperationException($"Agent team name '{name}' is already in use.");
    }

    private static string NormalizeTeamName(string? value)
        => (value ?? string.Empty).Trim();

    private static string NormalizeTeamDescription(string? value)
        => (value ?? string.Empty).Trim();
}
