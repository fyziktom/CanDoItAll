using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Persistence;

internal static class SandboxWorkspaceSeedNormalizer
{
    private const string RetiredSandboxAssemblyName = "CanDoItAll.AgentFramework.Sandbox";
    private static readonly ProviderProfileService ProviderProfileService = new();
    private static readonly IReadOnlySet<string> ManagedSeedOpenAiProviderNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "OpenAI default",
        "OpenAI chat completions",
        "OpenAI image generation"
    };
    private static readonly IReadOnlySet<string> RetiredProjectStructureCatalogToolKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "project-task-create",
        "project-task-update"
    };

    internal static SandboxWorkspaceDocument Normalize(SandboxWorkspaceDocument document)
    {
        return SandboxWorkspaceDocument.Combine(
            NormalizeCatalog(document.ToCatalog()),
            NormalizeExecutionState(document.ToExecutionState()));
    }

    internal static SandboxWorkspaceCatalog NormalizeCatalog(SandboxWorkspaceCatalog catalog)
    {
        var seeded = SandboxWorkspaceSeedFactory.Create();
        var providers = MergeProviders(catalog.Providers, seeded.Providers);
        var capabilities = MergeCapabilities(catalog.Capabilities, seeded.Capabilities);
        var agents = MergeAgents(
            catalog.Agents,
            seeded.Agents,
            providers.Items,
            capabilities.Items,
            providers.IdMap,
            capabilities.IdMap);
        var memory = MergeMemory(catalog.Memory, seeded.Memory, agents.IdMap);
        var activeCapabilities = RemoveRetiredCapabilities(capabilities.Items, seeded.Capabilities);
        var activeAgents = RemoveUnavailableAgentCapabilities(agents.Items, activeCapabilities);
        var agentTeams = MergeAgentTeams(catalog.AgentTeams, seeded.AgentTeams, agents.IdMap, activeAgents);

        return catalog with
        {
            Version = seeded.Version,
            Providers = providers.Items,
            Capabilities = activeCapabilities,
            Agents = activeAgents,
            AgentTeams = agentTeams,
            Memory = memory
        };
    }

    internal static SandboxWorkspaceExecutionState NormalizeExecutionState(SandboxWorkspaceExecutionState executionState)
    {
        var seeded = SandboxWorkspaceSeedFactory.Create();
        var normalizedRuns = NormalizeExecutionRuns(executionState.ExecutionRuns);
        var compatibilityRuns = CreateLegacySessionCompatibilityRuns(executionState.ChatSessions, normalizedRuns);
        if (compatibilityRuns.Count > 0)
        {
            normalizedRuns = NormalizeExecutionRuns(normalizedRuns.Concat(compatibilityRuns).ToList());
        }

        var latestRunBySessionId = BuildLatestRunBySessionId(normalizedRuns);
        return executionState with
        {
            Version = seeded.Version,
            ChatSessions = NormalizeChatSessions(executionState.ChatSessions, latestRunBySessionId),
            ExecutionLog = (executionState.ExecutionLog ?? []).OrderByDescending(item => item.CreatedAtUtc).ToList(),
            Metrics = (executionState.Metrics ?? []).OrderByDescending(item => item.CreatedAtUtc).ToList(),
            ProviderUsageObservations = (executionState.ProviderUsageObservations ?? []).OrderByDescending(item => item.CreatedAtUtc).ToList(),
            ExecutionRuns = normalizedRuns,
            ExecutionApprovals = NormalizeExecutionApprovals(executionState.ExecutionApprovals),
            ExecutionArtifacts = NormalizeExecutionArtifacts(executionState.ExecutionArtifacts),
            ExecutionWorkflowCheckpoints = NormalizeExecutionWorkflowCheckpoints(executionState.ExecutionWorkflowCheckpoints),
            ToolExecutionReceipts = NormalizeToolExecutionReceipts(executionState.ToolExecutionReceipts)
        };
    }

    private static MergeResult<ProviderProfile> MergeProviders(IReadOnlyList<ProviderProfile> existingProviders, IReadOnlyList<ProviderProfile> seededProviders)
    {
        var merged = existingProviders
            .Select(provider => ProviderProfileService.NormalizeImportedProfile(provider with
            {
                Tags = ResolveProviderTags(provider)
            }))
            .ToList();
        var idMap = new Dictionary<Guid, Guid>();

        foreach (var seededProvider in seededProviders.Select(ProviderProfileService.NormalizeImportedProfile))
        {
            var normalizedSeededProvider = seededProvider with
            {
                Tags = ResolveProviderTags(seededProvider)
            };
            var match = merged.FirstOrDefault(item => item.Id == seededProvider.Id)
                ?? merged.FirstOrDefault(item => string.Equals(item.Name, seededProvider.Name, StringComparison.OrdinalIgnoreCase) && item.Kind == seededProvider.Kind);

            if (match is null)
            {
                merged.Add(normalizedSeededProvider);
                idMap[seededProvider.Id] = seededProvider.Id;
                continue;
            }

            idMap[seededProvider.Id] = match.Id;
            var isManagedSeedOpenAiProvider = IsManagedSeedOpenAiProvider(match, seededProvider);
            var mergedProvider = match with
            {
                ApiKeyEnvironmentVariable = string.IsNullOrWhiteSpace(match.ApiKeyEnvironmentVariable) ? seededProvider.ApiKeyEnvironmentVariable : match.ApiKeyEnvironmentVariable,
                DefaultModel = ShouldUseSeedProviderDefaultModel(match, seededProvider)
                    ? seededProvider.DefaultModel
                    : match.DefaultModel,
                SupportsStreaming = match.SupportsStreaming || seededProvider.SupportsStreaming,
                SupportsTools = match.SupportsTools || seededProvider.SupportsTools,
                PreferFrameworkManagedChatHistory = match.PreferFrameworkManagedChatHistory || seededProvider.PreferFrameworkManagedChatHistory,
                SupportsBackgroundResponses = match.SupportsBackgroundResponses || seededProvider.SupportsBackgroundResponses,
                Purpose = ShouldUseSeedProviderPurpose(match, seededProvider) ? seededProvider.Purpose : match.Purpose,
                ConfigurationJson = string.IsNullOrWhiteSpace(match.ConfigurationJson) ? seededProvider.ConfigurationJson : match.ConfigurationJson,
                Notes = string.IsNullOrWhiteSpace(match.Notes) ? seededProvider.Notes : match.Notes,
                SuggestedModels = ResolveMergedProviderSuggestedModels(
                    match,
                    seededProvider,
                    isManagedSeedOpenAiProvider),
                ModelPrices = isManagedSeedOpenAiProvider
                    ? ProviderPricingDefaults.MergeAuthoritativeKnownDefaultPrices(
                        match.Kind,
                        seededProvider.DefaultModel,
                        match.ModelPrices)
                    : match.ModelPrices,
                Tags = NormalizeTags(match.Tags.Concat(normalizedSeededProvider.Tags))
            };

            ReplaceById(
                merged,
                match.Id,
                ProviderProfileService.NormalizeImportedProfile(mergedProvider));
        }

        return new MergeResult<ProviderProfile>(merged.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToList(), idMap);
    }

    private static bool IsManagedSeedOpenAiProvider(ProviderProfile existingProvider, ProviderProfile seededProvider)
        => IsManagedSeedOpenAiProvider(existingProvider) &&
           IsManagedSeedOpenAiProvider(seededProvider);

    private static bool IsManagedSeedOpenAiProvider(ProviderProfile provider)
    {
        return provider.Kind is ProviderKind.OpenAi or ProviderKind.AzureOpenAi &&
               ManagedSeedOpenAiProviderNames.Contains(provider.Name);
    }

    private static bool ShouldUseSeedProviderDefaultModel(
        ProviderProfile existingProvider,
        ProviderProfile seededProvider)
    {
        if (string.IsNullOrWhiteSpace(existingProvider.DefaultModel))
        {
            return true;
        }

        if (seededProvider.Purpose == ProviderProfilePurpose.ImageGeneration)
        {
            return HasCanonicalManagedSeedIdentity(existingProvider, seededProvider) &&
                   string.Equals(
                       existingProvider.DefaultModel,
                       OpenAiModelIds.GptImage1Mini,
                       StringComparison.Ordinal);
        }

        return IsManagedSeedOpenAiProvider(existingProvider, seededProvider);
    }

    private static bool ShouldUseSeedProviderPurpose(ProviderProfile existingProvider, ProviderProfile seededProvider)
    {
        return seededProvider.Purpose != ProviderProfilePurpose.Chat &&
               existingProvider.Kind == seededProvider.Kind &&
               (existingProvider.Id == seededProvider.Id ||
                string.Equals(existingProvider.Name, seededProvider.Name, StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<string> ResolveMergedProviderSuggestedModels(
        ProviderProfile existingProvider,
        ProviderProfile seededProvider,
        bool isManagedSeedOpenAiProvider)
    {
        var existingSuggestedModels = seededProvider.Purpose == ProviderProfilePurpose.ImageGeneration &&
                                      HasCanonicalManagedSeedIdentity(existingProvider, seededProvider)
            ? existingProvider.SuggestedModels.Where(model => !string.Equals(
                model,
                OpenAiModelIds.GptImage1Mini,
                StringComparison.Ordinal))
            : existingProvider.SuggestedModels;
        var candidates = isManagedSeedOpenAiProvider
            ? seededProvider.SuggestedModels.Concat(existingSuggestedModels)
            : existingSuggestedModels.Concat(seededProvider.SuggestedModels);
        return candidates
            .Where(model => !string.IsNullOrWhiteSpace(model))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool HasCanonicalManagedSeedIdentity(
        ProviderProfile existingProvider,
        ProviderProfile seededProvider)
    {
        return existingProvider.Id == seededProvider.Id &&
               IsManagedSeedOpenAiProvider(seededProvider);
    }

    private static MergeResult<CapabilityCatalogItem> MergeCapabilities(IReadOnlyList<CapabilityCatalogItem> existingCapabilities, IReadOnlyList<CapabilityCatalogItem> seededCapabilities)
    {
        var merged = existingCapabilities
            .Select(capability => capability with { Tags = ResolveCapabilityTags(capability) })
            .ToList();
        var idMap = new Dictionary<Guid, Guid>();

        foreach (var seededCapability in seededCapabilities)
        {
            var normalizedSeededCapability = seededCapability with
            {
                Tags = ResolveCapabilityTags(seededCapability)
            };
            var idMatches = merged
                .Where(item => item.Id == seededCapability.Id)
                .ToArray();
            if (idMatches.Length > 1)
            {
                throw new InvalidOperationException(
                    $"Capability id '{seededCapability.Id:D}' is duplicated in the catalog.");
            }

            var semanticMatches = merged
                .Where(item => item.Kind == seededCapability.Kind)
                .Where(item => string.Equals(
                    item.Key,
                    seededCapability.Key,
                    StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (semanticMatches.Length > 1)
            {
                throw new InvalidOperationException(
                    $"Capability identity '{seededCapability.Kind}:{seededCapability.Key}' is ambiguous in the catalog.");
            }

            var match = idMatches.SingleOrDefault() ?? semanticMatches.SingleOrDefault();
            if (seededCapability.IsBuiltIn &&
                semanticMatches.SingleOrDefault() is { IsBuiltIn: false } customCollision &&
                customCollision.Id != seededCapability.Id)
            {
                throw new InvalidOperationException(
                    $"Custom capability '{customCollision.Id:D}' collides with built-in capability " +
                    $"'{seededCapability.Kind}:{seededCapability.Key}'.");
            }

            if (match is null)
            {
                merged.Add(normalizedSeededCapability);
                idMap[seededCapability.Id] = seededCapability.Id;
                continue;
            }

            idMap[seededCapability.Id] = match.Id;
            var mergedCapability = match with
            {
                Kind = seededCapability.IsBuiltIn ? seededCapability.Kind : match.Kind,
                Key = seededCapability.IsBuiltIn ? seededCapability.Key : match.Key,
                Name = string.IsNullOrWhiteSpace(match.Name) ? seededCapability.Name : match.Name,
                Description = string.IsNullOrWhiteSpace(match.Description) ? seededCapability.Description : match.Description,
                EndpointOrPath = string.IsNullOrWhiteSpace(match.EndpointOrPath) ? seededCapability.EndpointOrPath : match.EndpointOrPath,
                ConfigurationJson = string.IsNullOrWhiteSpace(match.ConfigurationJson) ? seededCapability.ConfigurationJson : match.ConfigurationJson,
                ProofNotes = string.IsNullOrWhiteSpace(match.ProofNotes) ? seededCapability.ProofNotes : match.ProofNotes,
                IsBuiltIn = match.IsBuiltIn || seededCapability.IsBuiltIn,
                Tags = NormalizeTags(match.Tags.Concat(normalizedSeededCapability.Tags))
            };

            if (ShouldRefreshManagedCapabilityFromSeed(match, seededCapability))
            {
                mergedCapability = RefreshManagedCapabilityFromSeed(mergedCapability, seededCapability);
            }

            ReplaceById(merged, match.Id, mergedCapability);
        }

        return new MergeResult<CapabilityCatalogItem>(merged.OrderBy(item => item.Kind).ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToList(), idMap);
    }

    private static MergeResult<AgentDefinition> MergeAgents(
        IReadOnlyList<AgentDefinition> existingAgents,
        IReadOnlyList<AgentDefinition> seededAgents,
        IReadOnlyList<ProviderProfile> providers,
        IReadOnlyList<CapabilityCatalogItem> capabilities,
        IReadOnlyDictionary<Guid, Guid> providerIdMap,
        IReadOnlyDictionary<Guid, Guid> capabilityIdMap)
    {
        var merged = existingAgents.ToList();
        var idMap = new Dictionary<Guid, Guid>();
        var providersById = providers.ToDictionary(provider => provider.Id);

        foreach (var seededAgent in seededAgents.Select(agent => RemapSeedAgent(agent, providerIdMap, capabilityIdMap)))
        {
            var requiresCanonicalIdentity = ManagedAdministrativeAgentIdentityCatalog.AgentIds.Contains(seededAgent.Id);
            var idMatch = merged.FirstOrDefault(item => item.Id == seededAgent.Id);
            if (requiresCanonicalIdentity && idMatch is not null)
            {
                idMatch = CanonicalizeReservedManagedAgentIdentity(idMatch, seededAgent);
            }

            var match = idMatch
                ?? (!requiresCanonicalIdentity && !string.IsNullOrWhiteSpace(seededAgent.TemplateKey)
                    ? merged.FirstOrDefault(item => string.Equals(item.TemplateKey, seededAgent.TemplateKey, StringComparison.OrdinalIgnoreCase))
                    : null)
                ?? (!requiresCanonicalIdentity
                    ? merged.FirstOrDefault(item => string.Equals(item.Name, seededAgent.Name, StringComparison.OrdinalIgnoreCase) && item.IsTemplate == seededAgent.IsTemplate)
                    : null);

            if (match is null)
            {
                merged.Add(seededAgent);
                idMap[seededAgent.Id] = seededAgent.Id;
                continue;
            }

            idMap[seededAgent.Id] = match.Id;
            var canonicalizedMatch = CanonicalizePresentManagedAssignments(match, seededAgent, capabilities);
            var migratedMatch = ApplySchedulerSchedulingPermissionMigration(
                ApplyWorkflowRuntimeGrantMigration(
                    ApplyHrCapabilityCurationGrantMigration(canonicalizedMatch, seededAgent),
                    seededAgent),
                seededAgent);
            var preserveProviderAssignment = ShouldPreserveExplicitProviderAssignment(migratedMatch, seededAgent, providersById);
            var hasCustomization = AgentManagedSeedCustomizationMetadata.HasCustomization(migratedMatch.ConfigurationJson);
            var mergedAgent = hasCustomization
                ? migratedMatch
                : migratedMatch with
                {
                    RoleTitle = string.IsNullOrWhiteSpace(migratedMatch.RoleTitle) ? seededAgent.RoleTitle : migratedMatch.RoleTitle,
                    Summary = string.IsNullOrWhiteSpace(migratedMatch.Summary) ? seededAgent.Summary : migratedMatch.Summary,
                    Instructions = string.IsNullOrWhiteSpace(migratedMatch.Instructions) ? seededAgent.Instructions : migratedMatch.Instructions,
                    ProviderProfileId = migratedMatch.ProviderProfileId ?? seededAgent.ProviderProfileId,
                    Model = ResolveMergedAgentModel(migratedMatch, seededAgent, preserveProviderAssignment),
                    RequirePerServiceCallChatHistoryPersistence = migratedMatch.RequirePerServiceCallChatHistoryPersistence || seededAgent.RequirePerServiceCallChatHistoryPersistence,
                    EnableBackgroundResponses = migratedMatch.EnableBackgroundResponses || seededAgent.EnableBackgroundResponses,
                    ConfigurationJson = string.IsNullOrWhiteSpace(migratedMatch.ConfigurationJson) ? seededAgent.ConfigurationJson : migratedMatch.ConfigurationJson,
                    Capabilities = MergeAgentCapabilities(migratedMatch.Capabilities, seededAgent.Capabilities),
                    Tags = migratedMatch.Tags.Concat(seededAgent.Tags).Where(item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
                };

            if (ShouldRefreshManagedAgentFromSeed(migratedMatch, seededAgent))
            {
                mergedAgent = RefreshManagedAgentFromSeed(mergedAgent, seededAgent, providersById);
            }

            ReplaceById(merged, match.Id, mergedAgent);
        }

        var catalogById = capabilities.ToDictionary(capability => capability.Id);
        var normalizedAssignments = merged
            .Select(agent => CanonicalizeCatalogAssignmentSnapshots(agent, catalogById))
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return new MergeResult<AgentDefinition>(normalizedAssignments, idMap);
    }

    private static AgentDefinition CanonicalizeReservedManagedAgentIdentity(
        AgentDefinition existingAgent,
        AgentDefinition seededAgent)
    {
        if (!TryGetManagedSeedVersion(seededAgent, out _))
        {
            throw new InvalidOperationException(
                $"Reserved managed agent seed '{seededAgent.Id:D}' does not declare a managed seed version.");
        }

        if (!TryGetManagedSeedVersion(existingAgent, out _))
        {
            throw new InvalidOperationException(
                $"Agent '{existingAgent.Id:D}' collides with reserved managed identity '{seededAgent.TemplateKey}'.");
        }

        return existingAgent with
        {
            IsTemplate = seededAgent.IsTemplate,
            TemplateKey = seededAgent.TemplateKey
        };
    }

    private static string ResolveMergedAgentModel(
        AgentDefinition existingAgent,
        AgentDefinition seededAgent,
        bool preserveProviderAssignment)
    {
        if (preserveProviderAssignment)
        {
            return string.IsNullOrWhiteSpace(existingAgent.Model)
                ? string.Empty
                : existingAgent.Model;
        }

        return string.IsNullOrWhiteSpace(existingAgent.Model) ? seededAgent.Model : existingAgent.Model;
    }

    private static AgentDefinition RemapSeedAgent(
        AgentDefinition seededAgent,
        IReadOnlyDictionary<Guid, Guid> providerIdMap,
        IReadOnlyDictionary<Guid, Guid> capabilityIdMap)
    {
        return seededAgent with
        {
            ProviderProfileId = seededAgent.ProviderProfileId.HasValue && providerIdMap.TryGetValue(seededAgent.ProviderProfileId.Value, out var providerId)
                ? providerId
                : seededAgent.ProviderProfileId,
            Capabilities = seededAgent.Capabilities.Select(capability => capabilityIdMap.TryGetValue(capability.CapabilityId, out var capabilityId) ? capability with { CapabilityId = capabilityId } : capability).ToList()
        };
    }

    private static IReadOnlyList<AgentCapabilityAssignment> MergeAgentCapabilities(
        IReadOnlyList<AgentCapabilityAssignment> existingCapabilities,
        IReadOnlyList<AgentCapabilityAssignment> seededCapabilities)
    {
        var merged = existingCapabilities.ToList();
        foreach (var seededCapability in seededCapabilities)
        {
            if (merged.Any(item => item.CapabilityId == seededCapability.CapabilityId || (item.Kind == seededCapability.Kind && string.Equals(item.CapabilityKey, seededCapability.CapabilityKey, StringComparison.OrdinalIgnoreCase))))
            {
                continue;
            }

            merged.Add(seededCapability);
        }

        return merged.OrderBy(item => item.Kind).ThenBy(item => item.CapabilityKey, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static AgentDefinition CanonicalizePresentManagedAssignments(
        AgentDefinition existingAgent,
        AgentDefinition seededAgent,
        IReadOnlyList<CapabilityCatalogItem> capabilityCatalog)
    {
        if (!IsTrustedManagedSeedPair(existingAgent, seededAgent))
        {
            return existingAgent;
        }

        var catalogById = capabilityCatalog.ToDictionary(capability => capability.Id);
        var seededAssignmentsByKey = seededAgent.Capabilities
            .GroupBy(assignment => assignment.CapabilityKey, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single(), StringComparer.OrdinalIgnoreCase);
        var synchronized = existingAgent.Capabilities
            .Select(assignment => CanonicalizePresentAssignment(
                assignment,
                catalogById,
                seededAssignmentsByKey))
            .GroupBy(assignment => assignment.CapabilityId)
            .Select(CollapseAssignmentDuplicates)
            .OrderBy(assignment => assignment.Kind)
            .ThenBy(assignment => assignment.CapabilityKey, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return existingAgent with { Capabilities = synchronized };
    }

    private static AgentDefinition CanonicalizeCatalogAssignmentSnapshots(
        AgentDefinition agent,
        IReadOnlyDictionary<Guid, CapabilityCatalogItem> catalogById)
    {
        var synchronized = agent.Capabilities
            .Select(assignment => catalogById.TryGetValue(assignment.CapabilityId, out var capability)
                ? assignment with
                {
                    CapabilityKey = capability.Key,
                    Kind = capability.Kind
                }
                : assignment)
            .GroupBy(assignment => assignment.CapabilityId)
            .Select(CollapseAssignmentDuplicates)
            .OrderBy(assignment => assignment.Kind)
            .ThenBy(assignment => assignment.CapabilityKey, StringComparer.Ordinal)
            .ToArray();
        return agent with { Capabilities = synchronized };
    }

    private static bool IsTrustedManagedSeedPair(AgentDefinition existingAgent, AgentDefinition seededAgent)
    {
        return existingAgent.Id == seededAgent.Id &&
               existingAgent.IsTemplate == seededAgent.IsTemplate &&
               string.Equals(existingAgent.TemplateKey, seededAgent.TemplateKey, StringComparison.Ordinal) &&
               TryGetManagedSeedVersion(existingAgent, out _) &&
               TryGetManagedSeedVersion(seededAgent, out _);
    }

    private static AgentCapabilityAssignment CanonicalizePresentAssignment(
        AgentCapabilityAssignment assignment,
        IReadOnlyDictionary<Guid, CapabilityCatalogItem> catalogById,
        IReadOnlyDictionary<string, AgentCapabilityAssignment> seededAssignmentsByKey)
    {
        if (catalogById.TryGetValue(assignment.CapabilityId, out var catalogCapability))
        {
            return assignment with
            {
                CapabilityKey = catalogCapability.Key,
                Kind = catalogCapability.Kind
            };
        }

        return seededAssignmentsByKey.TryGetValue(assignment.CapabilityKey, out var seededAssignment)
            ? assignment with
            {
                CapabilityId = seededAssignment.CapabilityId,
                CapabilityKey = seededAssignment.CapabilityKey,
                Kind = seededAssignment.Kind
            }
            : assignment;
    }

    private static AgentCapabilityAssignment CollapseAssignmentDuplicates(
        IGrouping<Guid, AgentCapabilityAssignment> assignments)
    {
        var candidates = assignments.ToArray();
        var selected = candidates[0];
        if (candidates.Length == 1 || candidates.All(candidate => AssignmentProofEquals(candidate, selected)))
        {
            return selected;
        }

        return selected with
        {
            ProofStatus = CapabilityProofStatus.NotRun,
            LastVerifiedAtUtc = null,
            ProofNotes = string.Empty
        };
    }

    private static bool AssignmentProofEquals(
        AgentCapabilityAssignment left,
        AgentCapabilityAssignment right)
    {
        return left.ProofStatus == right.ProofStatus &&
               left.LastVerifiedAtUtc == right.LastVerifiedAtUtc &&
               string.Equals(left.ProofNotes, right.ProofNotes, StringComparison.Ordinal);
    }

    private static AgentDefinition ApplyHrCapabilityCurationGrantMigration(
        AgentDefinition existingAgent,
        AgentDefinition seededAgent)
        => ApplyManagedCapabilityGrantMigration(
            existingAgent,
            seededAgent,
            HrAgentIdentity.Matches,
            HrAgentIdentity.CapabilityCurationAccessVersionPropertyName,
            HrAgentIdentity.CurrentCapabilityCurationAccessVersion,
            HrAgentIdentity.CapabilityCurationCapabilityKeys,
            "HR capability-curation");

    private static AgentDefinition ApplyWorkflowRuntimeGrantMigration(
        AgentDefinition existingAgent,
        AgentDefinition seededAgent)
        => ApplyManagedCapabilityGrantMigration(
            existingAgent,
            seededAgent,
            WorkflowCuratorAgentIdentity.Matches,
            WorkflowCuratorAgentIdentity.RuntimeAccessVersionPropertyName,
            WorkflowCuratorAgentIdentity.CurrentRuntimeAccessVersion,
            WorkflowRuntimeCapabilityKeys.Keys,
            "Workflow Curator runtime-access");

    private static AgentDefinition ApplySchedulerSchedulingPermissionMigration(
        AgentDefinition existingAgent,
        AgentDefinition seededAgent)
    {
        if (!TryPrepareManagedMigrationConfiguration(
                existingAgent,
                seededAgent,
                SchedulerAgentIdentity.Matches,
                SchedulerAgentIdentity.SchedulingAccessVersionPropertyName,
                SchedulerAgentIdentity.CurrentSchedulingAccessVersion,
                out var existingConfiguration))
        {
            return existingAgent;
        }

        if (!seededAgent.Permissions.CanScheduleWork)
        {
            throw new InvalidOperationException(
                "The managed Scheduler scheduling-access migration must enable scheduling permission.");
        }

        return existingAgent with
        {
            ConfigurationJson = existingConfiguration.ToJsonString(),
            Permissions = existingAgent.Permissions with { CanScheduleWork = true }
        };
    }

    private static AgentDefinition ApplyManagedCapabilityGrantMigration(
        AgentDefinition existingAgent,
        AgentDefinition seededAgent,
        Func<AgentDefinition?, bool> matchesIdentity,
        string versionPropertyName,
        string currentVersion,
        IReadOnlySet<string> capabilityKeys,
        string migrationName)
    {
        if (!TryPrepareManagedMigrationConfiguration(
                existingAgent,
                seededAgent,
                matchesIdentity,
                versionPropertyName,
                currentVersion,
                out var existingConfiguration))
        {
            return existingAgent;
        }

        var migrationCapabilities = seededAgent.Capabilities
            .Where(item => capabilityKeys.Contains(item.CapabilityKey))
            .ToArray();
        var migrationCapabilityKeys = migrationCapabilities
            .Select(item => item.CapabilityKey)
            .ToHashSet(StringComparer.Ordinal);
        var missingCapabilityKeys = capabilityKeys
            .Except(migrationCapabilityKeys, StringComparer.Ordinal)
            .ToArray();
        if (missingCapabilityKeys.Length > 0 ||
            migrationCapabilities.Length != capabilityKeys.Count)
        {
            throw new InvalidOperationException(
                $"The managed {migrationName} migration is incomplete: {string.Join(", ", missingCapabilityKeys)}.");
        }

        return existingAgent with
        {
            ConfigurationJson = existingConfiguration.ToJsonString(),
            Capabilities = MergeAgentCapabilities(existingAgent.Capabilities, migrationCapabilities)
        };
    }

    private static bool TryPrepareManagedMigrationConfiguration(
        AgentDefinition existingAgent,
        AgentDefinition seededAgent,
        Func<AgentDefinition?, bool> matchesIdentity,
        string versionPropertyName,
        string currentVersion,
        out JsonObject existingConfiguration)
    {
        existingConfiguration = [];
        if (!matchesIdentity(existingAgent) ||
            !matchesIdentity(seededAgent))
        {
            return false;
        }

        JsonObject seededConfiguration;
        try
        {
            existingConfiguration = JsonNode.Parse(existingAgent.ConfigurationJson) as JsonObject
                ?? throw new JsonException("The existing managed agent configuration is not a JSON object.");
            seededConfiguration = JsonNode.Parse(seededAgent.ConfigurationJson) as JsonObject
                ?? throw new JsonException("The seeded managed agent configuration is not a JSON object.");
        }
        catch (JsonException)
        {
            existingConfiguration = [];
            return false;
        }

        if (existingConfiguration.ContainsKey(versionPropertyName))
        {
            return false;
        }

        if (seededConfiguration[versionPropertyName] is not JsonValue seededVersion ||
            !seededVersion.TryGetValue<string>(out var version) ||
            !string.Equals(version, currentVersion, StringComparison.Ordinal))
        {
            return false;
        }

        existingConfiguration[versionPropertyName] = version;
        return true;
    }

    private static bool ShouldRefreshManagedAgentFromSeed(AgentDefinition existingAgent, AgentDefinition seededAgent)
    {
        if (AgentManagedSeedCustomizationMetadata.HasCustomization(existingAgent.ConfigurationJson))
        {
            return false;
        }

        if (!TryGetManagedSeedVersion(seededAgent, out var managedSeedVersion))
        {
            return false;
        }

        if (TryGetManagedSeedVersion(existingAgent, out var currentSeedVersion) &&
            string.Equals(currentSeedVersion, managedSeedVersion, StringComparison.OrdinalIgnoreCase))
        {
            return HasManagedAgentPolicyDrift(existingAgent, seededAgent);
        }

        return !string.IsNullOrWhiteSpace(seededAgent.TemplateKey);
    }

    private static bool HasManagedAgentPolicyDrift(AgentDefinition existingAgent, AgentDefinition seededAgent)
    {
        return !string.Equals(existingAgent.Model, seededAgent.Model, StringComparison.OrdinalIgnoreCase) ||
               !ThinkingEffortPolicyEquals(existingAgent.ConfigurationJson, seededAgent.ConfigurationJson) ||
               !ProjectStructureAccessEquals(existingAgent.ConfigurationJson, seededAgent.ConfigurationJson) ||
               !ProcessAccessEquals(existingAgent.ConfigurationJson, seededAgent.ConfigurationJson) ||
               !WorkspaceToolAccessEquals(existingAgent.ConfigurationJson, seededAgent.ConfigurationJson) ||
               !ImageGenerationAccessEquals(existingAgent.ConfigurationJson, seededAgent.ConfigurationJson) ||
               !PermissionsPolicyEquals(existingAgent.Permissions, seededAgent.Permissions) ||
               !CapabilityPolicyEquals(existingAgent.Capabilities, seededAgent.Capabilities);
    }

    private static bool ThinkingEffortPolicyEquals(
        string existingConfigurationJson,
        string seededConfigurationJson)
    {
        return AgentThinkingEffortPolicy.ReadConfiguredEffort(existingConfigurationJson, "existing agent") ==
               AgentThinkingEffortPolicy.ReadConfiguredEffort(seededConfigurationJson, "seeded agent");
    }

    private static bool CapabilityPolicyEquals(
        IReadOnlyList<AgentCapabilityAssignment> existingCapabilities,
        IReadOnlyList<AgentCapabilityAssignment> seededCapabilities)
    {
        var existingPolicy = existingCapabilities
            .Select(ToCapabilityAssignmentPolicy)
            .OrderBy(item => item.CapabilityId)
            .ThenBy(item => item.CapabilityKey, StringComparer.Ordinal)
            .ThenBy(item => item.Kind);
        var seededPolicy = seededCapabilities
            .Select(ToCapabilityAssignmentPolicy)
            .OrderBy(item => item.CapabilityId)
            .ThenBy(item => item.CapabilityKey, StringComparer.Ordinal)
            .ThenBy(item => item.Kind);

        return existingPolicy.SequenceEqual(seededPolicy);
    }

    private static CapabilityAssignmentPolicy ToCapabilityAssignmentPolicy(AgentCapabilityAssignment assignment)
        => new(assignment.CapabilityId, assignment.CapabilityKey, assignment.Kind);

    private static bool PermissionsPolicyEquals(AgentPermissionsPolicy existing, AgentPermissionsPolicy seeded)
    {
        return existing.CanUseTools == seeded.CanUseTools &&
               existing.CanAskOtherAgents == seeded.CanAskOtherAgents &&
               existing.CanEscalateToHuman == seeded.CanEscalateToHuman &&
               existing.CanObserveOtherAgents == seeded.CanObserveOtherAgents &&
               existing.CanScheduleWork == seeded.CanScheduleWork &&
               existing.RequiresApprovalForExternalCalls == seeded.RequiresApprovalForExternalCalls &&
               existing.AutoApproveExternalCallsByDefault == seeded.AutoApproveExternalCallsByDefault;
    }

    private static bool ProjectStructureAccessEquals(string existingConfigurationJson, string seededConfigurationJson)
    {
        var existing = AgentProjectStructureAccessMetadata.Read(existingConfigurationJson);
        var seeded = AgentProjectStructureAccessMetadata.Read(seededConfigurationJson);
        return existing.CanRead == seeded.CanRead &&
               existing.CanWrite == seeded.CanWrite &&
               existing.CanWriteNonTaskStructure == seeded.CanWriteNonTaskStructure &&
               existing.CanWriteTasks == seeded.CanWriteTasks &&
               existing.CanCreateProjects == seeded.CanCreateProjects &&
               existing.CanCreateSubprojects == seeded.CanCreateSubprojects &&
               existing.AllowAllProjects == seeded.AllowAllProjects &&
               existing.AllowedProjectIds.SequenceEqual(seeded.AllowedProjectIds);
    }

    private static bool ProcessAccessEquals(string existingConfigurationJson, string seededConfigurationJson)
    {
        var existing = AgentProcessAccessMetadata.Read(existingConfigurationJson);
        var seeded = AgentProcessAccessMetadata.Read(seededConfigurationJson);
        return existing.CanRead == seeded.CanRead &&
               existing.CanWrite == seeded.CanWrite &&
               existing.AllowAllDefinitions == seeded.AllowAllDefinitions &&
               existing.AllowedDefinitionIds.SequenceEqual(seeded.AllowedDefinitionIds);
    }

    private static bool WorkspaceToolAccessEquals(string existingConfigurationJson, string seededConfigurationJson)
    {
        var existing = AgentWorkspaceToolAccessMetadata.Read(existingConfigurationJson);
        var seeded = AgentWorkspaceToolAccessMetadata.Read(seededConfigurationJson);
        return existing.Profile == seeded.Profile &&
               existing.CanReadFiles == seeded.CanReadFiles &&
               existing.CanWriteFiles == seeded.CanWriteFiles &&
               existing.CanRunValidationCommands == seeded.CanRunValidationCommands &&
               existing.CanRunLocalScripts == seeded.CanRunLocalScripts &&
               existing.CanScaffoldProjects == seeded.CanScaffoldProjects &&
               existing.CanManageWorkspacePaths == seeded.CanManageWorkspacePaths &&
               existing.CanTransformArtifacts == seeded.CanTransformArtifacts &&
               existing.CanReadStorage == seeded.CanReadStorage &&
               existing.CanWriteStorage == seeded.CanWriteStorage &&
               existing.AllowAllStorageCatalogs == seeded.AllowAllStorageCatalogs &&
               existing.AllowedExternalTargetAliases.SequenceEqual(seeded.AllowedExternalTargetAliases, StringComparer.OrdinalIgnoreCase) &&
               existing.AllowedStorageCatalogIds.SequenceEqual(seeded.AllowedStorageCatalogIds);
    }

    private static bool ImageGenerationAccessEquals(string existingConfigurationJson, string seededConfigurationJson)
    {
        var existing = AgentImageGenerationAccessMetadata.Read(existingConfigurationJson);
        var seeded = AgentImageGenerationAccessMetadata.Read(seededConfigurationJson);
        return existing.CanGenerateImages == seeded.CanGenerateImages &&
               existing.PreferredProviderProfileId == seeded.PreferredProviderProfileId &&
               string.Equals(existing.DefaultModel, seeded.DefaultModel, StringComparison.Ordinal) &&
               existing.CanStoreImagesAsProjectAssets == seeded.CanStoreImagesAsProjectAssets;
    }

    private static AgentDefinition RefreshManagedAgentFromSeed(
        AgentDefinition existingAgent,
        AgentDefinition seededAgent,
        IReadOnlyDictionary<Guid, ProviderProfile> providersById)
    {
        var preserveProviderAssignment = ShouldPreserveExplicitProviderAssignment(existingAgent, seededAgent, providersById);

        return existingAgent with
        {
            RoleTitle = seededAgent.RoleTitle,
            Summary = seededAgent.Summary,
            Instructions = seededAgent.Instructions,
            ProviderProfileId = preserveProviderAssignment ? existingAgent.ProviderProfileId : seededAgent.ProviderProfileId,
            Model = preserveProviderAssignment ? existingAgent.Model : seededAgent.Model,
            Workload = seededAgent.Workload,
            ChatHistoryMode = preserveProviderAssignment ? existingAgent.ChatHistoryMode : seededAgent.ChatHistoryMode,
            Temperature = seededAgent.Temperature,
            RequirePerServiceCallChatHistoryPersistence = preserveProviderAssignment
                ? existingAgent.RequirePerServiceCallChatHistoryPersistence
                : seededAgent.RequirePerServiceCallChatHistoryPersistence,
            EnableBackgroundResponses = preserveProviderAssignment
                ? existingAgent.EnableBackgroundResponses
                : seededAgent.EnableBackgroundResponses,
            ConfigurationJson = preserveProviderAssignment
                ? CopyManagedSeedVersion(existingAgent.ConfigurationJson, seededAgent.ConfigurationJson)
                : seededAgent.ConfigurationJson,
            AvatarImageUrl = string.IsNullOrWhiteSpace(existingAgent.AvatarImageUrl)
                ? seededAgent.AvatarImageUrl
                : existingAgent.AvatarImageUrl,
            Permissions = seededAgent.Permissions,
            Capabilities = seededAgent.Capabilities,
            Tags = seededAgent.Tags
                .Concat(existingAgent.Tags.Where(AgentSpecialTags.IsFavorite))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
        };
    }

    private static bool ShouldPreserveExplicitProviderAssignment(
        AgentDefinition existingAgent,
        AgentDefinition seededAgent,
        IReadOnlyDictionary<Guid, ProviderProfile> providersById)
    {
        if (!existingAgent.ProviderProfileId.HasValue ||
            existingAgent.ProviderProfileId == seededAgent.ProviderProfileId ||
            !providersById.TryGetValue(existingAgent.ProviderProfileId.Value, out var provider))
        {
            return false;
        }

        if (ManagedSeedProviderFallbacks.HasProviderRepairFallbackOverride(existingAgent))
        {
            return true;
        }

        return !IsManagedSeedOpenAiProvider(provider) &&
               !ManagedSeedProviderFallbacks.IsGeneratedManagedSeedFallbackProvider(provider);
    }

    private static string CopyManagedSeedVersion(
        string targetConfigurationJson,
        string sourceConfigurationJson)
    {
        var target = ParseConfigurationObject(targetConfigurationJson);
        var source = ParseConfigurationObject(sourceConfigurationJson);
        if (source["managedSeedVersion"] is JsonValue sourceVersion &&
            sourceVersion.TryGetValue<string>(out var version) &&
            !string.IsNullOrWhiteSpace(version))
        {
            target["managedSeedVersion"] = version;
        }

        return target.ToJsonString();
    }

    private static JsonObject ParseConfigurationObject(string configurationJson)
    {
        if (string.IsNullOrWhiteSpace(configurationJson))
        {
            return [];
        }

        try
        {
            return JsonNode.Parse(configurationJson) as JsonObject ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static bool TryGetManagedSeedVersion(AgentDefinition agent, out string version)
    {
        return TryGetManagedSeedVersion(agent.ConfigurationJson, out version);
    }

    private static bool TryGetManagedSeedVersion(string configurationJson, out string version)
    {
        version = string.Empty;
        if (string.IsNullOrWhiteSpace(configurationJson))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(configurationJson);
            if (!document.RootElement.TryGetProperty("managedSeedVersion", out var versionElement))
            {
                return false;
            }

            version = versionElement.GetString() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(version);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool ShouldRefreshManagedCapabilityFromSeed(CapabilityCatalogItem existingCapability, CapabilityCatalogItem seededCapability)
    {
        if (ShouldRefreshVersionedManagedCapabilityFromSeed(existingCapability, seededCapability))
        {
            return true;
        }

        if (string.Equals(seededCapability.Key, "architecture-map-inline-skill", StringComparison.OrdinalIgnoreCase))
        {
            return !existingCapability.ConfigurationJson.Contains("Use this skill only when the user explicitly asks for a Mermaid or class-diagram output.", StringComparison.OrdinalIgnoreCase);
        }

        if (string.Equals(seededCapability.Key, "architecture-review-inline-skill", StringComparison.OrdinalIgnoreCase))
        {
            return !existingCapability.ConfigurationJson.Contains("Do not start with a broad workspace inventory", StringComparison.OrdinalIgnoreCase)
                   || !existingCapability.ConfigurationJson.Contains("Do not call out `net10.0` as a problem by itself", StringComparison.OrdinalIgnoreCase)
                   || !existingCapability.ConfigurationJson.Contains("AgentFrameworkWorkspaceService.cs", StringComparison.OrdinalIgnoreCase)
                   || !existingCapability.ConfigurationJson.Contains("Do not claim missing abstractions", StringComparison.OrdinalIgnoreCase)
                   || !existingCapability.ConfigurationJson.Contains("Good candidates are: `AgentFrameworkWorkspaceService.Chat.cs`", StringComparison.OrdinalIgnoreCase)
                   || !existingCapability.ConfigurationJson.Contains("Return 2 to 4 bullets only", StringComparison.OrdinalIgnoreCase);
        }

        if (string.Equals(seededCapability.Key, "blazor-ssr-delivery-inline-skill", StringComparison.OrdinalIgnoreCase))
        {
            return !InlineSkillInstructionsContain(existingCapability.ConfigurationJson, "Use this skill only for Blazor app delivery")
                   || !InlineSkillInstructionsContain(existingCapability.ConfigurationJson, "generic across app domains")
                   || !InlineSkillInstructionsContain(existingCapability.ConfigurationJson, "If the project structure or attached step materials name a concrete output directory")
                   || !InlineSkillInstructionsContain(existingCapability.ConfigurationJson, "external-target/<drive>/...")
                   || !InlineSkillInstructionsContain(existingCapability.ConfigurationJson, "do not scaffold a parallel copy under `artifacts/...`, `output/...`, or another generated implementation folder")
                   || !InlineSkillInstructionsContain(existingCapability.ConfigurationJson, "scaffold directly into it instead of adding an extra nested")
                   || !InlineSkillInstructionsContain(existingCapability.ConfigurationJson, "Before any scaffold call")
                   || !InlineSkillInstructionsContain(existingCapability.ConfigurationJson, "Use a distinct concrete type such as `<Feature>Service`")
                   || !InlineSkillInstructionsContain(existingCapability.ConfigurationJson, "service, model, value object, or enum")
                   || !InlineSkillInstructionsContain(existingCapability.ConfigurationJson, "inside the grounded product root")
                   || !InlineSkillInstructionsContain(existingCapability.ConfigurationJson, "do not reuse the host scaffold parent directory with `name: <Host>.Tests`")
                   || !InlineSkillInstructionsContain(existingCapability.ConfigurationJson, "Do not scaffold `<ProductParent>/<Host>.Domain` as a sibling")
                   || !InlineSkillInstructionsContain(existingCapability.ConfigurationJson, "If policy denies an external-target path")
                   || !InlineSkillInstructionsContain(existingCapability.ConfigurationJson, "current Blazor Web App")
                   || !InlineSkillInstructionsContain(existingCapability.ConfigurationJson, "never authorize the obsolete `blazorserver` template")
                   || !InlineSkillInstructionsContain(existingCapability.ConfigurationJson, "Before final evidence artifacts or a governed outcome")
                   || !InlineSkillInstructionsContain(existingCapability.ConfigurationJson, "Final product-validation order")
                    || !InlineSkillInstructionsContain(existingCapability.ConfigurationJson, "Never make validation pass by writing fake package")
                    || !InlineSkillInstructionsContain(existingCapability.ConfigurationJson, "Never inspect, cite, copy, or infer implementation patterns from sibling external-target applications")
                    || !InlineSkillInstructionsContain(existingCapability.ConfigurationJson, "Do not claim contextual examples, source files, templates, or implementation references were reviewed")
                    || !InlineSkillInstructionsContain(existingCapability.ConfigurationJson, "leave `keepAlive` false unless this same step immediately needs browser tools")
                    || !InlineSkillInstructionsContain(existingCapability.ConfigurationJson, "Workspace command timeout arguments are seconds")
                    || !InlineSkillInstructionsContain(existingCapability.ConfigurationJson, "When writing xUnit tests, include a visible `using Xunit;`")
                    || !InlineSkillInstructionsContain(existingCapability.ConfigurationJson, "run target must be the runnable project file")
                    || !InlineSkillInstructionsContain(existingCapability.ConfigurationJson, "custom route backed only by scaffold-default `app.css` and layout CSS")
                    || !InlineSkillInstructionsContain(existingCapability.ConfigurationJson, "custom class names without matching loaded styles")
                    || !InlineSkillInstructionsContain(existingCapability.ConfigurationJson, "one run-app proof node, one run-tests proof node, and one manager summary node");
        }

        if (string.Equals(seededCapability.Key, "concrete-deliverable-delivery-inline-skill", StringComparison.OrdinalIgnoreCase))
        {
            return !InlineSkillInstructionsContain(existingCapability.ConfigurationJson, "any process step that creates, repairs, validates, or summarizes a concrete deliverable")
                   || !InlineSkillInstructionsContain(existingCapability.ConfigurationJson, "A deliverable can be an app, service, API")
                   || !InlineSkillInstructionsContain(existingCapability.ConfigurationJson, "Do not reuse sample topics, older generated apps")
                   || !InlineSkillInstructionsContain(existingCapability.ConfigurationJson, "Use technology-specific skills and tools only after the current files or step contract justify them")
                    || !InlineSkillInstructionsContain(existingCapability.ConfigurationJson, "Final delivery order is strict")
                    || !InlineSkillInstructionsContain(existingCapability.ConfigurationJson, "Do not cite files, paths, examples, source artifacts, or tool results as evidence")
                    || !InlineSkillInstructionsContain(existingCapability.ConfigurationJson, "For documents, render/export/open the produced file")
                   || !InlineSkillInstructionsContain(existingCapability.ConfigurationJson, "For spreadsheets, inspect workbook structure")
                    || !InlineSkillInstructionsContain(existingCapability.ConfigurationJson, "Do not claim completion with chat-only evidence")
                    || !InlineSkillInstructionsContain(existingCapability.ConfigurationJson, "computed styles apply to the primary surface")
                   || !InlineSkillInstructionsContain(existingCapability.ConfigurationJson, "one run-app proof node, one run-tests proof node, and one manager summary node");
        }

        if (string.Equals(seededCapability.Key, "dotnet-app-delivery-inline-skill", StringComparison.OrdinalIgnoreCase))
        {
            return !InlineSkillInstructionsContain(existingCapability.ConfigurationJson, "For greenfield work with an explicit output root")
                   || !InlineSkillInstructionsContain(existingCapability.ConfigurationJson, "parentDirectory: external-target/C/work/apps")
                   || !InlineSkillInstructionsContain(existingCapability.ConfigurationJson, "Before any scaffold call")
                   || !InlineSkillInstructionsContain(existingCapability.ConfigurationJson, "Keep the requested product domain authoritative")
                   || !InlineSkillInstructionsContain(existingCapability.ConfigurationJson, "keep every generated app project, support library, test project")
                   || !InlineSkillInstructionsContain(existingCapability.ConfigurationJson, "<product-root>/src/<AppName>.Domain")
                   || !InlineSkillInstructionsContain(existingCapability.ConfigurationJson, "managed workspace roots such as `src/`, `tests/`, `tools/`")
                   || !InlineSkillInstructionsContain(existingCapability.ConfigurationJson, "If policy denies an external-target path")
                   || !InlineSkillInstructionsContain(existingCapability.ConfigurationJson, "Preserve the template-selected `TargetFramework`")
                   || !InlineSkillInstructionsContain(existingCapability.ConfigurationJson, "Use one test framework per test project")
                   || !InlineSkillInstructionsContain(existingCapability.ConfigurationJson, "If a scaffold command returns an unsuccessful result")
                   || !InlineSkillInstructionsContain(existingCapability.ConfigurationJson, "For ASP.NET Core, web API, or Blazor apps")
                   || !InlineSkillInstructionsContain(existingCapability.ConfigurationJson, "Final product-validation order")
                   || !InlineSkillInstructionsContain(existingCapability.ConfigurationJson, "Before final evidence artifacts or a governed outcome")
                    || !InlineSkillInstructionsContain(existingCapability.ConfigurationJson, "Never make validation pass by writing fake package")
                    || !InlineSkillInstructionsContain(existingCapability.ConfigurationJson, "Never inspect, cite, copy, or infer implementation patterns from sibling external-target applications")
                    || !InlineSkillInstructionsContain(existingCapability.ConfigurationJson, "Do not claim contextual examples, source files, templates, or implementation references were reviewed")
                    || !InlineSkillInstructionsContain(existingCapability.ConfigurationJson, "waitForHttp: false")
                   || !InlineSkillInstructionsContain(existingCapability.ConfigurationJson, "Leave `keepAlive` false for startup proof")
                   || !InlineSkillInstructionsContain(existingCapability.ConfigurationJson, "Workspace command timeout arguments are seconds")
                   || !InlineSkillInstructionsContain(existingCapability.ConfigurationJson, "When writing xUnit tests, include a visible `using Xunit;`")
                   || !InlineSkillInstructionsContain(existingCapability.ConfigurationJson, "domain-specific classes but only stock template CSS")
                   || !InlineSkillInstructionsContain(existingCapability.ConfigurationJson, "one run-app proof node, one run-tests proof node, and one manager summary node");
        }

        if (string.Equals(seededCapability.Key, "workspace-list-files", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(seededCapability.Key, "workspace-read-file", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(seededCapability.Key, "workspace-stat-path", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(seededCapability.Key, "workspace-create-directory", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(seededCapability.Key, "workspace-write-file", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(seededCapability.Key, "workspace-append-file", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(seededCapability.Key, "workspace-dotnet-restore", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(seededCapability.Key, "workspace-dotnet-build", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(seededCapability.Key, "workspace-dotnet-test", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(seededCapability.Key, "workspace-dotnet-run", StringComparison.OrdinalIgnoreCase))
        {
            return !existingCapability.Description.Contains("grounded external-target alias", StringComparison.OrdinalIgnoreCase)
                   || (string.Equals(seededCapability.Key, "workspace-dotnet-run", StringComparison.OrdinalIgnoreCase) &&
                       (!existingCapability.Description.Contains("stops the launched process tree by default", StringComparison.OrdinalIgnoreCase) ||
                        !existingCapability.Description.Contains("keepAlive true", StringComparison.OrdinalIgnoreCase)))
                   || (string.Equals(seededCapability.Key, "workspace-list-files", StringComparison.OrdinalIgnoreCase) &&
                       (!existingCapability.Description.Contains("broad managed-root browsing is denied", StringComparison.OrdinalIgnoreCase) ||
                        !existingCapability.Description.Contains("recursive globstar patterns", StringComparison.OrdinalIgnoreCase)))
                   || (string.Equals(seededCapability.Key, "workspace-read-file", StringComparison.OrdinalIgnoreCase) &&
                       !existingCapability.Description.Contains("unmanaged source or helper roots", StringComparison.OrdinalIgnoreCase))
                   || ((string.Equals(seededCapability.Key, "workspace-create-directory", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(seededCapability.Key, "workspace-write-file", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(seededCapability.Key, "workspace-append-file", StringComparison.OrdinalIgnoreCase)) &&
                       !existingCapability.Description.Contains("managed src/tests/tools roots", StringComparison.OrdinalIgnoreCase));
        }

        if (string.Equals(seededCapability.Key, "workspace-dotnet-stop", StringComparison.OrdinalIgnoreCase))
        {
            return !existingCapability.Description.Contains("startup.json receipt", StringComparison.OrdinalIgnoreCase)
                   || !existingCapability.Description.Contains("cleanup.json proof", StringComparison.OrdinalIgnoreCase)
                   || !existingCapability.Description.Contains("workspace_pwsh_run_script", StringComparison.OrdinalIgnoreCase);
        }

        if (string.Equals(seededCapability.Key, "workspace-search", StringComparison.OrdinalIgnoreCase))
        {
            return !existingCapability.Description.Contains("external-target paths require explicit current-run grounding", StringComparison.OrdinalIgnoreCase)
                   || !existingCapability.Description.Contains("broad managed-root search is denied", StringComparison.OrdinalIgnoreCase);
        }

        if (string.Equals(seededCapability.Key, "workspace-dotnet-new", StringComparison.OrdinalIgnoreCase))
        {
            return !existingCapability.Description.Contains("grounded external-target alias", StringComparison.OrdinalIgnoreCase)
                   || !existingCapability.Description.Contains("parentDirectory", StringComparison.OrdinalIgnoreCase)
                   || !existingCapability.Description.Contains("parentDirectory under the grounded product root", StringComparison.OrdinalIgnoreCase)
                   || !existingCapability.Description.Contains("never reuse the product parent", StringComparison.OrdinalIgnoreCase)
                   || !existingCapability.Description.Contains("inspect an unsuccessful result before retrying", StringComparison.OrdinalIgnoreCase)
                   || !existingCapability.Description.Contains("managed src/tests/tools roots", StringComparison.OrdinalIgnoreCase);
        }

        if (string.Equals(seededCapability.Key, "generated-app-summary-inline-skill", StringComparison.OrdinalIgnoreCase))
        {
            return !InlineSkillInstructionsContain(existingCapability.ConfigurationJson, "summarizing a generated application or runnable deliverable")
                   || !InlineSkillInstructionsContain(existingCapability.ConfigurationJson, "Do not read or cite artifacts/baseline")
                   || !InlineSkillInstructionsContain(existingCapability.ConfigurationJson, "explicit distinction between proven facts, inferences, and gaps");
        }

        if (string.Equals(seededCapability.Key, "architecture-source-rag", StringComparison.OrdinalIgnoreCase))
        {
            return !existingCapability.ConfigurationJson.Contains("\"tools\"", StringComparison.OrdinalIgnoreCase)
                   || !existingCapability.ConfigurationJson.Contains("src/CanDoItAll.AgentFramework.Sandbox/Components/Pages", StringComparison.OrdinalIgnoreCase);
        }

        if (string.Equals(seededCapability.Key, "workspace-source-rag", StringComparison.OrdinalIgnoreCase))
        {
            return !existingCapability.ConfigurationJson.Contains(".playwright-mcp", StringComparison.OrdinalIgnoreCase)
                   || !existingCapability.ConfigurationJson.Contains("process-runs", StringComparison.OrdinalIgnoreCase)
                   || !existingCapability.ConfigurationJson.Contains("\"data\"", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static bool ShouldRefreshVersionedManagedCapabilityFromSeed(CapabilityCatalogItem existingCapability, CapabilityCatalogItem seededCapability)
    {
        if (!seededCapability.IsBuiltIn)
        {
            return false;
        }

        if (!TryGetManagedSeedVersion(seededCapability.ConfigurationJson, out var seededVersion))
        {
            return false;
        }

        if (!TryGetManagedSeedVersion(existingCapability.ConfigurationJson, out var existingVersion))
        {
            return true;
        }

        if (!string.Equals(existingVersion, seededVersion, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (ManagedCapabilitySeedMetadata.TryReadCapabilityVersion(
                seededCapability.ConfigurationJson,
                out var seededCapabilityVersion) &&
            (!ManagedCapabilitySeedMetadata.TryReadCapabilityVersion(
                 existingCapability.ConfigurationJson,
                 out var existingCapabilityVersion) ||
             existingCapabilityVersion != seededCapabilityVersion))
        {
            return true;
        }

        return !string.Equals(existingCapability.Name, seededCapability.Name, StringComparison.Ordinal) ||
               !string.Equals(existingCapability.Description, seededCapability.Description, StringComparison.Ordinal) ||
               !string.Equals(existingCapability.EndpointOrPath, seededCapability.EndpointOrPath, StringComparison.Ordinal) ||
               !string.Equals(existingCapability.ProofNotes, seededCapability.ProofNotes, StringComparison.Ordinal) ||
               existingCapability.IsBuiltIn != seededCapability.IsBuiltIn;
    }

    private static CapabilityCatalogItem RefreshManagedCapabilityFromSeed(CapabilityCatalogItem existingCapability, CapabilityCatalogItem seededCapability)
    {
        return existingCapability with
        {
            Kind = seededCapability.Kind,
            Key = seededCapability.Key,
            Name = seededCapability.Name,
            Description = seededCapability.Description,
            EndpointOrPath = seededCapability.EndpointOrPath,
            ConfigurationJson = seededCapability.ConfigurationJson,
            ProofNotes = seededCapability.ProofNotes,
            IsBuiltIn = seededCapability.IsBuiltIn,
            Tags = ResolveCapabilityTags(seededCapability)
        };
    }

    private readonly record struct CapabilityAssignmentPolicy(
        Guid CapabilityId,
        string CapabilityKey,
        CapabilityKind Kind);

    private static IReadOnlyList<string> ResolveProviderTags(ProviderProfile provider)
    {
        var tags = provider.Tags?.Where(item => !string.IsNullOrWhiteSpace(item)).ToList() ?? [];
        if (tags.Count == 0)
        {
            tags.Add(provider.Kind == ProviderKind.Ollama ? "ollama" : "openai");
            tags.Add(provider.BaseUrl.Contains("127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
                     provider.BaseUrl.Contains("localhost", StringComparison.OrdinalIgnoreCase)
                ? "local"
                : provider.BaseUrl.Contains("192.168.", StringComparison.OrdinalIgnoreCase) ||
                  provider.BaseUrl.Contains("ollama", StringComparison.OrdinalIgnoreCase)
                    ? "remote"
                    : "cloud");
            tags.Add(provider.Purpose == ProviderProfilePurpose.ImageGeneration ? "image" : "chat");
            if (provider.Purpose == ProviderProfilePurpose.ImageGeneration)
            {
                tags.Add("image-generation");
            }
            else
            {
                tags.Add(provider.Transport == ProviderTransportKind.Responses ? "responses" : "chat-completions");
            }

            if (provider.Name.Contains("fallback", StringComparison.OrdinalIgnoreCase) ||
                provider.Notes.Contains("fallback", StringComparison.OrdinalIgnoreCase))
            {
                tags.Add("fallback");
            }
        }

        return NormalizeTags(tags);
    }

    private static IReadOnlyList<string> ResolveCapabilityTags(CapabilityCatalogItem capability)
    {
        var tags = capability.Tags?.Where(item => !string.IsNullOrWhiteSpace(item)).ToList() ?? [];
        if (tags.Count == 0)
        {
            tags.Add(capability.Kind switch
            {
                CapabilityKind.McpServer => "mcp",
                CapabilityKind.Skill => "skill",
                CapabilityKind.Tool => "tool",
                CapabilityKind.Rag => "rag",
                CapabilityKind.Memory => "memory",
                CapabilityKind.AiContext => "context",
                _ => capability.Kind.ToString().ToLowerInvariant()
            });

            if (capability.Key.StartsWith("workspace-", StringComparison.OrdinalIgnoreCase))
            {
                tags.Add("workspace");
            }

            if (capability.Key.Contains("dotnet", StringComparison.OrdinalIgnoreCase) ||
                capability.Name.Contains(".NET", StringComparison.OrdinalIgnoreCase))
            {
                tags.Add("dotnet");
            }

            if (capability.Key.Contains("blazor", StringComparison.OrdinalIgnoreCase))
            {
                tags.Add("blazor");
            }

            if (capability.Key.Contains("playwright", StringComparison.OrdinalIgnoreCase) ||
                capability.Key.Contains("browser", StringComparison.OrdinalIgnoreCase))
            {
                tags.Add("browser");
            }

            if (capability.IsBuiltIn)
            {
                tags.Add("built-in");
            }
        }

        return NormalizeTags(tags);
    }

    private static IReadOnlyList<string> NormalizeTags(IEnumerable<string>? tags)
    {
        return tags?
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim().TrimStart('#').ToLowerInvariant())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToList()
            ?? [];
    }

    private static bool InlineSkillInstructionsContain(string configurationJson, string expectedPhrase)
    {
        if (string.IsNullOrWhiteSpace(configurationJson) || string.IsNullOrWhiteSpace(expectedPhrase))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(configurationJson);
            if (document.RootElement.TryGetProperty("inlineSkill", out var inlineSkillElement) &&
                inlineSkillElement.TryGetProperty("instructions", out var instructionsElement))
            {
                var instructions = instructionsElement.GetString();
                return !string.IsNullOrWhiteSpace(instructions) &&
                       instructions.Contains(expectedPhrase, StringComparison.OrdinalIgnoreCase);
            }
        }
        catch (JsonException)
        {
        }

        return configurationJson.Contains(expectedPhrase, StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<CapabilityCatalogItem> RemoveRetiredCapabilities(
        IReadOnlyList<CapabilityCatalogItem> capabilities,
        IReadOnlyList<CapabilityCatalogItem> seededCapabilities)
    {
        var seededCapabilityKeys = seededCapabilities
            .Select(capability => capability.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return capabilities
            .Where(capability => !LegacyMemoryCapabilityPolicy.IsRetired(capability.Kind) &&
                                 !IsRetiredSandboxRegisteredSkillCapability(capability) &&
                                 !IsRetiredProjectStructureCatalogTool(capability) &&
                                 !IsRetiredBuiltInInlineSkillCapability(capability, seededCapabilityKeys))
            .OrderBy(item => item.Kind)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<AgentDefinition> RemoveUnavailableAgentCapabilities(
        IReadOnlyList<AgentDefinition> agents,
        IReadOnlyList<CapabilityCatalogItem> capabilities)
    {
        var availableCapabilityIds = capabilities
            .Select(item => item.Id)
            .ToHashSet();

        return agents
            .Select(agent =>
            {
                var filteredCapabilities = agent.Capabilities
                    .Where(item => availableCapabilityIds.Contains(item.CapabilityId))
                    .OrderBy(item => item.Kind)
                    .ThenBy(item => item.CapabilityKey, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                return filteredCapabilities.Count == agent.Capabilities.Count
                    ? agent
                    : agent with
                    {
                        Capabilities = filteredCapabilities
                    };
            })
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsRetiredSandboxRegisteredSkillCapability(CapabilityCatalogItem capability)
    {
        if (capability.Kind != CapabilityKind.Skill)
        {
            return false;
        }

        if (string.Equals(capability.Key, "workspace-delivery-skill", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(capability.Name, "Workspace Delivery Skill", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(capability.Key, "candoitall-bundle-workflow", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(capability.Name, "Bundle Workflow Skill", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(capability.EndpointOrPath) &&
            capability.EndpointOrPath.Contains(RetiredSandboxAssemblyName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(capability.ConfigurationJson) &&
            capability.ConfigurationJson.Contains("WorkspaceDeliverySkill", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return TryReadConfigurationString(capability.ConfigurationJson, "registeredSkillServiceType", out var serviceTypeName) &&
               IsRetiredSandboxRegisteredSkillServiceType(serviceTypeName);
    }

    private static bool IsRetiredBuiltInInlineSkillCapability(
        CapabilityCatalogItem capability,
        IReadOnlySet<string> seededCapabilityKeys)
    {
        return capability.Kind == CapabilityKind.Skill &&
               capability.IsBuiltIn &&
               !seededCapabilityKeys.Contains(capability.Key) &&
               !string.IsNullOrWhiteSpace(capability.EndpointOrPath) &&
               capability.EndpointOrPath.StartsWith("inline://", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRetiredProjectStructureCatalogTool(CapabilityCatalogItem capability)
    {
        return capability.Kind == CapabilityKind.Tool &&
               RetiredProjectStructureCatalogToolKeys.Contains(capability.Key);
    }

    private static bool IsRetiredSandboxRegisteredSkillServiceType(string? serviceTypeName)
    {
        return !string.IsNullOrWhiteSpace(serviceTypeName) &&
               serviceTypeName.Contains(RetiredSandboxAssemblyName, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryReadConfigurationString(
        string? configurationJson,
        string propertyName,
        out string value)
    {
        value = string.Empty;
        if (string.IsNullOrWhiteSpace(configurationJson))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(configurationJson);
            if (!document.RootElement.TryGetProperty(propertyName, out var valueElement) ||
                valueElement.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            value = valueElement.GetString() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(value);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static IReadOnlyList<AgentMemoryRecord> MergeMemory(
        IReadOnlyList<AgentMemoryRecord> existingMemory,
        IReadOnlyList<AgentMemoryRecord> seededMemory,
        IReadOnlyDictionary<Guid, Guid> agentIdMap)
    {
        var merged = existingMemory.ToList();
        foreach (var memory in seededMemory)
        {
            var targetAgentId = agentIdMap.TryGetValue(memory.AgentId, out var mappedAgentId) ? mappedAgentId : memory.AgentId;
            if (merged.Any(item => item.AgentId == targetAgentId && string.Equals(item.Title, memory.Title, StringComparison.OrdinalIgnoreCase) && string.Equals(item.Source, memory.Source, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            merged.Add(memory with { AgentId = targetAgentId });
        }

        return merged.OrderBy(item => item.AgentId).ThenByDescending(item => item.Importance).ThenBy(item => item.Title, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static IReadOnlyList<AgentTeamDefinition> NormalizeAgentTeams(
        IReadOnlyList<AgentTeamDefinition>? existingTeams,
        IReadOnlyList<AgentDefinition> activeAgents)
    {
        var agentNamesById = activeAgents
            .ToDictionary(item => item.Id, item => item.Name);
        var activeAgentIds = agentNamesById.Keys.ToHashSet();
        return (existingTeams ?? [])
            .Where(item => item is not null && item.Id != Guid.Empty && !string.IsNullOrWhiteSpace(item.Name))
            .GroupBy(item => item.Id)
            .Select(group => group.Last())
            .Select(team => team with
            {
                Name = team.Name.Trim(),
                Description = (team.Description ?? string.Empty).Trim(),
                Icon = AgentTeamIconCatalog.Normalize(team.Icon),
                AgentIds = (team.AgentIds ?? [])
                    .Where(item => item != Guid.Empty && activeAgentIds.Contains(item))
                    .Distinct()
                    .OrderBy(item => agentNamesById.TryGetValue(item, out var name) ? name : string.Empty, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(item => item)
                    .ToList()
            })
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Id)
            .ToList();
    }

    private static IReadOnlyList<AgentTeamDefinition> MergeAgentTeams(
        IReadOnlyList<AgentTeamDefinition>? existingTeams,
        IReadOnlyList<AgentTeamDefinition> seededTeams,
        IReadOnlyDictionary<Guid, Guid> agentIdMap,
        IReadOnlyList<AgentDefinition> activeAgents)
    {
        var merged = (existingTeams ?? []).ToList();
        foreach (var seededTeam in seededTeams.Select(team => RemapSeedTeam(team, agentIdMap)))
        {
            var match = merged.FirstOrDefault(item => item.Id == seededTeam.Id)
                ?? merged.FirstOrDefault(item => string.Equals(item.Name, seededTeam.Name, StringComparison.OrdinalIgnoreCase));

            if (match is null)
            {
                merged.Add(seededTeam);
                continue;
            }

            ReplaceTeamById(
                merged,
                match.Id,
                seededTeam with
                {
                    Id = match.Id,
                    CreatedAtUtc = match.CreatedAtUtc,
                    UpdatedAtUtc = seededTeam.UpdatedAtUtc
                });
        }

        return NormalizeAgentTeams(merged, activeAgents);
    }

    private static AgentTeamDefinition RemapSeedTeam(
        AgentTeamDefinition team,
        IReadOnlyDictionary<Guid, Guid> agentIdMap)
    {
        return team with
        {
            AgentIds = team.AgentIds
                .Select(agentId => agentIdMap.TryGetValue(agentId, out var mappedAgentId) ? mappedAgentId : agentId)
                .ToList()
        };
    }

    private static void ReplaceTeamById(
        List<AgentTeamDefinition> teams,
        Guid id,
        AgentTeamDefinition replacement)
    {
        var index = teams.FindIndex(item => item.Id == id);
        if (index >= 0)
        {
            teams[index] = replacement;
        }
    }

    private static IReadOnlyList<ChatSessionRecord> NormalizeChatSessions(
        IReadOnlyList<ChatSessionRecord>? sessions,
        IReadOnlyDictionary<Guid, ExecutionRunRecord> latestRunBySessionId)
    {
        return (sessions ?? [])
            .Select(session => session with
            {
                Messages = session.Messages ?? [],
                Compatibility = latestRunBySessionId.ContainsKey(session.Id)
                    ? null
                    : ChatSessionRuntimeCompatibilityRecord.Create(
                        session.Compatibility?.RuntimeSessionKey,
                        session.Compatibility?.SerializedSessionStateJson,
                        session.Compatibility?.PendingApprovals,
                        session.Compatibility?.AutoApprovePendingToolCalls ?? false),
                LatestExecutionRunId = latestRunBySessionId.TryGetValue(session.Id, out var latestRun)
                    ? latestRun.Id
                    : session.LatestExecutionRunId
            })
            .OrderByDescending(session => session.UpdatedAtUtc)
            .ToList();
    }

    private static IReadOnlyList<ExecutionRunRecord> NormalizeExecutionRuns(IReadOnlyList<ExecutionRunRecord>? runs)
    {
        return (runs ?? [])
            .Select(run => run with
            {
                MetadataJson = string.IsNullOrWhiteSpace(run.MetadataJson) ? "{}" : run.MetadataJson,
                PendingApprovals = run.PendingApprovals ?? [],
                Revision = run.Revision <= 0 ? 1L : run.Revision
            })
            .OrderByDescending(run => run.UpdatedAtUtc)
            .ToList();
    }

    private static IReadOnlyList<ExecutionRunRecord> CreateLegacySessionCompatibilityRuns(
        IReadOnlyList<ChatSessionRecord>? sessions,
        IReadOnlyList<ExecutionRunRecord> normalizedRuns)
    {
        var existingSessionIds = normalizedRuns
            .Where(run => run.ChatSessionId.HasValue)
            .Select(run => run.ChatSessionId!.Value)
            .ToHashSet();

        return (sessions ?? [])
            .Where(session => !existingSessionIds.Contains(session.Id) && (session.Compatibility?.PendingApprovals.Count ?? 0) > 0)
            .Select(session =>
            {
                var compatibility = session.Compatibility!;
                return new ExecutionRunRecord(
                    Id: CreateDeterministicGuid($"legacy-session-run|{session.Id:N}"),
                    AgentId: session.AgentId,
                    ChatSessionId: session.Id,
                    Title: string.IsNullOrWhiteSpace(session.Title) ? "Approval continuation" : session.Title,
                    SourceKind: "chat-session",
                    SourceId: session.Id.ToString("N"),
                    CorrelationId: string.Empty,
                    CausationId: string.Empty,
                    RequestedBy: "legacy-chat-session",
                    RequestedByKind: "compatibility",
                    MetadataJson: "{}",
                    InputSummary: session.Messages.LastOrDefault(item => item.Role == ChatMessageRole.User)?.Content ?? string.Empty,
                    ResultSummary: $"Awaiting approval for {compatibility.PendingApprovals.Count} tool request(s).",
                    ProviderName: string.Empty,
                    Model: string.Empty,
                    State: ExecutionState.WaitingOnTool,
                    Outcome: null,
                    CreatedAtUtc: session.CreatedAtUtc,
                    UpdatedAtUtc: session.UpdatedAtUtc,
                    StartedAtUtc: session.CreatedAtUtc,
                    CompletedAtUtc: null,
                    RuntimeSessionKey: compatibility.RuntimeSessionKey,
                    SerializedSessionStateJson: compatibility.SerializedSessionStateJson,
                    PendingApprovals: compatibility.PendingApprovals,
                    AutoApprovePendingToolCalls: compatibility.AutoApprovePendingToolCalls,
                    ProcessRunId: string.Empty,
                    ProcessStepId: string.Empty,
                    SchedulerRunId: string.Empty,
                    MessageId: string.Empty);
            })
            .ToList();
    }

    private static IReadOnlyDictionary<Guid, ExecutionRunRecord> BuildLatestRunBySessionId(
        IReadOnlyList<ExecutionRunRecord> normalizedRuns)
    {
        return normalizedRuns
            .Where(run => run.ChatSessionId.HasValue)
            .GroupBy(run => run.ChatSessionId!.Value)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(run => run.UpdatedAtUtc)
                    .ThenByDescending(run => run.CreatedAtUtc)
                    .First());
    }

    private static Guid CreateDeterministicGuid(string key)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return new Guid(hash[..16]);
    }

    private static IReadOnlyList<ExecutionApprovalRecord> NormalizeExecutionApprovals(IReadOnlyList<ExecutionApprovalRecord>? approvals)
    {
        return (approvals ?? [])
            .OrderByDescending(item => item.DecidedAtUtc ?? item.RequestedAtUtc)
            .ThenBy(item => item.ApprovalId, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<ExecutionArtifactRecord> NormalizeExecutionArtifacts(IReadOnlyList<ExecutionArtifactRecord>? artifacts)
    {
        return (artifacts ?? [])
            .OrderByDescending(item => item.CreatedAtUtc)
            .ThenBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<ExecutionWorkflowCheckpointRecord> NormalizeExecutionWorkflowCheckpoints(IReadOnlyList<ExecutionWorkflowCheckpointRecord>? checkpoints)
    {
        return (checkpoints ?? [])
            .Select(checkpoint => checkpoint with
            {
                PendingApprovalIds = checkpoint.PendingApprovalIds ?? []
            })
            .OrderByDescending(item => item.CapturedAtUtc)
            .ThenBy(item => item.WorkflowCheckpointId, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<ToolExecutionReceiptRecord> NormalizeToolExecutionReceipts(IReadOnlyList<ToolExecutionReceiptRecord>? receipts)
    {
        return (receipts ?? [])
            .OrderByDescending(item => item.CompletedAtUtc)
            .ThenBy(item => item.ToolName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void ReplaceById<T>(List<T> items, Guid id, T replacement)
    {
        var index = items.FindIndex(item => ExtractId(item) == id);
        if (index >= 0)
        {
            items[index] = replacement;
        }
    }

    private static Guid ExtractId<T>(T item)
    {
        return item switch
        {
            ProviderProfile provider => provider.Id,
            CapabilityCatalogItem capability => capability.Id,
            AgentDefinition agent => agent.Id,
            _ => throw new InvalidOperationException($"Unsupported merge item type '{typeof(T).Name}'.")
        };
    }

    private sealed record MergeResult<T>(IReadOnlyList<T> Items, IReadOnlyDictionary<Guid, Guid> IdMap);
}
