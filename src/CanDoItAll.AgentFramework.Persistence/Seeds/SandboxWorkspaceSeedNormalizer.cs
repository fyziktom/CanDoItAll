using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
        "OpenAI chat completions"
    };

    private static readonly HashSet<string> ManagedSeriousDeliveryTemplateKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "portfolio-architect",
        "delivery-qa-observer",
        "programming-workspace-analyst",
        "code-review-lead",
        "ui-review-lead",
        "security-reviewer",
        "release-readiness-manager",
        "hr-staffing-manager",
        "research-deep-dive-analyst",
        "dotnet-solution-architect",
        "dotnet-application-developer",
        "dotnet-qa-review-lead",
        "javascript-solution-architect",
        "javascript-application-developer",
        "javascript-qa-review-lead",
        "business-strategist",
        "financial-strategist",
        "marketing-specialist"
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
        var agents = MergeAgents(catalog.Agents, seeded.Agents, providers.IdMap, capabilities.IdMap);
        var memory = MergeMemory(catalog.Memory, seeded.Memory, agents.IdMap);
        var activeCapabilities = RemoveRetiredCapabilities(capabilities.Items);
        var activeAgents = RemoveUnavailableAgentCapabilities(agents.Items, activeCapabilities);

        return catalog with
        {
            Version = seeded.Version,
            Providers = providers.Items,
            Capabilities = activeCapabilities,
            Agents = activeAgents,
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
            ExecutionRuns = normalizedRuns,
            ExecutionApprovals = NormalizeExecutionApprovals(executionState.ExecutionApprovals),
            ExecutionArtifacts = NormalizeExecutionArtifacts(executionState.ExecutionArtifacts),
            ExecutionWorkflowCheckpoints = NormalizeExecutionWorkflowCheckpoints(executionState.ExecutionWorkflowCheckpoints),
            ToolExecutionReceipts = NormalizeToolExecutionReceipts(executionState.ToolExecutionReceipts)
        };
    }

    private static MergeResult<ProviderProfile> MergeProviders(IReadOnlyList<ProviderProfile> existingProviders, IReadOnlyList<ProviderProfile> seededProviders)
    {
        var merged = existingProviders.Select(ProviderProfileService.NormalizeImportedProfile).ToList();
        var idMap = new Dictionary<Guid, Guid>();

        foreach (var seededProvider in seededProviders.Select(ProviderProfileService.NormalizeImportedProfile))
        {
            var match = merged.FirstOrDefault(item => item.Id == seededProvider.Id)
                ?? merged.FirstOrDefault(item => string.Equals(item.Name, seededProvider.Name, StringComparison.OrdinalIgnoreCase) && item.Kind == seededProvider.Kind);

            if (match is null)
            {
                merged.Add(seededProvider);
                idMap[seededProvider.Id] = seededProvider.Id;
                continue;
            }

            idMap[seededProvider.Id] = match.Id;
            var isManagedSeedOpenAiProvider = IsManagedSeedOpenAiProvider(match, seededProvider);
            var mergedProvider = match with
            {
                ApiKeyEnvironmentVariable = string.IsNullOrWhiteSpace(match.ApiKeyEnvironmentVariable) ? seededProvider.ApiKeyEnvironmentVariable : match.ApiKeyEnvironmentVariable,
                DefaultModel = isManagedSeedOpenAiProvider || string.IsNullOrWhiteSpace(match.DefaultModel)
                    ? seededProvider.DefaultModel
                    : match.DefaultModel,
                SupportsStreaming = match.SupportsStreaming || seededProvider.SupportsStreaming,
                SupportsTools = match.SupportsTools || seededProvider.SupportsTools,
                PreferFrameworkManagedChatHistory = match.PreferFrameworkManagedChatHistory || seededProvider.PreferFrameworkManagedChatHistory,
                SupportsBackgroundResponses = match.SupportsBackgroundResponses || seededProvider.SupportsBackgroundResponses,
                ConfigurationJson = string.IsNullOrWhiteSpace(match.ConfigurationJson) ? seededProvider.ConfigurationJson : match.ConfigurationJson,
                Notes = string.IsNullOrWhiteSpace(match.Notes) ? seededProvider.Notes : match.Notes,
                SuggestedModels = isManagedSeedOpenAiProvider
                    ? seededProvider.SuggestedModels.Concat(match.SuggestedModels).Where(item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
                    : match.SuggestedModels.Concat(seededProvider.SuggestedModels).Where(item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
            };

            ReplaceById(
                merged,
                match.Id,
                ProviderProfileService.NormalizeImportedProfile(mergedProvider));
        }

        return new MergeResult<ProviderProfile>(merged.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToList(), idMap);
    }

    private static bool IsManagedSeedOpenAiProvider(ProviderProfile existingProvider, ProviderProfile seededProvider)
    {
        return existingProvider.Kind is ProviderKind.OpenAi or ProviderKind.AzureOpenAi &&
               seededProvider.Kind is ProviderKind.OpenAi or ProviderKind.AzureOpenAi &&
               ManagedSeedOpenAiProviderNames.Contains(existingProvider.Name) &&
               ManagedSeedOpenAiProviderNames.Contains(seededProvider.Name);
    }

    private static MergeResult<CapabilityCatalogItem> MergeCapabilities(IReadOnlyList<CapabilityCatalogItem> existingCapabilities, IReadOnlyList<CapabilityCatalogItem> seededCapabilities)
    {
        var merged = existingCapabilities.ToList();
        var idMap = new Dictionary<Guid, Guid>();

        foreach (var seededCapability in seededCapabilities)
        {
            var match = merged.FirstOrDefault(item => item.Id == seededCapability.Id)
                ?? merged.FirstOrDefault(item => item.Kind == seededCapability.Kind && string.Equals(item.Key, seededCapability.Key, StringComparison.OrdinalIgnoreCase));

            if (match is null)
            {
                merged.Add(seededCapability);
                idMap[seededCapability.Id] = seededCapability.Id;
                continue;
            }

            idMap[seededCapability.Id] = match.Id;
            var mergedCapability = match with
            {
                Name = string.IsNullOrWhiteSpace(match.Name) ? seededCapability.Name : match.Name,
                Description = string.IsNullOrWhiteSpace(match.Description) ? seededCapability.Description : match.Description,
                EndpointOrPath = string.IsNullOrWhiteSpace(match.EndpointOrPath) ? seededCapability.EndpointOrPath : match.EndpointOrPath,
                ConfigurationJson = string.IsNullOrWhiteSpace(match.ConfigurationJson) ? seededCapability.ConfigurationJson : match.ConfigurationJson,
                ProofNotes = string.IsNullOrWhiteSpace(match.ProofNotes) ? seededCapability.ProofNotes : match.ProofNotes,
                IsBuiltIn = match.IsBuiltIn || seededCapability.IsBuiltIn
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
        IReadOnlyDictionary<Guid, Guid> providerIdMap,
        IReadOnlyDictionary<Guid, Guid> capabilityIdMap)
    {
        var merged = existingAgents.ToList();
        var idMap = new Dictionary<Guid, Guid>();

        foreach (var seededAgent in seededAgents.Select(agent => RemapSeedAgent(agent, providerIdMap, capabilityIdMap)))
        {
            var match = merged.FirstOrDefault(item => item.Id == seededAgent.Id)
                ?? (!string.IsNullOrWhiteSpace(seededAgent.TemplateKey)
                    ? merged.FirstOrDefault(item => string.Equals(item.TemplateKey, seededAgent.TemplateKey, StringComparison.OrdinalIgnoreCase))
                    : null)
                ?? merged.FirstOrDefault(item => string.Equals(item.Name, seededAgent.Name, StringComparison.OrdinalIgnoreCase) && item.IsTemplate == seededAgent.IsTemplate);

            if (match is null)
            {
                merged.Add(seededAgent);
                idMap[seededAgent.Id] = seededAgent.Id;
                continue;
            }

            idMap[seededAgent.Id] = match.Id;
            var mergedAgent = match with
            {
                RoleTitle = string.IsNullOrWhiteSpace(match.RoleTitle) ? seededAgent.RoleTitle : match.RoleTitle,
                Summary = string.IsNullOrWhiteSpace(match.Summary) ? seededAgent.Summary : match.Summary,
                Instructions = string.IsNullOrWhiteSpace(match.Instructions) ? seededAgent.Instructions : match.Instructions,
                ProviderProfileId = match.ProviderProfileId ?? seededAgent.ProviderProfileId,
                Model = string.IsNullOrWhiteSpace(match.Model) ? seededAgent.Model : match.Model,
                RequirePerServiceCallChatHistoryPersistence = match.RequirePerServiceCallChatHistoryPersistence || seededAgent.RequirePerServiceCallChatHistoryPersistence,
                EnableBackgroundResponses = match.EnableBackgroundResponses || seededAgent.EnableBackgroundResponses,
                ConfigurationJson = string.IsNullOrWhiteSpace(match.ConfigurationJson) ? seededAgent.ConfigurationJson : match.ConfigurationJson,
                Capabilities = MergeAgentCapabilities(match.Capabilities, seededAgent.Capabilities),
                Tags = match.Tags.Concat(seededAgent.Tags).Where(item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
            };

            if (ShouldRefreshManagedAgentFromSeed(match, seededAgent))
            {
                mergedAgent = RefreshManagedAgentFromSeed(mergedAgent, seededAgent);
            }

            ReplaceById(merged, match.Id, mergedAgent);
        }

        return new MergeResult<AgentDefinition>(merged.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToList(), idMap);
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

    private static bool ShouldRefreshManagedAgentFromSeed(AgentDefinition existingAgent, AgentDefinition seededAgent)
    {
        if (!TryGetManagedSeedVersion(seededAgent, out var managedSeedVersion))
        {
            return false;
        }

        if (TryGetManagedSeedVersion(existingAgent, out var currentSeedVersion) &&
            string.Equals(currentSeedVersion, managedSeedVersion, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return ManagedSeriousDeliveryTemplateKeys.Contains(seededAgent.TemplateKey);
    }

    private static AgentDefinition RefreshManagedAgentFromSeed(AgentDefinition existingAgent, AgentDefinition seededAgent)
    {
        return existingAgent with
        {
            RoleTitle = seededAgent.RoleTitle,
            Summary = seededAgent.Summary,
            Instructions = seededAgent.Instructions,
            ProviderProfileId = seededAgent.ProviderProfileId,
            Model = seededAgent.Model,
            Workload = seededAgent.Workload,
            ChatHistoryMode = seededAgent.ChatHistoryMode,
            Temperature = seededAgent.Temperature,
            RequirePerServiceCallChatHistoryPersistence = seededAgent.RequirePerServiceCallChatHistoryPersistence,
            EnableBackgroundResponses = seededAgent.EnableBackgroundResponses,
            ConfigurationJson = seededAgent.ConfigurationJson,
            Permissions = seededAgent.Permissions,
            Capabilities = seededAgent.Capabilities,
            Tags = seededAgent.Tags
        };
    }

    private static bool TryGetManagedSeedVersion(AgentDefinition agent, out string version)
    {
        version = string.Empty;
        if (string.IsNullOrWhiteSpace(agent.ConfigurationJson))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(agent.ConfigurationJson);
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
        if (string.Equals(seededCapability.Key, "blazor-calculator-inline-skill", StringComparison.OrdinalIgnoreCase))
        {
            return !existingCapability.ConfigurationJson.Contains("do not pre-create a nested `SimpleCalculatorApp` project directory", StringComparison.OrdinalIgnoreCase)
                   || !existingCapability.ConfigurationJson.Contains("The page must include an explicit clear or reset path", StringComparison.OrdinalIgnoreCase)
                   || !existingCapability.ConfigurationJson.Contains("Add targeted validation for the calculator behavior", StringComparison.OrdinalIgnoreCase);
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
            return !InlineSkillInstructionsContain(existingCapability.ConfigurationJson, "If the project structure or attached step materials name a concrete output directory")
                   || !InlineSkillInstructionsContain(existingCapability.ConfigurationJson, "external-target/<drive>/...")
                   || !InlineSkillInstructionsContain(existingCapability.ConfigurationJson, "do not scaffold a parallel copy under `artifacts/...`, `output/...`, or another generated implementation folder")
                   || !InlineSkillInstructionsContain(existingCapability.ConfigurationJson, "scaffold directly into it instead of adding an extra nested");
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

    private static CapabilityCatalogItem RefreshManagedCapabilityFromSeed(CapabilityCatalogItem existingCapability, CapabilityCatalogItem seededCapability)
    {
        return existingCapability with
        {
            Name = seededCapability.Name,
            Description = seededCapability.Description,
            EndpointOrPath = seededCapability.EndpointOrPath,
            ConfigurationJson = seededCapability.ConfigurationJson,
            ProofNotes = seededCapability.ProofNotes,
            IsBuiltIn = seededCapability.IsBuiltIn
        };
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

    private static IReadOnlyList<CapabilityCatalogItem> RemoveRetiredCapabilities(IReadOnlyList<CapabilityCatalogItem> capabilities)
    {
        return capabilities
            .Where(capability => !IsRetiredSandboxRegisteredSkillCapability(capability))
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
