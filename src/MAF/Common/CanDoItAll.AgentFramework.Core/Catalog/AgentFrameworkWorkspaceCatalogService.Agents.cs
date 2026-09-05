using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

internal sealed partial class AgentFrameworkWorkspaceCatalogService
{
    public async Task<IReadOnlyList<AgentDefinition>> ListAgentsAsync(
        bool includeTemplates = true,
        CancellationToken cancellationToken = default)
    {
        var catalog = await store.LoadCatalogAsync(cancellationToken);
        return catalog.Agents
            .Where(item => includeTemplates || !item.IsTemplate)
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<AgentEditorModel> GetAgentEditorAsync(
        Guid? agentId = null,
        CancellationToken cancellationToken = default)
    {
        if (!agentId.HasValue)
        {
            return new AgentEditorModel();
        }

        var catalog = await store.LoadCatalogAsync(cancellationToken);
        var agent = catalog.Agents.FirstOrDefault(item => item.Id == agentId.Value)
            ?? throw new InvalidOperationException("Agent was not found.");

        return AgentEditorModel.FromDefinition(agent);
    }

    public async Task<Guid> SaveAgentAsync(AgentEditorModel model, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var id = model.Id ?? Guid.NewGuid();
        var selectedProvider = model.ProviderProfileId is { } providerProfileId
            ? await providerRegistry.GetProviderAsync(providerProfileId, cancellationToken)
            : null;
        await UpdateCatalogAsync(catalog =>
        {
            var existingAgent = catalog.Agents.FirstOrDefault(item => item.Id == id);
            if (model.ExpectedUpdatedAtUtc.HasValue && existingAgent is null)
            {
                throw new AgentCatalogConcurrencyException(
                    id,
                    model.ExpectedUpdatedAtUtc.Value,
                    actualUpdatedAtUtc: null);
            }

            if (model.ExpectedUpdatedAtUtc is DateTimeOffset expectedUpdatedAtUtc &&
                existingAgent?.UpdatedAtUtc != expectedUpdatedAtUtc)
            {
                throw new AgentCatalogConcurrencyException(
                    id,
                    expectedUpdatedAtUtc,
                    existingAgent?.UpdatedAtUtc);
            }

            var validationCatalog = selectedProvider is null
                ? catalog
                : catalog with
                {
                    Providers = catalog.Providers
                        .Where(item => item.Id != selectedProvider.Id)
                        .Append(selectedProvider)
                        .ToList()
                };
            AgentDefinition definition;
            try {
                definition = AgentDefinitionFactory.Create(
                    validationCatalog,
                    model,
                    id,
                    existingAgent,
                    now,
                    providerProfileService,
                    "Agent save");
            } catch (InvalidOperationException exception) {
                throw new AgentEditorValidationException(exception.Message, exception);
            }

            return validationCatalog with
            {
                Agents = catalog.Agents
                    .Where(item => item.Id != id)
                    .Append(definition)
                    .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList()
            };
        }, cancellationToken);
        return id;
    }

    public Task GrantAgentProjectStructureAccessAsync(
        Guid agentId,
        Guid projectId,
        CancellationToken cancellationToken = default)
        => UpdateAgentProjectStructureAccessAsync(
            agentId,
            projectId,
            static (access, id) =>
            {
                if (access.AllowAllProjects || access.AllowedProjectIds.Contains(id))
                {
                    return false;
                }

                access.AllowedProjectIds.Add(id);
                return true;
            },
            cancellationToken);

    public Task RevokeAgentProjectStructureAccessAsync(
        Guid agentId,
        Guid projectId,
        CancellationToken cancellationToken = default)
        => UpdateAgentProjectStructureAccessAsync(
            agentId,
            projectId,
            static (access, id) => access.AllowedProjectIds.Remove(id),
            cancellationToken);

    public async Task<int> RevokeProjectStructureAccessFromAllAgentsAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        if (projectId == Guid.Empty)
        {
            throw new ArgumentException("A project id is required.", nameof(projectId));
        }

        var changedAgentCount = 0;
        var now = DateTimeOffset.UtcNow;
        await UpdateCatalogAsync(catalog =>
        {
            changedAgentCount = 0;
            var updatedAgents = new List<AgentDefinition>(catalog.Agents.Count);
            foreach (var agent in catalog.Agents)
            {
                var revocation = AgentProjectStructureAccessMetadata.RevokeProject(
                    agent.ConfigurationJson,
                    projectId);
                if (!revocation.Changed)
                {
                    updatedAgents.Add(agent);
                    continue;
                }

                changedAgentCount++;
                updatedAgents.Add(agent with
                {
                    ConfigurationJson = revocation.ConfigurationJson,
                    UpdatedAtUtc = now
                });
            }

            return changedAgentCount == 0
                ? catalog
                : catalog with
                {
                    Agents = updatedAgents
                        .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                        .ToList()
                };
        }, cancellationToken);

        return changedAgentCount;
    }

