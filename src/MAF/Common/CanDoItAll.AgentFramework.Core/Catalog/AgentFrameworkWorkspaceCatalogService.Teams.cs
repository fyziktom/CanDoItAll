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
        var expected = model.ExpectedUpdatedAtUtc;
        var description = NormalizeTeamDescription(model.Description);
        var icon = AgentTeamIconCatalog.Normalize(model.Icon);
        var members = model.AgentIds.ToArray();
        if (id == Guid.Empty) {
            throw new ArgumentException("A team identity cannot be empty.", nameof(model));
        }
        var name = NormalizeTeamName(model.Name);
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("Agent team name is required.");
        }

        var accepted = await UpdateCatalogAsync(catalog =>
        {
            EnsureUniqueTeamName(catalog.AgentTeams, id, name);
            var existingTeam = catalog.AgentTeams.FirstOrDefault(item => item.Id == id);
            if (existingTeam is not null && expected is null || expected is not null && existingTeam?.UpdatedAtUtc != expected) {
                throw new AgentTeamConcurrencyException(id);
            }
            var team = new AgentTeamDefinition(
                Id: id,
                Name: name,
                Description: description,
                AgentIds: NormalizeRequestedTeamAgentIds(members, catalog.Agents),
                CreatedAtUtc: existingTeam?.CreatedAtUtc ?? now,
                UpdatedAtUtc: existingTeam is null ? now : AgentConfigurationVersion.NextRevision(existingTeam.UpdatedAtUtc, now),
                Icon: icon);

            return catalog with
            {
                AgentTeams = catalog.AgentTeams
                    .Where(item => item.Id != id)
                    .Append(team)
                    .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList()
            };
        }, catalog => catalog.AgentTeams.Single(team => team.Id == id), cancellationToken);

        return accepted.Id;
    }

    public async Task<AgentTeamEditorModel> SaveAgentTeamMetadataAsync(
        AgentTeamEditorModel model, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(model);
        var requestedId = model.Id;
        var id = requestedId ?? Guid.NewGuid();
        var name = NormalizeTeamName(model.Name);
        var description = NormalizeTeamDescription(model.Description);
        var icon = AgentTeamIconCatalog.Normalize(model.Icon);
        var expected = model.ExpectedMetadata;
        if (id == Guid.Empty || string.IsNullOrWhiteSpace(name)) {
            throw new AgentTeamMetadataRejectedException("A team identity and nonblank name are required.");
        }
        var now = DateTimeOffset.UtcNow;
        return await UpdateCatalogAsync(catalog => {
            var existing = catalog.AgentTeams.FirstOrDefault(team => team.Id == id);
            if (requestedId.HasValue && existing is null) {
                throw new AgentTeamMetadataRejectedException("The edited team no longer exists.");
            }
            if (existing is not null && (expected is null || expected.CreatedAtUtc != existing.CreatedAtUtc ||
                expected.Name != existing.Name || expected.Description != existing.Description ||
                expected.Icon != AgentTeamIconCatalog.Normalize(existing.Icon))) {
                throw new AgentTeamConcurrencyException(id);
            }
            if (catalog.AgentTeams.Any(team => team.Id != id && string.Equals(team.Name, name, StringComparison.OrdinalIgnoreCase))) {
                throw new AgentTeamMetadataRejectedException("The team name is already in use.");
            }
            var version = existing is null ? now : AgentConfigurationVersion.NextRevision(existing.UpdatedAtUtc, now);
            var team = new AgentTeamDefinition(id, name, description, existing?.AgentIds ?? [], existing?.CreatedAtUtc ?? now, version, icon);
            return catalog with {
                AgentTeams = catalog.AgentTeams.Where(item => item.Id != id).Append(team)
                    .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToList()
            };
        }, catalog => AgentTeamEditorModel.FromDefinition(catalog.AgentTeams.Single(team => team.Id == id)), cancellationToken);
    }

    public Task<AgentTeamDefinition> UpdateAgentTeamMembersAsync(Guid teamId, IReadOnlyList<Guid> agentIds, CancellationToken cancellationToken = default)
        => UpdateAgentTeamMembersCoreAsync(teamId, agentIds, null, cancellationToken);

    public Task<AgentTeamDefinition> UpdateAgentTeamMembersAsync(Guid teamId, IReadOnlyList<Guid> agentIds,
        DateTimeOffset expectedUpdatedAtUtc, CancellationToken cancellationToken = default)
        => UpdateAgentTeamMembersCoreAsync(teamId, agentIds, expectedUpdatedAtUtc, cancellationToken);

    private async Task<AgentTeamDefinition> UpdateAgentTeamMembersCoreAsync(
        Guid teamId,
        IReadOnlyList<Guid> agentIds,
        DateTimeOffset? expectedUpdatedAtUtc,
        CancellationToken cancellationToken)
    {
        var requestedAgentIds = agentIds?.ToArray() ?? [];
        var now = DateTimeOffset.UtcNow;
        return await UpdateCatalogAsync(catalog =>
        {
            var currentTeam = catalog.AgentTeams.FirstOrDefault(item => item.Id == teamId)
                ?? throw new InvalidOperationException("Agent team was not found.");
            if (expectedUpdatedAtUtc is null || currentTeam.UpdatedAtUtc != expectedUpdatedAtUtc) {
                throw new AgentTeamConcurrencyException(teamId);
            }
            var updatedTeam = currentTeam with
            {
                AgentIds = NormalizeRequestedTeamAgentIds(requestedAgentIds, catalog.Agents),
                UpdatedAtUtc = AgentConfigurationVersion.NextRevision(currentTeam.UpdatedAtUtc, now)
            };

            return catalog with
            {
                AgentTeams = catalog.AgentTeams
                    .Where(item => item.Id != teamId)
                    .Append(updatedTeam)
                    .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList()
            };
        }, catalog => catalog.AgentTeams.Single(team => team.Id == teamId), cancellationToken);
    }

    public async Task<AgentTeamDefinition> EnsureAgentTeamMemberAsync(Guid teamId, DateTimeOffset expectedCreatedAtUtc, Guid agentId,
        CancellationToken cancellationToken = default) {
        return await UpdateCatalogAsync(catalog => {
            var current = catalog.AgentTeams.FirstOrDefault(item => item.Id == teamId);
            if (current is null || current.CreatedAtUtc != expectedCreatedAtUtc) {
                throw new AgentTeamConcurrencyException(teamId);
            }
            if (agentId == Guid.Empty || !catalog.Agents.Any(agent => agent.Id == agentId)) {
                throw new ArgumentException("A current agent is required for team membership.", nameof(agentId));
            }
            if (current.AgentIds.Contains(agentId)) {
                return catalog;
            }
            var updated = current with {
                AgentIds = NormalizeRequestedTeamAgentIds(current.AgentIds.Append(agentId).ToArray(), catalog.Agents),
                UpdatedAtUtc = AgentConfigurationVersion.NextRevision(current.UpdatedAtUtc, DateTimeOffset.UtcNow)
            };
            return catalog with { AgentTeams = catalog.AgentTeams.Select(item => item.Id == teamId ? updated : item).ToArray() };
        }, catalog => catalog.AgentTeams.Single(team => team.Id == teamId), cancellationToken);
    }

    public Task DeleteAgentTeamAsync(Guid teamId, CancellationToken cancellationToken = default)
        => DeleteAgentTeamCoreAsync(teamId, null, cancellationToken);

    public Task DeleteAgentTeamAsync(Guid teamId, DateTimeOffset expectedUpdatedAtUtc, CancellationToken cancellationToken = default)
        => DeleteAgentTeamCoreAsync(teamId, expectedUpdatedAtUtc, cancellationToken);

    private async Task DeleteAgentTeamCoreAsync(Guid teamId, DateTimeOffset? expectedUpdatedAtUtc, CancellationToken cancellationToken) {
        await UpdateCatalogAsync(catalog =>
        {
            var current = catalog.AgentTeams.FirstOrDefault(item => item.Id == teamId);
            if (current is null) {
                return catalog;
            }
            if (expectedUpdatedAtUtc is null || current.UpdatedAtUtc != expectedUpdatedAtUtc) {
                throw new AgentTeamConcurrencyException(teamId);
            }

            return catalog with
            {
                AgentTeams = catalog.AgentTeams
                    .Where(item => item.Id != teamId)
                    .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList()
            };
        }, _ => teamId, cancellationToken);
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
