using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Persistence;

internal static class SandboxWorkspaceSeedBuilder
{
    private const string LatestVersion = "3.0";
    private const string SeriousDeliveryManagedSeedVersion = "2026-08-agent-template-teams-v71";
    private static readonly DateTimeOffset SeedTimestamp = new(2026, 4, 10, 0, 0, 0, TimeSpan.Zero);

    private static readonly IReadOnlyList<string> OpenAiSuggestedModels =
    [
        ManagedSeedProviderFallbacks.OpenAiDefaultModel,
        OpenAiModelIds.Gpt56,
        OpenAiModelIds.Gpt56Luna,
        OpenAiModelIds.Gpt56Terra,
        OpenAiModelIds.Gpt56Sol,
        "gpt-5.4",
        "gpt-4.1-mini",
        "gpt-4.1"
    ];

    private static readonly IReadOnlyList<string> OpenAiImageSuggestedModels =
    [
        OpenAiModelIds.GptImage2
    ];

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    internal static SandboxWorkspaceDocument Build(
        string? capabilityTemplatePackRoot = null,
        string? agentTemplatePackRoot = null)
    {
        var now = SeedTimestamp;

        var openAiProviderId = CreateStableGuid("providers/openai-default");
        var openAiChatProviderId = CreateStableGuid("providers/openai-chat-completions");
        var openAiImageProviderId = CreateStableGuid("providers/openai-image-generation");
        var localComfyUiFluxProviderId = CreateStableGuid("providers/comfyui-flux-local");
        var ollamaProviderId = CreateStableGuid("providers/ollama-local");
        var localOllamaProviderId = CreateStableGuid("providers/ollama-local-default");

        var capabilityPack = new CapabilityTemplatePackLoader(capabilityTemplatePackRoot).Load();
        var capabilities = CapabilityTemplateSeedMaterializer.MaterializeDefaultCapabilities(capabilityPack);
        var sessionId = CreateStableGuid("sessions/integration-target-summary");

        var providers = new List<ProviderProfile>
        {
            new(
                openAiProviderId,
                "OpenAI default",
                ProviderKind.OpenAi,
                "https://api.openai.com/v1",
                "OPENAI_API_KEY",
                ManagedSeedProviderFallbacks.OpenAiDefaultModel,
                ProviderTransportKind.Responses,
                true,
                true,
                true,
                false,
                true,
                CreateOpenAiProviderConfigurationJson("service-managed"),
                "Responses profile for hosted routes, DevUI, and background-response scenarios.",
                "Not checked",
                null,
                OpenAiSuggestedModels)
            {
                Tags = ["openai", "cloud", "responses", "chat"],
                ModelPrices = ProviderPricingDefaults.CreateDefaultPrices(
                    ProviderKind.OpenAi,
                    ManagedSeedProviderFallbacks.OpenAiDefaultModel)
            },
            new(
                openAiChatProviderId,
                "OpenAI chat completions",
                ProviderKind.OpenAi,
                "https://api.openai.com/v1",
                "OPENAI_API_KEY",
                ManagedSeedProviderFallbacks.OpenAiDefaultModel,
                ProviderTransportKind.ChatCompletions,
                true,
                true,
                true,
                true,
                false,
                SerializeConfiguration(new
                {
                    history = "framework-managed",
                    timeoutSeconds = ManagedSeedProviderFallbacks.OpenAiDefaultTimeoutSeconds
                }),
                "Chat-completions profile for local history, approvals, compaction, and workload-specific skill runs.",
                "Not checked",
                null,
                OpenAiSuggestedModels)
            {
                Tags = ["openai", "cloud", "chat-completions", "chat"],
                ModelPrices = ProviderPricingDefaults.CreateDefaultPrices(
                    ProviderKind.OpenAi,
                    ManagedSeedProviderFallbacks.OpenAiDefaultModel)
            },
            new(
                openAiImageProviderId,
                "OpenAI image generation",
                ProviderKind.OpenAi,
                "https://api.openai.com/v1",
                "OPENAI_API_KEY",
                OpenAiModelIds.GptImage2,
                ProviderTransportKind.Responses,
                true,
                false,
                false,
                false,
                false,
                CreateOpenAiImageProviderConfigurationJson(),
                "Image-generation profile for OpenAI Images API workflows. Defaults to GPT Image 2; runtime image tools should still require explicit agent permission.",
                "Not checked",
                null,
                OpenAiImageSuggestedModels,
                ProviderProfilePurpose.ImageGeneration)
            {
                Tags = ["openai", "cloud", "image-generation", "image"]
            },
            new(
                localComfyUiFluxProviderId,
                ComfyUiFluxProviderDefaults.ProviderName,
                ProviderKind.ComfyUi,
                ComfyUiFluxProviderDefaults.DefaultBaseUrl,
                string.Empty,
                ComfyUiFluxProviderDefaults.DefaultModel,
                ProviderTransportKind.ChatCompletions,
                true,
                false,
                false,
                false,
                false,
                ComfyUiFluxProviderDefaults.CreateConfigurationJson(),
                "Local ComfyUI Flux image-generation provider for developer workstations exposing the ComfyUI HTTP API.",
                "Not checked",
                null,
                ComfyUiFluxProviderDefaults.SuggestedModels,
                ProviderProfilePurpose.ImageGeneration)
            {
                IsPrivateProvider = true,
                Tags = ["comfyui", "flux", "image", "image-generation", "local"]
            },
            new(
                localOllamaProviderId,
                "Local Ollama",
                ProviderKind.Ollama,
                "http://127.0.0.1:11434",
                string.Empty,
                "llama3.1",
                ProviderTransportKind.ChatCompletions,
                true,
                true,
                true,
                true,
                false,
                SerializeConfiguration(new
                {
                    history = "framework-managed",
                    local = true,
                    timeoutSeconds = 45,
                    modelParameters = new
                    {
                        numPredict = AgentProviderModelParameterPolicy.DefaultOllamaMaxOutputTokens
                    }
                }),
                "Local Ollama provider for developer workstations running the standard Ollama API endpoint.",
                "Not checked",
                null,
                ["llama3.1", "qwen3.5:9b", "phi4-16k", "mistral-nemo"])
            {
                Tags = ["ollama", "local", "chat"]
            },
            new(
                ollamaProviderId,
                "Remote Ollama",
                ProviderKind.Ollama,
                "http://192.168.10.132:11434",
                string.Empty,
                "qwen3.5:9b",
                ProviderTransportKind.ChatCompletions,
                true,
                true,
                true,
                true,
                false,
                SerializeConfiguration(new
                {
                    history = "framework-managed",
                    modelParameters = new
                    {
                        numPredict = AgentProviderModelParameterPolicy.DefaultOllamaMaxOutputTokens
                    }
                }),
                "Targets the remote host validated during the latest Ollama repair and networking checks.",
                "Not checked",
                null,
                ["qwen3.5:9b", "gemma3-12b-128k:latest", "deepseek-r1:8b-32k", "qwen3.5:2b", "phi4-16k", "mistral-nemo"])
            {
                Tags = ["ollama", "remote", "fallback", "chat"]
            }
        };

        var providerIdsByKey = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase)
        {
            ["openai-default"] = openAiProviderId,
            ["openai-chat-completions"] = openAiChatProviderId,
            ["openai-image-generation"] = openAiImageProviderId,
            ["ollama-local"] = localOllamaProviderId,
            ["ollama-remote"] = ollamaProviderId,
            ["managed-seed-openai-default"] = openAiProviderId
        };
        var agentSeed = BuildAgentSeedFromTemplates(
            now,
            providerIdsByKey,
            providers,
            capabilities,
            agentTemplatePackRoot);
        var agentIdsByTemplateKey = agentSeed.Agents.ToDictionary(
            item => item.TemplateKey,
            item => item.Id,
            StringComparer.OrdinalIgnoreCase);
        var architectAgentId = RequireAgentId(agentIdsByTemplateKey, "portfolio-architect");
        var qaAgentId = RequireAgentId(agentIdsByTemplateKey, "delivery-qa-observer");
        var programmingAgentId = RequireAgentId(agentIdsByTemplateKey, "programming-workspace-analyst");
        var hrStaffingManagerAgentId = RequireAgentId(agentIdsByTemplateKey, "hr-staffing-manager");
        var spreadsheetAgentId = RequireAgentId(agentIdsByTemplateKey, "spreadsheet-analyst");
        var mailAgentId = RequireAgentId(agentIdsByTemplateKey, "mail-triage-analyst");
        var researchAgentId = RequireAgentId(agentIdsByTemplateKey, "research-deep-dive-analyst");