    private async Task UpdateAgentProjectStructureAccessAsync(
        Guid agentId,
        Guid projectId,
        Func<AgentProjectStructureAccessSettings, Guid, bool> update,
        CancellationToken cancellationToken)
    {
        if (projectId == Guid.Empty)
        {
            throw new ArgumentException("A project id is required.", nameof(projectId));
        }

        var now = DateTimeOffset.UtcNow;
        await UpdateCatalogAsync(catalog =>
        {
            var agent = catalog.Agents.FirstOrDefault(item => item.Id == agentId)
                ?? throw new InvalidOperationException("Agent was not found.");
            var access = AgentProjectStructureAccessMetadata.Read(agent.ConfigurationJson);
            if (!update(access, projectId))
            {
                return catalog;
            }

            var updated = agent with
            {
                ConfigurationJson = AgentProjectStructureAccessMetadata.Write(agent.ConfigurationJson, access),
                UpdatedAtUtc = now
            };

            return catalog with
            {
                Agents = catalog.Agents
                    .Where(item => item.Id != agentId)
                    .Append(updated)
                    .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList()
            };
        }, cancellationToken);
    }

    public async Task DeleteAgentAsync(Guid agentId, CancellationToken cancellationToken = default)
    {
        if (agentId == Guid.Empty)
        {
            throw new ArgumentException("An agent identifier is required.", nameof(agentId));
        }

        var catalog = await store.LoadCatalogAsync(cancellationToken);
        var agent = catalog.Agents.FirstOrDefault(item => item.Id == agentId);
        if (agent is null)
        {
            return;
        }

        if (ManagedSeedProviderFallbacks.IsManagedSeedAgent(agent))
        {
            throw new AgentDeletionConflictException(
                agentId,
                AgentDeletionConflictKind.ManagedSeedAgent,
                $"Managed seed agent '{agent.Name}' cannot be deleted.");
        }

        await store.DeleteAgentWorkspaceDataAsync(agentId, cancellationToken);
    }

