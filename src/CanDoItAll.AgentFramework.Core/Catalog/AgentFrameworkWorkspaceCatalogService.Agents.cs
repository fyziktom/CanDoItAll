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
        var normalizedTemplateKey = WorkspaceCatalogIdentityNormalizer.NormalizeTemplateKey(model.TemplateKey, model.Name);
        var id = model.Id ?? Guid.NewGuid();
        var configurationJson = AgentProjectStructureAccessMetadata.Write(model.ConfigurationJson, model.ProjectStructureAccess);
        configurationJson = AgentProcessAccessMetadata.Write(configurationJson, model.ProcessAccess);
        configurationJson = AgentWorkspaceToolAccessMetadata.Write(configurationJson, model.WorkspaceToolAccess);
        configurationJson = AgentImageGenerationAccessMetadata.Write(configurationJson, model.ImageGenerationAccess);
        configurationJson = AgentVoiceAccessMetadata.Write(configurationJson, model.VoiceAccess);
        await UpdateCatalogAsync(catalog =>
        {
            var existingAgent = catalog.Agents.FirstOrDefault(item => item.Id == id);
            var capabilities = catalog.Capabilities
                .Where(item => model.SelectedCapabilityIds.Contains(item.Id))
                .Select(item =>
                {
                    var existingCapability = existingAgent?.Capabilities
                        .FirstOrDefault(capability => capability.CapabilityId == item.Id);

                    return new AgentCapabilityAssignment(
                        item.Id,
                        item.Key,
                        item.Kind,
                        existingCapability?.ProofStatus ?? item.ProofStatus,
                        existingCapability?.LastVerifiedAtUtc ?? item.LastVerifiedAtUtc,
                        existingCapability?.ProofNotes ?? item.ProofNotes);
                })
                .ToList();

            EnsureUniqueTemplateKey(catalog.Agents, id, normalizedTemplateKey, "Agent save");
            var definition = new AgentDefinition(
                Id: id,
                Name: model.Name.Trim(),
                RoleTitle: model.RoleTitle.Trim(),
                Summary: model.Summary.Trim(),
                Instructions: model.Instructions.Trim(),
                Status: model.Status,
                ProviderProfileId: model.ProviderProfileId,
                Model: model.Model.Trim(),
                Workload: model.Workload,
                ChatHistoryMode: model.ChatHistoryMode,
                Temperature: model.Temperature,
                RequirePerServiceCallChatHistoryPersistence: model.RequirePerServiceCallChatHistoryPersistence,
                EnableBackgroundResponses: model.EnableBackgroundResponses,
                ConfigurationJson: configurationJson.Trim(),
                IsTemplate: model.IsTemplate,
                TemplateKey: normalizedTemplateKey,
                Permissions: model.Permissions with
                {
                    AllowedSecrets = NormalizeAllowedSecretReferences(model.AllowedSecretReferences)
                },
                Capabilities: capabilities,
                Tags: model.Tags
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Select(item => item.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                CreatedAtUtc: existingAgent?.CreatedAtUtc ?? now,
                UpdatedAtUtc: now)
            {
                AvatarImageUrl = string.IsNullOrWhiteSpace(model.AvatarImageUrl)
                    ? null
                    : model.AvatarImageUrl.Trim()
            };

            return catalog with
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

    public async Task DeleteAgentAsync(Guid agentId, CancellationToken cancellationToken = default)
    {
        await store.UpdateWorkspaceAsync(
            document => PruneAgentWorkspace(document, agentId),
            cancellationToken);
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
            EnsureUniqueTemplateKey(catalog.Agents, cloneId, cloneTemplateKey, "Agent clone");
            var clone = source with
            {
                Id = cloneId,
                Name = cloneName.Trim(),
                IsTemplate = false,
                TemplateKey = cloneTemplateKey,
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
            EnsureUniqueTemplateKey(catalog.Agents, templateId, normalizedTemplateKey, "Template conversion");
            var template = source with
            {
                Id = templateId,
                IsTemplate = true,
                TemplateKey = normalizedTemplateKey,
                Name = $"{source.Name} template",
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
            var prunedDocument = PruneAgentWorkspace(document, importedAgent.Id);
            EnsureUniqueTemplateKey(prunedDocument.Agents, importedAgent.Id, WorkspaceCatalogIdentityNormalizer.GetAgentTemplateIdentity(importedAgent), "Agent import");

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

    private static SandboxWorkspaceDocument PruneAgentWorkspace(SandboxWorkspaceDocument document, Guid agentId)
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
                .ToList()
        };
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

    private static IReadOnlyList<AgentAllowedSecretReference> NormalizeAllowedSecretReferences(
        IEnumerable<AgentAllowedSecretReference> references)
    {
        return references
            .Where(item => item.SecretId != Guid.Empty)
            .GroupBy(item => item.SecretId)
            .Select(group =>
            {
                var item = group.Last();
                return new AgentAllowedSecretReference(
                    item.SecretId,
                    item.NameSnapshot.Trim(),
                    string.IsNullOrWhiteSpace(item.Purpose)
                        ? AgentSecretPurposes.GeneralAgentRequest
                        : item.Purpose.Trim());
            })
            .OrderBy(item => item.NameSnapshot, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.SecretId)
            .ToList();
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

    private static void EnsureUniqueTemplateKey(
        IEnumerable<AgentDefinition> agents,
        Guid currentAgentId,
        string templateKey,
        string operationLabel)
    {
        var collisions = agents
            .Where(item => item.Id != currentAgentId)
            .Where(item => string.Equals(WorkspaceCatalogIdentityNormalizer.GetAgentTemplateIdentity(item), templateKey, StringComparison.Ordinal))
            .Select(item => item.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (collisions.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            $"{operationLabel} would reuse canonical template key '{templateKey}', which already belongs to: {string.Join(", ", collisions)}.");
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