        return new SandboxWorkspaceDocument(
            LatestVersion,
            agentSeed.Agents,
            providers,
            capabilities,
            [
                new ChatSessionRecord(
                    sessionId,
                    architectAgentId,
                    "Integration target summary",
                    now,
                    now,
                    string.Empty,
                    null,
                    [
                        new ChatMessageRecord(CreateStableGuid("messages/integration-target-summary/user"), ChatMessageRole.User, "Summarize the integration target for this sandbox.", now, 10),
                        new ChatMessageRecord(CreateStableGuid("messages/integration-target-summary/assistant"), ChatMessageRole.Assistant, "The sandbox should stay standalone today while aligning with CanDoItAll identity, provider, automation, assignment, and rights seams for later integration.", now, 28)
                    ],
                    [])
            ],
            [new ExecutionLogEntry(CreateStableGuid("execution-log/integration-target-summary"), architectAgentId, sessionId, now, ExecutionState.Completed, "Seeded run", "Created the initial sandbox summary conversation.")],
            [new AgentRunMetric(CreateStableGuid("metrics/integration-target-summary"), architectAgentId, sessionId, now, RunOutcome.Succeeded, "OpenAI default", ManagedSeedProviderFallbacks.OpenAiDefaultModel, 420, 10, 28, 0)],
            [
                new AgentMemoryRecord(CreateStableGuid("memory/future-candoitall-seam"), architectAgentId, MemoryKind.Architecture, "Future CanDoItAll seam", "Align with CRM and HR agent identity, project-node assignments, provider profiles, automation telemetry, and rights masks.", "seed", 5, "{}", now),
                new AgentMemoryRecord(CreateStableGuid("memory/proof-discipline"), qaAgentId, MemoryKind.FollowUp, "Proof discipline", "Reopen any phase when browser proof or dependency gates are weak.", "seed", 4, "{}", now),
                new AgentMemoryRecord(CreateStableGuid("memory/framework-first-coding"), programmingAgentId, MemoryKind.Context, "Framework-first coding", "Prefer Microsoft Agent Framework primitives before adding wrapper-specific coding behavior.", "seed", 4, "{}", now),
                new AgentMemoryRecord(CreateStableGuid("memory/staffing-grounding"), hrStaffingManagerAgentId, MemoryKind.Context, "Staffing grounding", "Prefer currently assigned project resources and bound AI agents when they satisfy the role facts. Escalate unresolved gaps instead of inventing a confident-looking match.", "seed", 4, "{}", now),
                new AgentMemoryRecord(CreateStableGuid("memory/spreadsheet-review-checklist"), spreadsheetAgentId, MemoryKind.Context, "Spreadsheet review checklist", "Explain key metrics, anomalies, and any rows that deserve follow-up.", "seed", 3, "{}", now),
                new AgentMemoryRecord(CreateStableGuid("memory/reply-style"), mailAgentId, MemoryKind.Preference, "Reply style", "Keep drafted replies concise, direct, and explicit about the next action.", "seed", 3, "{}", now),
                new AgentMemoryRecord(CreateStableGuid("memory/evidence-first-claims"), researchAgentId, MemoryKind.Context, "Evidence-first claims", "Separate proven repo evidence from inference and capture any validation gap honestly.", "seed", 5, "{}", now)
            ])
        {
            AgentTeams = agentSeed.Teams
        };
    }

    private static AgentTemplateSeed BuildAgentSeedFromTemplates(
        DateTimeOffset now,
        IReadOnlyDictionary<string, Guid> providerIdsByKey,
        IReadOnlyList<ProviderProfile> providers,
        IReadOnlyList<CapabilityCatalogItem> capabilities,
        string? agentTemplatePackRoot)
    {
        var pack = new AgentTemplatePackLoader(agentTemplatePackRoot).Load();
        var seedVersion = string.IsNullOrWhiteSpace(pack.Manifest.SeedVersion)
            ? SeriousDeliveryManagedSeedVersion
            : pack.Manifest.SeedVersion.Trim();
        var assignmentValidation = CapabilityTemplateSeedAssignmentValidator.ValidateAgentAssignments(pack, capabilities);
        if (!assignmentValidation.IsValid)
        {
            throw new CapabilityTemplatePackValidationException(assignmentValidation.Issues);
        }

        var capabilitiesByKey = capabilities.ToDictionary(item => item.Key, StringComparer.OrdinalIgnoreCase);
        var providersById = providers.ToDictionary(item => item.Id);
        var agents = new List<AgentDefinition>();

        foreach (var member in pack.Teams.SelectMany(team => team.MemberTemplates))
        {
            var settings = member.Settings;
            var id = CreateStableGuid(RequireTemplateValue(settings.StableIdKey, member.Key, "stableIdKey"));
            var templateKey = RequireTemplateValue(settings.TemplateKey, member.Key, "templateKey");
            var providerProfileId = ResolveProviderProfileId(settings.ProviderProfileKey, providerIdsByKey, member.Key);
            var model = NormalizeTemplateText(settings.Model);
            EnsureTemplateThinkingEffortSupported(
                settings,
                member.Key,
                providerProfileId,
                model,
                providersById);
            var configurationJson = BuildAgentTemplateConfigurationJson(settings, providerIdsByKey, seedVersion);
            var assignments = ResolveCapabilityAssignments(member, capabilitiesByKey);

            agents.Add(new AgentDefinition(
                id,
                RequireTemplateValue(settings.Name, member.Key, "name"),
                RequireTemplateValue(settings.RoleTitle, member.Key, "roleTitle"),
                RequireTemplateValue(settings.Summary, member.Key, "summary"),
                RequireTemplateValue(member.Instructions, member.Key, "instructions"),
                ParseEnumOrDefault(settings.Status, AgentLifecycleStatus.Active),
                providerProfileId,
                model,
                ParseEnumOrDefault(settings.Workload, AgentWorkloadKind.General),
                ParseEnumOrDefault(settings.ChatHistoryMode, AgentChatHistoryMode.FrameworkManaged),
                settings.Temperature,
                settings.RequirePerServiceCallChatHistoryPersistence,
                settings.EnableBackgroundResponses,
                configurationJson,
                settings.IsTemplate,
                templateKey,
                BuildPermissions(settings.Permissions),
                assignments,
                settings.Tags
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Select(item => item.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                now,
                now)
            {
                AvatarImageUrl = AgentAvatarImageCatalog.RequireBundledAvatarUrl(
                    settings.AvatarImageUrl,
                    member.Key)
            });
        }

        EnsureUniqueTemplateKeys(agents);
        var agentsByTemplateKey = agents.ToDictionary(item => item.TemplateKey, item => item.Id, StringComparer.OrdinalIgnoreCase);
        var teams = pack.Teams
            .Select(team => new AgentTeamDefinition(
                CreateStableGuid(RequireTemplateValue(team.StableIdKey, team.Key, "stableIdKey")),
                RequireTemplateValue(team.Name, team.Key, "name"),
                NormalizeTemplateText(team.Description),
                team.MemberTemplates
                    .Select(member => RequireAgentId(agentsByTemplateKey, member.Settings.TemplateKey))
                    .ToList(),
                now,
                now,
                ResolveSeedTeamIcon(team.Key)))
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new AgentTemplateSeed(
            agents.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToList(),
            teams);
    }

    private static string BuildAgentTemplateConfigurationJson(
        AgentTemplateSettings settings,
        IReadOnlyDictionary<string, Guid> providerIdsByKey,
        string seedVersion)
    {
        var configuration = settings.Configuration.ToDictionary(
            item => item.Key,
            item => ConvertSeedConfigurationValue(item.Value),
            StringComparer.OrdinalIgnoreCase);
        configuration["managedSeedVersion"] = seedVersion;
        var configurationJson = SerializeConfiguration(configuration);

        if (settings.Access.ProjectStructure is { } projectStructure)
        {
            configurationJson = AgentProjectStructureAccessMetadata.Write(
                configurationJson,
                new AgentProjectStructureAccessSettings
                {
                    CanRead = projectStructure.CanRead,
                    CanWrite = projectStructure.CanWrite,
                    CanWriteNonTaskStructure = projectStructure.CanWriteNonTaskStructure,
                    CanWriteTasks = projectStructure.CanWriteTasks,
                    CanCreateProjects = projectStructure.CanCreateProjects,
                    CanCreateSubprojects = projectStructure.CanCreateSubprojects,
                    AllowAllProjects = projectStructure.AllowAllProjects,
                    AllowedProjectIds = projectStructure.AllowedProjectIds
                });
        }

        if (settings.Access.Processes is { } processes)
        {
            configurationJson = AgentProcessAccessMetadata.Write(
                configurationJson,
                new AgentProcessAccessSettings
                {
                    CanRead = processes.CanRead,
                    CanWrite = processes.CanWrite,
                    AllowAllDefinitions = processes.AllowAllDefinitions,
                    AllowedDefinitionIds = processes.AllowedDefinitionIds
                });
        }

        if (settings.Access.WorkspaceTools is { } workspaceTools)
        {
            var profile = ParseEnumOrDefault(workspaceTools.Profile, AgentWorkspaceToolProfileKind.Custom);
            var access = AgentWorkspaceToolAccessProfiles.CreateSettings(profile);
            ApplyWorkspaceToolTemplateOverrides(access, workspaceTools);
            access.CanReadStorage = workspaceTools.CanReadStorage;
            access.CanWriteStorage = workspaceTools.CanWriteStorage;
            access.AllowAllStorageCatalogs = workspaceTools.AllowAllStorageCatalogs;
            access.AllowedStorageCatalogIds = workspaceTools.AllowedStorageCatalogIds;
            access.AllowedExternalTargetAliases = workspaceTools.AllowedExternalTargetAliases;
            configurationJson = AgentWorkspaceToolAccessMetadata.Write(configurationJson, access);
        }

        if (settings.Access.ImageGeneration is { } imageGeneration)
        {
            configurationJson = AgentImageGenerationAccessMetadata.Write(
                configurationJson,
                new AgentImageGenerationAccessSettings
                {
                    CanGenerateImages = imageGeneration.CanGenerateImages,
                    PreferredProviderProfileId = ResolveOptionalProviderProfileId(
                        imageGeneration.PreferredProviderProfileKey,
                        providerIdsByKey),
                    DefaultModel = imageGeneration.DefaultModel,
                    CanStoreImagesAsProjectAssets = imageGeneration.CanStoreImagesAsProjectAssets
                });
        }

        return AgentThinkingEffortPolicy.WriteAgentOverride(
            configurationJson,
            settings.ReasoningEffort);
    }

    private static void ApplyWorkspaceToolTemplateOverrides(
        AgentWorkspaceToolAccessSettings access,
        AgentTemplateWorkspaceToolAccess workspaceTools)
    {
        access.CanReadFiles = workspaceTools.CanReadFiles ?? access.CanReadFiles;
        access.CanWriteFiles = workspaceTools.CanWriteFiles ?? access.CanWriteFiles;
        access.CanManageWorkspacePaths = workspaceTools.CanManageWorkspacePaths ?? access.CanManageWorkspacePaths;
        access.CanRunValidationCommands = workspaceTools.CanRunValidationCommands ?? access.CanRunValidationCommands;
        access.CanScaffoldProjects = workspaceTools.CanScaffoldProjects ?? access.CanScaffoldProjects;
        access.CanRunLocalScripts = workspaceTools.CanRunLocalScripts ?? access.CanRunLocalScripts;
        access.CanTransformArtifacts = workspaceTools.CanTransformArtifacts ?? access.CanTransformArtifacts;
    }

    private static string ResolveSeedTeamIcon(string? teamKey)
    {
        return (teamKey ?? string.Empty).Trim() switch
        {
            "delivery-platform" => "rocket_launch",
            "dotnet-delivery" => "code",
            "javascript-delivery" => "integration_instructions",
            "business-and-research" => "science",
            "visual-automation-templates" => "visibility",
            _ => AgentTeamIconCatalog.DefaultIcon
        };
    }

    private static IReadOnlyList<AgentCapabilityAssignment> ResolveCapabilityAssignments(
        AgentTemplateMember member,
        IReadOnlyDictionary<string, CapabilityCatalogItem> capabilitiesByKey)
    {
        return member.Skills.CapabilityKeys
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item =>
            {
                var capabilityKey = item.Trim();
                if (!capabilitiesByKey.TryGetValue(capabilityKey, out var capability))
                {
                    throw new InvalidOperationException(
                        $"Agent template '{member.Key}' references missing capability '{capabilityKey}'.");
                }

                return CreateAssignment(capability.Id, capability.Key, capability.Kind);
            })
            .ToList();
    }

    private static AgentPermissionsPolicy BuildPermissions(AgentTemplatePermissions template)
    {
        var defaults = AgentPermissionsPolicy.Default;
        return new AgentPermissionsPolicy(
            template.CanUseTools ?? defaults.CanUseTools,
            template.CanAskOtherAgents ?? defaults.CanAskOtherAgents,
            template.CanEscalateToHuman ?? defaults.CanEscalateToHuman,
            template.CanObserveOtherAgents ?? defaults.CanObserveOtherAgents,
            template.CanScheduleWork ?? defaults.CanScheduleWork,
            template.RequiresApprovalForExternalCalls ?? defaults.RequiresApprovalForExternalCalls,
            template.AutoApproveExternalCallsByDefault ?? defaults.AutoApproveExternalCallsByDefault,
            []);
    }

    private static Guid? ResolveProviderProfileId(
        string providerProfileKey,
        IReadOnlyDictionary<string, Guid> providerIdsByKey,
        string templateKey)
    {
        if (string.IsNullOrWhiteSpace(providerProfileKey))
        {
            return null;
        }

        return providerIdsByKey.TryGetValue(providerProfileKey.Trim(), out var providerId)
            ? providerId
            : throw new InvalidOperationException(
                $"Agent template '{templateKey}' references missing provider profile key '{providerProfileKey}'.");
    }

    private static Guid? ResolveOptionalProviderProfileId(
        string providerProfileKey,
        IReadOnlyDictionary<string, Guid> providerIdsByKey)
    {
        if (string.IsNullOrWhiteSpace(providerProfileKey))
        {
            return null;
        }

        return providerIdsByKey.TryGetValue(providerProfileKey.Trim(), out var providerId)
            ? providerId
            : throw new InvalidOperationException(
                $"Agent template references missing provider profile key '{providerProfileKey}'.");
    }

    private static void EnsureTemplateThinkingEffortSupported(
        AgentTemplateSettings settings,
        string templateKey,
        Guid? providerProfileId,
        string model,
        IReadOnlyDictionary<Guid, ProviderProfile> providersById)
    {
        if (settings.ReasoningEffort is not { } reasoningEffort)
        {
            return;
        }

        if (!providerProfileId.HasValue ||
            !providersById.TryGetValue(providerProfileId.Value, out var provider))
        {
            throw new InvalidOperationException(
                $"Agent template '{templateKey}' defines thinking effort '{AgentThinkingEffortPolicy.FormatEffort(reasoningEffort)}' without a resolved provider profile.");
        }

        var effectiveModel = string.IsNullOrWhiteSpace(model)
            ? provider.DefaultModel
            : model;
        AgentThinkingEffortPolicy.EnsureOverrideSupported(
            provider,
            effectiveModel,
            reasoningEffort,
            $"agent template '{templateKey}'");
    }

    private static Guid RequireAgentId(
        IReadOnlyDictionary<string, Guid> agentIdsByTemplateKey,
        string templateKey)
    {
        return agentIdsByTemplateKey.TryGetValue(templateKey, out var agentId)
            ? agentId
            : throw new InvalidOperationException($"Seed agent template '{templateKey}' was not materialized.");
    }

    private static void EnsureUniqueTemplateKeys(IReadOnlyList<AgentDefinition> agents)
    {
        var duplicateTemplateKey = agents
            .GroupBy(item => item.TemplateKey, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateTemplateKey is not null)
        {
            throw new InvalidOperationException(
                $"Agent template pack contains duplicate template key '{duplicateTemplateKey.Key}'.");
        }
    }

    private static TEnum ParseEnumOrDefault<TEnum>(string value, TEnum defaultValue)
        where TEnum : struct, Enum
    {
        return !string.IsNullOrWhiteSpace(value) &&
               Enum.TryParse<TEnum>(value.Trim(), ignoreCase: true, out var parsed)
            ? parsed
            : defaultValue;
    }

    private static string RequireTemplateValue(string value, string templateKey, string label)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"Agent template '{templateKey}' is missing required setting '{label}'.")
            : value.Trim();
    }

    private static string NormalizeTemplateText(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private sealed record AgentTemplateSeed(
        IReadOnlyList<AgentDefinition> Agents,
        IReadOnlyList<AgentTeamDefinition> Teams);

    private static AgentCapabilityAssignment CreateAssignment(
        Guid capabilityId,
        string capabilityKey,
        CapabilityKind kind,
        CapabilityProofStatus status = CapabilityProofStatus.NotRun,
        string notes = "")
    {
        return new AgentCapabilityAssignment(capabilityId, capabilityKey, kind, status, null, notes);
    }

    private static string CreateOpenAiProviderConfigurationJson(string history)
    {
        return AgentThinkingEffortPolicy.WriteProviderDefault(
            SerializeConfiguration(new
            {
                history,
                timeoutSeconds = ManagedSeedProviderFallbacks.OpenAiDefaultTimeoutSeconds
            }),
            AgentReasoningEffortLevel.Medium);
    }

    private static string CreateOpenAiImageProviderConfigurationJson()
    {
        return SerializeConfiguration(new
        {
            endpointFamily = "images",
            defaultQuality = "low",
            defaultSize = "1024x1024",
            defaultOutputFormat = "png"
        });
    }

    private static Guid CreateStableGuid(string key)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        Span<byte> buffer = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(buffer);
        buffer[6] = (byte)((buffer[6] & 0x0F) | 0x50);
        buffer[8] = (byte)((buffer[8] & 0x3F) | 0x80);
        return new Guid(buffer);
    }

    private static object? ConvertSeedConfigurationValue(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number when value.TryGetInt64(out var longValue) => longValue,
            JsonValueKind.Number when value.TryGetDecimal(out var decimalValue) => decimalValue,
            JsonValueKind.Array => value.EnumerateArray().Select(ConvertSeedConfigurationValue).ToList(),
            JsonValueKind.Object => value.EnumerateObject().ToDictionary(
                property => property.Name,
                property => ConvertSeedConfigurationValue(property.Value),
                StringComparer.OrdinalIgnoreCase),
            _ => value.ToString()
        };
    }

    private static string SerializeConfiguration<T>(T value)
    {
        return JsonSerializer.Serialize(value, SerializerOptions);
    }
}