    public async Task<Guid> CloneAgentAsync(Guid agentId, string cloneName, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var cloneId = Guid.NewGuid();
        var cloneTemplateKey = WorkspaceCatalogIdentityNormalizer.NormalizeTemplateKey(cloneName, cloneName);
        await UpdateCatalogAsync(catalog =>
        {
            var source = catalog.Agents.FirstOrDefault(item => item.Id == agentId)
                ?? throw new InvalidOperationException("Source agent was not found.");
            AgentDefinitionFactory.EnsureUniqueTemplateKey(catalog.Agents, cloneId, cloneTemplateKey, "Agent clone");
            var clone = source with
            {
                Id = cloneId,
                Name = cloneName.Trim(),
                IsTemplate = false,
                TemplateKey = cloneTemplateKey,
                ConfigurationJson = AgentManagedSeedCustomizationMetadata.RemoveManagedSeedOwnership(
                    source.ConfigurationJson),
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            return catalog with
            {
                Agents = catalog.Agents
                    .Append(clone)
                    .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList()
            };
        }, cancellationToken);

        return cloneId;
    }

    public async Task<Guid> ConvertToTemplateAsync(
        Guid agentId,
        string templateKey,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var templateId = Guid.NewGuid();
        await UpdateCatalogAsync(catalog =>
        {
            var source = catalog.Agents.FirstOrDefault(item => item.Id == agentId)
                ?? throw new InvalidOperationException("Agent was not found.");
            var normalizedTemplateKey = WorkspaceCatalogIdentityNormalizer.NormalizeTemplateKey(templateKey, source.Name);
            AgentDefinitionFactory.EnsureUniqueTemplateKey(catalog.Agents, templateId, normalizedTemplateKey, "Template conversion");
            var template = source with
            {
                Id = templateId,
                IsTemplate = true,
                TemplateKey = normalizedTemplateKey,
                Name = $"{source.Name} template",
                ConfigurationJson = AgentManagedSeedCustomizationMetadata.RemoveManagedSeedOwnership(
                    source.ConfigurationJson),
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            return catalog with
            {
                Agents = catalog.Agents
                    .Append(template)
                    .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList()
            };
        }, cancellationToken);

        return templateId;
    }

    public async Task<AgentExportResult> ExportAgentAsync(Guid agentId, CancellationToken cancellationToken = default)
    {
        var document = await store.LoadAsync(cancellationToken);
        var agent = document.Agents.FirstOrDefault(item => item.Id == agentId)
            ?? throw new InvalidOperationException("Agent was not found.");

        return await packageService.ExportAsync(document, agent, cancellationToken);
    }

    public async Task<Guid> ImportAgentAsync(string packagePath, CancellationToken cancellationToken = default)
    {
        var imported = await packageService.ImportAsync(packagePath, cancellationToken);
        var normalizedImportedProviders = imported.Providers
            .Select(providerProfileService.NormalizeImportedProfile)
            .ToList();
        var normalizedImportedCapabilities = imported.Capabilities
            .Select(NormalizeCapability)
            .ToList();
        var normalizedImportedAgent = NormalizeImportedAgent(imported.Agent);
        await store.UpdateWorkspaceAsync(document =>
        {
            var providerIdMap = BuildProviderIdMap(document.Providers, normalizedImportedProviders);
            var capabilityIdMap = BuildCapabilityIdMap(document.Capabilities, normalizedImportedCapabilities);
            var importedAgent = RemapImportedAgent(normalizedImportedAgent, providerIdMap, capabilityIdMap, document.Capabilities, normalizedImportedCapabilities);
            var importedProviders = normalizedImportedProviders
                .Where(provider => providerIdMap[provider.Id] == provider.Id)
                .ToList();
            var importedCapabilities = normalizedImportedCapabilities
                .Where(capability => capabilityIdMap[capability.Id] == capability.Id)
                .ToList();
            var importedProviderIds = importedProviders
                .Select(item => item.Id)
                .ToHashSet();
            var importedCapabilityIds = importedCapabilities
                .Select(item => item.Id)
                .ToHashSet();
            var prunedDocument = PruneAgentWorkspace(document, importedAgent.Id, pruneTeamMemberships: false);
            AgentDefinitionFactory.EnsureUniqueTemplateKey(
                prunedDocument.Agents,
                importedAgent.Id,
                WorkspaceCatalogIdentityNormalizer.GetAgentTemplateIdentity(importedAgent),
                "Agent import");

            return prunedDocument with
            {
                Agents = prunedDocument.Agents
                    .Append(importedAgent)
                    .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                Providers = prunedDocument.Providers
                    .Where(existing => !importedProviderIds.Contains(existing.Id))
                    .Concat(importedProviders)
                    .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                Capabilities = prunedDocument.Capabilities
                    .Where(existing => !importedCapabilityIds.Contains(existing.Id))
                    .Concat(importedCapabilities)
                    .OrderBy(item => item.Kind)
                    .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                Memory = prunedDocument.Memory
                    .Concat(imported.Memory)
                    .OrderBy(item => item.CreatedAtUtc)
                    .ToList(),
                ChatSessions = prunedDocument.ChatSessions
                    .Concat(imported.Sessions)
                    .OrderByDescending(item => item.UpdatedAtUtc)
                    .ToList(),
                ExecutionRuns = prunedDocument.ExecutionRuns
                    .Concat(imported.Runs)
                    .OrderByDescending(item => item.UpdatedAtUtc)
                    .ToList(),
                ExecutionLog = prunedDocument.ExecutionLog
                    .Concat(imported.ExecutionLog)
                    .OrderByDescending(item => item.CreatedAtUtc)
                    .ToList(),
                Metrics = prunedDocument.Metrics
                    .Concat(imported.Metrics)
                    .OrderByDescending(item => item.CreatedAtUtc)
                    .ToList(),
                ProviderUsageObservations = prunedDocument.ProviderUsageObservations
                    .OrderByDescending(item => item.CreatedAtUtc)
                    .ToList(),
                ExecutionApprovals = prunedDocument.ExecutionApprovals
                    .Concat(imported.Approvals)
                    .OrderByDescending(item => item.DecidedAtUtc ?? item.RequestedAtUtc)
                    .ToList(),
                ExecutionArtifacts = prunedDocument.ExecutionArtifacts
                    .Concat(imported.Artifacts)
                    .OrderByDescending(item => item.CreatedAtUtc)
                    .ToList(),
                ExecutionWorkflowCheckpoints = prunedDocument.ExecutionWorkflowCheckpoints
                    .Concat(imported.Checkpoints)
                    .OrderByDescending(item => item.CapturedAtUtc)
                    .ToList(),
                ToolExecutionReceipts = prunedDocument.ToolExecutionReceipts
                    .Concat(imported.ToolReceipts)
                    .OrderByDescending(item => item.CompletedAtUtc)
                    .ToList()
            };
        }, cancellationToken);

        return imported.Agent.Id;
    }

    private static SandboxWorkspaceDocument PruneAgentWorkspace(
        SandboxWorkspaceDocument document,
        Guid agentId,
        bool pruneTeamMemberships = true)
    {
        var sessionIdsToDelete = document.ChatSessions
            .Where(item => item.AgentId == agentId)
            .Select(item => item.Id)
            .ToHashSet();
        var runIdsToDelete = document.ExecutionRuns
            .Where(item => item.AgentId == agentId
                || item.ChatSessionId.HasValue && sessionIdsToDelete.Contains(item.ChatSessionId.Value))
            .Select(item => item.Id)
            .ToHashSet();

        return document with
        {
            Agents = document.Agents.Where(item => item.Id != agentId).ToList(),
            AgentExternalBindings = document.AgentExternalBindings
                .Where(item => item.AgentId != agentId)
                .ToList(),
            Memory = document.Memory.Where(item => item.AgentId != agentId).ToList(),
            ChatSessions = document.ChatSessions.Where(item => item.AgentId != agentId).ToList(),
            ExecutionRuns = document.ExecutionRuns.Where(item => !runIdsToDelete.Contains(item.Id)).ToList(),
            ExecutionLog = document.ExecutionLog
                .Where(item =>
                    item.AgentId != agentId
                    && (!item.ChatSessionId.HasValue || !sessionIdsToDelete.Contains(item.ChatSessionId.Value))
                    && (item.ExecutionRunId == Guid.Empty || !runIdsToDelete.Contains(item.ExecutionRunId)))
                .ToList(),
            Metrics = document.Metrics
                .Where(item =>
                    item.AgentId != agentId
                    && (!item.ChatSessionId.HasValue || !sessionIdsToDelete.Contains(item.ChatSessionId.Value))
                    && (item.ExecutionRunId == Guid.Empty || !runIdsToDelete.Contains(item.ExecutionRunId)))
                .ToList(),
            ProviderUsageObservations = document.ProviderUsageObservations
                .Where(item =>
                    item.AgentId != agentId
                    && (!item.ChatSessionId.HasValue || !sessionIdsToDelete.Contains(item.ChatSessionId.Value))
                    && (!item.ExecutionRunId.HasValue || !runIdsToDelete.Contains(item.ExecutionRunId.Value)))
                .ToList(),
            ExecutionApprovals = document.ExecutionApprovals
                .Where(item => !runIdsToDelete.Contains(item.ExecutionRunId))
                .ToList(),
            ExecutionArtifacts = document.ExecutionArtifacts
                .Where(item => !runIdsToDelete.Contains(item.ExecutionRunId))
                .ToList(),
            ExecutionWorkflowCheckpoints = document.ExecutionWorkflowCheckpoints
                .Where(item => !runIdsToDelete.Contains(item.ExecutionRunId))
                .ToList(),
            ToolExecutionReceipts = document.ToolExecutionReceipts
                .Where(item => !runIdsToDelete.Contains(item.ExecutionRunId))
                .ToList(),
            AgentTeams = pruneTeamMemberships
                ? PruneAgentTeamMemberships(document.AgentTeams, agentId)
                : document.AgentTeams
        };
    }

    private static IReadOnlyList<AgentTeamDefinition> PruneAgentTeamMemberships(
        IReadOnlyList<AgentTeamDefinition> teams,
        Guid agentId)
    {
        var now = DateTimeOffset.UtcNow;
        return teams
            .Select(team =>
            {
                if (!team.AgentIds.Contains(agentId))
                {
                    return team;
                }

                return team with
                {
                    AgentIds = team.AgentIds
                        .Where(item => item != agentId)
                        .ToList(),
                    UpdatedAtUtc = now
                };
            })
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private IReadOnlyDictionary<Guid, Guid> BuildProviderIdMap(
        IReadOnlyList<ProviderProfile> existingProviders,
        IReadOnlyList<ProviderProfile> importedProviders)
    {
        EnsureUniqueCanonicalEntries(
            importedProviders,
            providerProfileService.GetIdentityKey,
            provider => provider.Name,
            "Imported package providers");

        var existingByIdentity = existingProviders
            .GroupBy(providerProfileService.GetIdentityKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);
        var map = new Dictionary<Guid, Guid>();
        foreach (var provider in importedProviders)
        {
            var existing = existingProviders.FirstOrDefault(item => item.Id == provider.Id)
                ?? ResolveCanonicalImportMatch(
                    providerProfileService.GetIdentityKey(provider),
                    provider.Name,
                    existingByIdentity,
                    "provider");

            map[provider.Id] = existing?.Id ?? provider.Id;
        }

        return map;
    }

    private static IReadOnlyDictionary<Guid, Guid> BuildCapabilityIdMap(
        IReadOnlyList<CapabilityCatalogItem> existingCapabilities,
        IReadOnlyList<CapabilityCatalogItem> importedCapabilities)
    {
        EnsureUniqueCanonicalEntries(
            importedCapabilities,
            WorkspaceCatalogIdentityNormalizer.GetCapabilityIdentityKey,
            capability => capability.Key,
            "Imported package capabilities");

        var existingByIdentity = existingCapabilities
            .GroupBy(WorkspaceCatalogIdentityNormalizer.GetCapabilityIdentityKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);
        var map = new Dictionary<Guid, Guid>();
        foreach (var capability in importedCapabilities)
        {
            var existing = existingCapabilities.FirstOrDefault(item => item.Id == capability.Id)
                ?? ResolveCanonicalImportMatch(
                    WorkspaceCatalogIdentityNormalizer.GetCapabilityIdentityKey(capability),
                    capability.Key,
                    existingByIdentity,
                    "capability");

            map[capability.Id] = existing?.Id ?? capability.Id;
        }

        return map;
    }

    private static AgentDefinition RemapImportedAgent(
        AgentDefinition agent,
        IReadOnlyDictionary<Guid, Guid> providerIdMap,
        IReadOnlyDictionary<Guid, Guid> capabilityIdMap,
        IReadOnlyList<CapabilityCatalogItem> existingCapabilities,
        IReadOnlyList<CapabilityCatalogItem> importedCapabilities)
    {
        var capabilityLookup = existingCapabilities
            .Concat(importedCapabilities)
            .ToDictionary(item => item.Id, item => item);

        return agent with
        {
            ProviderProfileId = agent.ProviderProfileId.HasValue && providerIdMap.TryGetValue(agent.ProviderProfileId.Value, out var providerId)
                ? providerId
                : agent.ProviderProfileId,
            Capabilities = agent.Capabilities
                .Select(capability =>
                {
                    if (!capabilityIdMap.TryGetValue(capability.CapabilityId, out var capabilityId))
                    {
                        return capability;
                    }

                    return capabilityLookup.TryGetValue(capabilityId, out var mappedCapability)
                        ? capability with
                        {
                            CapabilityId = capabilityId,
                            CapabilityKey = mappedCapability.Key,
                            Kind = mappedCapability.Kind
                        }
                        : capability with { CapabilityId = capabilityId };
                })
                .ToList()
        };
    }

    private static AgentDefinition NormalizeImportedAgent(AgentDefinition agent)
    {
        return agent with
        {
            Name = agent.Name.Trim(),
            RoleTitle = agent.RoleTitle.Trim(),
            Summary = agent.Summary.Trim(),
            Instructions = agent.Instructions.Trim(),
            Model = agent.Model.Trim(),
            ConfigurationJson = agent.ConfigurationJson.Trim(),
            TemplateKey = WorkspaceCatalogIdentityNormalizer.NormalizeTemplateKey(agent.TemplateKey, agent.Name),
            Tags = agent.Tags
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
        };
    }

    private static CapabilityCatalogItem NormalizeCapability(CapabilityCatalogItem capability)
    {
        return capability with
        {
            Key = WorkspaceCatalogIdentityNormalizer.NormalizeCapabilityKey(capability.Key),
            Name = capability.Name.Trim(),
            Description = capability.Description.Trim(),
            EndpointOrPath = capability.EndpointOrPath.Trim(),
            ConfigurationJson = capability.ConfigurationJson.Trim(),
            ProofNotes = capability.ProofNotes.Trim()
        };
    }

    private static void EnsureUniqueCanonicalEntries<TItem>(
        IEnumerable<TItem> items,
        Func<TItem, string> keySelector,
        Func<TItem, string> labelSelector,
        string collectionLabel)
    {
        var collisions = items
            .GroupBy(keySelector, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => new
            {
                Key = group.Key,
                Labels = group.Select(labelSelector).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
            })
            .ToList();

        if (collisions.Count == 0)
        {
            return;
        }

        var collision = collisions[0];
        throw new InvalidOperationException(
            $"{collectionLabel} contain ambiguous canonical identity '{collision.Key}' for: {string.Join(", ", collision.Labels)}.");
    }

    private static TItem? ResolveCanonicalImportMatch<TItem>(
        string canonicalIdentity,
        string label,
        IReadOnlyDictionary<string, List<TItem>> existingByIdentity,
        string entityLabel)
    {
        if (!existingByIdentity.TryGetValue(canonicalIdentity, out var matches))
        {
            return default;
        }

        if (matches.Count > 1)
        {
            throw new InvalidOperationException(
                $"Import for {entityLabel} '{label}' is ambiguous because canonical identity '{canonicalIdentity}' matches multiple existing {entityLabel} records.");
        }

        return matches[0];
    }
}
