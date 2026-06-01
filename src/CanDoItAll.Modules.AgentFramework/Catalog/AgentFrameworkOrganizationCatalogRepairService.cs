using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.AgentFramework;

public interface IAgentFrameworkOrganizationCatalogRepairService
{
    Task EnsureCurrentOrganizationCatalogAsync(CancellationToken cancellationToken = default);
}

internal sealed class AgentFrameworkOrganizationCatalogRepairService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ICanDoItAllAgentWorkspaceFactory workspaceFactory,
    IProviderProfileService providerProfileService) : IAgentFrameworkOrganizationCatalogRepairService
{
    private const string CrmHrRuntimeAgentTemplateKeyPrefix = "crmhr-ai-resource-";

    private static readonly IReadOnlySet<string> ManagedSeedOpenAiProviderNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "OpenAI default",
        "OpenAI chat completions"
    };

    public async Task EnsureCurrentOrganizationCatalogAsync(CancellationToken cancellationToken = default)
    {
        await EnsureCurrentOrganizationCatalogCoreAsync(cancellationToken);
    }

    private async Task EnsureCurrentOrganizationCatalogCoreAsync(CancellationToken cancellationToken)
    {
        var currentWorkspace = workspaceFactory.GetWorkspaceService(workspaceFactory.GetOrganizationScope());
        var currentAgents = (await currentWorkspace.ListAgentsAsync(includeTemplates: false, cancellationToken)).ToList();

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var aiPartyIds = await dbContext.Set<Party>()
            .Where(item => item.PartyType == PartyType.AiAgent)
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);
        if (aiPartyIds.Count == 0)
        {
            await RepairOpenAiAgentAssignmentsAsync(currentWorkspace, cancellationToken);
            return;
        }

        var aiPartyIdSet = aiPartyIds.ToHashSet();
        var boundBindings = await dbContext.Set<AiResourceBinding>()
            .Where(item => aiPartyIds.Contains(item.PartyId) && item.TechnicalAgentId.HasValue)
            .ToListAsync(cancellationToken);
        if (CurrentWorkspaceAlreadyOwnsProjectedAgents(currentAgents, aiPartyIds, boundBindings))
        {
            await RepairOpenAiAgentAssignmentsAsync(currentWorkspace, cancellationToken);
            return;
        }

        var legacyScopeKeys = GetLegacyOrganizationScopeKeys();
        if (legacyScopeKeys.Count == 0)
        {
            await RepairOpenAiAgentAssignmentsAsync(currentWorkspace, cancellationToken);
            return;
        }

        var boundTechnicalAgentIds = boundBindings
            .Select(item => item.TechnicalAgentId!.Value)
            .ToHashSet();
        var currentProviders = (await currentWorkspace.ListProvidersAsync(cancellationToken)).ToList();
        var currentCapabilities = (await currentWorkspace.ListCapabilitiesAsync(cancellationToken)).ToList();

        foreach (var legacyScopeKey in legacyScopeKeys)
        {
            var legacyWorkspace = workspaceFactory.GetWorkspaceService(WorkspaceScopeDescriptor.Organization(legacyScopeKey));
            var legacyAgents = await legacyWorkspace.ListAgentsAsync(includeTemplates: false, cancellationToken);
            var agentsToImport = legacyAgents
                .Where(agent => ShouldImportAgent(agent, aiPartyIdSet, boundTechnicalAgentIds))
                .Where(agent => FindMatchingCurrentAgent(currentAgents, agent) is null)
                .ToList();
            if (agentsToImport.Count == 0)
            {
                continue;
            }

            var legacyProviders = await legacyWorkspace.ListProvidersAsync(cancellationToken);
            var legacyCapabilities = await legacyWorkspace.ListCapabilitiesAsync(cancellationToken);
            var providerIdMap = await EnsureProvidersAsync(
                currentWorkspace,
                currentProviders,
                legacyProviders,
                agentsToImport,
                cancellationToken);
            var capabilityIdMap = await EnsureCapabilitiesAsync(
                currentWorkspace,
                currentCapabilities,
                legacyCapabilities,
                agentsToImport,
                cancellationToken);

            foreach (var legacyAgent in agentsToImport)
            {
                var editor = AgentEditorModel.FromDefinition(legacyAgent);
                if (editor.ProviderProfileId.HasValue &&
                    providerIdMap.TryGetValue(editor.ProviderProfileId.Value, out var mappedProviderId))
                {
                    editor.ProviderProfileId = mappedProviderId;
                }

                editor.SelectedCapabilityIds = editor.SelectedCapabilityIds
                    .Select(capabilityId => capabilityIdMap.TryGetValue(capabilityId, out var mappedCapabilityId)
                        ? mappedCapabilityId
                        : capabilityId)
                    .Distinct()
                    .ToList();

                var importedAgentId = await currentWorkspace.SaveAgentAsync(editor, cancellationToken);
                currentAgents.Add(legacyAgent with
                {
                    Id = importedAgentId,
                    ProviderProfileId = editor.ProviderProfileId,
                    Capabilities = legacyAgent.Capabilities
                        .Select(capability => capabilityIdMap.TryGetValue(capability.CapabilityId, out var mappedCapabilityId)
                            ? capability with { CapabilityId = mappedCapabilityId }
                            : capability)
                        .ToList()
                });
            }
        }

        await RepairOpenAiAgentAssignmentsAsync(currentWorkspace, cancellationToken);
    }

    private async Task RepairOpenAiAgentAssignmentsAsync(
        IAgentFrameworkWorkspaceService currentWorkspace,
        CancellationToken cancellationToken)
    {
        var providers = (await currentWorkspace.ListProvidersAsync(cancellationToken)).ToList();
        var openAiProvider = SelectManagedSeedOpenAiProvider(providers);
        if (openAiProvider is null)
        {
            return;
        }

        var store = new FileSandboxWorkspaceStore(
            workspaceFactory.GetWorkspaceRoot(),
            workspaceFactory.GetOrganizationScope());
        var repairedOpenAiProviderConfigurationJson = ManagedSeedProviderFallbacks.EnsureDefaultReasoningConfigurationJson(
            openAiProvider.ConfigurationJson,
            "service-managed");
        if (!string.Equals(openAiProvider.DefaultModel, ManagedSeedProviderFallbacks.OpenAiDefaultModel, StringComparison.Ordinal) ||
            !string.Equals(openAiProvider.Name, ManagedSeedProviderFallbacks.OpenAiDefaultProviderName, StringComparison.Ordinal) ||
            openAiProvider.Transport != ProviderTransportKind.Responses ||
            openAiProvider.PreferFrameworkManagedChatHistory ||
            !openAiProvider.SupportsBackgroundResponses ||
            !string.Equals(openAiProvider.ConfigurationJson, repairedOpenAiProviderConfigurationJson, StringComparison.Ordinal))
        {
            openAiProvider = await RepairManagedSeedOpenAiProviderDefaultAsync(
                store,
                openAiProvider,
                cancellationToken);
        }

        var agents = await currentWorkspace.ListAgentsAsync(includeTemplates: false, cancellationToken);
        var agentsNeedingRepair = agents
            .Where(RequiresOpenAiAssignmentRepair)
            .Where(agent =>
                agent.ProviderProfileId != openAiProvider.Id ||
                !string.IsNullOrWhiteSpace(agent.Model) ||
                !string.Equals(
                    agent.ConfigurationJson,
                    ManagedSeedProviderFallbacks.EnsureDefaultReasoningConfigurationJson(agent.ConfigurationJson),
                    StringComparison.Ordinal))
            .ToList();
        if (agentsNeedingRepair.Count == 0)
        {
            return;
        }

        var agentIdsNeedingRepair = agentsNeedingRepair
            .Select(agent => agent.Id)
            .ToHashSet();
        var updatedAtUtc = DateTimeOffset.UtcNow;
        await store.UpdateCatalogAsync(catalog => catalog with
        {
            Agents = catalog.Agents
                .Select(agent => agentIdsNeedingRepair.Contains(agent.Id)
                    ? agent with
                    {
                        ProviderProfileId = openAiProvider.Id,
                        Model = string.Empty,
                        ConfigurationJson = ManagedSeedProviderFallbacks.EnsureDefaultReasoningConfigurationJson(agent.ConfigurationJson),
                        UpdatedAtUtc = updatedAtUtc
                    }
                    : agent)
                .OrderBy(agent => agent.Name, StringComparer.OrdinalIgnoreCase)
                .ToList()
        }, cancellationToken);
    }

    private static async Task<ProviderProfile> RepairManagedSeedOpenAiProviderDefaultAsync(
        FileSandboxWorkspaceStore store,
        ProviderProfile openAiProvider,
        CancellationToken cancellationToken)
    {
        var updatedProvider = openAiProvider with
        {
            Name = ManagedSeedProviderFallbacks.OpenAiDefaultProviderName,
            DefaultModel = ManagedSeedProviderFallbacks.OpenAiDefaultModel,
            Transport = ProviderTransportKind.Responses,
            PreferFrameworkManagedChatHistory = false,
            SupportsBackgroundResponses = true,
            ConfigurationJson = ManagedSeedProviderFallbacks.EnsureDefaultReasoningConfigurationJson(
                openAiProvider.ConfigurationJson,
                "service-managed"),
            SuggestedModels =
            [
                ManagedSeedProviderFallbacks.OpenAiDefaultModel,
                .. openAiProvider.SuggestedModels
                    .Where(item => !string.Equals(item, ManagedSeedProviderFallbacks.OpenAiDefaultModel, StringComparison.OrdinalIgnoreCase))
            ]
        };

        await store.UpdateCatalogAsync(catalog => catalog with
        {
            Providers = catalog.Providers
                .Select(provider => provider.Id == openAiProvider.Id ? updatedProvider : provider)
                .OrderBy(provider => provider.Name, StringComparer.OrdinalIgnoreCase)
                .ToList()
        }, cancellationToken);

        return updatedProvider;
    }

    private static ProviderProfile? SelectManagedSeedOpenAiProvider(
        IReadOnlyList<ProviderProfile> providers)
    {
        return providers.FirstOrDefault(provider =>
                   provider.Kind is ProviderKind.OpenAi or ProviderKind.AzureOpenAi &&
                   provider.Transport == ProviderTransportKind.Responses &&
                   string.Equals(provider.Name, ManagedSeedProviderFallbacks.OpenAiDefaultProviderName, StringComparison.OrdinalIgnoreCase)) ??
               providers.FirstOrDefault(provider =>
                   IsManagedSeedOpenAiProvider(provider) &&
                   provider.Transport == ProviderTransportKind.Responses) ??
               providers.FirstOrDefault(IsManagedSeedOpenAiProvider);
    }

    private static bool RequiresOpenAiAssignmentRepair(
        AgentDefinition agent)
    {
        return ManagedSeedProviderFallbacks.IsManagedSeedAgent(agent) ||
               IsCrmHrRuntimeAgent(agent);
    }

    private static bool IsCrmHrRuntimeAgent(
        AgentDefinition agent)
    {
        return !agent.IsTemplate &&
               (agent.TemplateKey.StartsWith(CrmHrRuntimeAgentTemplateKeyPrefix, StringComparison.OrdinalIgnoreCase) ||
                AgentFrameworkCrmHrMetadata.ResolvePartyId(agent.ConfigurationJson, agent.Tags).HasValue);
    }

    private static bool IsManagedSeedOpenAiProvider(
        ProviderProfile provider)
    {
        return provider.Kind is ProviderKind.OpenAi or ProviderKind.AzureOpenAi &&
               ManagedSeedOpenAiProviderNames.Contains(provider.Name);
    }

    private IReadOnlyList<string> GetLegacyOrganizationScopeKeys()
    {
        var currentScope = workspaceFactory.GetOrganizationScope();
        var organizationRoot = Path.Combine(
            workspaceFactory.GetWorkspaceRoot(),
            "data",
            "scopes",
            "organization");
        if (!Directory.Exists(organizationRoot))
        {
            return [];
        }

        return Directory.GetDirectories(organizationRoot)
            .Select(Path.GetFileName)
            .Where(scopeKey => !string.IsNullOrWhiteSpace(scopeKey))
            .Where(scopeKey => !string.Equals(scopeKey, currentScope.Key, StringComparison.OrdinalIgnoreCase))
            .Where(scopeKey => LegacyOrganizationScopeExists(organizationRoot, scopeKey!))
            .Cast<string>()
            .OrderBy(scopeKey => scopeKey, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool LegacyOrganizationScopeExists(
        string organizationRoot,
        string scopeKey)
    {
        var scopeRoot = Path.Combine(organizationRoot, scopeKey);
        return File.Exists(Path.Combine(scopeRoot, "workspace.json")) ||
               File.Exists(Path.Combine(scopeRoot, "workspace.index.json")) ||
               File.Exists(Path.Combine(scopeRoot, "workspace.execution.json")) ||
               Directory.Exists(Path.Combine(scopeRoot, "execution"));
    }

    private async Task<Dictionary<Guid, Guid>> EnsureProvidersAsync(
        IAgentFrameworkWorkspaceService currentWorkspace,
        List<ProviderProfile> currentProviders,
        IReadOnlyList<ProviderProfile> legacyProviders,
        IReadOnlyList<AgentDefinition> agentsToImport,
        CancellationToken cancellationToken)
    {
        var providerIds = agentsToImport
            .Where(agent => agent.ProviderProfileId.HasValue)
            .Select(agent => agent.ProviderProfileId!.Value)
            .Distinct()
            .ToList();
        var providerIdMap = new Dictionary<Guid, Guid>();

        foreach (var providerId in providerIds)
        {
            var legacyProvider = legacyProviders.FirstOrDefault(item => item.Id == providerId);
            if (legacyProvider is null)
            {
                continue;
            }

            var currentProvider = currentProviders.FirstOrDefault(item => item.Id == legacyProvider.Id)
                ?? currentProviders.FirstOrDefault(item =>
                    string.Equals(
                        providerProfileService.GetIdentityKey(item),
                        providerProfileService.GetIdentityKey(legacyProvider),
                        StringComparison.Ordinal));
            if (currentProvider is null)
            {
                var importedProviderId = await currentWorkspace.SaveProviderAsync(
                    ProviderProfileEditorModel.FromDefinition(legacyProvider),
                    cancellationToken);
                currentProvider = (await currentWorkspace.ListProvidersAsync(cancellationToken))
                    .First(item => item.Id == importedProviderId);
                currentProviders.RemoveAll(item => item.Id == currentProvider.Id);
                currentProviders.Add(currentProvider);
            }

            providerIdMap[providerId] = currentProvider.Id;
        }

        return providerIdMap;
    }

    private async Task<Dictionary<Guid, Guid>> EnsureCapabilitiesAsync(
        IAgentFrameworkWorkspaceService currentWorkspace,
        List<CapabilityCatalogItem> currentCapabilities,
        IReadOnlyList<CapabilityCatalogItem> legacyCapabilities,
        IReadOnlyList<AgentDefinition> agentsToImport,
        CancellationToken cancellationToken)
    {
        var capabilityIds = agentsToImport
            .SelectMany(agent => agent.Capabilities.Select(capability => capability.CapabilityId))
            .Distinct()
            .ToList();
        var capabilityIdMap = new Dictionary<Guid, Guid>();

        foreach (var capabilityId in capabilityIds)
        {
            var legacyCapability = legacyCapabilities.FirstOrDefault(item => item.Id == capabilityId);
            if (legacyCapability is null)
            {
                continue;
            }

            var currentCapability = currentCapabilities.FirstOrDefault(item => item.Id == legacyCapability.Id)
                ?? currentCapabilities.FirstOrDefault(item =>
                    string.Equals(
                        BuildCapabilityIdentity(item),
                        BuildCapabilityIdentity(legacyCapability),
                        StringComparison.Ordinal));
            if (currentCapability is null)
            {
                var importedCapabilityId = await currentWorkspace.SaveCapabilityAsync(
                    CapabilityEditorModel.FromDefinition(legacyCapability),
                    cancellationToken);
                currentCapability = (await currentWorkspace.ListCapabilitiesAsync(cancellationToken))
                    .First(item => item.Id == importedCapabilityId);
                currentCapabilities.RemoveAll(item => item.Id == currentCapability.Id);
                currentCapabilities.Add(currentCapability);
            }

            capabilityIdMap[capabilityId] = currentCapability.Id;
        }

        return capabilityIdMap;
    }

    private static bool CurrentWorkspaceAlreadyOwnsProjectedAgents(
        IReadOnlyList<AgentDefinition> currentAgents,
        IReadOnlyList<Guid> aiPartyIds,
        IReadOnlyList<AiResourceBinding> boundBindings)
    {
        if (aiPartyIds.Count == 0)
        {
            return true;
        }

        if (currentAgents.Count == 0)
        {
            return false;
        }

        var currentAgentIds = currentAgents
            .Select(item => item.Id)
            .ToHashSet();
        var coveredPartyIds = currentAgents
            .Select(agent => AgentFrameworkCrmHrMetadata.ResolvePartyId(agent.ConfigurationJson, agent.Tags))
            .Where(item => item.HasValue)
            .Select(item => item!.Value)
            .ToHashSet();

        foreach (var binding in boundBindings)
        {
            if (binding.TechnicalAgentId.HasValue &&
                currentAgentIds.Contains(binding.TechnicalAgentId.Value))
            {
                coveredPartyIds.Add(binding.PartyId);
            }
        }

        return aiPartyIds.All(coveredPartyIds.Contains);
    }

    private static bool ShouldImportAgent(
        AgentDefinition agent,
        IReadOnlySet<Guid> aiPartyIds,
        IReadOnlySet<Guid> boundTechnicalAgentIds)
    {
        if (boundTechnicalAgentIds.Contains(agent.Id))
        {
            return true;
        }

        var partyId = AgentFrameworkCrmHrMetadata.ResolvePartyId(agent.ConfigurationJson, agent.Tags);
        return partyId.HasValue && aiPartyIds.Contains(partyId.Value);
    }

    private static AgentDefinition? FindMatchingCurrentAgent(
        IReadOnlyList<AgentDefinition> currentAgents,
        AgentDefinition candidate)
    {
        var candidatePartyId = AgentFrameworkCrmHrMetadata.ResolvePartyId(candidate.ConfigurationJson, candidate.Tags);
        return currentAgents.FirstOrDefault(agent =>
        {
            if (agent.Id == candidate.Id)
            {
                return true;
            }

            if (!candidatePartyId.HasValue)
            {
                return false;
            }

            var currentPartyId = AgentFrameworkCrmHrMetadata.ResolvePartyId(agent.ConfigurationJson, agent.Tags);
            return currentPartyId == candidatePartyId.Value;
        });
    }

    private static string BuildCapabilityIdentity(
        CapabilityCatalogItem capability)
    {
        return $"{capability.Kind}:{NormalizeComparableKey(capability.Key)}";
    }

    private static string NormalizeComparableKey(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new System.Text.StringBuilder(value.Length);
        var pendingSeparator = false;
        foreach (var character in value.Trim())
        {
            if (char.IsLetterOrDigit(character))
            {
                if (pendingSeparator && builder.Length > 0)
                {
                    builder.Append('-');
                }

                builder.Append(char.ToLowerInvariant(character));
                pendingSeparator = false;
                continue;
            }

            if (builder.Length > 0)
            {
                pendingSeparator = true;
            }
        }

        return builder.ToString();
    }
}
